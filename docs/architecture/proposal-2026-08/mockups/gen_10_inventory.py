from _kit import *

o, top = chrome(active="Plugins", eyebrow_text="Plugin · INV-01  ·  clubgear.plugin.inventory",
                title="INV-2291  ·  Akkuschrauber Makita DHP484",
                subtitle="Warehouses, stock, images, barcodes, loans and a mobile API — all inside the plugin schema.",
                unread=3, actions=[("Print label", "outline"), ("Adjust stock", "primary")])
out = [o]

# left: images + identity
p, cy = panel(28, top, 330, 440, "Images", "Stored through CORE-02, ordered, one primary")
out.append(p)
out.append(rect(46, cy + 10, 294, 168, "#dde6ec", C["panel_border"], rx=8))
out.append(text(193, cy + 100, "primary image", size=13, fill=C["muted2"], anchor="middle"))
b, _ = badge(56, cy + 20, "primary", "ok"); out.append(b)
for i in range(4):
    out.append(rect(46 + i * 76, cy + 190, 66, 56, "#e7edf1", C["panel_border"], rx=6))
out.append(rect(46 + 3 * 76, cy + 190, 66, 56, C["surface"], C["input_border"], rx=6, dash="4 3"))
out.append(text(46 + 3 * 76 + 33, cy + 224, "＋", size=17, fill=C["muted"], anchor="middle"))
out.append(line(28, cy + 266, 358, cy + 266))
out.append(text(46, cy + 292, "Codes", size=13, fill=C["ink"], weight=700))
out.append(text(46, cy + 314, "EAN  4053423104745", size=12.5, fill=C["body"]))
out.append(text(46, cy + 334, "QR   cg-inv-2291", size=12.5, fill=C["body"]))
out.append(text(46, cy + 354, "Ref  RG-SUED/WZ/0091", size=12.5, fill=C["body"]))

# center: master data + stock
p, cy = panel(374, top, 560, 440, "Master data")
out.append(p)
f, y1 = field(392, cy + 4, 250, "Description", "Akkuschrauber Makita DHP484"); out.append(f)
f, _ = field(660, cy + 4, 256, "Category", "Elektrowerkzeug", kind="select"); out.append(f)
f, y2 = field(392, y1 + 14, 250, "Warehouse", "Werkstatt Süd", kind="select"); out.append(f)
f, _ = field(660, y1 + 14, 256, "Storage location", "Regal B · Fach 3", kind="select"); out.append(f)
f, y3 = field(392, y2 + 14, 250, "Owner unit", "Regionalgruppe Süd", kind="select"); out.append(f)
f, _ = field(660, y2 + 14, 120, "Quantity", "3"); out.append(f)
f, _ = field(796, y2 + 14, 120, "Unit", "Stück", kind="select"); out.append(f)
out.append(line(374, y3 + 24, 934, y3 + 24))
out.append(text(392, y3 + 50, "Stock movements are append-only. Concurrent adjustments cannot lose history.", size=12, fill=C["muted"]))

# right: loan
p, cy = panel(950, top, W - 950 - 28, 440, "Loan", "Simple lending — commercial rental is RENT-01")
out.append(p)
out.append(alert(968, cy + 10, W - 950 - 64, "On loan · due 02.09.2026", "warn", h=44, icon="⌛"))
out.append(text(968, cy + 82, "Borrower", size=12, fill=C["muted"], weight=600))
out.append(avatar(968, cy + 92, "CW", r=15))
out.append(text(1006, cy + 112, "Clara Weiss", size=13.5, fill=C["ink"], weight=600))
out.append(text(968, cy + 148, "Checked out 28.08.2026 by B. Kruse", size=12, fill=C["muted"]))
b, _ = btn(968, cy + 172, "Return", "primary", w=100); out.append(b)
b, _ = btn(1076, cy + 172, "Extend", "outline", w=94); out.append(b)
out.append(line(950, cy + 226, W - 28, cy + 226))
out.append(eyebrow(968, cy + 252, "Permissions in play"))
for i, l in enumerate(["inv.item.view (warehouse-scoped)", "inv.stock.adjust", "inv.loan.manage"]):
    out.append(text(968, cy + 276 + i * 22, "· " + l, size=12, fill=C["body"]))

# history
hy = top + 456
p, cy = panel(28, hy, 700, 344, "History", "Immutable — creation, edits, moves, adjustments, loans")
out.append(p)
cols = [("When", 116, "l"), ("Event", 176, "l"), ("Before → after", 178, "l"), ("Actor", 110, "l"), ("Note", 118, "l")]
rows = [
    ["28.08. 14:02", {"t": "Loan checked out", "weight": 600}, "3 → 2 available", "B. Kruse", "due 02.09."],
    ["19.08. 09:31", {"t": "Stock adjustment", "weight": 600}, "2 → 3", "A. Falk", "Spende"],
    ["04.07. 16:20", {"t": "Moved", "weight": 600}, "Nord → Werkstatt Süd", "A. Falk", "Regal B"],
    ["11.03. 10:05", {"t": "Image added", "weight": 600}, "—", "B. Kruse", "file 8f3a…c1"],
    ["02.02. 08:44", {"t": "Created", "weight": 600}, "—", "import", "CSV catalog"],
]
t, _ = table(29, cy + 6, 698, cols, rows)
out.append(t)

# mobile
mx = 744
p, cy = panel(mx, hy, W - mx - 28, 344, "Mobile API", "Same permissions as the web page")
out.append(p)
ph_x, ph_y, ph_w, ph_h = mx + 22, cy + 12, 130, 236
out.append(rect(ph_x, ph_y, ph_w, ph_h, "#1c2b36", rx=16))
out.append(rect(ph_x + 6, ph_y + 12, ph_w - 12, ph_h - 24, C["white"], rx=10))
out.append(rect(ph_x + 6, ph_y + 12, ph_w - 12, 26, C["brand"], rx=10))
out.append(text(ph_x + ph_w / 2, ph_y + 30, "Inventur", size=10.5, fill=C["white"], weight=700, anchor="middle"))
out.append(rect(ph_x + 16, ph_y + 48, ph_w - 32, 52, C["surface2"], C["input_border"], rx=6))
out.append(text(ph_x + ph_w / 2, ph_y + 70, "▣", size=17, fill=C["brand"], anchor="middle"))
out.append(text(ph_x + ph_w / 2, ph_y + 90, "Scan QR / EAN", size=9.5, fill=C["muted"], anchor="middle"))
out.append(text(ph_x + 16, ph_y + 122, "INV-2291", size=10.5, fill=C["ink"], weight=700))
out.append(text(ph_x + 16, ph_y + 138, "Akkuschrauber", size=9.5, fill=C["muted"]))
out.append(rect(ph_x + 16, ph_y + 150, ph_w - 32, 24, C["white"], C["input_border"], rx=5))
out.append(text(ph_x + 22, ph_y + 166, "Bestand  3", size=9.5, fill=C["body"]))
out.append(rect(ph_x + 16, ph_y + 182, ph_w - 32, 24, C["brand"], rx=5))
out.append(text(ph_x + ph_w / 2, ph_y + 198, "Buchen", size=10, fill=C["white"], weight=700, anchor="middle"))
ex = mx + 172
out.append(eyebrow(ex, cy + 30, "Namespaced endpoints"))
for i, l in enumerate(["GET   /api/p/inventory/items",
                       "GET   /api/p/inventory/items/by-code/{code}",
                       "POST  /api/p/inventory/items/{id}/stock",
                       "POST  /api/p/inventory/items/{id}/images"]):
    out.append(text(ex, cy + 54 + i * 22, l, size=11.5, fill=C["ink"]))
for i, l in enumerate(["Namespaced route owned by the plugin",
                       "Documented error envelope + pagination",
                       "Warehouse scope enforced identically",
                       "Contract version negotiated on activation"]):
    out.append(text(ex, cy + 162 + i * 24, "· " + l, size=11.5, fill=C["muted"]))

write("10-inventory.svg", out, W, 1004)
