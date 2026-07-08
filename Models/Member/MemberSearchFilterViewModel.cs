namespace ClubGear.Models.MemberFilters;

public sealed class MemberSearchFilterViewModel
{
    public string? Search { get; set; }

    public string Status { get; set; } = "all";

    public string NormalizedStatus => NormalizeStatus(Status);

    public static string NormalizeStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "active" => "active",
            "inactive" => "inactive",
            _ => "all"
        };
    }
}