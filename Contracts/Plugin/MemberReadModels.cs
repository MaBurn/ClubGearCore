namespace ClubGear.Plugin.Contracts;

public sealed record PluginMemberSummary(
    int Id,
    string MemberNumber,
    string FullName,
    bool IsActive,
    string? Email);

public sealed record PluginMemberDetail(
    int Id,
    string MemberNumber,
    string FullName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    bool IsActive);