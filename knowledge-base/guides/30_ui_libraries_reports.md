# 30. UI, визуальные паттерны, сторонние библиотеки, отчёты

Справочник по визуальной части, UI-паттернам, внешним библиотекам и отчётам
из production-решений ESM, Agile, Targets. Используй как каталог возможностей
при проектировании новых решений.

Источники: ESM (rosa.ESM), Agile (DirRX.AgileBoards), Targets (DirRX.DirectumTargets).
**Targets source/ извлечён из .dat пакетов**: `targets/source/` — DirRX.DirectumTargets, DirRX.DTCommons, DirRX.KPI, DirRX.Targets, DirRX.TargetsAndKPIsUI.
**ESM и Agile source НЕ доступны** (нет .dat пакетов) — описания паттернов валидны как архитектурная документация, для copy-paste используй `targets/source/`, `CRM/crm-package/source/` и MCP `search_metadata`.

---

## 1. Каталог UI-возможностей — что доступно и когда применять

### 1.1 Стандартные формы (DefaultCardView.xml + MTD)

**Когда:** любая сущность — справочник, документ, задача.

- Разметка формы определяется **в MTD** (секция Forms/Controls)
- DefaultCardView.xml — минимальный XML с настройками grid-ов (DevExpress XtraSerializer)
- Табличные коллекции: XtraGrid с сортировкой, фильтрацией, группировкой

**Пример настройки коллекции (Target → KeyResults):**
```xml
<XtraSerializer version="1.0" application="GridControl">
  <property name="$GridControl">
    <property name="View">
      <property name="AllowColumnMoving">true</property>
      <property name="AllowSorting">true</property>
      <property name="AutoWidth">false</property>
    </property>
  </property>
</XtraSerializer>
```
Колонки: Index, Description, Metric, InitialValue, PlannedValue, ActualValue,
Weight, ResultGrade, Responsible, AchievementPercentage, IsAchieved, Note

**Где смотреть:**
- `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/Target/Generated/DefaultCardView.xml`
- `archive/esm_extracted/.../RegistrationRequest/Generated/DefaultCardView.xml` ⚠️ НЕ ДОСТУПЕН

---

### 1.2 Remote Components (React + Module Federation)

**Когда:** стандартных контролов платформы недостаточно — нужны деревья, графики,
кастомные таблицы, drag-and-drop, rich-редакторы.

#### Каталог Remote Components из production

| Компонент | Решение | Scope | Что делает | Где код |
|-----------|---------|-------|-----------|---------|
| **ServiceCatalogControl** | ESM | Cover | Каталог услуг на обложке (карточки с иконками) | ⚠️ НЕ ДОСТУПЕН: `.../ESMRemoteControls/` |
| **CIRelationsTree** | ESM | Card | Дерево связей между КЕ (CMDB) | там же |
| **UploadRequestApplicationControl** | ESM | Card | Загрузка файлов к обращению | там же |
| **WorkEvaluationControl** | ESM | Card | Оценка качества работы | там же |
| **TableControl** | Targets | Card | Таблица KPI (4 режима loader) | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../TableControl/` |
| **ChartsControl** | Targets | Card | Графики и диаграммы | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../ChartsControl/` |
| **GoalsMap** | Targets | Card+Cover | Дерево целей (иерархия) | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../GoalsMap/` |
| **PeriodControl** | Targets | Card | Селектор периода | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../PeriodControl/` |
| **RichMarkdownEditor** | Targets | Card | WYSIWYG редактор (RXMD) | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../RichMarkdownEditor/` |
| **AnalyticsControl** | Targets | Card | Дашборд аналитики KPI | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/.../AnalyticsControl/` |
| **TimesheetsComponent** | Agile | Card | Учёт времени по тикетам | ⚠️ НЕ ДОСТУПЕН: `.../TimesheetsComponent/` |

#### metadata.json — шаблон

```json
{
  "vendorName": "CompanyName",
  "componentName": "MyControl",
  "componentVersion": "1.1",
  "controls": [
    {
      "name": "MyControl",
      "loaders": [
        { "name": "my-card-loader", "scope": "Card" },
        { "name": "my-cover-loader", "scope": "Cover" }
      ],
      "displayNames": [
        { "locale": "en", "name": "My Control" },
        { "locale": "ru", "name": "Мой контрол" }
      ]
    }
  ],
  "publicName": "CompanyName_MyControl_1_1",
  "hostApiVersion": "1.0.0"
}
```

#### npm-зависимости (из webpack bundles)

| Библиотека | Версия | Решение | Назначение |
|-----------|--------|---------|-----------|
| `react` | 18.2.0 | ESM, Targets, Agile | UI framework |
| `react-dom` | 18.2.0 | ESM, Targets, Agile | DOM rendering |
| `i18next` | 22.4.11 | ESM | Интернационализация |
| `moment` | 2.29.1 / 2.30.1 | Targets | Работа с датами/периодами |

#### Webpack 5 Module Federation — сборка

```
remoteEntry.js          — точка входа (shared dependencies)
index_1.1_{hash}.js     — основной бандл
chunks/{id}_{hash}.js   — lazy-loaded чанки
css/{id}.{hash}.css     — стили компонента
```

**Shared dependencies** разрешаются через хост (react, react-dom) —
не дублируются в каждом компоненте.

#### Интеграция Remote Component с Actions

Серверная сторона отдаёт данные через WebAPI, клиент обрабатывает действия:

```csharp
// Actions.cs — обработка клика из Remote Component
public virtual void ShowGoalFromRemoteControl(ExecuteActionArgs e)
{
  var actionGuid = new Guid("63a6fd2f-...");
  var payload = TeamsCommonAPI.PublicFunctions.Module
    .ExtractRemoteControlActionContextPayload(actionGuid, e.Entity.Id, typeGuid);
  var data = JsonConvert.DeserializeObject<ShowFromRemoteControl_Payload>(payload);
  Targets.Get(data.Id)?.ShowModal();
}
```

**Где смотреть:**
- `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/TargetsMap/TargetsMapActions.cs` — ShowGoalFromRemoteControl, AddIndicatorRowFromRemoteControl
- `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/TargetsMap/TargetsMapClientFunctions.cs` — CreateDocument, ShowReport

---

### 1.3 Внешнее веб-приложение (отдельный frontend)

**Когда:** нужен полностью кастомный UI с real-time обновлениями
(канбан-доска, чат, дашборд с WebSocket).

**Пример — Agile Kanban Board:**

Архитектура:
```
[Внешнее веб-приложение]  ←REST API→  [Directum RX Server]
         ↕ WebSocket                      ↕ AgileMessageSender
    [Все клиенты]         ←broadcast←  [AgileUtils.dll]
```

- Канбан НЕ Remote Component — отдельное приложение
- Десктоп-клиент открывает через гиперлинк:
  ```csharp
  [Public, LocalizeFunction]
  public void GoToWebsite(string website, long boardId, long? ticketId)
  {
    Hyperlinks.Open(Functions.Module.MakeBoardLink(website, boardId, ticketId));
  }
  ```
- Server отдаёт данные через `[Public(WebApiRequestType)]` endpoints
- Real-time через **AgileMessageSender** (12 типов сообщений)

**12 типов real-time сообщений:**
```
SendColumnMoved, SendColumnAdded, SendColumnRemoved, SendColumnConfigUpdated,
SendTicketAdded, SendTicketUpdated, SendTicketMoved, SendTicketsRemoved,
SendTicketLockChanged, SendSwimlanesSettingsChanged, SendTicketVoted, SendTicketLinked
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleServerFunctions.cs` — WebAPI endpoints (GetAllEditableBoards, MoveTicketsToBoard, etc.)
- ⚠️ НЕ ДОСТУПЕН: `.../ColumnServerFunctions.cs` — MoveColumn
- ⚠️ НЕ ДОСТУПЕН: `.../TicketServerFunctions.cs` — MoveTicketsToBoard
- ⚠️ НЕ ДОСТУПЕН: `.../Libraries/AgileUtils.dll` — messaging DLL

---

### 1.4 InputDialog (каскадные поля, валидация)

**Когда:** нужен ввод параметров перед действием — создание обращения,
выбор периода, фильтрация, подтверждение с параметрами.

#### Паттерн: каскадные связанные поля (ESM)

```csharp
var dialog = Dialogs.CreateInputDialog(Resources.Title);

// Поля
var category = dialog.AddSelect(Resources.Category, false, ServiceCategories.Null)
  .From(categories);
var service = dialog.AddSelect(Resources.Service, true, Services.Null)
  .From(services);
var articles = dialog.AddHyperlink(Resources.Articles);
articles.IsVisible = false;

// Категория → фильтрует услуги
category.SetOnValueChanged((e) => {
  service.From(services.Where(s =>
    category.Value == null ||
    s.CategoriesCollection.Any(c => Equals(c.CategoriesServices, category.Value))
  ).ToArray());
});

// Услуга → блокирует категорию + показывает статьи
service.SetOnValueChanged((e) => {
  category.IsEnabled = e.NewValue == null;
  if (e.NewValue != null)
  {
    var available = Functions.Service.Remote.GetAvailableArticles(e.NewValue, Employees.Current);
    articles.IsVisible = available.Any();
  }
});

// Кастомная кнопка + серверная валидация при нажатии
var createBtn = dialog.Buttons.AddCustom(Resources.CreateButton);
dialog.Buttons.AddCancel();

dialog.SetOnButtonClick((e) => {
  if (e.Button == createBtn)
  {
    var error = Functions.Module.Remote.ValidateRequest(service.Value, employee);
    if (!string.IsNullOrEmpty(error))
    {
      e.AddError(error);
      return;
    }
    var request = Functions.Module.Remote.CreateRequest(service.Value, employee, priority, urgency);
    request?.ShowModal();
  }
});

dialog.Show();
```

#### Паттерн: диалог с файлом (Agile — Trello import)

```csharp
var dialog = Dialogs.CreateInputDialog(Resources.ImportTitle);
var fileSelect = dialog.AddFileSelect(Resources.FileLabel, true)
  .WithFilter(Resources.JsonFilter, "json");

if (dialog.Show() == DialogButtons.Ok)
{
  Functions.Board.Remote.ImportBoardFromTrello(fileSelect.Value.Content);
  Dialogs.ShowMessage(Resources.ImportStarted, MessageType.Information);
}
```

#### Паттерн: диалог с Assignment (ESM — выбор сотрудника)

```csharp
var dialog = Dialogs.CreateInputDialog(_obj.Info.Actions.RequestForWork.LocalizedName);
var dept = dialog.AddSelect(Departments.Info.LocalizedName, false, Departments.Null)
  .From(departments);
var emp = dialog.AddSelect(Employees.Info.LocalizedName, true, Employees.Null)
  .From(employees);

// Подразделение → фильтрует сотрудников
dept.SetOnValueChanged((d) => {
  if (d.NewValue != null && d.NewValue != d.OldValue)
  {
    if (emp.Value != null && emp.Value.Department != d.NewValue)
      emp.Value = null;
    emp.From(employees.Where(s => Equals(s.Department, d.NewValue)));
  }
});

// Сотрудник → подставляет подразделение
emp.SetOnValueChanged((e) => {
  if (e.NewValue != null)
    dept.Value = e.NewValue.Department;
});

if (dialog.Show() == DialogButtons.Ok)
  _obj.RelatedDepartmentResponsible = emp.Value;
if (dialog.IsCanceled)
  e.Cancel();
```

#### Паттерн: виджет-диалог с кастомной кнопкой (ESM)

```csharp
var dialog = Dialogs.CreateInputDialog(Resources.ContextTypeSelectDialogTitle);
var contextType = dialog.AddSelect(Resources.ContextTypeTitle, true)
  .From(contextTypes.ToArray());

var applyBtn = dialog.Buttons.AddCustom(Resources.ApplyButton);
dialog.Buttons.AddCancel();

dialog.SetOnRefresh(e => {
  if (!string.IsNullOrEmpty(contextType.Value))
    e.AddInformation(Resources.ContextTypeChangingInfo);
});

if (dialog.Show() == applyBtn)
{
  var key = string.Format(Constants.Module.CacheKey, Users.Current.Id);
  Functions.Module.Remote.AddOrUpdateCacheValue(key, contextType.Value, Calendar.Now.AddDays(2));
}
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleClientFunctions.cs` — CreateRequestDialog (каскадные поля)
- ⚠️ НЕ ДОСТУПЕН: `.../ProcessingByResponsiblePersonAssignmentActions.cs` — Assignment dialog
- ⚠️ НЕ ДОСТУПЕН: `.../BoardActions.cs` — ImportFromTrello (файл)
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleWidgetHandlers.cs` — Widget context dialog

---

### 1.5 StateView (информационные блоки на карточке)

**Когда:** нужно показать структурированную информацию на карточке сущности —
связанные документы, статусы, графики текстом.

```csharp
[Remote, LocalizeFunction("FuncName", "FuncDesc")]
public StateView GetConfigurationItemState()
{
  var stateView = StateView.Create();
  stateView.AddDefaultLabel(Resources.WithoutRelatedDocuments);

  var relatedDocs = Functions.ConfigurationItemKind.GetRelatedDocuments(_obj.ConfigurationItemKind);
  if (relatedDocs.Any())
  {
    var block = stateView.AddBlock();
    block.AddLabel(Resources.RelatedDocumentsHeader);
    foreach (var doc in relatedDocs)
    {
      var line = block.AddLabel(doc.Name);
      line.AddAction(Resources.Open, () => doc.Show());
    }
  }
  return stateView;
}
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ConfigurationItemServerFunctions.cs` — GetConfigurationItemState

---

### 1.6 Cover Actions ([LocalizeFunction])

**Когда:** нужны кнопки/действия на обложке модуля.

```csharp
// Клиентская функция с LocalizeFunction → появляется как Cover Action
[Public, LocalizeFunction("ExpFunc_ShowFormViews_Name", "ExpFunc_ShowFormViews_Description")]
public void ShowConfigurationItemFormViews()
{
  if (!CanCurrentUserUpdateCIForms())
  {
    Dialogs.ShowMessage(Resources.FormViewError, MessageType.Error);
    return;
  }
  ESM.PublicFunctions.Module.ShowFormViewsByEntityType(
    Resources.EntityTypeName, Constants.Module.EntitiesGuids.TypeGuid);
}

// Серверная StateView функция → показывается как информационный блок
[Remote, LocalizeFunction("FuncName", "FuncDescription")]
public StateView GetConfigurationItemState() { ... }
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../rosa.CMDB.ClientBase/ModuleClientFunctions.cs` — Cover functions
- ⚠️ НЕ ДОСТУПЕН: `.../rosa.CMDB.Server/ConfigurationItem/ConfigurationItemServerFunctions.cs`

---

### 1.7 Виджеты на обложке

**Когда:** нужны KPI-метрики, графики, счётчики на обложке модуля.

```csharp
// Server handler — вычисление данных
partial class RequestsByStatusWidgetHandlers
{
  public virtual void GetData(Sungero.Domain.WidgetGetDataEventArgs e)
  {
    var count = Requests.GetAll()
      .Where(r => r.Status == Status.Active)
      .Count();
    e.Value = count;
    e.Label = Resources.WidgetLabel;
  }
}

// Client handler — обработка клика по виджету
public virtual void ExecuteWidgetAchievementTargetChartTargetsMapAction()
{
  var map = Functions.Module.Remote.GetTargetsMapForWidget(
    _parameters.StructuralUnitId, _parameters.Period).FirstOrDefault();
  if (map != null)
    map.ShowModal();
}
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleWidgetHandlers.cs` — SLA виджеты
- `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleWidgetHandlers.cs` — Achievement widget

---

## 2. Сторонние библиотеки — каталог

### 2.1 C# / NuGet

| Библиотека | Решение | Назначение | Как подключить | Где пример |
|-----------|---------|-----------|---------------|-----------|
| **Newtonsoft.Json** | ESM, Agile, Targets | JSON сериализация/десериализация | `using Newtonsoft.Json;` | Все ModuleServerFunctions.cs |
| **Aspose.Words** | Targets | Генерация Word-документов (отчёты) | `using Aspose.Words;` + DLL | `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleServerFunctions.cs` |
| **Aspose.Cells** | Targets | Парсинг XLSX (массовый импорт) | IsolatedArea + DLL | `targets/source/DirRX.KPI/DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/` |
| **Svg** | ESM | Рендеринг SVG иконок | `using Svg;` + DLL | ⚠️ НЕ ДОСТУПЕН: `.../ModuleServerFunctions.cs` |
| **NaturalSort** | Targets | Натуральная сортировка (1,2,10) | `using NaturalSort;` + DLL | `targets/source/DirRX.Targets/` |
| **System.Net.Mail** | ESM | Email-интеграция | Стандартная .NET | ⚠️ НЕ ДОСТУПЕН: `.../` |

### 2.2 Кастомные DLL (libraries/)

| DLL | Решение | Назначение |
|-----|---------|-----------|
| **AgileUtils.dll** | Agile | Real-time messaging (SignalR/WebSocket обёртка) |
| **RemoteTableUtilites.dll** | Targets | Утилиты для Remote Table (парсинг CellValue) |
| **ResourceReport.dll** | Targets | Генерация отчётов |
| **ConfigWrapperV1_0_0.dll** | Targets | Управление конфигурацией |

### 2.3 npm (Frontend)

| Библиотека | Версия | Решения | Назначение |
|-----------|--------|---------|-----------|
| **react** | 18.2.0 | ESM, Agile, Targets | UI framework |
| **react-dom** | 18.2.0 | ESM, Agile, Targets | DOM rendering |
| **i18next** | 22.4.11 | ESM | Интернационализация React-компонентов |
| **moment** | 2.29-2.30 | Targets | Работа с датами (периоды, форматирование) |

### 2.4 Кастомные DLL — подключение

```
source/{Company}.{Module}/
  Libraries/
    {GUID}/
      MyLibrary.dll          # Сама DLL
      MyLibrary.deps.json    # Зависимости (опционально)
```

В Module.mtd:
```json
"Libraries": [
  { "NameGuid": "...", "Name": "MyLibrary", "Path": "Libraries/{GUID}/MyLibrary.dll" }
]
```

---

## 3. Отчёты и документы — каталог подходов

### 3.1 Встроенные Report (Sungero.Reporting + FastReport)

**Когда:** стандартный отчёт с параметрами, SQL/Collection источниками.

```
Shared/Reports/{Name}/
  {Name}.mtd                — метаданные (BaseGuid cef9a810)
  {Name}Constants.cs        — SourceTableName (имя temp-таблицы)
  {Name}.resx / .ru.resx    — ресурсы
Server/Reports/{Name}/
  {Name}Handlers.cs         — BeforeExecute (данные), AfterExecute (очистка)
  {Name}.frx                — FastReport XML шаблон
  {Name}Queries.xml         — SQL: CREATE TABLE / SELECT
ClientBase/Reports/{Name}/
  {Name}Handlers.cs         — BeforeExecute (диалог параметров)
```

#### FastReport .frx — движок отчётов

Directum RX использует **FastReport** для рендеринга отчётов. 50+ .frx файлов в базовой платформе.

**Структура .frx (XML):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp"
  ReferencedAssemblies="System.dll, ..., FastReport.Compat.dll, FastReport.DataVisualization.dll"
  ReportInfo.CreatorVersion="2020.2.12.0">
  <ScriptText>namespace FastReport { public class ReportScript { } }</ScriptText>
  <Dictionary>
    <SungeroSqlDataConnection Name="Sungero_Connection">
      <TableDataSource Name="Sungero_Reports_{Name}" SelectCommand="SELECT * FROM {Table} WHERE ReportSessionId = @ReportSessionId">
        <Column Name="ColumnName" DataType="System.String"/>
      </TableDataSource>
    </SungeroSqlDataConnection>
    <SungeroParameter Name="ReportDate" DataType="System.DateTime"/>
    <SungeroParameter Name="ReportName" DataType="System.String"
      Expression="[Reports.Resources.{Name}.ReportName]"/>
  </Dictionary>
  <ReportPage>
    <ReportTitleBand>...</ReportTitleBand>
    <PageHeaderBand>...</PageHeaderBand>
    <GroupHeaderBand Condition="[...GroupColumn]">
      <DataBand DataSource="Sungero_Reports_{Name}">
        <TextObject Text="[Sungero_Reports_{Name}.Column]"/>
      </DataBand>
    </GroupHeaderBand>
    <PageFooterBand>...</PageFooterBand>
  </ReportPage>
</Report>
```

**Ключевые элементы .frx:**
| Элемент | Назначение |
|---------|-----------|
| `SungeroSqlDataConnection` | Подключение к БД через платформу |
| `TableDataSource` + `SelectCommand` | SQL-запрос к temp-таблице |
| `SungeroParameter` | Параметры из MTD (ReportSessionId, даты и т.д.) |
| `ReportTitleBand` | Заголовок отчёта |
| `PageHeaderBand` | Шапка колонок (повторяется на каждой странице) |
| `GroupHeaderBand` + `Condition` | Группировка данных |
| `DataBand` + `DataSource` | Строки данных |
| `Highlight` | Условное форматирование |
| `Hyperlink.Expression` | Кликабельные ссылки на документы |

#### Queries.xml — SQL для temp-таблицы

```xml
<?xml version="1.0" encoding="utf-8"?>
<queries>
  <query key="CreateSourceTable">
    <mssql><![CDATA[CREATE TABLE {0}
(ReportSessionId VARCHAR(256) NOT NULL,
 Id INT NOT NULL,
 Name NVARCHAR(MAX) NULL,
 Amount INT NULL,
 Created DATETIME NULL)]]></mssql>
    <postgres><![CDATA[CREATE TABLE {0}
(ReportSessionId citext NOT NULL,
 Id int NOT NULL,
 Name citext NULL,
 Amount int NULL,
 Created timestamp NULL)]]></postgres>
  </query>
  <query key="SourceQuery">
    <default><![CDATA[SELECT * FROM {0} WHERE ReportSessionId = @ReportSessionId]]></default>
  </query>
</queries>
```

**SQL типы: MSSQL → PostgreSQL:**
`NVARCHAR(MAX)` → `citext`, `INT` → `int`, `DATETIME` → `timestamp`,
`BIT` → `boolean`, `FLOAT` → `double precision`

#### Pipeline выполнения

```
1. Client BeforeExecute → InputDialog → установить параметры
2. Server BeforeExecute → Guid.NewGuid() → собрать данные → WriteStructuresToTable()
3. FastReport (.frx) → SELECT из temp-таблицы → рендер PDF/Word
4. Server AfterExecute → DeleteReportData() (очистка temp-таблицы)
```

**Где смотреть:**
- `.claude/skills/create-report/SKILL.md` — полный шаблон (MTD + .frx + Queries.xml + Constants + Handlers)
- Платформенные отчёты: MCP `search_metadata name=Report` — Sungero.Docflow (RegistrationSettingReport, SkippedNumbersReport, ExchangeOrderReport)

### 3.2 Aspose.Words — программная генерация Word

**Когда:** нужен отформатированный Word-документ из данных сущности.

```csharp
using Aspose.Words;
using Aspose.Words.Tables;
using Aspose.Words.Replacing;

// Паттерн из Targets:
// 1. Определить тип документа (Report/Plan)
Functions.TargetsMap.CreateDocument(_obj, Constants.Module.DocumentKinds.TargetsMapReport);

// 2. Логика создания:
//    - Проверить статус (Draft, UnderApproval, Active, ResultsApproval)
//    - Диалог: Create vs. Open (если документ уже существует)
//    - Создать новую версию или открыть существующую
//    - Заполнить данными из сущности
```

**Где смотреть:**
- `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/TargetsMap/TargetsMapClientFunctions.cs` — CreateDocument
- `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleServerFunctions.cs` — серверная генерация

### 3.3 Aspose.Cells — XLSX парсинг (IsolatedArea)

**Когда:** массовый импорт данных из Excel.

```csharp
// IsolatedFunctions.cs (в IsolatedAreas/XLSXParsing/)
public List<Structures.Module.IMetricMassImportActualValues> GetMetricActiualDataFromXLSX(
    byte[] fileBytes, long metricId)
{
  var result = new List<Structures.Module.IMetricMassImportActualValues>();
  using (var stream = new MemoryStream(fileBytes))
  {
    var workbook = new Aspose.Cells.Workbook(stream);
    var sheet = workbook.Worksheets[0];
    for (int row = 1; row <= sheet.Cells.MaxDataRow; row++)
    {
      var item = Structures.Module.MetricMassImportActualValues.Create();
      item.MetricId = metricId;
      item.Date = sheet.Cells[row, 0].DateTimeValue;
      item.Value = sheet.Cells[row, 1].DoubleValue;
      result.Add(item);
    }
  }
  return result;
}
```

**Вызов из AsyncHandler:**
```csharp
var data = DirRX.KPI.IsolatedFunctions.XLSXParsing
  .GetMetricActiualDataFromXLSX(byteArray, metricId);
```

**Где смотреть:**
- `targets/source/DirRX.KPI/DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/IsolatedFunctions.cs`

### 3.4 Виджетная аналитика (без отчётов)

**Когда:** не нужен печатный отчёт, достаточно показать данные на обложке.

- Фоновое задание агрегирует данные → сохраняет в кэш-сущность
- Виджет GetData читает кэш → показывает число/график
- Клик по виджету → открывает карточку с деталями

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleWidgetHandlers.cs` — SLA, динамика обращений
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleJobs.cs` — фоновая агрегация

### 3.5 Cumulative Flow (серверная агрегация)

**Когда:** нужен график потока (CFD) без отдельного отчёта.

```csharp
[Public(WebApiRequestType = RequestType.Get)]
public string GetBoardCumulativeDataBetweenDates(long boardId, DateTime from, DateTime till)
{
  // SQL-запрос к истории перемещений тикетов
  // Агрегация по дням: сколько тикетов в каждой колонке
  // Возврат JSON для фронтенда
}
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleServerFunctions.cs` — GetBoardCumulativeDataBetweenDates

---

## 4. Паттерны UI-взаимодействия

### 4.1 Params кэш (Showing/Refresh — без Remote)

```csharp
// Client handler — кэшируем результат один раз
public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
{
  bool exists = false;
  e.Params.TryGetValue(Constants.Target.LinkedProjectPlanExists, out exists);
  // Используем exists вместо Remote-вызова
}

// Заполняем кэш при открытии
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  var exists = Functions.Target.Remote.CheckProjectPlanExists(_obj.Id);
  e.Params[Constants.Target.LinkedProjectPlanExists] = exists;
}
```

### 4.2 Лицензирование (проверка на каждую операцию)

```csharp
// Agile — проверка перед каждым действием
Sungero.AgileBoardsLicense.PublicFunctions.Module.ThrowExceptionIfCurrentUserHasNoLicense();

// Проверка наличия лицензии
if (!Sungero.AgileBoardsLicense.PublicFunctions.Module.HasLicenseForCurrentUser())
{
  // Заблокировать все свойства
  foreach (var prop in _obj.State.Properties)
    prop.IsEnabled = false;
}
```

### 4.3 CanExecute для Actions

```csharp
public override bool CanDeleteEntity(CanExecuteActionArgs e)
{
  return !_obj.IsDefault.GetValueOrDefault() &&
         Functions.Module.GetRegistrationRequests().Count > 1;
}

public override bool CanStartNextIteration(CanExecuteActionArgs e)
{
  return _obj.Status == Status.Active &&
         HasLicenseForCurrentUser() &&
         _obj.Columns.Any(c => c.Column.IsFinal == true);
}
```

### 4.4 AI-интеграция (ESM — AIAgentTools)

```csharp
// Создание AI-инструмента для автоматизации обращений
using Sungero.Commons;

var tool = AIAgentTools.Create();
tool.ToolName = toolName;
tool.HandlerModule = Constants.Module.ESMModuleName;
tool.HandlerName = Constants.Module.CreateRequestFromToolFunctionName;
tool.Save();

// Выдача прав на инструмент роли
AIAgentTools.AccessRights.Grant(scUsersRole, DefaultAccessRightsTypes.Read);
AIAgentTools.AccessRights.Save();
```

**Где смотреть:**
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleServerFunctions.cs` — CreateServiceTool, GrantAccessRightsToTools
- ⚠️ НЕ ДОСТУПЕН: `.../ModuleAsyncHandlers.cs` — GrantAccessRightsToTools, DeleteServiceTool

---

## 5. Матрица выбора: что использовать в каком случае

| Задача | Решение | Пример |
|--------|---------|--------|
| Простая форма справочника | DefaultCardView + MTD | ESM: Service, Urgency |
| Таблица с inline-редактированием | XtraGrid (DevExpress) | Targets: Target.KeyResults |
| Дерево/граф объектов | **Remote Component (React)** | ESM: CIRelationsTree, Targets: GoalsMap |
| Графики и диаграммы | **Remote Component** | Targets: ChartsControl, AnalyticsControl |
| Rich-текст / markdown | **Remote Component** | Targets: RichMarkdownEditor |
| Кастомная таблица с фильтрами | **Remote Component** | Targets: TableControl (4 mode) |
| Каталог с карточками/иконками | **Remote Component (Cover)** | ESM: ServiceCatalogControl |
| Селектор периода | **Remote Component** | Targets: PeriodControl |
| Kanban-доска с D&D | **Отдельное веб-приложение** + WebSocket | Agile: внешний сайт + AgileMessageSender |
| Ввод параметров перед действием | **InputDialog** | ESM: CreateRequest, Agile: ImportTrello |
| Связанные каскадные поля | **InputDialog + SetOnValueChanged** | ESM: Category → Service → Articles |
| Информационный блок на карточке | **StateView** | ESM: CMDB related docs |
| Числовой KPI на обложке | **Widget (NumberWidget)** | ESM: Active requests count |
| График на обложке | **Widget (ChartWidget)** | Targets: Achievement chart |
| Печатный отчёт (Word) | **Aspose.Words** | Targets: TargetsMapReport |
| Импорт из Excel | **Aspose.Cells + IsolatedArea** | Targets: MetricMassImport |
| Стандартный RX-отчёт | **Report entity (Sungero.Reporting)** | `.claude/skills/create-report/` |
| Email-интеграция | **System.Net.Mail** | ESM: email capture |
| AI-автоматизация | **AIAgentTools** | ESM: service request automation |
| Real-time обновления | **AgileMessageSender** (WebSocket DLL) | Agile: 12 типов сообщений |
| Импорт из Trello | **JSON parse + AsyncHandler** | Agile: ImportTrelloBoard |
| Генерация SVG/иконок | **Svg library** | ESM: service category icons |

---

## 6. Ссылки на исходный код (абсолютные пути)

### ESM
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/rosa.ESM.Server/ModuleServerFunctions.cs` — WebAPI, логика
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/rosa.ESM.Server/ModuleAsyncHandlers.cs` — 11 async handlers
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/rosa.ESM.Server/ModuleInitializer.cs` — versioned init
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/rosa.ESM.ClientBase/ModuleClientFunctions.cs` — диалоги, cover actions
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESM/rosa.ESM.Shared/ModuleStructures.cs` — Public DTO
- ⚠️ НЕ ДОСТУПЕН: `rosa.ESMSolution/rosa.ESMSolution.Components/ESMRemoteControls/metadata.json`
- ⚠️ НЕ ДОСТУПЕН: `rosa.CMDB/rosa.CMDB.ClientBase/ModuleClientFunctions.cs` — LocalizeFunction

### Agile
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.Server/ModuleServerFunctions.cs` — messaging, API
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.Server/ModuleAsyncHandlers.cs` — CloseTicket, DeleteTicket
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.Server/Column/ColumnServerFunctions.cs` — MoveColumn
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.Server/Ticket/TicketServerFunctions.cs` — MoveTicketsToBoard
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.Shared/ModuleStructures.cs` — 50+ DTO
- ⚠️ НЕ ДОСТУПЕН: `DirRX.AgileBoards/DirRX.AgileBoards.ClientBase/Board/BoardActions.cs` — StartNextIteration, ImportTrello

### Targets
- `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleServerFunctions.cs` — TableMetadata, Remote
- `targets/source/DirRX.Targets/DirRX.Targets.Server/TargetsMap/TargetsMapServerFunctions.cs` — иерархия
- `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/TargetsMap/TargetsMapActions.cs` — Remote Component actions
- `targets/source/DirRX.KPI/DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/IsolatedFunctions.cs` — Aspose.Cells
- `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/` — 6 Remote Components
- `targets/source/DirRX.DTCommons/DirRX.DTCommons.Server/Period/PeriodServerFunctions.cs` — периоды
