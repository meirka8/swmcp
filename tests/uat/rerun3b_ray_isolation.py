"""Is select_by_ray ALSO unreliable, or did I aim it badly?

Plate 80x40x6 (z 0..6). R6 fillet removal signature (no material set, 1000 kg/m^3):
   46.354 mm^3 = the 6 mm vertical corner edge   <- what I want
  309.026 mm^3 = a 40 mm edge
  618.052 mm^3 = an 80 mm edge

Variables: ray origin height, ray direction, and whether the part has had a
topology change (two through-holes) since the coordinates were chosen.
"""
import math
from uat_client import Session

SIG = {46.354: "6mm VERTICAL corner edge (WANTED)", 309.026: "40 mm edge",
       618.052: "80 mm edge"}


def classify(v):
    if v < 1:
        return "nothing removed"
    k = min(SIG, key=lambda x: abs(x - v))
    return SIG[k] if abs(k - v) < 1.0 else f"unknown ({v:.2f})"


AIMS = [
    ("down -Z from z=10mm, r=0.2mm", {"x": "40 mm", "y": "20 mm", "z": "10 mm",
                                      "rz": -1, "radius": "0.2 mm"}),
    ("down -Z from z=30mm, r=0.2mm", {"x": "40 mm", "y": "20 mm", "z": "30 mm",
                                      "rz": -1, "radius": "0.2 mm"}),
    ("down -Z from z=10mm, r=0.02mm", {"x": "40 mm", "y": "20 mm", "z": "10 mm",
                                       "rz": -1, "radius": "0.02 mm"}),
    ("diagonal inward at mid-height z=3mm", {"x": "50 mm", "y": "30 mm", "z": "3 mm",
                                             "rx": -0.7071, "ry": -0.7071, "rz": 0,
                                             "radius": "0.2 mm"}),
    ("+X inward at mid-height z=3mm", {"x": "60 mm", "y": "20 mm", "z": "3 mm",
                                       "rx": -1, "rz": 0, "radius": "0.2 mm"}),
]


def plate(s, doc, holes):
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_corner_rectangle", {"x1": "-40 mm", "y1": "-20 mm",
                                     "x2": "40 mm", "y2": "20 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc, echo=False)
    if holes:
        s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
        s.op("insert_sketch", {}, doc, echo=False)
        s.op("create_circle_by_radius", {"centerX": "25 mm", "centerY": "0 mm",
                                         "radius": "2.5 mm"}, doc, echo=False)
        s.op("create_circle_by_radius", {"centerX": "-25 mm", "centerY": "0 mm",
                                         "radius": "2.5 mm"}, doc, echo=False)
        s.op("exit_sketch", {}, doc, echo=False)
        s.op("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc, echo=False)
        s.op("cut_extrude", {"endCondition1": 1, "reverseDirection": True}, doc, echo=False)


with Session(quiet=True) as s:
    for holes in (False, True):
        doc = s.op("new_part")["return"]["title"]
        print(f"\n##### {'plate + 2 through-holes' if holes else 'plain plate'}  (doc {doc})")
        plate(s, doc, holes)
        base = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]
        for label, aim in AIMS:
            s.op("clear_selection", {}, doc, echo=False)
            a = {"type": 1, "mark": 0}
            a.update(aim)
            sel = s.op("select_by_ray", a, doc, must_succeed=False, echo=False)
            if not sel.get("success"):
                print(f"  {label:42} SELECT FAILED: {(sel.get('error') or '')[:90]}")
                continue
            fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc,
                      must_succeed=False, echo=False)
            if not fr.get("success"):
                print(f"  {label:42} FILLET FAILED: {(fr.get('error') or '')[:90]}")
                continue
            m = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]
            v = (base - m) * 1e6
            print(f"  {label:42} removed {v:8.3f} mm^3 -> {classify(v)}")
            s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
            s.op("rebuild", {}, doc, must_succeed=False, echo=False)
        print(f"  ### CLEANUP: {doc}")
