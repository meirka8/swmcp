"""RUNG 3 - CSWA-style exam part 'UAT-CSWA-01', fully specified below, built
through the server, then the exam question answered: what is its mass?

SPEC (all sketches on Front Plane = XY, all extrudes blind along +Z, merged):
  1. Base plate       rectangle (-50,-30) to (50,30) mm, extruded 20 mm
  2. Upright wall     rectangle (-50, 10) to (50,30) mm, extruded 60 mm (merges
                      with the base; only the 40 mm above z=20 is new material)
  3. Cylindrical boss circle r=15 mm at (0,-10), extruded 45 mm (only the 25 mm
                      above z=20 is new material)
  4. Bore             circle r=8 mm at (0,-10), cut through all (45 mm of material)
  5. Angled cut       through-all cut removing the corner triangle
                      (30,-30) (50,-30) (50,-10) mm -> 200 mm^2 x 20 mm deep
  6. Fillet           R10 on the vertical base corner edge at (-50,-30), 20 mm tall
  Material: 6061 Alloy (2700 kg/m^3)

HAND CALCULATION
  base      100 x 60 x 20            = 120 000.000 mm^3
  wall      100 x 20 x 40            =  80 000.000
  boss      pi x 15^2 x 25           =  17 671.459
  bore     -pi x  8^2 x 45           =  -9 047.787
  angled   -0.5 x 20 x 20 x 20       =  -4 000.000
  fillet   -(1-pi/4) x 10^2 x 20     =    -429.204
  TOTAL                              = 204 194.468 mm^3
  mass = 204194.468e-9 m^3 x 2700 kg/m^3 = 0.5513251 kg = 551.325 g
"""
import json
import math
from uat_client import Session, bbox_mm

EXPECTED_VOL_MM3 = 120000 + 80000 + math.pi * 225 * 25 - math.pi * 64 * 45 \
                   - 4000 - (1 - math.pi / 4) * 100 * 20
DENSITY = 2700.0

SET_MATERIAL = {
    "name": "set_material",
    "summary": "Applies a SolidWorks material to the part (IPartDoc.SetMaterialPropertyName2). database is usually 'SOLIDWORKS Materials' or the full path to a .sldmat file; name is the material name as shown in the material tree, e.g. '6061 Alloy'. configuration '' means the active configuration.",
    "scope": "document",
    "target": "",
    "kind": "method",
    "member": "SetMaterialPropertyName2",
    "requires": [{"check": "documentType", "value": "Part"}],
    "params": [
        {"name": "configuration", "type": "string", "default": ""},
        {"name": "database", "type": "string", "default": "SOLIDWORKS Materials"},
        {"name": "name", "type": "string", "required": True},
    ],
    "returns": {"type": "void"},
    "verify": [],
}


def sketch_poly(s, doc, pts):
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        s.op("create_line", {"x1": f"{ax} mm", "y1": f"{ay} mm",
                             "x2": f"{bx} mm", "y2": f"{by} mm"}, doc, echo=False)


with Session(quiet=True) as s:
    print("--- discovery: material + save members on the document root ---")
    r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": ""}, echo=False)
    for m in r.get("members", []):
        if m["name"].startswith("SaveAs") or "MaterialPropertyName" in m["name"]:
            print(f"  {m['kind']:12} {m['name']:28} params={m['paramCount']} -> {m['returnType']}")
    print("register set_material ->",
          json.dumps(s.call("register_operation", {"recipe": SET_MATERIAL}, echo=False)))

    doc = s.op("new_part")["return"]["title"]
    print(f"\n### scratch doc: {doc}")
    tries = {"total": 0, "failed": 0}

    def step(label, op, args, expect_ok=True):
        tries["total"] += 1
        r = s.op(op, args, doc, must_succeed=False, echo=False)
        ok = r.get("success")
        if not ok:
            tries["failed"] += 1
        print(f"  [{'ok ' if ok else 'FAIL'}] {label}"
              + ("" if ok else f"\n         {r.get('error')[:220]}"))
        return r

    # 1 base -------------------------------------------------------------
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 1", "insert_sketch", {})
    sketch_poly(s, doc, [(-50, -30), (50, -30), (50, 30), (-50, 30), (-50, -30)])
    step("exit sketch 1", "exit_sketch", {})
    step("select Sketch1", "select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0})
    step("extrude base 20mm", "extrude_boss", {"depth1": "20 mm"})

    # 2 wall -------------------------------------------------------------
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 2", "insert_sketch", {})
    sketch_poly(s, doc, [(-50, 10), (50, 10), (50, 30), (-50, 30), (-50, 10)])
    step("exit sketch 2", "exit_sketch", {})
    step("select Sketch2", "select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0})
    step("extrude wall 60mm", "extrude_boss", {"depth1": "60 mm"})

    # 3 boss -------------------------------------------------------------
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 3", "insert_sketch", {})
    s.op("create_circle_by_radius", {"centerX": "0 mm", "centerY": "-10 mm", "radius": "15 mm"},
         doc, echo=False)
    step("exit sketch 3", "exit_sketch", {})
    step("select Sketch3", "select_by_id", {"name": "Sketch3", "type": "SKETCH", "mark": 0})
    step("extrude boss 45mm", "extrude_boss", {"depth1": "45 mm"})

    # 4 bore -------------------------------------------------------------
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 4", "insert_sketch", {})
    s.op("create_circle_by_radius", {"centerX": "0 mm", "centerY": "-10 mm", "radius": "8 mm"},
         doc, echo=False)
    step("exit sketch 4", "exit_sketch", {})
    step("select Sketch4", "select_by_id", {"name": "Sketch4", "type": "SKETCH", "mark": 0})
    step("cut bore through all", "cut_extrude", {"endCondition1": 1, "reverseDirection": True})

    # 5 angled cut -------------------------------------------------------
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 5", "insert_sketch", {})
    sketch_poly(s, doc, [(30, -30), (50, -10), (65, -10), (65, -45), (30, -45), (30, -30)])
    step("exit sketch 5", "exit_sketch", {})
    step("select Sketch5", "select_by_id", {"name": "Sketch5", "type": "SKETCH", "mark": 0})
    step("cut angled corner through all", "cut_extrude", {"endCondition1": 1, "reverseDirection": True})

    # 6 fillet (select_by_ray, because select_by_id's coordinate pick is unreliable)
    step("clear selection", "clear_selection", {})
    step("ray-select base corner edge (-50,-30)", "select_by_ray",
         {"x": "-50 mm", "y": "-30 mm", "z": "30 mm", "rz": -1, "radius": "0.2 mm", "type": 1})
    step("fillet R10", "fillet_constant_radius", {"radius": "10 mm"})
    step("rebuild", "rebuild", {})

    # measure before material ------------------------------------------
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    vol = info["mass"] * 1e9 / 1000.0  # default no-material density is 1000 kg/m^3
    print(f"\n  geometry check: volume = {vol:.3f} mm^3, expected {EXPECTED_VOL_MM3:.3f} mm^3, "
          f"error {100*(vol-EXPECTED_VOL_MM3)/EXPECTED_VOL_MM3:+.4f}%")
    print("  bbox mm:", bbox_mm(info), "(expected 100 x 60 x 60)")

    # 7 material ---------------------------------------------------------
    step("apply 6061 Alloy", "set_material", {"name": "6061 Alloy"})
    step("rebuild", "rebuild", {})
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    mass = info["mass"]
    expected = EXPECTED_VOL_MM3 * 1e-9 * DENSITY
    print(f"\n=== EXAM ANSWER ===")
    print(f"  reported mass  = {mass*1000:.3f} g")
    print(f"  hand-calc mass = {expected*1000:.3f} g  (V={EXPECTED_VOL_MM3:.3f} mm^3 x 2700 kg/m^3)")
    print(f"  error          = {100*(mass-expected)/expected:+.4f} %")
    print(f"  implied density = {mass*1e9/vol if vol else 0:.1f} kg/m^3 (6061 Alloy = 2700)")
    print(f"\n  steps attempted {tries['total']}, failed {tries['failed']}")
    print("  features:", [(f["name"], f["typeName"]) for f in info["features"]
                          if f["typeName"] in ("Extrusion", "ICE", "Cut", "Fillet")])
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
