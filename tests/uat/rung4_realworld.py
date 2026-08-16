"""The mundane real-world stuff: save to a filename with a space and a revision
suffix, then keep driving the document under its new name. Plus: what does an
interrupted server leave behind?"""
import json
import subprocess
import sys
import time
from pathlib import Path
from uat_client import Session, SERVER

OUT = Path(r"C:\projects\aibuilds\models\UAT Bracket rev B.SLDPRT")

with Session(quiet=True) as s:
    print("--- looking for a save member the recipe vocabulary can actually call ---")
    for target in ["", "Extension"]:
        r = s.call("describe_com_members", {"documentName": "Part1", "targetPath": target}, echo=False)
        names = [(m["name"], m["paramCount"], m["returnType"]) for m in r.get("members", [])
                 if "save" in m["name"].lower()]
        print(f"  target='{target or '(root)'}' truncated={r.get('truncated')} "
              f"({r.get('memberCount')} members): {names}")

    SAVE = {
        "name": "save_as",
        "summary": "Saves the document to an absolute path (IModelDoc2.SaveAs3). version 0 = current SolidWorks version, options 1 = silent (swSaveAsOptions_Silent).",
        "scope": "document", "target": "", "kind": "method", "member": "SaveAs3",
        "requires": [],
        "params": [
            {"name": "path", "type": "string", "required": True},
            {"name": "version", "type": "int", "default": 0},
            {"name": "options", "type": "int", "default": 1, "enum": "swSaveAsOptions_e"},
        ],
        "returns": {"type": "bool"},
        "verify": [{"check": "returnTrue"}],
    }
    print("\nregister save_as ->", json.dumps(s.call("register_operation", {"recipe": SAVE}, echo=False)))

    doc = sys.argv[1] if len(sys.argv) > 1 else None
    if not doc:
        print("pass the rung3 document title as argv[1]")
        sys.exit(1)

    r = s.op("save_as", {"path": str(OUT)}, doc, must_succeed=False, echo=False)
    print(f"save_as -> success={r.get('success')} err={r.get('error')}")
    print("file on disk:", OUT.exists(), OUT.stat().st_size if OUT.exists() else "")
    print("open docs:", [d["title"] for d in s.call("list_open_documents", {}, echo=False)["documents"]])

    for name in ["UAT Bracket rev B.SLDPRT", "UAT Bracket rev B", "uat bracket rev b.sldprt",
                 str(OUT), "UAT Bracket"]:
        i = s.call("get_part_info", {"documentName": name}, echo=False)
        print(f"  resolve '{name}': " + (f"mass={i['mass']*1000:.3f} g" if "mass" in i else i.get("error", "?")[:110]))

    # keep driving it under the new name
    r = s.op("rebuild", {}, "UAT Bracket rev B.SLDPRT", must_succeed=False, echo=False)
    print("rebuild by spacey name ->", r.get("success"), r.get("error"))

print("\n--- INTERRUPTION: kill the server while a sketch is open ---")
proc = subprocess.Popen([str(SERVER)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                        stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
i = 0


def rq(method, params=None):
    global i
    i += 1
    m = {"jsonrpc": "2.0", "id": i, "method": method}
    if params:
        m["params"] = params
    proc.stdin.write(json.dumps(m) + "\n")
    proc.stdin.flush()
    while True:
        line = proc.stdout.readline()
        if not line:
            return None
        r = json.loads(line)
        if r.get("id") == i:
            return r


rq("initialize", {"protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": {"name": "x", "version": "1"}})
proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": "notifications/initialized"}) + "\n")
proc.stdin.flush()
rq("tools/call", {"name": "run_operation", "arguments": {"operation": "select_by_id",
   "args": {"name": "Front Plane", "type": "PLANE"}, "documentName": "UAT Bracket rev B.SLDPRT"}})
rq("tools/call", {"name": "run_operation", "arguments": {"operation": "insert_sketch",
   "args": {}, "documentName": "UAT Bracket rev B.SLDPRT"}})
print("sketch opened; killing the server process now (simulating a client crash / ctrl-C)")
proc.kill()
proc.wait(timeout=10)
time.sleep(1)

print("\n--- reconnect with a fresh server and see what state SolidWorks is in ---")
with Session(quiet=True) as s2:
    docs = s2.call("list_open_documents", {}, echo=False)
    print("open docs:", [d["title"] for d in docs["documents"]])
    r = s2.call("run_operation", {"operation": "rebuild", "documentName": "UAT Bracket rev B.SLDPRT"}, echo=False)
    print("rebuild after kill ->", json.dumps(r.get("documentState")), "success:", r.get("success"))
    r = s2.call("run_operation", {"operation": "exit_sketch", "documentName": "UAT Bracket rev B.SLDPRT"}, echo=False)
    print("exit_sketch after kill ->", r.get("success"), (r.get("error") or "")[:180])
    i2 = s2.call("get_part_info", {"documentName": "UAT Bracket rev B.SLDPRT"}, echo=False)
    print("mass still:", i2.get("mass"), i2.get("error"))
