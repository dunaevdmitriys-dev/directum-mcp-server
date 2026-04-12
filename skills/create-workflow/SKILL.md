---
description: "Создать полный workflow: блоки, хендлеры, RouteScheme для задачи Directum RX"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание Workflow (блоки + хендлеры) для задачи Directum RX

## ШАГ 0: Посмотри рабочий пример

**Эталон: ProposalApprovalTask** — задача согласования КП из DirRX.CRMDocuments.

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **Task.mtd (Scheme, BlockIds, Blocks)** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Shared/ProposalApprovalTask/ProposalApprovalTask.mtd` |
| **BlockHandlers (Server)** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Server/ProposalApprovalTask/ProposalApprovalTaskBlockHandlers.cs` |
| **TaskHandlers (BeforeStart)** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Server/ProposalApprovalTask/ProposalApprovalTaskHandlers.cs` |
| **RouteHandlers** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Server/ProposalApprovalTask/ProposalApprovalTaskRouteHandlers.cs` |
| **ServerFunctions** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Server/ProposalApprovalTask/ProposalApprovalTaskServerFunctions.cs` |
| **Assignment.mtd** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Shared/ProposalApprovalAssignment/ProposalApprovalAssignment.mtd` |
| **Notice.mtd** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Shared/ProposalNotice/ProposalNotice.mtd` |

**Реальный BeforeStart из ProposalApprovalTaskHandlers.cs:**
```csharp
namespace DirRX.CRMDocuments
{
  partial class ProposalApprovalTaskServerHandlers
  {
    public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
    {
      base.BeforeStart(e);
      if (!_obj.Attachments.Any())
        e.AddError(DirRX.CRMDocuments.ProposalApprovalTasks.Resources.DocumentRequired);
    }
  }
}
```

**Реальный BlockHandlers namespace (из ProposalApprovalTaskBlockHandlers.cs):**
```csharp
namespace DirRX.CRMDocuments.Server.ProposalApprovalTaskBlocks
{
  // Хендлеры блоков маршрута
}
```

**Ключевые поля MTD Task (из ProposalApprovalTask.mtd):**
- `"$type": "Sungero.Metadata.TaskMetadata, Sungero.Workflow.Shared"`
- `"BlockIds": []` — всегда пустой
- `"Scheme"` с `CurrentVersionGuid`
- `"UseSchemeFromSettings": true`
- `"IntegrationServiceName": "CRMDocumentsProposalApprovalTask"`
- `"HandledEvents": ["BeforeStartServer"]`

Перед созданием нового workflow — **обязательно прочитай** эти файлы и адаптируй.

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании (например, `Acme`)
- **ModuleName** — имя модуля
- **TaskName** — имя задачи (должна уже существовать)
- **Blocks** — описание блоков (имя, тип, задание/уведомление, обработчики)

## Типы блоков

| Тип | $type в MTD | Handler события |
|-----|------------|-----------------|
| Assignment | `Sungero.Metadata.AssignmentBlockMetadata` | StartAssignment, CompleteAssignment, End |
| Notice | `Sungero.Metadata.NoticeBlockMetadata` | StartNotice |
| Script | `Sungero.Metadata.ScriptBlockMetadata` | Execute |
| Task | `Sungero.Metadata.TaskBlockMetadata` | StartTask |

## КРИТИЧНО — сигнатуры BlockHandler (подтверждено из ESM + archive/base)

### Assignment Block

```csharp
namespace {Company}.{Module}.Server.{ModuleName}Blocks
{
  partial class {BlockName}Handlers
  {
    // StartAssignment — интерфейс задания НАПРЯМУЮ (НЕ generic args!)
    public virtual void {BlockName}StartAssignment({Company}.{Module}.I{AssignmentType} assignment)
    {
      assignment.Subject = _obj.Subject;
      assignment.Deadline = _obj.MaxDeadline;
    }

    // CompleteAssignment — тот же паттерн
    public virtual void {BlockName}CompleteAssignment({Company}.{Module}.I{AssignmentType} assignment)
    {
      var result = assignment.Result;
      if (result == {Module}.{AssignmentType}.Result.Approve)
      {
        // бизнес-логика
      }
    }

    // End — получает все созданные задания
    public virtual void {BlockName}End(
      System.Collections.Generic.IEnumerable<{Company}.{Module}.I{AssignmentType}> createdAssignments)
    {
      var last = createdAssignments.OrderByDescending(s => s.Created).FirstOrDefault();
      _block.OutProperties.PropName = last.PropName;
    }
  }
}
```

### Notice Block

```csharp
partial class {BlockName}Handlers
{
  public virtual void {BlockName}StartNotice({Company}.{Module}.I{NoticeType} notice)
  {
    notice.Subject = _obj.Subject;
  }
}
```

### Script Block

```csharp
partial class {BlockName}Handlers
{
  public virtual void {BlockName}Execute()
  {
    _block.RetrySettings.Retry = false;
    var attachments = _block.Attachments.ToList();
    // логика
  }
}
```

### Task Block

```csharp
partial class {BlockName}Handlers
{
  public virtual void {BlockName}StartTask({Company}.{Module}.I{TaskType} task)
  {
    task.Request = _obj.Request;
    task.Attachments.Add(_obj.Request);
  }
}
```

## Доступные контексты в блок-хендлерах

| Контекст | Описание |
|----------|----------|
| `_obj` | Родительская задача |
| `_block` | Конфигурация блока |
| `_block.OutProperties.{Prop}` | Выходные свойства блока |
| `_block.RetrySettings.Retry` | Управление повтором |
| `_block.Attachments` | Вложения блока |
| `_block.Author` | Автор блока |
| `_block.{Prop}.GetValueOrDefault()` | Nullable свойства |
| `_block.Performers` | Исполнители блока |
| `_block.SendNotificationAboutAborting` | Параметр уведомления |

## MTD — определение блоков в Task.mtd

```json
{
  "BlockIds": [],
  "Blocks": [
    {
      "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<новый-GUID>",
      "Name": "ApproveBlock",
      "EntityType": "<GUID-типа-задания>",
      "HandledEvents": ["ApproveBlockStartAssignment", "ApproveBlockCompleteAssignment"],
      "ProcessStagesDisplayMode": "Show",
      "OutProperties": [
        {
          "$type": "Sungero.Metadata.NavigationBlockPropertyMetadata, Sungero.Metadata",
          "NameGuid": "<GUID>",
          "Name": "ForwardTo",
          "EntityGuid": "<GUID-типа-сущности>"
        }
      ]
    },
    {
      "$type": "Sungero.Metadata.NoticeBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<новый-GUID>",
      "Name": "NotifyBlock",
      "EntityType": "<GUID-типа-уведомления>",
      "HandledEvents": ["NotifyBlockStartNotice"]
    },
    {
      "$type": "Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<новый-GUID>",
      "Name": "CreateInteraction",
      "HandledEvents": ["CreateInteractionExecute"]
    }
  ]
}
```

**КРИТИЧНО:**
- `BlockIds: []` — ВСЕГДА пустой массив (НЕ GUID-ы!)
- `HandledEvents` — точные имена методов из хендлера
- `EntityType` — GUID типа задания/уведомления

## RouteScheme.xml

Минимальная схема маршрута:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RouteScheme xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Version>{SchemeVersionGuid}</Version>
  <Activities>
    <Activity xsi:type="StartActivity">
      <Name>Start</Name>
      <Id>1</Id>
    </Activity>
    <Activity xsi:type="AssignmentBlockActivity">
      <Name>ApproveBlock</Name>
      <Id>2</Id>
      <BlockGuid>{BlockGuid}</BlockGuid>
    </Activity>
    <Activity xsi:type="EndActivity">
      <Name>End</Name>
      <Id>3</Id>
    </Activity>
  </Activities>
  <Transitions>
    <Transition><From>1</From><To>2</To></Transition>
    <Transition><From>2</From><To>3</To></Transition>
  </Transitions>
</RouteScheme>
```

## Файлы

```
source/{Company}.{Module}/
  ...Server/{TaskName}/
    ModuleBlockHandlers.cs   # Все блок-хендлеры
  ...Shared/{TaskName}/
    {TaskName}.mtd               # Обновить Blocks[], BlockIds
    RouteScheme.xml              # Схема маршрута
```

## Алгоритм

1. Прочитай Task.mtd — определи существующие блоки и задания
2. Сгенерируй GUID для новых блоков и OutProperties
3. Добавь блоки в `Blocks[]` в Task.mtd
4. Создай/обнови `ModuleBlockHandlers.cs` с правильными сигнатурами
5. Создай/обнови `RouteScheme.xml`
6. Добавь `HandledEvents` в MTD для каждого обработчика
7. Сверь namespace: `{Company}.{Module}.Server.{ModuleName}Blocks`

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
