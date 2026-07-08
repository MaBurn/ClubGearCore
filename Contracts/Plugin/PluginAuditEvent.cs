namespace ClubGear.Plugin.Contracts;

public sealed record PluginAuditEvent(
    string Action,
    string? Actor,
    string? Source,
    string? TargetType,
    string? TargetId,
    DateTime OccurredAtUtc);
