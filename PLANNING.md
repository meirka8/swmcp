# PLANNING.MD: SolidWorks MCP Server Project

## 1. Project Title
SolidWorks Model Context Protocol (MCP) Server

## 2. Goal
To develop a Python-based server that implements the Model Context Protocol (MCP) to enable AI clients (e.g., Large Language Models) to interact with SolidWorks Computer-Aided Design (CAD) software. This server will act as a bridge, allowing AI to query SolidWorks model data, and potentially perform basic operations, by exposing SolidWorks functionalities as "tools" via the MCP.

## 3. Scope

### In-Scope:
* **MCP Server Implementation:** Create a Python server adhering to the Model Context Protocol specification using the official Python SDK.
* **SolidWorks API Interaction:** Utilize the SolidWorks COM API through Python (e.g., using `pywin32` or `comtypes`) to communicate with a running SolidWorks instance.
* **Basic Read Operations:** Implement MCP tools for fundamental read operations such as:
    * Opening a specified SolidWorks file (part, assembly, drawing).
    * Retrieving active document information (filename, type, path).
    * Listing key model properties (e.g., mass, volume, custom properties).
    * Listing features in a part.
    * Listing components in an assembly.
    * Getting basic parameters/dimensions (e.g., global variables, specific feature dimensions if easily addressable).
* **Local SolidWorks Instance:** The server will primarily target interaction with a SolidWorks instance running on the same machine as the server.
* **Error Handling:** Basic error handling for API interactions and MCP requests.
* **Security:** Basic considerations for tool exposure; assume a trusted environment for the initial scope. MCP itself has security considerations (host process managing client instances).

### Out-of-Scope (for initial version):
* **Complex Modeling Operations:** Creating complex geometry, advanced feature manipulation, running complex simulations directly via MCP tools.
* **Direct Remote SolidWorks Control (beyond local machine):** Complex DCOM configurations or other remote solutions for SolidWorks API are out of scope. The server and SolidWorks are assumed to be co-located. (Note: SolidWorks Document Manager API could be an alternative for some offline operations but has different capabilities).
* **SolidWorks PDM Integration:** While a PDM Web API exists, integrating PDM operations is out of scope for this project phase.
* **Advanced UI/Client:** Development of a sophisticated client UI for interacting with the MCP server. Testing will likely use simple scripts or MCP inspector tools.
* **User Management & Advanced Permissions:** Complex user authentication and granular permission systems beyond what MCP might offer at the host level.
* **Handling all SolidWorks file types and versions:** Focus on common part (`.SLDPRT`) and assembly (`.SLDASM`) files from a recent, stable SolidWorks version.
* **Real-time collaborative editing features.**
* **Full SolidWorks GUI Automation:** The server will interact with the API, not automate GUI clicks.

## 4. High-Level Architecture

+-----------------+     (JSON-RPC over HTTP/S, WebSockets, etc.)     +---------------------+     (COM)     +-------------------+
|   AI Client /   | &lt;----------------------------------------------> | Python MCP Server   | &lt;-----------> | SolidWorks        |
|   MCP Consumer  |                                                  | (Flask/FastAPI etc.)|             | Application       |
+-----------------+                                                  | - MCP Python SDK    |             +-------------------+
| - SolidWorks API    |
|   Wrapper (pywin32/ |
|    comtypes)        |
+---------------------+


* **AI Client/MCP Consumer:** Any application (e.g., an LLM agent, a script) that can communicate using the Model Context Protocol.
* **Python MCP Server:** The core component to be built. It will:
    * Host MCP services.
    * Translate MCP requests into SolidWorks API calls.
    * Format SolidWorks data into MCP responses.
* **SolidWorks Application:** A running instance of SolidWorks desktop software.

## 5. Tech Stack
* **Programming Language:** Python (3.12)
* **MCP Framework:** `modelcontextprotocol` Python SDK.
* **SolidWorks API Interaction:** `pywin32` or `comtypes` library for COM automation.
* **Web Server Framework (for MCP):** The `modelcontextprotocol` SDK may handle server aspects, or a lightweight framework like Flask or FastAPI could be used as a base if needed for more control over HTTP aspects or additional non-MCP endpoints. FastAPI is a strong candidate for API development.
* **Operating System:** Windows (due to SolidWorks COM API dependency).
* **SolidWorks Version:** A specific recent version of SolidWorks (e.g., SolidWorks 2023+).
* **Development Environment:** Standard Python development tools (VS Code, PyCharm), Git.

## 6. Key Challenges/Considerations
* **SolidWorks API Complexity:** The SolidWorks API is extensive and can be complex. Identifying the right API calls and handling their nuances will be a key challenge.
* **COM Marshalling:** Data type conversion and error handling between Python and the COM interface can be tricky.
* **SolidWorks Instance Management:** The server will rely on a running SolidWorks instance. Handling cases where SolidWorks is not running, crashes, or is busy will be important.
* **Performance:** SolidWorks API calls can sometimes be slow. The server design should consider asynchronous operations if possible, though COM interaction is often synchronous.
* **Error Propagation:** Translating SolidWorks API errors into meaningful MCP error responses.
* **Security:** Exposing CAD operations via an API needs careful consideration of security implications, especially if write operations are ever implemented.
* **State Management:** Some SolidWorks operations are stateful (e.g., require a document to be open). The MCP server will need to manage this state appropriately for different clients or requests.
* **Licensing:** Ensure compliance with SolidWorks API and potential MCP licensing if applicable for distribution.

## 7. Documentation
* **API Documentation:** SW API documentation is available at https://help.solidworks.com/2025/english/api/SWHelp_List.html. However, 2 precompiled documents are available as part of the project setup:
    * `swmcp\documentation\SW_API\SW_API_basic_actions.md` - documents the basic actions that can be performed using the SW API
    * `swmcp\documentation\SW_API\SW_API_polling.md` - documents the polling actions, i.e. getting extensive information about the current state of the SW document that can be performed using the SW API.

## 8. Future Enhancements
* Support for more SolidWorks entities (drawings, sketches, mates, configurations).
* Basic write operations (e.g., modify a custom property, change a dimension, suppress/unsuppress features).
* Integration with SolidWorks Document Manager API for operations that don't require a full SolidWorks instance.
* Asynchronous task handling for long-running SolidWorks operations.
* More robust error handling and logging.
* Support for SolidWorks PDM actions through its web API.
* Containerization of the Python server (though SolidWorks itself cannot be containerized easily).
* Enhanced security features (authentication, authorization for MCP tools).