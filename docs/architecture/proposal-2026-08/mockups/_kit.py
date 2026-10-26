"""Tiny SVG widget kit used to draw the ClubGear mockups.

Colours and spacing follow ADR 0002 (UI style guide) so the mockups read as the
real application rather than as generic wireframes.
"""

W, H = 1280, 860

C = {
    "brand": "#0077BA",
    "brand_dark": "#005b99",
    "accent": "#EF7D00",
    "ink": "#16354f",
    "body": "#222222",
    "muted": "#5f6368",
    "muted2": "#6a7178",
    "panel_border": "#e6e8eb",
    "input_border": "#d5dce2",
    "white": "#ffffff",
    "surface": "#fbfcfd",
    "surface2": "#f4f7fa",
    "hairline": "#f0f2f4",
    "page_bg": "#f4f7fa",
    "success": "#187344",
    "success_bg": "#e4f2ea",
    "warn": "#8a5a00",
    "warn_bg": "#fdf3e0",
    "danger": "#9b2c2c",
    "danger_bg": "#fbe9e9",
    "info_bg": "#e6f2f9",
    "tint": "rgba(0,119,186,0.06)",
}

FONT = "Segoe UI, Arial, Helvetica, sans-serif"


def esc(s):
    return (str(s).replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def rect(x, y, w, h, fill="none", stroke=None, rx=8, sw=1, opacity=None, dash=None):
    a = f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{rx}" fill="{fill}"'
    if stroke:
        a += f' stroke="{stroke}" stroke-width="{sw}"'
    if dash:
        a += f' stroke-dasharray="{dash}"'
    if opacity is not None:
        a += f' opacity="{opacity}"'
    return a + "/>"


def text(x, y, s, size=14, fill=None, weight=400, anchor="start", ls=None, op=None):
    fill = fill or C["body"]
    a = (f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" '
         f'fill="{fill}" font-weight="{weight}" text-anchor="{anchor}"')
    if ls:
        a += f' letter-spacing="{ls}"'
    if op is not None:
        a += f' opacity="{op}"'
    return a + f">{esc(s)}</text>"


def eyebrow(x, y, s):
    return text(x, y, s.upper(), size=11, fill=C["muted"], weight=700, ls="1.2")


def line(x1, y1, x2, y2, stroke=None, sw=1, dash=None):
    stroke = stroke or C["panel_border"]
    a = f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{stroke}" stroke-width="{sw}"'
    if dash:
        a += f' stroke-dasharray="{dash}"'
    return a + "/>"


def btn(x, y, label, kind="primary", w=None, h=34, size=13.5):
    w = w or max(84, int(len(label) * 7.6) + 30)
    if kind == "primary":
        fill, stroke, fg = C["brand"], C["brand"], C["white"]
    elif kind == "outline":
        fill, stroke, fg = C["white"], C["input_border"], C["ink"]
    elif kind == "accent":
        fill, stroke, fg = C["accent"], C["accent"], C["white"]
    elif kind == "danger":
        fill, stroke, fg = C["white"], "#e0b4b4", C["danger"]
    else:
        fill, stroke, fg = C["surface2"], C["input_border"], C["muted"]
    return (rect(x, y, w, h, fill, stroke, rx=6)
            + text(x + w / 2, y + h / 2 + 4.5, label, size=size, fill=fg, weight=600, anchor="middle")), w


def badge(x, y, label, kind="info", size=11.5):
    pal = {
        "info": (C["info_bg"], C["brand_dark"]),
        "ok": (C["success_bg"], C["success"]),
        "warn": (C["warn_bg"], C["warn"]),
        "bad": (C["danger_bg"], C["danger"]),
        "grey": (C["hairline"], C["muted"]),
        "plugin": ("#efe6f7", "#5b3d86"),
    }[kind]
    w = int(len(label) * 6.4) + 18
    return (rect(x, y, w, 20, pal[0], rx=10)
            + text(x + w / 2, y + 14, label, size=size, fill=pal[1], weight=600, anchor="middle")), w


def panel(x, y, w, h, title=None, subtitle=None, pad=18):
    out = [rect(x, y, w, h, C["white"], C["panel_border"], rx=10)]
    cy = y + pad
    if title:
        out.append(text(x + pad, cy + 12, title, size=16, fill=C["brand"], weight=600))
        cy += 22
        if subtitle:
            out.append(text(x + pad, cy + 12, subtitle, size=12.5, fill=C["muted"]))
            cy += 20
        out.append(line(x, cy + 14, x + w, cy + 14))
        cy += 14
    return "".join(out), cy


def field(x, y, w, label, value, placeholder=False, h=36, kind="text"):
    out = [text(x, y + 10, label, size=12, fill=C["muted"], weight=600)]
    top = y + 18
    out.append(rect(x, top, w, h, C["white"], C["input_border"], rx=6))
    fg = C["muted2"] if placeholder else C["body"]
    out.append(text(x + 11, top + h / 2 + 4.5, value, size=13.5, fill=fg))
    if kind == "select":
        out.append(text(x + w - 16, top + h / 2 + 5, "⌄", size=15, fill=C["muted"], anchor="middle"))
    return "".join(out), top + h


def checkbox(x, y, checked=True, size=15):
    out = [rect(x, y, size, size, C["brand"] if checked else C["white"],
                C["brand"] if checked else C["input_border"], rx=3)]
    if checked:
        out.append(text(x + size / 2, y + size - 3, "✓", size=12, fill=C["white"], weight=700, anchor="middle"))
    return "".join(out)


def toggle(x, y, on=True):
    out = [rect(x, y, 34, 18, C["brand"] if on else "#c9d1d8", rx=9)]
    cx = x + 25 if on else x + 9
    out.append(f'<circle cx="{cx}" cy="{y + 9}" r="7" fill="#fff"/>')
    return "".join(out)


def radio(x, y, on=True):
    out = [f'<circle cx="{x + 7}" cy="{y + 7}" r="7" fill="#fff" stroke="{C["brand"] if on else C["input_border"]}" stroke-width="{2 if on else 1}"/>']
    if on:
        out.append(f'<circle cx="{x + 7}" cy="{y + 7}" r="3.5" fill="{C["brand"]}"/>')
    return "".join(out)


def table(x, y, w, cols, rows, row_h=38, head_h=36):
    """cols: list of (label, width, align). rows: list of list of cell specs.

    A cell is either a string or a dict: {"t": str, "badge": kind, "muted": bool,
    "weight": int, "raw": svg}
    """
    out = [rect(x, y, w, head_h, C["surface2"], rx=0)]
    out.append(line(x, y + head_h, x + w, y + head_h, C["panel_border"]))
    cx = x
    for label, cw, align in cols:
        tx = cx + 12 if align == "l" else (cx + cw - 12 if align == "r" else cx + cw / 2)
        anchor = {"l": "start", "r": "end", "c": "middle"}[align]
        out.append(text(tx, y + head_h / 2 + 4, label.upper(), size=10.5, fill=C["muted"],
                        weight=700, anchor=anchor, ls="0.8"))
        cx += cw
    ry = y + head_h
    for i, row in enumerate(rows):
        if i % 2 == 1:
            out.append(rect(x, ry, w, row_h, C["surface"], rx=0))
        cx = x
        for (label, cw, align), cell in zip(cols, row):
            anchor = {"l": "start", "r": "end", "c": "middle"}[align]
            tx = cx + 12 if align == "l" else (cx + cw - 12 if align == "r" else cx + cw / 2)
            if isinstance(cell, dict):
                if "badge" in cell:
                    bx = cx + 12 if align == "l" else cx + cw / 2 - 30
                    b, bw = badge(bx, ry + row_h / 2 - 10, cell["t"], cell["badge"])
                    out.append(b)
                elif "raw" in cell:
                    out.append(cell["raw"](cx, ry, cw, row_h))
                else:
                    out.append(text(tx, ry + row_h / 2 + 4.5, cell["t"], size=13,
                                    fill=C["muted"] if cell.get("muted") else C["body"],
                                    weight=cell.get("weight", 400), anchor=anchor))
            else:
                out.append(text(tx, ry + row_h / 2 + 4.5, cell, size=13, anchor=anchor))
            cx += cw
        out.append(line(x, ry + row_h, x + w, ry + row_h, C["hairline"]))
        ry += row_h
    return "".join(out), ry


def chrome(active="Members", title="", eyebrow_text="", subtitle="", unread=None, actions=()):
    """Navbar + page header. Returns (svg, content_top)."""
    out = [rect(0, 0, W, H, C["page_bg"], rx=0)]
    out.append(rect(0, 0, W, 56, C["brand"], rx=0))
    out.append(text(28, 34, "ClubGear", size=19, fill=C["white"], weight=700))
    nav = ["Dashboard", "Members", "Organisation", "Calendar", "Plugins", "Admin"]
    nx = 150
    for n in nav:
        w = int(len(n) * 7.6) + 26
        if n == active:
            out.append(rect(nx, 14, w, 28, "rgba(255,255,255,0.18)", rx=6))
        out.append(text(nx + w / 2, 34, n, size=13.5, fill=C["white"], weight=600 if n == active else 400,
                        anchor="middle", op=1 if n == active else 0.85))
        nx += w + 4
    # bell + avatar
    out.append(text(W - 96, 36, "✉", size=19, fill=C["white"], anchor="middle", op=0.9))
    if unread:
        out.append(f'<circle cx="{W - 86}" cy="20" r="9" fill="{C["accent"]}"/>')
        out.append(text(W - 86, 24, str(unread), size=11, fill=C["white"], weight=700, anchor="middle"))
    out.append(f'<circle cx="{W - 44}" cy="28" r="15" fill="rgba(255,255,255,0.22)"/>')
    out.append(text(W - 44, 33, "MB", size=12.5, fill=C["white"], weight=700, anchor="middle"))

    y = 56 + 24
    if eyebrow_text:
        out.append(eyebrow(28, y + 8, eyebrow_text))
        y += 18
    out.append(text(28, y + 22, title, size=25, fill=C["brand"], weight=700))
    y += 30
    if subtitle:
        out.append(text(28, y + 16, subtitle, size=13.5, fill=C["muted"]))
        y += 20
    ax = W - 28
    for label, kind in reversed(actions):
        b, bw = btn(0, 0, label, kind)
        ax -= bw
        b, _ = btn(ax, 88, label, kind)
        out.append(b)
        ax -= 10
    return "".join(out), y + 22


def svg(parts, w=W, h=H):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" '
            f'viewBox="0 0 {w} {h}" font-family="{FONT}">'
            + "".join(parts) + "</svg>")


def write(name, parts, w=W, h=H):
    import os
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), name)
    with open(p, "w", encoding="utf-8") as f:
        f.write(svg(parts, w, h))
    return p


def tabs(x, y, w, labels, active=0, size=13.5):
    out = [line(x, y + 34, x + w, y + 34, C["panel_border"])]
    tx = x
    for i, lab in enumerate(labels):
        tw = int(len(lab) * 7.4) + 34
        if i == active:
            out.append(rect(tx, y + 30, tw, 4, C["brand"], rx=2))
        out.append(text(tx + tw / 2, y + 22, lab, size=size,
                        fill=C["brand"] if i == active else C["muted"],
                        weight=600 if i == active else 400, anchor="middle"))
        tx += tw
    return "".join(out), y + 34


def kpi(x, y, w, label, value, sub=None, h=88, accent=None):
    out = [rect(x, y, w, h, C["white"], C["panel_border"], rx=10)]
    if accent:
        out.append(rect(x, y, 4, h, accent, rx=2))
    out.append(eyebrow(x + 18, y + 26, label))
    out.append(text(x + 18, y + 56, value, size=25, fill=C["ink"], weight=700))
    if sub:
        out.append(text(x + 18, y + 76, sub, size=11.5, fill=C["muted"]))
    return "".join(out)


def progress(x, y, w, pct, h=8, color=None):
    color = color or C["brand"]
    return (rect(x, y, w, h, "#e9edf1", rx=h / 2)
            + rect(x, y, max(4, w * pct / 100), h, color, rx=h / 2))


def scrim():
    return rect(0, 0, W, H, "#0b2233", rx=0, opacity=0.42)


def modal(x, y, w, h, title, subtitle=None):
    out = [rect(x + 3, y + 6, w, h, "#0b2233", rx=12, opacity=0.18)]
    out.append(rect(x, y, w, h, C["white"], C["panel_border"], rx=12))
    out.append(text(x + 24, y + 36, title, size=18, fill=C["brand"], weight=700))
    cy = y + 46
    if subtitle:
        out.append(text(x + 24, y + 58, subtitle, size=12.5, fill=C["muted"]))
        cy = y + 68
    out.append(text(x + w - 24, y + 36, "✕", size=15, fill=C["muted"], anchor="middle"))
    out.append(line(x, cy + 8, x + w, cy + 8))
    return "".join(out), cy + 8


def avatar(x, y, initials, r=16, fill="#d8e6ef", fg=None):
    fg = fg or C["brand_dark"]
    return (f'<circle cx="{x + r}" cy="{y + r}" r="{r}" fill="{fill}"/>'
            + text(x + r, y + r + 5, initials, size=r * 0.72, fill=fg, weight=700, anchor="middle"))


def alert(x, y, w, msg, kind="info", h=44, icon="ⓘ"):
    pal = {"info": (C["info_bg"], C["brand_dark"]), "ok": (C["success_bg"], C["success"]),
           "warn": (C["warn_bg"], C["warn"]), "bad": (C["danger_bg"], C["danger"])}[kind]
    return (rect(x, y, w, h, pal[0], rx=8)
            + text(x + 16, y + h / 2 + 5, icon, size=14, fill=pal[1])
            + text(x + 38, y + h / 2 + 5, msg, size=13, fill=pal[1]))


def note(x, y, w, lines, title="Architecture note"):
    """Dashed callout used to tie a screen to the architecture decision."""
    h = 30 + 18 * len(lines)
    out = [rect(x, y, w, h, "#fffaf2", "#f0d5ab", rx=8, dash="4 3")]
    out.append(text(x + 14, y + 22, title, size=12, fill="#8a5a00", weight=700))
    for i, l in enumerate(lines):
        out.append(text(x + 14, y + 42 + i * 18, l, size=12, fill="#7a5310"))
    return "".join(out), y + h


def chip(x, y, label, on=False):
    w = int(len(label) * 6.8) + 26
    return (rect(x, y, w, 26, "rgba(0,119,186,0.10)" if on else C["white"],
                 C["brand"] if on else C["input_border"], rx=13)
            + text(x + w / 2, y + 17.5, label, size=12,
                   fill=C["brand_dark"] if on else C["muted"],
                   weight=600 if on else 400, anchor="middle")), w


def wrap(s, budget):
    """Word-wrap a string into lines of at most `budget` characters."""
    words, lines, cur = s.split(), [], ""
    for w in words:
        if cur and len(cur) + 1 + len(w) > budget:
            lines.append(cur)
            cur = w
        else:
            cur = f"{cur} {w}".strip()
    if cur:
        lines.append(cur)
    return lines


def bullet_list(x, y, items, budget=46, size=11.8, gap=17, item_gap=10, mark="✓", mark_fill=None):
    mark_fill = mark_fill or C["success"]
    out, cy = [], y
    for it in items:
        out.append(text(x, cy, mark, size=12.5, fill=mark_fill, weight=700))
        for j, l in enumerate(wrap(it, budget)):
            out.append(text(x + 20, cy + j * gap, l, size=size, fill=C["body"]))
        cy += gap * len(wrap(it, budget)) + item_gap
    return "".join(out), cy
