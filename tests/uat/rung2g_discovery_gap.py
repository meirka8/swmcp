"""describe_com_members did not list SelectByRay on Extension, yet the recipe I
registered against it works. Is the discovery list truncated, or blind?"""
import json
from uat_client import Session

with Session(quiet=True) as s:
    for target in ["Extension", "FeatureManager", "SketchManager"]:
        r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": target}, echo=False)
        names = [m["name"] for m in r.get("members", [])]
        print(f"{target}: via={r.get('discoveredVia')} count={r.get('memberCount')} "
              f"truncated={r.get('truncated')} returned={len(names)}")
        for probe in ["SelectByRay", "SelectByID2", "FeatureExtrusion3", "FeatureCut4",
                      "FeatureFillet3", "InsertFeatureChamfer", "CreateCornerRectangle",
                      "FeatureRevolve2", "SetMaterialPropertyName2"]:
            if probe in names:
                print(f"    LISTED   {probe}")
        for probe in ["SelectByRay", "SelectByID2", "FeatureExtrusion3", "FeatureCut4",
                      "FeatureFillet3", "InsertFeatureChamfer", "CreateCornerRectangle",
                      "FeatureRevolve2", "SetMaterialPropertyName2"]:
            if probe not in names:
                print(f"    missing  {probe}")
        if r.get("note"):
            print("    note:", r["note"])

    # what does register_operation say about a member that definitely does not exist?
    bogus = {
        "name": "uat_bogus_member", "summary": "does not exist", "scope": "document",
        "target": "Extension", "kind": "method", "member": "SelectByTelepathy",
        "requires": [], "params": [], "returns": {"type": "bool"},
        "verify": [{"check": "returnTrue"}],
    }
    print("\nregister bogus member ->",
          json.dumps(s.call("register_operation", {"recipe": bogus}, echo=False)))
    bogus2 = dict(bogus, name="uat_bogus_target", target="NoSuchManager", member="Whatever")
    print("register bogus target ->",
          json.dumps(s.call("register_operation", {"recipe": bogus2}, echo=False)))
    print("run bogus ->", json.dumps(s.call("run_operation",
          {"operation": "uat_bogus_member", "documentName": "Part1"}, echo=False)))
    print("run bogus target ->", json.dumps(s.call("run_operation",
          {"operation": "uat_bogus_target", "documentName": "Part1"}, echo=False)))
