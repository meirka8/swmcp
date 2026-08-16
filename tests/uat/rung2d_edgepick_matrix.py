"""Discriminating test: after a cut-extrude, do ALL edge picks still resolve correctly,
or only the corner one go wrong? Also isolates whether the extra SKETCH or the CUT is the cause.

removal signature at R6: 46.35 = 6mm vertical edge, 309.03 = 40mm edge, 618.05 = 80mm edge.
"""
from uat_client import Session

PICKS = [
    ("vertical corner edge (40,20,3)", {"x": 0.040, "y": 0.020, "z": 0.003}),
    ("40mm bottom edge    (40, 0,0)", {"x": 0.040, "y": 0.000, "z": 0.000}),
    ("80mm top edge       ( 0,20,6)", {"x": 0.000, "y": 0.020, "z": 0.006}),
    ("vertical corner edge (-40,-20,3)", {"x": -0.040, "y": -0.020, "z": 0.003}),
]


def plate(s, doc):
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    pts = [(-40, -20), (40, -20), (40, 20), (-40, 20), (-40, -20)]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        s.op("create_line", {"x1": f"{ax} mm", "y1": f"{ay} mm",
                             "x2": f"{bx} mm", "y2": f"{by} mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc, echo=False)


def sweep(s, doc, label):
    pre = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]
    print(f"-- {label}: mass {pre:.9f}")
    for name, c in PICKS:
        s.op("clear_selection", {}, doc, echo=False)
        a = {"name": "", "type": "EDGE", "mark": 0}
        a.update(c)
        sel = s.op("select_by_id", a, doc, must_succeed=False, echo=False)
        fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc, must_succeed=False, echo=False)
        m = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]
        print(f"   {name}: sel={sel.get('success')} fillet={fr.get('success')} removed={(pre-m)*1e6:8.2f} mm^3")
        s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
        s.op("rebuild", {}, doc, must_succeed=False, echo=False)


with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print("### scratch doc:", doc)
    plate(s, doc)
    sweep(s, doc, "A: plain plate")

    # add an unused sketch only (no cut) -- isolates 'extra sketch' from 'cut'
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "-25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    sweep(s, doc, "B: plate + unconsumed sketch")

    s.op("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("cut_extrude", {"endCondition1": 1, "reverseDirection": True}, doc, echo=False)
    sweep(s, doc, "C: plate + 2 through holes")
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
