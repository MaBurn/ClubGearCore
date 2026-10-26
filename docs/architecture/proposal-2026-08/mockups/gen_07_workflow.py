from _kit import *

o, top = chrome(active="Admin", eyebrow_text="Core · CORE-06",
                title="Workflow  ·  rental.order",
                subtitle="Generic state machine in core. The definition belongs to the plugin that registered it.",
                unread=3, actions=[("New version", "outline"), ("Open instances", "primary")])
out = [o]
b, _ = badge(268, 82, "registered by clubgear.plugin.rental 0.9.0 · version 3 · immutable", "plugin"); out.append(b)

# state diagram
p, cy = panel(28, top, W - 56, 352, "Definition v3", "States and permitted transitions — core validates, the plugin decides business rules")
out.append(p)

states = {
    "Requested":   (90, 70, "info"),
    "Approved":    (300, 70, "info"),
    "PickedUp":    (510, 70, "info"),
    "Returned":    (720, 70, "info"),
    "Settled":     (930, 70, "ok"),
    "Rejected":    (300, 200, "bad"),
    "Cancelled":   (510, 200, "bad"),
    "Damaged":     (720, 200, "warn"),
}
sw, sh = 150, 52
for name, (sx, sy, kind) in states.items():
    x, y = 46 + sx, cy + sy
    fill = {"info": "#eaf3f9", "ok": C["success_bg"], "bad": C["danger_bg"], "warn": C["warn_bg"]}[kind]
    stroke = {"info": "#a8cee4", "ok": "#a9d3bc", "bad": "#e3b6b6", "warn": "#e8cd9a"}[kind]
    out.append(rect(x, y, sw, sh, fill, stroke, rx=10))
    out.append(text(x + sw / 2, y + 31, name, size=13.5, fill=C["ink"], weight=700, anchor="middle"))

def arrow(x1, y1, x2, y2, label, dash=None, color=None):
    color = color or C["brand"]
    a = [f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="1.6"'
         + (f' stroke-dasharray="{dash}"' if dash else "") + '/>']
    import math
    ang = math.atan2(y2 - y1, x2 - x1)
    for d in (2.6, -2.6):
        a.append(f'<line x1="{x2}" y1="{y2}" x2="{x2 - 9 * math.cos(ang - 0.4 * d / 2.6)}" '
                 f'y2="{y2 - 9 * math.sin(ang - 0.4 * d / 2.6)}" stroke="{color}" stroke-width="1.6"/>')
    mx, my = (x1 + x2) / 2, (y1 + y2) / 2
    a.append(rect(mx - len(label) * 3.4 - 6, my - 19, len(label) * 6.8 + 12, 17, C["white"], rx=4))
    a.append(text(mx, my - 6, label, size=10.5, fill=C["muted"], anchor="middle"))
    return "".join(a)

Y = cy + 70 + 26
for a, b_ in [("Requested", "Approved"), ("Approved", "PickedUp"), ("PickedUp", "Returned"), ("Returned", "Settled")]:
    x1 = 46 + states[a][0] + sw
    x2 = 46 + states[b_][0]
    lbl = {"Requested": "approve", "Approved": "hand over", "PickedUp": "take back", "Returned": "settle"}[a]
    out.append(arrow(x1 + 4, Y, x2 - 6, Y, lbl))
out.append(arrow(46 + 165, Y + 12, 46 + 340, cy + 200 - 6, "reject", color="#b05252"))
out.append(arrow(46 + 375, Y + 26, 46 + 545, cy + 200 - 6, "cancel", color="#b05252"))
out.append(arrow(46 + 795, Y + 26, 46 + 795, cy + 200 - 6, "report damage", color="#a8761f"))
out.append(arrow(46 + 870, cy + 200 + 26, 46 + 930 - 6, Y + 20, "settle with deduction", color="#a8761f"))

leg = cy + 268
out.append(text(46, leg, "Each transition declares: source, target, required permission, validation hook, side-effect events.", size=12.5, fill=C["muted"]))
out.append(text(46, leg + 20, "Side effects run through an outbox — a rolled-back transition emits nothing.", size=12.5, fill=C["muted"]))

# instance history
iy = top + 368
p, cy = panel(28, iy, 760, 300, "Instance rent-4471", "Owner resource: rental order 4471 · pinned to definition v3 · row version 7")
out.append(p)
cols = [("When", 150, "l"), ("Transition", 190, "l"), ("Actor", 150, "l"), ("Result", 130, "l"), ("Reason", 130, "l")]
rows = [
    ["30.08. 09:12", {"t": "→ Requested", "weight": 600}, "Clara Weiss", {"t": "Committed", "badge": "ok"}, "self-service"],
    ["30.08. 09:40", {"t": "Requested → Approved", "weight": 600}, "Anna Falk", {"t": "Committed", "badge": "ok"}, "deposit paid"],
    ["30.08. 09:41", {"t": "Approved → Settled", "weight": 600}, "Anna Falk", {"t": "Rejected", "badge": "bad"}, "not permitted"],
    ["30.08. 10:02", {"t": "Approved → PickedUp", "weight": 600}, "Bernd Kruse", {"t": "Committed", "badge": "ok"}, "handover signed"],
    ["30.08. 10:02", {"t": "side effect: notify", "weight": 600}, "system", {"t": "Queued", "badge": "warn"}, "outbox"],
]
t, _ = table(29, cy + 6, 758, cols, rows)
out.append(t)

# guarantees
p, cy = panel(804, iy, W - 804 - 28, 300, "Guarantees", "Verified by contract tests")
out.append(p)
gs = ["Invalid transition is rejected, not silently ignored",
      "Stale row version loses — no lost update",
      "Same idempotency key replays without a second effect",
      "History stays readable after the plugin is deactivated",
      "Definitions are immutable once used; v4 is a new row",
      "Admin override exists only as an audited capability"]
gy = cy + 24
for g in gs:
    out.append(text(822, gy, "✓", size=13, fill=C["success"], weight=700))
    out.append(text(842, gy, g, size=12.3, fill=C["body"]))
    gy += 34

write("07-workflow.svg", out, W, 900)
