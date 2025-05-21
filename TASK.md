# TASK.MD: SolidWorks MCP Server Project Plan

## Phase 1: Setup, Research & Basic MCP Server
* **T1.1:** Set up Python development environment for the project (virtual environment, linters, etc.).
* **T1.2:** Install and experiment with the `modelcontextprotocol` Python SDK.
    * **T1.2.1:** Create a minimal "hello world" MCP server using the SDK.
    * **T1.2.2:** Understand how to define and register MCP tools.
* **T1.3:** Set up `pywin32` or `comtypes` for Python.
    * **T1.3.1:** Write a simple Python script to connect to a running SolidWorks instance (e.g., get SolidWorks version).
    * **T1.3.2:** Practice basic SolidWorks API calls: open a file, get active document.
* **T1.4:** In-depth review of SolidWorks API documentation relevant to planned read operations (documents, properties, features, components).
* **T1.5:** Choose a web server framework if the MCP SDK doesn't fully cover needs (e.g., Flask, FastAPI). For initial tasks, assume MCP SDK is sufficient or provides integration points.

## Phase 2: Core SolidWorks API Integration Layer
* **T2.1:** Design a Python wrapper/module for SolidWorks API interactions.
    * **T2.1.1:** Implement functions for connecting to SolidWorks.
    * **T2.1.2:** Implement functions for opening SolidWorks files (parts, assemblies). Error handling for file not found, SolidWorks not running, etc.
    * **T2.1.3:** Implement functions to get active document details (name, path, type).
* **T2.2:** Implement functions to read basic model properties:
    * **T2.2.1:** Get mass, volume, center of mass.
    * **T2.2.2:** Get custom properties (file properties).
* **T2.3:** Implement functions to list structural elements:
    * **T2.3.1:** List features in a part document (e.g., extrudes, cuts, fillets - names and types).
    * **T2.3.2:** List components in an assembly document (names, paths, instance counts).
* **T2.4:** Implement functions to retrieve basic parameters:
    * **T2.4.1:** Access global variables/equations.
    * *(Stretch Goal)*: Identify how to access specific, named dimensions (this can be complex).
* **T2.5:** Unit tests for the SolidWorks API wrapper functions (mocking COM objects might be necessary for isolated testing, or integration tests with a live SolidWorks instance).

## Phase 3: MCP Tool Definition and Implementation
* **T3.1:** Define the MCP toolset based on the implemented SolidWorks API functions from Phase 2.
    * **T3.1.1:** Define tool names, input parameters, and output structures (JSON schemas).
* **T3.2:** Implement MCP tools within the server using the `modelcontextprotocol` Python SDK.
    * **T3.2.1:** Tool: `open_solidworks_document` (Input: file path; Output: success/failure, document info).
    * **T3.2.2:** Tool: `get_active_document_info` (Output: document name, type, path).
    * **T3.2.3:** Tool: `get_model_properties` (Input: [optional] document identifier; Output: mass, volume, custom properties).
    * **T3.2.4:** Tool: `list_part_features` (Input: [optional] document identifier; Output: list of feature names/types).
    * **T3.2.5:** Tool: `list_assembly_components` (Input: [optional] document identifier; Output: list of component names/paths).
    * **T3.2.6:** Tool: `get_global_variables` (Input: [optional] document identifier; Output: list of global variables).
* **T3.3:** Implement error handling within MCP tools, translating SolidWorks API errors to appropriate MCP error responses.
* **T3.4:** Basic logging for MCP server requests and SolidWorks API interactions.

## Phase 4: Testing and Refinement
* **T4.1:** Develop test scripts or use an MCP client/inspector tool to test each implemented MCP tool thoroughly.
    * **T4.1.1:** Test with valid SolidWorks files (parts, assemblies).
    * **T4.1.2:** Test edge cases (e.g., empty files, files with no features/components, unsupported file types).
    * **T4.1.3:** Test error conditions (e.g., SolidWorks not running, file not found, API errors).
* **T4.2:** Refine MCP tool definitions (inputs/outputs) based on testing feedback for clarity and usability.
* **T4.3:** Code review and refactoring for clarity, performance, and robustness.
* **T4.4:** Test with different simple SolidWorks models to ensure general applicability.

## Phase 5: Documentation
* **T5.1:** Document the setup process for the MCP server.
* **T5.2:** Document each MCP tool:
    * Purpose
    * Required MCP request format (input parameters)
    * Expected MCP response format (output structure)
    * Potential error codes/messages.
* **T5.3:** Document basic troubleshooting steps.
* **T5.4:** Document known limitations and assumptions.

## Phase 6: Deployment Considerations & Future Planning (High-Level)
* **T6.1:** Outline steps for deploying the server in a target environment (e.g., on a specific Windows machine with SolidWorks).
* **T6.2:** Identify monitoring needs for a deployed server (e.g., server uptime, SolidWorks instance health).
* **T6.3:** Revisit "Out-of-Scope" items and "Future Enhancements" from `PLANNING.MD` to outline a potential roadmap for v2.
