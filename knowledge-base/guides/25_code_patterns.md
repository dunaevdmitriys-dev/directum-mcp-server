# 25. Паттерны C# кода Directum RX — из реального кода платформы и CRM

## Источник

Реальный код из `archive/base/` (Sungero.Company, Sungero.Docflow) и `CRM/crm-package/source/` (DirRX.CRM* модули).
Все примеры проверены на Directum RX v26.1 / Sungero. Каждый пример содержит ссылку на реальный файл.

---

## 1. Using statements

### Server (.Server/)

Базовый набор для всех серверных файлов:

```csharp
// Пример: DirRX.CRMSales.Server/ModuleServerFunctions.cs:1-5
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

Дополнительно для **ModuleInitializer**:

```csharp
// Пример: DirRX.CRM.Server/ModuleInitializer.cs:6
using Sungero.Domain.Initialization;
```

### Client (.ClientBase/)

```csharp
// Пример: DirRX.CRM.ClientBase/ModuleClientFunctions.cs:1-5
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

### Shared

```csharp
// Пример: DirRX.CRMSales.Shared/Deal/DealHandlers.cs:1-5
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

> **Правило:** В реальном CRM-коде Shared-файлы используют тот же набор из 5 using, что и Server/Client.

---

## 2. Namespace

| Слой | Namespace | Пример из CRM |
|------|-----------|---------------|
| Server (module functions) | `{Company}.{Module}.Server` | `DirRX.CRMSales.Server` |
| Server (entity handlers) | `{Company}.{Module}` | `DirRX.CRMSales` |
| Client | `{Company}.{Module}.Client` | `DirRX.CRMSales.Client` |
| Shared | `{Company}.{Module}` | `DirRX.CRMSales` |
| BlockHandlers | `{Company}.{Module}.Server.{TaskName}Blocks` | `Sungero.Docflow.Server.ApprovalTaskBlocks` |

> **Важно:** Entity ServerHandlers (BeforeSave и т.д.) используют namespace `{Company}.{Module}` (без `.Server`), а ServerFunctions — `{Company}.{Module}.Server`.

---

## 3. Remote функции (ServerFunctions)

### Паттерн A: Module-level static (основной паттерн CRM)

Все Remote-функции в CRM 26.1 — `static` методы в `partial class ModuleFunctions` или `partial class {Entity}Functions`.

```csharp
// Пример: DirRX.CRMSales.Server/ModuleServerFunctions.cs:15-19
// Паттерн: [Public, Remote(IsPure = true)] static — read-only запрос
[Public, Remote(IsPure = true)]
public static IQueryable<IDeal> GetDealsByPipeline(long pipelineId)
{
  return Deals.GetAll().Where(d => d.Pipeline != null && d.Pipeline.Id == pipelineId);
}
```

```csharp
// Пример: DirRX.CRMSales.Server/Deal/DealServerFunctions.cs:14-27
// Паттерн: [Public, Remote] static — state-changing операция
[Public, Remote]
public static long CreateProposalFromDeal(long dealId)
{
  var deal = Deals.Get(dealId);
  if (deal == null)
    return 0;

  var proposal = DirRX.CRMDocuments.CommercialProposals.Create();
  proposal.Deal = deal;
  proposal.TotalAmount = deal.Amount;
  proposal.Name = deal.Name;
  proposal.Save();
  return proposal.Id;
}
```

### Паттерн B: Entity-level virtual (платформенный паттерн)

В базовых модулях Sungero `[Remote]` используется на `virtual` методах entity-level:

```csharp
// Паттерн из base/Sungero.Company — EntityServerFunctions
[Remote]
public virtual IQueryable<IAbsence> GetEmployeeAbsences(IEmployee employee)
{
  return Absences.GetAll(a => Equals(a.User, employee));
}
```

### Когда какой паттерн

| Паттерн | Когда использовать | Класс |
|---------|-------------------|-------|
| `[Public, Remote(IsPure = true)] static` | Read-only запросы, кэшируемые | `ModuleFunctions` или `{Entity}Functions` |
| `[Public, Remote] static` | Операции с побочными эффектами (Create, Save) | `ModuleFunctions` или `{Entity}Functions` |
| `[Remote] virtual` | Методы entity-level, которые можно перекрыть | `{Entity}Functions` (в entity scope) |
| `[Remote(IsPure = true), Public] static` | Проверка ролей/прав (кэшируемые) | `ModuleFunctions` |

### Правила

- `[Remote]` — вызов с клиента на сервер, создает сетевой запрос.
- `[Remote(IsPure = true)]` — результат кэшируется, функция не должна менять данные.
- `[Public]` — доступна из других модулей решения.
- `virtual` — для функций entity scope (можно перекрыть в наследниках).
- `static` — для функций module scope и entity-класса (в CRM это основной паттерн).

---

## 4. Сравнение сущностей: `Equals()` vs `.Id ==`

### Оба паттерна валидны, но используются в разных контекстах

**Паттерн A: `Equals()` — когда доступен объект сущности:**

```csharp
// Пример: DirRX.CRM.Server/ModuleWidgetHandlers.cs:23
// Используется когда сравниваем с ОБЪЕКТОМ (employee, pipeline, stage)
return query.Where(l =>
  l.Status == Sungero.CoreEntities.DatabookEntry.Status.Active &&
  Equals(l.Responsible, employee));
```

```csharp
// Пример: DirRX.CRMSales.Shared/Deal/DealHandlers.cs:23
// В Shared-коде с навигационным свойством
var firstStage = Stages.GetAll()
  .Where(s => Equals(s.Pipeline, _obj.Pipeline))
  .OrderBy(s => s.Position)
  .FirstOrDefault();
```

**Паттерн B: `.Id ==` — когда доступен только Id (long):**

```csharp
// Пример: DirRX.CRMSales.Server/ModuleServerFunctions.cs:18
// Используется когда параметр — long pipelineId (не объект)
return Deals.GetAll().Where(d => d.Pipeline != null && d.Pipeline.Id == pipelineId);
```

```csharp
// Пример: DirRX.CRM.Server/ModuleAsyncHandlers.cs:19
// В AsyncHandler — параметры всегда примитивы (long, string)
var deal = DirRX.CRMSales.Deals.GetAll().Where(d => d.Id == args.DealId).FirstOrDefault();
```

### Правила сравнения

| Контекст | Паттерн | Пример |
|----------|---------|--------|
| Сравнение с объектом entity | `Equals(a.Entity, entityObj)` | Widget, Shared, Client handlers |
| Фильтрация по Id (long параметр) | `a.Entity != null && a.Entity.Id == id` | ModuleServerFunctions, WebAPI, AsyncHandlers |
| Null-проверка entity | `entity == null` / `entity != null` | Везде (корректно работает) |
| Сравнение enum | `==` / `!=` | `_obj.Status == Status.Active` |

> **Важно:** При использовании `.Id ==` всегда добавляйте null-проверку навигационного свойства: `d.Pipeline != null && d.Pipeline.Id == pipelineId`.

---

## 5. Server Handlers

### Created

Вызывается при создании записи. Используется для установки значений по умолчанию.

```csharp
// Пример: DirRX.CRMSales.Server/Deal/DealHandlers.cs:11-17
public override void Created(Sungero.Domain.CreatedEventArgs e)
{
  base.Created(e);
  _obj.CreatedDate = Calendar.Today;
  _obj.Responsible = Sungero.Company.Employees.Current;
  _obj.Status = DirRX.CRMSales.Deal.Status.Active;
}
```

### BeforeSave

Вызывается перед сохранением. Используется для валидации.

```csharp
// Пример: DirRX.CRMSales.Server/Deal/DealHandlers.cs:19-41
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  base.BeforeSave(e);

  // Валидация обязательного текстового поля.
  if (string.IsNullOrEmpty(_obj.Name))
    e.AddError(_obj.Info.Properties.Name,
      DirRX.CRMSales.Deals.Resources.NameRequired);

  // Валидация обязательного навигационного свойства.
  if (_obj.Pipeline == null)
    e.AddError(_obj.Info.Properties.Pipeline,
      DirRX.CRMCommon.Resources.FieldRequiredFormat("Pipeline"));

  if (_obj.Stage == null)
    e.AddError(_obj.Info.Properties.Stage,
      DirRX.CRMCommon.Resources.FieldRequiredFormat("Stage"));

  // Условная обязательность: LossReason нужен для проигранных сделок.
  if (_obj.Stage != null && _obj.Stage.IsFinal == true &&
      _obj.Stage.IsWon != true && _obj.LossReason == null)
    e.AddError(_obj.Info.Properties.LossReason,
      DirRX.CRMSales.Deals.Resources.LossReasonRequired);
}
```

```csharp
// Пример: DirRX.CRMSales.Server/Stage/StageHandlers.cs:25-52
// BeforeSave с проверкой уникальности
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  base.BeforeSave(e);

  if (string.IsNullOrEmpty(_obj.Name))
    e.AddError(_obj.Info.Properties.Name,
      DirRX.CRMCommon.Resources.FieldRequiredFormat("Name"));

  // Уникальность Position в рамках Pipeline.
  if (_obj.Pipeline != null && _obj.Position.HasValue)
  {
    var duplicate = Stages.GetAll()
      .Where(s => Equals(s.Pipeline, _obj.Pipeline))
      .Where(s => s.Position == _obj.Position)
      .Where(s => s.Id != _obj.Id)
      .Any();
    if (duplicate)
      e.AddError(_obj.Info.Properties.Position,
        DirRX.CRMSales.Stages.Resources.PositionDuplicate);
  }
}
```

### AfterSave

Вызывается после успешной записи в БД (в транзакции).

```csharp
// Пример: DirRX.CRMSales.Server/Deal/DealHandlers.cs:43-53
public override void AfterSave(Sungero.Domain.AfterSaveEventArgs e)
{
  base.AfterSave(e);

  // Track stage change date.
  var stageChanged = _obj.State.Properties.Stage.IsChanged;
  if (stageChanged && _obj.Stage != null)
  {
    _obj.LastStageChangeDate = Calendar.Now;
  }
}
```

### Порядок вызова событий сохранения

```
Created → [пользователь заполняет] → BeforeSave → [Запись в БД] → AfterSave → [Commit] → Saved
```

- `Created` — начальные значения при создании.
- `BeforeSave` — валидация, можно отменить через `e.AddError()`.
- `AfterSave` — в транзакции, можно сохранять связанные сущности.
- `Saved` — вне транзакции, для уведомлений и побочных эффектов.

---

## 6. Shared Handlers (PropertyChanged)

Вызываются при изменении свойства. Работают и на клиенте, и на сервере.

```csharp
// Пример: DirRX.CRMSales.Shared/Deal/DealHandlers.cs:11-28
// Namespace — без .Server, без .Client
namespace DirRX.CRMSales
{
  partial class DealSharedHandlers
  {
    // Смена стадии → автоустановка вероятности.
    public virtual void StageChanged(DirRX.CRMSales.Shared.DealStageChangedEventArgs e)
    {
      if (_obj.Stage != null && _obj.Stage.Probability.HasValue)
        _obj.Probability = _obj.Stage.Probability;
    }

    // Смена воронки → сброс стадии на первую.
    public virtual void PipelineChanged(DirRX.CRMSales.Shared.DealPipelineChangedEventArgs e)
    {
      if (_obj.Pipeline != null)
      {
        var firstStage = Stages.GetAll()
          .Where(s => Equals(s.Pipeline, _obj.Pipeline))
          .OrderBy(s => s.Position)
          .FirstOrDefault();
        _obj.Stage = firstStage;
      }
    }
  }
}
```

```csharp
// Пример: DirRX.CRMMarketing.Shared/Lead/LeadHandlers.cs:9-31
// BANT-скоринг: при изменении любого boolean-флага пересчитать Score.
namespace DirRX.CRMMarketing
{
  partial class LeadSharedHandlers
  {
    public virtual void HasBudgetChanged(Sungero.Domain.Shared.BooleanPropertyChangedEventArgs e)
    {
      Functions.Lead.RecalcScore(_obj);
    }

    public virtual void HasAuthorityChanged(Sungero.Domain.Shared.BooleanPropertyChangedEventArgs e)
    {
      Functions.Lead.RecalcScore(_obj);
    }

    public virtual void HasNeedChanged(Sungero.Domain.Shared.BooleanPropertyChangedEventArgs e)
    {
      Functions.Lead.RecalcScore(_obj);
    }

    public virtual void HasTimelineChanged(Sungero.Domain.Shared.BooleanPropertyChangedEventArgs e)
    {
      Functions.Lead.RecalcScore(_obj);
    }
  }
}
```

```csharp
// Пример: DirRX.CRMSales.Shared/SalesPlan/SalesPlanHandlers.cs:9-28
// Авто-расчёт PeriodEnd при выборе типа периода.
public virtual void PeriodChanged(Sungero.Domain.Shared.EnumerationPropertyChangedEventArgs e)
{
  if (_obj.PeriodStart.HasValue && _obj.Period.HasValue)
  {
    var start = _obj.PeriodStart.Value;
    DateTime? periodEnd = null;
    if (_obj.Period == DirRX.CRMSales.SalesPlan.Period.Month)
      periodEnd = start.AddMonths(1).AddDays(-1);
    else if (_obj.Period == DirRX.CRMSales.SalesPlan.Period.Quarter)
      periodEnd = start.AddMonths(3).AddDays(-1);
    else if (_obj.Period == DirRX.CRMSales.SalesPlan.Period.Year)
      periodEnd = start.AddYears(1).AddDays(-1);
    if (periodEnd.HasValue)
      _obj.PeriodEnd = periodEnd;
  }
}
```

---

## 7. Client Handlers

### Refresh

Вызывается при каждом обновлении формы. **Запрещено вызывать Remote-функции!**

```csharp
// Паттерн из base/Sungero.Company
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  base.Refresh(e);

  // Управление обязательностью.
  _obj.State.Properties.BusinessUnit.IsRequired = true;

  // Управление видимостью.
  _obj.State.Properties.Name.IsVisible = _obj.Status == Status.Active;
}
```

### Showing

Вызывается один раз при открытии формы. **Запрещено вызывать Remote-функции!**

```csharp
// Паттерн из base/Sungero.Company
public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
{
  base.Showing(e);

  _obj.State.Properties.Department.IsVisible =
    !CallContext.CalledFrom(Departments.Info);
}
```

### Правила для Showing и Refresh

- **Никогда** не вызывать `[Remote]` функции — они вызываются при каждом обновлении.
- Данные для отображения кэшировать через `e.Params` / `_obj.Params`.
- Использовать только `_obj.State.Properties.*` для управления UI.

---

## 8. Actions (Client)

### Обычное действие (кнопка)

```csharp
// Пример: DirRX.CRMSales.ClientBase/Deal/DealActions.cs:11-23
public virtual void CreateProposal(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var proposal = DirRX.CRMDocuments.CommercialProposals.Create();
  proposal.Deal = _obj;
  proposal.TotalAmount = _obj.Amount;
  proposal.Name = _obj.Name;
  proposal.Show();
}

public virtual bool CanCreateProposal(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return !_obj.State.IsInserted;
}
```

### Действие с диалогом (ConvertLead)

```csharp
// Пример: DirRX.CRMMarketing.ClientBase/Lead/LeadActions.cs:11-74
public virtual void ConvertLead(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var dialog = Dialogs.CreateInputDialog(DirRX.CRMMarketing.Leads.Resources.ConvertLead);
  var pipelineField = dialog.AddSelect("Pipeline", true, DirRX.CRMSales.Pipelines.Null);
  if (dialog.Show() == DialogButtons.Ok)
  {
    var pipeline = pipelineField.Value;
    var firstStage = DirRX.CRMSales.Stages.GetAll()
      .Where(s => Equals(s.Pipeline, pipeline))
      .OrderBy(s => s.Position)
      .FirstOrDefault();

    var deal = DirRX.CRMSales.Deals.Create();
    deal.Name = _obj.CompanyName ?? _obj.Name;
    deal.Pipeline = pipeline;
    deal.Stage = firstStage;
    // ... создание Company, Contact ...
    deal.Save();

    _obj.LeadStatus = DirRX.CRMMarketing.Lead.LeadStatus.Converted;
    _obj.ConvertedDeal = deal;
    _obj.Save();
    deal.Show();
  }
}

public virtual bool CanConvertLead(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return _obj.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.Qualified &&
         !_obj.State.IsInserted;
}
```

### Действие открытия документов

```csharp
// Пример: DirRX.CRMSales.ClientBase/Deal/DealActions.cs:69-93
public virtual void ShowDealDocuments(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var proposals = DirRX.CRMDocuments.CommercialProposals.GetAll()
    .Where(p => p.Deal != null && Equals(p.Deal, _obj));
  var invoices = DirRX.CRMDocuments.Invoices.GetAll()
    .Where(i => i.Deal != null && Equals(i.Deal, _obj));

  var documents = new List<Sungero.Content.IElectronicDocument>();
  documents.AddRange(proposals.Cast<Sungero.Content.IElectronicDocument>());
  documents.AddRange(invoices.Cast<Sungero.Content.IElectronicDocument>());

  if (documents.Any())
    documents.AsQueryable().Show();
  else
    Dialogs.ShowMessage("Нет документов", "...", MessageType.Information);
}
```

### Правила Actions

- `Can*` — определяет доступность кнопки. Не должен содержать тяжелой логики.
- `Execute*` — логика выполнения. Может вызывать `[Remote]` функции.
- `e.AddError()` — показать ошибку пользователю.
- `e.Cancel()` — отменить действие без ошибки.

---

## 9. AsyncHandlers

Асинхронные обработчики с полноценной обработкой блокировок и retry.

### Вызов из Server кода

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:126-128
var asyncHandler = DirRX.CRM.AsyncHandlers.DealStageChanged.Create();
asyncHandler.DealId = deal.Id;
asyncHandler.ExecuteAsync();
```

### Обработчик с блокировками (production-паттерн из CRM)

```csharp
// Пример: DirRX.CRM.Server/ModuleAsyncHandlers.cs:15-70
public virtual void DealStageChanged(
  DirRX.CRM.Server.AsyncHandlerInvokeArgs.DealStageChangedInvokeArgs args)
{
  Logger.DebugFormat("DealStageChanged. Start. DealId = {0}", args.DealId);

  var deal = DirRX.CRMSales.Deals.GetAll().Where(d => d.Id == args.DealId).FirstOrDefault();
  if (deal == null)
  {
    Logger.DebugFormat("DealStageChanged. Deal with Id {0} not found.", args.DealId);
    args.Retry = false;
    return;
  }

  // Шаг 1: предварительная проверка блокировки (без попытки захвата).
  if (Locks.GetLockInfo(deal).IsLocked)
  {
    Logger.DebugFormat("DealStageChanged. Deal {0} is locked. Retry.", args.DealId);
    args.Retry = true;
    return;
  }

  try
  {
    // Шаг 2: попытка захвата блокировки.
    if (!Locks.TryLock(deal))
    {
      args.Retry = true;
      return;
    }

    // Бизнес-логика.
    if (deal.Stage != null && deal.Stage.IsFinal == true && deal.Stage.IsWon == true)
    {
      if (deal.Responsible != null)
      {
        var salesPlans = DirRX.CRMSales.SalesPlans.GetAll()
          .Where(p => p.Employee != null && p.Employee.Id == deal.Responsible.Id)
          .Where(p => p.Status == Sungero.CoreEntities.DatabookEntry.Status.Active);
        foreach (var plan in salesPlans)
          ModuleFunctions.RecalculateSalesPlanActual(plan.Id);
      }
    }

    deal.Save();
    Logger.DebugFormat("DealStageChanged. End. DealId = {0}", args.DealId);
  }
  catch (Exception ex)
  {
    Logger.ErrorFormat("DealStageChanged. Error: {0}", ex.Message);
    args.Retry = true;
  }
  finally
  {
    // Шаг 3: разблокировка только если заблокировано текущим пользователем.
    if (Locks.GetLockInfo(deal).IsLockedByMe)
      Locks.Unlock(deal);
  }
}
```

### Обработчик без блокировки (выдача прав)

```csharp
// Пример: DirRX.CRM.Server/ModuleAsyncHandlers.cs:75-117
public virtual void GrantDealRights(
  DirRX.CRM.Server.AsyncHandlerInvokeArgs.GrantDealRightsInvokeArgs args)
{
  Logger.DebugFormat("GrantDealRights. Start. DealId = {0}, UserId = {1}", args.DealId, args.UserId);

  var deal = DirRX.CRMSales.Deals.GetAll().Where(d => d.Id == args.DealId).FirstOrDefault();
  if (deal == null) { args.Retry = false; return; }

  var user = Sungero.Company.Employees.GetAll().Where(e => e.Id == args.UserId).FirstOrDefault();
  if (user == null) { args.Retry = false; return; }

  if (Locks.GetLockInfo(deal).IsLocked) { args.Retry = true; return; }

  try
  {
    if (!deal.AccessRights.IsGranted(DefaultAccessRightsTypes.Change, user))
    {
      deal.AccessRights.Grant(user, DefaultAccessRightsTypes.Change);
      deal.AccessRights.Save();
    }
  }
  catch (Exception ex)
  {
    Logger.ErrorFormat("GrantDealRights. Error: {0}", ex.Message);
    args.Retry = true;
  }
}
```

### Обработчик отправки уведомления

```csharp
// Пример: DirRX.CRM.Server/ModuleAsyncHandlers.cs:122-162
public virtual void NotifyStaleDeal(
  DirRX.CRM.Server.AsyncHandlerInvokeArgs.NotifyStaleDealInvokeArgs args)
{
  Logger.DebugFormat("NotifyStaleDeal. Start. DealId = {0}, ManagerId = {1}", args.DealId, args.ManagerId);

  var deal = DirRX.CRMSales.Deals.GetAll().Where(d => d.Id == args.DealId).FirstOrDefault();
  if (deal == null) { args.Retry = false; return; }

  var manager = Sungero.Company.Employees.GetAll().Where(e => e.Id == args.ManagerId).FirstOrDefault();
  if (manager == null) { args.Retry = false; return; }

  try
  {
    var task = Sungero.Workflow.SimpleTasks.Create();
    task.Subject = DirRX.CRM.Resources.StaleDealNotificationSubjectFormat(deal.Name);
    task.ActiveText = DirRX.CRM.Resources.StaleDealNotificationTextFormat(deal.Name, deal.Stage?.Name);

    var routeStep = task.RouteSteps.AddNew();
    routeStep.AssignmentType = Sungero.Workflow.SimpleTaskRouteSteps.AssignmentType.Notice;
    routeStep.Performer = manager;
    routeStep.Deadline = null;
    task.Start();
  }
  catch (Exception ex)
  {
    Logger.ErrorFormat("NotifyStaleDeal. Error: {0}", ex.Message);
    args.Retry = true;
  }
}
```

### Правила AsyncHandler

- Всегда проверять существование сущности (`.FirstOrDefault()` + `== null`).
- Если не нашли — `args.Retry = false` (нет смысла повторять).
- `Locks.GetLockInfo().IsLocked` — предварительная проверка ДО `TryLock`.
- `Locks.TryLock()` — захват блокировки перед изменением.
- `Locks.GetLockInfo().IsLockedByMe` в `finally` — разблокировка.
- При ошибке — `args.Retry = true`.
- Логировать начало, ошибки, завершение через `Logger.DebugFormat` / `Logger.ErrorFormat`.

---

## 10. Cover Functions (Client)

В CRM обложки реализованы как обычные `virtual void` методы в `ModuleClientFunctions.cs`.

```csharp
// Пример: DirRX.CRM.ClientBase/ModuleClientFunctions.cs:16-18
// Открыть CRM SPA приложение.
public virtual void OpenCRMApp()
{
  Hyperlinks.Open(ClientApplication.ApplicationUri.GetLeftPart(System.UriPartial.Authority) + "/Client/content/crm/");
}
```

```csharp
// Пример: DirRX.CRM.ClientBase/ModuleClientFunctions.cs:24-29
// Открыть список активных сделок.
public virtual void OpenDeals()
{
  DirRX.CRMSales.Deals.GetAll()
    .Where(d => d.Status == Sungero.CoreEntities.DatabookEntry.Status.Active)
    .Show();
}
```

```csharp
// Пример: DirRX.CRM.ClientBase/ModuleClientFunctions.cs:84-100
// Открыть диалог отчёта "Воронка продаж".
public virtual void OpenSalesFunnelReport()
{
  var dialog = Dialogs.CreateInputDialog(DirRX.CRM.Resources.ReportTitle_SalesFunnel);
  var pipeline = dialog.AddSelect(DirRX.CRM.Resources.CoverAction_OpenPipelines, true, DirRX.CRMSales.Pipelines.Null);
  var periodStart = dialog.AddDate(DirRX.CRM.Resources.CoverAction_OpenSalesFunnelReport, false);
  var periodEnd = dialog.AddDate(DirRX.CRM.Resources.CoverAction_OpenPlanFactReport, false);

  if (dialog.Show() == DialogButtons.Ok)
  {
    var pipelineId = pipeline.Value != null ? pipeline.Value.Id : 0;
    DirRX.CRMSales.Deals.GetAll()
      .Where(d => d.Pipeline != null && d.Pipeline.Id == pipelineId)
      .Show();
  }
}
```

### Правила Cover Functions

- Функции обложки всегда `virtual void` без атрибутов.
- Для открытия списков: `GetAll().Where(...).Show()`.
- Для получения/создания сущности: `.Create()` → `.Show()` или Remote-функция → `.Show()`.
- Для диалогов: `Dialogs.CreateInputDialog()` с проверкой `dialog.Show() == DialogButtons.Ok`.

---

## 11. WebAPI endpoint (Module-level)

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:16-83
// GET endpoint — получить данные канбан-доски.
[Public(WebApiRequestType = RequestType.Get)]
public string LoadPipeline(long pipelineId, long responsibleId, string periodStart, string periodEnd)
{
  if (!HasCRMAccess())
    return "{\"error\":\"Access denied\"}";

  var pipeline = DirRX.CRMSales.Pipelines.Get(pipelineId);
  if (pipeline == null)
    return string.Empty;

  var stages = DirRX.CRMSales.Stages.GetAll()
    .Where(s => s.Pipeline != null && s.Pipeline.Id == pipelineId)
    .OrderBy(s => s.Position)
    .ToList();

  // Build DTOs.
  var stageDtos = new List<Structures.Module.IStageDto>();
  foreach (var stage in stages)
  {
    var dto = Structures.Module.StageDto.Create();
    dto.Id = stage.Id;
    dto.Name = stage.Name;
    // ...
    stageDtos.Add(dto);
  }

  return PipelineDtoToJson(pipelineDto);
}
```

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:88-131
// POST endpoint — переместить сделку на новый этап.
[Public(WebApiRequestType = RequestType.Post)]
public string MoveDealToStage(long dealId, long newStageId, int position)
{
  if (!HasCRMAccess())
    return "{\"error\":\"Access denied\"}";

  var deal = DirRX.CRMSales.Deals.Get(dealId);
  if (deal == null)
    return string.Empty;

  deal.Stage = DirRX.CRMSales.Stages.Get(newStageId);
  deal.Save();

  // Fire async handler.
  var asyncHandler = DirRX.CRM.AsyncHandlers.DealStageChanged.Create();
  asyncHandler.DealId = deal.Id;
  asyncHandler.ExecuteAsync();

  return DealDtoToJson(BuildDealDto(deal));
}
```

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:354-377
// POST endpoint — возвращает List<IStructure>.
[Public(WebApiRequestType = RequestType.Post)]
public List<Structures.Module.IProposalInfo> GetActiveProposals(long employeeId)
{
  if (!HasCRMAccess())
    return new List<Structures.Module.IProposalInfo>();

  var result = new List<Structures.Module.IProposalInfo>();
  var proposals = DirRX.CRMDocuments.CommercialProposals.GetAll()
    .Where(p => p.Author != null && p.Author.Id == employeeId)
    .ToList();

  foreach (var p in proposals)
  {
    var info = Structures.Module.ProposalInfo.Create();
    info.Id = p.Id;
    info.Name = p.Name;
    info.Amount = p.TotalAmount ?? 0;
    result.Add(info);
  }
  return result;
}
```

### Правила WebAPI

- `[Public(WebApiRequestType = RequestType.Get)]` для GET, `RequestType.Post` для POST.
- Параметры — примитивы (long, string, int, double). Нет поддержки JSON body.
- Всегда проверять доступ (роли/права) в начале метода.
- Возвращать `string` (JSON) для сложных DTO или `List<Structures.Module.ITypeName>` для простых.
- Null-проверка каждого входного параметра.

---

## 12. Widget Handlers (Server + Client)

### Server: фильтрация данных виджета

```csharp
// Пример: DirRX.CRM.Server/ModuleWidgetHandlers.cs:9-24
// Counter widget — фильтрация активных лидов текущего пользователя.
partial class ActiveLeadsCounterWidgetHandlers
{
  public virtual IQueryable<DirRX.CRMMarketing.ILead> ActiveLeadsCounterActiveLeadsFiltering(
      IQueryable<DirRX.CRMMarketing.ILead> query)
  {
    var employee = Sungero.Company.Employees.As(Users.Current);
    if (employee == null)
      return query.Where(l => false);

    return query.Where(l =>
      l.Status == Sungero.CoreEntities.DatabookEntry.Status.Active &&
      (l.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.NewLead ||
       l.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.InProgress ||
       l.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.Qualified) &&
      Equals(l.Responsible, employee));
  }
}
```

### Server: BarChart widget с данными

```csharp
// Пример: DirRX.CRM.Server/ModuleWidgetHandlers.cs:73-123
// Горизонтальная диаграмма воронки: стеки urgent (красный) / normal.
public virtual void GetDealsPipelineChartPipelineChartValue(Sungero.Domain.GetWidgetBarChartValueEventArgs e)
{
  var employee = Sungero.Company.Employees.As(Users.Current);
  if (employee == null)
    return;

  var activeDeals = DirRX.CRMSales.Deals.GetAll()
    .Where(d => d.Status == Sungero.CoreEntities.DatabookEntry.Status.Active &&
                d.Stage.IsFinal != true &&
                Equals(d.Responsible, employee))
    .ToList();

  var today = Calendar.UserToday;
  var deadlineThreshold = today.AddDays(7);

  var stageGroups = activeDeals
    .GroupBy(d => d.Stage)
    .OrderBy(g => g.Key.Position)
    .ToList();

  e.Chart.IsLegendVisible = true;

  foreach (var group in stageGroups)
  {
    var series = e.Chart.AddNewSeries(group.Key.Id.ToString(), group.Key.Name);
    series.AddValue("urgent", "Срочные", urgentCount, Colors.Charts.Red);
    series.AddValue("normal", "Обычные", normalCount, Colors.Charts.Color4);
  }
}
```

### Client: обработчик клика по виджету

```csharp
// Пример: DirRX.CRM.ClientBase/ModuleWidgetHandlers.cs:9-30
// Клик по столбцу — открыть список сделок стадии.
public virtual void ExecuteDealsPipelineChartPipelineChartAction(
  Sungero.Domain.Client.ExecuteWidgetBarChartActionEventArgs e)
{
  long stageId;
  if (!long.TryParse(e.SeriesId, out stageId))
    return;

  var stage = DirRX.CRMSales.Stages.Get(stageId);
  if (stage == null)
    return;

  DirRX.CRMSales.Deals.GetAll()
    .Where(d => d.Status == Sungero.CoreEntities.DatabookEntry.Status.Active &&
                Equals(d.Stage, stage))
    .Show();
}
```

---

## 13. Initializer (ModuleInitializer)

### Точка входа

```csharp
// Пример: DirRX.CRM.Server/ModuleInitializer.cs:12-18
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  // v7.0 — First initialization (roles, document kinds, rights, default data).
  FirstInitializing();
  // v8.0 — WebAPI endpoints, enhanced initialization.
  InitializingV80();
}
```

### Создание ролей (идемпотентное)

```csharp
// Пример: DirRX.CRM.Server/ModuleInitializer.cs:50-84
public static void CreateRoles()
{
  InitializationLogger.Debug("Init: Create CRM roles.");

  CreateRole(Constants.Module.Roles.CRMAdmin,
    DirRX.CRM.Resources.RoleName_CRMAdmin,
    DirRX.CRM.Resources.RoleDescription_CRMAdmin);

  CreateRole(Constants.Module.Roles.SalesManager,
    DirRX.CRM.Resources.RoleName_SalesManager,
    DirRX.CRM.Resources.RoleDescription_SalesManager);
}

private static void CreateRole(Guid roleGuid, string roleName, string roleDescription)
{
  // Идемпотентность: проверка по Sid.
  if (Roles.GetAll().Where(r => r.Sid == roleGuid).Any())
    return;

  InitializationLogger.DebugFormat("Init: Creating role '{0}'.", roleName);
  Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(
    roleName, roleDescription, roleGuid);
}
```

### Создание видов документов (идемпотентное)

```csharp
// Пример: DirRX.CRM.Server/ModuleInitializer.cs:105-128
private static void CreateCommercialProposalKind()
{
  var kindName = DirRX.CRM.Resources.DocumentKind_CommercialProposal;
  // Идемпотентность: проверка по имени.
  if (Sungero.Docflow.DocumentKinds.GetAll().Where(k => k.Name == kindName).Any())
    return;

  var kind = Sungero.Docflow.DocumentKinds.Create();
  kind.Name = kindName;
  kind.ShortName = kindName;
  kind.DocumentFlow = Sungero.Docflow.DocumentKind.DocumentFlow.Inner;
  kind.NumberingType = Sungero.Docflow.DocumentKind.NumberingType.NotNumerable;
  kind.GenerateDocumentName = true;

  var docType = Sungero.Docflow.DocumentTypes.GetAll()
    .Where(t => t.DocumentTypeGuid == Constants.Module.DocumentTypeGuids.CommercialProposal)
    .FirstOrDefault();
  if (docType != null)
    kind.DocumentType = docType;

  kind.Save();
}
```

### Правила Initializer

- Использовать `InitializationLogger`, а не `Logger`.
- Проверять существование данных перед созданием (идемпотентность: по Sid, Name, Guid).
- Ресурсы модуля — полный путь: `DirRX.CRM.Resources.RoleName_CRMAdmin`.
- `using Sungero.Domain.Initialization;` — обязательно для `InitializationLogger`.

---

## 14. IsPublic Structures — используем интерфейсы

Структуры с `IsPublic: true` в Module.mtd генерируют **интерфейсы** (`ITypeName`).

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:47-63
// ПРАВИЛЬНО: IStageDto (интерфейс), StageDto.Create() возвращает IStageDto.
var stageDtos = new List<Structures.Module.IStageDto>();
var dto = Structures.Module.StageDto.Create();
dto.Id = stage.Id;
dto.Name = stage.Name;
stageDtos.Add(dto);
```

```csharp
// Пример: DirRX.CRM.Server/ModuleServerFunctions.cs:160-172
// ПРАВИЛЬНО: IFunnelStageData в List<>.
var funnelData = new List<Structures.Module.IFunnelStageData>();
var data = Structures.Module.FunnelStageData.Create();
data.StageName = stage.Name;
data.Count = stageDeals.Count();
funnelData.Add(data);
```

```csharp
// НЕПРАВИЛЬНО:
List<Structures.Module.FunnelStageData> result; // CS0029: Cannot convert
```

---

## 15. Enum-свойства сущностей

В Sungero enum-свойства имеют тип `Sungero.Core.Enumeration?`. Сравнение — через `==`.

```csharp
// Пример: DirRX.CRMMarketing.Server/Lead/LeadHandlers.cs:14
// Enum сравнение.
_obj.LeadStatus = DirRX.CRMMarketing.Lead.LeadStatus.NewLead;

if (_obj.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.Converted &&
    _obj.ConvertedDeal == null)
  e.AddError(...);
```

```csharp
// Пример: DirRX.CRMMarketing.Server/Lead/LeadServerFunctions.cs:100
// Фильтрация по Enumeration через new Enumeration(string).
return Leads.GetAll().Where(l => l.LeadStatus != null &&
  l.LeadStatus.Value == new Enumeration(statusCode));
```

**Правила:**
- Тип параметра функции: `Sungero.Core.Enumeration` (НЕ static class).
- Сравнение: `_obj.EnumProp == Entity.EnumProp.Value` (через `==`).
- Фильтрация в LINQ: `l.EnumProp.Value == new Enumeration("Code")` для динамического значения.

---

## 16. Антипаттерны (что ЗАПРЕЩЕНО)

### Таблица запрещенных паттернов

| Запрещено | Правильно | Причина |
|-----------|-----------|---------|
| `entity is IEmployee` | `Employees.Is(entity)` | NHibernate прокси не совместимы с `is` |
| `entity as IEmployee` | `Employees.As(entity)` | NHibernate прокси не совместимы с `as` |
| `(IEmployee)entity` | `Employees.As(entity)` | Прямой каст не работает с прокси |
| `DateTime.Now` | `Calendar.Now` | Платформа управляет серверным временем |
| `DateTime.Today` | `Calendar.Today` | Платформа управляет серверным временем |
| `DateTime.UtcNow` | `Calendar.Now` | Платформа управляет часовыми поясами |
| `new Tuple<>()` | `Structures.Module.Create()` | Ограничение платформы |
| Анонимные типы `new { }` в Remote | Структуры | Запрещено платформой для Remote |
| `System.Threading` | AsyncHandlers | Многопоточность запрещена |
| `System.Reflection` | -- | Запрещено платформой |
| `GetAll()` без `Where()` | `GetAll().Where(...)` | Загрузка всей таблицы |
| `.ToList()` на больших выборках | `IQueryable<T>` | Материализация миллионов записей |
| `[Remote]` в Showing/Refresh | `e.Params` / кэш | Showing/Refresh вызываются часто |
| Диалоги в Server-коде | Только в Client | На сервере нет UI |
| Create/Delete/SQL в Client-коде | Только в Server | На клиенте нет доступа к БД |
| Хардкод русских строк | `.resx` ресурсы | Нарушение локализации |
| `d.Pipeline.Id == pipelineId` без null-check | `d.Pipeline != null && d.Pipeline.Id == pipelineId` | NullReferenceException |

> **Примечание:** `new { }` анонимные типы допустимы в коде ПОСЛЕ `.ToList()` (в memory), как в `DirRX.CRM.Server/ModuleWidgetHandlers.cs:93`. Запрещены только в LINQ-to-DB и как возвращаемые типы Remote-функций.

### Типичные ошибки

```csharp
// НЕПРАВИЛЬНО: Remote в Refresh.
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  var data = Functions.MyEntity.Remote.GetData(_obj); // Сетевой вызов при каждом обновлении!
}

// ПРАВИЛЬНО: кэш через Params.
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  bool hasData;
  if (!e.Params.TryGetValue("HasData", out hasData))
    return;
  _obj.State.Properties.Name.IsVisible = hasData;
}
```

```csharp
// НЕПРАВИЛЬНО: каст через is/as.
if (entity is IEmployee)
  var emp = entity as IEmployee;

// ПРАВИЛЬНО: через менеджер.
// Пример: DirRX.CRM.Server/ModuleWidgetHandlers.cs:14
var employee = Sungero.Company.Employees.As(Users.Current);
if (employee == null)
  return query.Where(l => false);
```

---

## Паттерны из ESM (production-решение rosa.ESM v1.6.261)

### BlockHandler — сигнатуры

```csharp
// Подтверждено из ESM + archive/base
namespace Company.Module.Server.TaskNameBlocks
{
  partial class BlockNameHandlers
  {
    // Assignment block — StartAssignment
    public virtual void BlockNameStartAssignment(Company.Module.IAssignmentType assignment)
    {
      assignment.Subject = _obj.Subject;
      assignment.Deadline = _obj.MaxDeadline;
    }

    // Assignment block — CompleteAssignment
    public virtual void BlockNameCompleteAssignment(Company.Module.IAssignmentType assignment)
    {
      var result = assignment.Result;
    }

    // Assignment block — End
    public virtual void BlockNameEnd(IEnumerable<Company.Module.IAssignmentType> createdAssignments)
    {
      var last = createdAssignments.OrderByDescending(s => s.Created).FirstOrDefault();
      _block.OutProperties.PropName = last.PropName;
    }
  }

  // Script block — Execute
  partial class ScriptBlockHandlers
  {
    public virtual void ScriptBlockExecute()
    {
      _block.RetrySettings.Retry = false;
      var author = Sungero.Company.Employees.As(_block.Author);
    }
  }
}
```

**Доступные в блоке контексты:**
- `_obj` — задача (родительская)
- `_block` — конфигурация блока
- `_block.OutProperties.{Prop}` — выходные свойства
- `_block.RetrySettings.Retry` — управление повтором

### CreatingFrom Handler

```csharp
// Подтверждено из ESM
partial class EntityCreatingFromServerHandler
{
  public override void CreatingFrom(Sungero.Domain.CreatingFromEventArgs e)
  {
    base.CreatingFrom(e);
    e.Without(_info.Properties.RegistrationDate);  // Исключить свойство
    e.Map(_info.Properties.RequestPriority, defaultPriority);  // Маппинг значения
    e.Params.AddOrUpdate(Constants.Entity.RequestLinkId, _source.Id);
  }
}
```

---

## 17. SQL Queries (Queries.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<queries>
  <query key="GetCount">
    <default><![CDATA[select count(*) from MyTable where Status = {0}]]></default>
  </query>
  <query key="UpdateField">
    <mssql><![CDATA[update MyTable set Field = {1} where Id = {0}]]></mssql>
    <postgres><![CDATA[update MyTable set Field = {1} where Id = {0}]]></postgres>
  </query>
</queries>
```

### Правила SQL

- Параметры через `{0}`, `{1}` — платформа экранирует их.
- Секции `<mssql>` и `<postgres>` при различии синтаксиса.
- `<default>` — когда синтаксис универсален.
- Всегда оборачивать в `CDATA`.

---

## 18. AttachmentGroups — доступ к вложениям задач

```csharp
// ПРАВИЛЬНО — Server-код:
task.Attachments.Add(document);
var doc = _obj.Attachments.Where(a => Documents.Is(a))
  .Select(a => Documents.As(a)).FirstOrDefault();

// ПРАВИЛЬНО — Client-код (видимость группы):
_obj.State.Attachments.GroupName.IsVisible = true;

// НЕПРАВИЛЬНО:
task.AttachmentGroups.GroupName.Attach(document); // нет такого свойства
```

---

## Сводка: чеклист перед написанием кода

1. **Using** — 5 стандартных + `Sungero.Domain.Initialization` для Initializer.
2. **Namespace** — `{Company}.{Module}.Server` для ServerFunctions; `{Company}.{Module}` для ServerHandlers/SharedHandlers.
3. **Сравнение сущностей** — `Equals()` с объектом, `.Id ==` с long (+ null-check навигации).
4. **Приведение типов** — `Employees.Is()` / `Employees.As()`, не `is` / `as`.
5. **Даты** — `Calendar.Now` / `Calendar.Today` / `Calendar.UserToday`.
6. **Remote** — `static` + `[Public, Remote(IsPure = true)]` для read-only; `[Public, Remote]` для state-changing.
7. **Remote** — не вызывать из Showing/Refresh.
8. **GetAll()** — всегда с `Where()`.
9. **Фильтрация** — возвращать `IQueryable<T>`.
10. **AsyncHandler** — `Create()` -> set params -> `ExecuteAsync()`; в обработчике: null-check, IsLocked, TryLock, `args.Retry`.
11. **Строки** — только из `.resx`, не хардкодить.
12. **WebAPI** — `[Public(WebApiRequestType = RequestType.Post)]` на module-level.
13. **Structures** — `ITypeName` (интерфейс), `Create()` возвращает интерфейс.
14. **Initializer** — ресурсы `{Company}.{Module}.Resources.Key`, идемпотентность через проверку существования.
15. **Cover Functions** — `virtual void` без атрибутов, в `ModuleClientFunctions.cs`.
16. **Widget Handlers** — Server фильтрация IQueryable, Client действие по клику.
17. **Shared Handlers** — PropertyChanged для автозаполнения, namespace без `.Server`/`.Client`.
