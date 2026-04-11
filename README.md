# Directum MCP Server

Набор MCP-инструментов для разработки на платформе Directum RX 26.1 с помощью AI.

Подключаете к [Claude Code](https://claude.ai/code) — и получаете: создание сущностей, валидацию пакетов, поиск по платформе, сборку .dat, работу с OData.

**86 инструментов, 624 теста, 34 ресурса knowledge base.**

---

## Быстрый старт

### Что нужно

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Claude Code](https://claude.ai/code) (подписка Claude Pro $20/мес или Max)
- Git-репозиторий решения Directum RX (для работы с файлами)

### Установка

```bash
# 1. Клонировать
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server
cd directum-mcp-server

# 2. Собрать
dotnet build

# 3. Проверить (624 теста)
dotnet test src/DirectumMcp.Tests/
```

### Подключение к Claude Code

Скопируйте `.mcp.json` в корень вашего проекта и поправьте `SOLUTION_PATH`:

```json
{
  "mcpServers": {
    "directum-scaffold": {
      "command": "dotnet",
      "args": ["run", "--project", "/путь/к/directum-mcp-server/src/DirectumMcp.Scaffold"],
      "env": { "SOLUTION_PATH": "/путь/к/вашему/решению" }
    },
    "directum-validate": {
      "command": "dotnet",
      "args": ["run", "--project", "/путь/к/directum-mcp-server/src/DirectumMcp.Validate"],
      "env": { "SOLUTION_PATH": "/путь/к/вашему/решению" }
    },
    "directum-analyze": {
      "command": "dotnet",
      "args": ["run", "--project", "/путь/к/directum-mcp-server/src/DirectumMcp.Analyze"],
      "env": { "SOLUTION_PATH": "/путь/к/вашему/решению" }
    },
    "directum-deploy": {
      "command": "dotnet",
      "args": ["run", "--project", "/путь/к/directum-mcp-server/src/DirectumMcp.Deploy"],
      "env": { "SOLUTION_PATH": "/путь/к/вашему/решению" }
    },
    "directum-runtime": {
      "command": "dotnet",
      "args": ["run", "--project", "/путь/к/directum-mcp-server/src/DirectumMcp.Runtime"],
      "env": {
        "RX_ODATA_URL": "http://localhost/Integration/odata",
        "RX_USERNAME": "${RX_USERNAME}",
        "RX_PASSWORD": "${RX_PASSWORD}"
      }
    }
  }
}
```

> **directum-runtime** нужен только если есть запущенный стенд RX. Остальные 4 работают с файлами на диске.

### Проверка

```bash
claude
> Найди сущность Employee через MCP
```

Если вернулось `Employee, NameGuid: b7905516-...` — всё работает.

---

## 5 серверов

| Сервер | Инструментов | Что делает | Когда подключать |
|--------|:---:|-----------|-----------------|
| **directum-scaffold** | 11 | Создание сущностей, модулей, функций, workflow | Пишете новый код |
| **directum-validate** | 17 | Проверки пакета, GUID, resx, автоисправление | Всегда |
| **directum-analyze** | 19 | Поиск по платформе, метрики, зависимости | Исследуете платформу |
| **directum-deploy** | 6 | Сборка .dat, деплой, диагностика ошибок | Публикуете на стенд |
| **directum-runtime** | 33 | OData-запросы, задачи, документы, аналитика | Работаете с живым RX |

Не нужно подключать все сразу. Для начала хватит **scaffold + validate + analyze**.

---

## Инструменты

### Scaffold — создание (11)

| Инструмент | Описание |
|-----------|----------|
| `scaffold_entity` | Создать сущность: справочник, документ, задачу |
| `scaffold_module` | Создать модуль с нуля |
| `scaffold_function` | Серверная/клиентская функция |
| `scaffold_job` | Фоновое задание |
| `scaffold_async_handler` | Асинхронный обработчик |
| `scaffold_task` | Task + Assignment + Notice для workflow |
| `scaffold_webapi` | WebAPI endpoint |
| `scaffold_widget` | Виджет на рабочий стол |
| `scaffold_report` | Отчёт (MTD + FastReport) |
| `scaffold_dialog` | InputDialog с полями |
| `scaffold_cover_action` | Действие на обложку |

### Validate — проверки (17)

| Инструмент | Описание |
|-----------|----------|
| `validate_all` | Все проверки разом |
| `check_package` | 14 проверок пакета за 2 секунды |
| `fix_package` | Автоисправление типовых ошибок |
| `check_code_consistency` | Код соответствует метаданным? |
| `check_resx` | Формат ключей .resx |
| `check_component` | Remote Component валидация |
| `check_initializer` | ModuleInitializer корректен? |
| `validate_workflow` | Маршрут согласования |
| `validate_guid_consistency` | GUID не конфликтуют |
| `validate_expression_elements` | ExpressionElement'ы |
| `validate_report` | Отчёт (MTD + .frx) |
| `validate_remote_component` | RC валидация |
| `validate_deploy` | Готовность к деплою |
| `validate_isolated_areas` | Изолированные области |
| `find_dead_resources` | Неиспользуемые ресурсы |
| `fix_cover_localization` | Исправить локализацию обложки |
| `sync_resx_keys` | Синхронизировать ключи .resx |

### Analyze — поиск и анализ (19)

| Инструмент | Описание |
|-----------|----------|
| `search_metadata` | Найти сущность в 30+ модулях платформы |
| `extract_entity_schema` | Полная схема сущности с GUID и свойствами |
| `analyze_solution` | Обзор решения |
| `analyze_code_metrics` | Метрики кода |
| `analyze_relationship_graph` | Граф связей между сущностями |
| `solution_health` | Дашборд здоровья решения |
| `dependency_graph` | Граф зависимостей модулей |
| `visualize_dependencies` | Интерактивный HTML-граф |
| `predict_odata_name` | Какое OData-имя сгенерирует DDS |
| `preview_card` | Предпросмотр карточки |
| `suggest_form_view` | Рекомендации по форме |
| `suggest_pattern` | Подсказка подходящего паттерна |
| `inspect` | Детальный осмотр .mtd файла |
| `extract_public_structures` | Публичные структуры |
| `lint_async_handlers` | Линтер асинхронных обработчиков |
| `map_db_schema` | Маппинг на БД |
| `compare_db_schema` | Сравнение схемы |
| `check_permissions` | Права доступа |
| `trace_integration_points` | Точки интеграции |

### Deploy — сборка и публикация (6)

| Инструмент | Описание |
|-----------|----------|
| `build_dat` | Собрать .dat-пакет |
| `deploy_to_stand` | Опубликовать на стенд |
| `diagnose_build_error` | Разобрать ошибку сборки DDS |
| `diff_packages` | Сравнить два пакета |
| `trace_errors` | Трассировка ошибок |
| `pipeline` | Конвейер: scaffold → validate → build |

### Runtime — работа со стендом (33)

| Инструмент | Описание |
|-----------|----------|
| `search` | Поиск на естественном языке |
| `odata_query` | Произвольный OData-запрос |
| `my_tasks` | Мои задания |
| `daily_briefing` | Брифинг на сегодня |
| `find_docs` | Поиск документов |
| `find_contracts` | Поиск договоров |
| `create_document` | Создать документ |
| `create_action_item` | Создать поручение |
| `send_task` | Отправить задачу |
| `complete` | Выполнить задание |
| `approve` | Согласовать/отклонить |
| `delegate` | Делегировать |
| `update_entity` | Обновить запись |
| `delete_entity` | Удалить запись |
| `pending_approvals` | Ожидающие согласования |
| `overdue_report` | Просроченные |
| `deadline_risk` | Риски по срокам |
| `team_workload` | Загрузка команды |
| `process_stats` | Статистика процессов |
| `bottleneck_detect` | Узкие места |
| `contract_expiry` | Истекающие договоры |
| `contract_review` | Ревью договора |
| `absences` | Отсутствия сотрудников |
| `analyze_bant` | BANT-анализ |
| `analyze_pipeline_value` | Стоимость воронки |
| `analyze_sla_rules` | SLA-правила |
| `audit_assignment_strategy` | Аудит стратегии назначений |
| `auto_classify` | Автоклассификация |
| `bulk_complete` | Массовое выполнение |
| `route_bulk_action` | Массовые действия по маршруту |
| `workflow_escalation` | Эскалация |
| `summarize` | Саммари сущности |
| `discover` | Обнаружение схемы OData |

---

## Knowledge Base — 34 ресурса

Встроенная база знаний о платформе Directum RX 26.1. AI читает нужный ресурс автоматически перед работой.

- Каталог 30 модулей платформы с GUID
- 30+ сущностей с GUID, свойствами, связями
- Паттерны из 4 production-решений (CRM, ESM, Targets, AgileBoard)
- 18 известных проблем DDS с решениями
- Правила C#, workflow, интеграции, отчётов

---

## Переменные окружения

| Переменная | Серверы | Описание |
|-----------|---------|----------|
| `SOLUTION_PATH` | scaffold, validate, analyze, deploy | Путь к git-репозиторию решения |
| `RX_ODATA_URL` | runtime | URL IntegrationService |
| `RX_USERNAME` | runtime | Имя пользователя |
| `RX_PASSWORD` | runtime | Пароль |

---

## Структура проекта

```
directum-mcp-server/
├── src/
│   ├── DirectumMcp.Core/          — Общие сервисы, парсеры, валидаторы
│   ├── DirectumMcp.Shared/        — Инфраструктура: фильтры, хелперы
│   ├── DirectumMcp.Scaffold/      — Сервер создания (11 tools)
│   ├── DirectumMcp.Validate/      — Сервер валидации (17 tools)
│   ├── DirectumMcp.Analyze/       — Сервер анализа (19 tools)
│   ├── DirectumMcp.Deploy/        — Сервер деплоя (6 tools)
│   ├── DirectumMcp.Runtime/       — Сервер рантайма (33 tools)
│   └── DirectumMcp.Tests/         — 624 теста (xUnit)
├── .mcp.json                      — Конфигурация для Claude Code
├── CLAUDE.md                      — Инструкции для AI
└── README.md
```

---

## Тестирование

```bash
dotnet test src/DirectumMcp.Tests/   # 624 теста, ~1 сек
```

---

## Совместимость

| Directum RX | Статус |
|------------|--------|
| 26.1 | Проверено |
| 25.3 | Должно работать |

| MCP-клиент | Статус |
|-----------|--------|
| Claude Code | Проверено |
| Claude Desktop | Совместимо |
| VS Code (Copilot) | Совместимо |

---

## Лицензия

MIT. Для runtime-инструментов нужна лицензия Directum RX.
