"""Clean units + argument-binding test. Fresh sketch for every attempt, so a
failure can only come from the argument, not from a consumed profile."""
import json
from uat_client import Session


with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print("### scratch doc:", doc)
    n = 0

    def try_extrude(label, args):
        global n
        n += 1
        s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
        s.op("insert_sketch", {}, doc, echo=False)
        s.op("create_circle_by_radius", {"centerX": f"{n*60} mm", "centerY": 0,
                                         "radius": "5 mm"}, doc, echo=False)
        s.op("exit_sketch", {}, doc, echo=False)
        s.op("select_by_id", {"name": f"Sketch{n}", "type": "SKETCH", "mark": 0}, doc, echo=False)
        pre = s.call("get_part_info", {"documentName": doc}, echo=False)
        pre_mass = pre.get("mass") or 0.0
        a = {"merge": False}
        a.update(args)
        r = s.call("run_operation", {"operation": "extrude_boss", "args": a,
                                     "documentName": doc}, echo=False)
        post = s.call("get_part_info", {"documentName": doc}, echo=False)
        dv = ((post.get("mass") or 0.0) - pre_mass) * 1e6  # mm^3 @1000kg/m3
        depth_mm = dv / (3.14159265 * 25) if dv else 0
        print(f"\n{label}\n   args={json.dumps(args)}\n   success={r.get('success')} "
              f"resulting depth = {depth_mm:.4f} mm")
        if r.get("error"):
            print("   error:", r["error"][:300])
        if r.get("success"):
            s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
            s.op("rebuild", {}, doc, must_succeed=False, echo=False)

    try_extrude("A. depth1 = '6 mm'            (explicit unit)", {"depth1": "6 mm"})
    try_extrude("B. depth1 = 0.006             (bare number = metres)", {"depth1": 0.006})
    try_extrude("C. depth1 = 6                 (engineer types 6, means 6 mm)", {"depth1": 6})
    try_extrude("D. depth1 = '0.25 in'         (imperial)", {"depth1": "0.25 in"})
    try_extrude("E. depth1 = '6mm'             (no space)", {"depth1": "6mm"})
    try_extrude("F. depth1 = '6 millimeters'   (bad unit word)", {"depth1": "6 millimeters"})
    try_extrude("G. depth1 = '6 mm ' + junk    (garbage)", {"depth1": "six mm"})
    try_extrude("H. depth1 = -6 mm             (negative)", {"depth1": "-6 mm"})
    try_extrude("I. unknown param name         (typo 'depth' not 'depth1')",
                {"depth1": "6 mm", "thickness": "2 mm"})
    try_extrude("J. draftAngle1 = 5            (bare number on an ANGLE param = radians?)",
                {"depth1": "6 mm", "draftOn1": True, "draftAngle1": 5})
    try_extrude("K. draftAngle1 = '5 deg'      (explicit)",
                {"depth1": "6 mm", "draftOn1": True, "draftAngle1": "5 deg"})
    print(f"\n### DOC TITLE FOR CLEANUP: {doc}")
