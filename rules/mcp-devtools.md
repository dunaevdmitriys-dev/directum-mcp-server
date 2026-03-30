---
paths:
  - ".claude/directum-mcp-server/**"
---

# Directum MCP v2.0 (5 серверов, 86 tools)

## Архитектура
5 модульных серверов вместо 1 монолита. Каждый подключается отдельно в `.mcp.json`.

| Сервер | Tools | Назначение |
|--------|-------|------------|
| **directum-scaffold** | 11 | Генерация сущностей, модулей, функций, workflow, компонентов |
| **directum-validate** | 17 | Валидация, GUID consistency, resx, fix, isolated areas |
| **directum-analyze** | 19 | Поиск метаданных (с кэшем!), метрики, зависимости, схемы |
| **directum-deploy** | 6 | Сборка .dat, деплой, диагностика ошибок |
| **directum-runtime** | 33 | OData запросы, задачи, документы, аналитика |

## Расположение
`.claude/directum-mcp-server/src/DirectumMcp.{Scaffold,Validate,Analyze,Deploy,Runtime}/`

## Инфраструктура
- **DirectumMcp.Core** — 9 сервисов, MetadataCache (LRU+FileWatcher), ToolResults (6 типов)
- **DirectumMcp.Shared** — ToolHelpers, ServerSetup (filters: telemetry+PathGuard+errors), SolutionPathConfig
- **DirectumMcp.Tests** — 624 тестов (xUnit)

## Конвенции
- `[McpServerTool(Name = "lowercase_with_underscores")]`
- `[Description("Русское описание: что + когда + когда НЕ + лимиты")]`
- DI через параметры метода (SDK v1.1.0 резолвит из IServiceProvider)
- PathGuard — через ServerSetup filter (не в каждом tool)
- Async/await, fail fast

## Env vars
- `SOLUTION_PATH` (Scaffold, Validate, Analyze, Deploy) → корень workspace
- `RX_ODATA_URL`, `RX_USERNAME`, `RX_PASSWORD` (Runtime)

## Ключевые улучшения v2 (vs v1)
- **MetadataCache** — LRU кэш parsed .mtd с FileSystemWatcher → search_metadata мгновенный
- **ServerSetup filters** — telemetry + PathGuard + error handling централизованно
- **Structured output types** — ScaffoldResult, ValidationResult, AnalysisResult, DeployResult
- **P0 fix** — убран `CanBeUsedInIntegration` из scaffold_entity
- **P1 fix** — validate_isolated_areas добавлен в validate_all
- **P1 fix** — scaffold_async_handler обновляет оба .resx (neutral + ru)

## Known Issues (аудит 2026-03-28)
> Учитывай при использовании scaffold tools. После scaffold — ВСЕГДА проверяй результат.

**Исправлено в v2:**
- ~~`scaffold_entity` добавляет `CanBeUsedInIntegration`~~ → УБРАНО
- ~~`validate_all` не включает `validate_isolated_areas`~~ → ДОБАВЛЕНО
- ~~`scaffold_async_handler` обновляет только .ru.resx~~ → ОБНОВЛЯЕТ ОБА

**Остаётся:**
- `scaffold_async_handler` — монолит без Core Service (inline логика в tool)
- FormTabs warning ссылается на "25.3" вместо "26.1" в старых validate tools

## Legacy серверы
`DirectumMcp.DevTools` (67 tools) и `DirectumMcp.RuntimeTools` (33 tools) — в solution, но НЕ в `.mcp.json`. Удалить после подтверждения стабильности v2.
