from dio import Page

def page_context():
    p = Page("1 · System context", 1600, 1060)
    p.text(40, 24, 900, 30, "ClubGear — system context (target)", 20, "#0077BA", bold=True)
    p.text(40, 54, 1100, 40,
           "Who uses the system and which external systems it talks to. Every external integration is reached "
           "through a plugin; the core never contains vendor-specific code.", 12)

    # actors
    p.band(40, 110, 300, 470, "People")
    actors = [
        ("Member\nself-service, registrations, inbox", 140),
        ("Board / administrator\nmembers, organisation, plugins", 210),
        ("Treasurer\nfee runs, payments, SEPA", 280),
        ("Unit administrator\ndelegated scope only", 350),
        ("Editor / warehouse manager\ncontent, inventory", 420),
        ("Mobile app / bot\nservice account + API key", 490),
    ]
    aids = []
    for label, y in actors:
        aids.append(p.node(60, y, 260, 56, label, "actor", fontsize=11))

    # system
    sys_id = p.node(430, 190, 420, 300,
                    "ClubGear\n\nSelf-hosted ASP.NET Core 8 application\nCore domain + in-process plugin host\n\n"
                    "One identity, one permission model,\none audit trail, one storage service",
                    "core_new", fontsize=13, bold=True)
    p.text(430, 500, 420, 20, "Trust boundary: everything inside is owned by the association.", 11, "#5f6368", align="center")

    # externals
    p.band(960, 110, 600, 470, "External systems — reached through plugins or core channels")
    ext = [
        ("SMTP / IMAP mailbox", "core channel", 140),
        ("OIDC identity provider (Keycloak)", "IamOidc plugin", 200),
        ("Nextcloud — CalDAV + WebDAV", "optional storage backend, plugin", 260),
        ("Matrix / Telegram / SMS gateway", "channel-provider plugins", 320),
        ("Bank — camt.053 in, pain.008 out", "Finance plugin", 380),
        ("Listmonk / postal provider / Traccar", "optional adapter plugins", 440),
    ]
    eids = []
    for label, sub, y in ext:
        eids.append(p.node(980, y, 560, 48, f"{label}   ·   {sub}", "ext", fontsize=11))

    for i, a in enumerate(aids):
        p.edge(a, sys_id, "", "#8a949c", exitp=(1, 0.5),
               entryp=(0, round(0.10 + i * 0.16, 2)))
    labels = ["e-mail, bounces", "login, claims", "files + calendars (optional store)", "messages, receipts",
              "payment files", "campaigns, letters, tracks"]
    for i, (e, lab) in enumerate(zip(eids, labels)):
        p.edge(sys_id, e, lab, "#5b3d86", dashed=1,
               exitp=(1, round(0.10 + i * 0.16, 2)), entryp=(0, 0.5))

    p.band(40, 610, 1520, 200, "Principles carried into the target architecture")
    notes = [
        ("Core owns the platform", "identity, permissions, members, organisation, files, templates,\nnotifications, workflow, calendar, audit."),
        ("Plugins own the domain", "inventory, finance, newsletter, rental, documents, connectors —\neach with its own schema, permissions and release cycle."),
        ("No vendor in core", "Matrix, Nextcloud, Listmonk, Traccar and postal services are\nprovider plugins behind generic contracts."),
        ("One of everything", "one template system, one file service, one audit trail,\none inbox — no parallel legacy paths."),
    ]
    x = 60
    for title, body in notes:
        p.text(x, 646, 350, 20, title, 13, "#16354f", bold=True)
        p.text(x, 670, 350, 90, body, 11.5)
        x += 375
    return p


def page_layers():
    p = Page("2 · Target architecture", 1700, 1200)
    p.text(40, 24, 1000, 30, "Target architecture — layers and ownership", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "Blue = ClubGear core.  Orange = the versioned plugin contract.  Purple = plugins (own repository, own SemVer, own schema).  "
           "Dashed = new or substantially extended by this proposal.", 12)

    # presentation
    p.band(40, 110, 1620, 120, "Presentation — one rendering pipeline")
    pres = [("Razor views\nmembers, organisation, admin", 60), ("Host-rendered plugin surfaces\npages, forms, tables, slots", 460),
            ("JSON API\ncore + namespaced plugin routes", 860), ("Mobile / service clients\nAPI key or service account", 1260)]
    for label, x in pres:
        p.node(x, 146, 380, 66, label, "core", fontsize=11)

    # core services
    p.band(40, 250, 1620, 300, "Core application services")
    row1 = [
        ("Identity, roles,\npermissions", "core", 0),
        ("Members, membership\ntypes, self-service", "core", 0),
        ("Organisation units\nCORE-01", "core_new", 1),
        ("Household relations\nCORE-08", "core_new", 1),
        ("Calendar and events\nCORE-07", "core_new", 1),
        ("Workflow engine\nCORE-06", "core_new", 1),
    ]
    row2 = [
        ("File and media storage\nCORE-02", "core_new", 1),
        ("Templates and events\nCORE-05", "core_new", 1),
        ("Notification dispatch\n+ inbox  CORE-04", "core_new", 1),
        ("Audit, event log,\nsystem config", "core", 0),
        ("API keys / service\naccounts  CORE-09", "core_new", 1),
        ("Branding, navigation,\ni18n  CORE-10/11", "core_new", 1),
    ]
    for i, (label, kind, dash) in enumerate(row1):
        p.node(60 + i * 266, 286, 246, 76, label, kind, fontsize=11, dashed=dash)
    for i, (label, kind, dash) in enumerate(row2):
        p.node(60 + i * 266, 380, 246, 76, label, kind, fontsize=11, dashed=dash)
    p.text(60, 470, 1580, 40,
           "Rule: a core service never branches on a module id. Everything a plugin needs is expressed as a contract capability.",
           11.5, "#5f6368")

    # contract
    p.band(40, 570, 1620, 170, "Plugin contract — ClubGear.Plugin.Contracts, SemVer, no core reference")
    caps = ["Pages, forms, tables,\nfilters, pagination", "Typed commands\n+ validation schema",
            "Namespaced JSON\nendpoints", "File upload / download\nvia CORE-02",
            "Notification channel\nproviders", "Template events\n+ placeholders",
            "Workflow definitions", "Member / self-service /\nadmin slots"]
    for i, label in enumerate(caps):
        x = 60 + (i % 4) * 400
        y = 606 + (i // 4) * 66
        p.node(x, y, 380, 56, label, "contract", fontsize=11)

    # runtime
    p.band(40, 760, 1620, 120, "Plugin runtime — host side")
    rt = ["Loader + isolated\nassembly context", "Registry and\nlifecycle", "Manifest + signature\nverification",
          "Contract version gate", "Migrations and\nschema policy", "Background job\nrunner"]
    for i, label in enumerate(rt):
        p.node(60 + i * 266, 796, 246, 66, label, "runtime", fontsize=11)

    # plugins
    p.band(40, 900, 1620, 130, "Plugins — independent repositories and versions")
    plugs = [("CarInfo", 0), ("ServiceBook", 0), ("Inventar → INV-01", 1), ("Finance → FIN-01", 1), ("IamOidc", 0),
             ("Newsletter (new)", 1), ("CalDAV connector (new)", 1), ("Channel providers (new)", 1)]
    for i, (label, dash) in enumerate(plugs):
        p.node(60 + i * 200, 940, 182, 62, label, "plugin", fontsize=11, dashed=dash)

    # data
    p.band(40, 1050, 1620, 110, "Persistence")
    p.node(60, 1088, 500, 56, "Core schema — EF Core migrations", "data", fontsize=11)
    p.node(580, 1088, 540, 56, "Plugin schemas — prefixed tables, per-plugin migrations", "data", fontsize=11)
    p.node(1140, 1088, 500, 56, "File / calendar backend — local (core) or Nextcloud (plugin)", "data", fontsize=11)
    return p
