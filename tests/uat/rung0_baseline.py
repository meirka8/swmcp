"""Baseline: what is open before I touch anything, and what does the tool surface look like."""
import json
from uat_client import Session

with Session() as s:
    tools = s.request("tools/list", {})
    print("TOOLS:", [t["name"] for t in tools["result"]["tools"]])
    docs = s.call("list_open_documents", {})
    ops = s.call("list_operations", {})
    print("OPS:", [o["name"] for o in ops["operations"]])
