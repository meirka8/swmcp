# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`swmcp` is an MCP (Model Context Protocol) server, written in C#/.NET 8, that exposes a **running local SolidWorks instance** to AI agents over STDIO. All SolidWorks COM access goes through the MIT-licensed [SwBridge](https://github.com/meirka8/swbridge) package — this repo contains only the MCP layer: tool definitions and the dynamic feature-schema registry. See the workspace-level `../CLAUDE.md` for the two-repo boundary rules.

Windows-only (`net8.0-windows`, COM interop, SolidWorks installed and running).

## Commands

Build (this also produces the exe the MCP client config points at):

```bash
dotnet build src/server/server.csproj
```

Run directly (STDIO server — it will sit waiting on stdin):

```bash
dotnet run --project src/server/server.csproj
```

Live smoke tests (require SolidWorks running; drive the built exe over newline-delimited JSON-RPC):

```bash
python tests/test_client.py Part2       # read path (list_open_documents, get_part_info, register_feature_schema)
python tests/washer_smoke.py            # write path: draws a washer end-to-end through the six operation tools
```

`washer_smoke.py` creates one new scratch part via `new_part` and leaves it open at the end (the printed transcript names its title) — close it yourself in the SolidWorks window, or via `dotnet run --project tests/SeedVerifier -- close <title>`, once you're done inspecting it. It never touches any other open document.

Unit tests (pure logic — unit parsing, argument binding, recipe JSON round-trip — no SolidWorks required):

```bash
dotnet test tests/swmcp.server.tests/swmcp.server.tests.csproj
```

SwBridge comes from the `workspace-local` NuGet source (`../localnuget`, see `nuget.config`). After changing SwBridge, repack it there — bump the version or run `dotnet nuget locals all --clear` if restore keeps a stale package:

```bash
dotnet pack ../swbridge/src/SwBridge/SwBridge.csproj -c Release -o ../localnuget
```

The `pixi.toml` environment (`pixi run -e rnd jupyterlab`) is only for Python-side prototyping/research; it is not part of the server build.

## Architecture

Request flow (read path): MCP client → `Tools/SolidWorksTool.cs` (tool surface) → SwBridge (`DocumentManager`/`SwDocument`/`ModelInspector`) → SolidWorks COM. Request flow (write/operation path): MCP client → `Tools/OperationsTool.cs` → `Services/OperationRunner.cs` → SwBridge (`ComPath`/`ComInvoker`/`DocumentStateProbes`/`ResultConverters`) → SolidWorks COM. `Program.cs` wires `SwConnection`, `DocumentManager`, `SchemaManager`, `OperationManager` and `OperationRunner` as singletons and discovers tools by assembly scan (`WithToolsFromAssembly()` picks up `[McpServerToolType]`/`[McpServerTool]`).

**Logging must go to stderr.** `Program.cs` sets `LogToStandardErrorThreshold = Information` because stdout is the MCP transport. Any `Console.WriteLine` in server code corrupts the protocol stream — use `Console.Error.WriteLine`.

**Tool names on the wire are snake_case** (`get_part_info`, `list_open_documents`, `register_feature_schema`) — the MCP SDK derives them from the C# method names. `DOCUMENTATION.md` is the user-facing contract for the tool surface; update it whenever a tool is added or its shape changes.

### The schema-driven reflection design (the central idea)

SolidWorks feature definitions (`IFeature.GetDefinition()`) are late-bound COM objects whose members differ per feature type. Rather than hardcoding a C# class (or a dedicated MCP tool) per feature type, the server keeps a **data-driven registry** of `featureTypeName → [property specs]` and reads those members off the definition object by reflection (SwBridge's `ComPropertyReader`):

- `Services/SchemaManager.cs` — the registry. Loads/saves `known_features.json`; exposes `GetSchema` as the lookup SwBridge consumes.
- A spec is `{name, member, args?}` because definition objects expose some values as bare properties (`BothDirections`) and others only via accessor methods (`GetDepth(true)`) — verified live; bare names alone were silently returning nothing before this format existed.
- Adding support for a new feature type normally requires **no C# changes** — register the type via the `register_feature_schema` tool or seed `known_features.json`. Feature type names come from `IFeature.GetTypeName2()` (e.g. `Extrusion`, `Fillet`, `CirPattern`).

Every seed entry is signature-checked against the interop interface its feature type maps to, and 15 of the 20 types are live-verified end to end against SolidWorks 2026 SP3.0 (feature created with known dimensions, value read back and compared). `Sweep`, `Loft`, `HoleWzd`, `Rib`, `SweepThread` and `Dome` are signature-verified only. `tests/SeedVerifier` is the standalone harness that does both (`static` and `zoo` modes); `../models/FeatureZoo.SLDPRT` is the regression part it builds; `../docs/seed-verification.md` has the per-spec verdicts and the SolidWorks quirks found. The planned enrichment path is a tool that consults SolidWorks API documentation to derive correct specs for unknown feature types.

### known_features.json persistence gotcha

`src/server/known_features.json` is only a **seed**. On first run `SchemaManager` copies it to `%LOCALAPPDATA%\swmcp\known_features.json` and reads/writes there forever after. Editing the repo copy has no effect on an existing install — delete the LOCALAPPDATA copy to re-seed, or expect confusing stale behavior when debugging schema issues. The legacy format (arrays of bare strings) still parses, as bare-property specs.

### The operation-recipe design (the write-side counterpart)

The read side's "no per-feature C# class" rule has a write-side analogue, per `../docs/adr/0001-generic-operation-surface.md` (and 0002/0003 for verification and COM-thread confinement): a **recipe** is one declared COM invocation, described as data, instead of a dedicated tool per SolidWorks capability (`create_extrusion`, `create_fillet`, ... never exist as C# methods here).

- `Models/OperationRecipe.cs` — the recipe shape: `name`, `scope` (`application`/`document`), `target` (dotted `ComPath`), `kind` (`method`/`propertySet`/`propertyGet`), `member`, `params` (named, typed, defaulted — `bool`/`int`/`double`/`string`/`length`/`angle`/`enum`/`comNull`), `requires` (preconditions), `returns`, `verify` (post-conditions), `source` (`seed`/`registered`).
- `Services/OperationManager.cs` — the registry. `known_operations.json` (shipped, `Content`/`CopyToOutputDirectory`) is re-read fresh from disk on **every** start; `%LOCALAPPDATA%\swmcp\known_operations.json` holds only user-`register_operation`-ed recipes and is never touched by that refresh — deliberately *not* mirroring the `known_features.json` stale-copy gotcha below. `Validate` checks recipe shape against the closed v1 vocabulary; `Register` also best-effort live-checks the target/member against the COM type library via `ComTypeInspector` when SolidWorks is reachable, warning (never rejecting) on a mismatch.
- `Services/OperationRunner.cs` — executes one recipe against one document (or the application, for `new_part`) inside a single `SwDispatcher.Run` call: resolves `target` via `ComPath`, binds named args to a positional array (`Services/UnitParser.cs` handles `"5 mm"`/`"30 deg"` quantity-string sugar; `comNull` params always bind `new DispatchWrapper(null)` — a bare `null` triggers `DISP_E_TYPEMISMATCH`), checks `requires` (refuses, never auto-satisfies), invokes via `ComInvoker`, evaluates `verify` (a step that invokes without a COM error but whose post-conditions don't hold is still reported as a failure — SolidWorks write APIs often return `Nothing`/`False` instead of throwing). `new_part` (`scope: application`, `member: NewPart`) is a documented special case: it calls `DocumentManager.NewPart` directly rather than dispatching through `ComPath`/`ComInvoker`, because creating a document (including the default-template lookup) is SwBridge policy, not a raw COM member on `ISldWorks`.
- `Tools/OperationsTool.cs` — the six MCP tools: `list_operations`, `describe_operation`, `run_operation`, `run_operations` (ordered batch, fail-fast, no auto-undo), `register_operation` (the enrichment entry point), `describe_com_members` (wraps SwBridge's `ComPath`+`ComTypeInspector` for live member discovery — the enrichment loop's eyes). `documentName` is **required** on every `scope: document` operation — stricter than the read tools, deliberately (a wrong read is a wrong answer; a wrong write modifies the wrong part).
- The seed (`new_part`, `select_by_id`, `clear_selection`, `insert_sketch`, `exit_sketch`, `create_circle_by_radius`, `create_line`, `extrude_boss`, `rebuild`, `undo`) is exactly the washer chain plus its safety valves; parameter defaults and proven argument shapes came from `tests/SeedVerifier/Zoo.Live.cs`'s live-verified calls. `tests/washer_smoke.py` drives the built server through all ten to build and verify a real washer part.
- Deviation from the ADR, documented in `Models/OperationRecipe.cs`: the `selectionType(type, mark)` precondition's `type` is implemented as a `swSelectType_e` integer (given as a string) rather than the unspecified shape the ADR left open, evaluated generically via `ComPath`+`ComInvoker` against `SelectionMgr` since SwBridge 0.4.0 has no dedicated selection-type probe.
- `known_operations.json` deliberately does **not** have the `known_features.json` stale-copy gotcha described above — see the `Services/OperationManager.cs` bullet.

### COM connection

SwBridge's `SwConnection` attaches lazily on first use and re-attaches automatically if SolidWorks was closed/restarted — there is no need to restart the server around SolidWorks lifecycle events. When SolidWorks is not running, tools return an `error` field explaining that.

## Working in this repo

- Anything that talks COM belongs in SwBridge, not here. This repo must contain no interop code; the decision about *how a capability is exposed to an AI client* is what lives here.
- `documentation/SW_API/` holds two long research briefs on the SolidWorks .NET API — feature-tree traversal, sketch geometry (`ISketch`, `ModelToSketchTransform`), projections (`IModeler.GetBodyOutline2`), and feature-creation `FeatureData` patterns. Consult these before guessing at SolidWorks API signatures.
- `.vscode/mcp.json` points the local MCP client at the **built exe**, not `dotnet run` — rebuild before re-testing through a client.
- `tests/test_client.py` is the manual smoke script (correct newline-delimited JSON framing). There is no automated test suite yet.
- Branch naming follows `<issue-number>-<slug>` (e.g. `5-swbridge-extraction`).
