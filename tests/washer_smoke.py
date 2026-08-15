"""End-to-end live smoke test for the generic operation surface (ADR 0001):
draws a washer (new part -> sketch two concentric circles -> extrude) purely
through the six generic operation tools, then asserts the result via
get_part_info.

Same newline-delimited JSON-RPC framing as tests/test_client.py. Requires
SolidWorks to be running. Creates ONE new scratch part document; does not
touch any other open document.

Usage:
    python tests/washer_smoke.py
"""
import json
import subprocess
import sys
from pathlib import Path

SERVER = Path(__file__).parent.parent / "src/server/bin/Debug/net8.0-windows/server.exe"

OUTER_RADIUS_MM = 20
INNER_RADIUS_MM = 10
EXTRUDE_DEPTH_MM = 3
EXPECTED_XY_MM = 2 * OUTER_RADIUS_MM  # 40mm bounding box in X and Y
TOLERANCE_MM = 1


class ToolError(RuntimeError):
    pass


def main() -> int:
    proc = subprocess.Popen(
        [str(SERVER)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        encoding="utf-8",
    )

    next_id = 0
    failures: list[str] = []

    def request(method: str, params: dict | None = None) -> dict:
        nonlocal next_id
        next_id += 1
        msg = {"jsonrpc": "2.0", "id": next_id, "method": method}
        if params is not None:
            msg["params"] = params
        proc.stdin.write(json.dumps(msg) + "\n")
        proc.stdin.flush()
        while True:
            line = proc.stdout.readline()
            if not line:
                raise RuntimeError("server closed stdout")
            response = json.loads(line)
            if response.get("id") == next_id:
                return response

    def notify(method: str) -> None:
        proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": method}) + "\n")
        proc.stdin.flush()

    def call_tool(name: str, arguments: dict) -> dict:
        response = request("tools/call", {"name": name, "arguments": arguments})
        print(f"\n=== {name} {arguments} ===")
        if "error" in response:
            print("RPC ERROR:", json.dumps(response["error"]))
            raise ToolError(f"{name}: RPC error {response['error']}")
        result = response.get("result", response)
        text = None
        for block in result.get("content", []):
            if block.get("type") == "text":
                text = block["text"]
        if text is None:
            print(result)
            raise ToolError(f"{name}: no text content block in response")
        try:
            parsed = json.loads(text)
        except json.JSONDecodeError:
            print(text)
            raise ToolError(f"{name}: response was not JSON")
        print(json.dumps(parsed, indent=2))
        if result.get("isError"):
            raise ToolError(f"{name}: tool reported isError: {parsed}")
        return parsed

    def run_operation(operation: str, args: dict | None = None, document_name: str | None = None) -> dict:
        params = {"operation": operation}
        if args is not None:
            params["args"] = args
        if document_name is not None:
            params["documentName"] = document_name
        parsed = call_tool("run_operation", params)
        if not parsed.get("success"):
            raise ToolError(f"run_operation '{operation}' failed: {parsed.get('error')}")
        return parsed

    def check(condition: bool, description: str) -> None:
        status = "PASS" if condition else "FAIL"
        print(f"[{status}] {description}")
        if not condition:
            failures.append(description)

    doc_title = None
    try:
        init = request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "swmcp-washer-smoke", "version": "0.0.1"},
        })
        print("initialize ->", json.dumps(init.get("result", {}).get("serverInfo")))
        notify("notifications/initialized")

        tools = request("tools/list", {})
        names = [t["name"] for t in tools["result"]["tools"]]
        print("tools/list ->", names)
        for expected_tool in ["list_operations", "describe_operation", "run_operation",
                               "run_operations", "register_operation", "describe_com_members"]:
            check(expected_tool in names, f"tool surface includes '{expected_tool}'")

        ops = call_tool("list_operations", {})
        op_names = [o["name"] for o in ops["operations"]]
        print("list_operations ->", op_names)
        for expected_op in ["new_part", "select_by_id", "clear_selection", "insert_sketch",
                             "exit_sketch", "create_circle_by_radius", "create_line",
                             "extrude_boss", "rebuild", "undo"]:
            check(expected_op in op_names, f"list_operations includes seed op '{expected_op}'")

        # --- 1. new part -----------------------------------------------------
        result = run_operation("new_part")
        doc_title = result["return"]["title"]
        print(f"created scratch document: '{doc_title}'")
        check(bool(doc_title), "new_part returned a document title")

        # --- 2. select Front Plane, start a sketch ----------------------------
        run_operation("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc_title)
        run_operation("insert_sketch", {}, doc_title)

        # --- 3. two concentric circles -> washer profile ----------------------
        run_operation("create_circle_by_radius",
                       {"centerX": 0, "centerY": 0, "radius": f"{OUTER_RADIUS_MM} mm"}, doc_title)
        run_operation("create_circle_by_radius",
                       {"centerX": 0, "centerY": 0, "radius": f"{INNER_RADIUS_MM} mm"}, doc_title)

        # --- 4. exit sketch, select it, extrude --------------------------------
        run_operation("exit_sketch", {}, doc_title)
        run_operation("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc_title)
        run_operation("extrude_boss", {"depth1": f"{EXTRUDE_DEPTH_MM} mm"}, doc_title)
        run_operation("rebuild", {}, doc_title)

        # --- 5. verify via get_part_info ---------------------------------------
        info = call_tool("get_part_info", {"documentName": doc_title})

        extrude_features = [f for f in info["features"] if f["typeName"] in ("Extrusion", "ICE")]
        check(len(extrude_features) == 1,
              f"exactly one Extrusion/ICE feature (found {len(extrude_features)}: "
              f"{[f['typeName'] for f in extrude_features]})")

        mass = info["mass"]
        check(mass > 0, f"mass > 0 (mass={mass})")

        bbox = info["boundingBox"]
        if bbox:
            x_mm = (bbox["max"]["x"] - bbox["min"]["x"]) * 1000
            y_mm = (bbox["max"]["y"] - bbox["min"]["y"]) * 1000
            z_mm = (bbox["max"]["z"] - bbox["min"]["z"]) * 1000
            print(f"bounding box (mm): x={x_mm:.3f} y={y_mm:.3f} z={z_mm:.3f}")
            check(abs(x_mm - EXPECTED_XY_MM) <= TOLERANCE_MM, f"bounding box X ~{EXPECTED_XY_MM}mm (got {x_mm:.3f}mm)")
            check(abs(y_mm - EXPECTED_XY_MM) <= TOLERANCE_MM, f"bounding box Y ~{EXPECTED_XY_MM}mm (got {y_mm:.3f}mm)")
            check(abs(z_mm - EXTRUDE_DEPTH_MM) <= TOLERANCE_MM, f"bounding box Z ~{EXTRUDE_DEPTH_MM}mm (got {z_mm:.3f}mm)")
        else:
            check(False, "boundingBox present")

        print("\n=== FINAL PART INFO ===")
        print(json.dumps(info, indent=2))

    finally:
        proc.stdin.close()
        proc.wait(timeout=15)

    print(f"\n=== RESULT: {len(failures)} failure(s) ===")
    for f in failures:
        print(f" - {f}")

    if doc_title:
        print(f"\nScratch document left for cleanup: '{doc_title}'")

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
