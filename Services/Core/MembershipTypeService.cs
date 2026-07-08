using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public sealed class MembershipTypeService : IMembershipTypeService
{
    private readonly ApplicationDbContext _db;

    public MembershipTypeService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var types = await _db.MembershipTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);

        foreach (var type in types)
        {
            type.Fields = type.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Label).ToList();
        }

        return types;
    }

    public async Task<MembershipType?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var type = await _db.MembershipTypes
            .AsNoTracking()
            .Include(t => t.Fields)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (type is not null)
        {
            type.Fields = type.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Label).ToList();
        }

        return type;
    }

    public async Task<MembershipTypeOperationResult> CreateTypeAsync(MembershipType type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);

        var key = type.Key?.Trim() ?? string.Empty;
        var name = type.Name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            return MembershipTypeOperationResult.Duplicate("Der Schluessel (Key) darf nicht leer sein.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return MembershipTypeOperationResult.Duplicate("Der Name darf nicht leer sein.");
        }

        var duplicate = await _db.MembershipTypes
            .AnyAsync(t => t.Key == key, cancellationToken);

        if (duplicate)
        {
            return MembershipTypeOperationResult.Duplicate($"Eine Mitgliedsart mit dem Schluessel '{key}' existiert bereits.");
        }

        var now = DateTime.UtcNow;
        var entity = new MembershipType
        {
            Key = key,
            Name = name,
            Description = string.IsNullOrWhiteSpace(type.Description) ? null : type.Description.Trim(),
            DefaultDiscountPercent = type.DefaultDiscountPercent,
            IsSystemDefined = false,
            SortOrder = type.SortOrder,
            IsActive = type.IsActive,
            AllowsSubMembers = type.AllowsSubMembers,
            SubMemberLabel = string.IsNullOrWhiteSpace(type.SubMemberLabel) ? null : type.SubMemberLabel.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.MembershipTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeOperationResult.Ok(entity);
    }

    public async Task<MembershipTypeOperationResult> UpdateTypeAsync(int id, MembershipType updated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updated);

        var existing = await _db.MembershipTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (existing is null)
        {
            return MembershipTypeOperationResult.NotFoundResult();
        }

        var name = updated.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return MembershipTypeOperationResult.Duplicate("Der Name darf nicht leer sein.");
        }

        existing.Name = name;
        existing.Description = string.IsNullOrWhiteSpace(updated.Description) ? null : updated.Description.Trim();
        existing.DefaultDiscountPercent = updated.DefaultDiscountPercent;
        existing.SortOrder = updated.SortOrder;
        existing.IsActive = updated.IsActive;
        existing.AllowsSubMembers = updated.AllowsSubMembers;
        existing.SubMemberLabel = string.IsNullOrWhiteSpace(updated.SubMemberLabel) ? null : updated.SubMemberLabel.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeOperationResult.Ok(existing);
    }

    public async Task<MembershipTypeOperationResult> DeleteTypeAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _db.MembershipTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (existing is null)
        {
            return MembershipTypeOperationResult.NotFoundResult();
        }

        var referencedByMembers = await _db.Members
            .AnyAsync(m => m.MembershipTypeId == id, cancellationToken);

        if (referencedByMembers)
        {
            return MembershipTypeOperationResult.BlockedResult(
                $"Die Mitgliedsart '{existing.Name}' wird noch von Mitgliedern verwendet und kann nicht geloescht werden.");
        }

        _db.MembershipTypes.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeOperationResult.Ok(existing);
    }

    public async Task<MembershipTypeFieldOperationResult> AddFieldAsync(int membershipTypeId, MembershipTypeField field, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(field);

        var type = await _db.MembershipTypes.FirstOrDefaultAsync(t => t.Id == membershipTypeId, cancellationToken);
        if (type is null)
        {
            return MembershipTypeFieldOperationResult.NotFoundResult();
        }

        var key = field.Key?.Trim() ?? string.Empty;
        var label = field.Label?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            return MembershipTypeFieldOperationResult.Duplicate("Der Schluessel (Key) darf nicht leer sein.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return MembershipTypeFieldOperationResult.Duplicate("Das Label darf nicht leer sein.");
        }

        var duplicate = await _db.MembershipTypeFields
            .AnyAsync(f => f.MembershipTypeId == membershipTypeId && f.Key == key, cancellationToken);

        if (duplicate)
        {
            return MembershipTypeFieldOperationResult.Duplicate($"Ein Feld mit dem Schluessel '{key}' existiert bereits fuer diese Mitgliedsart.");
        }

        var entity = new MembershipTypeField
        {
            MembershipTypeId = membershipTypeId,
            Key = key,
            Label = label,
            FieldType = field.FieldType,
            IsRequired = field.IsRequired,
            HelpText = string.IsNullOrWhiteSpace(field.HelpText) ? null : field.HelpText.Trim(),
            SortOrder = field.SortOrder,
            IsSystemDefined = false
        };

        _db.MembershipTypeFields.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeFieldOperationResult.Ok(entity);
    }

    public async Task<MembershipTypeFieldOperationResult> UpdateFieldAsync(int fieldId, MembershipTypeField updated, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updated);

        var existing = await _db.MembershipTypeFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (existing is null)
        {
            return MembershipTypeFieldOperationResult.NotFoundResult();
        }

        var key = updated.Key?.Trim() ?? string.Empty;
        var label = updated.Label?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(key))
        {
            return MembershipTypeFieldOperationResult.Duplicate("Der Schluessel (Key) darf nicht leer sein.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return MembershipTypeFieldOperationResult.Duplicate("Das Label darf nicht leer sein.");
        }

        if (!string.Equals(existing.Key, key, StringComparison.Ordinal))
        {
            var duplicate = await _db.MembershipTypeFields
                .AnyAsync(f => f.Id != fieldId && f.MembershipTypeId == existing.MembershipTypeId && f.Key == key, cancellationToken);

            if (duplicate)
            {
                return MembershipTypeFieldOperationResult.Duplicate($"Ein Feld mit dem Schluessel '{key}' existiert bereits fuer diese Mitgliedsart.");
            }
        }

        existing.Key = key;
        existing.Label = label;
        existing.FieldType = updated.FieldType;
        existing.IsRequired = updated.IsRequired;
        existing.HelpText = string.IsNullOrWhiteSpace(updated.HelpText) ? null : updated.HelpText.Trim();
        existing.SortOrder = updated.SortOrder;

        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeFieldOperationResult.Ok(existing);
    }

    public async Task<MembershipTypeFieldOperationResult> RemoveFieldAsync(int fieldId, CancellationToken cancellationToken = default)
    {
        var existing = await _db.MembershipTypeFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (existing is null)
        {
            return MembershipTypeFieldOperationResult.NotFoundResult();
        }

        if (existing.IsSystemDefined)
        {
            var referencedByData = await _db.MemberMetadataValues
                .AnyAsync(v => v.FieldId == fieldId, cancellationToken);

            if (referencedByData)
            {
                return MembershipTypeFieldOperationResult.BlockedResult(
                    $"Das Systemfeld '{existing.Label}' wird noch von Mitgliederdaten verwendet und kann nicht entfernt werden.");
            }
        }

        _db.MembershipTypeFields.Remove(existing);
        await _db.SaveChangesAsync(cancellationToken);

        return MembershipTypeFieldOperationResult.Ok(existing);
    }
}
