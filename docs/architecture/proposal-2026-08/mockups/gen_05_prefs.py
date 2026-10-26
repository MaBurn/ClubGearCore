from _kit import *

o, top = chrome(active="Dashboard", eyebrow_text="Core · CORE-04",
                title="Notification preferences",
                subtitle="Channels come from installed provider plugins. Core knows the contract, never the provider.",
                unread=3, actions=[("Cancel", "outline"), ("Save", "primary")])
out = [o]

# provider status strip
p, cy = panel(28, top, W - 56, 172, "Available channels", "Registered through the channel-provider contract")
out.append(p)
provs = [
    ("In-app inbox", "Core", "text, html", "ok", "Always on"),
    ("E-mail (SMTP)", "Core", "text, html, subject, attachments", "ok", "Default"),
    ("Matrix", "clubgear.plugin.matrix 0.4.1", "text, html, receipts", "ok", "Configured"),
    ("Telegram", "clubgear.plugin.telegram 0.2.0", "text, 4096 chars", "warn", "No destination"),
    ("E-Post letter", "clubgear.plugin.epost 0.1.0", "pdf, async status", "grey", "Disabled"),
]
cwid = (W - 92) / 5
for i, (name, owner, caps, st, note_) in enumerate(provs):
    x = 46 + i * cwid
    out.append(rect(x, cy + 12, cwid - 14, 80, C["white"], C["panel_border"], rx=8))
    out.append(text(x + 14, cy + 36, name, size=13.5, fill=C["ink"], weight=700))
    out.append(text(x + 14, cy + 54, owner, size=10.5, fill="#5b3d86" if "plugin" in owner else C["muted"]))
    out.append(text(x + 14, cy + 72, caps[:34], size=10.5, fill=C["muted"]))
    b, bw = badge(x + cwid - 14 - 8 - int(len(note_) * 6.4) - 18, cy + 22, note_, st); out.append(b)

# matrix
my = top + 188
p, cy = panel(28, my, W - 56, 396, "Per event category", "Mandatory security and administrative events cannot be switched off")
out.append(p)
hdr = ["Event category", "In-app", "E-mail", "Matrix", "Telegram", "E-Post", "Fallback policy"]
colw = [330, 96, 96, 96, 100, 96, 210]
hx = 46
out.append(rect(29, cy + 6, W - 58, 36, C["surface2"], rx=0))
for h, w in zip(hdr, colw):
    out.append(text(hx + (12 if h in ("Event category", "Fallback policy") else w / 2), cy + 30, h.upper(),
                    size=10.5, fill=C["muted"], weight=700,
                    anchor="start" if h in ("Event category", "Fallback policy") else "middle", ls="0.8"))
    hx += w
out.append(line(29, cy + 42, W - 29, cy + 42))

rowsm = [
    ("Security and account", "mandatory", [2, 2, 0, 0, 0], "Locked — always inbox + e-mail"),
    ("Membership and fees", None, [1, 1, 0, 0, 0], "Queue and retry"),
    ("Rental and inventory", None, [1, 1, 1, 0, 0], "Fall back to e-mail"),
    ("Calendar and events", None, [1, 0, 1, 0, 0], "Inbox only"),
    ("Plugin: Service book", "plugin", [1, 0, 0, 0, 0], "Inbox only"),
    ("Marketing (opt-in)", "optin", [0, 1, 0, 0, 0], "Fail and report"),
]
ry = cy + 42
for i, (name, tag, checks, policy) in enumerate(rowsm):
    if i % 2 == 1:
        out.append(rect(29, ry, W - 58, 46, C["surface"], rx=0))
    out.append(text(58, ry + 29, name, size=13, fill=C["ink"], weight=600))
    if tag == "mandatory":
        b, bw = badge(58 + int(len(name) * 7.2) + 12, ry + 16, "mandatory", "bad"); out.append(b)
        out.append(text(40, ry + 30, "🔒", size=12))
    if tag == "plugin":
        b, bw = badge(58 + int(len(name) * 7.2) + 12, ry + 16, "plugin event", "plugin"); out.append(b)
    if tag == "optin":
        b, bw = badge(58 + int(len(name) * 7.2) + 12, ry + 16, "consent required", "warn"); out.append(b)
    cx = 46 + colw[0]
    for j, v in enumerate(checks):
        if v == 2:
            out.append(checkbox(cx + colw[j + 1] / 2 - 7.5, ry + 15, True))
            out.append(rect(cx + colw[j + 1] / 2 - 7.5, ry + 15, 15, 15, "none", "#9aa5ad", rx=3))
        elif v == 1:
            out.append(checkbox(cx + colw[j + 1] / 2 - 7.5, ry + 15, True))
        else:
            out.append(checkbox(cx + colw[j + 1] / 2 - 7.5, ry + 15, False))
        cx += colw[j + 1]
    out.append(text(cx + 12, ry + 29, policy, size=12, fill=C["muted"]))
    out.append(line(29, ry + 46, W - 29, ry + 46, C["hairline"]))
    ry += 46

# destinations
dy = my + 412
p, cy = panel(28, dy, 700, 200, "My destinations", "Owned and validated by the provider plugin, not by core member fields")
out.append(p)
f, _ = field(46, cy + 4, 300, "E-mail (core member record)", "max@example.org"); out.append(f)
f, _ = field(374, cy + 4, 320, "Matrix user id  ·  provider-validated", "@max:matrix.club.de"); out.append(f)
f, _ = field(46, cy + 76, 300, "Telegram chat id", "not linked — open link wizard", placeholder=True); out.append(f)
f, _ = field(374, cy + 76, 320, "Postal address (E-Post)", "from member address on file", placeholder=True); out.append(f)

n, _ = note(744, dy, W - 744 - 28,
            ["Core stores only the contract-defined reference plus consent state.",
             "Secrets and provider credentials stay namespaced in the plugin,",
             "encrypted at rest and redacted in UI and logs.",
             "Removing a provider hides its column — it never deletes history",
             "or the user's stored preferences."],
            title="Boundary")
out.append(n)

write("05-notification-preferences.svg", out, W, 990)
