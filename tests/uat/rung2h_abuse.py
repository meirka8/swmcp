"""The things a busy engineer actually does: wrong names, missing args, bare
numbers that mean millimetres, batches that die halfway, four docs open.

Nothing here is allowed to touch Part1 or Part2.SLDPRT.
"""
import json
from uat_client import Session


def show(label, obj):
    print(f"\n--- {label}")
    print(json.dumps(obj, indent=2)[:1200])


with Session(quiet=True) as s:
    docs = s.call("list_open_documents", {}, echo=False)["documents"]
    print("open:", [d["title"] for d in docs])
    before = {}
    for d in docs:
        i = s.call("get_part_info", {"documentName": d["title"]}, echo=False)
        before[d["title"]] = (i.get("mass"), len(i.get("features", [])), i.get("error"))
    print("snapshot:", json.dumps(before, indent=1))

    # ---- error message quality -------------------------------------------
    show("unknown operation", s.call("run_operation", {"operation": "make_bracket"}, echo=False))
    show("document-scoped op with NO documentName",
         s.call("run_operation", {"operation": "rebuild"}, echo=False))
    show("documentName that does not exist",
         s.call("run_operation", {"operation": "rebuild", "documentName": "Bracket rev C"}, echo=False))
    show("ambiguous-ish documentName 'Part2'",
         s.call("run_operation", {"operation": "rebuild", "documentName": "Part2"}, echo=False))
    show("prefix documentName 'Part'",
         s.call("run_operation", {"operation": "rebuild", "documentName": "Part"}, echo=False))
    show("get_part_info with no documentName, 4 docs open",
         s.call("get_part_info", {}, echo=False))
    show("describe_operation on a typo",
         s.call("describe_operation", {"operation": "extrude"}, echo=False))

    # ---- the units trap ---------------------------------------------------
    doc = s.op("new_part")["return"]["title"]
    print("\n### scratch doc:", doc)
    s.op("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc, echo=False)
    s.op("insert_sketch", {}, doc, echo=False)
    s.op("create_circle_by_radius", {"centerX": 0, "centerY": 0, "radius": "10 mm"}, doc, echo=False)
    s.op("exit_sketch", {}, doc, echo=False)
    s.op("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc, echo=False)
    show("extrude_boss with a MISSING required param",
         s.call("run_operation", {"operation": "extrude_boss", "args": {},
                                  "documentName": doc}, echo=False))
    show("extrude_boss depth1 = 40 (bare number - engineer meant 40 mm)",
         s.call("run_operation", {"operation": "extrude_boss", "args": {"depth1": 40},
                                  "documentName": doc}, echo=False))
    i = s.call("get_part_info", {"documentName": doc}, echo=False)
    b = i.get("boundingBox")
    if b:
        print("resulting bbox (mm):",
              [(b["max"][k] - b["min"][k]) * 1000 for k in "xyz"], " mass kg:", i.get("mass"))
    show("extrude_boss with a bad unit string",
         s.call("run_operation", {"operation": "extrude_boss", "args": {"depth1": "40 millimeters"},
                                  "documentName": doc}, echo=False))
    show("extrude_boss with a param name that does not exist",
         s.call("run_operation", {"operation": "extrude_boss",
                                  "args": {"depth1": "5 mm", "thickness": "2 mm"},
                                  "documentName": doc}, echo=False))
    print(f"### DOC TITLE FOR CLEANUP: {doc}")

    # ---- batch that dies halfway -----------------------------------------
    doc2 = s.op("new_part")["return"]["title"]
    print("\n### scratch doc 2:", doc2)
    show("run_operations batch, step 4 selects a plane that does not exist", s.call("run_operations", {
        "documentName": doc2,
        "steps": [
            {"operation": "select_by_id", "args": {"name": "Front Plane", "type": "PLANE"}},
            {"operation": "insert_sketch"},
            {"operation": "create_circle_by_radius", "args": {"radius": "10 mm"}},
            {"operation": "select_by_id", "args": {"name": "Side Plane", "type": "PLANE"}},
            {"operation": "exit_sketch"},
        ]}, echo=False))
    show("state after the failed batch (is the sketch still open?)",
         s.call("run_operation", {"operation": "rebuild", "documentName": doc2}, echo=False))
    print(f"### DOC TITLE FOR CLEANUP: {doc2}")

    # ---- did anything touch the documents I did not name? -----------------
    print("\n--- untouched check ---")
    for t, v in before.items():
        i = s.call("get_part_info", {"documentName": t}, echo=False)
        now = (i.get("mass"), len(i.get("features", [])), i.get("error"))
        print(f"  {t}: before={v} after={now} {'OK' if v == now else '*** CHANGED ***'}")
