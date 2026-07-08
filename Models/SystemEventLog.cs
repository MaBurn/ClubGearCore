namespace ClubGear.Models;

public class SystemEventLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";
    public string Category { get; set; } = "Application";
    public string Message { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? Path { get; set; }
    public string? Method { get; set; }
    public string? UserName { get; set; }
    public string? DetailsJson { get; set; }
}
