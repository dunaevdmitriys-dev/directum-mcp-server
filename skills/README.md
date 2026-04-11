# Skills -- 41 команда для разработки Directum RX

Skills -- пошаговые инструкции для типовых задач. Вызываются командой `/имя-skill` в Claude Code.

## Быстрый старт

```bash
# Скопируйте skills в проект
cp -r skills/ /ваш-проект/.claude/skills/
```

## Каталог

### Создание сущностей (10 штук)

| Команда | Описание |
|---------|----------|
| `/create-databook` | Создать новый справочник (DatabookEntry) Directum RX |
| `/create-document` | Создать новый тип документа Directum RX |
| `/create-task` | Создать новый тип задачи Directum RX с заданиями и workflow-схемой |
| `/create-module` | Создать новый модуль Directum RX с нуля |
| `/create-dialog` | Создать InputDialog с полями, валидацией и связями для действия Directum RX |
| `/create-widget` | Создать виджет (Widget) для обложки модуля Directum RX -- MTD, обработчик, ресурсы |
| `/create-report` | Создать отчёт Directum RX -- MTD, FastReport .frx, Queries.xml, обработчики, ресурсы |
| `/create-fastreport` | Создать FastReport-отчёт Directum RX: .mtd + .frx + обработчики + resx + PublicStructures |
| `/override-entity` | Перекрыть существующий тип сущности или модуль Directum RX |
| `/create-initializer` | Создать ModuleInitializer с версионной инициализацией, ролями, правами и справочниками |

### Создание логики и обработчиков (10 штук)

| Команда | Описание |
|---------|----------|
| `/create-handler` | Создать обработчик события сущности Directum RX (BeforeSave, Showing, Created и др.) |
| `/create-workflow` | Создать полный workflow: блоки, хендлеры, RouteScheme для задачи Directum RX |
| `/create-async-handler` | Создать асинхронный обработчик (AsyncHandler) для модуля Directum RX |
| `/create-job` | Создать фоновое задание (Background Job) Directum RX -- MTD, обработчик, расписание |
| `/create-webapi` | Создать WebAPI endpoint Directum RX -- GET/POST, DTO-структуры, ролевая проверка |
| `/create-entity-action` | Добавить действие (кнопку) на карточку/список сущности RX -- Actions в .mtd + C# обработчик |
| `/create-cover-action` | Добавить действие на обложку модуля RX -- CoverEntityListAction, CoverFunctionAction, CoverReportAction |
| `/create-isolated-function` | Создать изолированную функцию (IsolatedArea) -- .NET 8 песочница для сторонних библиотек |
| `/create-odata-query` | Составить OData-запрос к Directum RX Integration Service |
| `/configure-access-rights` | Настройка прав доступа для сущностей Directum RX: роли, Grant на тип/экземпляр, ComputedRoles |

### Проверки и качество (6 штук)

| Команда | Описание |
|---------|----------|
| `/validate-all` | Единая валидация всех артефактов Directum RX через MCP -- запускай после КАЖДОГО изменения .mtd, .resx, .cs |
| `/validate-package` | Валидация пакета разработки Directum RX -- финальная проверка перед импортом |
| `/code-review` | Ревью кода Directum RX -- качество, архитектура, паттерны платформы |
| `/security-audit` | Аудит безопасности кода Directum RX -- запрещённые паттерны, секреты, SQL-инъекции |
| `/dds-guardrails` | Антипаттерны вайбкодинга Directum RX -- типичные ошибки Claude и алгоритм разрыва цикла |
| `/dds-build-errors` | Диагностика ошибок сборки DDS: Missing area, NullReferenceException, file locks, reserved identifiers и др. |

### Проектирование и помощь (4 штуки)

| Команда | Описание |
|---------|----------|
| `/dds-entity-design` | Проектирование сущностей Directum RX: выбор типа, свойства, формы, resx |
| `/unstuck` | Помощник мышления: когда задача непонятная, решение неочевидное, или стандартный подход не работает |
| `/generate-test-data` | Генерация тестовых данных для любых сущностей Directum RX через прямые SQL INSERT в PostgreSQL |
| `/diagnose` | Диагностика стенда Directum RX -- проверка сервисов, логов, решений, ошибок |

### Инфраструктура и деплой (7 штук)

| Команда | Описание |
|---------|----------|
| `/deploy` | Собрать .dat пакет и опубликовать на стенд Directum RX через DeploymentTool |
| `/export-package` | Экспорт .dat пакета из git-репозитория через DeploymentToolCore (dt export-package) |
| `/manage-dat-package` | Полный lifecycle .dat пакета: валидация, исправление, сборка, деплой на стенд Directum RX |
| `/settings-management` | Export/import бизнес-настроек Directum RX (.datx) через DeploymentToolCore |
| `/launcher-service` | Управление сервисами Directum RX через DirectumLauncher: up/down/status/health/logs |
| `/push-all` | Закоммитить и запушить все изменения в репозиторий |
| `/pipeline` | Оркестратор мультиагентной системы: полный цикл разработки от PRD до деплоя и E2E-тестирования |

### Плагины и компоненты (4 штуки)

| Команда | Описание |
|---------|----------|
| `/create-remote-component` | Создать Remote Component (сторонний React-контрол) для веб-клиента Directum RX |
| `/create-rc-plugin` | Создать Python RC-плагин для DirectumLauncher (компонент do.sh) |
| `/create-microservice-component` | Создать DotNetServiceBase микросервис-обёртку для DirectumLauncher |
| `/create-solution-installer` | Создать BaseRnDComponent-based installer для прикладного решения DirectumLauncher |

## Как создать свой Skill

Skill -- markdown-файл `.claude/skills/<имя>/SKILL.md`:

```markdown
---
description: "Что делает"
---
# Название
## Шаги
...
```

Claude Code автоматически находит все `SKILL.md` в `.claude/skills/` и показывает их при вводе `/имя-skill`.

## Установка отдельных skills

Можно скопировать только нужные:

```bash
# Только создание сущностей
cp -r skills/create-databook skills/create-document skills/create-task /ваш-проект/.claude/skills/

# Только проверки
cp -r skills/validate-all skills/validate-package skills/code-review /ваш-проект/.claude/skills/

# Только деплой
cp -r skills/deploy skills/export-package skills/manage-dat-package /ваш-проект/.claude/skills/
```
