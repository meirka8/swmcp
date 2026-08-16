"""Re-verdict baseline: tool surface, seed contents, open documents."""
import json
from uat_client import Session

with Session(quiet=True) as s:
    print("TOOLS:", sorted(t["name"] for t in s.request("tools/list", {})["result"]["tools"]))
    docs = s.call("list_open_documents", {}, echo=False)["documents"]
    print("OPEN:", [d["title"] for d in docs])
    ops = s.call("list_operations", {}, echo=False)["operations"]
    print(f"OPERATIONS ({len(ops)}):")
    for o in sorted(ops, key=lambda x: x["name"]):
        print(f"  {o['source']:10} {o['scope']:11} {o['name']}")
    print("\nselect_by_id summary now reads:")
    print(" ", s.call("describe_operation", {"operation": "select_by_id"}, echo=False)["summary"])
