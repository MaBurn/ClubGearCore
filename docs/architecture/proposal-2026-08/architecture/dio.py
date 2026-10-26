"""Minimal draw.io (mxGraph) XML builder."""
import base64, html, os

PAL = {
    "core":     ("#eaf3f9", "#0077BA", "#16354f"),
    "core_new": ("#d7ecf7", "#0077BA", "#16354f"),
    "plugin":   ("#f3ecfa", "#5b3d86", "#3d2a5c"),
    "contract": ("#fff4e3", "#EF7D00", "#7a4a00"),
    "runtime":  ("#eef1f4", "#5f6368", "#2b3238"),
    "data":     ("#e6f4ec", "#187344", "#0f4d2d"),
    "ext":      ("#f4f6f8", "#8a949c", "#3f474d"),
    "actor":    ("#ffffff", "#16354f", "#16354f"),
    "warn":     ("#fdf3e0", "#c98a1e", "#7a5310"),
    "danger":   ("#fbe9e9", "#b05252", "#8a2f2f"),
    "ok":       ("#e4f2ea", "#187344", "#0f4d2d"),
    "band":     ("#f7f9fb", "#dde3e8", "#5f6368"),
}


def esc(s):
    return html.escape(str(s), quote=True).replace("\n", "&#10;")


class Page:
    def __init__(self, name, w=1600, h=1100):
        self.name, self.w, self.h = name, w, h
        self.cells = []
        self.n = 0

    def _id(self, prefix="n"):
        self.n += 1
        return f"{prefix}{self.n}"

    def node(self, x, y, w, h, label, kind="core", rounded=1, fontsize=13,
             dashed=0, bold=False, extra="", align="center"):
        fill, stroke, fc = PAL[kind]
        style = (f"rounded={rounded};whiteSpace=wrap;html=1;fillColor={fill};"
                 f"strokeColor={stroke};fontColor={fc};fontSize={fontsize};"
                 f"align={align};verticalAlign=middle;arcSize=8;"
                 f"{'dashed=1;dashPattern=6 4;' if dashed else ''}"
                 f"{'fontStyle=1;' if bold else ''}{extra}")
        i = self._id()
        self.cells.append(
            f'<mxCell id="{i}" value="{esc(label)}" style="{style}" vertex="1" parent="1">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        return i

    def band(self, x, y, w, h, label, kind="band", fontsize=12):
        fill, stroke, fc = PAL[kind]
        style = (f"rounded=1;whiteSpace=wrap;html=1;fillColor={fill};strokeColor={stroke};"
                 f"fontColor={fc};fontSize={fontsize};align=left;verticalAlign=top;"
                 f"spacingLeft=10;spacingTop=6;fontStyle=1;arcSize=4;dashed=0;")
        i = self._id("b")
        self.cells.append(
            f'<mxCell id="{i}" value="{esc(label)}" style="{style}" vertex="1" parent="1">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        return i

    def text(self, x, y, w, h, label, fontsize=12, color="#5f6368", align="left", bold=False):
        style = (f"text;html=1;strokeColor=none;fillColor=none;align={align};"
                 f"verticalAlign=top;fontSize={fontsize};fontColor={color};"
                 f"{'fontStyle=1;' if bold else ''}")
        i = self._id("t")
        self.cells.append(
            f'<mxCell id="{i}" value="{esc(label)}" style="{style}" vertex="1" parent="1">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        return i

    def edge(self, src, tgt, label="", color="#0077BA", dashed=0, style_extra="",
             exitp=None, entryp=None, fontsize=11):
        style = (f"edgeStyle=orthogonalEdgeStyle;rounded=1;html=1;jettySize=auto;"
                 f"orthogonalLoop=1;strokeColor={color};fontColor=#4d5358;fontSize={fontsize};"
                 f"endArrow=blockThin;endFill=1;labelBackgroundColor=#ffffff;"
                 f"{'dashed=1;dashPattern=6 4;' if dashed else ''}{style_extra}")
        if exitp:
            style += f"exitX={exitp[0]};exitY={exitp[1]};exitDx=0;exitDy=0;"
        if entryp:
            style += f"entryX={entryp[0]};entryY={entryp[1]};entryDx=0;entryDy=0;"
        i = self._id("e")
        self.cells.append(
            f'<mxCell id="{i}" value="{esc(label)}" style="{style}" edge="1" parent="1" '
            f'source="{src}" target="{tgt}"><mxGeometry relative="1" as="geometry"/></mxCell>')
        return i

    def image(self, x, y, w, h, path, label=""):
        with open(path, "rb") as f:
            b64 = base64.b64encode(f.read()).decode()
        style = (f"shape=image;verticalLabelPosition=bottom;labelBackgroundColor=none;"
                 f"verticalAlign=top;aspect=fixed;imageAspect=0;fontSize=13;fontColor=#16354f;"
                 f"image=data:image/svg+xml,{b64}")
        i = self._id("img")
        self.cells.append(
            f'<mxCell id="{i}" value="{esc(label)}" style="{style}" vertex="1" parent="1">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        return i

    def xml(self, idx):
        return (f'<diagram id="pg{idx}" name="{esc(self.name)}">'
                f'<mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" guides="1" tooltips="1" '
                f'connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="{self.w}" '
                f'pageHeight="{self.h}" math="0" shadow="0"><root>'
                f'<mxCell id="0"/><mxCell id="1" parent="0"/>'
                + "".join(self.cells) +
                '</root></mxGraphModel></diagram>')


def write_file(path, pages):
    body = "".join(p.xml(i + 1) for i, p in enumerate(pages))
    xml = ('<mxfile host="app.diagrams.net" agent="ClubGear architecture proposal" '
           f'version="24.7.17" type="device">{body}</mxfile>')
    with open(path, "w", encoding="utf-8") as f:
        f.write(xml)
    return path
