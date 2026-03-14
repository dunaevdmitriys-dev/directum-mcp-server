# Directum MCP Server

**MCP-сервер для платформы Directum RX** — позволяет AI-ассистентам (Claude, GPT, GigaChat) работать с метаданными решений и данными Directum RX через стандартный протокол [Model Context Protocol](https://modelcontextprotocol.io/).

> Первый MCP-сервер для российской ECM/BPM платформы.

---

## Обзор

Directum MCP Server предоставляет AI-ассистентам два набора инструментов:

- **DevTools** — инструменты разработчика для работы с метаданными (.mtd, .resx, .dat), не требуют подключения к стенду
- **RuntimeTools** — инструменты для работы с живым стендом Directum RX через OData API

Всего **25 инструментов**: 18 для разработки и 7 для операций на стенде.

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

## Архитектура

```
                                    ┌──────────────────────────────────────────┐
                               ┌───►  DirectumMcp.DevTools (stdio)             │
                               │    │                                          │
┌──────────────────────┐       │    │  18 инструментов разработчика            │
│                      │       │    │  (файловая система — .mtd, .resx, .dat)  │
│  Claude Code         ├───────┘    └──────────────────────────────────────────┘
│  Claude Desktop      │
│  VS Code             │            ┌──────────────────────────────────────────┐
│  Любой MCP-клиент    ├───────────►  DirectumMcp.RuntimeTools (stdio)         │
│                      │            │                                          │
└──────────────────────┘            │  7 инструментов работы со стендом        │
                                    └───────────┬──────────────────────────────┘
                                                │
                                          OData v4 / HTTP
                                                │
                                    ┌───────────▼──────────────────────────────┐
                                    │  Directum RX                             │
                                    │  IntegrationService (:27002)             │
                                    │  PublicApi (:39700)                      │
                                    └──────────────────────────────────────────┘
```

Оба сервера используют **stdio транспорт** (стандарт MCP). Общая библиотека `DirectumMcp.Core` содержит OData-клиент, парсеры MTD/resx и вспомогательные утилиты.

---

## DevTools — инструменты разработки (18)

Работают с файловой системой: читают и анализируют `.mtd`, `.resx`, `.dat`, `.cs`, `.frx` файлы. Не требуют запущенного стенда.

### Анализ и инспекция

| Инструмент | Описание | Ключевые параметры |
|-----------|----------|--------------------|
| `inspect` | Универсальный инструмент чтения метаданных Directum RX — MTD сущности, MTD модуля, resx, директория модуля | `path` — путь к файлу или директории |
| `search_metadata` | Поиск по всем MTD-файлам репозитория: сущности по имени, GUID, типу свойства, ссылке EntityGuid | `query`, `scope`, `filterType` |
| `dependency_graph` | Строит и анализирует граф зависимостей модулей. Режимы: `graph` — полная карта, `cycles` — поиск циклов, `impact` — анализ влияния | `solutionPath`, `action`, `moduleGuid` |
| `extract_entity_schema` | Извлекает компактную семантическую схему сущности из .mtd: свойства, перечисления, навигационные ссылки, коллекции, действия, группы вложений | `path`, `format` (markdown/json-schema), `includeInherited` |
| `find_dead_resources` | Поиск мёртвых ресурсов в модуле: ключи System.resx без свойства/действия в MTD, свойства MTD без перевода, ResourcesKeys без ключей в Entity.resx | `modulePath` |
| `diff_packages` | Сравнение двух пакетов (.dat или директорий): различия в метаданных, ресурсах и коде | `pathA`, `pathB`, `scope` (all/metadata/resources/code) |

### Валидация

| Инструмент | Описание | Ключевые параметры |
|-----------|----------|--------------------|
| `check_package` | Валидация .dat пакета перед импортом в DDS — 7 проверок: CollectionProperty в DatabookEntry, кросс-модульные ссылки, зарезервированные слова C#, дублирование Code, согласованность AttachmentGroup, формат ключей System.resx, наличие Analyzers | `packagePath` |
| `check_resx` | Проверка формата ключей System.resx файлов: обнаруживает ключи `Resource_<GUID>` и предлагает правильные имена на основе MTD | `directoryPath` |
| `check_code_consistency` | Проверка согласованности между MTD-метаданными и C#-кодом: функции, классы, пространства имён, инициализатор модуля | `packagePath` |
| `check_component` | Валидация проекта Remote Component (стороннего контрола): структура, manifest, loaders, зависимости, билд, i18n | `componentPath` |
| `validate_workflow` | Валидация маршрутных схем (RouteScheme) в .mtd файлах задач: мёртвые блоки, ConditionBlock без условий, обработчики без кода, тупики | `path`, `severity` |
| `validate_report` | Валидация отчётов Directum RX: проверка связки FastReport-шаблона (.frx) и Queries.xml — датасеты, хардкоженные строки подключения, неиспользуемые запросы | `path` |

### Генерация и исправление

| Инструмент | Описание | Ключевые параметры |
|-----------|----------|--------------------|
| `fix_package` | Автоисправление ошибок .dat пакета: resx-ключи (`Resource_<GUID>` → `Property_<Name>`), дубли Code, зарезервированные enum-значения, Constraints. По умолчанию `dryRun=true` | `packagePath`, `dryRun` |
| `sync_resx_keys` | Сканирует .mtd файлы сущностей, извлекает свойства и действия, добавляет недостающие ключи в `*System.resx` и `*System.ru.resx` по конвенциям платформы | `packagePath`, `dryRun` |
| `scaffold_entity` | Генерация скелета новой сущности или override существующей: MTD-метаданные, resx-ресурсы, серверные/клиентские функции. Поддерживает режим `job` для фоновых заданий | `outputPath`, `entityName`, `moduleName`, `baseType`, `mode`, `properties` |
| `scaffold_component` | Создание нового Remote Component: генерация полной структуры проекта с webpack, manifest, loaders, i18n и заготовками контролов | `outputPath`, `vendorName`, `componentName`, `controls` |
| `build_dat` | Сборка .dat пакета из директории (source/ + settings/ + PackageInfo.xml в ZIP). Читает версию из Module.mtd | `packagePath`, `outputPath`, `version` |

### Диагностика

| Инструмент | Описание | Ключевые параметры |
|-----------|----------|--------------------|
| `trace_errors` | Читает и фильтрует лог-файлы Directum RX (логи сборки DDS, логи runtime-сервисов), возвращает последние ошибки с контекстом | `logsPath`, `level`, `lastMinutes`, `keyword`, `maxEntries` |

---

## RuntimeTools — инструменты стенда (7)

Работают с живым стендом Directum RX через OData v4 API. Требуют переменных окружения `RX_ODATA_URL`, `RX_USERNAME`.

| Инструмент | Описание | Ключевые параметры |
|-----------|----------|--------------------|
| `find_docs` | Поиск документов в Directum RX по названию, типу, дате, статусу | `query`, `documentType`, `dateFrom`, `dateTo`, `status`, `top` |
| `my_tasks` | Мои задания в Directum RX — активные, просроченные, выполненные. Отмечает просроченные `!!!` | `status` (InProcess/Completed/Aborted), `top` |
| `complete` | Выполнить задание с результатом и комментарием | `assignmentId`, `result`, `activeText` |
| `send_task` | Создать простую задачу с назначением исполнителя (поиск по имени) и сроком. Поддерживает `autoStart` | `subject`, `assigneeName`, `deadline`, `description`, `importance`, `autoStart` |
| `summarize` | Краткое содержание документа: метаданные, статус, история согласования. Поиск по ID или ключевому слову | `documentId`, `query` |
| `bulk_complete` | Массовое выполнение заданий с предпросмотром. По умолчанию `confirmed=false` (только показ списка) | `taskType` (Acquaintance/Approval/All), `result`, `comment`, `limit`, `confirmed` |
| `odata_query` | Выполнение произвольных OData GET-запросов к Directum RX. Режимы: `query`, `recent`, `by_id`, `count`. Форматы: `table` или `json` | `entity`, `filter`, `select`, `expand`, `top`, `skip`, `orderby`, `mode`, `format` |

---

## Примеры использования

### Разработчик: валидация пакета перед импортом

```
> check_package path/to/CRM.dat

## Результат валидации пакета

Пакет: CRM.dat | MTD файлов: 8 | System.resx файлов: 12

Итого: 2 проверок пройдено, 2 проблемы найдены

## 1. [FAIL] CollectionPropertyMetadata в DatabookEntry
  - Deal в Deal.mtd — DatabookEntry с CollectionPropertyMetadata
Рекомендация: Удалите CollectionPropertyMetadata или смените базовый тип на Document.

## 6. [FAIL] Формат ключей System.resx (Resource_<GUID> → Property_<Name>)
  - DealSystem.ru.resx: ключ Resource_<GUID> (значение: "Name") — должен быть Property_Name
Рекомендация: Запустите fix_package для автоисправления.
```

### Разработчик: автоисправление

```
> fix_package path/to/CRM.dat dryRun=false

## Результат исправления пакета CRM.dat

## Исправлено автоматически (14)
| # | Проверка | Файл | Было | Стало |
| 1 | Check6 | DealSystem.ru.resx | ключ Resource_<GUID> | Property_Name |
...

## Требует ручного исправления (1)
| 1 | Check1 | Deal.mtd | DatabookEntry с CollectionPropertyMetadata |
```

### Разработчик: инспекция сущности

```
> inspect path/to/Deal.mtd

## Сущность: Deal
| GUID | 58cca102-... |
| Тип | Document |

### Свойства (12)
| Свойство | Тип | Code | Обязательное |
| Name | String | Name | да |
| Counterparty | Navigation | CPart | да |
| Amount | Double | Amount | нет |
```

### Разработчик: поиск по всем метаданным

```
> search_metadata query=Counterparty filterType=Document

## Результаты поиска: "Counterparty"
Найдено совпадений: 5

| Имя | Тип | Совпадение | Путь |
| Contract | Document | Property.Name: Counterparty | work/Contracts/Contract.mtd |
| Deal | Document | Property.Name: Counterparty | work/CRM/Deal.mtd |
```

### Руководитель: мои задания и массовое выполнение

```
> my_tasks

Задания (InProcess): 15
ПРОСРОЧЕНО: 3 из 15 заданий (отмечены !!!)

| | ID | Тема | Автор | Срок |
| !!! | 4521 | Согласование договора ООО "Вектор" | Петров А.А. | 10.03 |

> bulk_complete taskType=Acquaintance confirmed=false
## Предпросмотр: 7 заданий на ознакомление
> Для выполнения вызовите с confirmed=true

> bulk_complete taskType=Acquaintance confirmed=true
Выполнено: 7 | Пропущено: 0 | Ошибок: 0
```

### Аналитик: произвольный OData-запрос

```
> odata_query entity=IOfficialDocuments filter="LifeCycleState eq 'Active'" select=Id,Name,Created orderby="Created desc" top=10

Результаты запроса IOfficialDocuments: 10
| Id | Name | Created |
| 1042 | Договор №15 | 2026-03-10 |
...
```

---

## Разработка

### Сборка и тесты

```bash
dotnet build                           # сборка всего решения
dotnet test src/DirectumMcp.Tests/     # запуск тестов
```

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
- Один инструмент на файл
- `[McpServerTool(Name = "...")]` + `[Description("...")]` — раздельные атрибуты (не Description внутри McpServerTool)
- Описания инструментов и параметров — на русском
- Код — на английском
- Async/await, markdown output
- Валидация всех входных параметров через allowlist
- Graceful error handling (без stack traces пользователю)
- PathGuard для ограничения доступа к файловой системе

### Структура проекта

```
directum-mcp-server/
├── DirectumMcp.sln
├── .mcp.json                           — конфигурация MCP для Claude Code
├── README.md
├── CLAUDE.md                           — инструкции для AI-агента
│
├── src/
│   ├── DirectumMcp.Core/               — общая библиотека
│   │   ├── Models/
│   │   │   ├── MtdModels.cs            — типизированные модели MTD-метаданных
│   │   │   └── DirectumConfig.cs       — конфигурация подключения к RX
│   │   ├── Parsers/
│   │   │   ├── MtdParser.cs            — парсинг .mtd (JSON)
│   │   │   └── ResxParser.cs           — парсинг и валидация .resx (XML)
│   │   ├── OData/
│   │   │   └── DirectumODataClient.cs  — HTTP-клиент для OData v4 API
│   │   ├── Helpers/
│   │   │   ├── ODataHelpers.cs         — общие утилиты для работы с OData
│   │   │   └── PathGuard.cs            — ограничение доступа к файловой системе
│   │   └── Validators/
│   │       └── PackageValidator.cs     — 7 проверок валидации пакета
│   │
│   ├── DirectumMcp.DevTools/           — MCP-сервер разработчика (stdio)
│   │   ├── Program.cs
│   │   └── Tools/                      — 18 инструментов
│   │
│   ├── DirectumMcp.RuntimeTools/       — MCP-сервер рантайма (stdio → OData)
│   │   ├── Program.cs
│   │   └── Tools/                      — 7 инструментов
│   │
│   └── DirectumMcp.Tests/             — unit-тесты (xUnit)
│       ├── MtdParserTests.cs
│       ├── ResxValidatorTests.cs
│       └── ODataClientTests.cs
```

---

## Безопасность

- DevTools ограничивают доступ к файлам только в `SOLUTION_PATH` и temp-директории (PathGuard)
- RuntimeTools используют HTTP Basic Auth через OData API Directum RX
- Все права доступа наследуются от учётной записи пользователя Directum RX
- Деструктивные операции (`fix_package`, `bulk_complete`, `sync_resx_keys`) требуют явного подтверждения (`dryRun=false` / `confirmed=true`)
- Zip-распаковка защищена от Zip Slip атак
- OData-запросы защищены от инъекций (валидация входных параметров через allowlist, экранирование)

---

## Технологический стек

| Компонент | Версия | Назначение |
|----------|--------|-----------|
| [.NET 8](https://dotnet.microsoft.com/) | 8.0 | Платформа |
| [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) | 1.1.0 | MCP-протокол |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/) | 8.0 | Парсинг MTD и OData |
| [xUnit](https://xunit.net/) | 2.9 | Тестирование |
| Транспорт: stdio | — | MCP standard |

---

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

## Дорожная карта

- [x] MVP: 7 базовых инструментов (check_package, inspect, find_docs, my_tasks, complete, send_task, check_resx)
- [x] fix_package, summarize, bulk_complete, dependency_graph, search_metadata, find_dead_resources, diff_packages, check_code_consistency
- [x] scaffold_entity, scaffold_component, build_dat, sync_resx_keys, trace_errors
- [x] check_component, validate_workflow, validate_report, extract_entity_schema, odata_query
- [ ] NL-поиск: трансляция естественного языка в OData-фильтры
- [ ] `delegate`: переадресация заданий
- [ ] `team_workload`: нагрузка подразделения
- [ ] `auto_classify`: классификация входящих документов
- [ ] `contract_review`: анализ рисков в договорах
- [ ] HTTP-транспорт (Streamable HTTP) для удалённого доступа
- [ ] Telegram-бот на базе MCP
- [ ] Поддержка GigaChat / YandexGPT

---

## Лицензия

MIT License. Для подключения RuntimeTools требуется лицензия Directum RX.

---

## Связанные ресурсы

- [Model Context Protocol](https://modelcontextprotocol.io/) — спецификация протокола
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) — официальный SDK
- [Directum RX](https://www.directum.ru/) — платформа ECM/BPM
- [Claude Code](https://claude.ai/code) — MCP-клиент от Anthropic
