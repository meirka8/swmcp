"""End-to-end live smoke test for the seed recipes promoted from the UAT
(docs/uat-ladder-report.md): a plate with a through hole and a filleted
corner, material assigned, saved to disk — driven entirely through SEED
recipes (create_corner_rectangle, cut_extrude, select_by_ray,
fillet_constant_radius, set_material, save_as). Zero register_operation
calls, by design: this proves the promoted recipes work out of the box.

Same newline-delimited JSON-RPC framing as tests/test_client.py. Requires
SolidWorks to be running. Creates ONE new scratch part document and saves it
to a temp file; both are cleaned up at the end regardless of outcome. Does
not touch any other open document.

Usage:
    python tests/bracket_smoke.py
"""
import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).parent.parent
SERVER = REPO_ROOT / "src/server/bin/Debug/net8.0-windows/server.exe"
SEED_VERIFIER = REPO_ROOT / "tests/SeedVerifier"

PLATE_X_MM = 80
PLATE_Y_MM = 40
PLATE_THICKNESS_MM = 6
HOLE_RADIUS_MM = 2.5
HOLE_X_MM = 25  # hole center offset from plate center
FILLET_RADIUS_MM = 6
SAVE_PATH = Path(tempfile.gettempdir()) / "swmcp_bracket_smoke.SLDPRT"


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
        print(json.dumps(parsed, indent=2)[:4000])
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
        assert "boundArgs" in parsed, f"run_operation '{operation}' response missing 'boundArgs'"
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
            "clientInfo": {"name": "swmcp-bracket-smoke", "version": "0.0.1"},
        })
        print("initialize ->", json.dumps(init.get("result", {}).get("serverInfo")))
        notify("notifications/initialized")

        ops = call_tool("list_operations", {})
        op_names = [o["name"] for o in ops["operations"]]
        for expected_op in ["create_corner_rectangle", "cut_extrude", "select_by_ray",
                             "fillet_constant_radius", "set_material", "save_as", "unregister_operation"]:
            check(expected_op in op_names or expected_op == "unregister_operation",
                  f"list_operations includes seed op '{expected_op}'" if expected_op != "unregister_operation"
                  else "note: unregister_operation is checked via tools/list, not list_operations")

        tools = request("tools/list", {})
        tool_names = [t["name"] for t in tools["result"]["tools"]]
        check("unregister_operation" in tool_names, "tool surface includes 'unregister_operation'")

        # --- 1. new part -------------------------------------------------------
        result = run_operation("new_part")
        doc_title = result["return"]["title"]
        print(f"created scratch document: '{doc_title}'")

        # --- 2. plate profile via create_corner_rectangle (not four create_line) ---
        run_operation("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc_title)
        run_operation("insert_sketch", {}, doc_title)
        half_x, half_y = PLATE_X_MM / 2, PLATE_Y_MM / 2
        run_operation("create_corner_rectangle", {
            "x1": f"-{half_x} mm", "y1": f"-{half_y} mm",
            "x2": f"{half_x} mm", "y2": f"{half_y} mm",
        }, doc_title)
        run_operation("exit_sketch", {}, doc_title)
        run_operation("select_by_id", {"name": "Sketch1", "type": "SKETCH", "mark": 0}, doc_title)
        run_operation("extrude_boss", {"depth1": f"{PLATE_THICKNESS_MM} mm"}, doc_title)
        run_operation("rebuild", {}, doc_title)

        info = call_tool("get_part_info", {"documentName": doc_title})
        plate_feature_count = len(info["features"])
        bbox = info["boundingBox"]
        x_mm = (bbox["max"]["x"] - bbox["min"]["x"]) * 1000
        y_mm = (bbox["max"]["y"] - bbox["min"]["y"]) * 1000
        z_mm = (bbox["max"]["z"] - bbox["min"]["z"]) * 1000
        check(abs(x_mm - PLATE_X_MM) < 0.5, f"plate bbox X ~{PLATE_X_MM}mm (got {x_mm:.3f}mm)")
        check(abs(y_mm - PLATE_Y_MM) < 0.5, f"plate bbox Y ~{PLATE_Y_MM}mm (got {y_mm:.3f}mm)")
        check(abs(z_mm - PLATE_THICKNESS_MM) < 0.5, f"plate bbox Z ~{PLATE_THICKNESS_MM}mm (got {z_mm:.3f}mm)")

        # --- 3. through hole via cut_extrude -------------------------------------
        run_operation("select_by_id", {"name": "Front Plane", "type": "PLANE"}, doc_title)
        run_operation("insert_sketch", {}, doc_title)
        run_operation("create_circle_by_radius", {
            "centerX": f"{HOLE_X_MM} mm", "radius": f"{HOLE_RADIUS_MM} mm",
        }, doc_title)
        run_operation("exit_sketch", {}, doc_title)
        run_operation("select_by_id", {"name": "Sketch2", "type": "SKETCH", "mark": 0}, doc_title)
        # The cut sketch plane (Front Plane, Z=0) is coincident with the plate's
        # own bottom face — the documented seed-verification.md §4.7 quirk (also
        # hit live during the UAT, docs/uat-ladder-report.md rung 2): a blind cut
        # from a sketch plane coincident with a solid face needs reverseDirection
        # true, or it silently cuts away from the material and returns nothing.
        run_operation("cut_extrude", {
            "depth1": f"{PLATE_THICKNESS_MM + 4} mm",  # deliberately more than plate thickness
            "reverseDirection": True,
        }, doc_title)
        run_operation("rebuild", {}, doc_title)

        info = call_tool("get_part_info", {"documentName": doc_title})
        # +2, not +1: insert_sketch's Sketch2 and cut_extrude's Cut-Extrude1 are
        # each their own feature-tree entry.
        after_hole_count = plate_feature_count + 2
        check(len(info["features"]) == after_hole_count,
              f"feature count increased by exactly 2 (sketch + cut) after the hole "
              f"({plate_feature_count} -> {len(info['features'])})")

        # --- 4. pick the vertical edge at the (+X,+Y) corner via select_by_ray ---
        # A straight-down ray (the recipe's default direction) whose origin sits
        # directly above the corner column is colinear with that column's
        # vertical edge — a robust pick, unlike select_by_id's coordinate hint
        # (UAT B3: view-dependent, silently wrong after the topology change we
        # just made with the hole cut above).
        run_operation("clear_selection", {}, doc_title)
        ray = run_operation("select_by_ray", {
            "x": f"{half_x} mm", "y": f"{half_y} mm", "z": f"{PLATE_THICKNESS_MM + 5} mm",
        }, doc_title)
        check(ray["documentState"]["selectionCount"] == 1, "select_by_ray selected exactly one entity")

        # UAT re-verdict gap #1 ("the last silent-wrongness path"): the
        # response's documentState.selectedEntities must name WHAT was
        # selected, not just how many — enough to confirm (without a
        # hand-computed mass check) that the ray hit the intended 6mm edge
        # and not some neighbor.
        selected = ray["documentState"].get("selectedEntities")
        check(selected is not None and len(selected) == 1,
              f"select_by_ray's documentState.selectedEntities has exactly one entry (got {selected})")
        if selected:
            entity = selected[0]
            check(entity.get("type") == "swSelEDGES", f"selected entity type is swSelEDGES (got {entity.get('type')})")
            descriptor = entity.get("descriptor") or ""
            check("edge" in descriptor.lower() and "length=" in descriptor,
                  f"selected entity descriptor plausibly identifies an edge (got '{descriptor}')")
            # The plate is 6mm thick, so the vertical corner edge is 6mm long —
            # confirms this is genuinely the intended edge, not a 40/80mm neighbor
            # (a mis-aimed ray, per the seed's own aiming guidance, silently
            # picks one of those instead with no error).
            check("length=0.006" in descriptor, f"descriptor's length matches the 6mm vertical edge (got '{descriptor}')")

        # UAT re-verdict gap #5: a read-only state check mid-plan, with no
        # side effects — confirms selection/sketch state without writing.
        state = call_tool("get_document_state", {"documentName": doc_title})
        check(state.get("inSketchMode") is False, "get_document_state: not in sketch mode mid-flow")
        check(state.get("selectionCount") == 1, "get_document_state: selectionCount matches the ray pick")
        check(state.get("selectedEntities") == selected,
              "get_document_state's selectedEntities matches what select_by_ray's own documentState reported")
        check("needsRebuild" in state, "get_document_state: needsRebuild present")

        # --- 5. fillet that edge --------------------------------------------------
        run_operation("fillet_constant_radius", {"radius": f"{FILLET_RADIUS_MM} mm"}, doc_title)
        run_operation("rebuild", {}, doc_title)

        info = call_tool("get_part_info", {"documentName": doc_title})
        # +1 more: fillet_constant_radius needs a selection, not a sketch, so
        # only Fillet1 itself is a new feature-tree entry.
        check(len(info["features"]) == after_hole_count + 1,
              f"feature count increased by exactly 1 more (fillet, no new sketch) "
              f"({after_hole_count} -> {len(info['features'])})")
        fillet_features = [f for f in info["features"] if f["typeName"] == "Fillet"]
        check(len(fillet_features) == 1, f"exactly one Fillet-type feature landed (found {len(fillet_features)})")
        if fillet_features:
            radius_value = fillet_features[0].get("data", {}).get("DefaultRadius")
            check(radius_value is not None and abs(radius_value - FILLET_RADIUS_MM / 1000) < 1e-6,
                  f"fillet DefaultRadius ~{FILLET_RADIUS_MM}mm (got {radius_value})")

        # UAT re-verdict gap #4: default feature list is folder-free; the same
        # document with includeFolderFeatures:true shows (many) more entries.
        default_feature_count = len(info["features"])
        folder_names = {"CommentsFolder", "FavoriteFolder", "HistoryFolder", "MaterialFolder", "NotesAreaFtrFolder"}
        check(not any(f["typeName"] in folder_names for f in info["features"]),
              "get_part_info default response contains no folder-noise feature-tree entries")
        info_with_folders = call_tool("get_part_info", {"documentName": doc_title, "includeFolderFeatures": True})
        check(len(info_with_folders["features"]) > default_feature_count,
              f"includeFolderFeatures:true returns more entries ({len(info_with_folders['features'])} vs {default_feature_count})")
        check(any(f["typeName"] in folder_names for f in info_with_folders["features"]),
              "includeFolderFeatures:true response contains at least one folder-type entry")

        # --- 6. material ------------------------------------------------------------
        mass_before = info["mass"]
        check(info.get("material") is None, f"material is null before set_material (got {info.get('material')})")
        check(info.get("density") is not None and abs(info["density"] - 1000.0) < 1,
              f"density defaults to water's 1000 kg/m^3 before set_material (got {info.get('density')})")

        run_operation("set_material", {"name": "6061 Alloy", "database": "SOLIDWORKS Materials"}, doc_title)
        run_operation("rebuild", {}, doc_title)
        info = call_tool("get_part_info", {"documentName": doc_title})
        mass_after = info["mass"]
        check(mass_after != mass_before, f"mass changed after set_material ({mass_before} -> {mass_after})")
        # 6061 aluminum density is 2700 kg/m^3 (vs. the unassigned-part default
        # of 1000 kg/m^3 water) — the mass should have scaled up by ~2.7x.
        check(abs(mass_after / mass_before - 2.7) < 0.05,
              f"mass scaled by ~2.7x (6061 density / water density), got {mass_after / mass_before:.3f}x")
        # UAT re-verdict gap #4: get_part_info now confirms what set_material
        # actually applied, instead of requiring an independently hand-computed
        # mass to infer it.
        check(info.get("material") == "6061 Alloy", f"get_part_info reports material '6061 Alloy' (got {info.get('material')})")
        check(info.get("density") is not None and abs(info["density"] - 2700.0) < 1,
              f"get_part_info reports density 2700 kg/m^3 for 6061 (got {info.get('density')})")

        # --- 7. save_as, verified via returnEquals -------------------------------
        if SAVE_PATH.exists():
            SAVE_PATH.unlink()
        save_result = run_operation("save_as", {"path": str(SAVE_PATH)}, doc_title)
        check(save_result["return"] == 0, f"save_as returned 0 (success) via returnEquals, got {save_result['return']}")
        check(SAVE_PATH.exists(), f"save_as actually wrote a file to '{SAVE_PATH}'")
        if SAVE_PATH.exists():
            check(SAVE_PATH.stat().st_size > 0, "saved file is non-empty")
        # SaveAs3 renames the live document's identity to the new file — every
        # later lookup (including the cleanup close below) must use the new
        # title, not the pre-save scratch title ("Part36" or similar).
        doc_title = SAVE_PATH.name

        print("\n=== FINAL PART INFO ===")
        print(json.dumps(info, indent=2)[:4000])

    finally:
        proc.stdin.close()
        proc.wait(timeout=15)

        # Close the SolidWorks document BEFORE deleting the file on disk — while
        # SolidWorks still has it open (post save_as), the file is locked and
        # deletion fails with WinError 32.
        if doc_title:
            close = subprocess.run(
                ["dotnet", "run", "--project", str(SEED_VERIFIER), "--", "close", doc_title],
                cwd=str(SEED_VERIFIER), capture_output=True, text=True, timeout=60,
            )
            print(f"\nclosed scratch document '{doc_title}':")
            print(close.stdout.strip())
            if close.returncode != 0 or "not open" in close.stdout:
                print(close.stderr.strip())
                failures.append(f"could not close scratch document '{doc_title}' via SeedVerifier")

        if SAVE_PATH.exists():
            try:
                SAVE_PATH.unlink()
                print(f"\ndeleted scratch file '{SAVE_PATH}'")
            except OSError as ex:
                print(f"\nWARNING: could not delete scratch file '{SAVE_PATH}': {ex}")
                failures.append(f"could not delete scratch file '{SAVE_PATH}'")

    print(f"\n=== RESULT: {len(failures)} failure(s) ===")
    for f in failures:
        print(f" - {f}")

    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
