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
    - `Features`: A list of features in the feature tree (e.g., Extrusions, Fillets, Chamfers, Circular Patterns) with their specific data.
    - `BoundingBox`: The X, Y, Z coordinates for the minimum and maximum points of the part's bounding box.

**Example Response:**
```json
{
  "Path": "C:\\Users\\Public\\Documents\\SOLIDWORKS\\SOLIDWORKS 2024\\samples\\tutorial\\api\\box.sldprt",
  "Title": "box",
  "Mass": 0.123,
  "Features": [
    {
      "Name": "Boss-Extrude1",
      "Type": "Extrusion",
      "Data": {
        "Depth": 0.05,
        "ReverseDepth": 0.0
      }
    }
  ],
  "BoundingBox": {
    "Min": { "X": -0.05, "Y": -0.05, "Z": 0.0 },
    "Max": { "X": 0.05, "Y": 0.05, "Z": 0.05 }
  }
}
```

## Architecture

The project is built using C# and .NET 8.0.

- **`src/server/Program.cs`**: Entry point, configures the MCP server and dependency injection.
- **`src/server/Controllers/SolidWorksController.cs`**: Handles direct interaction with the SolidWorks COM API. It manages the connection to the running SolidWorks instance and extracts data.
- **`src/server/Tools/SolidWorksTool.cs`**: Defines the MCP tools exposed to the client.
- **`src/server/Models/`**: Contains data models for serializing SolidWorks feature data (e.g., `ExtrusionData`, `FilletData`).
