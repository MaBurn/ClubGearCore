from _kit import *

o, top = chrome(active="Plugins", eyebrow_text="Plugin · FIN-01  ·  clubgear.plugin.finance",
                title="Payment matching  ·  import 2026-08-28",
                subtitle="Bank data never moves into core. Core supplies templates, audit and notifications; the plugin owns the money.",
                unread=3, actions=[("Export CSV", "outline"), ("Confirm 42 matches", "primary")])
out = [o]

kw = (W - 56 - 3 * 16) / 4
for i, (lab, val, sub, col) in enumerate([
        ("Imported records", "318", "camt.053 · duplicate check passed", C["brand"]),
        ("Auto-matched", "271", "deterministic rules, reasons shown", C["success"]),
        ("Needs review", "42", "ambiguous or partial amount", C["accent"]),
        ("Rejected", "5", "logged, nothing booked", C["danger"])]):
    out.append(kpi(28 + i * (kw + 16), top, kw, lab, val, sub, accent=col))
t2 = top + 104

p, cy = panel(28, t2, W - 56, 404, "Review queue", "Suggestions are deterministic — the reason is always visible")
out.append(p)
cols = [("Value date", 110, "l"), ("Counterparty", 200, "l"), ("IBAN", 170, "l"), ("Amount", 100, "r"),
        ("Suggested member", 210, "l"), ("Why", 220, "l"), ("Decision", 180, "l")]
rows = [
    ["28.08.2026", {"t": "Weiss, Dirk", "weight": 600}, "DE89 **** **** 3210", "€ 96,00",
     {"t": "4470 Dirk Weiss", "weight": 600}, "mandate ref + exact amount", {"t": "Accept", "badge": "ok"}],
    ["28.08.2026", {"t": "C. Weiss", "weight": 600}, "DE21 **** **** 8817", "€ 48,00",
     {"t": "4471 Clara Weiss", "weight": 600}, "name similarity 0.91", {"t": "Review", "badge": "warn"}],
    ["27.08.2026", {"t": "Sammelzahlung OG Süd", "weight": 600}, "DE55 **** **** 1109", "€ 384,00",
     {"t": "8 members (household run)", "weight": 600}, "amount = sum of 8 demands", {"t": "Split", "badge": "warn"}],
    ["27.08.2026", {"t": "Unbekannt", "weight": 600}, "DE02 **** **** 4412", "€ 25,00",
     {"t": "—", "muted": True}, "no rule matched", {"t": "Manual", "badge": "grey"}],
    ["26.08.2026", {"t": "Kruse, Bernd", "weight": 600}, "DE77 **** **** 0021", "€ 96,00",
     {"t": "4402 Bernd Kruse", "weight": 600}, "already matched 26.08.", {"t": "Duplicate", "badge": "bad"}],
]
t, ty = table(29, cy + 6, W - 58, cols, rows, row_h=46)
out.append(t)
out.append(text(46, ty + 30, "IBAN is masked by default. Unmasking is a separate permission and is written to the audit log.", size=12, fill=C["muted"]))
b, _ = btn(W - 190, ty + 14, "Unmask (audited)", "outline", w=150); out.append(b)

# fee run
fy = t2 + 420
p, cy = panel(28, fy, 640, 240, "Fee run 2026 · state: approved", "Reproducible — same inputs produce identical demands")
out.append(p)
steps = [("Draft", "ok"), ("Reviewed", "ok"), ("Approved", "ok"), ("Exported", "warn"), ("Booked", "grey")]
sx = 46
for i, (s, k) in enumerate(steps):
    pal = {"ok": (C["success_bg"], C["success"]), "warn": (C["warn_bg"], C["warn"]), "grey": (C["hairline"], C["muted"])}[k]
    out.append(rect(sx, cy + 12, 104, 34, pal[0], rx=8))
    out.append(text(sx + 52, cy + 34, s, size=12, fill=pal[1], weight=700, anchor="middle"))
    if i < 4:
        out.append(text(sx + 112, cy + 34, "›", size=15, fill=C["muted"], anchor="middle"))
    sx += 122
out.append(text(46, cy + 76, "612 demands · € 54.912,00 · rules pinned at run time", size=13, fill=C["ink"], weight=600))
out.append(text(46, cy + 100, "Family discount applied to 84 demands via the CORE-08 read model.", size=12.5, fill=C["muted"]))
out.append(text(46, cy + 122, "pain.008 generated from approved demands with a valid mandate only.", size=12.5, fill=C["muted"]))
b, _ = btn(46, cy + 142, "Download pain.008", "primary", w=168); out.append(b)
b, _ = btn(222, cy + 142, "Batch history", "outline", w=124); out.append(b)

# guardrails
p, cy = panel(684, fy, W - 684 - 28, 240, "Guardrails")
out.append(p)
gs = [("Re-running an unchanged calculation yields identical totals", "ok"),
      ("Importing the same file twice creates no duplicate payment", "ok"),
      ("A collection batch cannot be exported twice by accident", "ok"),
      ("Match / unmatch / rematch is fully audited with a reason", "ok"),
      ("Member messages use semantic core events, not template ids", "ok"),
      ("Rental payments arrive via a contract, never via table access", "warn")]
gy = cy + 24
for g, k in gs:
    out.append(text(702, gy, "✓" if k == "ok" else "→", size=13, fill=C["success"] if k == "ok" else C["warn"], weight=700))
    out.append(text(722, gy, g, size=12.2, fill=C["body"]))
    gy += 30

write("11-finance.svg", out, W, 960)
