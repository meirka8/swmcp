"""Re-verdict step 1: verify B1-B5 + unregister_operation + ambiguous documentName.
Each check prints PASS/FAIL on its own line."""
import json
import os
from pathlib import Path
from uat_client import Session

TMP = Path(os.environ["TEMP"]) / "swmcp_uat"
TMP.mkdir(parents=True, exist_ok=True)
results = []


def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(f"[{'PASS' if cond else 'FAIL'}] {name}" + (f"\n        {detail}" if detail else ""))


with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print(f"### scratch doc A: {doc}\n")
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_circle_by_radius", {"radius": "10 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)

    # ================= B1 =================
    print("=== B1: bare numbers on unit-carrying params ===")
    r = s.call("run_operation", {"operation": "extrude_boss", "args": {"depth1": 40},
                                 "documentName": doc}, echo=False)
    check("B1 the 40-metre part is REFUSED", r.get("success") is False, f"error: {r.get('error')}")
    check("B1 refusal names the accepted forms",
          r.get("error") and ("mm" in r["error"] and ("unit" in r["error"].lower())),
          "")
    r2 = s.call("run_operation", {"operation": "extrude_boss", "args": {"depth1": 6},
                                 "documentName": doc}, echo=False)
    check("B1 bare 6 also refused (not just implausible values)", r2.get("success") is False,
          f"error: {r2.get('error')}")
    r3 = s.call("run_operation", {"operation": "extrude_boss", "args": {"depth1": "6 mm"},
                                 "documentName": doc}, echo=False)
    check("B1 '6 mm' still works", r3.get("success") is True)
    ba = r3.get("boundArgs")
    print("        boundArgs:", json.dumps(ba))
    check("B1 response echoes boundArgs", ba is not None)
    check("B1 boundArgs shows the SI value actually passed (depth1 = 0.006 m)",
          ba and abs(float(ba.get("depth1", -1)) - 0.006) < 1e-12)
    info = s.call("get_part_info", {"documentName": doc}, echo=False)
    b = info["boundingBox"]
    zmm = (b["max"]["z"] - b["min"]["z"]) * 1000
    check("B1 geometry really is 6 mm, not 6000", abs(zmm - 6.0) < 0.001, f"bbox Z = {zmm} mm")
    # angle params
    r4 = s.call("run_operation", {"operation": "extrude_boss",
                                  "args": {"depth1": "6 mm", "draftOn1": True, "draftAngle1": 5},
                                  "documentName": doc}, echo=False)
    check("B1 bare number on an ANGLE param refused too", r4.get("success") is False,
          f"error: {(r4.get('error') or '')[:160]}")
    # zero should still be allowed (defaults are bare zeros)
    r5 = s.call("run_operation", {"operation": "select_by_ray",
                                  "args": {"x": "0 mm", "y": "0 mm", "z": "0 mm"}, "documentName": doc}, echo=False)
    print("        (bare 0 on a length param ->", r5.get("error") or "accepted", ")")

    # ================= B2 =================
    print("\n=== B2: unknown argument keys ===")
    s.op("clear_selection", {}, doc, echo=False)
    r = s.call("run_operation", {"operation": "extrude_boss",
                                 "args": {"depth1": "6 mm", "marks": 16},
                                 "documentName": doc}, echo=False)
    check("B2 unknown key {'marks': 16} rejected", r.get("success") is False,
          f"error: {(r.get('error') or '')[:400]}")
    check("B2 error lists accepted parameter names",
          r.get("error") and "depth1" in r["error"] and "reverseDirection" in r["error"])
    r = s.call("run_operation", {"operation": "extrude_boss",
                                 "args": {"DEPTH1": "6 mm"}, "documentName": doc}, echo=False)
    check("B2 matching is case-insensitive (DEPTH1 accepted, not treated as unknown)",
          "DEPTH1" not in (r.get("error") or "") and "unknown" not in (r.get("error") or "").lower(),
          f"-> success={r.get('success')} error={(r.get('error') or '')[:120]}")

    # ================= B4 =================
    print("\n=== B4: describe_com_members filters ===")
    r = s.call("describe_com_members", {"documentName": doc, "targetPath": "Extension",
                                        "nameFilter": "SelectByRay"}, echo=False)
    names = [m["name"] for m in r.get("members", [])]
    check("B4 SelectByRay is now FINDABLE on Extension via nameFilter", "SelectByRay" in names,
          f"totalCount={r.get('totalCount')} returned={len(names)} names={names}")
    r2 = s.call("describe_com_members", {"documentName": doc, "targetPath": "Extension"}, echo=False)
    check("B4 unfiltered call reports an honest totalCount",
          r2.get("totalCount") and r2["totalCount"] >= 359,
          f"totalCount={r2.get('totalCount')} returned={len(r2.get('members', []))} "
          f"truncated={r2.get('truncated')}")
    r3 = s.call("describe_com_members", {"documentName": doc, "targetPath": "Extension",
                                         "offset": 300, "limit": 100}, echo=False)
    n3 = [m["name"] for m in r3.get("members", [])]
    check("B4 offset/limit page past 300 and reach SelectByRay", "SelectByRay" in n3,
          f"page[300:400] returned {len(n3)} members")
    r4 = s.call("describe_com_members", {"documentName": doc, "targetPath": "FeatureManager",
                                         "nameFilter": "chamfer"}, echo=False)
    check("B4 nameFilter is case-insensitive",
          any("hamfer" in m["name"] for m in r4.get("members", [])),
          f"-> {[m['name'] for m in r4.get('members', [])]}")

    # ================= B5 =================
    print("\n=== B5: returnEquals / save_as ===")
    good = TMP / "UAT Blocker rev A.SLDPRT"
    r = s.call("run_operation", {"operation": "save_as", "args": {"path": str(good)},
                                 "documentName": doc}, echo=False)
    check("B5 a good save reports SUCCESS", r.get("success") is True,
          f"error={r.get('error')} boundArgs={json.dumps(r.get('boundArgs'))}")
    check("B5 the file is really on disk", good.exists(),
          f"{good} size={good.stat().st_size if good.exists() else 'MISSING'}")
    bad = Path(r"Z:\no such drive\nope.SLDPRT")
    r = s.call("run_operation", {"operation": "save_as", "args": {"path": str(bad)},
                                 "documentName": "UAT Blocker rev A.SLDPRT"}, echo=False)
    check("B5 a bad save path reports FAILURE", r.get("success") is False,
          f"error: {(r.get('error') or '')[:220]}")
    check("B5 the bad path did not create a file", not bad.exists())
    print("        save_as verify spec:",
          json.dumps(s.call("describe_operation", {"operation": "save_as"}, echo=False).get("verify")))

    # ================= unregister_operation =================
    print("\n=== unregister_operation ===")
    disposable = {
        "name": "uat_disposable", "summary": "throwaway recipe for the unregister test",
        "scope": "document", "target": "", "kind": "method", "member": "EditRebuild3",
        "requires": [], "params": [], "returns": {"type": "bool"},
        "verify": [{"check": "returnTrue"}],
    }
    print("        register ->", json.dumps(s.call("register_operation", {"recipe": disposable}, echo=False)))
    ops = [o["name"] for o in s.call("list_operations", {}, echo=False)["operations"]]
    check("unregister: recipe is listed after register", "uat_disposable" in ops)
    r = s.call("run_operation", {"operation": "uat_disposable",
                                 "documentName": "UAT Blocker rev A.SLDPRT"}, echo=False)
    check("unregister: registered recipe is usable", r.get("success") is True)
    u = s.call("unregister_operation", {"operation": "uat_disposable"}, echo=False)
    print("        unregister ->", json.dumps(u))
    ops = [o["name"] for o in s.call("list_operations", {}, echo=False)["operations"]]
    check("unregister: recipe is GONE from list_operations", "uat_disposable" not in ops)
    r = s.call("run_operation", {"operation": "uat_disposable",
                                 "documentName": "UAT Blocker rev A.SLDPRT"}, echo=False)
    check("unregister: calling it now errors", r.get("success") is False or "error" in r,
          f"-> {json.dumps(r)[:200]}")
    u2 = s.call("unregister_operation", {"operation": "extrude_boss"}, echo=False)
    check("unregister: a SEED recipe is refused", "error" in u2, f"-> {json.dumps(u2)[:250]}")
    ops = [o["name"] for o in s.call("list_operations", {}, echo=False)["operations"]]
    check("unregister: extrude_boss survived the attempt", "extrude_boss" in ops)
    u3 = s.call("unregister_operation", {"operation": "never_existed"}, echo=False)
    check("unregister: unknown name gives a clear error", "error" in u3, f"-> {json.dumps(u3)[:200]}")

    # ================= ambiguous documentName =================
    print("\n=== ambiguous documentName ===")
    doc2 = s.op("new_part")["return"]["title"]
    collide = TMP / f"{doc}.SLDPRT"          # filename stem == doc A's title
    r = s.call("run_operation", {"operation": "save_as", "args": {"path": str(collide)},
                                 "documentName": doc2}, echo=False)
    print(f"        saved scratch B as '{collide.name}' -> success={r.get('success')}")
    openn = [d["title"] for d in s.call("list_open_documents", {}, echo=False)["documents"]]
    print("        open:", openn)
    amb = s.call("get_part_info", {"documentName": doc}, echo=False)
    check("ambiguous name errors instead of silently picking one",
          "error" in amb and "ambiguous" in amb["error"].lower(),
          f"-> {json.dumps(amb)[:300]}")
    amb2 = s.call("run_operation", {"operation": "rebuild", "documentName": doc}, echo=False)
    check("ambiguous name on a WRITE errors too",
          amb2.get("success") is False and "ambiguous" in (amb2.get("error") or "").lower(),
          f"-> {(amb2.get('error') or '')[:260]}")
    check("the unambiguous full path still resolves",
          "mass" in s.call("get_part_info", {"documentName": str(collide)}, echo=False))

    print(f"\n### CLEANUP: docs {doc} (saved as '{good.name}') and {doc2} (saved as '{collide.name}')")

print("\n===== SUMMARY =====")
for n, ok in results:
    print(f"  [{'PASS' if ok else 'FAIL'}] {n}")
print(f"  {sum(1 for _, o in results if o)}/{len(results)} passed")
