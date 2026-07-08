namespace ClubGear.Models.MemberFilters;

public sealed class BulkMemberActionRequest
{
    public List<int> SelectedMemberIds { get; set; } = new();

    public string? Search { get; set; }

    public string? Status { get; set; }

    public IReadOnlyList<int> GetValidSelectedMemberIds()
    {
        return SelectedMemberIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
    }
}