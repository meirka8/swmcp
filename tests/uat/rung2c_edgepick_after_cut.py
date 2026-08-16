"""Does an intervening cut-extrude change which edge select_by_id(EDGE, x,y,z) resolves to?

Same plate as rung2b (80x40x6) but with the two 5mm through-holes cut first,
then the SAME corner-edge pick (40, 20, 3) and the SAME R6 fillet.
Expected removal if the pick is honoured: 46.35 mm^3.
"""
import json
from uat_client import Session, bbox_mm

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

    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": "-25 mm", "centerY": 0, "radius": "2.5 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("cut_extrude", {"endCondition1": 1, "reverseDirection": True}, doc, echo=False)
    pre = s.call("get_part_info", {"documentName": doc}, echo=False)
    print(f"after cut mass = {pre['mass']:.9f}  (expect 0.018964381)")

    for label, coords in [("corner (40,20,3)", {"x": 0.040, "y": 0.020, "z": 0.003}),
                          ("corner (40,20,3) again", {"x": 0.040, "y": 0.020, "z": 0.003}),
                          ("corner, z=0.0045", {"x": 0.040, "y": 0.020, "z": 0.0045})]:
        s.op("clear_selection", {}, doc, echo=False)
        a = {"name": "", "type": "EDGE", "mark": 0}
        a.update(coords)
        s.op("select_by_id", a, doc, echo=False)
        fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc, must_succeed=False, echo=False)
        info = s.call("get_part_info", {"documentName": doc}, echo=False)
        removed = (pre["mass"] - info["mass"]) * 1e6  # mm^3 at 1000 kg/m3
        print(f"{label}: fillet success={fr.get('success')} removed={removed:.2f} mm^3 "
              f"(46.35=vertical corner edge, 309.03=40mm edge, 618.05=80mm edge)")
        s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
        s.op("rebuild", {}, doc, must_succeed=False, echo=False)
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
