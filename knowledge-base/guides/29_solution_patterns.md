# 29. Production Solution Patterns — ESM, Agile, Targets

Паттерны извлечены из трёх production-решений Directum RX:
- **ESM** (rosa.ESM v1.6.261) — Управление корпоративными услугами (4 модуля, ~30 сущностей)
- **Agile** (DirRX.AgileBoard v2.8.2520) — Канбан-доски (7 модулей, 2 решения)
- **Targets** (DirRX.DirectumTargets v1.5.261) — Цели и KPI (5 модулей, 6 Remote Components)

Источники: ESM (rosa.ESM), Agile (DirRX.AgileBoards), Targets (DirRX.DirectumTargets).
**Targets source/ извлечён из .dat пакетов**: `targets/source/` — DirRX.DirectumTargets, DirRX.DTCommons, DirRX.KPI, DirRX.Targets, DirRX.TargetsAndKPIsUI.
**ESM и Agile source НЕ доступны** (нет .dat пакетов) — описания паттернов валидны как архитектурная документация, для copy-paste используй `targets/source/`, `CRM/crm-package/source/` и MCP `search_metadata`.

---

## 1. WebAPI Endpoints

Публичные REST-эндпоинты через атрибут `[Public(WebApiRequestType = ...)]`.

### GET-эндпоинт (ESM/CMDB)
```csharp
[Public(WebApiRequestType = RequestType.Get)]
public string GetConfigurationItemsRelationsTree(long configurationItemId, int level)
{
  var configurationItem = ConfigurationItems.GetAll(u => u.Id == configurationItemId).FirstOrDefault();
  if (configurationItem == null)
    return "{}";

  var relations = GetConfigurationItemRelations(configurationItem, level);
  var relatedIds = relations.Select(r => r.SourceId)
    .Concat(relations.Select(r => r.TargetId)).ToList();

  var items = ConfigurationItems.GetAll(u => relatedIds.Contains(u.Id)).ToList();
  var typesIds = relations.Select(r => r.RelationTypeId).ToList();
  var types = ConfigurationItemRelationTypes.GetAll(t => typesIds.Contains(t.Id)).ToList();

  var result = Structures.Module.ConfigurationItemRelationTreeData.Create();
  result.ConfigurationItems = JsonConvert.SerializeObject(GetConfigurationItemsData(items));
  result.ConfigurationItemsRelations = JsonConvert.SerializeObject(relations);
  result.ConfigurationItemsRelationsTypes = JsonConvert.SerializeObject(GetConfigurationItemRelationsTypesData(types));
  return JsonConvert.SerializeObject(result);
}
```

### POST-эндпоинт (ESM)
```csharp
[Public(WebApiRequestType = Sungero.Core.RequestType.Post)]
public long? CreateRequest(long serviceId, long userId)
{
  IService service = Services.GetAll(s => s.Id == serviceId).FirstOrDefault();
  IEmployee employee = Employees.GetAll(e => e.Id == userId).FirstOrDefault();
  var urgency = service?.Urgency ?? Functions.Module.GetDefaultUrgencyShared();
  var request = CreateRequest(service, employee, urgency);
  return request?.Id;
}
```

### POST с ролевой проверкой и DTO (ESM)
```csharp
[Public(WebApiRequestType = Sungero.Core.RequestType.Post)]
public List<Structures.Module.IReplaceableUser> GetReplaceableUsers()
{
  var adminsGuid = ESM.PublicConstants.Module.ESMRolesGroup.ServiceCatalogAdministrators;
  var agentsGuid = ESM.PublicConstants.Module.ESMRolesGroup.ServiceDeskAgents;

  if (Users.Current.IncludedIn(adminsGuid) || Users.Current.IncludedIn(agentsGuid))
    return new List<Structures.Module.IReplaceableUser>();

  return Sungero.CoreEntities.Substitutions.ActiveSubstitutedUsers
    .OrderBy(u => u.Name)
    .Select(u => Employees.As(u))
    .Where(e => e != null)
    .Select(e => Structures.Module.ReplaceableUser.Create(
      e.Id, e.Name,
      string.Join("/", new[] { e.JobTitle?.Name, e.Department?.Name, e.BusinessUnit?.Name }
        .Where(x => !string.IsNullOrEmpty(x)))))
    .ToList();
}
```

**Правила:**
- GET — для чтения без side-effects, POST — для мутаций и сложных запросов
- Всегда возвращать `IStructureName` (IsPublic структуры)
- Проверять роли через `Users.Current.IncludedIn(guid)`
- Сериализация через `JsonConvert.SerializeObject()`

---

## 2. AsyncHandler Patterns

### 2.1. Lock-Safe с Retry (ESM)
```csharp
public virtual void GrantAccessRightsToRequestAttachment(
    rosa.ESM.Server.AsyncHandlerInvokeArgs.GrantAccessRightsToRequestAttachmentInvokeArgs args)
{
  var attachment = AttachmentToRequests.GetAll(u => u.Id == args.attachmentId).FirstOrDefault();
  var user = Employees.GetAll(u => u.Id == args.userId).FirstOrDefault();

  if (attachment == null || user == null)
  {
    args.Retry = false;
    return;
  }

  if (!Locks.GetLockInfo(attachment).IsLocked)
  {
    try
    {
      if (!attachment.AccessRights.IsGranted(rightsGuid, user))
      {
        attachment.AccessRights.Grant(user, rightsGuid);
        attachment.AccessRights.Save();
      }
    }
    catch (Exception ex)
    {
      Logger.ErrorFormat("{0}. Error: {1}", nameof(GrantAccessRightsToRequestAttachment), ex.Message);
    }
  }
  else
  {
    args.Retry = true;
  }
}
```

### 2.2. ExponentialDelayStrategy + NextRetryTime (Targets/KPI)
```csharp
public virtual void DeleteCheckFile(
    DirRX.KPI.Server.AsyncHandlerInvokeArgs.DeleteCheckFileInvokeArgs args)
{
  args.Retry = false;
  if (args.RetryIteration == 0)
  {
    // Первый запуск — отложить на 12 часов
    args.Retry = true;
    args.NextRetryTime = Calendar.Now.AddHours(12);
    return;
  }
  // Основная логика удаления файла
  var doc = OfficialDocuments.GetAll(d => d.Id == args.DocumentId).FirstOrDefault();
  if (doc != null && !Locks.GetLockInfo(doc).IsLocked)
  {
    OfficialDocuments.Delete(doc);
  }
}
```

### 2.3. State-Driven Automation — IsFinal → Close Tickets (Agile)
```csharp
public virtual void CloseTicket(
    DirRX.AgileBoards.Server.AsyncHandlerInvokeArgs.CloseTicketInvokeArgs args)
{
  string failReason;
  if (TryCloseColumnTickets(args.ColumnId, out failReason))
  {
    if (!string.IsNullOrEmpty(failReason))
      Logger.Error($"'{nameof(CloseTicket)}' failed: {failReason}. Iteration: {args.RetryIteration}");
  }
  else
  {
    args.Retry = true;
    args.RetryReason = failReason;
  }
}

private static bool TryCloseColumnTickets(long columnId, out string failReason)
{
  failReason = string.Empty;
  var column = Columns.GetAll(x => x.Id == columnId).SingleOrDefault();
  if (column == null) { failReason = $"Column '{columnId}' not found."; return true; }

  const int MaxLockHours = 5;
  bool allSuccess = true;

  foreach (ITicket ticket in column.Tickets.Select(x => x.Ticket))
  {
    bool isFinal = column.IsFinal ?? false;
    if (isFinal && ticket.Status == Status.Active || !isFinal && ticket.Status == Status.Closed)
    {
      var lockInfo = Locks.GetLockInfo(ticket);
      if (lockInfo.IsLocked)
      {
        if ((Calendar.Now - lockInfo.LockTime).TotalHours < MaxLockHours)
        { allSuccess = false; continue; }
        else
          ForceUnlock(ticket);
      }
      ticket.Status = isFinal ? Status.Closed : Status.Active;
      ticket.CompleteDate = isFinal ? Calendar.Now : (DateTime?)null;
      ticket.Save();
    }
  }
  return allSuccess;
}
```

### 2.4. Batch Processing с лимитом (Targets/KPI)
```csharp
public virtual void ImportActualValuesInMetric(
    DirRX.KPI.Server.AsyncHandlerInvokeArgs.ImportActualValuesInMetricInvokeArgs args)
{
  const int batchLimit = 100;
  var records = GetUnprocessedRecords(args.MetricId).Take(batchLimit).ToList();

  foreach (var record in records)
  {
    if (Locks.TryLock(record))
    {
      try { ProcessRecord(record); record.Save(); }
      finally { Locks.Unlock(record); }
    }
    else
    {
      args.Retry = true;
    }
  }

  // Если остались необработанные — повторить
  if (GetUnprocessedRecords(args.MetricId).Any())
  {
    args.Retry = true;
    args.RetryReason = "Unprocessed records remain";
  }
}
```

### 2.5. AsyncHandler MTD — DelayStrategy
```json
{
  "NameGuid": "c8a47d37-...",
  "Name": "CloseTicket",
  "DelayPeriod": 15,
  "DelayStrategy": "ExponentialDelayStrategy",
  "IsHandlerGenerated": true,
  "MaxRetryCount": 15,
  "Parameters": [
    { "NameGuid": "...", "Name": "ColumnId", "ParameterType": "LongInteger" }
  ]
}
```

**Стратегии:**
| Стратегия | Поведение | Когда использовать |
|-----------|-----------|-------------------|
| `RegularDelayStrategy` | Фиксированный интервал | Простые retry (права, статусы) |
| `ExponentialDelayStrategy` | Экспоненциальный рост | Конкурентный доступ, блокировки |

---

## 3. Versioned Initialization

### Паттерн ModuleVersionInit (ESM)
```csharp
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  Dictionary<long, byte[]> licenses = null;
  try
  {
    licenses = Sungero.Docflow.PublicFunctions.Module.ReadLicense();
    Sungero.Docflow.PublicFunctions.Module.DeleteLicense();

    // Версионная инициализация: каждая версия выполняется только один раз
    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.FirstInitializing, Constants.Module.Init.ESM.Name,
      Version.Parse(Constants.Module.Init.ESM.FirstInitVersion));
    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.Initializing14252, Constants.Module.Init.ESM.Name,
      Version.Parse(Constants.Module.Init.ESM.Version14252));
    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.Initializing15253, Constants.Module.Init.ESM.Name,
      Version.Parse(Constants.Module.Init.ESM.Version15253));
    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.Initializing16261, Constants.Module.Init.ESM.Name,
      Version.Parse(Constants.Module.Init.ESM.Version16261));
  }
  finally
  {
    Sungero.Docflow.PublicFunctions.Module.RestoreLicense(licenses);
  }
}

public virtual void FirstInitializing()
{
  CreateProcessRightsForRequests();
  GrantAccessRightsToRoles();
  CreateDefaultStatuses();
  CreateDocumentTypes();
  CreateDocumentKinds();
  CreateRelationTypes();
  CreateDefaultClosingRule();
  CreateDefaultSLARule();
}
```

### Константы версий
```csharp
public static class Init
{
  public static class ESM
  {
    public const string Name = "ESM";
    public const string FirstInitVersion = "1.4.0.0";
    public const string Version14252 = "1.4.252.0";
    public const string Version15253 = "1.5.253.0";
    public const string Version16261 = "1.6.261.0";
  }
}
```

**Правила:**
- Каждая версия — отдельный метод (`FirstInitializing`, `Initializing14252`)
- `ModuleVersionInit` запускает метод только если версия ещё не выполнялась
- Оборачивать в `ReadLicense/RestoreLicense` если init создаёт лицензируемые сущности
- Constants хранят имена и версии

---

## 4. Public DTO Structures

### Плоский DTO (ESM)
```csharp
[Public]
partial class ServiceData
{
  public long Id { get; set; }
  public string Name { get; set; }
  public string Description { get; set; }
  public byte[] Logo { get; set; }
  public int Priority { get; set; }
}
```

### Вложенный DTO с ссылками на другие структуры (Agile)
```csharp
[Public]
partial class BoardDto
{
  public long Id { get; set; }
  public string Name { get; set; }
  public string Prefix { get; set; }
  public bool IsEnabled { get; set; }
  public bool HasFullAccesRights { get; set; }
  public string SwimlanesSettings { get; set; }
  public List<DirRX.AgileBoards.Structures.Module.IColumnReferenceDto> Columns { get; set; }
  public List<DirRX.AgileBoards.Structures.Module.ITicketReferenceDto> Tickets { get; set; }
  public List<DirRX.AgileBoards.Structures.Module.IRecipientDto> Performers { get; set; }
}

[Public]
partial class TicketDto
{
  public long Id { get; set; }
  public string Name { get; set; }
  public string Uid { get; set; }
  public long BoardId { get; set; }
  public int Priority { get; set; }
  public double? PlannedWorkload { get; set; }
  public DateTime? Deadline { get; set; }
  public int CommentsCount { get; set; }
  public string Status { get; set; }
  public int ChildrenCount { get; set; }
  public List<DirRX.AgileBoards.Structures.Module.ICustomProperty> CustomProperties { get; set; }
  public DirRX.AgileBoards.Structures.Module.ITicketLockInfo LockInfo { get; set; }
}
```

### CellValue<T> для Remote Tables (Targets)
```csharp
[Public]
partial class CellValueString
{
  public string Value { get; set; }
  public bool Disabled { get; set; }
  public string Icon { get; set; }
}

[Public]
partial class CellValueDouble
{
  public double? Value { get; set; }
  public bool Disabled { get; set; }
}

[Public]
partial class CellValueObject
{
  public bool Disabled { get; set; }
  public DirRX.Targets.Structures.Module.ICellValueObjectValue Value { get; set; }
}

[Public]
partial class CellValueObjectValue
{
  public long? Value { get; set; }
  public string Label { get; set; }
  public string LowerLabel { get; set; }
  public string Color { get; set; }
}
```

### MTD PublicStructures
```json
"PublicStructures": [
  {
    "Name": "BoardDto",
    "IsPublic": true,
    "Properties": [
      { "Name": "Id", "TypeFullName": "global::System.Int64" },
      { "Name": "Name", "IsNullable": true, "TypeFullName": "global::System.String" },
      {
        "Name": "Columns",
        "IsList": true,
        "IsNullable": true,
        "TypeFullName": "global::System.Collections.Generic.List<global::DirRX.AgileBoards.Structures.Module.IColumnReferenceDto>"
      }
    ],
    "StructureNamespace": "DirRX.AgileBoards.Structures.Module"
  }
]
```

**Правила:**
- `[Public]` + `partial class` в ModuleStructures.cs
- DDS генерирует интерфейс `ITypeName` → всегда `List<ITypeName>` в функциях
- Ссылки на другие структуры через полный путь: `DirRX.AgileBoards.Structures.Module.IColumnDto`
- MTD: `TypeFullName` = `global::...` с полным namespace
- `IsList: true` для коллекций

---

## 5. Reference Collections с Position Tracking

### Паттерн (Agile: Board → Column → Ticket)

**Иерархия:**
```
Board
  └── Columns (BoardColumns) — child collection с IndexColumn
        └── Column
              └── Tickets (ColumnTickets) — child collection с Position
                    └── Ticket
```

### Добавление с позицией
```csharp
[Public]
public virtual IColumnTickets AddTicketToColumn(ITicket ticket, int position)
{
  if (position == Functions.Ticket.GetNotDefinedPositionConstant())
    position = _obj.Tickets.Any() ? _obj.Tickets.Max(x => x.Position.Value) + 1 : 0;

  if (ticket.BoardId != _obj.BoardId)
    ticket.BoardId = _obj.BoardId;

  var ticketRef = _obj.Tickets.AddNew();
  ticketRef.Position = position;
  ticketRef.Ticket = ticket;
  return ticketRef;
}
```

### Перестройка позиций (gap-free)
```csharp
[Public]
public virtual Dictionary<long, int> RebuildColumnPositions()
{
  var changedPositions = new Dictionary<long, int>();
  var orderedTickets = _obj.Tickets.OrderBy(x => x.Position).ToList();
  for (int i = 0; i < orderedTickets.Count; ++i)
  {
    if (orderedTickets[i].Position != i)
    {
      orderedTickets[i].Position = i;
      changedPositions.Add(orderedTickets[i].Id, i);
    }
  }
  return changedPositions;
}
```

### Перемещение колонки (shift-алгоритм)
```csharp
public static Structures.Module.IColumnMovedResult MoveColumn(
    string appId, IBoard board, IBoardColumns columnRef, int position)
{
  position = Math.Max(0, Math.Min(position, board.Columns.Count));
  int currentPosition = columnRef.IndexColumn.Value;
  var newPositions = new List<Structures.Module.INewPosition>();
  newPositions.Add(Structures.Module.NewPosition.Create(columnRef.Id, position));

  if (currentPosition < position) // Двигаем вправо
  {
    foreach (var col in board.Columns
      .Where(t => t.IndexColumn > currentPosition && t.IndexColumn <= position))
    {
      col.IndexColumn--;
      newPositions.Add(Structures.Module.NewPosition.Create(col.Id, col.IndexColumn.Value));
    }
  }
  else // Двигаем влево
  {
    foreach (var col in board.Columns
      .Where(t => t.IndexColumn >= position && t.IndexColumn < currentPosition))
    {
      col.IndexColumn++;
      newPositions.Add(Structures.Module.NewPosition.Create(col.Id, col.IndexColumn.Value));
    }
  }

  columnRef.IndexColumn = position;
  board.Save();
  return Structures.Module.ColumnMovedResult.Create(newPositions);
}
```

---

## 6. Soft Delete

### Паттерн (Agile)
```csharp
// AsyncHandler: пометить как удалённые (НЕ физическое удаление)
public virtual void DeleteTicket(
    DirRX.AgileBoards.Server.AsyncHandlerInvokeArgs.DeleteTicketInvokeArgs args)
{
  args.Retry = false;
  var ids = args.IdsString.Split(',').Select(long.Parse).ToList();
  var tickets = Tickets.GetAll(x => ids.Contains(x.Id))
    .Where(x => x.Status != Ticket.Status.Deleted);

  foreach (var ticket in tickets)
  {
    if (Locks.GetLockInfo(ticket).IsLocked)
    {
      args.Retry = true;
      continue;
    }
    ticket.Status = Ticket.Status.Deleted;
    ticket.Save();
  }
}
```

**Использование:**
```csharp
// Выборки — всегда фильтровать удалённые
var activeTickets = Tickets.GetAll(x => x.Status != Ticket.Status.Deleted);

// Восстановление
ticket.Status = Ticket.Status.Active;
ticket.Save();
```

---

## 7. Hierarchical Structures

### Department Hierarchy Traversal (Targets)

**Сверху вниз:**
```csharp
public List<IDepartment> GetDepartmentsHierarchyFromTop(IBusinessUnit businessUnit)
{
  var result = new List<IDepartment>();
  var departments = Departments.GetAll()
    .Where(d => Equals(d.BusinessUnit, businessUnit) && d.HeadOffice == null && d.Status == Status.Active)
    .ToList();

  foreach (var dept in departments)
  {
    result.Add(dept);
    result.AddRange(GetChildDepartments(dept));
  }
  return result;
}

private List<IDepartment> GetChildDepartments(IDepartment parent)
{
  var result = new List<IDepartment>();
  var children = Departments.GetAll()
    .Where(d => Equals(d.HeadOffice, parent) && d.Status == Status.Active).ToList();
  foreach (var child in children)
  {
    result.Add(child);
    result.AddRange(GetChildDepartments(child)); // Рекурсия
  }
  return result;
}
```

**Снизу вверх:**
```csharp
public List<IDepartment> GetDepartmentsHierarchyFromBottom(IDepartment department)
{
  var result = new List<IDepartment>();
  var current = department;
  while (current != null)
  {
    result.Add(current);
    current = current.HeadOffice;
  }
  return result;
}
```

### Поиск родительской карты целей (Targets)
```csharp
public ITargetsMap FindParentTargetsMap(ITargetsMap targetsMap)
{
  var structuralUnits = GetDepartmentsHierarchyFromBottom(targetsMap.StructuralUnit)
    .Skip(1).ToList(); // Исключаем текущий

  return TargetsMaps.GetAll()
    .Where(tm => structuralUnits.Contains(tm.StructuralUnit))
    .Where(tm => tm.Period.RelativeSize > targetsMap.Period.RelativeSize)
    .Where(tm => tm.PeriodStart <= targetsMap.PeriodStart
              && targetsMap.PeriodEnd <= tm.PeriodEnd)
    .OrderBy(tm => tm.Period.RelativeSize)
    .FirstOrDefault();
}
```

---

## 8. Period-Based Planning

### Вычисление дат периодов (Targets/DTCommons)
```csharp
public DateTime GetPreviousPeriodDate(DateTime date, string measure, int amount)
{
  switch (measure)
  {
    case "Day":   return date.AddDays(-amount);
    case "Week":  return date.AddDays(-7 * amount);
    case "Month": return date.AddMonths(-amount);
    case "Year":  return date.AddYears(-amount);
    default:      return date;
  }
}

public DateTime GetStartDateOfPeriod(DateTime date, string measure)
{
  switch (measure)
  {
    case "Day":   return date.Date;
    case "Week":  return date.BeginningOfWeek();
    case "Month": return date.BeginningOfMonth();
    case "Year":  return date.BeginningOfYear();
    default:      return date;
  }
}
```

### Определение типа периода по датам
```csharp
public IPeriod GetPeriodByDates(DateTime start, DateTime end)
{
  int days = (end - start).Days + 1;
  if (days <= 1)   return GetPeriod("Day");
  if (days <= 7)   return GetPeriod("Week");
  if (days <= 31)  return GetPeriod("Month");
  if (days <= 93)  return GetPeriod("Quarter");
  if (days <= 186) return GetPeriod("HalfYear");
  return GetPeriod("Year");
}
```

---

## 9. Remote Components — metadata.json

### Несколько контролов в одном компоненте (ESM)
```json
{
  "vendorName": "rosa",
  "componentName": "ESMRemoteControls",
  "componentVersion": "1.3.251.0",
  "controls": [
    {
      "name": "CIRelationsTree",
      "loaders": [{ "name": "ci-relations-tree-loader", "scope": "Card" }],
      "displayNames": [
        { "locale": "en", "name": "Configuration items relations" },
        { "locale": "ru", "name": "Связи конфигурационных единиц" }
      ]
    },
    {
      "name": "ServiceCatalogControl",
      "loaders": [{ "name": "service-catalog-loader", "scope": "Cover" }],
      "displayNames": [
        { "locale": "en", "name": "Service Catalog" },
        { "locale": "ru", "name": "Каталог Услуг" }
      ]
    },
    {
      "name": "WorkEvaluationControl",
      "loaders": [{ "name": "work-evaluation-loader", "scope": "Card" }],
      "displayNames": [
        { "locale": "en", "name": "Work evaluation on request" },
        { "locale": "ru", "name": "Оценка работы по обращению" }
      ]
    }
  ],
  "publicName": "rosa_ESMRemoteControls_1_3_251_0",
  "hostApiVersion": "1.0.1"
}
```

### Multi-scope контрол — Card + Cover (Targets)
```json
{
  "vendorName": "Directum",
  "componentName": "GoalsMap",
  "componentVersion": "1.1",
  "controls": [{
    "name": "GoalsMap",
    "loaders": [
      { "name": "GoalsMap-card-loader", "scope": "Card" },
      { "name": "GoalsMap-cover-loader", "scope": "Cover" }
    ]
  }],
  "publicName": "Directum_GoalsMap_1_1",
  "hostApiVersion": "1.0.0"
}
```

**Правила:**
- `publicName` = `{vendor}_{component}_{version}` (подчёркивания вместо точек)
- `scope`: `Card` (карточка сущности) / `Cover` (обложка модуля)
- `displayNames` — обязательна локализация en + ru
- `hostApiVersion`: `1.0.0` или `1.0.1`

---

## 10. Role-Based Access

### Проверка роли (Client)
```csharp
public bool CanCurrentUserManageCI()
{
  return Users.Current.IncludedIn(Roles.Administrators);
}

public bool CanCurrentUserCreateCI()
{
  return Users.Current.IncludedIn(Constants.Module.Roles.CMDBAdministrator);
}
```

### Массовая выдача прав (Initializer)
```csharp
public void GrantAccessRightsToRoles()
{
  InitializationLogger.Debug("Init: Выдача прав на типы сущностей.");
  var cmdbAdmin = Roles.GetAll(r => r.Sid == Constants.Module.Roles.CMDBAdministrator).FirstOrDefault();
  if (cmdbAdmin != null)
  {
    ConfigurationItems.AccessRights.Grant(cmdbAdmin, DefaultAccessRightsTypes.FullAccess);
    ConfigurationItems.AccessRights.Save();
    ConfigurationItemKinds.AccessRights.Grant(cmdbAdmin, DefaultAccessRightsTypes.FullAccess);
    ConfigurationItemKinds.AccessRights.Save();
  }
}
```

### Синхронизация прав между объектами (Targets — AsyncHandler)
```csharp
// Копировать права с исходного объекта
foreach (var rights in sourceObj.AccessRights.Current)
{
  doc.AccessRights.Grant(rights.Recipient, rights.AccessRightsType);
}
doc.AccessRights.Save();
```

---

## 11. Cover Actions + LocalizeFunction

### Server StateView с LocalizeFunction
```csharp
[Remote, LocalizeFunction("GetConfigurationItemStateFunctionName",
                           "GetConfigurationItemStateFunctionDescription")]
public StateView GetConfigurationItemState()
{
  var stateView = StateView.Create();
  stateView.AddDefaultLabel(ConfigurationItems.Resources.WithoutRelatedDocuments);
  // ... наполнение StateView блоками
  return stateView;
}
```

### Client Cover Action с LocalizeFunction
```csharp
[Public, LocalizeFunction("ExpFunc_ShowFormViews_Name", "ExpFunc_ShowFormViews_Description")]
public void ShowConfigurationItemFormViews()
{
  if (!CanCurrentUserUpdateCIForms())
  {
    Dialogs.ShowMessage(Resources.FormViewError, MessageType.Error);
    return;
  }
  ESM.PublicFunctions.Module.ShowFormViewsByEntityType(
    Resources.ConfigurationItemTypeNameInGenitiveCase,
    Constants.Module.EntitiesGuids.ConfigutationItemTypeGuid);
}
```

---

## 12. Multi-Session Isolation (Agile)

```csharp
// Сессия 1: удаление из старых колонок + добавление в новые
using (var session = new Session(true, false))
{
  board = Boards.Get(boardId);
  // ... операции перемещения ...
  newColumn.Save();
  session.SubmitChanges();
}

// Сессия 2: переупорядочивание в целевой колонке
using (var session = new Session(true, false))
{
  var column = Columns.Get(columnId);
  Functions.Module.MoveTicketsToColumnAndNotifyClient(
    string.Empty, boardId, ticketRefIds, column, columnRefId, 0);
  session.SubmitChanges();
}
```

**Когда использовать:**
- Операции, затрагивающие несколько агрегатов
- Нужна промежуточная фиксация в БД
- Конкурентный доступ к связанным сущностям

---

## 13. IsolatedAreas (Targets/KPI)

### XLSX Parsing в изолированном окружении
```csharp
// IsolatedFunctions.cs (в папке IsolatedAreas/XLSXParsing/)
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

---

## 14. Сводная таблица паттернов

| Паттерн | ESM | Agile | Targets | Секция |
|---------|-----|-------|---------|--------|
| WebAPI endpoints | + | - | - | 1 |
| AsyncHandler Lock-Safe | + | + | + | 2.1 |
| ExponentialDelayStrategy | - | + | + | 2.2, 2.5 |
| State-Driven Async | - | + | - | 2.3 |
| Batch Processing | - | - | + | 2.4 |
| Versioned Init | + | + | + | 3 |
| Public DTO Structures | + | + | + | 4 |
| Position Collections | - | + | - | 5 |
| Soft Delete | - | + | - | 6 |
| Hierarchy Traversal | - | - | + | 7 |
| Period Planning | - | - | + | 8 |
| Remote Components | + | - | + | 9 |
| Role-Based Access | + | - | + | 10 |
| LocalizeFunction | + | - | - | 11 |
| Multi-Session | - | + | - | 12 |
| IsolatedAreas | - | - | + | 13 |
