---
paths:
  - "knowledge-base/**"
---

# Knowledge Base (38 гайдов по Directum RX)

## Расположение
`knowledge-base/guides/` (корень workspace)

## Когда какой гайд
| Задача | Гайд |
|--------|------|
| Работа с сущностями | `02_entities.md` |
| Куда класть код | `03_server_client_shared.md` |
| Первый контакт с DDS | `09_getting_started.md` |
| BeforeSave, Showing, PropertyChanged | `11_events_lifecycle.md` |
| Jobs, AsyncHandlers | `14_background_async.md` |
| Типовые задачи (18 рецептов) | `19_cookbook.md` |
| .mtd JSON шаблоны | `23_mtd_reference.md` |
| C# handlers, фильтрация | `25_code_patterns.md` |
| Remote Components (React) | `26_remote_components.md` |
| RC Plugin Development | `32_rc_plugin_development.md` |
| Microservice Deployment | `33_microservice_deployment.md` |
| Applied Solution Packaging | `34_applied_solution_packaging.md` |
| **Production-паттерны** | **`solutions-reference.md`** ← ГЛАВНЫЙ |
| DeploymentToolCore internals | `35_deployment_tool_internals.md` |
| DirectumLauncher internals | `36_launcher_internals.md` |
| DevelopmentStudio internals | `37_development_studio_internals.md` |
| Карта интеграций платформы | `38_platform_integration_map.md` |
| **Reference-каталоги** | `targets/REFERENCE_CATALOG.md`, `targets/CODE_PATTERNS_CATALOG.md`, `targets/RC_COMPONENTS_CATALOG.md`, `omniapplied/REFERENCE_CATALOG.md`, `docs/platform/PLUGIN_PATTERNS_CATALOG.md` |

## solutions-reference.md — обязательный reference
- **AgileBoard** — REST WebAPI 30+ endpoints, real-time, SQL history, many-to-many
- **Targets** — 6 RC, RemoteTableControl, XLSX import, Word gen, fan-out async, licensing
- **ESM** — Email-to-Ticket DCS, SLA 4 modes, AI AgentTool, ExpressionElement, priority matrix
- **CRM** — Star-архитектура, JSON serialization, BANT, pipeline value, round-robin

## Организация
Фазы 0-8: архитектура (01-08) → сущности (09-12) → reports/async (13-16) → advanced (17-21) → reference (22-25) → frontend (26) → специализированные (27-31) → plugin/deploy/packaging (32-34) → platform internals (35-38) + solutions-reference
