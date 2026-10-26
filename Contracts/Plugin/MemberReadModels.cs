namespace ClubGear.Plugin.Contracts;

public sealed record PluginMemberSummary(
    int Id,
    string MemberNumber,
    string FullName,
    bool IsActive,
    string? Email,
    int? MembershipTypeId = null);

public sealed record PluginMemberDetail(
    int Id,
    string MemberNumber,
    string FullName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    int? MembershipTypeId = null,
    string? MembershipTypeName = null,
    decimal? MembershipFeeAmount = null,
    string? MembershipBillingInterval = null);