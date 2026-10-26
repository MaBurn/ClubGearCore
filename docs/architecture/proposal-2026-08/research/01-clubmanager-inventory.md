# What Clubmanager already implements

Source: `/Users/max/Code/Clubmanager` (working copy, surveyed 2026-08-30).
Method: controller and action inventory, `Models/ClubManagerContext.cs` DbSet inventory,
`Services/` inventory, `Views/` inventory. Sizes are file sizes on disk — a rough but honest
proxy for how much behaviour sits in one place.

## Scale of the legacy system

| Measure | Value |
|---|---|
| MVC controllers | 41 (`Controllers/Mvc`) |
| API controllers | 17 (`Controllers/Api`) |
| EF `DbSet`s | 115 |
| View folders | 33 |
| Largest controllers | `MembersController` 277 KB · `InventoryController` 128 KB · `AdminController` 112 KB · `UserController` 86 KB · `FinanceController` 73 KB · `MobileInventoryController` 70 KB |
| Largest services | `MarketplaceRentalService` 78 KB · `InvalidEmailLetterService` 70 KB · `NotificationService` 48 KB · `NextcloudCalendarService` 42 KB · `DatabaseConfigurationService` 38 KB · `EmailSyncService` 31 KB |

This matters for the proposal: the legacy behaviour is real and substantial, but it is packed into
a handful of very large controllers. A 1:1 port would carry that structure into ClubGear. Every
recommendation below assumes re-cutting into services, commands/queries and thin endpoints.

## Per topic — legacy state vs. ClubGear today

Legend for **ClubGear today**: ✅ present · 🟡 partial · ❌ missing.

### CORE-01 Organisation structure
- **Clubmanager:** `LocalUnitController` (40 KB, 17 actions incl. `JoinSelf`, `LeaveSelf`,
  `ToggleContact`, `ResponsibleView`, `RadiusSearch`, `ExportRadiusSearch`),
  `LocalUnitAdminController`, `OrganigrammController` + `OrganigrammApiController`,
  `OrganigrammService`. Tables: `LocalUnit`, `LocalUnitAdmins`, `LocalUnitCalendarMappings`,
  `OrgNodes`, `ClubRoles`, `ClubRoleMembers`, `ClubRoleNotifications`, plus Keycloak group
  mirroring (`Groups`, `GroupPermissions`, `UserGroups`, `ManagedKeycloakGroups`,
  `KeycloakDefaultGroups`). 7 views.
- **Notable:** self-service join/leave and delegated unit admins already exist. Radius search is a
  geographic feature not present in the ClubGear backlog — worth an explicit keep/drop decision.
- **ClubGear today:** ❌ no organisation model at all. Member hierarchy exists, unit scope does not.

### CORE-02 Secure file and media storage
- **Clubmanager:** no generic storage service. Uploads are handled per controller, and images live
  in **seven** separate tables: `InventoryImages`, `ToolImages`, `EditorialImages`,
  `RentalOrderPhotos`, `MarketplaceRentalDamageImages`, `DecisionTreeAttachments`,
  `MassMailAttachments`. Support services: `ImageThumbnailService`, `DocumentProcessingService`,
  `DocumentService` (21 KB), `WebDavService` (19 KB), `NextcloudDocumentService` (26 KB).
- **Notable:** this is the clearest case in the whole survey for building the capability once. The
  legacy system paid for upload handling at least seven times.
- **ClubGear today:** ❌ only `ProfileImageStorageService`.

### CORE-03 Plugin surfaces and contracts
- **Clubmanager:** out-of-process plugin architecture — `Protos/`, `ClubManager.PluginSDK`,
  `PluginProxyController`, `MatrixAgent/`, `plugins/traccar`, tables `PluginInstallations`,
  `PluginSettings`, `PluginDataRecords`. The backlog explicitly rejects porting this.
- **ClubGear today:** 🟡 in-process contract at `ContractVersion.Current = 1.10.0`
  (`MinimumSupported = 1.8.0`) with extension points `member.detail/edit/badge/action`,
  `selfservice.profile`, `admin.functions`, `runtime.route`, `nav.main`, `page.generic`,
  `audit.sink`, `identity.provider`.
  The gap is concrete: `IPluginPageProvider` offers columns + commands + a single
  `filterValue` string. There is no pagination, sorting, detail view, upload, download, or JSON
  endpoint, and `ExecuteCommandAsync` takes an
  `IReadOnlyDictionary<string, string>` rather than a typed schema.

### CORE-04 Notification inbox and channels
- **Clubmanager:** `NotificationController` already implements `GetNotifications`,
  `GetUnreadCount`, `MarkAsRead`, `MarkAllAsRead`, `DeleteNotification`, `DeleteAllNotifications`,
  `GetChannelSettings`, `SaveChannelSettings`. Tables: `Notifications`, `CommunicationChannels`,
  `CommunicationProducts`, `CommunicationProductChannels`, `MemberCommunicationPreferences`.
  `NotificationService` is 48 KB. A Matrix bot exists as `MatrixAgent/`.
- **Notable:** the inbox and per-product channel preferences are proven concepts, not speculation.
  What is missing there is the provider abstraction — Matrix is a first-class citizen of the host.
- **ClubGear today:** 🟡 `INotificationService` with a flat
  `NotificationMessage(Recipient, Subject, Body, Channel)`, two channels (`InAppNotificationChannel`,
  `SmtpEmailNotificationChannel`) and a `NotificationRecord` table. No inbox UI, no preferences,
  no capability negotiation, no outbox, no provider contract.

### CORE-05 Versioned templates
- **Clubmanager:** five controllers — `AdminTemplateController`, `AdminTemplateManagerController`,
  `AdminTemplatePortalController` (40 KB, incl. `History`, `Rollback`, `RollbackConfirm`),
  `TemplatesV2Controller`, `TemplateMappingsV2Controller`. Services: `TemplateVersioningService`,
  `TemplateMigrationService`, `ChannelTemplateComposer`. Tables: `MailTemplates`,
  `MailTemplateVersions`, `TemplateChannels`, `TemplateMappings`, `TemplateAuditLogs`,
  `NewsletterTemplates`.
- **Notable:** versioning and rollback already work — but in **two parallel systems** (V1 portal and
  V2 controllers). That is exactly the situation the ClubGear backlog forbids repeating.
- **ClubGear today:** 🟡 `SimpleTemplateRenderer` + `MessageComposer`, no versions, no event map.

### CORE-06 Workflow engine
- **Clubmanager:** generic tables exist (`Workflows`, `WorkflowStates`, `WorkflowTransitions`,
  `WorkflowInstances`, `WorkflowInstanceHistories`) with `AdminWorkflowController` and
  `WorkflowSeedController` (24 KB of seeding) — **and** a separate hard-coded
  `RentalStateMachine` (14 KB) next to it.
- **Notable:** the legacy system built the generic engine and then bypassed it. The proposal's rule
  ("no rental workflow in core, no domain state machine beside the engine") is a direct response.
- **ClubGear today:** ❌ missing.

### CORE-07 Calendar and events
- **Clubmanager:** `CalendarController` (21 KB: `AssignCalendar`, `ManageMapping`, `TriggerImport`,
  meeting-exception CRUD), `AdminCalendarController` (22 KB), `NextcloudCalendarService` (42 KB).
  Tables: `Events`, `EventRegistrations`, `RegularMeetings`, `RegularMeetingExceptions`,
  `NextcloudCalendars`, `NextcloudCalendarConfigs`, `LocalUnitCalendarMappings`.
- **Notable:** Nextcloud is woven into the calendar core. Splitting core model from connector is the
  main architectural work, not the event model itself.
- **ClubGear today:** ❌ missing.

### CORE-08 Household relationships
- **Clubmanager:** one table `FamilyMembers`, three actions in `MembersController`
  (`AddFamilyMember`, `RemoveFamilyMember`, `SetAsFamilyMainMember`).
- **Notable:** the thinnest legacy feature of all the P0/P1 topics. There is little to port; the
  work is the validation and privacy rules the backlog asks for.
- **ClubGear today:** 🟡 member hierarchy exists, relationship semantics do not.

### INV-01 Inventory 2.0
- **Clubmanager:** the most complete legacy domain. `InventoryController` 128 KB / 55 actions,
  `MobileInventoryController` 70 KB, `AdminInventoryReferenceCatalogController` 12 KB,
  `ToolRentalController`, `WarehousePermissionService` (17 KB), 30 views. Tables: `InventoryItems`,
  `InventoryHistories`, `InventoryImages`, `InventoryItemReferenceNumbers`,
  `InventoryReferenceCatalogEntries`, `InventoryReferenceTypeOptions`, `InventoryNotifications`,
  `Warehouses`, `StorageLocations`, `WarehousePermissions`, `ItemCategories`, `Tools`, `ToolImages`,
  `ToolRentals`.
- **Notable:** QR generation, bulk import, Excel export, warehouse sharing and an inventory-taking
  mode all exist. INV-01 is mostly a re-cut port, which is why it is XL but low-risk.
- **ClubGear today:** 🟡 `clubgear.plugin.inventory` — item CRUD only.

### FIN-01 Finance 2.0
- **Clubmanager:** `FinanceController` 73 KB with the matching workflow already built
  (`MemberFeePaymentImport`, `LoadAutomatches`, `MatchAllPayments`, `PaymentMatchMember`,
  `UnmatchPayment`, `UnmatchAllPayments`, `SaveMatchedMembers`, `ExportMatchedPaymentsCsv`,
  bulk verify/invalidate/delete of bank accounts). Tables: `BankAccounts`, `Payments`,
  `PaymentTypes`, `MemberFeePaymentImports`. Only 3 views — much of the finance UI lives inside
  `MembersController`.
- **ClubGear today:** 🟡 `clubgear.plugin.finance` — versioned bank data, SEPA mandates, accounts,
  bookings, fee demands, pain.008 export at differing maturity. Import and matching are the gap.

### NEWS-01 Mass mail and newsletter
- **Clubmanager:** `MassMailController` 51 KB / 24 actions including `FetchBounces`,
  `RescanMailbox`, `RevalidateEmail`, `InvalidEmails`, and a complete postal fallback run
  (`InvalidEmailLetterRun`, `…Resume`, `…Preview`, `…PreviewPdf`, `…Proof`, `…Dispatch`).
  Services: `MassMailBackgroundService` (21 KB), `MassMailRunPolicy`, `EmailSuppressionService`,
  `EmailSyncService` (31 KB), `ListmonkService` (19 KB), `InvalidEmailLetterService` (70 KB).
  Tables: `MassMails`, `MassMailRecipients`, `MassMailAttachments`, `AllowedNewsletterLists`,
  `EmailDeliveryIssues`, `BounceInboxStates`.
- **Notable:** bounce handling and suppression are mature. The 70 KB `InvalidEmailLetterService`
  is the strongest argument for POST-01 being a separate provider plugin rather than newsletter code.
- **ClubGear today:** ❌ missing; but `IImapMailboxService`, `BounceMailboxPoller` and
  `MemberEmailInvalidationService` already exist in core as building blocks.

## Cross-cutting observations

1. **Three features were built twice in the legacy system** — templates (V1 portal + V2 controllers),
   workflow (generic engine + `RentalStateMachine`), and rental/lending (`ToolRental` +
   `MarketplaceRental`). The backlog already forbids repeating each one; the survey confirms the
   risk is real rather than theoretical.
2. **Uploads were built at least seven times.** CORE-02 pays for itself on the first two consumers.
3. **The legacy inbox already works.** CORE-04's genuinely new part is the provider contract, not the
   inbox — which lowers the risk of that item and raises the value of getting the contract right.
4. **Keycloak is deeply embedded** in the legacy organisation model (five group tables). ClubGear's
   decision to keep local roles authoritative means this is a rewrite, not a port.
5. **Nextcloud is wired directly into the legacy code**, not behind an abstraction:
   `NextcloudCalendarService` 42 KB, `NextcloudDocumentService` 26 KB, `WebDavService` 19 KB. Both
   `NextcloudCalendarConfig` and `NextcloudDocumentConfig` store the app password in a plain
   `string` field carrying a `// TODO: Verschlüsseln` / `// TODO: Encrypt in production` comment.
   This is the concrete argument for the port-and-adapter shape in decision D6 and for keeping the
   credential in a plugin-namespaced encrypted store.
6. **Two features exist in Clubmanager and appear nowhere in the ClubGear backlog:** the local-unit
   radius search (`RadiusSearch` / `ExportRadiusSearch`) and birthday greetings automation
   (`BirthdayAutomationBackgroundService`, `BirthdayGreetingService`, `BirthdayGreetingDispatchLogs`).
   The second is mentioned in `TodoFeatures.md` as a small plugin; the first is not mentioned at all.
