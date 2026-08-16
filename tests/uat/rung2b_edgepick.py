"""RUNG 2 follow-up - does select_by_id(type=EDGE, x,y,z) really select the edge at those coordinates?

Plate 80 x 40 x 6 mm, volume 19200 mm^3, density 1000 kg/m^3 (no material set).
A constant-radius R fillet on an edge of length L removes (1 - pi/4) * R^2 * L.
R = 6 mm:
  vertical corner edge (L = 6 mm)  -> 46.35 mm^3  -> mass 0.01915365 kg
  short  edge         (L = 40 mm)  -> 309.03 mm^3 -> mass 0.01889097 kg
  long   edge         (L = 80 mm)  -> 618.05 mm^3 -> mass 0.01858195 kg
So the mass tells us unambiguously which edge SolidWorks actually filleted.
"""
import json
from uat_client import Session, bbox_mm

PICKS = [
    ("corner vertical edge  (40, 20, 3)", {"x": 0.040, "y": 0.020, "z": 0.003}, 0.01915365),
    ("short edge x=40, z=0  (40,  0, 0)", {"x": 0.040, "y": 0.000, "z": 0.000}, 0.01889097),
    ("long edge y=20, z=6   ( 0, 20, 6)", {"x": 0.000, "y": 0.020, "z": 0.006}, 0.01858195),
]

with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print("### scratch doc:", doc)
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    pts = [(-40, -20), (40, -20), (40, 20), (-40, 20), (-40, -20)]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        s.op("create_line", {"x1": f"{ax} mm", "y1": f"{ay} mm",
                             "x2": f"{bx} mm", "y2": f"{by} mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc, echo=False)
    base = s.call("get_part_info", {"documentName": doc}, echo=False)
    print("base mass:", base["mass"], "bbox:", bbox_mm(base))

    for label, coords, expect in PICKS:
        s.op("clear_selection", {}, doc, echo=False)
        args = {"name": "", "type": "EDGE", "mark": 0}
        args.update(coords)
        sel = s.op("select_by_id", args, doc, must_succeed=False, echo=False)
        if not sel.get("success"):
            print(f"{label}: SELECT FAILED -> {sel.get('error')}")
            continue
        fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc, must_succeed=False, echo=False)
        if not fr.get("success"):
            print(f"{label}: FILLET FAILED -> {fr.get('error')}")
            continue
        info = s.call("get_part_info", {"documentName": doc}, echo=False)
        m = info["mass"]
        which = min(PICKS, key=lambda p: abs(p[2] - m))
        print(f"{label}: mass={m:.8f} expected={expect:.8f} "
              f"-> ACTUALLY FILLETED: {which[0]}  {'MATCH' if abs(m-expect)<1e-7 else '*** MISMATCH ***'}")
        # back it out
        u = s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
        s.op("rebuild", {}, doc, must_succeed=False, echo=False)
        after = s.call("get_part_info", {"documentName": doc}, echo=False)
        print(f"    undo -> success={u.get('success')} mass now {after['mass']:.8f} "
              f"(base {base['mass']:.8f}) {'restored' if abs(after['mass']-base['mass'])<1e-9 else '*** UNDO DID NOT RESTORE ***'}")
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
