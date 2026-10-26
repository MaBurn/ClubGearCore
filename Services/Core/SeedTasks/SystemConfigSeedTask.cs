using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

public sealed class SystemConfigSeedTask : ISeedTask
{
    public int Order => 30;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var defaults = new[]
        {
            new SystemConfigEntry { Section = "Club", Name = "ClubName", Value = "Interessengemeinschaft T2 Freunde des VW-Busses 1967-1979 e.V.", Description = "Anzeigename des Vereins" },
            new SystemConfigEntry { Section = "Club", Name = "ClubAddress", Value = "Egonstrasse 4", Description = "Strasse und Hausnummer" },
            new SystemConfigEntry { Section = "Club", Name = "ClubZip", Value = "need to change", Description = "Postleitzahl" },
            new SystemConfigEntry { Section = "Club", Name = "ClubCity", Value = "45896 Gelsenkirchen", Description = "Ort" },
            new SystemConfigEntry { Section = "Club", Name = "ClubPhone", Value = "need to change", Description = "Telefonnummer" },
            new SystemConfigEntry { Section = "Club", Name = "ClubEmail", Value = "vorsitz@bulli.org", Description = "Kontakt E-Mail" },
            new SystemConfigEntry { Section = "Club", Name = "ClubWebsite", Value = "ClubWebsite", Description = "Webseite" },
            new SystemConfigEntry { Section = "Club", Name = "ClubDescription", Value = string.Empty, Description = "Beschreibung" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.SenderName), Value = "ClubGear", Description = "Anzeigename des Absenders" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.SenderAddress), Value = "noreply@clubgear.local", Description = "Absenderadresse" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.Host), Value = "localhost", Description = "SMTP Host" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.Port), Value = "25", Description = "SMTP Port" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.UseStartTls), Value = "false", Description = "StartTLS aktiv" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.Username), Value = string.Empty, Description = "SMTP Benutzername" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.Password), Value = string.Empty, Description = "SMTP Passwort" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.UsePickupDirectory), Value = "true", Description = "E-Mails im Pickup-Verzeichnis ablegen" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.PickupDirectory), Value = "App_Data/MailDrop", Description = "Pickup-Verzeichnis" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.ImapHost), Value = string.Empty, Description = "IMAP Host (leer = Postfachfunktionen aus)" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.ImapPort), Value = "993", Description = "IMAP Port" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.ImapUseSsl), Value = "true", Description = "IMAP mit SSL/TLS" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.ImapUsername), Value = string.Empty, Description = "IMAP Benutzername (leer = SMTP Benutzername)" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.ImapPassword), Value = string.Empty, Description = "IMAP Passwort (leer = SMTP Passwort)" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.SentFolder), Value = "Sent", Description = "Ordner fuer gesendete Nachrichten" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.AppendSentEnabled), Value = "false", Description = "Gesendete E-Mails in den Gesendet-Ordner kopieren" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.BounceFolder), Value = "INBOX", Description = "Ordner, der auf Unzustellbarkeitsmeldungen geprueft wird" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.BounceCheckEnabled), Value = "false", Description = "Postfach auf Unzustellbarkeitsmeldungen pruefen" },
            new SystemConfigEntry { Section = "Email", Name = nameof(EmailSettings.BouncePollIntervalMinutes), Value = "5", Description = "Pruefintervall in Minuten (mindestens 1)" },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberPrefix", Value = "M-", Description = "Prefix fuer automatisch vergebene Mitgliedsnummern" },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberSuffix", Value = string.Empty, Description = "Suffix fuer automatisch vergebene Mitgliedsnummern" },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberNextNumber", Value = "1", Description = "Naechste automatisch zu vergebende Mitgliedsnummer" },
            new SystemConfigEntry { Section = "Members", Name = "MemberNumberPadding", Value = "4", Description = "Mindestanzahl Ziffern fuer den Nummernanteil" },
            new SystemConfigEntry { Section = "System", Name = "MaintenanceMode", Value = "false", Description = "Wartungsmodus aktiv" }
        };

        var existingKeys = await dbContext.SystemConfigEntries
            .AsNoTracking()
            .Select(e => e.Section + "|" + e.Name)
            .ToListAsync(cancellationToken);

        var missing = defaults
            .Where(d => !existingKeys.Contains(d.Section + "|" + d.Name, StringComparer.OrdinalIgnoreCase))
            .Select(d =>
            {
                d.UpdatedAtUtc = DateTime.UtcNow;
                return d;
            })
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.SystemConfigEntries.AddRange(missing);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
