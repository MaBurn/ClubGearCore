from _kit import *

o, top = chrome(active="Admin", eyebrow_text="Core · CORE-02",
                title="File and media storage",
                subtitle="One storage service for core and plugins. Business modules keep opaque references, never wwwroot paths.",
                unread=3, actions=[("Storage settings", "outline"), ("Run cleanup", "primary")])
out = [o]

# KPI row
kw = (W - 56 - 3 * 16) / 4
for i, (lab, val, sub, col) in enumerate([
        ("Stored files", "12 847", "4.2 GB of 20 GB quota", C["brand"]),
        ("Awaiting scan", "6", "quarantined until scanner reports", C["accent"]),
        ("Orphaned", "23", "referenced by no business object", C["warn"]),
        ("Rejected today", "11", "type, size or signature mismatch", C["danger"])]):
    out.append(kpi(28 + i * (kw + 16), top, kw, lab, val, sub, accent=col))
top2 = top + 104

# policies
p, cy = panel(28, top2, 620, 300, "Upload policies", "Configured per owner scope, enforced before a byte is written")
out.append(p)
cols = [("Scope", 190, "l"), ("Media types", 200, "l"), ("Max size", 90, "r"), ("Access", 118, "l")]
rows = [
    [{"t": "core.profile-image", "weight": 600}, "image/png, image/jpeg", "2 MB", {"t": "Authenticated", "badge": "info"}],
    [{"t": "core.template-asset", "weight": 600}, "image/*, application/pdf", "10 MB", {"t": "Authenticated", "badge": "info"}],
    [{"t": "plugin.inventory", "weight": 600}, "image/png, image/jpeg", "8 MB", {"t": "Scoped", "badge": "plugin"}],
    [{"t": "plugin.finance", "weight": 600}, "text/csv, application/xml", "25 MB", {"t": "Permission", "badge": "warn"}],
    [{"t": "plugin.documents", "weight": 600}, "application/pdf", "50 MB", {"t": "Permission", "badge": "warn"}],
]
t, ty = table(29, cy + 6, 618, cols, rows)
out.append(t)
out.append(text(46, ty + 26, "Client MIME type and extension are never trusted alone — content signatures decide.", size=12, fill=C["muted"]))

# recent files
p, cy = panel(664, top2, W - 664 - 28, 300, "Recent objects", "Metadata only — bytes live behind the backend")
out.append(p)
cols2 = [("File", 210, "l"), ("Owner scope", 150, "l"), ("Size", 70, "r"), ("State", 110, "l")]
rows2 = [
    [{"t": "IMG_2291.jpg", "weight": 600}, "plugin.inventory", "1.9 MB", {"t": "Available", "badge": "ok"}],
    [{"t": "sepa-2026-08.xml", "weight": 600}, "plugin.finance", "412 KB", {"t": "Available", "badge": "ok"}],
    [{"t": "satzung.pdf", "weight": 600}, "plugin.documents", "3.1 MB", {"t": "Scanning", "badge": "warn"}],
    [{"t": "portrait.png", "weight": 600}, "core.profile-image", "780 KB", {"t": "Available", "badge": "ok"}],
    [{"t": "setup.exe", "weight": 600}, "plugin.inventory", "—", {"t": "Rejected", "badge": "bad"}],
]
t, ty2 = table(665, cy + 6, W - 664 - 30, cols2, rows2)
out.append(t)
out.append(text(682, ty2 + 26, "Storage name is generated; the display name is metadata only.", size=12, fill=C["muted"]))

# pipeline strip
py = top2 + 316
p, cy = panel(28, py, W - 56, 118, "Upload pipeline", "Every write — core or plugin — passes the same gates")
out.append(p)
steps = ["Policy lookup", "Size / quota", "Extension + MIME", "Content signature", "Malware hook", "Store bytes", "Metadata + audit"]
sx = 46
bw = (W - 92 - (len(steps) - 1) * 26) / len(steps)
for i, s in enumerate(steps):
    out.append(rect(sx, cy + 12, bw, 42, C["surface2"], C["input_border"], rx=8))
    out.append(text(sx + bw / 2, cy + 38, s, size=12, fill=C["ink"], weight=600, anchor="middle"))
    if i < len(steps) - 1:
        out.append(text(sx + bw + 13, cy + 39, "›", size=17, fill=C["brand"], anchor="middle", ))
    sx += bw + 26

# modal
mx, my, mw, mh = 350, 236, 580, 400
out.append(scrim())
m, cy = modal(mx, my, mw, mh, "Upload image", "Scope plugin.inventory · item INV-2291")
out.append(m)
out.append(rect(mx + 24, cy + 20, mw - 48, 96, C["surface"], C["input_border"], rx=8, dash="5 4"))
out.append(text(mx + mw / 2, cy + 62, "Drop a file or click to select", size=13.5, fill=C["muted"], anchor="middle"))
out.append(text(mx + mw / 2, cy + 84, "PNG or JPEG · up to 8 MB · 12 of 20 images used", size=12, fill=C["muted2"], anchor="middle"))
out.append(alert(mx + 24, cy + 130, mw - 48, "setup.exe rejected: extension not permitted for this scope.", "bad", icon="✕"))
out.append(alert(mx + 24, cy + 184, mw - 48, "IMG_2291.jpg accepted — checksum 8f3a…c1, queued for thumbnailing.", "ok", icon="✓"))
b, _ = btn(mx + mw - 116, my + mh - 56, "Upload", "primary", w=92); out.append(b)
b, _ = btn(mx + mw - 214, my + mh - 56, "Cancel", "outline", w=86); out.append(b)

write("02-secure-file-storage.svg", out)
