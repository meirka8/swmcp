"""RUNG 1 - M8 washer, my dimensions: OD 16mm, ID 8.4mm, thickness 1.6mm.

Expected: bbox 16 x 16 x 1.6 mm, one extrude feature.
Volume = pi/4*(16^2-8.4^2)*1.6 = 201.0*1.6 ... see report.
"""
import json
from uat_client import Session, bbox_mm

OD, ID, T = 16.0, 8.4, 1.6

with Session() as s:
    r = s.op("new_part")
    doc = r["return"]["title"]
    print(f"### scratch doc: {doc}")

    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc)
    s.op("insert_sketch", {}, doc)
    s.op("create_circle_by_radius", {"centerX": 0, "centerY": 0, "radius": f"{OD/2} mm"}, doc)
    s.op("create_circle_by_radius", {"centerX": 0, "centerY": 0, "radius": f"{ID/2} mm"}, doc)
    s.op("exit_sketch", {}, doc)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc)
    s.op("extrude_boss", {"depth1": f"{T} mm"}, doc)
    s.op("rebuild", {}, doc)

    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    print(json.dumps(info, indent=2))
    print("BBOX mm:", bbox_mm(info))
    print("MASS kg:", info.get("mass"))
    print("FEATURES:", [(f["name"], f["typeName"]) for f in info["features"]])
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
