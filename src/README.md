# MCP Server v2.0 — Архитектура

5 модульных серверов для Directum RX 26.1. Каждый подключается отдельно через `.mcp.json`.

## Архитектура

```
DirectumMcp.Core (shared library)
│   Services/          — 9 сервисов (EntityScaffold, PackageValidate, PackageBuild...)
│   Cache/             — MetadataCache (LRU + FileSystemWatcher для .mtd)
│   Parsers/           — MtdParser, ResxParser
│   OData/             — DirectumODataClient (Basic Auth, OData v4)
│   Pipeline/          — PipelineExecutor + Registry + PlaceholderResolver
│   Validators/        — PackageValidator (14 проверок), PackageWorkspace
│   Helpers/           — DirectumConstants, PathGuard, ODataHelpers
│   Models/            — MtdModels, DirectumConfig
│
DirectumMcp.Shared (shared infrastructure)
│   ServerSetup.cs     — фильтры: telemetry + PathGuard + error handling
│   SolutionPathConfig — резолвит SOLUTION_PATH из env
│   ToolHelpers.cs     — общие утилиты для tool-методов
│
├── DirectumMcp.Scaffold    — 11 tools: генерация сущностей
├── DirectumMcp.Validate    — 17 tools: валидация и исправление
├── DirectumMcp.Analyze     — 19 tools: поиск и анализ метаданных
├── DirectumMcp.Deploy      — 6 tools: сборка и деплой
├── DirectumMcp.Runtime     — 33 tools: OData, задачи, аналитика
│
├── DirectumMcp.DevTools    — (legacy, 67 tools) удалить после миграции
├── DirectumMcp.RuntimeTools— (legacy, 33 tools) удалить после миграции
└── DirectumMcp.Tests       — 624 xUnit теста
```

**Итого: 86 tools, 624 теста.**

---

## Серверы

### DirectumMcp.Scaffold (11 tools)

Генерация сущностей, модулей, функций, workflow-блоков, компонентов.

| Env | Описание |
|-----|----------|
| `SOLUTION_PATH` | Корень workspace (обязательный) |

**DI-сервисы:** EntityScaffoldService, ModuleScaffoldService, FunctionScaffoldService, JobScaffoldService

**Ключевые tools:**
- `scaffold_entity` — создание .mtd + .cs + .resx для сущности (Document, Databook, Task...)
- `scaffold_module` — новый модуль со всей структурой каталогов и Module.mtd
- `scaffold_function` — серверная/клиентская/shared функция с сигнатурой
- `scaffold_task` — задача + задание + уведомление + workflow-блоки
- `scaffold_webapi` — WebAPI endpoint (DDS Integration Service)

Все tools: `scaffold_entity`, `scaffold_module`, `scaffold_task`, `scaffold_async_handler`, `scaffold_webapi`, `scaffold_dialog`, `scaffold_report`, `scaffold_widget`, `scaffold_cover_action`, `scaffold_job`, `scaffold_function`

---

### DirectumMcp.Validate (17 tools)

Валидация пакетов, GUID-консистентность, resx-локализация, автоисправления.

| Env | Описание |
|-----|----------|
| `SOLUTION_PATH` | Корень workspace (обязательный) |

**DI-сервисы:** PackageValidateService, PackageFixService

**Ключевые tools:**
- `validate_all` — агрегированная валидация (включает isolated areas, GUID, resx, code)
- `check_package` — проверка PackageInfo.xml, Dependencies, структуры
- `check_resx` — валидация .resx файлов (ключи, парность neutral/ru)
- `validate_guid_consistency` — поиск дублей и конфликтов GUID в .mtd
- `fix_package` — автоисправление типовых ошибок пакета

Все tools: `validate_all`, `check_package`, `check_resx`, `fix_package`, `validate_guid_consistency`, `check_code_consistency`, `validate_expression_elements`, `validate_remote_component`, `validate_report`, `validate_workflow`, `validate_isolated_areas`, `validate_deploy`, `fix_cover_localization`, `find_dead_resources`, `sync_resx_keys`, `check_initializer`, `check_component`

---

### DirectumMcp.Analyze (19 tools)

Поиск метаданных с LRU-кэшем, анализ зависимостей, метрики кода, схемы БД.

| Env | Описание |
|-----|----------|
| `SOLUTION_PATH` | Корень workspace (обязательный) |

**DI-сервисы:** MetadataCache (IMetadataCache — LRU кэш parsed .mtd + FileSystemWatcher)

**Ключевые tools:**
- `search_metadata` — поиск сущностей по имени/типу (мгновенный через кэш)
- `extract_entity_schema` — полная схема: GUID, свойства, действия, связи
- `dependency_graph` — граф зависимостей между модулями
- `analyze_solution` — обзор решения: модули, сущности, метрики
- `solution_health` — здоровье решения: warnings, errors, рекомендации

Все tools: `search_metadata`, `extract_entity_schema`, `predict_odata_name`, `dependency_graph`, `visualize_dependencies`, `analyze_solution`, `solution_health`, `analyze_code_metrics`, `analyze_relationship_graph`, `preview_card`, `suggest_form_view`, `suggest_pattern`, `check_permissions`, `trace_integration_points`, `map_db_schema`, `compare_db_schema`, `lint_async_handlers`, `inspect`, `extract_public_structures`

---

### DirectumMcp.Deploy (6 tools)

Сборка .dat пакетов, деплой на стенд, диагностика ошибок, pipeline.

| Env | Описание |
|-----|----------|
| `SOLUTION_PATH` | Корень workspace (обязательный) |

**DI-сервисы:** PackageBuildService

**Ключевые tools:**
- `build_dat` — сборка .dat пакета из source/
- `deploy_to_stand` — деплой на Directum RX стенд (DTC)
- `diagnose_build_error` — анализ ошибки сборки + рекомендации из Known Issues
- `pipeline` — многошаговый pipeline (scaffold -> validate -> build -> deploy)
- `diff_packages` — сравнение двух версий пакета

Все tools: `build_dat`, `deploy_to_stand`, `diagnose_build_error`, `pipeline`, `diff_packages`, `trace_errors`

---

### DirectumMcp.Runtime (33 tools)

OData-запросы к Directum RX, управление задачами, документами, аналитика.

| Env | Описание |
|-----|----------|
| `RX_ODATA_URL` | URL OData endpoint (например `http://localhost/Integration/odata`) |
| `RX_USERNAME` | Логин сервисной учётки |
| `RX_PASSWORD` | Пароль |
| `RUNTIME_MCP_PORT` | Порт для HTTP-режима (default: 3001) |

**DI-сервисы:** DirectumODataClient, DirectumConfig

**Transport:** stdio (default) или HTTP (`--http` флаг, `--port=N`)

**Ключевые tools:**
- `odata_query` — произвольный OData-запрос к любому EntitySet
- `my_tasks` — задачи текущего пользователя (inbox)
- `search` — полнотекстовый поиск по сущностям
- `send_task` — создание и отправка задачи
- `daily_briefing` — сводка дня: задачи, дедлайны, согласования

Все tools: `odata_query`, `my_tasks`, `send_task`, `complete`, `delegate`, `approve`, `pending_approvals`, `bulk_complete`, `create_action_item`, `search`, `find_docs`, `find_contracts`, `discover`, `create_document`, `update_entity`, `delete_entity`, `process_stats`, `overdue_report`, `team_workload`, `deadline_risk`, `bottleneck_detect`, `daily_briefing`, `contract_expiry`, `contract_review`, `workflow_escalation`, `absences`, `auto_classify`, `summarize`, `route_bulk_action`, `analyze_pipeline_value`, `analyze_bant`, `analyze_sla_rules`, `audit_assignment_strategy`

---

## Технологии

- .NET 10, C# 12
- NuGet: `ModelContextProtocol` v1.1.0
- Transport: stdio (default) / HTTP (`--http`)
- OData v4 client для Directum RX Integration Service
- xUnit для тестирования

## Сборка

```bash
dotnet build DirectumMcp.sln
dotnet test
```

## Установка отдельного сервера

Можно подключить только нужный сервер в `.mcp.json`:

```json
{
  "mcpServers": {
    "directum-scaffold": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DirectumMcp.Scaffold"],
      "env": { "SOLUTION_PATH": "/path/to/workspace" }
    },
    "directum-validate": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DirectumMcp.Validate"],
      "env": { "SOLUTION_PATH": "/path/to/workspace" }
    },
    "directum-analyze": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DirectumMcp.Analyze"],
      "env": { "SOLUTION_PATH": "/path/to/workspace" }
    },
    "directum-deploy": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DirectumMcp.Deploy"],
      "env": { "SOLUTION_PATH": "/path/to/workspace" }
    },
    "directum-runtime": {
      "command": "dotnet",
      "args": ["run", "--project", "src/DirectumMcp.Runtime"],
      "env": {
        "RX_ODATA_URL": "http://localhost/Integration/odata",
        "RX_USERNAME": "Administrator",
        "RX_PASSWORD": "${RX_PASSWORD}"
      }
    }
  }
}
```

Runtime-сервер в HTTP-режиме (для удалённого доступа):

```bash
dotnet run --project src/DirectumMcp.Runtime -- --http --port=3001
```

## Shared-инфраструктура

### DirectumMcp.Core
Библиотека без точки входа. Содержит всю бизнес-логику:
- **9 сервисов** — EntityScaffold, ModuleScaffold, FunctionScaffold, JobScaffold, PackageValidate, PackageFix, PackageBuild, InitializerGenerate, PreviewCard
- **MetadataCache** — LRU-кэш parsed .mtd с FileSystemWatcher для инвалидации
- **Pipeline** — PipelineExecutor для многошаговых операций (scaffold -> validate -> build)
- **Validators** — PackageValidator (14 проверок), PackageWorkspace
- **Parsers** — MtdParser (JSON .mtd), ResxParser (.resx XML)
- **OData** — DirectumODataClient (HTTP Basic Auth, OData v4)

### DirectumMcp.Shared
Общая инфраструктура для всех серверов:
- **ServerSetup** — `AddDirectumFilters()`: telemetry (stderr), PathGuard (блокирует пути вне workspace), error handling
- **SolutionPathConfig** — резолвит `SOLUTION_PATH` из env, валидирует существование
- **ToolHelpers** — утилиты форматирования результатов
