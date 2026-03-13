# Directum MCP Server

## Project Overview
Standalone MCP (Model Context Protocol) server for Directum RX platform.
Two logical servers in one solution:
- **DirectumDev.Mcp** — Development tools (file-based, no RX connection needed)
- **DirectumRx.Mcp** — Runtime tools (OData API to running Directum RX instance)

## Technology Stack
- .NET 8 Console App
- NuGet: `ModelContextProtocol` (v1.1.0)
- Transport: stdio (primary), Streamable HTTP (future)
- OData v4 client for Directum RX Integration Service
- JSON parsing for MTD metadata files

## Directum RX Integration Points
- OData endpoint: `http://localhost/Integration/odata` (port 27002)
- Public API: `http://localhost/Client/api/public` (port 39700)
- Auth: HTTP Basic Auth (service account)

## Project Structure
```
directum-mcp-server/
├── src/
│   ├── DirectumMcp.Core/          — Shared models, OData client, MTD parser
│   ├── DirectumMcp.DevTools/      — Development MCP server (stdio)
│   │   └── Tools/
│   │       ├── InspectTool.cs         — inspect: universal metadata reader
│   │       ├── ValidatePackageTool.cs — check_package: 7 validation checks
│   │       ├── FixPackageTool.cs      — fix_package: auto-fix package issues
│   │       ├── ValidateResxTool.cs    — check_resx: resx validation
│   │       ├── SearchMetadataTool.cs  — search_metadata: full-text MTD search
│   │       ├── DependencyGraphTool.cs — dependency_graph: module dependency analysis
│   │       ├── FindDeadResourcesTool.cs — find_dead_resources: orphaned resx keys
│   │       ├── DiffPackagesTool.cs    — diff_packages: compare two packages
│   │       └── CheckCodeConsistencyTool.cs — check_code_consistency: MTD↔C# agreement
│   ├── DirectumMcp.RuntimeTools/  — Runtime MCP server (stdio)
│   │   └── Tools/
│   │       ├── SearchDocumentsTool.cs     — find_docs
│   │       ├── MyAssignmentsTool.cs       — my_tasks
│   │       ├── CompleteAssignmentTool.cs   — complete
│   │       ├── CreateTaskTool.cs          — send_task
│   │       ├── SummarizeTool.cs           — summarize
│   │       └── BulkCompleteTool.cs        — bulk_complete
│   └── DirectumMcp.Tests/        — Unit tests
├── .mcp.json                      — Claude Code MCP config
├── CLAUDE.md                      — This file
└── README.md
```

## Coding Conventions
- C# 12, file-scoped namespaces, nullable enabled
- Async/await everywhere
- Tool classes: `[McpServerToolType]`, methods: `[McpServerTool]`
- One tool per file
- `[McpServerTool(Name = "...")]` + separate `[Description("...")]` (NOT Description inside McpServerTool)
- Russian descriptions for user-facing text
- English code, comments only where logic is non-obvious
- No excessive error handling — fail fast with clear messages
