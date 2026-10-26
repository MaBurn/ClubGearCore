using System.Net;

namespace ClubGear.Services.Core;

/// <summary>
/// Maps the status of a <see cref="ClubGear.Models.NotificationRecord"/> to the badge used in the
/// admin notification log. Lives outside the view so the four-way distinction (sent, bounced,
/// suppressed, failed) is one directly testable rule instead of a Razor conditional duplicated at
/// every call site.
/// </summary>
public static class NotificationStatusBadge
{
    /// <summary>Badge class for a successfully handed-over message.</summary>
    public const string SentCssClass = "bg-success";

    /// <summary>Badge class for a message the recipient's mail server rejected after acceptance.</summary>
    public const string BouncedCssClass = "bg-warning text-dark";

    /// <summary>Badge class for a message ClubGear did not attempt because the address is flagged invalid.</summary>
    public const string SuppressedCssClass = "bg-secondary";

    /// <summary>Badge class for a failed send and for any status the core does not know.</summary>
    public const string FailedCssClass = "bg-danger";

    /// <summary>Marker class that makes the badge identifiable in markup assertions.</summary>
    public const string MarkerCssClass = "notification-status-badge";

    /// <summary>
    /// Returns the Bootstrap contextual class for <paramref name="status"/>. Unknown values and
    /// <c>null</c> fall back to <see cref="FailedCssClass"/>, so a status the core cannot interpret
    /// never looks like a success.
    /// </summary>
    public static string CssClass(string? status) => status?.Trim() switch
    {
        NotificationService.SentStatus => SentCssClass,
        MemberEmailInvalidationService.BouncedStatus => BouncedCssClass,
        NotificationService.SuppressedStatus => SuppressedCssClass,
        _ => FailedCssClass
    };

    /// <summary>
    /// Returns the complete badge markup for <paramref name="status"/>, with the status text
    /// HTML-encoded.
    /// </summary>
    public static string Html(string? status)
    {
        var label = WebUtility.HtmlEncode(status ?? string.Empty);
        return $"<span class=\"badge {CssClass(status)} {MarkerCssClass}\">{label}</span>";
    }
}
