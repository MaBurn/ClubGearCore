using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 5, checkbox 5.4 DoD: the suppression guard in
/// <c>NotificationService.NotifyAsync</c> is only worth anything if every outgoing mail really
/// goes through it. This guardrail fails as soon as a new direct SMTP sender appears anywhere in
/// the core outside the one sanctioned transport, <c>Services/Channels/SmtpEmailNotificationChannel.cs</c>.
/// </summary>
public sealed class EmailTransportGuardrailTests
{
    /// <summary>The single file allowed to talk SMTP directly (relative to the project root).</summary>
    private static readonly string[] SanctionedTransports =
    {
        Path.Combine("Services", "Channels", "SmtpEmailNotificationChannel.cs")
    };

    private static readonly string[] SmtpMarkers =
    {
        "System.Net.Mail",
        "SmtpClient",
        "MailKit.Net.Smtp",
        "MailMessage"
    };

    [Fact]
    public void NoCoreFileOutsideTheSmtpChannelTalksSmtpDirectly()
    {
        var offenders = new List<string>();

        foreach (var file in EnumerateCoreSourceFiles())
        {
            var relative = Path.GetRelativePath(ProjectRoot(), file);
            if (SanctionedTransports.Contains(relative, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var hits = SmtpMarkers.Where(marker => content.Contains(marker, StringComparison.Ordinal)).ToList();
            if (hits.Count > 0)
            {
                offenders.Add($"{relative} -> {string.Join(", ", hits)}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Direkter SMTP-Versand ausserhalb von Services/Channels/SmtpEmailNotificationChannel.cs umgeht die "
            + "Unterdrueckung ungueltiger Empfaenger in NotificationService.NotifyAsync. Betroffen: "
            + string.Join(" | ", offenders));
    }

    [Fact]
    public void TheSanctionedTransportExistsAndIsTheOneThatSendsSmtp()
    {
        var channel = Path.Combine(ProjectRoot(), SanctionedTransports[0]);

        Assert.True(File.Exists(channel), $"Erwarteter SMTP-Channel fehlt: {SanctionedTransports[0]}");
        Assert.Contains("SmtpClient", File.ReadAllText(channel), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGuardrailWouldCatchANewDirectSender()
    {
        // Sanity check of the detector itself: a file with a direct sender must trip at least one marker.
        const string offendingSource = """
            using System.Net.Mail;
            var client = new SmtpClient("localhost");
            """;

        Assert.Contains(SmtpMarkers, marker => offendingSource.Contains(marker, StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateCoreSourceFiles()
    {
        var root = ProjectRoot();
        var excludedTopLevel = new[] { "bin", "obj", "tests", "plugins", ".git", ".crispy", ".crispy-worktree" };

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(segment => excludedTopLevel.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            yield return file;
        }
    }

    private static string ProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClubGear.csproj")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }
}
