from dio import Page


def page_files():
    p = Page("6 · Secure file storage", 1600, 1020)
    p.text(40, 24, 1100, 30, "CORE-02 — one storage service, opaque references", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "Business modules store a file id and metadata. Nobody writes into wwwroot, nobody trusts a client path, "
           "and no plugin can reach another plugin's files.", 12)

    callers = [("Profile image (core)", "core"), ("Template assets (core)", "core"),
               ("Inventory images (plugin)", "plugin"), ("Finance imports (plugin)", "plugin"),
               ("Newsletter attachments (plugin)", "plugin"), ("Documents (plugin)", "plugin")]
    cids = []
    for i, (label, kind) in enumerate(callers):
        cids.append(p.node(60, 150 + i * 70, 260, 56, label, kind, fontsize=11))

    api = p.node(380, 300, 220, 180, "IFileStorage\nupload · read · stream\nreplace · delete\nnamespaced per scope", "core_new", fontsize=11, bold=True)

    p.band(660, 130, 400, 470, "Upload pipeline — same gates for core and plugins")
    gates = ["Policy lookup for the owner scope", "Size and quota limits",
             "Extension and declared MIME", "Content signature check",
             "Malware hook → quarantine state", "Generated storage name (no traversal)",
             "Stream to backend, bounded time", "Metadata row + audit entry"]
    gids = []
    for i, g in enumerate(gates):
        gids.append(p.node(680, 168 + i * 52, 360, 42, g, "core_new", fontsize=11))

    meta = p.node(1120, 240, 240, 110, "File metadata\nid · scope · display name ·\nstorage name · media type ·\nsize · checksum · creator", "data", fontsize=11)
    port = p.node(1120, 388, 240, 46, "IFileStorageBackend  (port)", "contract", fontsize=11, bold=True)
    local = p.node(1120, 444, 240, 52, "Local filesystem\ncore · default", "data", fontsize=11)
    ncb = p.node(1120, 508, 240, 52, "Nextcloud WebDAV\nplugin adapter", "plugin", fontsize=11)

    dl = p.node(1120, 600, 240, 90, "Download / stream\nauthorised per owning\nfeature or plugin resource\nsafe disposition headers", "core_new", fontsize=11)

    for c in cids:
        p.edge(c, api)
    p.edge(api, gids[0])
    for a, b in zip(gids, gids[1:]):
        p.edge(a, b)
    p.edge(gids[7], meta)
    p.edge(meta, port, "bytes")
    p.edge(port, local); p.edge(port, ncb, "", "#5b3d86")
    p.edge(api, dl, "", "#0077BA", dashed=1)

    p.band(60, 640, 980, 340, "Rules that make this worth building once")
    rules = [
        "Two files with the same original name never overwrite each other — the storage name is generated.",
        "A plugin can store and read its own file and cannot resolve or enumerate another plugin's file.",
        "Client-supplied MIME type and extension are never trusted on their own.",
        "Large files are streamed; partial uploads are cleaned up; cancellation is honoured.",
        "Deleting a business object marks its files for deletion; cleanup is retryable and reports orphans.",
        "Upload, replacement, deletion and downloads of sensitive files are auditable.",
        "The backend is selectable per installation and optionally per scope — local or Nextcloud (page 9).",
        "Business code, plugins and the metadata model are identical whichever backend is active.",
    ]
    for i, r in enumerate(rules):
        p.text(80, 682 + i * 34, 940, 30, "·  " + r, 11.5, "#3f474d")
    return p


def page_ownership():
    p = Page("7 · Data ownership and isolation", 1600, 980)
    p.text(40, 24, 1100, 30, "Who may read what", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "The boundary that keeps plugins releasable on their own. Green arrows are allowed; red dashed arrows are the "
           "shortcuts that must stay impossible.", 12)

    p.band(60, 120, 640, 520, "Core schema — ApplicationDbContext, EF migrations")
    core_tables = ["Members, addresses, membership types", "Organisation units + memberships (CORE-01)",
                   "Household relationships (CORE-08)", "Users, roles, permissions",
                   "Events, registrations (CORE-07)", "Templates + versions (CORE-05)",
                   "Notifications + preferences (CORE-04)", "Workflow definitions + instances (CORE-06)",
                   "File metadata (CORE-02)", "Audit log, event log, system config"]
    core_ids = []
    for i, t in enumerate(core_tables):
        core_ids.append(p.node(80, 160 + i * 46, 600, 36, t, "core", fontsize=11))

    p.band(940, 120, 600, 520, "Plugin schemas — one prefix per plugin, own migrations")
    plug_tables = [("inv_*  items, warehouses, stock, history, images", "plugin"),
                   ("fin_*  accounts, mandates, demands, payments, batches", "plugin"),
                   ("news_* campaigns, recipient snapshots, consent", "plugin"),
                   ("rent_* listings, orders, deposits, damages", "plugin"),
                   ("car_*  vehicles     ·     sb_*  service records", "plugin"),
                   ("cal_*  external calendar mappings and sync state", "plugin"),
                   ("chan_* provider config (encrypted) + destinations", "plugin")]
    plug_ids = []
    for i, (t, k) in enumerate(plug_tables):
        plug_ids.append(p.node(960, 160 + i * 66, 560, 52, t, k, fontsize=11))

    rm = p.node(730, 250, 180, 150, "Read models\nand facades\n\nmember summary\nunit reference\nhousehold snapshot\npermission check", "contract", fontsize=11, bold=True)

    code = p.node(1180, 660, 340, 60, "Plugin code (in-process, contract only)", "plugin", fontsize=11, bold=True)

    p.edge(code, rm, "allowed", "#187344", exitp=(0, 0.5), entryp=(1, 0.75))
    p.edge(rm, core_ids[0], "authorised read model", "#187344", exitp=(0, 0.35), entryp=(1, 0.5))
    p.edge(rm, core_ids[1], "", "#187344", exitp=(0, 0.55), entryp=(1, 0.5))
    p.edge(rm, core_ids[2], "", "#187344", exitp=(0, 0.75), entryp=(1, 0.5))
    p.edge(code, plug_ids[0], "own tables", "#5b3d86", exitp=(0.5, 0), entryp=(0.5, 1))
    p.edge(code, core_ids[3], "never — no direct table access", "#b05252", dashed=1,
           exitp=(0, 0.85), entryp=(1, 0.85))

    ok = p.node(160, 700, 460, 56, "Allowed:  plugin  →  contract facade  →  core read model", "ok", fontsize=12, bold=True)
    bad = p.node(660, 700, 460, 56, "Forbidden:  plugin  →  core table  ·  core  →  plugin table", "danger", fontsize=12, bold=True)

    p.band(60, 790, 1480, 160, "Consequences")
    p.text(80, 826, 700, 120,
           "· A plugin can be uninstalled without leaving foreign keys behind in the core schema.\n"
           "· Core migrations never have to know a plugin exists.\n"
           "· Finance keeps bank data; it is never merged back into member tables.\n"
           "· A read model is a contract: widening it is a SemVer decision, not a refactor.", 11.5)
    p.text(820, 826, 700, 120,
           "· Rental pays through a Finance contract or event, never through fin_* tables.\n"
           "· Newsletter reads an approved recipient projection, not the member entity.\n"
           "· Plugin removal keeps notification history and template versions intact.\n"
           "· Files belong to a scope, so uninstall policy can decide what to purge.", 11.5)
    return p


def page_roadmap():
    p = Page("8 · Delivery sequence", 1700, 900)
    p.text(40, 24, 1100, 30, "A sequence that keeps the product usable at every step", 20, "#0077BA", bold=True)
    p.text(40, 54, 1300, 40,
           "Each block ends with something a member or administrator can actually use. Estimates are implementation plus tests, "
           "excluding historical data migration.", 12)

    cols = [
        ("Block A — platform", "#0077BA", [
            "CORE-03 contract spike, driven by the real needs of\nInventory, Finance and one upload-heavy plugin",
            "CORE-02 file storage incl. the backend port,\nfirst used by profile images and one plugin slot",
            "CORE-01 organisation units, memberships,\ndelegated administration — no calendar yet",
        ], "≈ 33–50 PT"),
        ("Block B — communication", "#0077BA", [
            "CORE-05 versioned templates + semantic event keys,\nreplacing the ad-hoc renderer",
            "CORE-04 inbox, preferences, provider contract,\nplus one reference channel plugin",
            "CORE-08 household relationships incl. the read model\nFinance will consume",
        ], "≈ 32–50 PT"),
        ("Block C — existing plugins", "#5b3d86", [
            "Capability matrix of Inventar and Finance against the\nClubmanager actions they replace",
            "INV-01 in slices: warehouses+stock → images+codes →\nloans → mobile API",
            "FIN-01 in slices: fee runs → payment import/matching →\nSEPA export → treasurer dashboards",
        ], "≈ 55–90 PT"),
        ("Block D — new modules", "#5b3d86", [
            "CORE-06 workflow, then CORE-07 calendar with the\nstore port, then the Nextcloud plugin (NC-01)",
            "NEWS-01 newsletter, POST-01 letters,\nfurther channel providers",
            "RENT-01, EDIT-01, DOC-01 and the P3 optional set",
        ], "≈ 100+ PT"),
    ]
    x = 60
    for title, color, items, effort in cols:
        p.node(x, 120, 380, 50, title, "core_new" if color == "#0077BA" else "plugin", fontsize=13, bold=True)
        y = 190
        for it in items:
            p.node(x, y, 380, 90, it, "core" if color == "#0077BA" else "plugin", fontsize=11)
            y += 105
        p.node(x, y + 10, 380, 44, effort, "warn", fontsize=12, bold=True)
        x += 400

    p.band(60, 640, 1580, 210, "Two decisions worth taking before Block A starts")
    p.text(80, 680, 760, 20, "1.  How much Clubmanager parity is actually required?", 13, "#16354f", bold=True)
    p.text(80, 706, 760, 130,
           "The backlog contains 26 features and roughly 400+ person-days. Clubmanager itself has 115 tables and 58 controllers, "
           "several of them 50–280 KB in size. A full port is a multi-year effort for one person. The realistic question is which "
           "three or four domains must exist before the old system can be switched off — the rest can stay in Clubmanager or be "
           "dropped entirely.", 11.5)
    p.text(880, 680, 760, 20, "2.  Where does the contract stop?", 13, "#16354f", bold=True)
    p.text(880, 706, 760, 130,
           "CORE-03 is the single highest-leverage item and also the easiest to over-build. The proposal deliberately keeps it "
           "demand-driven: extend the contract only for a capability a concrete plugin needs now, with a compatibility test per "
           "increment. If the contract is designed speculatively, every later plugin pays for the wrong abstraction.", 11.5)
    return p
