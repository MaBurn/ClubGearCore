namespace ClubGear.Services.Abstractions;

/// <summary>
/// Lightweight projection of a plugin identity-provider contribution, suitable for
/// presenting in admin UIs and selecting an active provider on the login page.
/// </summary>
public sealed record ExternalProviderInfo(
    string ProviderKey,
    string DisplayName,
    string ModuleId,
    bool IsEnabled);

/// <summary>
/// High-level status codes returned by the external-login callback handler.
/// </summary>
public enum ExternalLoginStatus
{
    /// <summary>The callback succeeded and the user was signed in.</summary>
    Success,

    /// <summary>The callback succeeded but no ClubGear member is linked to the external account.</summary>
    NoLinkedMember,

    /// <summary>The external identity provider returned an error or the state was invalid.</summary>
    ProviderError,

    /// <summary>An unexpected internal error occurred during callback processing.</summary>
    InternalError
}

/// <summary>
/// Outcome produced by <c>IExternalLoginService.HandleCallbackAsync</c>.
/// </summary>
public sealed record ExternalLoginOutcome(
    ExternalLoginStatus Status,
    string? RedirectUrl = null,
    string? ErrorMessage = null,
    string? ExternalUserId = null,
    string? ProviderKey = null);
