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

Live smoke test (requires SolidWorks running; drives the built exe over newline-delimited JSON-RPC):

```bash
python tests/test_client.py Part2
```

SwBridge comes from the `workspace-local` NuGet source (`../localnuget`, see `nuget.config`). After changing SwBridge, repack it there — bump the version or run `dotnet nuget locals all --clear` if restore keeps a stale package:

```bash
dotnet pack ../swbridge/src/SwBridge/SwBridge.csproj -c Release -o ../localnuget
```

The `pixi.toml` environment (`pixi run -e rnd jupyterlab`) is only for Python-side prototyping/research; it is not part of the server build.

## Architecture

Request flow: MCP client → `Tools/SolidWorksTool.cs` (tool surface) → SwBridge (`DocumentManager`/`SwDocument`/`ModelInspector`) → SolidWorks COM. `Program.cs` wires `SwConnection`, `DocumentManager`, and `SchemaManager` as singletons and discovers tools by assembly scan (`WithToolsFromAssembly()` picks up `[McpServerToolType]`/`[McpServerTool]`).

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

### COM connection

SwBridge's `SwConnection` attaches lazily on first use and re-attaches automatically if SolidWorks was closed/restarted — there is no need to restart the server around SolidWorks lifecycle events. When SolidWorks is not running, tools return an `error` field explaining that.

## Working in this repo

- Anything that talks COM belongs in SwBridge, not here. This repo must contain no interop code; the decision about *how a capability is exposed to an AI client* is what lives here.
- `documentation/SW_API/` holds two long research briefs on the SolidWorks .NET API — feature-tree traversal, sketch geometry (`ISketch`, `ModelToSketchTransform`), projections (`IModeler.GetBodyOutline2`), and feature-creation `FeatureData` patterns. Consult these before guessing at SolidWorks API signatures.
- `.vscode/mcp.json` points the local MCP client at the **built exe**, not `dotnet run` — rebuild before re-testing through a client.
- `tests/test_client.py` is the manual smoke script (correct newline-delimited JSON framing). There is no automated test suite yet.
- Branch naming follows `<issue-number>-<slug>` (e.g. `5-swbridge-extraction`).
