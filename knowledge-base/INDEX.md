# Knowledge Base: Разработка решений на Sungero / DirectumRX

> База знаний для AI-агентов, строящих прикладные решения на платформе Sungero.

---

## Навигация по гайдам

### Раздел 0 — Базовая архитектура

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 1 | [01_architecture.md](guides/01_architecture.md) | Архитектура платформы | Модули, сервисы, решение, тенанты |
| 2 | [02_entities.md](guides/02_entities.md) | Сущности | Create, Delete, Save, GetAll, State, Properties, Validation |
| 3 | [03_server_client_shared.md](guides/03_server_client_shared.md) | Уровни кода | Server, Client, Shared, Remote, Initializing |
| 4 | [04_workflow.md](guides/04_workflow.md) | Workflow и процессы | Task, Assignment, Scheme, ScriptBlock, ProcessKind |
| 5 | [05_documents.md](guides/05_documents.md) | Документы | ElectronicDocument, Versions, Signatures, PDF, Relations |
| 6 | [06_access_rights.md](guides/06_access_rights.md) | Права доступа | AccessRights, AllowRead, ComputedRoles, Users, Groups |
| 7 | [07_overrides.md](guides/07_overrides.md) | Перекрытия | Override patterns, GetRolePerformers, rx-examples |
| 8 | [08_api_reference.md](guides/08_api_reference.md) | API Reference | Domain, Workflow, NoCode, Cache, Mail, Calendar |

### Раздел 1 — Критические (создание решения с нуля)

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 9 | [09_getting_started.md](guides/09_getting_started.md) | Создание решения с нуля | DDS IDE, решение, модуль, свойства, действия, публикация |
| 10 | [10_dialogs_ui.md](guides/10_dialogs_ui.md) | Диалоги и UI | ShowMessage, InputDialog, контролы, события формы |
| 11 | [11_events_lifecycle.md](guides/11_events_lifecycle.md) | События и жизненный цикл | BeforeSave, Saving, Saved, Created, Filtering, Params |
| 12 | [12_structures_constants_resources.md](guides/12_structures_constants_resources.md) | Структуры, константы, ресурсы | Create(), [Public], Constants, Resources, Enumerations |

### Раздел 2 — Отчёты, фон, БД

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 13 | [13_reports.md](guides/13_reports.md) | Разработка отчётов | Параметры, источники данных, макет, бэнды, Export |
| 14 | [14_background_async.md](guides/14_background_async.md) | Фоновые процессы и async | Jobs, AsyncHandlers, ExecuteAsync, Transactions, Retry |
| 15 | [15_sql_locking_database.md](guides/15_sql_locking_database.md) | SQL, блокировки, БД | SQL class, Locks, TryLock, BulkCopy, индексы, пакеты |
| 16 | [16_performance.md](guides/16_performance.md) | Быстродействие и ограничения | 3 сек, IQueryable, Cache, Params, разрешённые/запрещённые классы |

### Раздел 3 — Продвинутые возможности

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 17 | [17_nocode_features.md](guides/17_nocode_features.md) | NoCode-настройка | Базовые блоки, варианты процессов, вычисляемые роли, диалоги |
| 18 | [18_integrations_isolated.md](guides/18_integrations_isolated.md) | Интеграции и IsolatedArea | Изолированные области, Hyperlinks, Stream, сторонние библиотеки |
| 19 | [19_cookbook.md](guides/19_cookbook.md) | Рецепты и паттерны | 18 готовых решений, антипаттерны |
| 20 | [20_widgets_covers.md](guides/20_widgets_covers.md) | Виджеты, обложки, навигация | Widget actions/charts, параметры, наборы, Cover, Omni |
| 21 | [21_state_view.md](guides/21_state_view.md) | StateView | Контрол состояния, StateBlock, гиперссылки, этапы процесса |

### Раздел 4 — Справочники платформы (из archive/base/)

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 22 | [22_base_guids.md](guides/22_base_guids.md) | BaseGuid справочник | GUID базовых типов, $type метаданных |
| 23 | [23_mtd_reference.md](guides/23_mtd_reference.md) | MTD справочник | .mtd JSON шаблоны, Forms, Controls, Actions, Ribbon, HandledEvents |
| 24 | [24_platform_modules.md](guides/24_platform_modules.md) | Каталог модулей | 29 модулей, зависимости, сущности, рекомендации наследования |
| 25 | [25_code_patterns.md](guides/25_code_patterns.md) | Паттерны C# кода | Using, Handlers, AsyncHandlers, Jobs, Initializer, Actions, Filtering |

### Раздел 5 — Remote Components (фронтенд)

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 26 | [26_remote_components.md](guides/26_remote_components.md) | Remote Components | Webpack Module Federation, React, IRemoteComponentCardApi, Loader, Manifest |

### Раздел 6 — Специализированные гайды

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 27 | [27_dds_vs_crossplatform_ds.md](guides/27_dds_vs_crossplatform_ds.md) | DDS vs CrossPlatform DS | Сравнение IDE, Electron, WPF, HTTP API бекенда, VS Code Extension |
| 28 | [28_windows_autonomous_setup.md](guides/28_windows_autonomous_setup.md) | Windows-стенд для Claude Code | Автономная разработка, DirectumLauncher, сборка .dat, публикация |
| 29 | [29_solution_patterns.md](guides/29_solution_patterns.md) | Production Solution Patterns | WebAPI, AsyncHandler, Versioned Init, DTO, Position Collections, Soft Delete |
| 30 | [30_ui_libraries_reports.md](guides/30_ui_libraries_reports.md) | UI, библиотеки, отчёты | Remote Components каталог, FastReport .frx, Aspose, InputDialog паттерны |
| 31 | [31_cover_localization_fix.md](guides/31_cover_localization_fix.md) | Обложки: заголовки через БД | sungero_nocode_moduleview, sungero_settingslayer_localization, _Title_ ключи |

### Раздел 7 — Plugin Development & Packaging

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 32 | [32_rc_plugin_development.md](guides/32_rc_plugin_development.md) | RC Plugin Development | DirectumLauncher plugins, Python @action, Google Fire CLI, plugin lifecycle |
| 33 | [33_microservice_deployment.md](guides/33_microservice_deployment.md) | Microservice Deployment | Docker-контейнер, отдельный процесс, API, health check, docker-compose |
| 34 | [34_applied_solution_packaging.md](guides/34_applied_solution_packaging.md) | Applied Solution Packaging | .dat пакет, PackageInfo.xml, DeploymentTool, инсталлятор, версионирование |

### Раздел 8 — Инструменты платформы (Deep Internals)

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| 35 | [35_deployment_tool_internals.md](guides/35_deployment_tool_internals.md) | DeploymentToolCore Internals | CLI flags, exit-codes, Docker, export-package, distributed deploy, config |
| 36 | [36_launcher_internals.md](guides/36_launcher_internals.md) | DirectumLauncher Internals | Python CLI, 15 plugins, @action, config.yml, service lifecycle, Docker |
| 37 | [37_development_studio_internals.md](guides/37_development_studio_internals.md) | DevelopmentStudio Internals | Electron+.NET, code gen, _ConfigSettings.xml, VS Code Extension |
| 38 | [38_platform_integration_map.md](guides/38_platform_integration_map.md) | Карта интеграций платформы | DDS→Launcher→DTC→Platform, config flow, data flow, порты, зависимости |

### Комплексный справочник (production-решения)

| № | Файл | Тема | Ключевые понятия |
|---|------|------|-----------------|
| — | [solutions-reference.md](guides/solutions-reference.md) | Production reference | AgileBoard, Targets, ESM, CRM — полные паттерны из 4 решений |

---

## Карта понятий → гайды

### Создание решения / проекта
→ [09_getting_started.md](guides/09_getting_started.md)
- DDS IDE, создание решения и модуля, свойства, действия, публикация, отладка

### Создание/удаление/изменение объектов
→ [02_entities.md](guides/02_entities.md)
- `Sungero.Parties.People.Create()`, `entity.Save()`, `entity.Reload()`

### Получение данных из БД
→ [02_entities.md § Получение сущностей](guides/02_entities.md)
- `GetAll(filter)`, `Get(id)`, `GetAllCached(filter)`

### Валидация полей
→ [02_entities.md § Валидация](guides/02_entities.md)
- `e.AddError(property, message)`, `e.AddWarning()`, `e.AddInformation()`

### Управление полями формы
→ [02_entities.md § State.Properties](guides/02_entities.md)
→ [10_dialogs_ui.md § Управление формой](guides/10_dialogs_ui.md)
- `entity.State.Properties.Field.IsVisible`, `.IsEnabled`, `.IsRequired`

### События сущностей
→ [11_events_lifecycle.md](guides/11_events_lifecycle.md)
- Серверные: BeforeSave → Saving → Saved → AfterSave
- Клиентские: Refresh, Showing, Closing
- Shared: PropertyChanged, PropertyFiltering

### Серверный vs клиентский код
→ [03_server_client_shared.md](guides/03_server_client_shared.md)
- Server: БД, LINQ, email · Client: диалоги, формы · Shared: вычисления

### Диалоги и UI
→ [10_dialogs_ui.md](guides/10_dialogs_ui.md)
- `ShowMessage`, `CreateInputDialog`, все контролы, каскадные списки

### Структуры, константы, ресурсы
→ [12_structures_constants_resources.md](guides/12_structures_constants_resources.md)
- `Structure.Create()`, `Constants.Module`, `Resources.Message`

### Задачи и задания
→ [04_workflow.md](guides/04_workflow.md)
- `SimpleTasks.Create()`, `task.Start()`, `ScriptSchemeBlocks`

### Бизнес-процессы (NoCode)
→ [17_nocode_features.md](guides/17_nocode_features.md)
- Базовые блоки, варианты процессов, вычисляемые роли

### Документы и версии
→ [05_documents.md](guides/05_documents.md)
- Версии, подписи, шаблоны, PDF, связи

### Права доступа
→ [06_access_rights.md](guides/06_access_rights.md)
- `AccessRights.AllowRead()`, `Grant()`, `CanUpdate()`

### Перекрытия
→ [07_overrides.md](guides/07_overrides.md)
- `override`, `[Public]`, `[Remote]`

### Отчёты
→ [13_reports.md](guides/13_reports.md)
- Параметры, источники данных, макет, `Export()`, `Open()`

### Фоновые процессы и async
→ [14_background_async.md](guides/14_background_async.md)
- `Jobs.Enqueue()`, `AsyncHandlers.Create()`, `ExecuteAsync()`, `Transactions`

### SQL и блокировки
→ [15_sql_locking_database.md](guides/15_sql_locking_database.md)
- `SQL.CreateConnection()`, `Locks.TryLock()`, `BulkCopy`, индексы

### Быстродействие
→ [16_performance.md](guides/16_performance.md)
- 3 сек таргет, `IQueryable`, `Cache`, запрещённые классы .NET

### Интеграции
→ [18_integrations_isolated.md](guides/18_integrations_isolated.md)
- `IsolatedFunctions`, `Stream`, `Hyperlinks.Get()`

### Виджеты и обложки
→ [20_widgets_covers.md](guides/20_widgets_covers.md)
- Действия, диаграммы, параметры, наборы виджетов, Cover

### Визуальное состояние (StateView)
→ [21_state_view.md](guides/21_state_view.md)
- `StateView.Create()`, `AddBlock()`, `AddHyperlink()`

### Готовые рецепты
→ [19_cookbook.md](guides/19_cookbook.md)
- 18 типовых паттернов + антипаттерны

### API-классы
→ [08_api_reference.md](guides/08_api_reference.md)
- `AccessRights`, `Cache`, `Mail`, `Calendar`, `Logger`, `SchemeBlocks`

### BaseGuid и типы метаданных
→ [22_base_guids.md](guides/22_base_guids.md)
- GUID базовых типов (DatabookEntry, OfficialDocument, Task, Assignment, Notice, Report)

### Формат .mtd файлов
→ [23_mtd_reference.md](guides/23_mtd_reference.md)
- JSON шаблоны для Databook, Document, Task, Assignment, Notice, Report
- Forms, Controls, Actions, Ribbon, HandledEvents, PreviousPropertyGuid

### Модули платформы
→ [24_platform_modules.md](guides/24_platform_modules.md)
- 29 модулей, зависимости, сущности, рекомендации от какого модуля наследовать

### Паттерны кода C#
→ [25_code_patterns.md](guides/25_code_patterns.md)
- Using statements, Handlers, AsyncHandlers, Jobs, Initializer, Actions, Filtering, антипаттерны

### Remote Components (сторонние React-компоненты)
→ [26_remote_components.md](guides/26_remote_components.md)
- Webpack Module Federation, React 18, IRemoteComponentCardApi, IRemoteComponentCoverApi
- Loader, Control, Manifest, standalone/remote отладка, i18next

### DDS vs CrossPlatform DS
→ [27_dds_vs_crossplatform_ds.md](guides/27_dds_vs_crossplatform_ds.md)
- Сравнение IDE, архитектура, установка, режимы работы, автоматизация

### Windows-стенд для автономной работы
→ [28_windows_autonomous_setup.md](guides/28_windows_autonomous_setup.md)
- DirectumLauncher, сборка .dat, публикация, цикл разработки Claude Code

### Production Solution Patterns
→ [29_solution_patterns.md](guides/29_solution_patterns.md)
- WebAPI, AsyncHandler, Versioned Init, DTO, Position Collections, Soft Delete

### UI, библиотеки, отчёты
→ [30_ui_libraries_reports.md](guides/30_ui_libraries_reports.md)
- Remote Components каталог, FastReport .frx, Aspose, InputDialog, виджеты

### Обложки модулей: заголовки через БД
→ [31_cover_localization_fix.md](guides/31_cover_localization_fix.md)
- sungero_nocode_moduleview, sungero_settingslayer_localization, _Title_ ключи

### Production reference (4 решения)
→ [solutions-reference.md](guides/solutions-reference.md)
- AgileBoard, Targets, ESM, CRM — полные паттерны из production

### Реальная кодовая база (archive/base/)
- `archive/base/` — 29 модулей, 521 сущность, 11800+ .mtd файлов
- Использовать для сверки формата .mtd, паттернов кода, BaseGuid

---

## Модули платформы

| Префикс | Namespace | Описание | Страниц |
|---------|-----------|----------|---------|
| `om` | — | Object Model (документация API) | 556 |
| `sds` | — | Development Studio (IDE) | 332 |
| `func` | — | Function References по модулям | 426 |
| `sungero` | `Sungero.*` | Все модули платформы | 414 |
| `admin` | — | Администрирование системы | 1694 |
| `nc` | `Sungero.NoCode` | NoCode настройки | 174 |
| `hr` | — | Управление персоналом | 1848 |
| `rm` | `Sungero.RecordManagement` | Делопроизводство | 391 |
| `doc` | `Sungero.Docflow` | Документооборот | 367 |
| `ario` | `Sungero.SmartProcessing` | Умная обработка/распознавание | 286 |
| `1c` | — | Интеграция с 1С | 239 |
| `blok` | — | Блоки схем бизнес-процессов | 143 |
| `ds` | — | Data System | 332 |
| `esm` | — | Enterprise Service Management | 206 |
| `monitoring` | — | Мониторинг сервисов | 195 |

---

## XML API-документация (24 файла)

| Сборка | Путь | Содержит |
|--------|------|---------|
| `Sungero.Domain` | Libraries/Kernel/Sungero.Domain.xml | AccessRights, Cache, Mail, BackgroundJobs, Calendar |
| `Sungero.Domain.Shared` | Libraries/Kernel/Sungero.Domain.Shared.xml | Базовые интерфейсы |
| `Sungero.Workflow.Server` | Libraries/Workflow/Sungero.Workflow.Server.xml | AssignmentBlock, TaskBlock, SchemeBlocks |
| `Sungero.Workflow.Blocks` | Libraries/Workflow/Sungero.Workflow.Blocks.xml | Блоки схем |
| `Sungero.NoCode.Server` | Libraries/NoCode/Sungero.NoCode.Server.xml | ComputedRole, CustomDialog |
| `Sungero.CoreEntities.Server` | Libraries/CoreEntities/Sungero.CoreEntities.Server.xml | EntitySecureLinks, HistoryExtensions |
| `CommonLibrary` | Libraries/Common/CommonLibrary.xml | LocalizedString |
| `Sungero.Dialogs` | Libraries/Common/Sungero.Dialogs.xml | Диалоги |
| `Sungero.Localization` | Libraries/Common/Sungero.Localization.xml | Локализация |
| `Sungero.Reporting.Server` | Libraries/Report/Sungero.Reporting.Server.xml | Отчёты |

---

## Архив DDS (реальные примеры)

| Файл | Описание |
|------|----------|
| `archive/Пустые сущности.dat` | Пакет разработки из DDS (ZIP) |
| `archive/Пустые сущности.xml` | Метаданные пакета |
| `archive/extracted/` | Распакованный пакет — реальная структура решения |

Содержит: решение `Sungero.Solution` + модуль `Sungero.Module1` с:
- Пустой виджет, пустой фоновый процесс, пустой блок-скрипт
- Перекрытие `ContractBase` с добавленным текстовым свойством `Property`

---

## GitHub ресурсы

| Репозиторий | Ссылка | Что внутри |
|------------|--------|-----------|
| rx-examples | https://github.com/DirectumCompany/rx-examples | Примеры перекрытий: GetRolePerformers, ConvertToPdfWithMarks |
| Sungero.Plugins.Templates | https://github.com/DirectumCompany/Sungero.Plugins.Templates | Шаблоны плагинов подписания (MIT) |
| DirectumContribIndex | https://github.com/DirectumCompany/DirectumContribIndex | Решения сообщества: Kafka, Telegram, шаблоны |
| DirectumCompany (org) | https://github.com/DirectumCompany | 74 репозитория, всё открыто |

---

## Скрипты

| Скрипт | Назначение |
|--------|-----------|
| `scripts/01_extract_webhelp.py` | HTML → markdown (нужен `pip install beautifulsoup4 lxml`) |
| `scripts/05_build_rag.py` | Загрузка в ChromaDB (нужен `pip install chromadb sentence-transformers`) |

### Быстрый старт RAG

```bash
cd knowledge-base

# 1. Установить зависимости
pip install beautifulsoup4 lxml chromadb sentence-transformers

# 2. Извлечь страницы (тест на 20 страницах)
python scripts/01_extract_webhelp.py --sample 20

# 3. Загрузить guides в ChromaDB
python scripts/05_build_rag.py --guides-only

# 4. Тестовый запрос
python scripts/05_build_rag.py --query "как добавить валидацию на поле"
python scripts/05_build_rag.py --query "GetRolePerformers пример"
python scripts/05_build_rag.py --query "AccessRights.AllowRead"

# 5. Полная загрузка (после полного извлечения)
python scripts/01_extract_webhelp.py
python scripts/05_build_rag.py --reset
```

---

## Reference Packages (распакованы из .dat)
- `targets/REFERENCE_CATALOG.md` — 43 .mtd, сущности Targets/KPI
- `targets/CODE_PATTERNS_CATALOG.md` — 500+ .cs, WebAPI/AsyncHandlers/Initializer
- `targets/RC_COMPONENTS_CATALOG.md` — 7 production RC
- `omniapplied/REFERENCE_CATALOG.md` — override модуля, SQL, IdentityService
- `docs/platform/PLUGIN_PATTERNS_CATALOG.md` — 3 архетипа плагинов

---

## Структура knowledge-base/

```
knowledge-base/
├── INDEX.md                          ← этот файл
├── guides/                           ← 38 developer-гайдов + solutions-reference
│   ├── 01_architecture.md            — Архитектура платформы
│   ├── 02_entities.md                — CRUD сущностей
│   ├── 03_server_client_shared.md    — Server/Client/Shared код
│   ├── 04_workflow.md                — Задачи и задания
│   ├── 05_documents.md               — Документы
│   ├── 06_access_rights.md           — Права доступа
│   ├── 07_overrides.md               — Перекрытия
│   ├── 08_api_reference.md           — API Reference
│   ├── 09_getting_started.md         — Создание решения с нуля
│   ├── 10_dialogs_ui.md              — Диалоги и UI
│   ├── 11_events_lifecycle.md        — События и жизненный цикл
│   ├── 12_structures_constants_resources.md — Структуры, константы
│   ├── 13_reports.md                 — Отчёты
│   ├── 14_background_async.md        — Фоновые процессы, async
│   ├── 15_sql_locking_database.md    — SQL, блокировки, БД
│   ├── 16_performance.md             — Быстродействие
│   ├── 17_nocode_features.md         — NoCode-настройка
│   ├── 18_integrations_isolated.md   — Интеграции, IsolatedArea
│   ├── 19_cookbook.md                 — Рецепты и паттерны
│   ├── 20_widgets_covers.md          — Виджеты и обложки
│   ├── 21_state_view.md              — StateView
│   ├── 22_base_guids.md              — BaseGuid справочник
│   ├── 23_mtd_reference.md           — MTD форматы (.mtd JSON)
│   ├── 24_platform_modules.md        — Каталог модулей платформы
│   ├── 25_code_patterns.md           — Паттерны C# кода
│   ├── 26_remote_components.md       — Remote Components (React)
│   ├── 27_dds_vs_crossplatform_ds.md — DDS vs CrossPlatform DS
│   ├── 28_windows_autonomous_setup.md — Windows-стенд Claude Code
│   ├── 29_solution_patterns.md       — Production Solution Patterns
│   ├── 30_ui_libraries_reports.md    — UI, библиотеки, отчёты
│   ├── 31_cover_localization_fix.md  — Обложки: заголовки через БД
│   ├── 32_rc_plugin_development.md  — RC Plugin Development
│   ├── 33_microservice_deployment.md — Microservice Deployment
│   ├── 34_applied_solution_packaging.md — Applied Solution Packaging
│   ├── 35_deployment_tool_internals.md — DeploymentToolCore Internals
│   ├── 36_launcher_internals.md       — DirectumLauncher Internals
│   ├── 37_development_studio_internals.md — DevelopmentStudio Internals
│   ├── 38_platform_integration_map.md — Карта интеграций платформы
│   └── solutions-reference.md        — Production reference (4 решения)
├── pages/                            ← генерируется скриптом 01
│   └── ru-RU/
│       ├── index.json                ← машиночитаемый индекс
│       ├── om/                       ← Object Model (556 стр.)
│       ├── sds/                      ← Dev Studio (332 стр.)
│       ├── func/                     ← Functions (426 стр.)
│       ├── nc/                       ← NoCode (174 стр.)
│       └── ...
├── archive/                          ← экспорт из DDS
│   ├── Пустые сущности.dat           — пакет разработки (ZIP)
│   ├── Пустые сущности.xml           — метаданные пакета
│   └── extracted/                    — распакованные исходники
├── chroma_db/                        ← генерируется скриптом 05
└── scripts/
    ├── 01_extract_webhelp.py
    └── 05_build_rag.py
```
