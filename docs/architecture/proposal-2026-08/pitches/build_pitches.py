import os
from _slide import *
from data import FULL, COMPACT

EFFORT_WORD = {1: "XS", 2: "M", 3: "L", 4: "L+", 5: "XL"}


def feature_slide(f, n, total):
    kind = "info" if f["owner"] == "Core" else "plugin"
    out = slide_bg(f"PITCH {n} OF {total}   ·   {f['id']}", f["name"],
                   f["effort_label"], tag=f["owner"], tagkind=kind)

    # one-line pitch
    out.append(rect(56, 190, SW - 112, 74, "rgba(0,119,186,0.06)", rx=10))
    out.append(rect(56, 190, 4, 74, C["brand"], rx=2))
    for i, l in enumerate(wrap(f["pitch"], 108)):
        out.append(text(80, 222 + i * 24, l, size=17, fill=C["ink"], weight=600))

    # three columns
    colw = 400
    c1, _ = column(56, 300, colw, "Today", C["danger"], f["today"], budget=46)
    c2, _ = column(496, 300, colw, "After", C["success"], f["after"], budget=46)
    c3, _ = column(936, 300, 330, "If it is not built", C["warn"], f["without"], budget=40)
    out += [c1, c2, c3]

    # mockup thumbnail
    if f.get("mock"):
        out.append(text(1300, 300, "Screen", size=14, fill=C["muted"], weight=700))
        out.append(embed(os.path.join(MOCK, f["mock"]), 1300, 322, 244, 244))
        out.append(text(1300, 584, "full mockup in ../mockups", size=10.5, fill=C["muted"]))

    # scores
    out.append(line(56, 640, SW - 56, 640, "#dde3e8"))
    out.append(eyebrow(56, 674, "My assessment — argue with it, that is the point"))
    out.append(scorebar(56, 716, "Value to the club", f["value"]))
    out.append(scorebar(56, 754, "Blocks other work", f["blocking"]))
    out.append(scorebar(440, 716, "Effort", f["effort"], color=C["accent"]))
    out.append(scorebar(440, 754, "Risk", f["risk"], color=C["danger"]))

    # relations
    out.append(rect(830, 670, SW - 886, 112, C["white"], C["panel_border"], rx=10))
    out.append(text(852, 698, "Depends on", size=12, fill=C["muted"], weight=700))
    out.append(text(960, 698, f["depends"], size=13, fill=C["body"]))
    out.append(text(852, 726, "Unlocks", size=12, fill=C["muted"], weight=700))
    out.append(text(960, 726, f["unlocks"], size=13, fill=C["body"]))
    out.append(text(852, 754, "Watch out", size=12, fill=C["muted"], weight=700))
    rn = wrap(f["risknote"], 78)
    for i, l in enumerate(rn[:2]):
        out.append(text(960, 754 + i * 16, l, size=11.5, fill=C["warn"]))

    out.append(text(SW - 56, 856, "ClubGear · architecture proposal · pitch deck", size=11,
                    fill=C["muted"], anchor="end"))
    return out


def title_slide():
    out = [rect(0, 0, SW, SH, C["brand"], rx=0)]
    out.append(rect(0, 0, SW, SH, "#005b99", rx=0, opacity=0.25))
    out.append(text(96, 300, "ClubGear", size=64, fill="#ffffff", weight=700))
    out.append(text(96, 356, "Feature pitches", size=40, fill="#cfe6f4", weight=300))
    out.append(rect(96, 396, 120, 4, C["accent"], rx=2))
    for i, l in enumerate([
            "One slide per feature, so priorities can be set on comparable information.",
            "11 full pitches for the specified features, 15 compact cards for the rest of the backlog,",
            "and a priority matrix at the end."]):
        out.append(text(96, 448 + i * 30, l, size=17, fill="#dcecf6"))
    out.append(text(96, 600, "Scores are my assessment, not measurements.", size=15, fill="#a9d2e9"))
    out.append(text(96, 626, "They exist to be disagreed with — that is faster than a blank page.", size=15, fill="#a9d2e9"))
    out.append(text(96, 800, "30 August 2026 · nothing implemented · for prioritisation", size=13, fill="#8fc3e0"))
    return out


def howto_slide():
    out = slide_bg("HOW TO READ THIS", "Four numbers per feature",
                   "Same scale everywhere, so the slides can be compared rather than admired.")
    rows = [
        ("Value to the club", C["brand"],
         "How much a member, board member or treasurer actually notices it. 5 = someone's weekly work changes."),
        ("Blocks other work", C["brand"],
         "How much else is stuck behind it. 5 = several XL items cannot start. This is the number that "
         "usually decides sequence, not value."),
        ("Effort", C["accent"],
         "1 = XS (1–2 PT) · 2 = M (6–12) · 3 = L (13–25) · 4 = L+ · 5 = XL (26–50+). Implementation and "
         "tests, no historical data migration."),
        ("Risk", C["danger"],
         "Chance of building the wrong thing, or of it failing visibly in production. Money, personal data "
         "and concurrency push this up."),
    ]
    y = 240
    for label, color, body in rows:
        out.append(rect(56, y, SW - 112, 96, C["white"], C["panel_border"], rx=10))
        out.append(rect(56, y, 5, 96, color, rx=2))
        out.append(text(84, y + 38, label, size=17, fill=C["ink"], weight=700))
        for i, l in enumerate(wrap(body, 96)):
            out.append(text(400, y + 32 + i * 22, l, size=13.5, fill=C["body"]))
        y += 112
    out.append(text(56, 724, "A practical shortcut", size=15, fill=C["brand"], weight=700))
    out.append(text(56, 754, "If two features have the same value, build the one that blocks more. "
                             "If two block equally, build the cheaper one first.", size=14, fill=C["body"]))
    out.append(text(56, 782, "Everything in the Phase-1 column of the capability map scores high on "
                             "»blocks other work« and low on »value« — that is normal for platform work,", size=14, fill=C["muted"]))
    out.append(text(56, 806, "and the reason it tends to get postponed until it hurts.", size=14, fill=C["muted"]))
    return out


def compact_slide(items, n, total):
    out = slide_bg(f"REST OF THE BACKLOG  ·  {n} OF {total}", "Compact cards",
                   "Not specified in tmp/ — one line each, enough to rank them against the full pitches.")
    y = 210
    for cid, name, owner, eff, body, effort, risk in items:
        out.append(rect(56, y, SW - 112, 96, C["white"], C["panel_border"], rx=10))
        b, bw = badge(80, y + 20, cid, "info" if owner == "Core" else "plugin")
        out.append(b)
        out.append(text(80, y + 62, name, size=16, fill=C["ink"], weight=700))
        out.append(text(80, y + 84, f"{owner} · {eff}", size=11.5, fill=C["muted"]))
        for i, l in enumerate(wrap(body, 76)):
            out.append(text(560, y + 40 + i * 22, l, size=13.5, fill=C["body"]))
        out.append(scorebar(1180, y + 40, "Effort", effort, w=110, color=C["accent"]))
        out.append(scorebar(1180, y + 72, "Risk", risk, w=110, color=C["danger"]))
        y += 106
    return out


def matrix_slide():
    out = slide_bg("PRIORITISATION", "Value against effort",
                   "Bubble size = how much other work is blocked. Colour = core (blue) or plugin (purple).")
    ox, oy, gw, gh = 200, 250, 900, 520
    out.append(rect(ox, oy, gw, gh, C["white"], C["panel_border"], rx=10))
    for i in range(1, 5):
        out.append(line(ox + i * gw / 5, oy, ox + i * gw / 5, oy + gh, C["hairline"]))
        out.append(line(ox, oy + i * gh / 5, ox + gw, oy + i * gh / 5, C["hairline"]))
    out.append(text(ox + gw / 2, oy + gh + 40, "Effort  →", size=14, fill=C["muted"], weight=700, anchor="middle"))
    out.append(text(ox - 46, oy - 14, "Value ↑", size=14, fill=C["muted"], weight=700))
    for i, lab in enumerate(["XS", "M", "L", "L+", "XL"]):
        out.append(text(ox + (i + 0.5) * gw / 5, oy + gh + 20, lab, size=12, fill=C["muted"], anchor="middle"))

    for i, lab in enumerate(["1", "2", "3", "4", "5"]):
        out.append(text(ox - 14, oy + gh - (i + 0.5) * gh / 5 + 4, lab, size=12,
                        fill=C["muted"], anchor="middle"))

    pts = [(f["id"], f["effort"], f["value"], f["blocking"], f["owner"]) for f in FULL]
    cells = {}
    for pt in pts:
        cells.setdefault((pt[1], pt[2]), []).append(pt)
    for (eff, val), group in cells.items():
        n = len(group)
        for k, (cid, _, _, blk, owner) in enumerate(group):
            spread = 0 if n == 1 else (k - (n - 1) / 2) * 62
            cx = ox + (eff - 0.5) * gw / 5 + spread
            cy = oy + gh - (val - 0.5) * gh / 5 + (0 if n < 3 else (k % 2) * 26 - 13)
            fill = "rgba(0,119,186,0.20)" if owner == "Core" else "rgba(91,61,134,0.18)"
            stroke = C["brand"] if owner == "Core" else "#5b3d86"
            r = 16 + blk * 5
            out.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{fill}" stroke="{stroke}" stroke-width="1.6"/>')
            if r >= 26:
                out.append(text(cx, cy + 4, cid, size=10.5, fill=stroke, weight=700, anchor="middle"))
            else:
                out.append(text(cx, cy + r + 16, cid, size=10.5, fill=stroke, weight=700, anchor="middle"))

    out.append(rect(1140, 250, 404, 268, C["white"], C["panel_border"], rx=10))
    out.append(text(1164, 286, "How to read the quadrants", size=15, fill=C["brand"], weight=700))
    q = [("Top-left", "cheap and valuable — do them"),
         ("Top-right", "valuable and expensive — sequence them, do not skip"),
         ("Bottom-left", "cheap platform work — worth it for what it unblocks"),
         ("Bottom-right", "expensive and conditional — CORE-06 lives here")]
    yy = 318
    for a, b in q:
        out.append(text(1164, yy, a, size=12.5, fill=C["ink"], weight=700))
        for i, l in enumerate(wrap(b, 44)):
            out.append(text(1164, yy + 18 + i * 16, l, size=11.5, fill=C["muted"]))
        yy += 52

    out.append(rect(1140, 540, 404, 232, "#fffaf2", "#f0d5ab", rx=10, dash="4 3"))
    out.append(text(1164, 576, "What the picture says", size=15, fill="#8a5a00", weight=700))
    for i, l in enumerate([
            "CORE-02 is the only item that is cheap, valuable",
            "and blocking. It should be first or second.",
            "",
            "CORE-03 scores low on direct value and highest on",
            "blocking — the classic item that gets postponed",
            "until five plugins have worked around it.",
            "",
            "INV-01 and FIN-01 carry the visible value, and",
            "both sit behind the platform column.",
            "",
            "CORE-06 is the one I would cut first."]):
        out.append(text(1164, 606 + i * 16, l, size=11.5, fill="#7a5310"))
    return out


def recommendation_slide():
    out = slide_bg("IF YOU WANT MY OPINION", "One recommended cut",
                   "Not a plan — a starting position to disagree with.")
    blocks = [
        ("Build first", C["success"], ["CORE-02  file storage incl. the backend port",
                                       "CORE-03  contract increment, demand-driven",
                                       "CORE-01  organisation units"],
         "Roughly 33–50 PT. After this, a plugin can render a real page and store a file, "
         "and everything has a scope."),
        ("Build second", C["brand"], ["CORE-05  templates", "CORE-04  inbox + provider contract",
                                      "CORE-08  household relationships"],
         "Roughly 32–50 PT. This is where members start noticing the system rather than the developer."),
        ("Then pick ONE domain", C["accent"], ["FIN-01  if the treasurer is the bottleneck",
                                               "INV-01  if the workshop is the bottleneck"],
         "Both are XL. Doing one properly beats starting both. FIN-01 has higher risk, INV-01 has more volume."),
        ("Defer or drop", C["danger"], ["CORE-06  unless RENT-01 is genuinely coming",
                                        "NEWS-01, RENT-01, EDIT-01, and the whole P3 set",
                                        "CORE-11 translations unless there are non-German members"],
         "The backlog is 400+ PT. Deferring is not failure; pretending otherwise is."),
    ]
    y = 230
    for title, color, items, note in blocks:
        out.append(rect(56, y, SW - 112, 140, C["white"], C["panel_border"], rx=10))
        out.append(rect(56, y, 5, 140, color, rx=2))
        out.append(text(84, y + 36, title, size=18, fill=color, weight=700))
        for i, it in enumerate(items):
            out.append(text(84, y + 66 + i * 22, "· " + it, size=13.5, fill=C["body"]))
        for i, l in enumerate(wrap(note, 62)):
            out.append(text(1020, y + 40 + i * 22, l, size=13, fill=C["muted"]))
        y += 152
    out.append(text(56, 856, "The one question that changes all of this: which domains must exist before "
                             "Clubmanager can be switched off?", size=14, fill=C["brand"], weight=600))
    return out



def dnx_slide():
    out = slide_bg("CONSTRAINTS, NOT PRIORITIES", "Do not execute",
                   "These are not on the priority list because they are not choices. Full detail on diagram page 11.")
    rules = [
        ("DNX-01", "No provider-specific code in the core",
         "Matrix, Telegram, SMS, E-Post, Nextcloud, Listmonk, Traccar — all plugins behind generic contracts."),
        ("DNX-02", "No core branch on a module id",
         "No plugin-specific DTO in the contract, no switch on a plugin name in the host."),
        ("DNX-03", "No cross-schema access in either direction",
         "Read models and contract facades only — both ways."),
        ("DNX-04", "No silent fallback",
         "Channels and storage backends report the declared policy instead of substituting quietly."),
        ("DNX-05", "No second plugin architecture",
         "No out-of-process/gRPC host, no free Razor assemblies outside contract control."),
        ("DNX-06", "No parallel template systems",
         "Exactly one versioned system; the simple renderer is replaced, not kept beside it."),
        ("DNX-07", "No paths and no secrets in ordinary configuration",
         "No filesystem access from controllers, no credential in a plain config value."),
        ("DNX-08", "No hard-coded state machine beside the workflow engine",
         "Cutting CORE-06 is allowed; hard-coding states because it was cut is not."),
    ]
    y = 214
    for cid, title, body in rules:
        out.append(rect(56, y, 900, 68, "#fbe9e9", "#e3b6b6", rx=8))
        out.append(text(76, y + 28, cid, size=12, fill=C["danger"], weight=700))
        out.append(text(148, y + 28, title, size=14, fill=C["danger"], weight=700))
        out.append(text(76, y + 52, body, size=11.5, fill=C["body"]))
        y += 76

    out.append(text(996, 214, "Do not port, even though it works", size=15, fill=C["ink"], weight=700))
    ports = [
        "Thousand-line controllers as a porting unit — 277 KB / 128 KB / 73 KB in Clubmanager",
        "ReferateController as hard-coded view names — use CORE-01 plus plugin navigation",
        "Keycloak group management as the local authority — local roles stay authoritative",
        "Debug controllers as productive routes — development-only diagnostics",
        "ToolRental as a second plugin — lending in INV-01, commercial rental in RENT-01",
    ]
    yy = 252
    for pline in ports:
        out.append(text(996, yy, "✕", size=13, fill=C["danger"], weight=700))
        for j, l in enumerate(wrap(pline, 56)):
            out.append(text(1018, yy + j * 18, l, size=12, fill=C["body"]))
        yy += 18 * len(wrap(pline, 56)) + 16

    out.append(rect(996, 500, 548, 176, "#fffaf2", "#f0d5ab", rx=10, dash="4 3"))
    out.append(text(1018, 534, "Breaking one is allowed — silently is not", size=14, fill="#8a5a00", weight=700))
    for i, l in enumerate([
            "There will be a day when the pragmatic choice violates one of",
            "these. The rule is not »never«; it is: write down which item you",
            "are breaking, why, and what would undo it.",
            "",
            "That is the whole difference between a deliberate exception and",
            "the state Clubmanager reached over several years."]):
        out.append(text(1018, 562 + i * 19, l, size=11.5, fill="#7a5310"))

    out.append(text(56, 856, "Per-feature »out of scope« is listed verbatim on diagram page 11 — "
                             "eleven features, three exclusions each, straight from the specifications.",
                    size=13, fill=C["muted"]))
    return out


slides = []
slides.append(("00-title", title_slide()))
slides.append(("01-how-to-read", howto_slide()))
for i, f in enumerate(FULL, 1):
    slides.append((f"{i + 1:02d}-{f['id'].lower().replace('/', '-')}", feature_slide(f, i, len(FULL))))
slides.append(("13-backlog-a", compact_slide(COMPACT[:6], 1, 3)))
slides.append(("14-backlog-b", compact_slide(COMPACT[6:12], 2, 3)))
slides.append(("15-backlog-c", compact_slide(COMPACT[12:], 3, 3)))
slides.append(("16-matrix", matrix_slide()))
slides.append(("17-do-not-execute", dnx_slide()))
slides.append(("18-recommendation", recommendation_slide()))

for name, parts in slides:
    write(f"{name}.svg", parts)
print(len(slides), "slides written")
