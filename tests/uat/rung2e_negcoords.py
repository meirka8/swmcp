"""Why did select_by_id at (-40,-20,3) fail when (+40,+20,3) worked?
Tests all four vertical corner edges, bare-metre numbers and 'mm' quantity strings.
Also: what does get_part_info return for a part with no solid body?
"""
import json
from uat_client import Session

with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print("### scratch doc:", doc)

    print("\n--- get_part_info on an EMPTY part (no solid body) ---")
    print(json.dumps(s.call("get_part_info", {"documentName": doc}, echo=False), indent=2)[:900])

    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    pts = [(-40, -20), (40, -20), (40, 20), (-40, 20), (-40, -20)]
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        s.op("create_line", {"x1": f"{ax} mm", "y1": f"{ay} mm",
                             "x2": f"{bx} mm", "y2": f"{by} mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc, echo=False)

    print("\n--- all four vertical corner edges, bare metres ---")
    for sx in (1, -1):
        for sy in (1, -1):
            s.op("clear_selection", {}, doc, echo=False)
            r = s.op("select_by_id", {"name": "", "type": "EDGE", "mark": 0,
                                      "x": 0.040 * sx, "y": 0.020 * sy, "z": 0.003},
                     doc, must_succeed=False, echo=False)
            print(f"  ({0.040*sx:+.3f}, {0.020*sy:+.3f}, 0.003) -> success={r.get('success')} "
                  f"selCount={r['documentState']['selectionCount']} err={r.get('error')}")

    print("\n--- same, as 'mm' quantity strings ---")
    for sx in (1, -1):
        for sy in (1, -1):
            s.op("clear_selection", {}, doc, echo=False)
            r = s.op("select_by_id", {"name": "", "type": "EDGE", "mark": 0,
                                      "x": f"{40*sx} mm", "y": f"{20*sy} mm", "z": "3 mm"},
                     doc, must_succeed=False, echo=False)
            print(f"  ({40*sx:+d}mm, {20*sy:+d}mm, 3mm) -> success={r.get('success')} "
                  f"selCount={r['documentState']['selectionCount']} err={r.get('error')}")

    print("\n--- midpoints of the four 40mm/80mm side edges at z=0 ---")
    for label, c in [("x=+40,y=0,z=0", (0.040, 0, 0)), ("x=-40,y=0,z=0", (-0.040, 0, 0)),
                     ("x=0,y=+20,z=0", (0, 0.020, 0)), ("x=0,y=-20,z=0", (0, -0.020, 0))]:
        s.op("clear_selection", {}, doc, echo=False)
        r = s.op("select_by_id", {"name": "", "type": "EDGE", "mark": 0,
                                  "x": c[0], "y": c[1], "z": c[2]}, doc, must_succeed=False, echo=False)
        print(f"  {label} -> success={r.get('success')} err={r.get('error')}")
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
