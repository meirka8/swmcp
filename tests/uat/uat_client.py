"""Minimal MCP stdio driver for UAT of swmcp.

Same newline-delimited JSON-RPC framing as tests/washer_smoke.py, but factored
so each UAT rung script is short. Requires SolidWorks running.
"""
import json
import subprocess
from pathlib import Path

SERVER = Path(__file__).resolve().parent.parent.parent / "src/server/bin/Debug/net8.0-windows/server.exe"


class ToolError(RuntimeError):
    pass


class Session:
    def __init__(self, quiet=False, capture_stderr=False):
        self.quiet = quiet
        self.proc = subprocess.Popen(
            [str(SERVER)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=(subprocess.PIPE if capture_stderr else subprocess.DEVNULL),
            text=True,
            encoding="utf-8",
        )
        self._id = 0
        self.init()

    def request(self, method, params=None):
        self._id += 1
        msg = {"jsonrpc": "2.0", "id": self._id, "method": method}
        if params is not None:
            msg["params"] = params
        self.proc.stdin.write(json.dumps(msg) + "\n")
        self.proc.stdin.flush()
        while True:
            line = self.proc.stdout.readline()
            if not line:
                raise RuntimeError("server closed stdout")
            resp = json.loads(line)
            if resp.get("id") == self._id:
                return resp

    def notify(self, method):
        self.proc.stdin.write(json.dumps({"jsonrpc": "2.0", "method": method}) + "\n")
        self.proc.stdin.flush()

    def init(self):
        r = self.request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "swmcp-uat", "version": "1"},
        })
        self.notify("notifications/initialized")
        return r

    def call(self, name, arguments, echo=None):
        resp = self.request("tools/call", {"name": name, "arguments": arguments})
        if echo is None:
            echo = not self.quiet
        if "error" in resp:
            raise ToolError(f"{name}: RPC error {resp['error']}")
        result = resp.get("result", resp)
        text = None
        for block in result.get("content", []):
            if block.get("type") == "text":
                text = block["text"]
        if text is None:
            raise ToolError(f"{name}: no text content: {result}")
        try:
            parsed = json.loads(text)
        except json.JSONDecodeError:
            parsed = {"_raw": text}
        if echo:
            print(f"\n=== {name} {json.dumps(arguments)[:200]} ===")
            print(json.dumps(parsed, indent=2)[:4000])
        return parsed

    # convenience
    def op(self, operation, args=None, doc=None, echo=None, must_succeed=True):
        params = {"operation": operation}
        if args is not None:
            params["args"] = args
        if doc is not None:
            params["documentName"] = doc
        parsed = self.call("run_operation", params, echo=echo)
        if must_succeed and not parsed.get("success"):
            raise ToolError(f"run_operation '{operation}' FAILED: {parsed.get('error')}")
        return parsed

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=15)
        except Exception:
            self.proc.kill()

    def __enter__(self):
        return self

    def __exit__(self, *a):
        self.close()


def bbox_mm(info):
    b = info.get("boundingBox")
    if not b:
        return None
    return tuple(round((b["max"][k] - b["min"][k]) * 1000, 4) for k in ("x", "y", "z"))
