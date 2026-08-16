"""Two things:
1. Ambiguous documentName - done properly this time (leave doc A unsaved so its
   title survives, then save doc B to <tmp>\\<titleOfA>.SLDPRT).
2. describe_com_members on the document ROOT ("") - register_operation warned that
   EditRebuild3 "was not found", yet EditRebuild3 is the seed's own 'rebuild'.
"""
import json
import os
from pathlib import Path
from uat_client import Session

TMP = Path(os.environ["TEMP"]) / "swmcp_uat"
TMP.mkdir(parents=True, exist_ok=True)
res = []


def check(n, c, d=""):
    res.append((n, bool(c)))
    print(f"[{'PASS' if c else 'FAIL'}] {n}" + (f"\n        {d}" if d else ""))


def block(s, doc, size="20 mm"):
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_corner_rectangle", {"x1": "-10 mm", "y1": "-10 mm",
                                     "x2": "10 mm", "y2": "10 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    s.op("extrude_boss", {"depth1": size}, doc, echo=False)


with Session(quiet=True) as s:
    print("=== root-object discovery: can the tool see the seed's own root members? ===")
    r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": ""}, echo=False)
    print(f"  root: discoveredVia={r.get('discoveredVia')} totalCount={r.get('totalCount')}")
    for probe in ["EditRebuild3", "SaveAs3", "EditUndo2", "ClearSelection2",
                  "SetMaterialPropertyName2"]:
        f = s.call("describe_com_members", {"documentName": "Part1", "targetPath": "",
                                            "nameFilter": probe}, echo=False)
        found = probe in [m["name"] for m in f.get("members", [])]
        which = [o["name"] for o in s.call("list_operations", {}, echo=False)["operations"]]
        print(f"  nameFilter='{probe}': {'FOUND' if found else 'NOT FOUND'} "
              f"(totalCount={f.get('totalCount')})")
    check("root discovery finds EditRebuild3 (used by seed op 'rebuild')",
          "EditRebuild3" in [m["name"] for m in s.call(
              "describe_com_members", {"documentName": "Part1", "targetPath": "",
                                       "nameFilter": "EditRebuild"}, echo=False).get("members", [])])
    check("root discovery finds SaveAs3 (used by seed op 'save_as')",
          "SaveAs3" in [m["name"] for m in s.call(
              "describe_com_members", {"documentName": "Part1", "targetPath": "",
                                       "nameFilter": "SaveAs"}, echo=False).get("members", [])])

    print("\n=== ambiguous documentName, properly constructed ===")
    docA = s.op("new_part")["return"]["title"]     # stays unsaved -> title stays 'PartNN'
    docB = s.op("new_part")["return"]["title"]
    print(f"  doc A (unsaved, title '{docA}'), doc B '{docB}'")
    block(s, docB)
    collide = TMP / f"{docA}.SLDPRT"
    r = s.call("run_operation", {"operation": "save_as", "args": {"path": str(collide)},
                                 "documentName": docB}, echo=False)
    print(f"  saved doc B to '{collide.name}' -> success={r.get('success')}")
    openn = [d["title"] for d in s.call("list_open_documents", {}, echo=False)["documents"]]
    print("  open now:", openn)

    amb = s.call("get_part_info", {"documentName": docA}, echo=False)
    check("READ with an ambiguous name errors instead of picking one",
          "error" in amb and "ambiguous" in amb["error"].lower(), f"-> {json.dumps(amb)[:320]}")
    ambw = s.call("run_operation", {"operation": "rebuild", "documentName": docA}, echo=False)
    check("WRITE with an ambiguous name errors instead of picking one",
          ambw.get("success") is False and "ambiguous" in (ambw.get("error") or "").lower(),
          f"-> {(ambw.get('error') or json.dumps(ambw))[:320]}")
    check("the error lists the candidates so you can disambiguate",
          "error" in amb and str(collide) in amb["error"],
          f"full path present in message: {'error' in amb and str(collide) in amb['error']}")
    full = s.call("get_part_info", {"documentName": str(collide)}, echo=False)
    check("the unambiguous FULL PATH still resolves", "mass" in full,
          f"-> mass={full.get('mass')} err={full.get('error')}")
    exact = s.call("get_part_info", {"documentName": f"{docA}.SLDPRT"}, echo=False)
    check("the unambiguous exact title still resolves", "mass" in exact,
          f"-> mass={exact.get('mass')} err={exact.get('error')}")
    print(f"\n### CLEANUP: {docA}, and doc B now titled '{collide.name}'")

print("\n===== SUMMARY =====")
for n, ok in res:
    print(f"  [{'PASS' if ok else 'FAIL'}] {n}")
print(f"  {sum(1 for _, o in res if o)}/{len(res)} passed")
