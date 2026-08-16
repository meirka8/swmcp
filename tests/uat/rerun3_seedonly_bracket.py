"""Re-verdict step 2: build a real bracket through the SEED ONLY.
Zero register_operation calls. If this needs enrichment, the promotions failed.

Plate 80 x 40 x 6 mm (create_corner_rectangle + extrude_boss)
Two dia-5 mm through-holes at (+/-25, 0) (create_circle_by_radius + cut_extrude)
R6 fillet on the vertical corner edge at (40, 20) (select_by_ray + fillet_constant_radius)
Material 6061 Alloy (set_material), saved to a temp path (save_as)

  19200                       plate
   -2 x pi x 2.5^2 x 6        =  -235.6194
   -(1 - pi/4) x 6^2 x 6      =   -46.3540
  = 18918.0266 mm^3 x 2700 kg/m^3 = 51.0787 g
"""
import json
import math
import os
from pathlib import Path
from uat_client import Session, bbox_mm

TMP = Path(os.environ["TEMP"]) / "swmcp_uat"
TMP.mkdir(parents=True, exist_ok=True)
OUT = TMP / "UAT Bracket rev C.SLDPRT"
VOL = 19200 - 2 * math.pi * 6.25 * 6 - (1 - math.pi / 4) * 36 * 6
EXPECT_G = VOL * 1e-9 * 2700 * 1000
res = []


def check(n, c, d=""):
    res.append((n, bool(c)))
    print(f"[{'PASS' if c else 'FAIL'}] {n}" + (f"\n        {d}" if d else ""))


with Session(quiet=True) as s:
    seed = {o["name"]: o["source"] for o in s.call("list_operations", {}, echo=False)["operations"]}
    print("create_corner_rectangle params:",
          [(p["name"], p["type"], p.get("required"), p.get("default"))
           for p in s.call("describe_operation", {"operation": "create_corner_rectangle"},
                           echo=False)["params"]])

    doc = s.op("new_part")["return"]["title"]
    print(f"\n### scratch doc: {doc}")
    used, fails = [], []

    def step(label, op, args=None):
        used.append(op)
        r = s.op(op, args, doc, must_succeed=False, echo=False)
        if not r.get("success"):
            fails.append((label, r.get("error")))
            print(f"  [FAIL] {label}\n         {r.get('error')}")
        else:
            print(f"  [ok ] {label}"
                  + (f"   boundArgs={json.dumps(r.get('boundArgs'))[:110]}" if args else ""))
        return r

    # --- plate, as a run_operations BATCH (also checks boundArgs in batch results)
    batch = s.call("run_operations", {"documentName": doc, "steps": [
        {"operation": "select_by_id", "args": {"name": "Front Plane", "type": "PLANE"}},
        {"operation": "insert_sketch"},
        {"operation": "create_corner_rectangle",
         "args": {"x1": "-40 mm", "y1": "-20 mm", "x2": "40 mm", "y2": "20 mm"}},
        {"operation": "exit_sketch"},
        {"operation": "select_by_id", "args": {"name": "Sketch1", "type": "SKETCH", "mark": 0}},
        {"operation": "extrude_boss", "args": {"depth1": "6 mm"}},
    ]}, echo=False)
    ok = "error" not in batch
    check("plate built as a 6-step run_operations batch", ok,
          "" if ok else json.dumps(batch)[:400])
    if ok:
        rect = batch["completedSteps"][2]
        check("run_operations results carry boundArgs too", "boundArgs" in rect["result"],
              f"rect boundArgs = {json.dumps(rect['result'].get('boundArgs'))}")
    used += ["select_by_id", "insert_sketch", "create_corner_rectangle", "exit_sketch",
             "select_by_id", "extrude_boss"]
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    check("plate bbox is 80 x 40 x 6 mm", bbox_mm(info) == (80.0, 40.0, 6.0), f"{bbox_mm(info)}")

    # --- two through holes
    step("select Front Plane", "select_by_id", {"name": "Front Plane", "type": "PLANE"})
    step("insert sketch 2", "insert_sketch")
    step("hole circle +25", "create_circle_by_radius",
         {"centerX": "25 mm", "centerY": "0 mm", "radius": "2.5 mm"})
    step("hole circle -25", "create_circle_by_radius",
         {"centerX": "-25 mm", "centerY": "0 mm", "radius": "2.5 mm"})
    step("exit sketch 2", "exit_sketch")
    step("select Sketch2", "select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0})
    step("cut both holes through-all", "cut_extrude",
         {"endCondition1": 1, "reverseDirection": True})

    # --- fillet via select_by_ray (the promoted, reliable pick)
    step("clear selection", "clear_selection")
    step("ray-pick the corner edge at (40,20)", "select_by_ray",
         {"x": "40 mm", "y": "20 mm", "z": "30 mm", "rz": -1, "radius": "0.2 mm", "type": 1})
    step("fillet R6", "fillet_constant_radius", {"radius": "6 mm"})
    step("rebuild", "rebuild")

    # --- material + save
    step("apply 6061 Alloy", "set_material", {"name": "6061 Alloy"})
    step("rebuild", "rebuild")
    r = step("save as 'UAT Bracket rev C.SLDPRT'", "save_as", {"path": str(OUT)})
    check("save_as reported success", r.get("success") is True)
    check("saved file exists on disk", OUT.exists(),
          f"{OUT.name} size={OUT.stat().st_size if OUT.exists() else 'MISSING'}")

    # --- verify
    info = s.call("get_part_info", {"documentName": "UAT Bracket rev C.SLDPRT"}, echo=False)
    feats = [(f["name"], f["typeName"]) for f in info["features"]
             if f["typeName"] in ("Extrusion", "ICE", "Cut", "Fillet")]
    fillets = [f for f in info["features"] if f["typeName"] == "Fillet"]
    g = info["mass"] * 1000
    check("bbox still 80 x 40 x 6 mm", bbox_mm(info) == (80.0, 40.0, 6.0), f"{bbox_mm(info)}")
    check("a Fillet-type feature exists in the tree", len(fillets) == 1, f"{feats}")
    check("fillet DefaultRadius is 6 mm",
          fillets and abs(fillets[0]["data"]["DefaultRadius"] - 0.006) < 1e-9,
          f"{fillets[0]['data'] if fillets else None}")
    check("the fillet landed on the 6 mm vertical edge (mass proves which edge)",
          abs(g - EXPECT_G) < 0.002,
          f"mass {g:.4f} g vs hand-calc {EXPECT_G:.4f} g "
          f"(a 40 mm edge would give {(19200-235.6194-309.026)*1e-9*2700*1000:.4f} g)")
    check("density implies 6061 Alloy (2700 kg/m^3)",
          abs(info["mass"] * 1e9 / VOL - 2700) < 1.0,
          f"implied {info['mass']*1e9/VOL:.1f} kg/m^3")
    check("no register_operation was needed",
          all(seed.get(o) == "seed" for o in set(used)),
          f"ops used: {sorted(set(used))}")
    check("every step succeeded", not fails, f"{fails}")
    print(f"\n### CLEANUP: doc 'UAT Bracket rev C.SLDPRT', file {OUT}")

print("\n===== SUMMARY =====")
for n, o in res:
    print(f"  [{'PASS' if o else 'FAIL'}] {n}")
print(f"  {sum(1 for _, o in res if o)}/{len(res)} passed")
