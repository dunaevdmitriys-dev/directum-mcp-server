# Directum MCP Server

**Интеллектуальный ассистент разработки для платформы Directum RX** — MCP-сервер, который позволяет AI-агентам (Claude, Copilot, GigaChat) проектировать решения, генерировать код, валидировать пакеты и управлять задачами через стандартный протокол [Model Context Protocol](https://modelcontextprotocol.io/).

> Первый MCP-сервер для российской ECM/BPM платформы. 89 инструментов. 31 ресурс knowledge base. 619 тестов.

---

## Метрики

| Компонент | Количество |
|-----------|:----------:|
| Tools (DevTools) | 64 |
| Tools (RuntimeTools) | 25 |
| **Tools всего** | **89** |
| Resources (статические) | 24 |
| Resources (динамические) | 3 |
| Resources (RuntimeTools) | 4 |
| **Resources всего** | **31** |
| Prompts (DevTools) | 8 |
| Prompts (RuntimeTools) | 4 |
| **Prompts всего** | **12** |
| Unit-тесты | 619 |
| Строк кода | ~42 000 |

---

## Быстрый старт

### Требования

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) или новее
- Git-репозиторий решения Directum RX (для DevTools)
- Работающий стенд Directum RX (для RuntimeTools)

### Сборка

```bash
git clone https://github.com/your-org/directum-mcp-server.git
cd directum-mcp-server
dotnet build
```

### Подключение к Claude Code

Скопируйте `.mcp.json` в корень вашего проекта или используйте готовый из репозитория:

```json
{
  "mcpServers": {
    "directum-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/DirectumMcp.DevTools"],
      "env": {
        "SOLUTION_PATH": "d:\\path\\to\\RXlog\\git_repository"
      }
    },
    "directum-rx": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/src/DirectumMcp.RuntimeTools"],
      "env": {
        "RX_ODATA_URL": "http://localhost/Integration/odata",
        "RX_USERNAME": "Service User",
        "RX_PASSWORD": "password"
      }
    }
  }
}
```

Для Claude Desktop — добавьте в `%APPDATA%\Claude\claude_desktop_config.json`.

### Переменные окружения

| Переменная | Сервер | Описание | Обязательна |
|-----------|--------|----------|:-----------:|
| `SOLUTION_PATH` | DevTools | Путь к git-репозиторию решения Directum RX | Да |
| `RX_ODATA_URL` | RuntimeTools | URL IntegrationService (`http://host/Integration/odata`) | Да |
| `RX_USERNAME` | RuntimeTools | Имя пользователя для Basic Auth | Да |
| `RX_PASSWORD` | RuntimeTools | Пароль для Basic Auth | Нет |
| `RX_ALLOW_HTTP` | RuntimeTools | `true` — разрешить HTTP к не-localhost (небезопасно) | Нет |
| `DEPLOYMENT_TOOL_PATH` | DevTools | Путь к инструменту развёртывания | Нет |
| `DEPLOYMENT_STAGING_PATH` | DevTools | Путь к промежуточной директории развёртывания | Нет |

---

## DevTools — 64 инструмента разработки

Работают с файловой системой: читают и анализируют `.mtd`, `.resx`, `.dat`, `.cs`, `.frx` файлы. Не требуют запущенного стенда.

### Scaffolding — генерация кода (19)

| Инструмент | Описание |
|-----------|----------|
| `scaffold_module` | Создать модуль с нуля: Module.mtd, обложка, C# стабы, .sds, resx |
| `scaffold_entity` | Создать сущность или override: MTD + resx + C# стабы |
| `scaffold_task` | Создать Task + Assignment + Notice — полный комплект для workflow |
| `scaffold_function` | Создать серверную/клиентскую функцию + обновить Module.mtd |
| `scaffold_job` | Создать Background Job: MTD + обработчик + resx |
| `scaffold_async_handler` | Создать AsyncHandler: Module.mtd + C# обработчик + resx |
| `scaffold_report` | Создать отчёт: MTD + FastReport .frx + Queries.xml + обработчики |
| `scaffold_component` | Создать Remote Component: webpack, manifest, loaders, i18n |
| `scaffold_widget` | Создать виджет модуля: счётчик, диаграмма + Module.mtd + resx |
| `scaffold_cover_action` | Добавить действие на обложку модуля |
| `scaffold_dialog` | Создать InputDialog с полями, валидацией, каскадными зависимостями |
| `scaffold_webapi` | Создать WebAPI endpoint: `[Public(WebApiRequestType)]` |
| `scaffold_spa` | Сгенерировать React SPA (Vite + TypeScript + Ant Design) |
| `scaffold_ai_agent_tool` | Создать AIAgentTool: AsyncHandler для AI-агента |
| `scaffold_word_generator` | Создать Isolated-обработчик генерации Word через Aspose.Words |
| `scaffold_xlsx_import` | Создать Isolated-обработчик XLSX-импорта через ExcelDataReader |
| `generate_initializer` | Генерация ModuleInitializer: справочники, роли, права |
| `generate_routescheme` | Генерация RouteScheme для workflow: блоки, переходы, условия |
| `generate_structures_cs` | Генерация ModuleStructures.g.cs из PublicStructures в Module.mtd |

### Validation — проверки и линтинг (16)

| Инструмент | Описание |
|-----------|----------|
| `check_package` | Валидация .dat перед импортом в DDS — 14 проверок |
| `check_resx` | Поиск неверных ключей System.resx (`Resource_GUID` вместо `Property_Name`) |
| `check_code_consistency` | Проверка согласованности MTD и C#: функции, классы, namespace |
| `check_component` | Валидация Remote Component: manifest, webpack, loaders, i18n |
| `check_initializer` | Валидация ModuleInitializer: версии, роли, async-паттерн |
| `check_permissions` | Проверить AccessRights в MTD: пустые права, дубликаты, роли |
| `validate_workflow` | Валидация RouteScheme: мёртвые блоки, тупики, переходы без условий |
| `validate_report` | Валидация отчёта: .frx и Queries.xml — датасеты, подключения |
| `validate_guid_consistency` | Cross-file GUID validation: Controls, Properties, Forms, resx |
| `validate_deploy` | Проверка стенда после публикации: satellite DLL, RC, WebAPI, resx |
| `validate_remote_component` | Валидация RC: manifest.json, Module.mtd, Loaders, PublicName |
| `validate_isolated_areas` | Проверить IsolatedFunctions: контракты, параметры, return types |
| `validate_expression_elements` | Валидация ExpressionElement функций в workflow-блоках |
| `validate_all` | Единая валидация: 7 проверок одним вызовом |
| `lint_async_handlers` | Линтер AsyncHandlers: retry, DelayPeriod, fan-out, параметры |
| `sync_check` | Сравнить локальные исходники с опубликованным модулем на стенде |

### Analysis — анализ и инспекция (14)

| Инструмент | Описание |
|-----------|----------|
| `inspect` | Универсальное чтение метаданных: MTD сущности, модуля, resx, директория |
| `search_metadata` | Поиск по всем MTD: сущности по имени, GUID, типу свойства |
| `extract_entity_schema` | Компактная схема сущности: свойства, enum, навигация, действия |
| `extract_public_structures` | Извлечь PublicStructures из Module.mtd: DTO, JSON-схема, C# interface |
| `dependency_graph` | Граф зависимостей модулей: визуализация, циклы, impact-анализ |
| `analyze_solution` | Аудит решения: health, конфликты, сироты, дубликаты GUID, WebAPI |
| `analyze_code_metrics` | Метрики C# кода: LOC, сложность, anti-patterns, code review |
| `analyze_relationship_graph` | Граф связей между сущностями: NavigationProperty, collections |
| `find_dead_resources` | Поиск мёртвых ресурсов: ключи resx без MTD и наоборот |
| `diff_packages` | Сравнить два .dat пакета: изменения в MTD, resx, коде |
| `compare_db_schema` | Сравнение MTD-свойств с реальной схемой PostgreSQL |
| `map_db_schema` | MTD в имена таблиц и колонок PostgreSQL + CREATE TABLE |
| `predict_odata_name` | Предсказание OData EntitySet и имени таблицы БД |
| `trace_integration_points` | Найти все точки интеграции модуля: OData, WebAPI, AsyncHandlers, Jobs |

### Operations — исправление и рефакторинг (8)

| Инструмент | Описание |
|-----------|----------|
| `fix_package` | Автоисправление .dat: resx-ключи, дубли Code, enum, Constraints |
| `sync_resx_keys` | Добавить недостающие ключи в System.resx из MTD |
| `refactor_entity` | Каскадный рефакторинг: переименование свойств в MTD + resx + C# |
| `modify_workflow` | Изменить RouteScheme: добавить/удалить блок, параллельная ветка |
| `build_dat` | Сборка .dat пакета из директории в ZIP |
| `generate_crud_api` | Сгенерировать C# CRUD endpoints из MTD сущности |
| `generate_test_data` | Генерация SQL INSERT для тестовых данных (PostgreSQL) |
| `deploy_to_stand` | Оркестрация деплоя .dat пакета на стенд |

### Intelligence — подсказки и диагностика (7)

| Инструмент | Описание |
|-----------|----------|
| `suggest_pattern` | Найти паттерн реализации из базы 50+ паттернов |
| `suggest_form_view` | Предложить FormView JSON для многоформенности |
| `preview_card` | Предпросмотр карточки сущности без импорта в DDS |
| `diagnose_build_error` | Диагностика ошибки DDS: pattern-matching по 10 известным ошибкам |
| `trace_errors` | Чтение логов DDS/runtime с фильтрацией |
| `email_integration_check` | Проверить конфигурацию Email/DCS-интеграции |
| `pipeline` | Оркестратор: цепочка инструментов с передачей контекста |

---

## RuntimeTools — 25 инструментов работы со стендом

Работают с живым стендом Directum RX через OData v4 API. Требуют переменных окружения `RX_ODATA_URL`, `RX_USERNAME`.

### Search — поиск данных (5)

| Инструмент | Описание |
|-----------|----------|
| `search` | Поиск по естественному запросу на русском языке |
| `find_docs` | Поиск документов по названию, типу, дате, статусу |
| `find_contracts` | Поиск договоров по контрагенту, сумме, сроку, статусу |
| `discover` | Каталог доступных сущностей: имена, поля, навигация |
| `odata_query` | Произвольные OData GET-запросы: $filter, $select, $expand |

### Analytics — аналитика и отчётность (10)

| Инструмент | Описание |
|-----------|----------|
| `my_tasks` | Мои задания: активные, просроченные, выполненные |
| `pending_approvals` | Документы, ожидающие согласования текущим пользователем |
| `overdue_report` | Отчёт по просрочкам: группировка по исполнителям |
| `deadline_risk` | Предсказание просрочки: риск High/Medium/Low, загруженность |
| `team_workload` | Нагрузка команды: активные задания по исполнителям |
| `bottleneck_detect` | Узкие места в процессах: этапы и исполнители |
| `process_stats` | Статистика маршрутов: среднее время, % просрочки, частота |
| `analyze_bant` | Анализ BANT-распределения лидов/сделок: скоринг |
| `analyze_pipeline_value` | Взвешенная стоимость воронки продаж: прогноз выручки |
| `analyze_sla_rules` | Анализ SLA-правил: сроки, просрочки, режимы, статистика |

### Actions — управление заданиями (6)

| Инструмент | Описание |
|-----------|----------|
| `complete` | Выполнить задание с результатом и комментарием |
| `bulk_complete` | Массовое выполнение заданий с предпросмотром |
| `send_task` | Создать задачу с назначением исполнителя и сроком |
| `delegate` | Переадресовать задание другому сотруднику |
| `route_bulk_action` | Массовая маршрутизация: переадресация/выполнение по фильтру |
| `workflow_escalation` | Эскалация просроченных: руководитель, переадресация |

### Intelligence — интеллектуальные инструменты (4)

| Инструмент | Описание |
|-----------|----------|
| `summarize` | Краткое содержание документа: метаданные, история согласования |
| `auto_classify` | Классификация входящих документов: вид, контрагент |
| `contract_review` | Анализ рисков договора: поля, сроки, суммы, рекомендации |
| `audit_assignment_strategy` | Аудит распределения заданий: баланс, round-robin, перекосы |

---

## Resources — 31 ресурс Knowledge Base

### DevTools: статические ресурсы (24)

| URI | Описание |
|-----|----------|
| `directum://knowledge/platform-rules` | Правила и ограничения DDS 25.3 |
| `directum://knowledge/entity-types` | Типы сущностей: DatabookEntry, Document, Task |
| `directum://knowledge/resx-conventions` | Конвенции именования ключей .resx |
| `directum://knowledge/module-guids` | GUID платформенных модулей и базовых типов |
| `directum://knowledge/csharp-patterns` | Правила C# кода: partial class, namespace, forbidden patterns |
| `directum://knowledge/module-catalog` | Каталог 30 платформенных модулей v25.3 |
| `directum://knowledge/property-types` | Типы свойств, DataBinder, Controls, Forms |
| `directum://knowledge/csharp-functions` | Паттерны функций: [Public], [Remote], ModuleInitializer |
| `directum://knowledge/workflow-patterns` | Workflow: блоки, RouteScheme, Task/Assignment/Notice |
| `directum://knowledge/solution-design` | Проектирование решений: CRM, ESM, HR архитектуры |
| `directum://knowledge/cover-widgets` | Обложки, виджеты, Remote Components |
| `directum://knowledge/initializer-guide` | ModuleInitializer: роли, права, справочники, SQL |
| `directum://knowledge/integration-patterns` | Интеграция: WebAPI, OData, AsyncHandlers, 1С |
| `directum://knowledge/report-patterns` | Отчёты: FastReport .frx, Queries.xml, обработчики |
| `directum://knowledge/entity-catalog` | Полный каталог сущностей платформы с GUID и свойствами |
| `directum://knowledge/solutions-reference` | 30+ паттернов из 4 production-решений |
| `directum://knowledge/dds-known-issues` | 18 известных проблем DDS 25.3 с fix'ами |
| `directum://knowledge/dev-environments` | DDS vs CrossPlatform DS: сравнение сред |
| `directum://knowledge/standalone-setup` | Автономная установка стенда: команды, требования |
| `directum://knowledge/architecture-patterns` | 14 production-паттернов: WebAPI, DTO, SoftDelete |
| `directum://knowledge/ui-catalog` | UI-контролы, RC, библиотеки, FastReport, Aspose |
| `directum://knowledge/crm-patterns` | CRM: Deal, Lead, Pipeline, BANT, Round-robin |
| `directum://knowledge/esm-patterns` | ESM: Email-to-Ticket, SLA, AIAgentTool, ExpressionElement |
| `directum://knowledge/targets-patterns` | Targets/KPI: RemoteTableControl, XLSX, Word, WebAPI |

### DevTools: динамические ресурсы (3)

Генерируются из `SOLUTION_PATH` при каждом запросе:

| URI | Описание |
|-----|----------|
| `directum://solution/modules` | Список всех модулей в текущем решении (base/ + work/) |
| `directum://solution/entities` | Список сущностей в work/ модулях |
| `directum://solution/status` | Статус решения: количество модулей, сущностей, изменения |

### RuntimeTools: ресурсы (4)

| URI | Описание |
|-----|----------|
| `directum-rx://knowledge/odata-schema` | OData API: типы сущностей, свойства, Actions, фильтрация |
| `directum-rx://knowledge/task-workflow` | Задачи через OData: создание, выполнение, делегирование |
| `directum-rx://knowledge/document-operations` | Документы через OData: поиск, версии, подписи, жизненный цикл |
| `directum-rx://knowledge/analytics-patterns` | Аналитика: загрузка, просрочки, bottleneck, KPI |

---

## Prompts — 12 интерактивных сценариев

### DevTools (8)

| Prompt | Описание |
|--------|----------|
| `create-solution` | Создать решение с нуля по описанию на естественном языке |
| `create-entity` | Создать сущность с правильным типом и валидацией |
| `validate-and-fix` | Проверить пакет и исправить ошибки автоматически |
| `debug-import-error` | Разобраться с ошибкой импорта .dat в DDS |
| `review-package` | Code review пакета: архитектура, качество, паттерны |
| `create-task-workflow` | Создать задачу с workflow: Task + Assignment + RouteScheme |
| `diagnose-error` | Диагностика ошибки DDS: pattern-matching + auto-fix |
| `override-entity` | Перекрыть существующую сущность: добавить свойства, действия |

### RuntimeTools (4)

| Prompt | Описание |
|--------|----------|
| `analyze-workload` | Анализ загрузки подразделения: задания, просрочки, bottleneck |
| `investigate-overdue` | Расследование просрочек: причины, ответственные, рекомендации |
| `process-documents` | Найти и обработать документы: поиск, реестр, массовые действия |
| `quick-dashboard` | Быстрый дашборд: ключевые метрики за сегодня/неделю |

---

## Архитектура

```
                                    ┌───────────────────────────────────────────┐
                               ┌───>│  DirectumMcp.DevTools (stdio)             │
                               │    │                                           │
┌──────────────────────┐       │    │  64 инструмента разработчика              │
│                      │       │    │  24 ресурса Knowledge Base + 3 динамич.   │
│  Claude Code         ├───────┘    │  8 промптов                               │
│  Claude Desktop      │            │  (файловая система: .mtd, .resx, .dat)    │
│  VS Code (Copilot)   │            └───────────────────────────────────────────┘
│  Любой MCP-клиент    │
│                      │            ┌───────────────────────────────────────────┐
│                      ├───────────>│  DirectumMcp.RuntimeTools (stdio)         │
│                      │            │                                           │
└──────────────────────┘            │  25 инструментов работы со стендом        │
                                    │  4 ресурса Knowledge Base                 │
                                    │  4 промпта                                │
                                    └───────────┬───────────────────────────────┘
                                                │
                                          OData v4 / HTTP Basic Auth
                                                │
                                    ┌───────────▼───────────────────────────────┐
                                    │  Directum RX                              │
                                    │  IntegrationService (:27002)              │
                                    │  PublicApi (:39700)                        │
                                    └───────────────────────────────────────────┘
```

### Структура проекта

```
directum-mcp-server/
├── DirectumMcp.sln
├── .mcp.json                              — конфигурация MCP для Claude Code
├── README.md
├── CLAUDE.md                              — инструкции для AI-агента
│
├── src/
│   ├── DirectumMcp.Core/                  — общая библиотека
│   │   ├── Services/                      — 9 сервисов (EntityScaffold, PackageValidate, Pipeline...)
│   │   ├── Pipeline/                      — PipelineExecutor + Registry + PlaceholderResolver
│   │   ├── Validators/                    — PackageValidator (14 проверок), PackageWorkspace
│   │   ├── Helpers/                       — DirectumConstants, PathGuard, ODataHelpers
│   │   ├── Models/                        — MtdModels, DirectumConfig
│   │   ├── Parsers/                       — MtdParser, ResxParser
│   │   └── OData/                         — DirectumODataClient
│   │
│   ├── DirectumMcp.DevTools/              — MCP-сервер разработчика (stdio)
│   │   ├── Tools/                         — 64 инструмента
│   │   ├── Resources/                     — PlatformKnowledgeBase (24) + DynamicResources (3)
│   │   └── Prompts/                       — 8 промптов
│   │
│   ├── DirectumMcp.RuntimeTools/          — MCP-сервер рантайма (stdio -> OData)
│   │   ├── Tools/                         — 25 инструментов
│   │   ├── Resources/                     — RuntimeKnowledgeBase (4)
│   │   └── Prompts/                       — 4 промпта
│   │
│   └── DirectumMcp.Tests/                — 619 unit-тестов (xUnit)
```

### Ключевые паттерны

- **Services Layer** — каждый tool делегирует в Service с типизированным Request/Result. Сервисы реализуют `IPipelineStep` для использования в оркестраторе `pipeline`.
- **Pipeline** — `PipelineExecutor` выполняет цепочку шагов последовательно с передачей контекста через `PlaceholderResolver` (`$prev.field`, `$steps[0].field`).
- **Knowledge Base** — 31 ресурс, покрывающий 30 платформенных модулей, 30+ сущностей, 50+ паттернов из 4 production-решений.
- **PathGuard** — ограничение доступа к файловой системе в пределах `SOLUTION_PATH`.

---

## Knowledge Base — что покрыто

### Платформа Directum RX v25.3

- **30 модулей** — полный каталог с GUID, сущностями, зависимостями
- **30+ сущностей** — Employee, Department, Counterparty, Contract, Task и другие с GUID, свойствами, связями
- **Все типы свойств** — String, Int, Double, DateTime, Enum, Navigation, Collection, Binary
- **Типы сущностей** — DatabookEntry, Document, Task, Assignment, Notice — когда какой использовать
- **18 известных проблем DDS** — с причинами и fix'ами

### Паттерны из production-решений

- **CRM** — Deal, Lead, Pipeline, BANT scoring, Round-robin, JSON serialization
- **ESM / Service Desk** — Email-to-Ticket, SLA 4 режима, матричная приоритизация, AIAgentTool
- **Targets / KPI** — RemoteTableControl, Fan-out Async, XLSX Pipeline, Word Processing
- **Agile / Kanban** — WebAPI, Real-time, Remote Components, история изменений

### C# и архитектура

- **Функции** — `[Public]`, `[Remote]`, ModuleInitializer, partial class
- **Workflow** — блоки, RouteScheme, ConditionBlock, ScriptBlock, обработчики
- **Интеграция** — WebAPI, OData, AsyncHandlers, IsolatedFunctions, обмен с 1С
- **14 architecture-паттернов** — WebAPI, DTO, Position, SoftDelete, ExpressionElement

---

## Тестирование

```bash
dotnet test src/DirectumMcp.Tests/     # 619 тестов
dotnet build                           # сборка всего решения
```

619 тестов покрывают:
- Парсинг MTD и resx файлов
- Все 14 проверок валидации пакетов
- OData-клиент и формирование запросов
- Scaffold-генераторы сущностей, модулей, отчётов
- Pipeline-оркестратор и PlaceholderResolver
- Knowledge Base ресурсы

---

## Разработка

### Добавление нового инструмента

1. Создайте файл в `src/DirectumMcp.DevTools/Tools/` или `src/DirectumMcp.RuntimeTools/Tools/`:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class MyNewTool
{
    [McpServerTool(Name = "my_tool")]
    [Description("Описание инструмента на русском")]
    public async Task<string> Execute(
        [Description("Описание параметра")] string param1,
        [Description("Необязательный параметр")] int top = 20)
    {
        // Реализация
        return "## Результат\n\nДанные в формате markdown.";
    }
}
```

2. Инструмент автоматически обнаруживается при запуске (assembly scanning).

3. Для RuntimeTools — используйте DI для получения OData-клиента:

```csharp
public class MyRuntimeTool
{
    private readonly DirectumODataClient _client;
    public MyRuntimeTool(DirectumODataClient client) { _client = client; }

    [McpServerTool(Name = "my_runtime_tool")]
    [Description("Инструмент с доступом к Directum RX")]
    public async Task<string> Execute(...)
    {
        var data = await _client.GetAsync("IEntitySet", filter: "...");
        // ...
    }
}
```

### Соглашения

- C# 12, file-scoped namespaces, nullable enabled
- Один инструмент на файл (тонкий wrapper -> Service в Core)
- `[McpServerTool(Name = "...")]` + `[Description("...")]` — раздельные атрибуты
- Описания инструментов и параметров — на русском
- Код — на английском
- Async/await, markdown output
- Валидация входных параметров через allowlist
- PathGuard для ограничения доступа к файловой системе

---

## Безопасность

- DevTools ограничивают доступ к файлам только в `SOLUTION_PATH` и temp-директории (PathGuard)
- RuntimeTools используют HTTP Basic Auth через OData API Directum RX
- Все права доступа наследуются от учётной записи пользователя Directum RX
- Деструктивные операции (`fix_package`, `bulk_complete`, `sync_resx_keys`, `deploy_to_stand`) требуют явного подтверждения (`dryRun=false` / `confirmed=true`)
- Zip-распаковка защищена от Zip Slip атак
- OData-запросы защищены от инъекций (валидация через allowlist, экранирование)

---

## Технологический стек

| Компонент | Версия | Назначение |
|----------|--------|-----------|
| [.NET 8](https://dotnet.microsoft.com/) | 8.0 | Платформа |
| [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) | 1.1.0 | MCP-протокол |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/) | 8.0 | Парсинг MTD и OData |
| [xUnit](https://xunit.net/) | 2.9 | Тестирование |
| Транспорт: stdio | — | MCP standard |

## Совместимость

| Directum RX | Статус |
|------------|--------|
| 25.3 | Протестировано |
| 24.x | Должно работать (OData API совместим) |
| 23.x | Не тестировалось |

| MCP-клиент | Статус |
|-----------|--------|
| Claude Code | Протестировано |
| Claude Desktop | Совместимо |
| VS Code (Copilot) | Совместимо |
| Любой MCP-совместимый клиент | Совместимо |

---

## Лицензия

MIT License. Для подключения RuntimeTools требуется лицензия Directum RX.

---

## Связанные ресурсы

- [Model Context Protocol](https://modelcontextprotocol.io/) — спецификация протокола
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) — официальный SDK
- [Directum RX](https://www.directum.ru/) — платформа ECM/BPM
- [Claude Code](https://claude.ai/code) — MCP-клиент от Anthropic
