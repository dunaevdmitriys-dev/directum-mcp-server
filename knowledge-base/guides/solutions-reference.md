# Reference: Архитектурные паттерны из реальных решений Directum RX

> Этот документ — каталог реальных решений. Используй при проектировании новых фич.
> **Не генерируй из головы — подглядывай сюда.**

> **Targets source/ извлечён из .dat пакетов**: `targets/source/` — DirRX.DirectumTargets, DirRX.DTCommons, DirRX.KPI, DirRX.Targets, DirRX.TargetsAndKPIsUI.
> **ESM и Agile source НЕ доступны** (нет .dat пакетов) — описания паттернов валидны как архитектурная документация.
>
> **Доступные источники для copy-paste:**
> - **Targets исходники**: `targets/source/` — Remote Components, XLSX import, Word gen, periods, hierarchy, async handlers
> - **CRM исходники** (33 .mtd, 283 .cs): `CRM/crm-package/source/` — Databook, Document, Task, Assignment, WebAPI, AsyncHandler, Job, Widget, Report, Cover, RC
> - **Платформенные модули** (base/ внутри Docker): `MCP: search_metadata name=<keyword>` — Sungero.Docflow, Company, Contracts, RecordManagement
> - **MTD-шаблоны** (inline): `knowledge-base/guides/23_mtd_reference.md` — готовые JSON-шаблоны для 6 типов сущностей
> - **MCP scaffold_***: `scaffold_entity`, `scaffold_module`, `scaffold_report` и др. — генерация корректных .mtd из параметров

---

## Быстрый поиск: "Мне нужно сделать X"

| Задача | Решение-пример | Путь к коду |
|--------|----------------|-------------|
| REST API через WebApiRequestType | AgileBoard (30+ endpoints) | ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/` |
| Remote Component (React в RX) | Targets (6 RC: GoalsMap, TableControl, Charts) | `targets/source/DirRX.DirectumTargets/` |
| Kanban-доску | AgileBoard (Board→Column→Ticket) | ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/` |
| Кастомная таблица на карточке | Targets (RemoteTableControl + CRUD) | `targets/source/DirRX.KPI/` |
| Email-to-Ticket | ESM (DCS + regex парсинг) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| SLA с расчётом времени | ESM (4 режима: User/Group/Overtime/Default) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| Workflow с ролевой сводкой | ESM (SummaryView по ролям) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| AI-интеграция (AIAgentTool) | ESM (Tool per Service) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| Массовый импорт из XLSX | Targets (Isolated Area + async) | `targets/source/DirRX.KPI/` |
| Генерация Word-документов | Targets (Aspose.Words в Isolated) | `targets/source/DirRX.Targets/` |
| Граф связей (many-to-many) | AgileBoard (SQL-таблица связей) | ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/` |
| Real-time обновления | AgileBoard (ClientManager + MessageSender) | ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/` |
| Кастомная история изменений | AgileBoard (DirRX.History через SQL) | ⚠️ НЕ ДОСТУПЕН: `DirRX.History/` |
| Freemium/лицензирование | AgileBoard (300 тикетов, лицензируемые роли) | ⚠️ НЕ ДОСТУПЕН: `Sungero.AgileBoardsLicense/` |
| CRM-воронка продаж | CRM (Pipeline→Stage→Deal + Kanban RC) | `crm-package/source/DirRX.CRMSales/` |
| BANT lead scoring | CRM (4 boolean + Score + ChangedShared) | `crm-package/source/DirRX.CRMMarketing/DirRX.CRMMarketing.Shared/Lead/Lead.mtd` |
| Модульная архитектура "звезда" | CRM (6 модулей: 5 CRM + Solution) | `crm-package/source/` |
| Round-robin распределение | CRM (LeadAssignmentJob) | `crm-package/source/DirRX.CRM/` |
| Виджеты на обложке | CRM (4 виджета: счётчики + bar-chart) | `crm-package/source/DirRX.CRM/` |
| ExpressionElement для конструктора | ESM (5 функций для условий маршрута) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| Кастомные свойства через Metadata | AgileBoard (CustomProperties reflection) | ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/` |
| Матричная приоритизация | ESM (Influence × Urgency = Priority) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |
| Кэширование данных виджетов | ESM (фоновый Job + кэш по ключу) | ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/` |

---

## 1. AgileBoard — Kanban-доски

**Путь**: ⚠️ source НЕ ДОСТУПЕН (нет .dat пакета)
**Модули**: DirRX.AgileBoards, DirRX.TeamsCommonAPI, DirRX.History, DirRX.TimeTracker

### Архитектурные решения

**REST API через WebApiRequestType** — 30+ серверных функций с `[Public(WebApiRequestType = RequestType.Post/Get)]`. Весь фронтенд работает через эти эндпоинты. Паттерн для SPA без отдельного backend.

**Real-time collaboration** — `ClientManager.Instance.GetClientsOfUser(userId)` → `AgileMessageSender.SendTicketUpdated()`. Все пользователи на одной доске видят изменения мгновенно. Параметр `appId` исключает инициатора из рассылки.

**Кастомная система истории** — DirRX.History хранит изменения в SQL-таблице (прямой INSERT), с сериализацией в JSON. Поддержка virtual properties (перемещение между колонками). Батчевая запись по 1000 записей. Трекинг Added/Changed/Deleted в коллекциях через reflection.

**Граф связей через SQL DDL** — many-to-many через кастомную таблицу `TicketRelation` (создаётся в Initializer через SQL CREATE TABLE). 4 типа: require, duplicate, parent, simple. Обход ограничения DatabookEntry на связи.

**ForceUnlock через Reflection** — доступ к `LockManager.ForceUnlock` через Assembly.Load + GetMethod. Для автоматического разблокирования тикетов после 5 часов.

**Кастомные свойства** — сохранение произвольных свойств (добавленных при кастомизации) через `MetadataSearcher.FindFinalEntityMetadata` + reflection `GetProperty`. Паттерн "расширяемость без деплоя".

**IdsString через запятую** — обход ограничения async-обработчиков (нельзя передать List<long>): `args.IdsString.Split(',').Select(long.Parse)`.

### Ключевые файлы
- `DirRX.AgileBoards.Server/ModuleServerFunctions.cs` — 30+ WebAPI
- `DirRX.AgileBoards.Server/ModuleAsyncHandlers.cs` — 9 async (CloseTicket, ImportTrello, DeleteTicket)
- `DirRX.AgileBoards.Shared/ModuleStructures.cs` — 40+ DTO
- `DirRX.History.Server/ModuleServerFunctions.cs` — кастомная история
- `DirRX.AgileBoards.Server/ModuleInitializer.cs` — SQL DDL для связей

---

## 2. Targets/KPI — Цели и показатели

**Путь**: `targets/source/` (извлечён из .dat)
**Модули**: DirRX.Targets, DirRX.KPI, DirRX.DTCommons, DirRX.DirectumTargets, DirRX.TargetsAndKPIsUI

### Архитектурные решения

**6 Remote Components** — GoalsMap (дерево целей, Cover + Card scope), TableControl (CRUD-таблица), ChartsControl (графики), PeriodControl, RichMarkdownEditor, AnalyticsControl. RC зарегистрированы в Solution-модуле, а не в бизнес-модулях.

**RemoteTableControl — кастомный табличный паттерн**:
- Метаданные таблицы (колонки, типы, сортировка, кнопки)
- Типизированные ячейки: CellValueString, CellValueDouble, CellValueEntity
- CRUD: GetRowsData, BatchUpdate, DeleteRow, AddRow
- ChangeTracking: _ChangedColumns, _ChangedFrom, _ChangeType
- Иерархия: HierarchyConfig с уровнями вложенности

**Массовый импорт XLSX** — pipeline: Isolated Function `GenerateEmptyTemplate` → пользователь заполняет → `GetMetricActiualDataFromXLSX` парсинг → файл проверки (SimpleDocument) → async handler импорта → фоновая очистка.

**Word-генерация через Aspose.Words** — Isolated Area WordPostprocessing: удаление Markdown-якорей, форматирование таблиц, установка полей страницы. Пример обработки документов в изолированной среде.

**Fan-out async** — ConvertTargetsMapsDates запускает ExecutorConvertTargetsMapsDates для КАЖДОЙ карты отдельно. Правильный подход для длительных операций с retry.

**Лицензирование через пустой модуль** — TargetsAndKPIsUI: `IsLicensed: true`, `Importance: High`, 0 сущностей, 0 кода. Только для проверки наличия лицензии.

**Виджет с параметрами** — WidgetAchievingMetricValues (Plot): MetricId (NavigationParameter), Period (Enum), ShowPrevPeriod (Bool). Пример параметризованного виджета.

### Ключевые файлы
- `DirRX.Targets.Server/ModuleServerFunctions.cs` — основная бизнес-логика (57K)
- `DirRX.Targets.Shared/ModuleStructures.cs` — 1930 строк DTO
- `DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/` — парсинг Excel
- `DirRX.Targets.Isolated/IsolatedAreas/WordPostprocessing/` — Aspose.Words
- `DirRX.DirectumTargets.Components/` — 6 Remote Components

---

## 3. ESM — Service Desk

**Путь**: ⚠️ source НЕ ДОСТУПЕН (нет .dat пакета)
**Модули**: rosa.ESM, rosa.CMDB, rosa.ESMUI

### Архитектурные решения

**Email-to-Ticket через DCS** — `ProccessDCSPackage(packageJson)`: десериализация DCS-пакета → извлечение MailCaptureInstanceInfo (From, Subject) → regex-поиск номера обращения в теме → обновление существующего или создание нового → файлы письма → AttachmentToRequest. ZIP через SharpCompress в Isolated Area.

**SLA-калькулятор 4 режимов** — GetSolvationTime(): User (по рабочему календарю инициатора), Group/Department (по календарю подразделения), Group/BusinessUnit (по календарю НО), Overtime (24/7). Двухуровневое фоновое обновление: каждые 15 мин для срочных + раз в период для всех.

**Матричная приоритизация** — PrioritizationRule с PriorityCalculationMatrix: Influence × Urgency = Priority. Автоматический расчёт при создании обращения.

**AI-интеграция (AIAgentTool)** — Для каждой услуги создаётся AI-инструмент с `HandlerName = "CreateRequestFromTool"`. Пользователь говорит AI "Хочу заказать пропуск" → AI находит Tool → создаёт обращение. Паттерн self-registration в AI-помощнике.

**ExpressionElement** — 5 функций для визуального конструктора маршрутов: `IsTypeRequestProcessingTasks`, `GetResponsibles`, `CreateSummaryViewContentRow`, `GetClosingRequestRuleDeadline`, `HasNotificationTaskByRequest`. Позволяет бизнес-аналитику использовать сложную логику без кода.

**Ролевые представления сводки** — SummaryView коллекция в задаче: разный текст для Администратора / ССП / CMDB-админа / Пользователя. `GetRequestSummaryByRole()` — приоритетная выдача.

**Гибкая нумерация** — собственная система с SQL-счётчиком, составной формат (код НО + подразделение + год + квартал + порядковый номер). Независима от Sungero.Docflow RegistrationSetting.

**FormView с JSON Criteria** — кастомные формы обращений по услуге. Парсинг JSON через JObject для определения контекстного представления.

**RequestDatabook** — самая богатая DatabookEntry: 14 действий, кастомные фильтры (мультивыбор статусов + MasterControl-зависимости), EntityStyleSelectors (зачёркивание закрытых), 25+ свойств.

### Ключевые файлы
- `rosa.ESM.Server/ModuleServerFunctions.cs` — WebAPI, DCS, нумерация
- `rosa.ESM.Server/RequestDatabookServerFunctions.cs` — SLA, статусы, бизнес-логика
- `rosa.ESM.Server/ModuleAsyncHandlers.cs` — 14+ async (AI Tools, уведомления, права)
- `rosa.ESM.Shared/Module.mtd` — 64+ сущности, 5 Jobs, виджеты
- `rosa.CMDB.Shared/` — CMDB-сущности (ConfigurationItem, Category, Kind)

---

## 4. CRM — Управление продажами

**Путь**: `crm-package/source/`
**Модули**: DirRX.CRM, DirRX.CRMSales, DirRX.CRMMarketing, DirRX.CRMDocuments, DirRX.CRMCommon, DirRX.Solution

### Архитектурные решения

**"Звёздная" архитектура** — 6 модулей (5 CRM + Solution): CRMSales и CRMMarketing оба зависят от CRMCommon, но НЕ друг от друга. Фасадный CRM зависит от всех 4 (CRMCommon, CRMSales, CRMMarketing, CRMDocuments) и связывает их через PublicFunctions. CRMCommon — фасад авторизации (HasCRMAccess, IsCRMAdmin). Файлы: `DirRX.CRM/DirRX.CRM.Shared/Module.mtd` (Dependencies), `DirRX.CRMCommon/DirRX.CRMCommon.Server/ModuleServerFunctions.cs`.

**Ручная JSON-сериализация** — 22 метода *ToJson() через StringBuilder с helper-функциями JsonStr/JsonLong/JsonDouble + generic ListToJson<T>. Обход ограничения DDS на NuGet-пакеты. Файл: `DirRX.CRM/DirRX.CRM.Server/ModuleServerFunctions.cs` (строки 1316-1600+).

**BANT Lead Scoring** — 4 boolean свойства в Lead.mtd: HasBudget (Code: HasBdgt), HasAuthority (HasAuth), HasNeed, HasTimeline (HasTmln). Все 4 имеют `ChangedShared` handlers. Автоматический пересчёт Score (IntegerProperty). Lead также содержит: Name, CompanyName, Email, Phone, Position, Source (nav), Campaign (nav), LeadStatus (enum: NewLead/InProgress/Qualified/Converted/Rejected), Responsible, ConvertedDeal, ConvertedContact, CreatedDate, Note, 5 UTM-полей (UtmSource/Medium/Campaign/Term/Content), LandingPage. Файл: `DirRX.CRMMarketing/DirRX.CRMMarketing.Shared/Lead/Lead.mtd`.

**Pipeline Value** — `Sum(d => d.Amount.Value * (d.Probability.HasValue ? d.Probability.Value : 0) / 100.0)` — взвешенная стоимость воронки (только deals с не-финальным Stage и заполненным Amount). Стандарт CRM-индустрии. Файл: `DirRX.CRMSales/DirRX.CRMSales.Server/ModuleServerFunctions.cs` (метод CalculatePipelineValue, строки 49-56).

**Round-robin** — LeadAssignmentJob: `managers[i % managers.Count]` — циклическое распределение неназначенных лидов (LeadStatus == NewLead, Responsible == null) между участниками роли SalesManager. Файл: `DirRX.CRM/DirRX.CRM.Server/ModuleJobs.cs` (строка 109).

**Гибридная архитектура** — RX-карточки для CRUD + Remote Components (Kanban, Dashboard, Funnel, Customer360, LeadBoard) для визуализации + WebAPI как мост.

**OpenInCrmSpa** — Action на карточке Deal (определён в Deal.mtd) открывает SPA через `Sungero.Core.Hyperlinks.Open(spaUrl)` где spaUrl = `/Client/content/crm/#/deals/{id}`. Переход из RX в SPA по клику. Файл: `DirRX.CRMSales/DirRX.CRMSales.ClientBase/Deal/DealActions.cs` (строки 95-105).

### Ключевые файлы
- `DirRX.CRM/DirRX.CRM.Server/ModuleServerFunctions.cs` — 25+ WebAPI, KPI, 22 *ToJson() + JsonStr/JsonLong/JsonDouble helpers
- `DirRX.CRM/DirRX.CRM.Server/ModuleJobs.cs` — LeadAssignmentJob (round-robin), UpdateLeadScoresJob
- `DirRX.CRMSales/DirRX.CRMSales.Shared/Deal/Deal.mtd` — центральная сущность (16 свойств, 6 Actions включая OpenInCrmSpa)
- `DirRX.CRMSales/DirRX.CRMSales.Server/ModuleServerFunctions.cs` — CalculatePipelineValue, GetActivitiesByDeal
- `DirRX.CRMSales/DirRX.CRMSales.ClientBase/Deal/DealActions.cs` — OpenInCrmSpa через Hyperlinks.Open()
- `DirRX.CRMMarketing/DirRX.CRMMarketing.Shared/Lead/Lead.mtd` — BANT (4 bool + Score) + UTM (5 полей) + LeadStatus enum
- `DirRX.CRMCommon/DirRX.CRMCommon.Server/ModuleServerFunctions.cs` — HasCRMAccess, IsCRMAdmin
- `DirRX.CRM/DirRX.CRM.Server/ModuleInitializer.cs` — роли, воронка, начальные данные
- `DirRX.Solution/DirRX.Solution.Shared/Module.mtd` — 5 Remote Components

---

## Сводная таблица паттернов

| Паттерн | Agile | Targets | ESM | CRM |
|---------|:-----:|:-------:|:---:|:---:|
| WebAPI через WebApiRequestType | 30+ | 10+ | 5+ | 25+ |
| Remote Components | — | 6 | — | 5 |
| Real-time (ClientManager) | + | — | — | — |
| Кастомная история (SQL) | + | — | — | — |
| Isolated Area | — | 2 (XLSX, Word) | 1 (ZIP) | — |
| Async Handlers | 9 | 12 | 14+ | 4 |
| Jobs (фоновые) | — | 2 | 5 | 2 |
| Виджеты | — | 1 (Plot) | 3 (Pie, Bar, Plot) | 4 (Counter, Bar) |
| Отчёты | — | — | — | 6 |
| Workflow (Task/Assignment) | 1 block | 1 task | 1 task + 7 assignments | 1 task |
| Лицензирование | Freemium | Отдельный модуль | Отдельный модуль | Роли |
| AI-интеграция | — | — | AIAgentTool | — |
| ExpressionElement | — | — | 5 функций | — |
| Ручная JSON-сериализация | — | — | JObject | StringBuilder (22 *ToJson) |
| Reflection (internal API) | 3 хака | — | — | — |
| Импорт внешних данных | Trello JSON | XLSX | Email (DCS) | — |
| Граф связей (SQL DDL) | + | — | — | — |
| Round-robin распределение | — | — | — | + |
| Матричная приоритизация | — | — | + | — |
| BANT scoring | — | — | — | + |
