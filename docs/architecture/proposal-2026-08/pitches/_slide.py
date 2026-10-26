"""Pitch-slide kit — 16:9, reuses the mockup widget kit."""
import sys, os, base64
sys.path.insert(0, "/tmp/clubgear-proposal/mockups")
from _kit import (C, FONT, esc, rect, text, line, btn, badge, panel, eyebrow,
                  progress, wrap, avatar)

SW, SH = 1600, 900
MOCK = "/tmp/clubgear-proposal/mockups"


def svg(parts, w=SW, h=SH):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" '
            f'width="{w}" height="{h}" viewBox="0 0 {w} {h}" font-family="{FONT}">'
            + "".join(parts) + "</svg>")


def write(name, parts, w=SW, h=SH):
    p = os.path.join(os.path.dirname(os.path.abspath(__file__)), name)
    with open(p, "w", encoding="utf-8") as f:
        f.write(svg(parts, w, h))
    return p


def embed(path, x, y, w, h):
    with open(path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    return (f'<image x="{x}" y="{y}" width="{w}" height="{h}" preserveAspectRatio="xMidYMin slice" '
            f'xlink:href="data:image/svg+xml;base64,{b64}"/>'
            + rect(x, y, w, h, "none", C["panel_border"], rx=8))


def slide_bg(kicker, title, sub, tag=None, tagkind="info"):
    out = [rect(0, 0, SW, SH, C["page_bg"], rx=0)]
    out.append(rect(0, 0, SW, 8, C["brand"], rx=0))
    out.append(eyebrow(56, 62, kicker))
    out.append(text(56, 108, title, size=34, fill=C["brand"], weight=700))
    if tag:
        b, bw = badge(56, 124, tag, tagkind)
        out.append(b)
    out.append(text(56 + (0 if not tag else int(len(tag) * 6.4) + 34), 140, sub,
                    size=15, fill=C["muted"]))
    out.append(line(56, 168, SW - 56, 168, "#dde3e8"))
    return out


def scorebar(x, y, label, value, maxv=5, w=150, color=None):
    color = color or C["brand"]
    out = [text(x, y + 4, label, size=12, fill=C["muted"], weight=600)]
    bx = x + 132
    for i in range(maxv):
        filled = i < value
        out.append(rect(bx + i * (w / maxv + 4), y - 8, w / maxv, 12,
                        color if filled else "#e3e8ec", rx=3))
    out.append(text(bx + maxv * (w / maxv + 4) + 8, y + 4, f"{value}/{maxv}",
                    size=12, fill=C["ink"], weight=700))
    return "".join(out)


def column(x, y, w, title, color, items, budget=44, size=13, gap=19, item_gap=12, mark="·"):
    out = [text(x, y, title, size=14, fill=color, weight=700)]
    cy = y + 30
    for it in items:
        lines = wrap(it, budget)
        out.append(text(x, cy, mark, size=13, fill=color, weight=700))
        for j, l in enumerate(lines):
            out.append(text(x + 16, cy + j * gap, l, size=size, fill=C["body"]))
        cy += gap * len(lines) + item_gap
    return "".join(out), cy
