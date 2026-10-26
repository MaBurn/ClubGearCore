namespace ClubGear.Services.Abstractions;

/// <summary>
/// Flags every member whose email address matches a proven-undeliverable address, so that
/// further automated mail to it is suppressed until the address is corrected.
/// </summary>
public interface IMemberEmailInvalidationService
{
    /// <summary>
    /// Marks all members whose <see cref="Models.Member.Email"/> equals <paramref name="address"/>
    /// (case-insensitive, trimmed) as email-invalid. Members already flagged are left untouched,
    /// so repeated bounces for the same address are idempotent.
    /// </summary>
    /// <param name="address">The failed recipient address reported by the bounce/DSN.</param>
    /// <param name="reason">Optional diagnostic explaining why delivery failed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of members newly flagged by this call.</returns>
    Task<int> MarkInvalidByEmailAsync(string address, string? reason, CancellationToken cancellationToken = default);
}
