---
description: "Создать InputDialog с полями, валидацией и связями для действия Directum RX"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание InputDialog для действия Directum RX

## ШАГ 0: Посмотри рабочий пример

**Эталон: LeadActions.ConvertLead** — диалог с выбором Pipeline для конвертации лида в сделку.

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **Dialog + Action** | `DirRX.CRMMarketing/DirRX.CRMMarketing.ClientBase/Lead/LeadActions.cs` |
| **Cover Actions (без диалога)** | `DirRX.CRM/DirRX.CRM.ClientBase/ModuleClientFunctions.cs` |

**Реальный диалог из LeadActions.cs — ConvertLead:**
```csharp
namespace DirRX.CRMMarketing.Client
{
  partial class LeadActions
  {
    public virtual void ConvertLead(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      // Show dialog to select Pipeline.
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

        // ... создание Company, Contact, привязка к Deal
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
  }
}
```

**Ключевые паттерны из CRM:**
- `Dialogs.CreateInputDialog(Resources.Title)` — заголовок из ресурсов
- `dialog.AddSelect("Label", required, Entity.Null)` — выбор сущности
- `dialog.Show() == DialogButtons.Ok` — проверка подтверждения
- `CanAction` — проверка статуса + `!_obj.State.IsInserted`

Перед созданием нового диалога — **обязательно прочитай** `LeadActions.cs` и адаптируй.

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `check_code_consistency` — проверка согласованности MTD Actions и C# обработчиков
- `check_package` — валидация пакета после создания
- `sync_resx_keys` — синхронизация ключей resx (Action_ ключи для действий)
- `extract_entity_schema` — просмотр схемы сущности для получения Actions и PropertyGuid
- `search_metadata` — поиск эталонных Actions/Dialogs в платформе

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля
- **EntityName** — имя сущности (для Actions) или Module (для модульных функций)
- **ActionName** — имя действия (PascalCase)
- **DialogTitle** — заголовок (из Resources)
- **Fields** — список полей диалога (тип, обязательность, значение по умолчанию)
- **LinkedFields** — связи между полями (например, Department → Employee)
- **ServerCall** — какую Remote функцию вызвать после подтверждения

## Типы полей InputDialog

### Select (выбор сущности)
```csharp
var field = dialog.AddSelect(Employees.Info.LocalizedName, true, Employees.Current);
field.From(queryable);  // ограничить список
```

### String
```csharp
var field = dialog.AddString(Resources.CommentLabel, false, string.Empty);
field.MaxLength(500);
```

### Date
```csharp
var field = dialog.AddDate(Resources.DeadlineLabel, true, Calendar.Today.AddDays(3));
```

### Boolean
```csharp
var field = dialog.AddBoolean(Resources.NeedApprovalLabel, false);
```

### Integer / Double
```csharp
var field = dialog.AddInteger(Resources.CountLabel, true, 1);
var field = dialog.AddDouble(Resources.AmountLabel, false, 0.0);
```

### Hyperlink
```csharp
dialog.AddHyperlink(Resources.HelpLinkLabel);
```

## Шаблон — простой диалог (Action)

```csharp
// В {Entity}Actions.cs (ClientBase)
namespace {Company}.{Module}.Client
{
  partial class {Entity}Actions
  {
    public virtual void {ActionName}(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      var dialog = Dialogs.CreateInputDialog({Entity}s.Resources.{ActionName}Title);

      var assigneeField = dialog.AddSelect(
        Sungero.Company.Employees.Info.LocalizedName, true, Sungero.Company.Employees.Current);

      var commentField = dialog.AddString({Entity}s.Resources.CommentLabel, false, string.Empty);

      if (dialog.Show() == DialogButtons.Ok)
      {
        _obj.Save();
        Functions.{Entity}.Remote.{ServerMethod}(_obj, assigneeField.Value, commentField.Value);
        e.CloseFormAfterAction = false;
      }
    }

    public virtual bool Can{ActionName}(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return _obj.State.IsInserted == false;
    }
  }
}
```

## Шаблон — диалог со связанными полями (ESM паттерн)

```csharp
public virtual void {ActionName}(Sungero.Workflow.Client.ExecuteResultActionArgs e)
{
  var departments = Sungero.Company.Departments.GetAll(s => s.Status == Status.Active);
  var employees = Sungero.Company.Employees.GetAll(s => s.Status == Status.Active);

  var dialog = Dialogs.CreateInputDialog(_obj.Info.Actions.{ActionName}.LocalizedName);

  var selectedDepartment = dialog.AddSelect(
    Sungero.Company.Departments.Info.LocalizedName, false, Sungero.Company.Departments.Null);
  selectedDepartment.From(departments);

  var selectedEmployee = dialog.AddSelect(
    Sungero.Company.Employees.Info.LocalizedName, true, Sungero.Company.Employees.Null);
  selectedEmployee.From(employees);

  // Связь: при смене подразделения → фильтровать сотрудников
  selectedDepartment.SetOnValueChanged((dep) =>
  {
    if (dep.NewValue != null && dep.NewValue != dep.OldValue)
    {
      if (selectedEmployee.Value != null && selectedEmployee.Value.Department != dep.NewValue)
        selectedEmployee.Value = null;
      selectedEmployee.From(employees.Where(s => Equals(s.Department, dep.NewValue)));
    }
    else
    {
      selectedEmployee.From(employees);
      selectedEmployee.Value = null;
    }
  });

  // Обратная связь: при выборе сотрудника → подставить подразделение
  selectedEmployee.SetOnValueChanged((emp) =>
  {
    if (emp.NewValue != null)
      selectedDepartment.Value = emp.NewValue.Department;
  });

  if (dialog.Show() == DialogButtons.Ok)
  {
    _obj.ResponsiblePerson = selectedEmployee.Value;
  }
  if (dialog.IsCanceled)
  {
    e.Cancel();
  }
}
```

## Шаблон — диалог из ExecuteResultAction (задание)

```csharp
// Для кнопок задания используется ExecuteResultActionArgs
public virtual void Forward(Sungero.Workflow.Client.ExecuteResultActionArgs e)
{
  var dialog = Dialogs.CreateInputDialog(Resources.ForwardTitle);
  var performer = dialog.AddSelect(Employees.Info.LocalizedName, true, Employees.Null);

  if (dialog.Show() == DialogButtons.Ok)
  {
    _obj.ForwardTo = performer.Value;
  }
  else
  {
    e.Cancel();
  }
}
```

## Важные паттерны

### Params — передача данных между диалогом и Refresh
```csharp
// В Action:
e.Params.AddOrUpdate("NeedRefresh", true);

// В Refresh handler:
bool needRefresh;
if (e.Params.TryGetValue("NeedRefresh", out needRefresh) && needRefresh)
  e.Params.Remove("NeedRefresh");
```

### ShowMessage — простое уведомление (без диалога)
```csharp
Dialogs.ShowMessage(Resources.SuccessMessage, MessageType.Information);
Dialogs.NotifyMessage(Resources.InfoText);
```

### Custom Dialog (ESM паттерн)
```csharp
var customDialog = _obj.SendRequestDialog;
if (customDialog != null)
  Dialogs.ShowCustomDialog(customDialog);
else
{
  var baseDialog = CustomDialogs.GetAll(d => d.Uuid == Constants.Module.DefaultDialogGuid).FirstOrDefault();
  if (baseDialog != null)
    Dialogs.ShowCustomDialog(baseDialog);
}
```

## Валидация

```csharp
// В Action (CanExecute):
public virtual bool Can{ActionName}(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  var employee = Sungero.Company.Employees.Current;
  return employee != null
    && _obj.State.IsInserted == false
    && employee.IncludedIn(Constants.Module.RolesGroup.Managers);
}

// С проверкой лицензии через Params:
bool hasLicense;
e.Params.TryGetValue(Constants.Module.HasActiveLicense, out hasLicense);
return hasLicense && canUpdate;
```

## MCP валидация (ПОСЛЕ создания)

После добавления Action в MTD и обработчиков в .cs:
```
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: check_package packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```

## Что создаётся / обновляется

1. `{Entity}Actions.cs` (ClientBase) — метод действия и Can-метод
2. `{Entity}.mtd` — Action в секции Actions (если ещё нет)
3. `.resx` / `.ru.resx` — ресурсы для заголовков и подписей полей
4. Серверная Remote-функция (если нужен вызов после подтверждения)

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
