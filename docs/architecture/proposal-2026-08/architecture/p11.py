from dio import Page


def page_do_not_execute():
    p = Page("11 · Do not execute", 1700, 1180)
    p.text(40, 24, 1300, 30, "Do not execute — the decisions that are as binding as the plan", 20, "#0077BA", bold=True)
    p.text(40, 54, 1420, 40,
           "Every item here was either written into the specifications as »out of scope«, or observed in Clubmanager "
           "as something that cost real money. A plan without these is only half a plan.", 12)

    # --- platform rules
    p.band(40, 110, 1620, 470, "Architecture rules — violating one of these invalidates the boundary, not just the task")
    rules = [
        ("DNX-01", "No provider-specific code in the core",
         "Matrix, Telegram, WhatsApp, SMS, E-Post, Nextcloud, Listmonk, Traccar and postal services are\n"
         "plugins behind generic contracts. No provider DTO, config key, icon or destination format in core.",
         "Clubmanager keeps a MatrixAgent in the host and a 42 KB NextcloudCalendarService in Services/."),
        ("DNX-02", "No core branch on a module id",
         "The host must never compare a plugin id to select behaviour, and the contract must never carry an\n"
         "Inventory-, Finance- or Newsletter-specific type.",
         "This is the rule that keeps CORE-03 an investment instead of a growing switch statement."),
        ("DNX-03", "No cross-schema access in either direction",
         "A plugin does not read core tables; the core does not read plugin tables. Everything goes through a\n"
         "read model or a contract facade.",
         "Named explicitly in the Inventory 2.0 and Finance 2.0 specs."),
        ("DNX-04", "No silent fallback",
         "If a notification channel or a storage backend is unavailable, the declared policy applies and the\n"
         "operation reports it. Never quietly send through another medium or write to the other store.",
         "Applies to CORE-04 and, since the Nextcloud decision, to CORE-02 and CORE-07 as well."),
        ("DNX-05", "No second plugin architecture",
         "No out-of-process or gRPC plugin host next to the existing in-process contract host, and no\n"
         "server-side Razor assembly loaded outside contract control.",
         "Clubmanager's out-of-process attempt is explicitly not being ported."),
        ("DNX-06", "No parallel template systems",
         "Exactly one versioned template system. The existing simple renderer is replaced, not kept beside\n"
         "CORE-05.",
         "Clubmanager runs a V1 template portal and V2 controllers side by side — the mistake to avoid."),
        ("DNX-07", "No paths and no secrets in ordinary configuration",
         "Controllers never touch the filesystem directly, and credentials never live in a plain config value.\n"
         "Secrets are encrypted at rest, redacted in UI and logs.",
         "Both Nextcloud config tables store the app password with a // TODO: Verschlüsseln comment."),
        ("DNX-08", "No hard-coded state machine beside the workflow engine",
         "If CORE-06 is built, rental and approvals register definitions in it. If CORE-06 is not built,\n"
         "that is a conscious cut — not a licence to hard-code states.",
         "Clubmanager built 5 workflow tables and then bypassed them with a 14 KB RentalStateMachine."),
    ]
    y = 150
    for i, (cid, title, body, ev) in enumerate(rules):
        x = 60 if i % 2 == 0 else 860
        yy = y + (i // 2) * 108
        p.node(x, yy, 780, 96, "", "danger", rounded=1)
        p.text(x + 16, yy + 10, 70, 18, cid, 11.5, "#8a2f2f", bold=True)
        p.text(x + 80, yy + 8, 680, 20, title, 13.5, "#8a2f2f", bold=True)
        p.text(x + 16, yy + 32, 750, 40, body, 11)
        p.text(x + 16, yy + 72, 750, 20, "Evidence: " + ev, 10.5, "#7a5310")

    # --- porting rules
    p.band(40, 600, 800, 300, "Do not port these, even though they exist and work")
    ports = [
        ("Thousand-line controllers as a porting unit",
         "MembersController is 277 KB, InventoryController 128 KB, FinanceController 73 KB. Each domain gets\nservices, commands/queries, thin endpoints and its own tests."),
        ("ReferateController as a set of hard-coded view names",
         "Departments become configurable organisation units (CORE-01) plus plugin navigation."),
        ("Keycloak group management as the local authority",
         "Local roles stay authoritative; external group mapping is IamOidc plugin work."),
        ("Debug controllers as productive routes",
         "Token and claims pages are development-only diagnostics, not a product feature."),
        ("ToolRental as a second plugin",
         "Simple lending belongs in Inventory 2.0; commercial rental in RENT-01. Never both."),
    ]
    yy = 640
    for title, body in ports:
        p.text(60, yy, 780, 18, "✕  " + title, 12.5, "#8a2f2f", bold=True)
        p.text(80, yy + 18, 740, 34, body, 10.8)
        yy += 52

    # --- per feature
    p.band(880, 600, 780, 300, "Out of scope per feature — taken verbatim from the specifications")
    feats = [
        ("CORE-01", "hard-coded department pages · Keycloak groups as authority · business data"),
        ("CORE-02", "domain image galleries · a mandatory cloud vendor or scanner · public CMS routing"),
        ("CORE-03", "a second plugin architecture · plugin-specific behaviour in core · free Razor assemblies"),
        ("CORE-04", "person-to-person chat · shipping any third-party provider in core · campaign management"),
        ("CORE-05", "campaign selection and scheduling · a scripting language in templates · SMTP/Listmonk config"),
        ("CORE-06", "a visual BPMN engine · hard-coded rental seed workflows · domain forms and calculations"),
        ("CORE-07", "Nextcloud entities or credentials in core · meeting minutes · room booking in release 1"),
        ("CORE-08", "shared login credentials · automatic guardianship · fee or discount calculation in core"),
        ("INV-01", "deposits, payouts, damage settlement · direct member/finance/org table access · a second ToolRental"),
        ("FIN-01", "general accounting, tax, payroll · rental deposits before a contract exists · bank data in core"),
        ("NEWS-01", "postal delivery · editing core member records · a hard dependency on Listmonk"),
    ]
    yy = 638
    for cid, body in feats:
        p.text(900, yy, 90, 16, cid, 11, "#5f6368", bold=True)
        p.text(985, yy, 660, 16, body, 10.8)
        yy += 23

    # --- how to use
    p.band(40, 920, 1620, 230, "How this list is meant to be used")
    p.text(60, 958, 500, 20, "It is a review checklist, not a manifesto", 12.5, "#187344", bold=True)
    p.text(60, 984, 500, 150,
           "Each DNX item is phrased so it can be checked against a diff.\n"
           "»Does this change add a provider name to the core?« has a yes/no\n"
           "answer; »keep the architecture clean« does not.\n\n"
           "Four of them can be enforced by an automated test rather than by\n"
           "review: DNX-01, DNX-02, DNX-03 and DNX-06.", 11)
    p.text(600, 958, 500, 20, "Breaking one is allowed — silently is not", 12.5, "#c98a1e", bold=True)
    p.text(600, 984, 500, 150,
           "There will be a day when shipping a provider inside the core is the\n"
           "pragmatic choice. The rule is not »never«, it is: write down which\n"
           "item you are breaking, why, and what would undo it.\n\n"
           "That is the difference between a deliberate exception and the\n"
           "state Clubmanager arrived at over several years.", 11)
    p.text(1140, 958, 500, 20, "What is deliberately NOT on this list", 12.5, "#0077BA", bold=True)
    p.text(1140, 984, 500, 150,
           "Features the club may simply not want — birthday greetings, GPS\n"
           "tracking, chat, CMS. Those are priority decisions and belong in the\n"
           "pitch deck, not here.\n\n"
           "This page is only about things that would damage the architecture\n"
           "or repeat a known, paid-for mistake.", 11)
    return p
