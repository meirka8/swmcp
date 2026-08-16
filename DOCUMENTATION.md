# SolidWorks MCP Server Documentation

## Overview
The **SolidWorks MCP Server** (`swmcp`) is a Model Context Protocol (MCP) server that enables AI agents to interact with a running instance of SolidWorks. It allows for reading data from open SolidWorks parts (mass properties, features, bounding box dimensions), and for **creating and modifying geometry** through a generic, data-driven operation surface — there is no per-feature tool (no `create_extrusion`); instead a small, fixed set of seven tools execute named **operation recipes** that describe a single COM invocation each.

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

Seven tools cover **every** SolidWorks write capability, present and future — the tool count is fixed; SolidWorks coverage grows by adding entries to a data-driven registry (`known_operations.json`, plus anything registered/unregistered at runtime), never by adding a C# method. See `../docs/adr/0001-generic-operation-surface.md`, `0002` (verification/no-rollback) and `0003` (COM-thread confinement) for the full design rationale; this section is the user-facing contract.

**`documentName` is required on every document-scoped operation** — unlike the read tools above, there is no "exactly one document is open" fallback. A wrong read is merely a wrong answer; a wrong write modifies the wrong part. The one exception is `new_part`, which is *application*-scoped (it creates the document `documentName` would otherwise name) and returns the new document's title for you to pass to every subsequent step. A `documentName` that matches more than one open document (e.g. an unsaved scratch `Part2` alongside a saved `Part2.SLDPRT`) is **refused**, not guessed — on every tool, read or write.

### Unit policy: length/angle params always require an explicit unit

`length` and `angle` params never accept a bare number from a caller — `{"depth1": 40}` is refused, not silently treated as 40 meters. Use a quantity string: `"6 mm"`, `"0.25 in"`, `"30 deg"`, or an explicit SI quantity string like `"0.006 m"`/`"0.5 rad"`. This closes a real failure mode found in UAT: a bare number silently meant meters/radians, so a typo'd or AI-guessed magnitude produced a technically-successful but wildly wrong part with no indication anywhere in the response. (A recipe's own *declared default*, e.g. `select_by_ray`'s `radius` defaulting to `0.0005`, is exempt — it is pre-authored SI data, the same trust boundary as the recipe's `target`/`member`, not a caller's guess.)

Every `run_operation`/`run_operations` response echoes **`boundArgs`**: the exact, final SI values actually sent to COM after unit parsing and defaulting. Check it whenever geometry looks wrong — it is the audit trail for a bad binding, and it is present on both success and (whenever binding completed) failure responses.

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
    - `args` (object, optional): named arguments for the operation's declared params. `length`/`angle` params always require an explicit unit — see "Unit policy" above; a bare number is refused. Omitted params use their declared default; a missing *required* param with no default is a refused call, not a SolidWorks error. **Any key that does not name a declared param is refused**, listing the recipe's real param names — a typo (`"marks"` instead of `"mark"`) no longer silently falls back to a default.
    - `documentName` (string, optional but required for every `scope: "document"` operation): which open document to act on (title, file name, or path).
- **Returns**: `{ success, error, return, documentState, boundArgs }`.
    - `success`: `true` only when the invocation completed **and** its declared `verify` post-conditions held (ADR 0002). SolidWorks write APIs frequently report failure by returning `Nothing`/`False` rather than throwing, so a step can be `success: false` with `error: null`-looking COM behavior but a failed verification — the `error` field always explains which.
    - `return`: the operation's declared return shape (see "Return shapes" below), or `null` for `void`. A recipe whose declared `returns.type` cannot describe what the call actually returned is itself a **failure** (`success: false`) rather than a raw/unconvertible value leaking into the response.
    - `documentState`: `{ documentName, inSketchMode, featureCount, selectionCount }` — cheap diagnostic snapshot taken right after the call, useful when `success` is `false`.
    - `boundArgs`: the final, named SI values actually bound to the COM call — see "Unit policy" above. Null only when the call failed before binding completed (e.g. missing `documentName`).
- A **refused precondition** (`requires` not satisfied) is reported the same way — `success: false`, `error` names which operation to call first (e.g. *"Precondition 'inSketchMode' failed: no active sketch. Call 'insert_sketch' first."*). Preconditions are never auto-satisfied.
- SolidWorks being unreachable, or a single call taking longer than 120 seconds (e.g. a modal SolidWorks dialog is blocking it — check the SolidWorks window), is reported as `{ success: false, error }`, never an unhandled JSON-RPC error.

### `run_operations`
Executes an ordered batch of operations against **one** document, failing fast, as a **single unit of work** on SolidWorks' COM dispatcher — no other request (read or write) can interleave mid-batch and mutate the active sketch or selection a later step depends on.

- **Inputs**:
    - `steps` (array, required): `[{ operation, args? }, ...]`, executed in order.
    - `documentName` (string, optional but required whenever any step is document-scoped): the document every step in the batch acts on.
- Operation names are resolved **before any step runs**: an unknown operation anywhere in the list refuses the whole batch up front, with nothing executed and `completedSteps: []`.
- **Returns** on full success: `{ completedSteps: [{ index, operation, result }, ...] }`, where each `result` has the same shape `run_operation` returns (including `boundArgs`).
- **Returns** on the first failing step: `{ error, failedStepIndex, failedOperation, documentState, boundArgs, completedSteps }` — every step that *did* succeed, plus the failure detail, the failing step's bound args, and document state at the point execution stopped.
- **There is no automatic rollback.** A partial plan leaves the document exactly as the completed steps left it (ADR 0002) — call the `undo` operation yourself if you need to back out. Steps do not pass return values to each other; any coupling between steps goes entirely through SolidWorks' own state (the active sketch, the current selection) — this is why `select_by_id` and `insert_sketch`/`exit_sketch` exist as their own steps rather than being folded into `extrude_boss`.
- If the plan needs a brand-new document, call `run_operation` with `new_part` **first** (it is application-scoped and cannot be a step in a batch), then pass its returned title as `documentName` to `run_operations`.
- The whole batch shares **one generous timeout** (120s + 30s per step). If the entire batch does not complete within it — e.g. a modal SolidWorks dialog appears mid-batch — the call fails with **no transcript at all** (`{ error, completedSteps: [] }`): the in-progress work is still running on SolidWorks' dispatcher and cannot be recovered from a timed-out wait. This is rare with the generous default and is the accepted trade-off for single-dispatch batch isolation.

### `register_operation`
Validates and persists a new operation recipe — the entry point for adding SolidWorks capability beyond the shipped seed, without a server release.

- **Inputs**: `recipe` (object, required) — the full recipe, in the shape `describe_operation` returns (see "Recipe format").
- **Behavior**:
    1. Validates recipe shape: known `scope`/`kind`/param-`type`/`requires`-check/`verify`-check vocabulary, unique param names, non-empty `name`/`member`, application-scoped recipes cannot declare `requires` (every v1 precondition is document-scoped), a `selectionType` requires check needs `mark`, a `returnEquals` verify check needs `expected`. A shape error is rejected outright (`{ error }`, nothing persisted).
    2. When SolidWorks is reachable, best-effort checks the `target` path and `member` name/parameter-count against the live COM type library (`describe_com_members`'s same discovery mechanism). This only ever **warns**, never rejects — dispatch aliases and optional parameters make the type library an imperfect oracle, and rejecting here would undermine the whole point of runtime enrichment.
    3. Persists atomically to `%LOCALAPPDATA%\swmcp\known_operations.json` with `source: "registered"`. A name matching a seed operation shadows it from then on (a way to correct a seed recipe without a server release, and reversible via `unregister_operation`).
- **Returns**: `{ registered: name, warnings: [...] }` on success (an empty `verify` list is always one of the warnings — see ADR 0002), or `{ error, warnings }` on a shape-validation failure. If the on-disk store was found corrupted and quarantined earlier this session, that is also surfaced as a warning here (see "Registered-operation persistence" below).
- **Recommended loop**: `describe_com_members` to find real member names/signatures on the target you want to drive → cross-reference SolidWorks API documentation for parameter meaning/units/enum values → `register_operation`.

### `unregister_operation`
Removes a recipe added via `register_operation`, persisting the change.

- **Inputs**: `operation` (string, required) — name of a registered operation to remove.
- **Refuses** (rather than doing nothing silently) for a name that is not currently registered — including a **seed** operation's name: seed recipes ship with the server and are refreshed from `known_operations.json` on every start, so "removing" one would just have it reappear next launch.
- If a registered recipe shadowed a seed operation of the same name, unregistering it **restores the seed version** (it does not delete the name from `list_operations`).
- **Returns**: `{ unregistered: name }` on success, or `{ error }` naming why (unknown name, or a seed name).

### `describe_com_members`
Read-only discovery of the members a live SolidWorks COM object actually exposes — the enrichment loop's eyes, and the mechanism `register_operation`'s live check itself uses.

- **Inputs**:
    - `documentName` (string, optional): which open document to inspect. Omit to inspect the SolidWorks *application* object (`ISldWorks`) instead of a document.
    - `targetPath` (string, optional): dotted path from the document (or application) root, e.g. `"FeatureManager"`, `"Extension.SelectionManager"`, `"SketchManager"`. Empty/omitted means the root object itself. Ignored when `featureName` is given.
    - `featureName` (string, optional): name of a feature (as shown in the tree), e.g. `"Boss-Extrude1"`, whose *definition* object's members to discover instead. Requires `documentName`.
    - `nameFilter` (string, optional): case-insensitive substring filter on member name, applied before paging — e.g. `"Ray"` to jump straight to `SelectByRay` instead of paging through hundreds of members.
    - `offset` (int, default 0): zero-based index into the (optionally filtered) member list to start returning from.
    - `limit` (int, default 200): maximum members to return in this call.
- **Returns**: `{ target, discoveredVia, nameFilter, totalCount, offset, returned, hasMore, members: [{ name, kind, paramCount, returnType }] }`. `discoveredVia` is `"ITypeInfo"`, `"interop-assembly probe"` (the fallback used when an object publishes no type information of its own — most internal SolidWorks objects), or `"featureDefinition"` (the `featureName` path). **Results are never silently truncated**: `totalCount` is always the true member count (after `nameFilter`, before paging), and `returned`/`offset`/`hasMore` say exactly what page you are looking at — use `nameFilter` or increase `limit`/`offset` to see more. (An earlier version capped at 300 with no filter and no way to page further, which is how `Extension.SelectByRay` — the fix for `select_by_id`'s edge-picking unreliability — went undiscovered during UAT; see `docs/uat-ladder-report.md` B4.)

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
    - `length` — **meters** at the COM boundary; a caller-supplied value must be a quantity string (`"5 mm"`, `"1 in"`, `"2 cm"`, `"0.5 ft"`, or an explicit SI string like `"0.005 m"`) — a bare number is **refused**. A recipe's own `default` may be a bare number (already-canonical SI; see "Unit policy" above).
    - `angle` — **radians** at the COM boundary; same rule — a caller-supplied value must be a quantity string (`"30 deg"` or an explicit `"0.5 rad"`); a recipe's own `default` may be a bare number.
    - `enum` — a plain int; `enum` (a second field) documents which SolidWorks enum it is, e.g. `"swEndConditions_e"` — consult SolidWorks API documentation for the values.
    - `comNull` — a COM-interface parameter that must be a null interface pointer (e.g. `SelectByID2`'s `Callout`). Callers must never supply a value for this (refused if they do); the runner always binds a `DispatchWrapper(null)` — a bare `null` triggers `DISP_E_TYPEMISMATCH` on this API.
- **`returns.type`**: `void`, `bool`, `number`, `string`, `feature` (→ `{ name, typeName }`), `sketchSegment` (→ `{ id, segmentType }`), `sketchSegments` (array of those), or `document` (→ `{ title, path, type }`). A member return that does not match the declared `returns.type` (or an unrecognised `returns.type`) makes the step **fail** rather than pass a raw/unconvertible value through — SolidWorks COM objects never leave the dispatch thread under any circumstance.
- **`verify`** (post-conditions — a step that invokes without a COM error but fails these is still reported as a failed step): `returnNotNull`, `returnTrue`, `returnEquals` (`expected` — compares the invocation's raw return to a constant; for status-code APIs like `SaveAs3`, which returns `swFileSaveError_e` where 0 means success, not a bool), `featureCountIncreased` (`by`, default 1), `sketchSegmentCountIncreased` (`by`, default 1), `sketchModeIs` (`value`), `noNewRebuildErrors`.
- **`source`**: `"seed"` (shipped, refreshed from `known_operations.json` on every server start) or `"registered"` (added via `register_operation`, persisted in `%LOCALAPPDATA%\swmcp\known_operations.json`, never touched by the seed refresh, removable via `unregister_operation`).

### Registered-operation persistence

Writes to `%LOCALAPPDATA%\swmcp\known_operations.json` are atomic (temp file + rename) so a process kill or power loss mid-write cannot leave truncated JSON. If the file is nonetheless found unreadable on startup (e.g. hand-edited into invalid JSON), it is **quarantined** — renamed to `known_operations.json.bad-<timestamp>` — rather than silently treated as empty and then overwritten on the next `register_operation` call, which would have permanently destroyed every previously registered recipe. Registered operations are unavailable for that session; the quarantined file can be inspected and restored by hand. The next `register_operation` call's response carries a warning when this happened.

## The shipped operation seed

The original washer chain (new part → sketch → extrude), plus six recipes promoted from a UAT run against a bracket-with-hole-and-fillet part (`docs/uat-ladder-report.md`) and `create_corner_rectangle`.

| Operation | Scope | What it does |
|---|---|---|
| `new_part` | application | Creates a new part from a template (or SolidWorks' default) and returns its identity. |
| `select_by_id` | document | Selects one entity by name/type (`SelectByID2`) — populates the selection list other operations consume. **Not reliable for EDGE** after a topology change (view-dependent coordinate hint); prefer `select_by_ray`. |
| `select_by_ray` | document | Selects the entity hit by a model-space ray (`SelectByRay`) — view-independent, the reliable way to pick an edge or face. |
| `clear_selection` | document | Clears the selection list. |
| `insert_sketch` | document | Starts editing a new sketch on the selected plane/face (also used to exit one — see `exit_sketch`). |
| `exit_sketch` | document | Exits the active sketch (same COM member as `insert_sketch`; different precondition/postcondition). |
| `create_circle_by_radius` | document | Adds a circle to the active sketch. |
| `create_line` | document | Adds a line segment to the active sketch. |
| `create_corner_rectangle` | document | Adds a rectangle (four lines) to the active sketch (`CreateCornerRectangle`) — prefer this over four `create_line` calls for a rectangular profile. |
| `extrude_boss` | document | Boss-extrudes the selected sketch profile (`FeatureExtrusion3`, 23 named params). |
| `cut_extrude` | document | Cut-extrudes the selected sketch profile, removing material (`FeatureCut4`, 27 named params). If it fails, try `reverseDirection: true` first — the cut sketch plane is often coincident with a solid face. |
| `fillet_constant_radius` | document | Constant-radius fillet on the selected edge(s)/face(s) (`FeatureFillet3`, 14 named params). |
| `set_material` | document | Applies a SolidWorks material (`SetMaterialPropertyName2`) — without it, `get_part_info`'s mass is computed at water's density (1000 kg/m³). |
| `save_as` | document | Saves the document to an absolute path (`SaveAs3`); verified via `returnEquals` against the status-code 0. |
| `rebuild` | document | Forces a rebuild; reports success. |
| `undo` | document | Undoes the last N edits — never triggered automatically; call it deliberately after a failed plan. `EditUndo2` is void (no status code), so this is verified via `noNewRebuildErrors` rather than a return-value check; compare `documentState` across steps to confirm what changed. |

### Worked example: drawing a washer

Two concentric circles, extruded, is a washer. Every step below is one `run_operation` call; `tests/washer_smoke.py` drives exactly this sequence against the built server and asserts the result. Note `centerX`/`centerY` are **omitted**, not passed as `0` — a bare `0` from a caller is refused just like any other bare number (its declared default, `0`, already applies).

```jsonc
// 1. new_part  (scope: application, no documentName)
run_operation("new_part", {})
// -> { success: true, return: { title: "Part18", path: "", type: "Part" },
//      boundArgs: { templatePath: "" }, ... }
// Use "Part18" (or whatever title comes back) as documentName from here on.

// 2. select the sketch plane
run_operation("select_by_id", { name: "Front Plane", type: "PLANE" }, documentName: "Part18")

// 3. start a sketch
run_operation("insert_sketch", {}, documentName: "Part18")

// 4. two concentric circles — the washer profile
run_operation("create_circle_by_radius", { radius: "20 mm" }, documentName: "Part18")
run_operation("create_circle_by_radius", { radius: "10 mm" }, documentName: "Part18")
// -> boundArgs: { centerX: 0, centerY: 0, centerZ: 0, radius: 0.02 } — the SI
//    value actually sent to COM, confirming 20mm bound to 0.02m, not 20.

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

### Worked example: a filleted bracket with a hole and material (the promoted seed recipes)

`tests/bracket_smoke.py` builds a plate (`create_corner_rectangle` + `extrude_boss`), cuts a through hole (`cut_extrude`), picks a specific vertical edge reliably (`select_by_ray`, not `select_by_id`), fillets it (`fillet_constant_radius`), assigns 6061 aluminum (`set_material`), and saves to a temp path (`save_as`, verified via `returnEquals`) — using **only seed recipes**, zero `register_operation` calls. It asserts the fillet actually landed by checking `get_part_info` for a `Fillet`-type feature and its `DefaultRadius`, and that the mass scaled by the material's density ratio. Run it (`python tests/bracket_smoke.py`) as a second, independent proof the seed works out of the box beyond the washer.

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
- **`src/server/Services/OperationRunner.cs`**: Executes one recipe (or, via `RunBatch`, a whole `run_operations` plan in one dispatch call): target resolution, named-argument binding (unit parsing, type coercion, unknown-key rejection), precondition/postcondition evaluation, ownership-aware DTO conversion — all inside one SwBridge dispatcher call, with every SolidWorks-flavored exception (`SwBridgeException`/`COMException`/`InvalidComObjectException`) caught and turned into a structured failure rather than an unhandled exception.
- **`src/server/Services/UnitParser.cs`**: Parses the `"5 mm"`/`"30 deg"` quantity-string sugar into SI (meters/radians); refuses a bare number outright (see "Unit policy" above).
- **`src/server/Tools/OperationsTool.cs`**: The seven write-path MCP tools.
- **`tests/swmcp.server.tests/`**: xUnit unit tests for the pure logic above (unit parsing/rejection, argument binding incl. unknown-key and `comNull` rejection, `returnEquals`, recipe JSON round-trip, atomic persistence/quarantine, `unregister_operation` semantics) — no SolidWorks required.
- **`tests/washer_smoke.py`**: Live end-to-end test that draws a washer through the original seed operations and asserts the result via `get_part_info`.
- **`tests/bracket_smoke.py`**: Live end-to-end test that builds a filleted, drilled, material-assigned, saved bracket through the promoted seed operations only (zero `register_operation` calls) — see the worked example above.
- **SwBridge 0.5.0** (external, MIT): COM attachment, document resolution, generic feature reading by reflection, and the write-side mechanism — `SwDispatcher` (now message-pumping and timeout-bounded — a call that does not return within 120s throws `SwDispatchTimeoutException`, surfaced by every tool as `{success:false}`), `ComInvoker`, `ComPath` (now strictly property-get-only — a path segment naming a method fails to resolve rather than being silently invoked), `DocumentStateProbes`, `ResultConverters` (now `ownsReference`-aware, so converting a shared document handle never disconnects it for every other holder), `DocumentManager.NewPart`, `DocumentManager.Resolve` (now throws on an ambiguous match instead of silently picking the first), `ComTypeInspector`.
