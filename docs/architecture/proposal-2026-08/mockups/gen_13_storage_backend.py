from _kit import *

o, top = chrome(active="Admin", eyebrow_text="Core · CORE-02  ·  storage backend",
                title="File storage",
                subtitle="Where the bytes live. Metadata, authorisation and audit stay in ClubGear whichever backend is selected.",
                unread=3, actions=[("Test connection", "outline"), ("Save", "primary")])
out = [o]

# backend choice
p, cy = panel(28, top, 620, 320, "Active backend", "One backend is authoritative for new uploads")
out.append(p)

def card(x, y, w, h, title, sub, lines, on, tag, tagkind):
    o2 = [rect(x, y, w, h, "rgba(0,119,186,0.05)" if on else C["white"],
               C["brand"] if on else C["input_border"], rx=10, sw=2 if on else 1)]
    o2.append(radio(x + 16, y + 18, on))
    o2.append(text(x + 44, y + 30, title, size=14, fill=C["ink"], weight=700))
    b, bw = badge(x + w - 16 - int(len(tag) * 6.4) - 18, y + 20, tag, tagkind)
    o2.append(b)
    o2.append(text(x + 44, y + 50, sub, size=12, fill=C["muted"]))
    for i, l in enumerate(lines):
        o2.append(text(x + 44, y + 74 + i * 18, "· " + l, size=11.5, fill=C["body"]))
    return "".join(o2)

out.append(card(46, cy + 8, 584, 96, "Local filesystem", "Ships with the core · no extra service",
                ["Fastest, no network dependency", "Backup is a filesystem backup"], False, "core", "info"))
out.append(card(46, cy + 116, 584, 120, "Nextcloud (WebDAV)", "Provided by clubgear.plugin.nextcloud-storage",
                ["Bytes live in your Nextcloud, visible to admins there",
                 "ClubGear still enforces every download authorisation",
                 "Requires a dedicated service account and app password"], True, "plugin", "plugin"))

# connection
p, cy = panel(664, top, W - 664 - 28, 320, "Nextcloud connection", "Credentials are encrypted at rest and redacted in logs")
out.append(p)
f, y1 = field(682, cy + 4, 380, "Base URL", "https://cloud.musterverein.de"); out.append(f)
f, _ = field(1078, cy + 4, 174, "Service user", "clubgear-svc"); out.append(f)
f, y2 = field(682, y1 + 14, 380, "App password", "••••••••••••••••••••", kind="text"); out.append(f)
f, _ = field(1078, y1 + 14, 174, "Chunked upload", "on", kind="select"); out.append(f)
f, y3 = field(682, y2 + 14, 570, "Base path", "/ClubGear/storage   (created and owned by ClubGear)"); out.append(f)
out.append(alert(682, y3 + 20, 570, "Connected · Nextcloud 30.0.2 · 412 GB free · write test passed 09:41", "ok", h=42, icon="✓"))

# per-scope overrides
sy = top + 336
p, cy = panel(28, sy, 760, 268, "Per-scope override", "Optional — a scope may deliberately stay local")
out.append(p)
cols = [("Owner scope", 230, "l"), ("Backend", 190, "l"), ("Objects", 100, "r"), ("Reason", 220, "l")]
rows = [
    [{"t": "core.profile-image", "weight": 600}, {"t": "Nextcloud", "badge": "plugin"}, "1 284", "follows default"],
    [{"t": "plugin.inventory", "weight": 600}, {"t": "Nextcloud", "badge": "plugin"}, "9 902", "follows default"],
    [{"t": "plugin.documents", "weight": 600}, {"t": "Nextcloud", "badge": "plugin"}, "1 431", "follows default"],
    [{"t": "plugin.finance", "weight": 600}, {"t": "Local", "badge": "info"}, "230", "bank files stay on the host"],
]
t, _ = table(29, cy + 6, 758, cols, rows)
out.append(t)

# migration
p, cy = panel(804, sy, W - 804 - 28, 268, "Migration local → Nextcloud", "Resumable, verified by checksum")
out.append(p)
out.append(progress(822, cy + 26, W - 804 - 64, 71))
out.append(text(822, cy + 56, "9 118 of 12 847 objects copied · 3.0 GB", size=12.5, fill=C["ink"], weight=600))
out.append(text(822, cy + 78, "Every object records which backend holds it, so both", size=12, fill=C["muted"]))
out.append(text(822, cy + 96, "are readable while the copy runs. Nothing is deleted", size=12, fill=C["muted"]))
out.append(text(822, cy + 114, "from the source until its checksum is verified.", size=12, fill=C["muted"]))
b, _ = btn(822, cy + 136, "Pause", "outline", w=90); out.append(b)
b, _ = btn(920, cy + 136, "Verify all", "outline", w=104); out.append(b)

# policy
py = sy + 284
p, cy = panel(28, py, W - 56, 150, "Failure policy", "Same rule as notification channels: never a silent substitution")
out.append(p)
pol = [("Backend unreachable on upload", "The upload fails with a clear error. ClubGear does not quietly write the file to the local disk instead.", "bad"),
       ("Backend unreachable on download", "503 with a retry hint. The metadata row stays valid; the object is not marked missing.", "warn"),
       ("File removed inside Nextcloud", "Detected by a scheduled integrity check and reported as an orphaned reference, never auto-deleted.", "warn")]
ry = cy + 16
for title, body, kind in pol:
    col = {"bad": C["danger"], "warn": C["warn"]}[kind]
    out.append(text(46, ry + 16, title, size=12.5, fill=col, weight=700))
    out.append(text(360, ry + 16, body, size=12, fill=C["body"]))
    ry += 32

write("13-storage-backend.svg", out, W, 970)
