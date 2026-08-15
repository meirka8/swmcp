"""Manual smoke test for the swmcp STDIO server.

Spawns the built server exe and drives it over newline-delimited JSON-RPC
(the framing the ModelContextProtocol STDIO transport actually uses — not
LSP Content-Length headers). Requires SolidWorks to be running.

Usage:
    python tests/test_client.py [documentName]
"""
import json
import subprocess
import sys
from pathlib import Path

SERVER = Path(__file__).parent.parent / "src/server/bin/Debug/net8.0-windows/server.exe"


def main() -> int:
    document_name = sys.argv[1] if len(sys.argv) > 1 else "Part2"

    proc = subprocess.Popen(
        [str(SERVER)],
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL,
        text=True,
        encoding="utf-8",
    )

    next_id = 0

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

    def call_tool(name: str, arguments: dict) -> None:
        response = request("tools/call", {"name": name, "arguments": arguments})
        print(f"\n=== {name} {arguments} ===")
        if "error" in response:
            print("RPC ERROR:", json.dumps(response["error"]))
            return
        result = response.get("result", response)
        if result.get("isError"):
            print("TOOL ERROR:")
        for block in result.get("content", []):
            if block.get("type") == "text":
                try:
                    print(json.dumps(json.loads(block["text"]), indent=2))
                except json.JSONDecodeError:
                    print(block["text"])

    try:
        init = request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "swmcp-smoke", "version": "0.0.1"},
        })
        print("initialize ->", json.dumps(init.get("result", {}).get("serverInfo")))
        notify("notifications/initialized")

        tools = request("tools/list", {})
        names = [t["name"] for t in tools["result"]["tools"]]
        print("tools/list ->", names)

        call_tool("list_open_documents", {})
        call_tool("get_part_info", {"documentName": document_name})
        call_tool("register_feature_schema", {
            "featureType": "SmokeTestType",
            "properties": [
                {"name": "Depth", "member": "GetDepth", "args": [True]},
                {"name": "Bare"},
            ],
        })
        return 0
    finally:
        proc.stdin.close()
        proc.wait(timeout=10)


if __name__ == "__main__":
    sys.exit(main())
