"""RUNG 2 step A - enrichment discovery: what does FeatureManager actually expose?

This is the 'core product loop' as documented: describe_com_members against the
target, find the real member + signature, then register_operation.
"""
import json
import sys
from uat_client import Session

doc = sys.argv[1] if len(sys.argv) > 1 else None
want = ["FeatureCut", "FeatureFillet", "Chamfer", "Revolve", "Rectangle", "CenterLine", "Material"]

with Session(quiet=True) as s:
    for target in ["FeatureManager", "SketchManager", ""]:
        args = {"targetPath": target}
        if doc:
            args["documentName"] = doc
        r = s.call("describe_com_members", args, echo=False)
        print(f"\n===== target='{target or '(root)'}' via {r.get('discoveredVia')} "
              f"count={r.get('memberCount')} truncated={r.get('truncated')} =====")
        if "error" in r:
            print("ERROR:", r["error"])
            continue
        for m in r.get("members", []):
            if any(w.lower() in m["name"].lower() for w in want):
                print(f"  {m['kind']:12} {m['name']:32} params={m['paramCount']:3} -> {m['returnType']}")
