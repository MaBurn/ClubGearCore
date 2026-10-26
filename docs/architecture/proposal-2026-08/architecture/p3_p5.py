from dio import Page


def page_dependencies():
    p = Page("3 · Capability map and dependency order", 1700, 980)
    p.text(40, 24, 1200, 30, "Capability map — what depends on what", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "Each block can only start once the items it names under »needs« exist. The four columns are therefore a "
           "sequence, not four parallel tracks.", 12)

    cols = [
        (60, "Phase 1 — platform foundation (P0)", [
            ("CORE-03  Plugin surfaces and contracts", "L   ·   needs: nothing   ·   unblocks every large plugin", "core_new", True),
            ("CORE-02  Secure file and media storage", "M (+S for the backend port)   ·   needs: system config, audit", "core_new", True),
            ("CORE-01  Organisation structure", "L   ·   needs: members, roles, audit", "core_new", True),
            ("CORE-08  Household relationships", "M   ·   needs: members, membership types", "core_new", False),
            ("CORE-12  Audit and changelog views", "S–M   ·   needs: existing audit services", "core", False),
            ("CORE-09  API keys and service accounts", "M   ·   needs: permissions, audit, CORE-03", "core", False),
            ("CORE-10/11  Branding, navigation, i18n", "S–M / M   ·   needs: CORE-02", "core", False),
        ]),
        (480, "Phase 2 — communication and generic processes", [
            ("CORE-05  Versioned templates", "L   ·   needs: CORE-02, CORE-03", "core_new", True),
            ("CORE-04  Notification inbox and channels", "L   ·   needs: CORE-05, CORE-03, CORE-01", "core_new", True),
            ("CORE-06  Workflow engine", "L   ·   needs: CORE-03, audit, jobs", "core_new", False),
            ("CORE-07  Calendar and events\n+ calendar store port", "L + M   ·   needs: CORE-01, CORE-04", "core_new", False),
        ]),
        (900, "Phase 3 — complete the existing plugins", [
            ("INV-01  Inventory 2.0", "XL   ·   needs: CORE-03, CORE-02, CORE-01, CORE-04", "plugin", True),
            ("FIN-01  Finance 2.0", "XL   ·   needs: CORE-05, CORE-08, CORE-03", "plugin", True),
            ("CarInfo / ServiceBook parity", "S   ·   needs: CORE-03", "plugin", False),
            ("NC-01  Nextcloud plugin — WebDAV file backend\n+ CalDAV calendar store", "M–L   ·   needs: CORE-02 port, CORE-07 port", "plugin", True),
            ("Channel providers — Matrix, Telegram,\nSMS, E-Post", "S–M each   ·   needs: CORE-04", "plugin", False),
        ]),
        (1320, "Phase 4+ — new domain plugins", [
            ("NEWS-01  Mass mail and newsletter", "XL   ·   needs: CORE-04, CORE-05, CORE-02", "plugin", False),
            ("RENT-01  Marketplace and rental", "XL   ·   needs: INV-01, CORE-06, FIN-01 contract", "plugin", False),
            ("EDIT-01  Editorial and publication", "XL   ·   needs: CORE-01, CORE-02, CORE-04, CORE-05", "plugin", False),
            ("DOC-01 / NC-DOC-01  Documents", "M + M   ·   needs: CORE-02, CORE-03", "plugin", False),
            ("POST-01  Postal letters", "L   ·   needs: CORE-05, CORE-02, CORE-09", "plugin", False),
            ("TREE-01, ORGVIEW-01, CHAT-01,\nGPS-01, CMS-01", "L / M / L / L / M   ·   optional, independent", "plugin", False),
        ]),
    ]

    heads = []
    for x, title, items in cols:
        p.band(x, 110, 380, 620, title)
        heads.append(None)
        y = 150
        for name, needs, kind, bold in items:
            lines = name.count("\n") + 1
            h = 62 + (lines - 1) * 14
            p.node(x + 20, y, 340, h, name + "\n" + needs, kind, fontsize=11, bold=bold)
            y += h + 16

    a1 = p.node(400, 760, 60, 40, "→", "core_new", fontsize=18, bold=True)
    a2 = p.node(820, 760, 60, 40, "→", "core_new", fontsize=18, bold=True)
    a3 = p.node(1240, 760, 60, 40, "→", "plugin", fontsize=18, bold=True)
    p.text(60, 760, 340, 40, "Platform first — everything else is\nblocked or duplicated without it.", 11, "#5f6368")
    p.text(480, 760, 340, 40, "Communication foundation — the last\nthing shared by every later module.", 11, "#5f6368")
    p.text(900, 760, 340, 40, "Finish what is already installed before\nadding new domains.", 11, "#5f6368")
    p.text(1320, 760, 340, 40, "New domains, in the order the club\nactually needs them.", 11, "#5f6368")

    p.text(60, 830, 1580, 24, "Critical path for Clubmanager parity", 13, "#16354f", bold=True)
    p.node(60, 856, 1580, 40,
           "CORE-03  →  CORE-02 / CORE-04 / CORE-05  →  CORE-06  →  INV-01  →  RENT-01           "
           "·           organisation-bound:  CORE-01  →  CORE-07  →  NC-01   and   CORE-01  →  EDIT-01",
           "warn", fontsize=12, bold=True)
    p.text(60, 910, 1580, 30,
           "Effort scale: XS 1–2 PT · S 3–5 · M 6–12 · L 13–25 · XL 26–50+.   P0 core work alone is roughly 60–90 PT; "
           "INV-01 and FIN-01 together are of the same order again.", 11)
    return p


def page_notifications():
    p = Page("4 · Notification and template dispatch", 1700, 1030)
    p.text(40, 24, 1100, 30, "CORE-04 + CORE-05 — how one event reaches a person", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "Core resolves recipients, preferences, template and capabilities. Providers only translate an already "
           "rendered, provider-neutral message.", 12)

    emit1 = p.node(60, 150, 260, 60, "Core service\ne.g. member.welcome", "core", fontsize=11)
    emit2 = p.node(60, 230, 260, 60, "Plugin\nvia notification facade", "plugin", fontsize=11)
    fac = p.node(380, 180, 240, 110, "Notification facade\nsemantic event key\n+ payload\n+ idempotency key", "core_new", fontsize=11, bold=True)

    rec = p.node(680, 130, 250, 70, "Recipient resolution\nuser · member · role ·\nunit role", "core_new", fontsize=11)
    pref = p.node(680, 220, 250, 70, "Preference + policy\nmandatory events cannot\nbe switched off", "core_new", fontsize=11)
    tpl = p.node(680, 310, 250, 70, "Template resolution\nevent + channel + locale\n→ pinned version", "core_new", fontsize=11)

    render = p.node(990, 220, 230, 70, "Render\ndeterministic,\nHTML-encoded", "core_new", fontsize=11)
    cap = p.node(990, 310, 230, 70, "Capability check\ntext / html / subject /\nattachments / length", "core_new", fontsize=11)

    outbox = p.node(1280, 265, 230, 80, "Outbox\npersisted, retryable,\nidempotent per\n(key, recipient)", "core_new", fontsize=11, bold=True)

    inapp = p.node(1280, 380, 230, 56, "In-app inbox   (core)", "core", fontsize=11)
    mail = p.node(1280, 450, 230, 56, "SMTP e-mail   (core)", "core", fontsize=11)
    prov1 = p.node(1280, 520, 230, 56, "Matrix provider   (plugin)", "plugin", fontsize=11)
    prov2 = p.node(1280, 590, 230, 56, "Telegram provider   (plugin)", "plugin", fontsize=11)
    prov3 = p.node(1280, 660, 230, 56, "E-Post provider   (plugin)", "plugin", fontsize=11)

    status = p.node(940, 560, 280, 90, "Delivery status\naccepted / delivered / failed / unknown\n+ opaque provider reference\n"
                                       "async callback or polling", "core_new", fontsize=11)
    fail = p.node(600, 560, 280, 90, "Administrator view\nfailed deliveries, redacted\ncontent, manual retry", "core", fontsize=11)

    E = p.edge
    E(emit1, fac); E(emit2, fac)
    E(fac, rec); E(fac, pref); E(fac, tpl)
    E(rec, render, "", "#0077BA", dashed=1); E(pref, render); E(tpl, render)
    E(render, cap); E(cap, outbox)
    E(outbox, inapp); E(outbox, mail); E(outbox, prov1); E(outbox, prov2); E(outbox, prov3)
    E(prov1, status, "", "#5b3d86", dashed=1); E(prov2, status, "", "#5b3d86", dashed=1)
    E(prov3, status, "", "#5b3d86", dashed=1); E(mail, status, "bounce via IMAP", "#0077BA", dashed=1)
    E(status, fail)

    p.band(60, 750, 1460, 240, "Decisions this diagram commits to")
    dec = [
        ("Email is the only outbound channel in core",
         "Everything else — Matrix, Telegram, SMS, E-Post — is an installable\nprovider plugin. Core has no provider branch, key, icon or DTO."),
        ("Fallback is explicit per event",
         "queue and retry · fall back to e-mail · inbox only · fail and report.\nCore must never silently pick another medium."),
        ("Capability negotiation, not assumption",
         "An event declares what it needs; a provider advertises what it supports.\nA provider that cannot carry an attachment is not selected."),
        ("Provider lifecycle never destroys history",
         "Deactivating or removing a provider hides the channel and keeps\nnotifications, delivery records and user preferences intact."),
    ]
    x = 80
    for title, body in dec:
        p.text(x, 786, 340, 20, title, 12.5, "#16354f", bold=True)
        p.text(x, 812, 340, 90, body, 11, "#5f6368")
        x += 360
    return p


def page_plugin_surface():
    p = Page("5 · Plugin surface request lifecycle", 1700, 1000)
    p.text(40, 24, 1100, 30, "CORE-03 — what happens when a plugin page is opened", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "The host performs every security-relevant step. The plugin contributes data and executes commands. "
           "The same definition serves the HTML page and its JSON twin.", 12)

    br = p.node(60, 180, 200, 70, "Browser\nor mobile client", "ext", fontsize=11)
    ctl = p.node(320, 180, 240, 70, "Host controller\nPluginPage / API", "core", fontsize=11)

    p.band(620, 120, 420, 400, "Host pipeline — always, for every plugin")
    steps = ["Authentication", "Anti-CSRF", "Permission check\n(declared key, unit-scoped)",
             "Contract version gate", "Model validation\nagainst the declared schema", "Output encoding + messages"]
    ids = []
    for i, s in enumerate(steps):
        ids.append(p.node(640, 160 + i * 58, 380, 48, s, "core_new", fontsize=11))

    contract = p.node(1100, 200, 260, 110, "Plugin contract call\nGetPageDefinition\nGetRows\nExecuteCommand", "contract", fontsize=11, bold=True)
    plug = p.node(1420, 200, 220, 110, "Plugin module\nbusiness logic only", "plugin", fontsize=11, bold=True)

    host_ctx = p.node(1100, 380, 260, 130, "IPluginHostContext\nmember read model\npermission facade\nnotification facade\naudit facade\nfile storage (CORE-02)", "contract", fontsize=11)
    store = p.node(1420, 380, 220, 130, "IPluginDataStore\nprefixed tables,\nown migrations,\nno core table access", "data", fontsize=11)

    E = p.edge
    E(br, ctl, "GET / POST")
    E(ctl, ids[0])
    for a, b in zip(ids, ids[1:]):
        E(a, b)
    E(ids[5], contract, "")
    E(contract, plug)
    E(plug, host_ctx, "", "#5b3d86", dashed=1)
    E(plug, store, "", "#5b3d86")
    E(contract, ctl, "definition + rows + result", "#EF7D00", dashed=1,
      style_extra="exitX=0;exitY=0.25;entryX=1;entryY=0.25;")

    p.band(60, 560, 1580, 190, "What the contract must gain — and what it must never gain")
    p.text(80, 596, 740, 20, "Add (generic UI and transport concepts)", 12.5, "#187344", bold=True)
    p.text(80, 620, 740, 120,
           "· typed action arguments instead of string dictionaries\n"
           "· filters, sorting, pagination, detail views, confirmation actions\n"
           "· validated upload and streamed download actions\n"
           "· namespaced JSON endpoints with a documented error envelope\n"
           "· activation-time validation of keys, routes, permissions and schemas", 11.5)
    p.text(860, 596, 760, 20, "Never add", 12.5, "#b05252", bold=True)
    p.text(860, 620, 760, 120,
           "· Inventory-, Finance- or any product-specific DTO in the contract\n"
           "· a core branch that compares a module id to choose behaviour\n"
           "· plugin access to core tables or a core reference from a plugin\n"
           "· raw HTML from a plugin rendered without escaping\n"
           "· a second out-of-process / gRPC plugin mechanism", 11.5)

    p.band(60, 780, 1580, 170, "Migration of the existing plugins")
    p.text(80, 816, 1540, 120,
           "CarInfo, ServiceBook, Inventar, Finance and IamOidc already run on contract 1.10.0. The extension is additive: new "
           "optional interfaces and new contribution kinds, with the minimum-supported version raised only when an old shape is "
           "actually removed.\n\n"
           "Practical rule from the existing guardrails: before each domain plugin is built, decide the smallest contract increment it "
           "needs, ship that increment with compatibility tests, and only then build the plugin. Never widen the contract speculatively.",
           11.5)
    return p
