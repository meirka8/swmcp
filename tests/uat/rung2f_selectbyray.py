"""Can the enrichment loop fix view-dependent edge picking?

Hypothesis: SelectByID2's x/y/z pick behaves like a screen pick and silently
returns False for entities facing away from the current view. IModelDocExtension
.SelectByRay is a model-space ray and should not care. Register it and compare.
"""
import json
from uat_client import Session

RAY = {
    "name": "select_by_ray",
    "summary": "Selects the entity hit by a model-space ray (IModelDocExtension.SelectByRay) - view independent, unlike select_by_id's coordinate hint. Point (x,y,z) is the ray origin, (rx,ry,rz) the direction, radius the pick tolerance in metres. type is a swSelectType_e integer (1=EDGE, 2=FACE, 3=VERTEX).",
    "scope": "document",
    "target": "Extension",
    "kind": "method",
    "member": "SelectByRay",
    "requires": [{"check": "documentType", "value": "Part"}],
    "params": [
        {"name": "x", "type": "length", "required": True},
        {"name": "y", "type": "length", "required": True},
        {"name": "z", "type": "length", "required": True},
        {"name": "rx", "type": "double", "default": 0},
        {"name": "ry", "type": "double", "default": 0},
        {"name": "rz", "type": "double", "default": -1},
        {"name": "radius", "type": "length", "default": 0.0005, "description": "pick tolerance"},
        {"name": "type", "type": "enum", "enum": "swSelectType_e", "default": 1, "description": "1 = EDGE, 2 = FACE"},
        {"name": "append", "type": "bool", "default": False},
        {"name": "mark", "type": "int", "default": 0},
        {"name": "option", "type": "int", "default": 0, "enum": "swSelectOption_e"},
    ],
    "returns": {"type": "bool"},
    "verify": [{"check": "returnTrue"}],
}

with Session(quiet=True) as s:
    print("--- Extension members matching 'Select' ---")
    r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": "Extension"}, echo=False)
    for m in r.get("members", []):
        if "select" in m["name"].lower():
            print(f"  {m['kind']:12} {m['name']:28} params={m['paramCount']} -> {m['returnType']}")

    print("\n--- register select_by_ray ---")
    print(json.dumps(s.call("register_operation", {"recipe": RAY}, echo=False), indent=2))

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
    base = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]

    # the corner that select_by_id refuses: (-40,-20). Ray straight down -Z through it.
    for label, args in [
        ("ray down -Z at (-40,-20)", {"x": -0.040, "y": -0.020, "z": 0.010, "rz": -1}),
        ("ray down -Z at (+40,+20)", {"x": 0.040, "y": 0.020, "z": 0.010, "rz": -1}),
    ]:
        s.op("clear_selection", {}, doc, echo=False)
        a = {"type": 1, "radius": 0.0002, "mark": 0}
        a.update(args)
        r = s.op("select_by_ray", a, doc, must_succeed=False, echo=False)
        print(f"{label}: success={r.get('success')} selCount={r['documentState']['selectionCount']} err={r.get('error')}")
        if r.get("success"):
            fr = s.op("fillet_constant_radius", {"radius": "6 mm"}, doc, must_succeed=False, echo=False)
            m = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]
            print(f"    fillet={fr.get('success')} removed={(base-m)*1e6:.2f} mm^3 "
                  f"(46.35 = the 6mm vertical corner edge)")
            s.op("undo", {"steps": 1}, doc, must_succeed=False, echo=False)
            s.op("rebuild", {}, doc, must_succeed=False, echo=False)
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
