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
python tests/washer_smoke.py            # write path: draws a washer end-to-end through the seed operation tools
python tests/bracket_smoke.py           # write path: a filleted/drilled/material/saved bracket, promoted seed recipes only
```

`washer_smoke.py` creates one new scratch part via `new_part` and leaves it open at the end (the printed transcript names its title) — close it yourself in the SolidWorks window, or via `dotnet run --project tests/SeedVerifier -- close <title>`, once you're done inspecting it. `bracket_smoke.py` closes its own scratch document and deletes its own saved file in a `finally` block (using the SAME SeedVerifier `close` mode), even on failure — if it is ever interrupted (killed) mid-run rather than allowed to run to completion, check for and manually close a leftover `Part<N>` or `swmcp_bracket_smoke.SLDPRT` window. Neither script touches any other open document.

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

`Tools/SolidWorksTool.cs` also has `get_document_state` — a read-only, **passive** snapshot (`DocumentStateProbes` + `SelectionInspector`, never `DocumentStateProbes.RebuildSucceeded`, which forces a rebuild and would make a "just checking" call a write) of `inSketchMode`/`activeSketch`/`featureCount`/`selectionCount`/`selectedEntities`/`needsRebuild`. It exists specifically so discovering a dangling sketch or stale selection after reconnecting to a session (e.g. post-crash) doesn't require attempting a write first.

`get_part_info` gained `material`/`density` (read off the document root; `Services/FeatureTreeFilter.cs` filters 16 observed tree-plumbing type names — folders, lights, the material folder — out of `features` by default, restorable with `includeFolderFeatures: true`) so mass no longer reports a plausible-looking number (an unassigned part computes at water's 1000 kg/m³) with no way to confirm what, if anything, produced it. `material` is the one place in this codebase that names an interop type directly (`SolidWorks.Interop.sldworks.PartDoc`, early-bound cast) — verified live that `ComPropertyReader`'s late-bound `Type.InvokeMember` cannot call `GetMaterialPropertyName2` at all (its `ByRef` output parameter needs a `ParameterModifier` array SwBridge's reader does not provide), identically whether the `ByRef` slot is supplied, omitted, or null; `density` has no `ByRef` parameter and reads late-bound exactly as expected, same as everywhere else.

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

- `Models/OperationRecipe.cs` — the recipe shape: `name`, `scope` (`application`/`document`), `target` (dotted `ComPath`), `kind` (`method`/`propertySet`/`propertyGet`), `member`, `params` (named, typed, defaulted — `bool`/`int`/`double`/`string`/`length`/`angle`/`enum`/`comNull`), `requires` (preconditions), `returns`, `verify` (post-conditions, incl. `returnEquals`/`expected` for status-code APIs), `source` (`seed`/`registered`).
- `Services/OperationManager.cs` — the registry. `known_operations.json` (shipped, `Content`/`CopyToOutputDirectory`) is re-read fresh from disk on **every** start; `%LOCALAPPDATA%\swmcp\known_operations.json` holds only user-`register_operation`-ed recipes and is never touched by that refresh — deliberately *not* mirroring the `known_features.json` stale-copy gotcha below. `Validate` checks recipe shape against the closed v1 vocabulary (including: application-scoped recipes cannot declare `requires`; `selectionType` needs `mark`; `returnEquals` needs `expected`); `Register` also best-effort live-checks the target/member against the COM type library via `ComTypeInspector` when SolidWorks is reachable, warning (never rejecting) on a mismatch. `Unregister` removes a registered recipe (refuses for a seed name). Both dictionaries (`_seed`/`_registered`) are `volatile` fields swapped copy-on-write under a lock, never mutated in place — a request thread reading while another registers/unregisters never sees a torn dictionary. Persistence to disk is atomic (temp file + `File.Move(overwrite:true)`) and a malformed on-disk store is quarantined (renamed `*.bad-<timestamp>`) rather than silently treated as empty and then overwritten — an internal `(SwConnection, DocumentManager, seedPath, registeredPath)` constructor overload lets `swmcp.server.tests` exercise this against a temp directory.
- `Services/OperationRunner.cs` — executes one recipe (`Run`) or a whole ordered batch (`RunBatch`, used by `run_operations`) inside a single `SwDispatcher.Run` call so no other request can interleave mid-batch: resolves `target` via `ComPath`, binds named args to a positional array (`Services/UnitParser.cs` handles `"5 mm"`/`"30 deg"` quantity-string sugar and **refuses a bare number from a caller** — a recipe's own declared `default` is exempt, since it is pre-authored SI data, not a caller's guess; `comNull` params always bind `new DispatchWrapper(null)` and refuse if a caller supplies a value; an argument key matching no declared param is refused, naming the typo and the real param list — case-insensitively, matching the same-case-insensitive unknown-key check), checks `requires` (refuses, never auto-satisfies; application-scoped recipes cannot declare any, rejected at `Validate` time, never at run time — a deliberate single-layer choice), invokes via `ComInvoker`, evaluates `verify` (a step that invokes without a COM error but whose post-conditions don't hold is still reported as a failure). Every result carries `boundArgs` (the final SI values actually bound) alongside `documentState`, and `documentState.selectedEntities` (SwBridge 0.6.0's `SelectionInspector.GetSelection` — the `swSelectType_e` name plus a human-meaningful descriptor per selected entity, e.g. an edge's chord length and midpoint) whenever `selectionCount > 0` — the "which edge did it actually pick" readback the UAT re-verdict named the last silent-wrongness path; kept cheap by only paying for it when there is something selected to describe. Return conversion never lets a raw COM object leave the dispatch: an unhandled/unrecognised `returns.type`, or a return that does not match the declared shape, **fails the step** (after releasing the RCW) rather than passing a marker object through as a success payload; `ownsReference` is computed per-call (false when the returned object is reference-equal to the document model or the resolved target) so converting a shared handle never disconnects it for every other holder (`ResultConverters`' `ownsReference` API). Every SolidWorks-flavored exception (`SwBridgeException`/`COMException`/`InvalidComObjectException`) is caught per-step, turning what used to be an unhandled crash (SolidWorks closing mid-batch, an ambiguous `documentName`, a dispatch timeout) into a normal `{success:false}` result — critical for `RunBatch`, where a raw throw would otherwise discard every already-completed step's transcript. `new_part` (`scope: application`, `member: NewPart`) is a documented special case: it calls `DocumentManager.NewPart` directly rather than dispatching through `ComPath`/`ComInvoker`, because creating a document (including the default-template lookup) is SwBridge policy, not a raw COM member on `ISldWorks` — and it now honors its own declared `verify` (previously dead data).
- `Tools/OperationsTool.cs` — the seven MCP tools: `list_operations`, `describe_operation`, `run_operation`, `run_operations` (single-dispatch ordered batch, op names resolved before any step runs, fail-fast, no auto-undo, shares one 120s+30s/step timeout across the whole batch), `register_operation` (the enrichment entry point; its live arity/name check and `describe_com_members` both use `ComTypeInspector.DescribeAllMembers` — the union of the `ITypeInfo` and interop-assembly discovery paths, since a document root's `ITypeInfo`-only surface (~175 members) is missing `EditRebuild3`/`SaveAs3`/`EditUndo2`/`ClearSelection2` entirely, which used to produce a false "member not found" warning for four of this server's own seed operations every time one was registered), `unregister_operation` (removes a registered recipe; refuses a seed name), `describe_com_members` (wraps SwBridge's `ComPath`+`ComTypeInspector.DescribeAllMembers` for live member discovery — the enrichment loop's eyes; paginated with `nameFilter`/`offset`/`limit` and an honest `totalCount`, never a silent cap — matters more now that a document root reports 947 members, not 175). `documentName` is **required** on every `scope: document` operation — stricter than the read tools, deliberately (a wrong read is a wrong answer; a wrong write modifies the wrong part) — and an ambiguous `documentName` (matching more than one open document) is refused by `DocumentManager.Resolve` itself, caught here and in `SolidWorksTool` and turned into a structured error rather than an unhandled exception.
- The seed is the washer chain (`new_part`, `select_by_id`, `clear_selection`, `insert_sketch`, `exit_sketch`, `create_circle_by_radius`, `create_line`, `extrude_boss`, `rebuild`, `undo`) plus six recipes promoted from a UAT run (`docs/uat-ladder-report.md`): `cut_extrude`, `fillet_constant_radius`, `select_by_ray` (the reliable way to pick an edge/face — `select_by_id`'s coordinate hint is view-dependent and was proven wrong after a topology change during UAT), `set_material`, `save_as`, and `create_corner_rectangle` (not UAT-registered, added this round). Parameter defaults and proven argument shapes came from `tests/SeedVerifier/Zoo.Live.cs`'s live-verified calls and, for the promoted six, live UAT runs re-verified via `tests/bracket_smoke.py`. `tests/washer_smoke.py` builds a washer through the original ten; `tests/bracket_smoke.py` builds a filleted, drilled, material-assigned, saved bracket through the promoted six (plus the shared primitives) — zero `register_operation` calls in either script.
- `select_by_ray`'s recipe description carries UAT-re-verdict-derived aiming guidance: the pick tolerance (`radius`) is distance-dependent (a longer-range aim can silently pick a neighboring entity at the same radius — confirmed live across a 5-aim/2-topology matrix, `docs/uat-ladder-report.md` RE-VERDICT gap #2), so aim side-on at mid-length from close range and never collinear down an edge's length (which terminates ambiguously at a shared vertex). This is exactly the class of thing `documentState.selectedEntities` exists to let a caller verify after the fact, since a mis-aim reports `success: true` identically to a correct one.
- Deviation from the ADR, documented in `Models/OperationRecipe.cs`: the `selectionType(type, mark)` precondition's `type` is implemented as a `swSelectType_e` integer (given as a string) rather than the unspecified shape the ADR left open, evaluated generically via `ComPath`+`ComInvoker` against `SelectionMgr` since SwBridge has no dedicated selection-type probe.
- `known_operations.json` deliberately does **not** have the `known_features.json` stale-copy gotcha described above — see the `Services/OperationManager.cs` bullet.
- **Unit policy**: a caller-supplied `length`/`angle` value must always carry an explicit unit (a quantity string, or an explicit SI string like `"0.005 m"`) — a bare number is refused. This is deliberately stricter than the ADR's original text (which allowed a bare number as implicit SI); a live UAT found a bare number silently meaning meters/radians with no way to tell from the response, which is worse than requiring one extra character. A recipe's own `default` value may still be a bare number (already-canonical SI, the same trust boundary as the recipe's `target`/`member`) — only caller input goes through the strict gate.

### COM connection

SwBridge's `SwConnection` attaches lazily on first use and re-attaches automatically if SolidWorks was closed/restarted — there is no need to restart the server around SolidWorks lifecycle events. When SolidWorks is not running, tools return an `error` field explaining that.

## Working in this repo

- Anything that talks COM belongs in SwBridge, not here. This repo must contain no interop code; the decision about *how a capability is exposed to an AI client* is what lives here.
- `documentation/SW_API/` holds two long research briefs on the SolidWorks .NET API — feature-tree traversal, sketch geometry (`ISketch`, `ModelToSketchTransform`), projections (`IModeler.GetBodyOutline2`), and feature-creation `FeatureData` patterns. Consult these before guessing at SolidWorks API signatures.
- `.vscode/mcp.json` points the local MCP client at the **built exe**, not `dotnet run` — rebuild before re-testing through a client.
- `tests/test_client.py` is the manual smoke script (correct newline-delimited JSON framing). There is no automated test suite yet.
- Branch naming follows `<issue-number>-<slug>` (e.g. `5-swbridge-extraction`).
