using System.Net;
using ClubGear.Models;

namespace ClubGear.Services.Core;

/// <summary>
/// Renders the small "ungültig" badge that marks a member address as undeliverable.
/// Lives outside the views so that the "badge for a flagged member, nothing for an unflagged
/// one" decision is a single, directly testable rule instead of duplicated Razor conditionals.
/// </summary>
public static class MemberEmailBadge
{
    /// <summary>Badge caption shown next to an undeliverable address.</summary>
    public const string Label = "ungültig";

    /// <summary>CSS classes of the badge element.</summary>
    public const string CssClass = "badge bg-danger ms-2 member-email-invalid-badge";

    /// <summary>
    /// Returns the badge markup for <paramref name="member"/>, or an empty string when the
    /// member is null or the address is not flagged as invalid.
    /// </summary>
    public static string Html(Member? member)
    {
        if (member is null || !member.EmailInvalid)
        {
            return string.Empty;
        }

        var tooltip = WebUtility.HtmlEncode(DescribeInvalidState(member));
        // Label is a fixed literal, so it is emitted verbatim (HtmlEncode would turn "ü" into a
        // numeric entity); only the free-text diagnostic in the tooltip needs encoding.
        return $"<span class=\"{CssClass}\" title=\"{tooltip}\">{Label}</span>";
    }

    /// <summary>
    /// Human-readable context for a flagged address: when it was flagged and, if known, why.
    /// Returns an empty string for members that are not flagged.
    /// </summary>
    public static string DescribeInvalidState(Member? member)
    {
        if (member is null || !member.EmailInvalid)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (member.EmailInvalidatedAtUtc is { } invalidatedAt)
        {
            parts.Add($"Als unzustellbar markiert am {invalidatedAt:dd.MM.yyyy HH:mm} UTC");
        }
        else
        {
            parts.Add("Als unzustellbar markiert");
        }

        if (!string.IsNullOrWhiteSpace(member.EmailInvalidReason))
        {
            parts.Add(member.EmailInvalidReason!.Trim());
        }

        return string.Join(" – ", parts);
    }
}
