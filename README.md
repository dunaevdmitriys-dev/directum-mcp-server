# Directum MCP Server

**MCP-сервер для платформы Directum RX** -- позволяет AI-ассистентам (Claude, GPT, GigaChat) работать с метаданными решений и данными Directum RX через стандартный протокол [Model Context Protocol](https://modelcontextprotocol.io/).

> Первый MCP-сервер для российской ECM/BPM платформы.

---

## Возможности

### DevTools -- инструменты разработчика (без подключения к стенду)

| Инструмент | Описание |
|-----------|----------|
| `check_package` | Валидация .dat пакета перед импортом в DDS (7 автоматических проверок) |
| `fix_package` | Автоисправление ошибок пакета: resx-ключи, дубли Code, enum, Constraints |
| `inspect` | Универсальное чтение MTD-сущностей, MTD-модулей, resx-файлов, директорий |
| `check_resx` | Проверка формата ключей System.resx (`Property_<Name>` vs `Resource_<GUID>`) |

### RuntimeTools -- работа с данными Directum RX (через OData API)

| Инструмент | Описание |
|-----------|----------|
| `find_docs` | Поиск документов по названию, типу, дате, статусу |
| `my_tasks` | Список заданий текущего пользователя (активные, просроченные) |
| `complete` | Выполнение задания с результатом и комментарием |
| `send_task` | Создание и отправка задачи с назначением исполнителя |
| `summarize` | Краткое содержание документа: метаданные, текст, история согласования |
| `bulk_complete` | Массовое выполнение заданий с preview и подтверждением |

---

## Архитектура

```
                                    ┌──────────────────────────────────────┐
                               ┌───►  DirectumMcp.DevTools (stdio)        │
                               │    │                                      │
┌──────────────────────┐       │    │  check_package  fix_package          │
│                      │       │    │  inspect        check_resx           │
│  Claude Code         ├───────┘    └───────────┬──────────────────────────┘
│  Claude Desktop      │                        │
│  VS Code             │                  Файловая система
│  Любой MCP-клиент    │                  (.mtd, .resx, .dat)
│                      │
│                      │            ┌──────────────────────────────────────┐
│                      ├───────────►  DirectumMcp.RuntimeTools (stdio)     │
└──────────────────────┘            │                                      │
                                    │  find_docs    my_tasks    complete   │
                                    │  send_task    summarize   bulk_complete│
                                    └───────────┬──────────────────────────┘
                                                │
                                          OData v4 / HTTP
                                                │
                                    ┌───────────▼──────────────────────────┐
                                    │  Directum RX                         │
                                    │  IntegrationService (:27002)         │
                                    │  PublicApi (:39700)                  │
                                    └──────────────────────────────────────┘
```

---

## Быстрый старт

### Предварительные требования

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) или новее
- Git-репозиторий решения Directum RX (для DevTools)
- Работающий стенд Directum RX (для RuntimeTools)

### 1. Клонировать и собрать

```bash
git clone https://github.com/your-org/directum-mcp-server.git
cd directum-mcp-server
dotnet build
```

### 2. Подключить к Claude Code

Скопируйте `.mcp.json` в корень вашего проекта:

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

Для Claude Desktop -- добавьте в `%APPDATA%\Claude\claude_desktop_config.json`.

### 3. Использовать

```
> Проверь пакет CRM.dat перед импортом
> Покажи структуру сущности IncomingLetter
> Найди все договоры за март 2026
> Какие у меня просроченные задания?
> Выполни задание 4521 с результатом "Согласовано"
> Массово выполни все задания на ознакомление
```

---

## Примеры использования

### Разработчик: валидация пакета

```
> check_package path/to/CRM.dat

## Результат валидации: CRM.dat

### Проверка 1: CollectionProperty на DatabookEntry — ОШИБКА
Сущность Deal (DatabookEntry) содержит CollectionPropertyMetadata "Contacts".
Это вызовет NullReferenceException при импорте в DDS 25.3.
→ Решение: сменить тип на Document или удалить коллекцию.

### Проверка 6: Формат ключей System.resx — ОШИБКА
DealSystem.ru.resx: 12 ключей формата Resource_<GUID>.
Подписи полей будут пустыми на карточках.
→ Запустите fix_package для автоисправления.

Итого: 2 ошибки, 1 предупреждение, 4 проверки пройдены.
```

### Разработчик: автоисправление

```
> fix_package path/to/CRM.dat dryRun=false

## Результат исправления: CRM.dat

Исправлено автоматически (14):
- 12 ключей Resource_<GUID> → Property_<Name> в DealSystem.ru.resx
- Значение enum "new" → "newValue" в DealStatus
- Дубликат Code "Deal" → "CPDeal" в ContractDeal

Требует ручного исправления (1):
- CollectionProperty "Contacts" на DatabookEntry — нужно изменить тип сущности

Пакет перепакован: CRM_fixed.dat
```

### Разработчик: инспекция сущности

```
> inspect path/to/Deal.mtd

## Сущность: Deal

| Поле             | Значение                         |
|------------------|----------------------------------|
| GUID             | 58cca102-1e97-...                |
| Тип              | Document                         |
| Абстрактный      | нет                              |
| IntegrationService | IDealDocuments                 |

### Свойства (12)
| Свойство     | Тип        | Code       | Обязательное |
|-------------|------------|------------|:------------:|
| Name        | String     | Name       | да           |
| Counterparty| Navigation | CPart      | да           |
| Amount      | Double     | Amount     | нет          |
...
```

### Руководитель: мои задания

```
> my_tasks

У вас 15 заданий. 3 просрочены.

| # | ID   | Тема                              | Автор        | Срок       | Статус |
|---|------|-----------------------------------|-------------|-----------|--------|
| 1 | 4521 | Согласование договора ООО "Вектор" | Петров А.А. | 10.03 !!! | InProcess |
| 2 | 4530 | Рассмотрение служебной записки     | Сидорова Е.В.| 11.03 !!! | InProcess |
| 3 | 4535 | Ознакомление с приказом №45        | Козлов Д.И. | 12.03 !!! | InProcess |
| 4 | 4540 | Подписание акта КС-2              | Иванов М.С. | 13.03     | InProcess |
...

!!! = просрочено
```

### Руководитель: массовое выполнение

```
> bulk_complete taskType=Acquaintance confirmed=false

## Предпросмотр массового выполнения

Найдено заданий: 7
Тип: Acquaintance (ознакомление)

| # | ID   | Тема                     | Срок  |
|---|------|--------------------------|-------|
| 1 | 4535 | Ознакомление с приказом  | 12.03 |
| 2 | 4542 | Ознакомление с регламентом| 14.03 |
...

> Для выполнения вызовите с confirmed=true

> bulk_complete taskType=Acquaintance confirmed=true

Выполнено: 7 | Пропущено: 0 | Ошибок: 0
```

---

## Конфигурация

### Переменные окружения

| Переменная | Сервер | Описание | Обязательна |
|-----------|--------|----------|:-----------:|
| `SOLUTION_PATH` | DevTools | Путь к git-репозиторию решения Directum RX | Да |
| `RX_ODATA_URL` | RuntimeTools | URL IntegrationService (`http://host/Integration/odata`) | Да |
| `RX_USERNAME` | RuntimeTools | Имя пользователя для Basic Auth | Да |
| `RX_PASSWORD` | RuntimeTools | Пароль для Basic Auth | Нет |
| `RX_ALLOW_HTTP` | RuntimeTools | `true` — разрешить HTTP к не-localhost (небезопасно) | Нет |

### Безопасность

- DevTools ограничивают доступ к файлам только в `SOLUTION_PATH` и temp-директории
- RuntimeTools используют HTTP Basic Auth через OData API Directum RX
- Все права доступа наследуются от учётной записи пользователя Directum RX
- Деструктивные операции (`fix_package`, `bulk_complete`) требуют явного подтверждения
- Zip-распаковка защищена от Zip Slip атак
- OData-запросы защищены от инъекций (валидация входных параметров)

---

## Структура проекта

```
directum-mcp-server/
├── DirectumMcp.sln
├── .mcp.json                           — конфигурация MCP для Claude Code
├── README.md
├── CLAUDE.md                           — инструкции для AI-агента
├── PRODUCT_VISION.md                   — продуктовое видение
├── SECURITY_AUDIT.md                   — результаты аудита безопасности
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
│   │   │   └── ODataHelpers.cs         — общие утилиты для работы с OData
│   │   └── Validators/
│   │       └── PackageValidator.cs     — 7 проверок валидации пакета
│   │
│   ├── DirectumMcp.DevTools/           — MCP-сервер разработчика (stdio)
│   │   ├── Program.cs
│   │   └── Tools/
│   │       ├── ValidatePackageTool.cs  — check_package
│   │       ├── FixPackageTool.cs       — fix_package
│   │       ├── InspectTool.cs          — inspect
│   │       └── ValidateResxTool.cs     — check_resx
│   │
│   ├── DirectumMcp.RuntimeTools/       — MCP-сервер рантайма (stdio → OData)
│   │   ├── Program.cs
│   │   └── Tools/
│   │       ├── SearchDocumentsTool.cs  — find_docs
│   │       ├── MyAssignmentsTool.cs    — my_tasks
│   │       ├── CompleteAssignmentTool.cs — complete
│   │       ├── CreateTaskTool.cs       — send_task
│   │       ├── SummarizeTool.cs        — summarize
│   │       └── BulkCompleteTool.cs     — bulk_complete
│   │
│   └── DirectumMcp.Tests/             — unit-тесты (xUnit)
│       ├── MtdParserTests.cs
│       ├── ResxValidatorTests.cs
│       └── ODataClientTests.cs
│
└── .pipeline/                          — артефакты пайплайна разработки
    ├── 01-research/
    ├── 02-design/
    └── 03-plan/
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

3. Для RuntimeTools — используйте DI:

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
- `[McpServerTool(Name = "...")]` + `[Description("...")]` — раздельные атрибуты
- Описания инструментов и параметров — на русском
- Код — на английском
- Async/await, markdown output
- Валидация всех входных параметров
- Graceful error handling (без stack traces пользователю)

---

## Технологический стек

| Компонент | Версия | Назначение |
|----------|--------|-----------|
| [.NET 8](https://dotnet.microsoft.com/) | 8.0 | Платформа |
| [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) | 1.1.0 | MCP-протокол |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/) | 8.0 | Парсинг MTD |
| [xUnit](https://xunit.net/) | 2.9 | Тестирование |
| Транспорт: stdio | | MCP standard |

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
- [x] Q2 2026: fix_package, inspect, summarize, bulk_complete
- [ ] NL-поиск: трансляция естественного языка в OData-фильтры
- [ ] delegate: переадресация заданий
- [ ] team_workload: нагрузка подразделения
- [ ] auto_classify: классификация входящих документов
- [ ] contract_review: анализ рисков в договорах
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
