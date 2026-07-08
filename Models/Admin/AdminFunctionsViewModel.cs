using ClubGear.Models;
using ClubGear.Models.Feedback;

namespace ClubGear.Models.Admin;

public sealed class AdminFunctionsViewModel
{
    public ClubConfigFormViewModel Club { get; init; } = new();
    public IReadOnlyList<SystemConfigEntry> AllEntries { get; init; } = Array.Empty<SystemConfigEntry>();
    public IReadOnlyList<AdminConfigCardViewModel> ConfigCards { get; init; } = Array.Empty<AdminConfigCardViewModel>();
    public IReadOnlyList<NotificationRecord> NotificationRecords { get; init; } = Array.Empty<NotificationRecord>();
    public bool MaintenanceModeEnabled { get; init; }
    public bool CanManageMembershipTypes { get; init; }
    public ActionFeedbackViewModel? Feedback { get; init; }
}

public sealed class AdminConfigCardViewModel
{
    public string SectionGroupTitle { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ModalId { get; init; } = string.Empty;
    public string ThemeCssClass { get; init; } = string.Empty;
    public IReadOnlyList<AdminConfigFieldViewModel> Fields { get; init; } = Array.Empty<AdminConfigFieldViewModel>();
}

public sealed class AdminConfigFieldViewModel
{
    public string Label { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Section { get; init; } = string.Empty;
    public string InputType { get; init; } = "text";
    public int Rows { get; init; } = 1;
}

public sealed class ClubConfigFormViewModel
{
    public string ClubName { get; set; } = string.Empty;
    public string ClubAddress { get; set; } = string.Empty;
    public string ClubZip { get; set; } = string.Empty;
    public string ClubCity { get; set; } = string.Empty;
    public string ClubPhone { get; set; } = string.Empty;
    public string ClubEmail { get; set; } = string.Empty;
    public string ClubWebsite { get; set; } = string.Empty;
    public string ClubDescription { get; set; } = string.Empty;
}

public sealed class ConfigEntryInputModel
{
    public int Id { get; set; }
    public string Section { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
