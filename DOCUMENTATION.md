# SolidWorks MCP Server Documentation

## Overview
The **SolidWorks MCP Server** (`swmcp`) is a Model Context Protocol (MCP) server that enables AI agents to interact with a running instance of SolidWorks. It allows for reading data from active SolidWorks parts, such as mass properties, features, and bounding box dimensions.

## Prerequisites
- **Windows OS** (Required for SolidWorks)
- **SolidWorks** (Installed and running)
- **.NET 8.0 SDK** (To build and run the server)

## Installation & Build

1.  Clone the repository.
2.  Navigate to the server source directory:
    ```powershell
    cd src/server
    ```
3.  Build the project:
    ```powershell
    dotnet build
    ```

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

### Available Tools

#### `GetPartInfo`
Retrieves detailed information about the currently active SolidWorks part document.

- **Inputs**: None
- **Returns**: A JSON object containing:
    - `Path`: Full path to the file.
    - `Title`: Title of the document.
    - `Mass`: Mass of the part.
    - `Features`: A list of features.
        - If the feature type is **Known** (in `known_features.json`), `Data` will contain the properties defined in the schema.
        - If the feature type is **Unknown**, `Known` will be `false` and `Data` will be empty.
    - `BoundingBox`: The X, Y, Z coordinates for the minimum and maximum points of the part's bounding box.

#### `RegisterFeatureSchema`
Teaches the server how to extract data for a specific feature type.

- **Inputs**:
    - `featureType` (string): The SolidWorks feature type name (e.g., "Extrusion", "Cut").
    - `propertyNames` (string[]): A list of property names to extract from the feature's definition object (e.g., ["Depth", "DraftAngle"]).
- **Returns**: Confirmation message.

## Architecture

The project is built using C# and .NET 8.0.

- **`src/server/Program.cs`**: Entry point, configures the MCP server and dependency injection.
- **`src/server/Controllers/SolidWorksController.cs`**: Handles direct interaction with the SolidWorks COM API. It uses `SchemaManager` to determine which properties to fetch dynamically.
- **`src/server/Services/SchemaManager.cs`**: Manages the `known_features.json` database. Stores user data in `%LOCALAPPDATA%\swmcp\known_features.json`.
- **`src/server/Utilities/ComReflectionHelper.cs`**: Uses .NET Reflection to dynamically invoke properties on SolidWorks COM objects.
- **`src/server/Tools/SolidWorksTool.cs`**: Defines the MCP tools exposed to the client.
