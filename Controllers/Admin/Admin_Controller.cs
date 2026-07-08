using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Models.Feedback;
using ClubGear.Data;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Controllers;

[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class AdminController : Controller
{
    private const string ClubSection = "Club";
    private readonly ISystemConfigService _systemConfigService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPermissionService _permissionService;

    public AdminController(ISystemConfigService systemConfigService, ApplicationDbContext dbContext, IPermissionService permissionService)
    {
        _systemConfigService = systemConfigService;
        _dbContext = dbContext;
        _permissionService = permissionService;
    }

    [HttpGet]
    public async Task<IActionResult> Functions(CancellationToken cancellationToken = default)
    {
        var allEntries = await _systemConfigService.GetAllAsync(cancellationToken);
        var maintenanceModeEnabled = bool.TryParse(
            await _systemConfigService.GetValueAsync("System", "MaintenanceMode", cancellationToken),
            out var parsedMaintenanceMode)
            && parsedMaintenanceMode;

        var model = new AdminFunctionsViewModel
        {
            Club = MapClubConfig(allEntries),
            AllEntries = allEntries,
            ConfigCards = BuildConfigCards(),
            NotificationRecords = await _dbContext.NotificationRecords
                .AsNoTracking()
                .OrderByDescending(record => record.CreatedAtUtc)
                .Take(50)
                .ToListAsync(cancellationToken),
            MaintenanceModeEnabled = maintenanceModeEnabled,
            CanManageMembershipTypes = await _permissionService.HasPermissionAsync(User, PermissionKeys.MembersTypesManage, cancellationToken),
            Feedback = ActionFeedbackViewModel.FromTempData(TempData)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMaintenanceMode(CancellationToken cancellationToken = default)
    {
        var currentlyEnabled = bool.TryParse(
            await _systemConfigService.GetValueAsync("System", "MaintenanceMode", cancellationToken),
            out var parsedMaintenanceMode)
            && parsedMaintenanceMode;

        var enabled = !currentlyEnabled;

        await _systemConfigService.UpsertAsync(
            "System",
            "MaintenanceMode",
            enabled ? "true" : "false",
            "Wartungsmodus aktiv",
            cancellationToken);

        SetFeedback(enabled
            ? ActionFeedbackViewModel.Warning("Wartungsmodus wurde aktiviert.")
            : ActionFeedbackViewModel.Success("Wartungsmodus wurde deaktiviert."));

        return RedirectToAction(nameof(Functions), new { tab = "maintenance" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveClubConfig(ClubConfigFormViewModel club, CancellationToken cancellationToken = default)
    {
        var entries = new[]
        {
            ToEntry(ClubSection, "ClubName", club.ClubName, "Anzeigename des Vereins"),
            ToEntry(ClubSection, "ClubAddress", club.ClubAddress, "Strasse und Hausnummer"),
            ToEntry(ClubSection, "ClubZip", club.ClubZip, "Postleitzahl"),
            ToEntry(ClubSection, "ClubCity", club.ClubCity, "Ort"),
            ToEntry(ClubSection, "ClubPhone", club.ClubPhone, "Telefonnummer"),
            ToEntry(ClubSection, "ClubEmail", club.ClubEmail, "Kontakt E-Mail"),
            ToEntry(ClubSection, "ClubWebsite", club.ClubWebsite, "Webseite"),
            ToEntry(ClubSection, "ClubDescription", club.ClubDescription, "Beschreibung")
        };

        await _systemConfigService.UpsertManyAsync(entries, cancellationToken);
        SetFeedback(ActionFeedbackViewModel.Success("Vereinsdaten wurden gespeichert."));
        return RedirectToAction(nameof(Functions), new { tab = "config" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveConfigEntry(ConfigEntryInputModel input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            SetFeedback(ActionFeedbackViewModel.Error("Name ist erforderlich."));
            return RedirectToAction(nameof(Functions), new { tab = "config" });
        }

        await _systemConfigService.UpsertAsync(input.Section, input.Name, input.Value, input.Description, cancellationToken);
        SetFeedback(ActionFeedbackViewModel.Success("Konfigurationseintrag wurde gespeichert."));
        return RedirectToAction(nameof(Functions), new { tab = "config" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfigEntry(int id, CancellationToken cancellationToken = default)
    {
        await _systemConfigService.DeleteByIdAsync(id, cancellationToken);
        SetFeedback(ActionFeedbackViewModel.Warning("Konfigurationseintrag wurde geloescht."));
        return RedirectToAction(nameof(Functions), new { tab = "config" });
    }

    private static ClubConfigFormViewModel MapClubConfig(IReadOnlyList<SystemConfigEntry> entries)
    {
        string GetValue(string name)
            => entries.FirstOrDefault(e => string.Equals(e.Section, ClubSection, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        return new ClubConfigFormViewModel
        {
            ClubName = GetValue("ClubName"),
            ClubAddress = GetValue("ClubAddress"),
            ClubZip = GetValue("ClubZip"),
            ClubCity = GetValue("ClubCity"),
            ClubPhone = GetValue("ClubPhone"),
            ClubEmail = GetValue("ClubEmail"),
            ClubWebsite = GetValue("ClubWebsite"),
            ClubDescription = GetValue("ClubDescription")
        };
    }

    private static SystemConfigEntry ToEntry(string section, string name, string value, string description)
    {
        return new SystemConfigEntry
        {
            Section = section,
            Name = name,
            Value = value,
            Description = description
        };
    }

    private static IReadOnlyList<AdminConfigCardViewModel> BuildConfigCards()
    {
        return new[]
        {
            new AdminConfigCardViewModel
            {
                SectionGroupTitle = "Vereinsdaten",
                Title = "Verein",
                Description = "Name, Adresse, Kontaktdaten und Beschreibung des Vereins.",
                ModalId = "cfgModal-club",
                ThemeCssClass = "primary",
                Fields = new[]
                {
                    new AdminConfigFieldViewModel { Label = "Vereinsname", Key = "ClubName", Section = "Club" },
                    new AdminConfigFieldViewModel { Label = "Adresse", Key = "ClubAddress", Section = "Club" },
                    new AdminConfigFieldViewModel { Label = "PLZ", Key = "ClubZip", Section = "Club" },
                    new AdminConfigFieldViewModel { Label = "Stadt", Key = "ClubCity", Section = "Club" },
                    new AdminConfigFieldViewModel { Label = "Telefon", Key = "ClubPhone", Section = "Club" },
                    new AdminConfigFieldViewModel { Label = "E-Mail", Key = "ClubEmail", Section = "Club", InputType = "email" },
                    new AdminConfigFieldViewModel { Label = "Website", Key = "ClubWebsite", Section = "Club", InputType = "url" },
                    new AdminConfigFieldViewModel { Label = "Beschreibung", Key = "ClubDescription", Section = "Club", InputType = "textarea", Rows = 3 }
                }
            },
            new AdminConfigCardViewModel
            {
                SectionGroupTitle = "Vereinsdaten",
                Title = "Mitgliedsnummern",
                Description = "Nummernkreis fuer automatisch vergebene Mitgliedsnummern.",
                ModalId = "cfgModal-member-numbers",
                ThemeCssClass = "warning",
                Fields = new[]
                {
                    new AdminConfigFieldViewModel { Label = "Prefix", Key = "MemberNumberPrefix", Section = "Members" },
                    new AdminConfigFieldViewModel { Label = "Naechste Nummer", Key = "MemberNumberNextNumber", Section = "Members", InputType = "number" },
                    new AdminConfigFieldViewModel { Label = "Suffix", Key = "MemberNumberSuffix", Section = "Members" },
                    new AdminConfigFieldViewModel { Label = "Mindestanzahl Ziffern", Key = "MemberNumberPadding", Section = "Members", InputType = "number" }
                }
            },
            new AdminConfigCardViewModel
            {
                SectionGroupTitle = "Vereinsdaten",
                Title = "Finanzen & Bank",
                Description = "Bankdaten, SEPA, Mitgliedsbeitrag und Mahnwesen.",
                ModalId = "cfgModal-finance",
                ThemeCssClass = "success",
                Fields = new[]
                {
                    new AdminConfigFieldViewModel { Label = "Bankname", Key = "BankName", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "IBAN", Key = "BankIBAN", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "BIC", Key = "BankBIC", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "Kontoinhaber", Key = "BankAccountHolder", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "SEPA Glaeubiger-ID", Key = "SEPA-creditor-id", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "Waehrung", Key = "DefaultCurrency", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "Mitgliedsbeitrag", Key = "MemberFee", Section = "Finance" },
                    new AdminConfigFieldViewModel { Label = "Mahnung (Tage)", Key = "PaymentReminderDays", Section = "Finance", InputType = "number" }
                }
            },
            new AdminConfigCardViewModel
            {
                SectionGroupTitle = "Kommunikation",
                Title = "E-Mail / SMTP",
                Description = "SMTP-Server, Absender und Verschluesselung.",
                ModalId = "cfgModal-email",
                ThemeCssClass = "info",
                Fields = new[]
                {
                    new AdminConfigFieldViewModel { Label = "Absendername", Key = nameof(EmailSettings.SenderName), Section = "Email" },
                    new AdminConfigFieldViewModel { Label = "Absenderadresse", Key = nameof(EmailSettings.SenderAddress), Section = "Email", InputType = "email" },
                    new AdminConfigFieldViewModel { Label = "SMTP Host", Key = nameof(EmailSettings.Host), Section = "Email" },
                    new AdminConfigFieldViewModel { Label = "SMTP Port", Key = nameof(EmailSettings.Port), Section = "Email", InputType = "number" },
                    new AdminConfigFieldViewModel { Label = "StartTLS aktiv", Key = nameof(EmailSettings.UseStartTls), Section = "Email", InputType = "checkbox" },
                    new AdminConfigFieldViewModel { Label = "Benutzername", Key = nameof(EmailSettings.Username), Section = "Email" },
                    new AdminConfigFieldViewModel { Label = "Passwort", Key = nameof(EmailSettings.Password), Section = "Email", InputType = "password" },
                    new AdminConfigFieldViewModel { Label = "Pickup Directory aktiv", Key = nameof(EmailSettings.UsePickupDirectory), Section = "Email", InputType = "checkbox" },
                    new AdminConfigFieldViewModel { Label = "Pickup Directory", Key = nameof(EmailSettings.PickupDirectory), Section = "Email" }
                }
            }
        };
    }

    private void SetFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }
}
