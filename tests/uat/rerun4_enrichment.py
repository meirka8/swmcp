"""Re-verdict step 3: does the enrichment loop still close end to end?

Pick something NOT in the seed: a CHAMFER. Discover it with the new nameFilter,
register it, use it, verify the geometry numerically, then unregister it.

Plate 80x40x6. A 3 mm / 45 deg angle-distance chamfer on the 6 mm vertical corner
edge removes 0.5 x 3^2 x 6 = 27.000 mm^3 (vs a fillet's 46.354 mm^3 -- so the
number also proves it really made a CHAMFER and not something else).
"""
import json
from uat_client import Session

res = []


def check(n, c, d=""):
    res.append((n, bool(c)))
    print(f"[{'PASS' if c else 'FAIL'}] {n}" + (f"\n        {d}" if d else ""))


with Session(quiet=True) as s:
    print("=== 1. discover, using the new nameFilter ===")
    r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": "FeatureManager",
                                        "nameFilter": "chamfer"}, echo=False)
    print("  ", json.dumps(r.get("members")), f"totalCount={r.get('totalCount')}")
    m = next((x for x in r.get("members", []) if x["name"] == "InsertFeatureChamfer"), None)
    check("discovery finds InsertFeatureChamfer without paging tricks", m is not None,
          f"{m}")
    check("discovery reports its arity (8 params)", m and m["paramCount"] == 8)

    print("\n=== 2. register ===")
    CHAMFER = {
        "name": "chamfer_angle_distance",
        "summary": "Angle-distance chamfer on the pre-selected edge(s) (IFeatureManager.InsertFeatureChamfer). Select the edge first with select_by_ray (type 1 = EDGE). options 1 = tangent propagation; chamferType 1 = swChamferAngleDistance (distance measured on the first face, angle from it).",
        "scope": "document", "target": "FeatureManager", "kind": "method",
        "member": "InsertFeatureChamfer",
        "requires": [{"check": "documentType", "value": "Part"},
                     {"check": "notInSketchMode"},
                     {"check": "selectionCount", "min": 1}],
        "params": [
            {"name": "options", "type": "int", "default": 1,
             "enum": "swFeatureChamferOption_e", "description": "1 = tangent propagation"},
            {"name": "chamferType", "type": "enum", "enum": "swChamferType_e", "default": 1,
             "description": "1 = AngleDistance, 2 = DistanceDistance, 3 = Vertex"},
            {"name": "distance", "type": "length", "required": True},
            {"name": "angle", "type": "angle", "default": 0.7853981633974483},
            {"name": "otherDistance", "type": "length", "default": 0},
            {"name": "vertexDistance1", "type": "length", "default": 0},
            {"name": "vertexDistance2", "type": "length", "default": 0},
            {"name": "vertexDistance3", "type": "length", "default": 0},
        ],
        "returns": {"type": "feature"},
        "verify": [{"check": "returnNotNull"}, {"check": "featureCountIncreased", "by": 1}],
    }
    reg = s.call("register_operation", {"recipe": CHAMFER}, echo=False)
    print("  ", json.dumps(reg))
    check("register_operation accepted the recipe", reg.get("registered") == "chamfer_angle_distance")
    ops = {o["name"]: o["source"] for o in s.call("list_operations", {}, echo=False)["operations"]}
    check("it is listed with source 'registered'", ops.get("chamfer_angle_distance") == "registered")

    print("\n=== 3. use it ===")
    doc = s.op("new_part")["return"]["title"]
    print("   scratch doc:", doc)
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_corner_rectangle", {"x1": "-40 mm", "y1": "-20 mm",
                                     "x2": "40 mm", "y2": "20 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": "6 mm"}, doc, echo=False)
    base = s.call("get_part_info", {"documentName": doc}, echo=False)["mass"]

    s.op("clear_selection", {}, doc, echo=False)
    s.op("select_by_ray", {"x": "40 mm", "y": "20 mm", "z": "10 mm", "rz": -1,
                           "radius": "0.2 mm", "type": 1}, doc, echo=False)
    r = s.op("chamfer_angle_distance", {"distance": "3 mm", "angle": "45 deg"},
             doc, must_succeed=False, echo=False)
    print("   run ->", json.dumps({k: r.get(k) for k in ("success", "return", "boundArgs")}))
    check("the registered chamfer runs successfully", r.get("success") is True,
          (r.get("error") or "")[:200])
    check("boundArgs shows 45 deg bound as 0.7853981634 rad",
          r.get("boundArgs") and abs(r["boundArgs"]["angle"] - 0.7853981633974483) < 1e-12
          and abs(r["boundArgs"]["distance"] - 0.003) < 1e-12,
          json.dumps(r.get("boundArgs")))

    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    removed = (base - info["mass"]) * 1e6
    ch = [f for f in info["features"] if f["typeName"] == "Chamfer"]
    check("a Chamfer-type feature is in the tree", len(ch) == 1,
          f"{[(f['name'], f['typeName']) for f in info['features'] if f['typeName'] in ('Extrusion','ICE','Chamfer','Fillet')]}")
    check("geometry is right: 27.000 mm^3 removed (a fillet would be 46.354)",
          abs(removed - 27.0) < 0.05, f"removed {removed:.3f} mm^3")
    check("read-back schema agrees: Distance 3 mm, Angle 45 deg",
          ch and abs(ch[0]["data"]["Distance"] - 0.003) < 1e-9
          and abs(ch[0]["data"]["Angle"] - 0.7853981633974483) < 1e-9,
          json.dumps(ch[0]["data"]) if ch else "")

    print("\n=== 4. unregister ===")
    u = s.call("unregister_operation", {"operation": "chamfer_angle_distance"}, echo=False)
    print("  ", json.dumps(u))
    ops = [o["name"] for o in s.call("list_operations", {}, echo=False)["operations"]]
    check("gone from list_operations", "chamfer_angle_distance" not in ops)
    d = s.call("describe_operation", {"operation": "chamfer_angle_distance"}, echo=False)
    check("describe_operation no longer knows it", "error" in d, json.dumps(d)[:160])
    rr = s.call("run_operation", {"operation": "chamfer_angle_distance",
                                  "args": {"distance": "3 mm"}, "documentName": doc}, echo=False)
    check("running it now errors cleanly", "error" in rr and not rr.get("success"),
          json.dumps(rr)[:160])
    check("the chamfer it already made is still in the part (unregister != undo)",
          len([f for f in s.call("get_part_info", {"documentName": doc}, echo=False)["features"]
               if f["typeName"] == "Chamfer"]) == 1)
    print(f"\n### CLEANUP: {doc}")

print("\n===== SUMMARY =====")
for n, o in res:
    print(f"  [{'PASS' if o else 'FAIL'}] {n}")
print(f"  {sum(1 for _, o in res if o)}/{len(res)} passed")
