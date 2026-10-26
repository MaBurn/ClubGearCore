using System.Globalization;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

/// <summary>
/// Opt-in demo dataset: a roster of members spread across the membership types, so the list,
/// filter, hierarchy, metadata and fee views have something believable to show in demos,
/// screenshots and manual testing.
///
/// Off unless <c>ClubGear:SeedDemoData</c> is true. Set it in appsettings.Development.json or,
/// for containers, as the environment variable <c>ClubGear__SeedDemoData</c>.
///
/// It builds on the system types created by the migrations rather than inventing parallel ones:
/// households use <c>Familie</c> and its <c>main_member</c> reference field, plain members use
/// <c>Standard</c>, and one member each exercises <c>Firma</c> and <c>Verein</c> with their
/// required name fields. Only the three types a club would add itself — Jugend, Passiv and
/// Ehrenmitglied — are created here, which is also where the recurring-fee configuration is
/// demonstrated; the system types are left as the migrations wrote them.
///
/// The data is invented. Names are fictional, e-mail addresses use the non-routable
/// <c>clubgear.local</c> domain, and phone numbers come from the 030 23125 block that is set
/// aside for film and television, so nothing here can reach a real person. Never enable this
/// against a database holding real members.
/// </summary>
public class DemoDataSeedTask : ISeedTask
{
    public const string EnabledConfigKey = "ClubGear:SeedDemoData";

    /// <summary>Presence of this member number means the dataset is already seeded.</summary>
    private const string MarkerMemberNumber = "M-1001";

    /// <summary>Runs after <see cref="MemberSeedTask"/> (10), whose single member stays untouched.</summary>
    public int Order => 20;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (serviceProvider.GetService(typeof(IConfiguration)) is not IConfiguration configuration
            || !configuration.GetValue<bool>(EnabledConfigKey))
        {
            return;
        }

        if (await dbContext.Members.AnyAsync(m => m.MemberNumber == MarkerMemberNumber, cancellationToken))
        {
            return;
        }

        var types = await EnsureClubTypesAsync(dbContext, cancellationToken);
        await SeedMembersAsync(dbContext, types, cancellationToken);
    }

    /// <summary>
    /// Adds the three club-defined types. The system types (Standard, Familie, Firma, Verein)
    /// already exist from the migrations and are returned alongside, untouched.
    /// </summary>
    private static async Task<Dictionary<string, MembershipType>> EnsureClubTypesAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var types = await dbContext.MembershipTypes
            .Include(t => t.Fields)
            .ToDictionaryAsync(t => t.Key, cancellationToken);

        var wanted = new List<MembershipType>
        {
            new()
            {
                Key = "jugend",
                Name = "Jugendmitglied",
                Description = "Ermäßigte Mitgliedschaft bis zum vollendeten 18. Lebensjahr.",
                SortOrder = 10,
                FeeAmount = 15m,
                BillingInterval = "Quarterly",
                Fields =
                {
                    new MembershipTypeField
                    {
                        Key = "erziehungsberechtigt",
                        Label = "Erziehungsberechtigte Person",
                        FieldType = MemberMetadataFieldType.Text,
                        IsRequired = true,
                        SortOrder = 0
                    }
                }
            },
            new()
            {
                Key = "passiv",
                Name = "Passives Mitglied",
                Description = "Fördernde Mitgliedschaft ohne Teilnahme am Übungsbetrieb.",
                SortOrder = 11,
                FeeAmount = 40m,
                BillingInterval = "Yearly"
            },
            new()
            {
                Key = "ehrenmitglied",
                Name = "Ehrenmitglied",
                Description = "Auf Beschluss der Mitgliederversammlung verliehen, beitragsfrei.",
                SortOrder = 12
            }
        };

        var added = false;
        foreach (var type in wanted.Where(type => !types.ContainsKey(type.Key)))
        {
            dbContext.MembershipTypes.Add(type);
            types[type.Key] = type;
            added = true;
        }

        if (added)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return types;
    }

    private static async Task SeedMembersAsync(
        ApplicationDbContext dbContext,
        IReadOnlyDictionary<string, MembershipType> types,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        // Parent links a household member to its main member. The reference is resolved to an id
        // after the rows are saved, because main_member stores the target's primary key.
        var roster = new List<DemoMember>
        {
            new("M-1001", "Andreas", "Brandt", "m", 1978, "Standard", 14),
            new("M-1002", "Petra", "Hoffmann", "w", 1982, "Standard", 11, Discount: 10),
            new("M-1003", "Michael", "Krüger", "m", 1969, "Standard", 22),
            new("M-1004", "Sabine", "Wagner", "w", 1975, "Standard", 9),
            new("M-1005", "Thomas", "Schneider", "m", 1988, "Standard", 6),
            new("M-1006", "Claudia", "Neumann", "w", 1991, "Standard", 4, Discount: 25),
            new("M-1007", "Jürgen", "Lehmann", "m", 1962, "Standard", 28),
            new("M-1008", "Katrin", "Schäfer", "w", 1985, "Standard", 7),
            new("M-1009", "Dirk", "Wolter", "m", 1980, "Standard", 12, LeftYearsAgo: 2),
            new("M-1010", "Birgit", "Sommer", "w", 1973, "Standard", 18, LeftYearsAgo: 1),

            new("M-1011", "Stefan", "Albrecht", "m", 1979, "Familie", 10, HouseholdFee: 180),
            new("M-1012", "Nadine", "Kellermann", "w", 1983, "Familie", 8, HouseholdFee: 180),
            new("M-1013", "Ralf", "Bergmann", "m", 1971, "Familie", 15, HouseholdFee: 180),

            new("M-1014", "Lena", "Albrecht", "w", 2010, "Familie", 10, Parent: "M-1011"),
            new("M-1015", "Jonas", "Albrecht", "m", 2013, "Familie", 8, Parent: "M-1011"),
            new("M-1016", "Marie", "Kellermann", "w", 2009, "Familie", 8, Parent: "M-1012"),
            new("M-1017", "Tim", "Kellermann", "m", 2012, "Familie", 6, Parent: "M-1012"),
            new("M-1018", "Sophie", "Bergmann", "w", 2008, "Familie", 12, Parent: "M-1013"),
            new("M-1019", "Elena", "Bergmann", "w", 2015, "Familie", 5, Parent: "M-1013"),

            new("M-1020", "Paul", "Reinhardt", "m", 2007, "jugend", 5, Guardian: "Martina Reinhardt"),
            new("M-1021", "Mia", "Zimmermann", "w", 2009, "jugend", 4, Guardian: "Holger Zimmermann"),
            new("M-1022", "Felix", "Wenzel", "m", 2006, "jugend", 7, Guardian: "Anke Wenzel"),
            new("M-1023", "Hanna", "Vogt", "w", 2008, "jugend", 3, Guardian: "Bernd Vogt"),

            new("M-1024", "Werner", "Krause", "m", 1955, "passiv", 31),
            new("M-1025", "Ingrid", "Pohl", "w", 1958, "passiv", 26),
            new("M-1026", "Manfred", "Rothe", "m", 1949, "passiv", 35, IsDeceased: true),

            new("M-1027", "Hans-Peter", "Löffler", "m", 1943, "ehrenmitglied", 44),
            new("M-1028", "Gisela", "Bauer", "w", 1947, "ehrenmitglied", 39),

            new("M-1029", "Kerstin", "Maibach", "w", 1976, "Firma", 6, CompanyName: "Maibach Sportartikel GmbH"),
            new("M-1030", "Olaf", "Steinbrück", "m", 1968, "Verein", 9, ClubName: "Turnerschaft Nordheim e.V.")
        };

        var entities = new Dictionary<string, Member>();
        var phoneSuffix = 10;

        foreach (var demo in roster)
        {
            if (!types.TryGetValue(demo.TypeKey, out var membershipType))
            {
                continue;
            }

            var joined = today.AddYears(-demo.JoinedYearsAgo);
            var member = new Member
            {
                MemberNumber = demo.Number,
                FirstName = demo.FirstName,
                LastName = demo.LastName,
                Gender = demo.Gender,
                Email = BuildEmail(demo),
                PhoneNumber = demo.Parent is null ? $"+49 30 23125 {phoneSuffix++:D2}" : null,
                DateOfBirth = new DateTime(demo.BirthYear, 5, 14, 0, 0, 0, DateTimeKind.Utc),
                MembershipTypeId = membershipType.Id,
                IsActive = demo.LeftYearsAgo is null && !demo.IsDeceased,
                IsVerified = true,
                IsDeceased = demo.IsDeceased,
                DataprivacyAccepted = true,
                NotifyViaEmail = demo.Parent is null,
                NewsletterConsent = demo.JoinedYearsAgo % 2 == 0,
                JoinedAt = joined,
                Joined = joined,
                Leaved = demo.LeftYearsAgo is { } left ? today.AddYears(-left) : null
            };

            dbContext.Members.Add(member);
            entities[demo.Number] = member;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Metadata needs both sides persisted: field ids from the membership types, parent ids
        // from the members just written.
        var fields = types.Values
            .SelectMany(t => t.Fields)
            .ToDictionary(f => $"{f.MembershipTypeId}:{f.Key}", f => f);

        foreach (var demo in roster)
        {
            if (!entities.TryGetValue(demo.Number, out var member) || member.MembershipTypeId is not { } typeId)
            {
                continue;
            }

            if (demo.Parent is { } parentNumber && entities.TryGetValue(parentNumber, out var parent))
            {
                TryAddValue(dbContext, fields, typeId, "main_member", member, parent.Id.ToString(CultureInfo.InvariantCulture));
            }

            if (demo.HouseholdFee is { } householdFee)
            {
                TryAddValue(dbContext, fields, typeId, "membership_fee", member, householdFee.ToString(CultureInfo.InvariantCulture));
            }

            if (demo.Discount is { } discount)
            {
                TryAddValue(dbContext, fields, typeId, "membership_discount_override", member, discount.ToString(CultureInfo.InvariantCulture));
            }

            if (demo.Guardian is { } guardian)
            {
                TryAddValue(dbContext, fields, typeId, "erziehungsberechtigt", member, guardian);
            }

            if (demo.CompanyName is { } companyName)
            {
                TryAddValue(dbContext, fields, typeId, "company_name", member, companyName);
            }

            if (demo.ClubName is { } clubName)
            {
                TryAddValue(dbContext, fields, typeId, "club_name", member, clubName);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void TryAddValue(
        ApplicationDbContext dbContext,
        IReadOnlyDictionary<string, MembershipTypeField> fields,
        int membershipTypeId,
        string fieldKey,
        Member member,
        string value)
    {
        if (!fields.TryGetValue($"{membershipTypeId}:{fieldKey}", out var field))
        {
            return;
        }

        dbContext.MemberMetadataValues.Add(new MemberMetadataValue
        {
            MemberId = member.Id,
            FieldId = field.Id,
            Value = value
        });
    }

    private static string BuildEmail(DemoMember demo)
    {
        var local = $"{demo.FirstName}.{demo.LastName}"
            .ToLowerInvariant()
            .Replace("ä", "ae", StringComparison.Ordinal)
            .Replace("ö", "oe", StringComparison.Ordinal)
            .Replace("ü", "ue", StringComparison.Ordinal)
            .Replace("ß", "ss", StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        return $"{local}@clubgear.local";
    }

    private sealed record DemoMember(
        string Number,
        string FirstName,
        string LastName,
        string Gender,
        int BirthYear,
        string TypeKey,
        int JoinedYearsAgo,
        string? Parent = null,
        string? Guardian = null,
        string? CompanyName = null,
        string? ClubName = null,
        int? Discount = null,
        int? HouseholdFee = null,
        bool IsDeceased = false,
        int? LeftYearsAgo = null);
}
