"""Does an interrupted session leave a dangling sketch, and does the error tell
you how to get out of it? Also: is there any read-only way to see sketch state?"""
import json
import subprocess
import time
from uat_client import Session, SERVER

with Session(quiet=True) as s:
    doc = s.op("new_part")["return"]["title"]
    print("### scratch doc:", doc)

# open a sketch, then kill the client mid-flight
proc = subprocess.Popen([str(SERVER)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                        stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
n = 0


def rq(method, params=None):
    global n
    n += 1
    m = {"jsonrpc": "2.0", "id": n, "method": method}
    if params:
        m["params"] = params
    proc.stdin.write(json.dumps(m) + "\n")
    proc.stdin.flush()
    while True:
        line = proc.stdout.readline()
        if not line:
            return None
        r = json.loads(line)
        if r.get("id") == n:
            return r


rq("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "x", "version": "1"}})
proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
proc.stdin.flush()
rq("tools/call", {"name": "run_operation", "arguments": {"operation": "select_by_id",
   "args": {"name": "Front Plane", "type": "PLANE"}, "documentName": doc}})
rq("tools/call", {"name": "run_operation", "arguments": {"operation": "insert_sketch",
   "args": {}, "documentName": doc}})
rq("tools/call", {"name": "run_operation", "arguments": {"operation": "create_circle_by_radius",
   "args": {"radius": "10 mm"}, "documentName": doc}})
print("sketch open with a circle in it -> killing the server")
proc.kill()
proc.wait(timeout=10)
time.sleep(1)

with Session(quiet=True) as s:
    print("\n-- read-only tools: do they tell me a sketch is open? --")
    print("  get_part_info:", json.dumps(s.call("get_part_info", {"documentName": doc}, echo=False))[:200])
    print("  list_open_documents entry:",
          [d for d in s.call("list_open_documents", {}, echo=False)["documents"] if d["title"] == doc])
    print("\n-- the first thing a client would try next --")
    r = s.call("run_operation", {"operation": "select_by_id",
               "args": {"name": "Sketch1", "type": "SKETCH", "mark": 0}, "documentName": doc}, echo=False)
    print("  select sketch ->", r.get("success"), (r.get("error") or "")[:200], r.get("documentState"))
    r = s.call("run_operation", {"operation": "extrude_boss",
               "args": {"depth1": "5 mm"}, "documentName": doc}, echo=False)
    print("  extrude_boss ->", r.get("success"))
    print("  error:", r.get("error"))
    print("  documentState:", json.dumps(r.get("documentState")))
    r = s.call("run_operation", {"operation": "exit_sketch", "documentName": doc}, echo=False)
    print("  exit_sketch ->", r.get("success"), (r.get("error") or "")[:150])
    r = s.call("run_operation", {"operation": "extrude_boss",
               "args": {"depth1": "5 mm"}, "documentName": doc}, echo=False)
    print("  retry extrude_boss ->", r.get("success"), (r.get("error") or "")[:150])
    print(f"### DOC TITLE FOR CLEANUP: {doc}")
