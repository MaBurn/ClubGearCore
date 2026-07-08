namespace ClubGear.Services.Abstractions;

public enum AccountLoginStatus
{
    Success,
    MissingCredentials,
    InvalidCredentials
}

public sealed record AccountLoginOutcome(AccountLoginStatus Status);

public sealed record AccountRegistrationOutcome(bool Succeeded, IReadOnlyList<string> Errors);

public interface IAccountFeatureService
{
    Task<AccountLoginOutcome> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AccountRegistrationOutcome> RegisterAsync(string fullName, string email, string password, CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
