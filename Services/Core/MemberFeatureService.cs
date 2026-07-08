using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace ClubGear.Services.Core;

public sealed class MemberFeatureService : IMemberFeatureService
{
    private const string MemberNumberSection = "Members";
    private const string MemberNumberPrefixKey = "MemberNumberPrefix";
    private const string MemberNumberSuffixKey = "MemberNumberSuffix";
    private const string MemberNumberNextNumberKey = "MemberNumberNextNumber";
    private const string MemberNumberPaddingKey = "MemberNumberPadding";
    private const string DefaultMemberNumberPrefix = "M-";
    private const int DefaultMemberNumberPadding = 4;
    private const int MaxMemberNumberGenerationAttempts = 3;

    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService _auditLogService;
    private readonly IMemberMetadataService _memberMetadataService;

    public MemberFeatureService(ApplicationDbContext db, IAuditLogService auditLogService)
        : this(db, auditLogService, new MemberMetadataService())
    {
    }

    public MemberFeatureService(ApplicationDbContext db, IAuditLogService auditLogService, IMemberMetadataService memberMetadataService)
    {
        _db = db;
        _auditLogService = auditLogService;
        _memberMetadataService = memberMetadataService;
    }

    public async Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Member> query = _db.Members
            .AsNoTracking()
            .Include(m => m.Addresses)
            .Include(m => m.MembershipType)
            .Include(m => m.MetadataValues).ThenInclude(v => v.Field)
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(m =>
                (m.MemberNumber != null && m.MemberNumber.Contains(search)) ||
                m.FirstName.Contains(search) ||
                m.LastName.Contains(search) ||
                (m.Email != null && m.Email.Contains(search)) ||
                (m.OauthID != null && m.OauthID.Contains(search)) ||
                (m.KeycloakUsername != null && m.KeycloakUsername.Contains(search)));
        }

        return await query.ToListAsync(cancellationToken);
    }

    public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _db.Members
            .AsNoTracking()
            .Include(m => m.Addresses)
            .Include(m => m.MembershipType)
            .Include(m => m.MetadataValues).ThenInclude(v => v.Field)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MemberReferenceOption>> SearchForReferenceAsync(string? query, int limit = 10, CancellationToken cancellationToken = default)
    {
        var terms = (query ?? string.Empty)
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var candidates = await _db.Members
            .AsNoTracking()
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberNumber })
            .ToListAsync(cancellationToken);

        // Every typed term (e.g. "Brenne" and "Maximilian" from "Brenne Maximilian") must match
        // some field, but each term can match any of first name / last name / member number - this
        // isn't translatable to SQL reliably across providers, so it's filtered in memory.
        var matches = terms.Length == 0
            ? candidates
            : candidates.Where(m => terms.All(term =>
                m.FirstName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                m.LastName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (m.MemberNumber != null && m.MemberNumber.Contains(term, StringComparison.OrdinalIgnoreCase))));

        return matches
            .Take(limit)
            .Select(m => new MemberReferenceOption(m.Id, $"{m.LastName}, {m.FirstName}, {m.MemberNumber}"))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, string>> GetReferenceLabelsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        return await _db.Members
            .AsNoTracking()
            .Where(m => idList.Contains(m.Id))
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.MemberNumber })
            .ToDictionaryAsync(m => m.Id, m => $"{m.LastName}, {m.FirstName}, {m.MemberNumber}", cancellationToken);
    }

    public async Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Members
            .AsNoTracking()
            .Include(m => m.MembershipType)
            .Where(m => !m.IsActive)
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
    {
        member.Addresses = NormalizeAddresses(member.Addresses);

        var autoGenerateNumber = string.IsNullOrWhiteSpace(member.MemberNumber);

        var membershipType = await ResolveMembershipTypeAsync(member.MembershipTypeId, cancellationToken);
        member.MembershipTypeId = membershipType?.Id;

        var metadataOutcome = await ValidateMetadataAsync(membershipType, member.MetadataInputs, selfId: null, cancellationToken);
        member.MetadataValues = BuildMetadataValues(membershipType, metadataOutcome);

        _db.Members.Add(member);

        if (autoGenerateNumber)
        {
            // A blank MemberNumber is auto-assigned from config. Two overlapping Create requests
            // can both read the same "next" candidate before either commits, so a bounded retry
            // loop regenerates a fresh candidate (re-scanning committed Members) whenever the
            // insert fails on the unique MemberNumber index, instead of surfacing a raw duplicate
            // key crash to the user.
            for (var attempt = 1; attempt <= MaxMemberNumberGenerationAttempts; attempt++)
            {
                member.MemberNumber = await GenerateNextMemberNumberAsync(cancellationToken);

                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (DbUpdateException) when (attempt < MaxMemberNumberGenerationAttempts)
                {
                    // Another concurrent Create committed the same candidate first; loop again -
                    // GenerateNextMemberNumberAsync re-reads Members fresh, so the now-committed
                    // colliding number is skipped this time.
                }
                catch (DbUpdateException)
                {
                    throw new ClubGear.Services.BusinessLogicException(
                        "Es konnte keine eindeutige Mitgliedsnummer automatisch vergeben werden. Bitte erneut speichern.");
                }
            }
        }
        else
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var afterSnapshot = await LoadMemberForAuditAsync(member.Id, cancellationToken);

        await _auditLogService.LogChangeAsync(
            action: "Members.Create",
            before: null,
            after: afterSnapshot,
            actor: actor,
            source: "mvc",
            targetType: nameof(Member),
            targetId: member.Id.ToString(),
            cancellationToken: cancellationToken);
    }

    private Task<Member?> LoadMemberForAuditAsync(int memberId, CancellationToken cancellationToken)
    {
        return _db.Members
            .AsNoTracking()
            .Include(m => m.Addresses)
            .Include(m => m.MembershipType)
            .Include(m => m.MetadataValues).ThenInclude(v => v.Field)
            .FirstOrDefaultAsync(m => m.Id == memberId, cancellationToken);
    }

    private async Task<MembershipType?> ResolveMembershipTypeAsync(int? membershipTypeId, CancellationToken cancellationToken)
    {
        if (!membershipTypeId.HasValue)
        {
            return null;
        }

        var membershipType = await _db.MembershipTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == membershipTypeId.Value, cancellationToken);

        if (membershipType is null || !membershipType.IsActive)
        {
            throw new ClubGear.Services.ValidationException("Die ausgewaehlte Mitgliedsart ist ungueltig oder nicht aktiv.");
        }

        return membershipType;
    }

    private async Task<MemberMetadataValidationOutcome> ValidateMetadataAsync(
        MembershipType? membershipType,
        IReadOnlyDictionary<string, string?>? postedValues,
        int? selfId,
        CancellationToken cancellationToken)
    {
        if (membershipType is null)
        {
            return MemberMetadataValidationOutcome.Ok(new Dictionary<string, string?>());
        }

        IReadOnlyCollection<int>? existingMemberIds = null;
        MemberReferenceIntegrityContext? referenceContext = null;
        if (membershipType.Fields.Any(f => f.FieldType == MemberMetadataFieldType.MemberReference))
        {
            existingMemberIds = await _db.Members.Select(m => m.Id).ToListAsync(cancellationToken);

            // Single-level hierarchy integrity: project the current parent/sub-member graph as
            // ids only. A member that carries any MemberReference value is a sub-member; the value
            // it points at is a parent. These feed the self/grandchild/2-cycle checks in
            // MemberMetadataService.ValidateAndEncode (04_design.md §2.3).
            var referenceLinks = await _db.MemberMetadataValues
                .Where(v => v.Field != null
                    && v.Field.FieldType == MemberMetadataFieldType.MemberReference
                    && v.Value != null)
                .Select(v => new { v.MemberId, v.Value })
                .ToListAsync(cancellationToken);

            var existingSubMemberIds = new HashSet<int>();
            var existingParentIds = new HashSet<int>();
            foreach (var link in referenceLinks)
            {
                existingSubMemberIds.Add(link.MemberId);
                if (int.TryParse(link.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetId))
                {
                    existingParentIds.Add(targetId);
                }
            }

            referenceContext = new MemberReferenceIntegrityContext(selfId, existingSubMemberIds, existingParentIds);
        }

        var outcome = _memberMetadataService.ValidateAndEncode(
            membershipType.Fields,
            postedValues ?? new Dictionary<string, string?>(),
            existingMemberIds,
            referenceContext);

        if (!outcome.IsValid)
        {
            throw new ClubGear.Services.ValidationException(string.Join(" ", outcome.Errors));
        }

        return outcome;
    }

    private static List<MemberMetadataValue> BuildMetadataValues(MembershipType? membershipType, MemberMetadataValidationOutcome outcome)
    {
        var values = new List<MemberMetadataValue>();
        if (membershipType is null)
        {
            return values;
        }

        var now = DateTime.UtcNow;
        foreach (var field in membershipType.Fields)
        {
            if (outcome.EncodedValuesByFieldKey.TryGetValue(field.Key, out var encoded) && encoded is not null)
            {
                values.Add(new MemberMetadataValue
                {
                    FieldId = field.Id,
                    Value = encoded,
                    UpdatedAtUtc = now
                });
            }
        }

        return values;
    }

    private async Task UpsertMetadataValuesAsync(
        int memberId,
        MembershipType? membershipType,
        MemberMetadataValidationOutcome outcome,
        CancellationToken cancellationToken)
    {
        var existingValues = await _db.MemberMetadataValues
            .Where(v => v.MemberId == memberId)
            .ToListAsync(cancellationToken);

        var currentFieldIds = membershipType?.Fields.Select(f => f.Id).ToHashSet() ?? new HashSet<int>();

        // Fields no longer belonging to the member's (possibly changed) current type are stale.
        foreach (var stale in existingValues.Where(v => !currentFieldIds.Contains(v.FieldId)))
        {
            _db.MemberMetadataValues.Remove(stale);
        }

        if (membershipType is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var field in membershipType.Fields)
        {
            outcome.EncodedValuesByFieldKey.TryGetValue(field.Key, out var encoded);
            var existingValue = existingValues.FirstOrDefault(v => v.FieldId == field.Id);

            if (encoded is null)
            {
                if (existingValue is not null)
                {
                    _db.MemberMetadataValues.Remove(existingValue);
                }

                continue;
            }

            if (existingValue is null)
            {
                _db.MemberMetadataValues.Add(new MemberMetadataValue
                {
                    MemberId = memberId,
                    FieldId = field.Id,
                    Value = encoded,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                existingValue.Value = encoded;
                existingValue.UpdatedAtUtc = now;
            }
        }
    }

    private async Task<string> GenerateNextMemberNumberAsync(CancellationToken cancellationToken)
    {
        var config = await _db.SystemConfigEntries
            .AsNoTracking()
            .Where(e => e.Section == MemberNumberSection)
            .ToDictionaryAsync(e => e.Name, e => e.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var prefix = GetConfigValue(config, MemberNumberPrefixKey, DefaultMemberNumberPrefix);
        var suffix = GetConfigValue(config, MemberNumberSuffixKey, string.Empty);
        var padding = GetConfiguredNumber(config, MemberNumberPaddingKey, DefaultMemberNumberPadding, min: 0, max: 20);
        var configuredNextNumber = GetConfiguredNumber(config, MemberNumberNextNumberKey, 1, min: 1, max: int.MaxValue);
        var nextFromExistingNumbers = await GetNextNumberFromExistingMembersAsync(prefix, suffix, cancellationToken);
        var nextNumber = Math.Max(configuredNextNumber, nextFromExistingNumbers);

        string candidate;
        do
        {
            candidate = FormatMemberNumber(prefix, nextNumber, padding, suffix);
            nextNumber++;
        }
        while (await _db.Members.AnyAsync(m => m.MemberNumber == candidate, cancellationToken));

        await UpsertSystemConfigAsync(
            MemberNumberSection,
            MemberNumberNextNumberKey,
            nextNumber.ToString(CultureInfo.InvariantCulture),
            "Naechste automatisch zu vergebende Mitgliedsnummer",
            cancellationToken);

        return candidate;
    }

    private async Task<int> GetNextNumberFromExistingMembersAsync(string prefix, string suffix, CancellationToken cancellationToken)
    {
        var memberNumbers = await _db.Members
            .AsNoTracking()
            .Select(m => m.MemberNumber)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var memberNumber in memberNumbers)
        {
            if (!TryExtractNumber(memberNumber, prefix, suffix, out var number))
            {
                continue;
            }

            maxNumber = Math.Max(maxNumber, number);
        }

        return maxNumber + 1;
    }

    private static string GetConfigValue(IReadOnlyDictionary<string, string> config, string key, string fallback)
    {
        return config.TryGetValue(key, out var value) ? value : fallback;
    }

    private static int GetConfiguredNumber(IReadOnlyDictionary<string, string> config, string key, int fallback, int min, int max)
    {
        if (!config.TryGetValue(key, out var rawValue)
            || !int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return fallback;
        }

        return Math.Clamp(parsed, min, max);
    }

    private static string FormatMemberNumber(string prefix, int number, int padding, string suffix)
    {
        var formattedNumber = padding > 0
            ? number.ToString($"D{padding}", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);

        return $"{prefix}{formattedNumber}{suffix}";
    }

    private static bool TryExtractNumber(string? memberNumber, string prefix, string suffix, out int number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(memberNumber)
            || !memberNumber.StartsWith(prefix, StringComparison.Ordinal)
            || !memberNumber.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var start = prefix.Length;
        var length = memberNumber.Length - prefix.Length - suffix.Length;
        if (length <= 0)
        {
            return false;
        }

        var numericPart = memberNumber.Substring(start, length);
        return int.TryParse(numericPart, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private async Task UpsertSystemConfigAsync(
        string section,
        string name,
        string value,
        string description,
        CancellationToken cancellationToken)
    {
        var entry = await _db.SystemConfigEntries
            .FirstOrDefaultAsync(e => e.Section == section && e.Name == name, cancellationToken);

        if (entry is null)
        {
            _db.SystemConfigEntries.Add(new SystemConfigEntry
            {
                Section = section,
                Name = name,
                Value = value,
                Description = description,
                UpdatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        entry.Value = value;
        entry.Description = description;
        entry.UpdatedAtUtc = DateTime.UtcNow;
    }

    public async Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
    {
        var tracked = await _db.Members
            .Include(m => m.Addresses)
            .FirstOrDefaultAsync(m => m.Id == member.Id, cancellationToken);

        if (tracked is null)
        {
            return MemberMutationStatus.NotFound;
        }

        var before = await LoadMemberForAuditAsync(member.Id, cancellationToken);

        // Resolve + validate before mutating the tracked entity: ValidateMetadataAsync can throw
        // (e.g. missing required field for the new type), and GlobalExceptionMiddleware logs that
        // via IEventLogService on this same scoped DbContext, whose SaveChangesAsync would flush
        // *any* pending changes already applied to `tracked` — silently persisting an invalid,
        // half-updated member instead of rejecting the request.
        var membershipType = await ResolveMembershipTypeAsync(member.MembershipTypeId, cancellationToken);
        var metadataOutcome = await ValidateMetadataAsync(membershipType, member.MetadataInputs, selfId: member.Id, cancellationToken);

        tracked.Title = member.Title;
        tracked.OauthID = member.OauthID;
        tracked.OAuthUserName = member.OAuthUserName;
        tracked.IsVerified = member.IsVerified;
        if (!string.IsNullOrWhiteSpace(member.MemberNumber))
        {
            tracked.MemberNumber = member.MemberNumber;
        }

        tracked.FirstName = member.FirstName;
        tracked.LastName = member.LastName;
        tracked.Email = member.Email;
        tracked.PhoneNumber = member.PhoneNumber;
        tracked.DateOfBirth = member.DateOfBirth;
        tracked.Gender = member.Gender;
        tracked.IsActive = member.IsActive;
        tracked.JoinedAt = member.JoinedAt;
        tracked.Joined = member.Joined;
        tracked.Leaved = member.Leaved;
        tracked.LastUpdated = DateTime.UtcNow;
        tracked.IsDeceased = member.IsDeceased;
        tracked.NotifyViaEmail = member.NotifyViaEmail;
        tracked.NotifyViaMatrix = member.NotifyViaMatrix;
        tracked.DataprivacyAccepted = member.DataprivacyAccepted;
        tracked.NewsletterConsent = member.NewsletterConsent;
        tracked.ProfileImagePath = member.ProfileImagePath;
        tracked.PendingEmail = member.PendingEmail;
        tracked.EmailVerificationToken = member.EmailVerificationToken;
        tracked.EmailVerificationTokenExpiry = member.EmailVerificationTokenExpiry;
        tracked.RentalPayoutOptions = member.RentalPayoutOptions;
        tracked.InitPassword = member.InitPassword;
        tracked.KeycloakUsername = member.KeycloakUsername;
        tracked.MembershipTypeId = membershipType?.Id;

        await UpsertMetadataValuesAsync(tracked.Id, membershipType, metadataOutcome, cancellationToken);

        _db.MemberAddresses.RemoveRange(tracked.Addresses);
        tracked.Addresses = NormalizeAddresses(member.Addresses);

        await _db.SaveChangesAsync(cancellationToken);

        var after = await LoadMemberForAuditAsync(member.Id, cancellationToken);

        await _auditLogService.LogChangeAsync(
            action: "Members.Edit",
            before: before,
            after: after,
            actor: actor,
            source: "mvc",
            targetType: nameof(Member),
            targetId: member.Id.ToString(),
            cancellationToken: cancellationToken);

        return MemberMutationStatus.Success;
    }

    public async Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
    {
        var tracked = await _db.Members.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (tracked is null)
        {
            return MemberMutationStatus.NotFound;
        }

        if (tracked.IsVerified)
        {
            return MemberMutationStatus.Success;
        }

        var before = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        tracked.IsVerified = true;
        tracked.LastUpdated = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogChangeAsync(
            action: "Members.Verify",
            before: before,
            after: tracked,
            actor: actor,
            source: "mvc",
            targetType: nameof(Member),
            targetId: tracked.Id.ToString(),
            cancellationToken: cancellationToken);

        return MemberMutationStatus.Success;
    }

    public async Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
    {
        var member = await _db.Members.FindAsync(new object[] { id }, cancellationToken);
        if (member is null)
        {
            return MemberMutationStatus.NotFound;
        }

        var snapshot = new Member
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            OauthID = member.OauthID,
            OAuthUserName = member.OAuthUserName,
            IsVerified = member.IsVerified,
                Title = member.Title,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Email = member.Email,
            PhoneNumber = member.PhoneNumber,
                DateOfBirth = member.DateOfBirth,
                Gender = member.Gender,
                MembershipTypeId = member.MembershipTypeId,
            IsActive = member.IsActive,
            JoinedAt = member.JoinedAt,
            Joined = member.Joined,
            Leaved = member.Leaved,
            LastUpdated = member.LastUpdated,
            IsDeceased = member.IsDeceased,
            NotifyViaEmail = member.NotifyViaEmail,
            NotifyViaMatrix = member.NotifyViaMatrix,
            DataprivacyAccepted = member.DataprivacyAccepted,
            NewsletterConsent = member.NewsletterConsent,
            ProfileImagePath = member.ProfileImagePath,
            PendingEmail = member.PendingEmail,
            EmailVerificationToken = member.EmailVerificationToken,
            EmailVerificationTokenExpiry = member.EmailVerificationTokenExpiry,
            RentalPayoutOptions = member.RentalPayoutOptions,
            InitPassword = member.InitPassword,
            KeycloakUsername = member.KeycloakUsername,
            ApplicationUserId = member.ApplicationUserId
        };

        _db.Members.Remove(member);
        await _db.SaveChangesAsync(cancellationToken);

        await _auditLogService.LogChangeAsync(
            action: "Members.Delete",
            before: snapshot,
            after: null,
            actor: actor,
            source: "mvc",
            targetType: nameof(Member),
            targetId: snapshot.Id.ToString(),
            cancellationToken: cancellationToken);

        return MemberMutationStatus.Success;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
    {
        if (!hasManagePermission)
        {
            throw new UnauthorizedAccessException("BulkDelete erfordert members.manage-Berechtigung.");
        }

        if (ids.Count == 0)
        {
            return 0;
        }

        var members = await _db.Members
            .Where(m => ids.Contains(m.Id) && !m.IsActive)
            .ToListAsync(cancellationToken);
        if (members.Count == 0)
        {
            return 0;
        }

        var snapshots = members.Select(member => new Member
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            OauthID = member.OauthID,
            OAuthUserName = member.OAuthUserName,
            IsVerified = member.IsVerified,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Email = member.Email,
            PhoneNumber = member.PhoneNumber,
            IsActive = member.IsActive,
            JoinedAt = member.JoinedAt,
            Joined = member.Joined,
            Leaved = member.Leaved,
            LastUpdated = member.LastUpdated,
            IsDeceased = member.IsDeceased,
            MembershipTypeId = member.MembershipTypeId,
            NotifyViaEmail = member.NotifyViaEmail,
            NotifyViaMatrix = member.NotifyViaMatrix,
            DataprivacyAccepted = member.DataprivacyAccepted,
            NewsletterConsent = member.NewsletterConsent,
            ProfileImagePath = member.ProfileImagePath,
            PendingEmail = member.PendingEmail,
            EmailVerificationToken = member.EmailVerificationToken,
            EmailVerificationTokenExpiry = member.EmailVerificationTokenExpiry,
            RentalPayoutOptions = member.RentalPayoutOptions,
            InitPassword = member.InitPassword,
            KeycloakUsername = member.KeycloakUsername,
            ApplicationUserId = member.ApplicationUserId
        }).ToList();

        _db.Members.RemoveRange(members);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var snapshot in snapshots)
        {
            await _auditLogService.LogChangeAsync(
                action: "Members.BulkDelete",
                before: snapshot,
                after: null,
                actor: actor,
                source: "mvc",
                targetType: nameof(Member),
                targetId: snapshot.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        return snapshots.Count;
    }

    public async Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(csvStream);
        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var lineNo = 0;
        var isFirstDataLine = true;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            lineNo++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = line.Split(';');
            if (values.Length == 1)
            {
                values = line.Split(',');
            }

            if (values.Length < 3)
            {
                skipped++;
                errors.Add($"Zeile {lineNo}: Zu wenige Spalten.");
                continue;
            }

            var memberNumber = values[0].Trim();
            var firstName = values[1].Trim();
            var lastName = values[2].Trim();

            if (isFirstDataLine &&
                (memberNumber.Contains("member", StringComparison.OrdinalIgnoreCase)
                 || firstName.Contains("first", StringComparison.OrdinalIgnoreCase)
                 || lastName.Contains("last", StringComparison.OrdinalIgnoreCase)))
            {
                isFirstDataLine = false;
                continue;
            }

            isFirstDataLine = false;

            if (string.IsNullOrWhiteSpace(memberNumber) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                skipped++;
                errors.Add($"Zeile {lineNo}: Pflichtfelder fehlen (MemberNumber, FirstName, LastName).");
                continue;
            }

            var email = values.Length > 3 ? NullIfEmpty(values[3]) : null;
            var phone = values.Length > 4 ? NullIfEmpty(values[4]) : null;
            var isActive = values.Length > 5 ? ParseBoolOrDefault(values[5], true) : true;
            var joinedAt = values.Length > 6 ? ParseDateOrDefault(values[6], DateTime.UtcNow) : DateTime.UtcNow;
            var title = values.Length > 7 ? NullIfEmpty(values[7]) : null;
            var gender = values.Length > 8 ? NullIfEmpty(values[8]) : null;
            var isClub = values.Length > 9 ? ParseBoolOrDefault(values[9], false) : false;
            var clubName = values.Length > 10 ? NullIfEmpty(values[10]) : null;
            var isCompany = values.Length > 11 ? ParseBoolOrDefault(values[11], false) : false;
            var companyName = values.Length > 12 ? NullIfEmpty(values[12]) : null;
            var street = values.Length > 13 ? NullIfEmpty(values[13]) : null;
            var postalCode = values.Length > 14 ? NullIfEmpty(values[14]) : null;
            var city = values.Length > 15 ? NullIfEmpty(values[15]) : null;
            var country = values.Length > 16 ? NullIfEmpty(values[16]) : null;

            // Precedence matches the historical FullName computation: Firma > Verein > Standard.
            var csvMembershipType = await ResolveCsvMembershipTypeAsync(isClub, isCompany, cancellationToken);

            var existing = await _db.Members.FirstOrDefaultAsync(m => m.MemberNumber == memberNumber, cancellationToken);
            if (existing is null)
            {
                var newMember = new Member
                {
                    Title = title,
                    MemberNumber = memberNumber,
                    FirstName = firstName,
                    LastName = lastName,
                    Email = email,
                    PhoneNumber = phone,
                    Gender = gender,
                    MembershipTypeId = csvMembershipType?.Id,
                    MetadataValues = BuildCsvMetadataValues(csvMembershipType, isClub, clubName, isCompany, companyName),
                    IsActive = isActive,
                    JoinedAt = joinedAt,
                    LastUpdated = DateTime.UtcNow
                };

                if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(city) || !string.IsNullOrWhiteSpace(postalCode) || !string.IsNullOrWhiteSpace(country))
                {
                    newMember.Addresses.Add(new MemberAddress
                    {
                        Street = street,
                        PostalCode = postalCode,
                        City = city,
                        Country = country,
                        IsDefault = true
                    });
                }

                _db.Members.Add(newMember);
                await _db.SaveChangesAsync(cancellationToken);

                await _auditLogService.LogChangeAsync(
                    action: "Members.Import.Create",
                    before: null,
                    after: newMember,
                    actor: actor,
                    source: "mvc",
                    targetType: nameof(Member),
                    targetId: newMember.Id.ToString(),
                    cancellationToken: cancellationToken);

                created++;
                continue;
            }

            var before = new Member
            {
                Id = existing.Id,
                MemberNumber = existing.MemberNumber,
                Title = existing.Title,
                OauthID = existing.OauthID,
                OAuthUserName = existing.OAuthUserName,
                IsVerified = existing.IsVerified,
                FirstName = existing.FirstName,
                LastName = existing.LastName,
                Email = existing.Email,
                PhoneNumber = existing.PhoneNumber,
                DateOfBirth = existing.DateOfBirth,
                Gender = existing.Gender,
                MembershipTypeId = existing.MembershipTypeId,
                IsActive = existing.IsActive,
                JoinedAt = existing.JoinedAt,
                Joined = existing.Joined,
                Leaved = existing.Leaved,
                LastUpdated = existing.LastUpdated,
                IsDeceased = existing.IsDeceased,
                NotifyViaEmail = existing.NotifyViaEmail,
                NotifyViaMatrix = existing.NotifyViaMatrix,
                DataprivacyAccepted = existing.DataprivacyAccepted,
                NewsletterConsent = existing.NewsletterConsent,
                ProfileImagePath = existing.ProfileImagePath,
                PendingEmail = existing.PendingEmail,
                EmailVerificationToken = existing.EmailVerificationToken,
                EmailVerificationTokenExpiry = existing.EmailVerificationTokenExpiry,
                RentalPayoutOptions = existing.RentalPayoutOptions,
                InitPassword = existing.InitPassword,
                KeycloakUsername = existing.KeycloakUsername,
                ApplicationUserId = existing.ApplicationUserId
            };

            existing.Title = title;
            existing.FirstName = firstName;
            existing.LastName = lastName;
            existing.Email = email;
            existing.PhoneNumber = phone;
            existing.Gender = gender;
            existing.MembershipTypeId = csvMembershipType?.Id;
            existing.IsActive = isActive;
            existing.JoinedAt = joinedAt;
            existing.LastUpdated = DateTime.UtcNow;

            await UpsertCsvMetadataValueAsync(existing.Id, csvMembershipType, isClub, clubName, isCompany, companyName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(street) || !string.IsNullOrWhiteSpace(city) || !string.IsNullOrWhiteSpace(postalCode) || !string.IsNullOrWhiteSpace(country))
            {
                var defaultAddress = await _db.MemberAddresses.FirstOrDefaultAsync(a => a.MemberId == existing.Id && a.IsDefault, cancellationToken);
                if (defaultAddress is null)
                {
                    defaultAddress = new MemberAddress
                    {
                        MemberId = existing.Id,
                        IsDefault = true
                    };
                    _db.MemberAddresses.Add(defaultAddress);
                }

                defaultAddress.Street = street;
                defaultAddress.PostalCode = postalCode;
                defaultAddress.City = city;
                defaultAddress.Country = country;
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogChangeAsync(
                action: "Members.Import.Update",
                before: before,
                after: existing,
                actor: actor,
                source: "mvc",
                targetType: nameof(Member),
                targetId: existing.Id.ToString(),
                cancellationToken: cancellationToken);

            updated++;
        }

        return new MembersImportResult(created, updated, skipped, errors);
    }

    private async Task<MembershipType?> ResolveCsvMembershipTypeAsync(bool isClub, bool isCompany, CancellationToken cancellationToken)
    {
        // Firma takes precedence over Verein when both legacy flags are set, preserving the
        // historical FullName precedence order; neither flag resolves to Standard.
        var key = isCompany ? "Firma" : isClub ? "Verein" : "Standard";

        return await _db.MembershipTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Key == key, cancellationToken);
    }

    private static List<MemberMetadataValue> BuildCsvMetadataValues(
        MembershipType? membershipType,
        bool isClub,
        string? clubName,
        bool isCompany,
        string? companyName)
    {
        var values = new List<MemberMetadataValue>();
        var (fieldKey, value) = ResolveCsvFieldKeyAndValue(isClub, clubName, isCompany, companyName);
        if (fieldKey is null || membershipType is null)
        {
            return values;
        }

        var field = membershipType.Fields.FirstOrDefault(f => f.Key == fieldKey);
        if (field is not null)
        {
            values.Add(new MemberMetadataValue { FieldId = field.Id, Value = value, UpdatedAtUtc = DateTime.UtcNow });
        }

        return values;
    }

    private async Task UpsertCsvMetadataValueAsync(
        int memberId,
        MembershipType? membershipType,
        bool isClub,
        string? clubName,
        bool isCompany,
        string? companyName,
        CancellationToken cancellationToken)
    {
        var (fieldKey, value) = ResolveCsvFieldKeyAndValue(isClub, clubName, isCompany, companyName);
        if (fieldKey is null || membershipType is null)
        {
            return;
        }

        var field = membershipType.Fields.FirstOrDefault(f => f.Key == fieldKey);
        if (field is null)
        {
            return;
        }

        var existingValue = await _db.MemberMetadataValues
            .FirstOrDefaultAsync(v => v.MemberId == memberId && v.FieldId == field.Id, cancellationToken);

        if (existingValue is null)
        {
            _db.MemberMetadataValues.Add(new MemberMetadataValue
            {
                MemberId = memberId,
                FieldId = field.Id,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existingValue.Value = value;
            existingValue.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static (string? FieldKey, string? Value) ResolveCsvFieldKeyAndValue(
        bool isClub,
        string? clubName,
        bool isCompany,
        string? companyName)
    {
        if (isCompany && !string.IsNullOrWhiteSpace(companyName))
        {
            return ("company_name", companyName);
        }

        if (isClub && !string.IsNullOrWhiteSpace(clubName))
        {
            return ("club_name", clubName);
        }

        return (null, null);
    }

    private static string? NullIfEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool ParseBoolOrDefault(string value, bool fallback)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "1" => true,
            "0" => false,
            "true" => true,
            "false" => false,
            "ja" => true,
            "nein" => false,
            "yes" => true,
            "no" => false,
            _ => fallback
        };
    }

    private static DateTime ParseDateOrDefault(string value, DateTime fallback)
    {
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
        {
            return parsed.ToUniversalTime();
        }

        return fallback;
    }

    private static List<MemberAddress> NormalizeAddresses(IEnumerable<MemberAddress>? addresses)
    {
        if (addresses is null)
        {
            return new List<MemberAddress>();
        }

        var normalized = addresses
            .Where(a =>
                !string.IsNullOrWhiteSpace(a.Street)
                || !string.IsNullOrWhiteSpace(a.PostalCode)
                || !string.IsNullOrWhiteSpace(a.City)
                || !string.IsNullOrWhiteSpace(a.Country))
            .Select(a => new MemberAddress
            {
                Street = NullIfEmpty(a.Street),
                PostalCode = NullIfEmpty(a.PostalCode),
                City = NullIfEmpty(a.City),
                Country = NullIfEmpty(a.Country),
                IsDefault = a.IsDefault
            })
            .ToList();

        if (normalized.Count > 0 && normalized.All(a => !a.IsDefault))
        {
            normalized[0].IsDefault = true;
        }

        return normalized;
    }
}
