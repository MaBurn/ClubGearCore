using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

public enum MembershipTypeOperationStatus
{
    Success,
    NotFound,
    DuplicateKey,
    Blocked
}

public sealed record MembershipTypeOperationResult(
    MembershipTypeOperationStatus Status,
    string? ErrorMessage = null,
    MembershipType? Type = null)
{
    public bool Success => Status == MembershipTypeOperationStatus.Success;

    public static MembershipTypeOperationResult Ok(MembershipType type)
        => new(MembershipTypeOperationStatus.Success, null, type);

    public static MembershipTypeOperationResult NotFoundResult()
        => new(MembershipTypeOperationStatus.NotFound, "Mitgliedsart wurde nicht gefunden.");

    public static MembershipTypeOperationResult Duplicate(string message)
        => new(MembershipTypeOperationStatus.DuplicateKey, message);

    public static MembershipTypeOperationResult BlockedResult(string message)
        => new(MembershipTypeOperationStatus.Blocked, message);
}

public sealed record MembershipTypeFieldOperationResult(
    MembershipTypeOperationStatus Status,
    string? ErrorMessage = null,
    MembershipTypeField? Field = null)
{
    public bool Success => Status == MembershipTypeOperationStatus.Success;

    public static MembershipTypeFieldOperationResult Ok(MembershipTypeField field)
        => new(MembershipTypeOperationStatus.Success, null, field);

    public static MembershipTypeFieldOperationResult NotFoundResult()
        => new(MembershipTypeOperationStatus.NotFound, "Feld wurde nicht gefunden.");

    public static MembershipTypeFieldOperationResult Duplicate(string message)
        => new(MembershipTypeOperationStatus.DuplicateKey, message);

    public static MembershipTypeFieldOperationResult BlockedResult(string message)
        => new(MembershipTypeOperationStatus.Blocked, message);
}

/// <summary>
/// Manages Mitgliedsarten (membership types) and their per-type metadata field
/// definitions (<see cref="MembershipTypeField"/>). Deleting a type is blocked
/// while any <see cref="Member"/> still references it; removing a field is
/// blocked only when the field is system-defined AND still referenced by
/// existing <see cref="MemberMetadataValue"/> rows.
/// </summary>
public interface IMembershipTypeService
{
    Task<IReadOnlyList<MembershipType>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MembershipType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<MembershipTypeOperationResult> CreateTypeAsync(MembershipType type, CancellationToken cancellationToken = default);

    Task<MembershipTypeOperationResult> UpdateTypeAsync(int id, MembershipType updated, CancellationToken cancellationToken = default);

    Task<MembershipTypeOperationResult> DeleteTypeAsync(int id, CancellationToken cancellationToken = default);

    Task<MembershipTypeFieldOperationResult> AddFieldAsync(int membershipTypeId, MembershipTypeField field, CancellationToken cancellationToken = default);

    Task<MembershipTypeFieldOperationResult> UpdateFieldAsync(int fieldId, MembershipTypeField updated, CancellationToken cancellationToken = default);

    Task<MembershipTypeFieldOperationResult> RemoveFieldAsync(int fieldId, CancellationToken cancellationToken = default);
}
