import os, glob
from dio import Page, write_file
from p1_p2 import page_context, page_layers
from p3_p5 import page_dependencies, page_notifications, page_plugin_surface
from p6_p8 import page_files, page_ownership, page_roadmap
from p9 import page_storage_backends
from p10 import page_calendar_in_nextcloud
from p11 import page_do_not_execute

MOCKS = "/tmp/clubgear-proposal/mockups"

pages = [page_context(), page_layers(), page_dependencies(), page_notifications(),
         page_plugin_surface(), page_files(), page_ownership(), page_roadmap(),
         page_storage_backends(), page_calendar_in_nextcloud(),
         page_do_not_execute()]

titles = {
    "01": "CORE-01 · Organisation structure",
    "02": "CORE-02 · Secure file and media storage",
    "03": "CORE-03 · Host-rendered plugin page",
    "04": "CORE-04 · Notification inbox",
    "05": "CORE-04 · Notification preferences and channel providers",
    "06": "CORE-05 · Versioned templates",
    "07": "CORE-06 · Workflow engine",
    "08": "CORE-07 · Calendar and events",
    "09": "CORE-08 · Household relationships",
    "10": "INV-01 · Inventory 2.0",
    "11": "FIN-01 · Finance 2.0",
    "12": "NEWS-01 · Mass mail and newsletter",
    "13": "CORE-02 · Storage backend — local or Nextcloud",
    "14": "CORE-07 · Calendar storage — data lives in Nextcloud",
}

for f in sorted(glob.glob(os.path.join(MOCKS, "*.svg"))):
    key = os.path.basename(f)[:2]
    head = open(f, encoding="utf-8").read(400)
    import re
    w = int(re.search(r'width="(\d+)"', head).group(1))
    h = int(re.search(r'height="(\d+)"', head).group(1))
    pg = Page(f"M{key} · {titles[key].split(' · ')[0]}", w + 80, h + 140)
    pg.text(40, 24, 1000, 30, titles[key], 18, "#0077BA", bold=True)
    pg.text(40, 52, 1000, 24, "GUI mockup — layout and information architecture only, not a pixel spec.", 11.5)
    pg.image(40, 90, w, h, f)
    pages.append(pg)

out = write_file("/tmp/clubgear-proposal/architecture/ClubGear-Architecture-Proposal.drawio", pages)
print(out, os.path.getsize(out) // 1024, "KB,", len(pages), "pages")
