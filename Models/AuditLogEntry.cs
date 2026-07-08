namespace ClubGear.Models;

public class AuditLogEntry
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? Source { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string? MetadataJson { get; set; }
}
