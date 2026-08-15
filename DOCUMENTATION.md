# SolidWorks MCP Server Documentation

## Overview
The **SolidWorks MCP Server** (`swmcp`) is a Model Context Protocol (MCP) server that enables AI agents to interact with a running instance of SolidWorks. It allows for reading data from open SolidWorks parts, such as mass properties, features, and bounding box dimensions.

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

## Schema store

`src/server/known_features.json` is only a **seed**. On first run it is copied to `%LOCALAPPDATA%\swmcp\known_features.json`, which is the file actually read and written from then on.

Every spec in the current seed is **signature-checked** against the definition interface it targets in the `SolidWorks.Interop.sldworks` assembly, so no entry names a member that does not exist. Beyond that, 15 of the 20 feature types are **live-verified** against SolidWorks 2026 SP3.0 — each feature was created programmatically with known dimensions and every value read back matched exactly: `Extrusion`, `Cut`/`ICE`, `Fillet`, `Chamfer`, `CirPattern`, `LPattern`, `MirrorPattern`, `Revolution`, `RevCut`, `Shell`, `Draft`, `RefPlane`, `RefAxis`. `Sweep`, `Loft`, `HoleWzd`, `Rib`, `SweepThread` and `Dome` are signature-verified only. `models/FeatureZoo.SLDPRT` in the workspace is the regression asset those features were built into; `tests/SeedVerifier` is the harness. See `docs/seed-verification.md` for the per-spec verdicts and the SolidWorks behaviours discovered along the way.

Values are raw SolidWorks API units: **meters and radians**, never the document's display units. Specs deliberately read only scalars — a member that returns a COM object reference (a sketch, plane or face) is left out of the seed, since it cannot be usefully serialized into a tool response.

The legacy schema format (plain arrays of property-name strings) is still parsed — entries are treated as bare properties — but the modern object form is written on save.

## Architecture

The project is built using C# and .NET 8.0.

- **`src/server/Program.cs`**: Entry point; registers SwBridge's `SwConnection` (lazy attach + auto re-attach) and `DocumentManager`, the `SchemaManager`, and the MCP server over STDIO.
- **`src/server/Services/SchemaManager.cs`**: The dynamic schema registry — `featureType → property specs`. Loads/saves `%LOCALAPPDATA%\swmcp\known_features.json`.
- **`src/server/Tools/SolidWorksTool.cs`**: Defines the MCP tools; maps SwBridge results (feature `Properties`) to the tool contract (`known`/`data`).
- **SwBridge** (external, MIT): COM attachment, document resolution, generic feature reading by reflection.
