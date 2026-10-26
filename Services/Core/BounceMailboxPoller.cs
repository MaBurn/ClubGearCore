using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

/// <summary>
/// Periodically scans the configured bounce folder and flags every member whose address a
/// delivery-status notification reported as undeliverable. Runs for the lifetime of the
/// application; it is inert while "BounceCheckEnabled" is off (the default), because
/// <see cref="IImapMailboxService.FetchBouncesAsync"/> then returns immediately without touching
/// the network.
/// </summary>
/// <remarks>
/// The poller is a singleton hosted service but consumes scoped services, so it creates one
/// <see cref="IServiceScope"/> per cycle. A failing mailbox, an unreachable database or a broken
/// configuration is logged and the loop survives — a background poller must never take the host
/// down.
/// </remarks>
public sealed class BounceMailboxPoller : BackgroundService
{
    private const string EmailSection = "Email";

    /// <summary>
    /// Lower bound for the configured poll interval. Anything below this would hammer the mailbox
    /// (and the IMAP server's connection limits) for no practical gain.
    /// </summary>
    internal static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BounceMailboxPoller> _logger;
    private readonly TimeProvider _timeProvider;

    public BounceMailboxPoller(IServiceScopeFactory scopeFactory, ILogger<BounceMailboxPoller> logger)
        : this(scopeFactory, logger, TimeProvider.System)
    {
    }

    /// <summary>Test seam: lets a test drive the loop without waiting real minutes.</summary>
    internal BounceMailboxPoller(
        IServiceScopeFactory scopeFactory,
        ILogger<BounceMailboxPoller> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PeriodicTimer? timer = null;

        try
        {
            var interval = await ResolveIntervalAsync(stoppingToken).ConfigureAwait(false);
            timer = new PeriodicTimer(interval, _timeProvider);

            do
            {
                if (!await RunCycleSafelyAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }

                var next = await ResolveIntervalAsync(stoppingToken).ConfigureAwait(false);
                if (next != interval)
                {
                    // An admin changed the interval; adopt it without a restart.
                    interval = next;
                    timer.Dispose();
                    timer = new PeriodicTimer(interval, _timeProvider);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            // Host shutdown — expected, nothing to report.
        }
        finally
        {
            timer?.Dispose();
        }
    }

    /// <summary>
    /// Runs one fetch → invalidate cycle. Returns <c>false</c> when the cycle was cancelled and the
    /// loop must stop; any other failure is logged and reported as <c>true</c> so the next tick is
    /// still attempted.
    /// </summary>
    private async Task<bool> RunCycleSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await PollOnceAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bounce-Abruf fehlgeschlagen; der naechste Durchlauf wird erneut versucht");
            return true;
        }
    }

    /// <summary>
    /// One poll cycle: fetch the pending bounces through a fresh scope and hand each failed
    /// recipient to <see cref="IMemberEmailInvalidationService"/>.
    /// </summary>
    internal async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var mailbox = scope.ServiceProvider.GetRequiredService<IImapMailboxService>();
        var invalidation = scope.ServiceProvider.GetRequiredService<IMemberEmailInvalidationService>();

        var notices = await mailbox.FetchBouncesAsync(cancellationToken).ConfigureAwait(false);
        if (notices.Count == 0)
        {
            return;
        }

        foreach (var notice in notices)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(notice.FailedRecipient))
            {
                continue;
            }

            var flagged = await invalidation
                .MarkInvalidByEmailAsync(notice.FailedRecipient, notice.Diagnostic, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Bounce fuer {Recipient} verarbeitet, {Flagged} Mitglied(er) als ungueltig markiert",
                notice.FailedRecipient,
                flagged);
        }
    }

    /// <summary>
    /// Reads the poll interval from the system configuration, clamped to
    /// <see cref="MinimumInterval"/>. Falls back to the seeded default when the value is missing,
    /// unparsable, or the configuration store cannot be reached.
    /// </summary>
    internal async Task<TimeSpan> ResolveIntervalAsync(CancellationToken cancellationToken)
    {
        var fallback = new EmailSettings().BouncePollIntervalMinutes;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<ISystemConfigService>();
            var raw = await config
                .GetValueAsync(EmailSection, nameof(EmailSettings.BouncePollIntervalMinutes), cancellationToken)
                .ConfigureAwait(false);

            return ClampInterval(int.TryParse(raw, out var minutes) ? minutes : fallback);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Bounce-Pruefintervall konnte nicht gelesen werden; es gilt der Standard von {Minutes} Minuten",
                fallback);
            return ClampInterval(fallback);
        }
    }

    /// <summary>Clamps a configured interval in minutes to at least <see cref="MinimumInterval"/>.</summary>
    internal static TimeSpan ClampInterval(int minutes)
    {
        var configured = TimeSpan.FromMinutes(minutes);
        return configured < MinimumInterval ? MinimumInterval : configured;
    }
}
