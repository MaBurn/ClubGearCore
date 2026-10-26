using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 6, checkbox 6.1 DoD: the status → badge-class mapping distinguishes all four delivery
/// outcomes and falls back to the failure colour for anything it does not know.
/// </summary>
public sealed class NotificationStatusBadgeTests
{
    [Theory]
    [InlineData("Sent", "bg-success")]
    [InlineData("Bounced", "bg-warning text-dark")]
    [InlineData("Suppressed", "bg-secondary")]
    [InlineData("Failed", "bg-danger")]
    public void CssClass_MapsEveryKnownStatusToItsOwnBadge(string status, string expected)
    {
        Assert.Equal(expected, NotificationStatusBadge.CssClass(status));
    }

    [Fact]
    public void CssClass_UsesTheStatusConstantsOfTheServicesThatWriteThem()
    {
        Assert.Equal(NotificationStatusBadge.SentCssClass, NotificationStatusBadge.CssClass(NotificationService.SentStatus));
        Assert.Equal(NotificationStatusBadge.SuppressedCssClass, NotificationStatusBadge.CssClass(NotificationService.SuppressedStatus));
        Assert.Equal(NotificationStatusBadge.FailedCssClass, NotificationStatusBadge.CssClass(NotificationService.FailedStatus));
        Assert.Equal(NotificationStatusBadge.BouncedCssClass, NotificationStatusBadge.CssClass(MemberEmailInvalidationService.BouncedStatus));
    }

    [Theory]
    [InlineData("Queued")]
    [InlineData("sent")]
    [InlineData("")]
    [InlineData("   ")]
    public void CssClass_FallsBackToFailedForAnUnknownStatus(string status)
    {
        Assert.Equal("bg-danger", NotificationStatusBadge.CssClass(status));
    }

    [Fact]
    public void CssClass_FallsBackToFailedForNull()
    {
        Assert.Equal("bg-danger", NotificationStatusBadge.CssClass(null));
    }

    [Fact]
    public void CssClass_IgnoresSurroundingWhitespace()
    {
        Assert.Equal("bg-success", NotificationStatusBadge.CssClass(" Sent "));
    }

    [Fact]
    public void Html_RendersABadgeCarryingTheMappedClassAndTheStatusText()
    {
        var html = NotificationStatusBadge.Html("Bounced");

        Assert.Contains("badge", html, StringComparison.Ordinal);
        Assert.Contains("bg-warning text-dark", html, StringComparison.Ordinal);
        Assert.Contains(NotificationStatusBadge.MarkerCssClass, html, StringComparison.Ordinal);
        Assert.Contains(">Bounced<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ForNullRendersAFailureBadgeWithoutText()
    {
        var html = NotificationStatusBadge.Html(null);

        Assert.Contains("bg-danger", html, StringComparison.Ordinal);
        Assert.Contains("></span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_EncodesTheStatusText()
    {
        var html = NotificationStatusBadge.Html("<script>alert(1)</script>");

        Assert.DoesNotContain("<script>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }
}
