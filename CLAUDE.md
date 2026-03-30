# Directum MCP Server v2.0

## Project Overview
Modular MCP (Model Context Protocol) server system for Directum RX platform.
5 specialized servers replacing 1 monolith:

| Server | Tools | Purpose |
|--------|-------|---------|
| **directum-scaffold** | 11 | Entity, module, function, workflow, component generation |
| **directum-validate** | 17 | Package validation, GUID consistency, resx, fixes |
| **directum-analyze** | 19 | Metadata search (cached!), metrics, dependencies |
| **directum-deploy** | 6 | Build .dat, deploy, diagnostics |
| **directum-runtime** | 33 | OData queries, tasks, documents, analytics |

Total: **86 tools, 624 tests** + legacy servers (100 tools) during migration

## Technology Stack
- .NET 10 Console App
- NuGet: `ModelContextProtocol` (v1.1.0)
- Transport: stdio (default) / HTTP (--http flag)
- OData v4 client for Directum RX Integration Service
- JSON parsing for MTD metadata files

## Directum RX Integration Points
- OData endpoint: `http://localhost/Integration/odata` (via HAProxy :8080)
- Auth: HTTP Basic Auth (service account)
- HTTP errors: 401 (credentials), 403 (permissions), 404 (not found), 503 (service down)

## Project Structure
```
directum-mcp-server/
├── src/
│   ├── DirectumMcp.Core/              — Shared: models, OData client, parsers, validators
│   │   ├── Services/                  — 9 services (EntityScaffold, PackageValidate, Pipeline...)
│   │   ├── Pipeline/                  — PipelineExecutor + Registry + PlaceholderResolver
│   │   ├── Validators/                — PackageValidator (14 checks), PackageWorkspace
│   │   ├── Helpers/                   — DirectumConstants, PathGuard, ODataHelpers
│   │   ├── Models/                    — MtdModels, DirectumConfig
│   │   ├── Parsers/                   — MtdParser, ResxParser
│   │   └── OData/                     — DirectumODataClient
│   │
│   ├── DirectumMcp.DevTools/          — Development MCP server (stdio)
│   │   ├── Tools/                     — 64 tools (scaffold_*, validate_*, analyze_*, ...)
│   │   ├── Resources/                 — PlatformKnowledgeBase (22 static + 3 dynamic)
│   │   └── Prompts/                   — 9 prompts (create-solution, review-package, ...)
│   │
│   ├── DirectumMcp.RuntimeTools/      — Runtime MCP server (stdio)
│   │   ├── Tools/                     — 25 tools (search, analyze_*, delegate, ...)
│   │   ├── Resources/                 — RuntimeKnowledgeBase (4 resources)
│   │   └── Prompts/                   — 5 prompts (analyze-workload, quick-dashboard, ...)
│   │
│   └── DirectumMcp.Tests/            — 619 unit tests (xUnit)
│
├── .mcp.json                          — Claude Code MCP config
├── CLAUDE.md                          — This file
└── README.md
```

## Key Architecture Patterns

### Services Layer (Core/Services/)
- Each tool delegates to a Service with typed Request/Result
- Services implement `IPipelineStep` for dual-use (MCP + pipeline)
- 9 services: ModuleScaffold, EntityScaffold, FunctionScaffold, JobScaffold,
  PackageValidate, PackageFix, PackageBuild, InitializerGenerate, PreviewCard

### Pipeline (Core/Pipeline/)
- `PipelineExecutor` — sequential step execution
- `PlaceholderResolver` — `$prev.field`, `$steps[0].field`, `$steps[id].field`
- `PipelineToolRegistry` — 9 tools available for pipeline

### Knowledge Base (Resources/)
- PlatformKnowledgeBase.cs — 22 static Resources (~2253 lines), covering:
  - Platform rules, entity types, module GUIDs (30/30 modules)
  - Entity catalog (30+ entities with GUIDs, properties, usage guide)
  - C# patterns, workflow patterns, property types
  - Solution design (CRM, ESM, HR archetypes)
  - 4 solution-specific patterns: crm-patterns, esm-patterns, targets-patterns, solutions-reference
  - Known issues (18 DDS problems), integration patterns, report patterns
- DynamicResources.cs — 3 live Resources from SOLUTION_PATH
- RuntimeKnowledgeBase.cs — 4 Resources (OData, tasks, documents, analytics)

## Coding Conventions
- C# 12, file-scoped namespaces, nullable enabled
- Async/await everywhere
- Tool classes: `[McpServerToolType]`, methods: `[McpServerTool]`
- One tool per file (thin wrapper → Service in Core)
- `[McpServerTool(Name = "...")]` + separate `[Description("...")]` (NOT Description inside McpServerTool)
- Russian descriptions for user-facing text
- English code, comments only where logic is non-obvious
- No excessive error handling — fail fast with clear messages
- PathGuard for all file-system operations in DevTools
