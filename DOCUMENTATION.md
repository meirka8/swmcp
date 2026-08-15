# SolidWorks MCP Server Documentation

## Overview
The **SolidWorks MCP Server** (`swmcp`) is a Model Context Protocol (MCP) server that enables AI agents to interact with a running instance of SolidWorks. It allows for reading data from open SolidWorks parts (mass properties, features, bounding box dimensions), and for **creating and modifying geometry** through a generic, data-driven operation surface — there is no per-feature tool (no `create_extrusion`); instead a small, fixed set of six tools execute named **operation recipes** that describe a single COM invocation each.

SolidWorks COM access is provided by [SwBridge](https://github.com/meirka8/swbridge), an MIT-licensed abstraction layer consumed as a NuGet package. This repository contains only the MCP layer: tool definitions and the dynamic feature-schema registry.

## Prerequisites
- **Windows OS** (Required for SolidWorks)
- **SolidWorks** (Installed and running — the server attaches to the running instance, it never launches one)
- **.NET 8.0 SDK** (To build and run the server)

## Installation & Build

1.  Clone the repository.
2.  Build the project:
    ```powershell
    dotnet build src/server/server.csproj
    ```

Until SwBridge is published on nuget.org, restore uses the `workspace-local` source in `nuget.config`, which expects the packed SwBridge NuGet in `../localnuget` (see that file for the pack command).

## Configuration

To use this server with an MCP client (e.g., Claude Desktop, or an IDE extension), add the following configuration to your MCP settings file (usually `claude_desktop_config.json` or similar):

```json
{
  "mcpServers": {
    "solidworks": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/swmcp/src/server/server.csproj" 
      ]
    }
  }
}
```
*Note: Replace `C:/path/to/swmcp` with the actual absolute path to your cloned repository.*

## Functionality

Tool names on the wire are snake_case (derived from the C# method names by the MCP SDK).

### Available Tools

#### `list_open_documents`
Lists all documents currently open in SolidWorks.

- **Inputs**: None
- **Returns**: `{ documents: [{ title, path, type }] }` where `type` is `Part`, `Assembly`, or `Drawing`. `path` is empty for unsaved documents.

#### `get_part_info`
Retrieves detailed information about an open SolidWorks part document.

- **Inputs**:
    - `documentName` (string, optional): Which open document to inspect — matches title, file name, or full path, case-insensitively. May be omitted when exactly one document is open; otherwise the error lists the open documents.
- **Returns**: A JSON object containing:
    - `path` / `title`: Identity of the document.
    - `mass`: Mass of the part (kg).
    - `features`: The feature tree. Each entry has `name`, `typeName` (from `IFeature.GetTypeName2()`), and `known`:
        - If the feature type is **known** (registered in the schema store), `data` contains the values read per its schema.
        - Otherwise `known` is `false` and there is no `data`.
    - `boundingBox`: `min`/`max` points of the part's bounding box (meters).

#### `register_feature_schema`
Teaches the server how to extract data for a feature type. The registration persists across sessions, so the set of understood feature types grows over time — the shipped `known_features.json` is only a seed.

- **Inputs**:
    - `featureType` (string): The SolidWorks feature type name (e.g., `"Extrusion"`, `"CirPattern"`).
    - `properties` (array): The properties to read off the feature's COM definition object. Each entry:
        - `name` (string): Key under which the value is reported.
        - `member` (string, optional): COM member to read, case-insensitive; defaults to `name`.
        - `args` (array, optional): Arguments when the member is an accessor method. Omit for bare properties.

    SolidWorks definition objects expose some values as bare properties and others only through accessor methods — e.g. for `Extrusion`, depth is `{"name": "Depth", "member": "GetDepth", "args": [true]}` while `{"name": "BothDirections"}` is a bare property. Bare names that are actually methods (or vice versa) simply produce no value; entries can be corrected by re-registering the schema.
- **Returns**: `{ registered, propertyCount }`.

## The operation surface (create and modify geometry)

Six tools cover **every** SolidWorks write capability, present and future — the tool count is fixed; SolidWorks coverage grows by adding entries to a data-driven registry (`known_operations.json`, plus anything registered at runtime), never by adding a C# method. See `../docs/adr/0001-generic-operation-surface.md`, `0002` (verification/no-rollback) and `0003` (COM-thread confinement) for the full design rationale; this section is the user-facing contract.

**`documentName` is required on every document-scoped operation** — unlike the read tools above, there is no "exactly one document is open" fallback. A wrong read is merely a wrong answer; a wrong write modifies the wrong part. The one exception is `new_part`, which is *application*-scoped (it creates the document `documentName` would otherwise name) and returns the new document's title for you to pass to every subsequent step.

### `list_operations`
Lists every registered operation: name, one-line summary, scope, and provenance (`seed` = shipped with the server, `registered` = added at runtime via `register_operation`). Cheap — call this first.

- **Inputs**: None.
- **Returns**: `{ operations: [{ name, summary, scope, source }] }`.

### `describe_operation`
Returns the full recipe for one operation: every named parameter (type, unit, default, required), declared preconditions, the return shape, and the post-condition checks that decide success. Read this before calling `run_operation` with an operation you haven't used yet — parameter names and units are not guessable from the summary alone.

- **Inputs**: `operation` (string, required).
- **Returns**: the recipe object (see "Recipe format" below), or `{ error }` if the name is unknown.

### `run_operation`
Executes one operation recipe against one document (or the application, for `new_part`).

- **Inputs**:
    - `operation` (string, required).
    - `args` (object, optional): named arguments for the operation's declared params. `length` params accept a bare number (meters) or a quantity string like `"5 mm"`; `angle` params accept radians or e.g. `"30 deg"`. Omitted params use their declared default; a missing *required* param with no default is a refused call, not a SolidWorks error.
    - `documentName` (string, optional but required for every `scope: "document"` operation): which open document to act on (title, file name, or path).
- **Returns**: `{ success, error, return, documentState }`.
    - `success`: `true` only when the invocation completed **and** its declared `verify` post-conditions held (ADR 0002). SolidWorks write APIs frequently report failure by returning `Nothing`/`False` rather than throwing, so a step can be `success: false` with `error: null`-looking COM behavior but a failed verification — the `error` field always explains which.
    - `return`: the operation's declared return shape (see "Return shapes" below), or `null` for `void`.
    - `documentState`: `{ documentName, inSketchMode, featureCount, selectionCount }` — cheap diagnostic snapshot taken right after the call, useful when `success` is `false`.
- A **refused precondition** (`requires` not satisfied) is reported the same way — `success: false`, `error` names which operation to call first (e.g. *"Precondition 'inSketchMode' failed: no active sketch. Call 'insert_sketch' first."*). Preconditions are never auto-satisfied.

### `run_operations`
Executes an ordered batch of operations against **one** document, failing fast.

- **Inputs**:
    - `steps` (array, required): `[{ operation, args? }, ...]`, executed in order.
    - `documentName` (string, optional but required whenever any step is document-scoped): the document every step in the batch acts on.
- **Returns** on full success: `{ completedSteps: [{ index, operation, result }, ...] }`.
- **Returns** on the first failing step: `{ error, failedStepIndex, failedOperation, documentState, completedSteps }` — every step that *did* succeed, plus the failure detail and document state at the point execution stopped.
- **There is no automatic rollback.** A partial plan leaves the document exactly as the completed steps left it (ADR 0002) — call the `undo` operation yourself if you need to back out. Steps do not pass return values to each other; any coupling between steps goes entirely through SolidWorks' own state (the active sketch, the current selection) — this is why `select_by_id` and `insert_sketch`/`exit_sketch` exist as their own steps rather than being folded into `extrude_boss`.
- If the plan needs a brand-new document, call `run_operation` with `new_part` **first** (it is application-scoped and cannot be a step in a batch), then pass its returned title as `documentName` to `run_operations`.

### `register_operation`
Validates and persists a new operation recipe — the entry point for adding SolidWorks capability beyond the shipped seed, without a server release.

- **Inputs**: `recipe` (object, required) — the full recipe, in the shape `describe_operation` returns (see "Recipe format").
- **Behavior**:
    1. Validates recipe shape: known `scope`/`kind`/param-`type`/`requires`-check/`verify`-check vocabulary, unique param names, non-empty `name`/`member`. A shape error is rejected outright (`{ error }`, nothing persisted).
    2. When SolidWorks is reachable, best-effort checks the `target` path and `member` name/parameter-count against the live COM type library (`describe_com_members`'s same discovery mechanism). This only ever **warns**, never rejects — dispatch aliases and optional parameters make the type library an imperfect oracle, and rejecting here would undermine the whole point of runtime enrichment.
    3. Persists to `%LOCALAPPDATA%\swmcp\known_operations.json` with `source: "registered"`. A name matching a seed operation shadows it from then on (a way to correct a seed recipe without a server release).
- **Returns**: `{ registered: name, warnings: [...] }` on success (an empty `verify` list is always one of the warnings — see ADR 0002), or `{ error, warnings }` on a shape-validation failure.
- **Recommended loop**: `describe_com_members` to find real member names/signatures on the target you want to drive → cross-reference SolidWorks API documentation for parameter meaning/units/enum values → `register_operation`.

### `describe_com_members`
Read-only discovery of the members a live SolidWorks COM object actually exposes — the enrichment loop's eyes, and the mechanism `register_operation`'s live check itself uses.

- **Inputs**:
    - `documentName` (string, optional): which open document to inspect. Omit to inspect the SolidWorks *application* object (`ISldWorks`) instead of a document.
    - `targetPath` (string, optional): dotted path from the document (or application) root, e.g. `"FeatureManager"`, `"Extension.SelectionManager"`, `"SketchManager"`. Empty/omitted means the root object itself. Ignored when `featureName` is given.
    - `featureName` (string, optional): name of a feature (as shown in the tree), e.g. `"Boss-Extrude1"`, whose *definition* object's members to discover instead — the same discovery `register_feature_schema` authors use. Requires `documentName`.
- **Returns**: `{ target, discoveredVia, memberCount, truncated, members: [{ name, kind, paramCount, returnType }] }`. `discoveredVia` is `"ITypeInfo"` or `"interop-assembly probe"` (the fallback used when an object publishes no type information of its own — most internal SolidWorks objects, e.g. feature definitions). Truncated at 300 members with `truncated: true` and a note — keeps a large discovery response from blowing the context window.

## Recipe format

```json
{
  "name": "extrude_boss",
  "summary": "Boss-extrudes the pre-selected sketch profile by a blind depth...",
  "scope": "document",
  "target": "FeatureManager",
  "kind": "method",
  "member": "FeatureExtrusion3",
  "requires": [
    { "check": "documentType", "value": "Part" },
    { "check": "notInSketchMode" },
    { "check": "selectionCount", "min": 1 }
  ],
  "params": [
    { "name": "singleDirection", "type": "bool", "default": true },
    { "name": "depth1", "type": "length", "required": true, "description": "D1 — blind depth, direction 1." }
  ],
  "returns": { "type": "feature" },
  "verify": [
    { "check": "returnNotNull" },
    { "check": "featureCountIncreased", "by": 1 }
  ],
  "source": "seed",
  "verifiedOn": "SolidWorks 2026 SP3.0, live"
}
```

- **`scope`**: `"application"` (target root = the SolidWorks app; currently only `new_part`) or `"document"` (target root = the resolved document).
- **`target`**: dotted, read-only path from the scope root to the object the invocation happens on — e.g. `"FeatureManager"`, `"SketchManager"`, `"Extension"`, `"Extension.SelectionManager"`, or `""` for the root itself (e.g. `ClearSelection2`/`EditRebuild3`/`EditUndo2`, which are direct `IModelDoc2` members). An unresolvable path is a runtime error naming exactly which segment failed, not a schema error — this is deliberately open-ended so a newly discovered manager object never needs a server release.
- **`kind`**: `"method"`, `"propertySet"`, or `"propertyGet"` — exactly one COM dispatch flag, never combined.
- **`member`**: the COM member invoked on the resolved target.
- **`requires`** (preconditions — refused, never auto-satisfied): `documentType` (`value`: `"Part"`/`"Assembly"`/`"Drawing"`), `inSketchMode`, `notInSketchMode`, `selectionCount` (`min`/`max`), `selectionType` (`mark` required; `type` optional — a `swSelectType_e` **integer given as a string**, checked generically against `SelectionMgr` since there is no dedicated SwBridge probe for it — a deliberate, documented simplification of the ADR's unspecified shape).
- **`params`**: ordered, named, typed, defaulted. `type` is one of:
    - `bool`, `int`, `double`, `string` — passed through.
    - `length` — **meters** at the COM boundary; accepts a bare number or a quantity string (`"5 mm"`, `"1 in"`, `"2 cm"`, `"0.5 ft"`).
    - `angle` — **radians** at the COM boundary; accepts a bare number or a quantity string (`"30 deg"`).
    - `enum` — a plain int; `enum` (a second field) documents which SolidWorks enum it is, e.g. `"swEndConditions_e"` — consult SolidWorks API documentation for the values.
    - `comNull` — a COM-interface parameter that must be a null interface pointer (e.g. `SelectByID2`'s `Callout`). Callers never supply a value; the runner always binds a `DispatchWrapper(null)` — a bare `null` triggers `DISP_E_TYPEMISMATCH` on this API.
- **`returns.type`**: `void`, `bool`, `number`, `string`, `feature` (→ `{ name, typeName }`), `sketchSegment` (→ `{ id, segmentType }`), `sketchSegments` (array of those), or `document` (→ `{ title, path, type }` — `new_part` only).
- **`verify`** (post-conditions — a step that invokes without a COM error but fails these is still reported as a failed step): `returnNotNull`, `returnTrue`, `featureCountIncreased` (`by`, default 1), `sketchSegmentCountIncreased` (`by`, default 1), `sketchModeIs` (`value`), `noNewRebuildErrors`.
- **`source`**: `"seed"` (shipped, refreshed from `known_operations.json` on every server start) or `"registered"` (added via `register_operation`, persisted in `%LOCALAPPDATA%\swmcp\known_operations.json`, never touched by the seed refresh).

## The shipped operation seed (the washer chain)

| Operation | Scope | What it does |
|---|---|---|
| `new_part` | application | Creates a new part from a template (or SolidWorks' default) and returns its identity. |
| `select_by_id` | document | Selects one entity by name/type (`SelectByID2`) — populates the selection list other operations consume. |
| `clear_selection` | document | Clears the selection list. |
| `insert_sketch` | document | Starts editing a new sketch on the selected plane/face (also used to exit one — see `exit_sketch`). |
| `exit_sketch` | document | Exits the active sketch (same COM member as `insert_sketch`; different precondition/postcondition). |
| `create_circle_by_radius` | document | Adds a circle to the active sketch. |
| `create_line` | document | Adds a line segment to the active sketch. |
| `extrude_boss` | document | Boss-extrudes the selected sketch profile (`FeatureExtrusion3`, 23 named params). |
| `rebuild` | document | Forces a rebuild; reports success. |
| `undo` | document | Undoes the last N edits — never triggered automatically; call it deliberately after a failed plan. |

### Worked example: drawing a washer

Two concentric circles, extruded, is a washer. Every step below is one `run_operation` call; `tests/washer_smoke.py` drives exactly this sequence against the built server and asserts the result.

```jsonc
// 1. new_part  (scope: application, no documentName)
run_operation("new_part", {})
// -> { success: true, return: { title: "Part18", path: "", type: "Part" }, ... }
// Use "Part18" (or whatever title comes back) as documentName from here on.

// 2. select the sketch plane
run_operation("select_by_id", { name: "Front Plane", type: "PLANE" }, documentName: "Part18")

// 3. start a sketch
run_operation("insert_sketch", {}, documentName: "Part18")

// 4. two concentric circles — the washer profile
run_operation("create_circle_by_radius", { centerX: 0, centerY: 0, radius: "20 mm" }, documentName: "Part18")
run_operation("create_circle_by_radius", { centerX: 0, centerY: 0, radius: "10 mm" }, documentName: "Part18")

// 5. exit the sketch, select it, extrude
run_operation("exit_sketch", {}, documentName: "Part18")
run_operation("select_by_id", { name: "Sketch1", type: "SKETCH", mark: 0 }, documentName: "Part18")
run_operation("extrude_boss", { depth1: "3 mm" }, documentName: "Part18")
run_operation("rebuild", {}, documentName: "Part18")

// 6. verify with the read path
get_part_info({ documentName: "Part18" })
// -> exactly one feature with typeName "Extrusion" (or "ICE" — see the note below),
//    mass > 0, boundingBox ~40mm x 40mm x 3mm.
```

The same eight document-scoped steps (everything after `new_part`) can also run as a single `run_operations` batch with `documentName: "Part18"` — see that tool's description above for the trade-off.

**Note on `IFeature.GetTypeName2()`**: on this build, only the *first* extrude/cut created in a part session reports type name `"Extrusion"`; every subsequent one reports `"ICE"` (both are still ordinary boss/cut extrudes — see `../docs/seed-verification.md` §4.1). `get_part_info`'s feature schema seed covers both type names, and any assertion on extrude-feature type should accept either.

## Schema store

`src/server/known_features.json` is only a **seed**. On first run it is copied to `%LOCALAPPDATA%\swmcp\known_features.json`, which is the file actually read and written from then on.

Every spec in the current seed is **signature-checked** against the definition interface it targets in the `SolidWorks.Interop.sldworks` assembly, so no entry names a member that does not exist. Beyond that, 15 of the 20 feature types are **live-verified** against SolidWorks 2026 SP3.0 — each feature was created programmatically with known dimensions and every value read back matched exactly: `Extrusion`, `Cut`/`ICE`, `Fillet`, `Chamfer`, `CirPattern`, `LPattern`, `MirrorPattern`, `Revolution`, `RevCut`, `Shell`, `Draft`, `RefPlane`, `RefAxis`. `Sweep`, `Loft`, `HoleWzd`, `Rib`, `SweepThread` and `Dome` are signature-verified only. `models/FeatureZoo.SLDPRT` in the workspace is the regression asset those features were built into; `tests/SeedVerifier` is the harness. See `docs/seed-verification.md` for the per-spec verdicts and the SolidWorks behaviours discovered along the way.

Values are raw SolidWorks API units: **meters and radians**, never the document's display units. Specs deliberately read only scalars — a member that returns a COM object reference (a sketch, plane or face) is left out of the seed, since it cannot be usefully serialized into a tool response.

The legacy schema format (plain arrays of property-name strings) is still parsed — entries are treated as bare properties — but the modern object form is written on save.

## Architecture

The project is built using C# and .NET 8.0.

- **`src/server/Program.cs`**: Entry point; registers SwBridge's `SwConnection` (lazy attach + auto re-attach), `DocumentManager`, `SchemaManager`, `OperationManager`, `OperationRunner`, and the MCP server over STDIO.
- **`src/server/Services/SchemaManager.cs`**: The dynamic feature-property schema registry — `featureType → property specs`. Loads/saves `%LOCALAPPDATA%\swmcp\known_features.json`.
- **`src/server/Tools/SolidWorksTool.cs`**: The read-path MCP tools; maps SwBridge results (feature `Properties`) to the tool contract (`known`/`data`).
- **`src/server/Models/OperationRecipe.cs`**: The recipe model (`OperationRecipe`, `OperationParam`, `RequireCheck`, `VerifyCheck`, `ReturnsSpec`) — see "Recipe format" above.
- **`src/server/Services/OperationManager.cs`**: The operation registry — loads/refreshes `known_operations.json`, persists registered recipes to `%LOCALAPPDATA%\swmcp\known_operations.json`, validates recipe shape, best-effort live-checks against the COM type library.
- **`src/server/Services/OperationRunner.cs`**: Executes one recipe: target resolution, named-argument binding (unit parsing, type coercion), precondition/postcondition evaluation, DTO conversion — all inside one SwBridge dispatcher call.
- **`src/server/Services/UnitParser.cs`**: Parses the `"5 mm"`/`"30 deg"` quantity-string sugar into SI (meters/radians).
- **`src/server/Tools/OperationsTool.cs`**: The six write-path MCP tools.
- **`tests/swmcp.server.tests/`**: xUnit unit tests for the pure logic above (unit parsing, argument binding, recipe JSON round-trip) — no SolidWorks required.
- **`tests/washer_smoke.py`**: Live end-to-end test that draws a washer through the operation tools and asserts the result via `get_part_info`.
- **SwBridge** (external, MIT): COM attachment, document resolution, generic feature reading by reflection, and (0.4.0+) the write-side mechanism — `SwDispatcher`, `ComInvoker`, `ComPath`, `DocumentStateProbes`, `ResultConverters`, `DocumentManager.NewPart`, `ComTypeInspector`.
