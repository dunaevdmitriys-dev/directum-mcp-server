---
description: "Создать новый тип задачи Directum RX с заданиями и workflow-схемой"
---

# Создание задачи Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `scaffold_entity baseType=Task` — генерация Task MTD + resx + C# стабов
- `scaffold_entity baseType=Assignment` — генерация Assignment MTD + resx + C# стабов
- `scaffold_entity baseType=Notice` — генерация Notice MTD + resx + C# стабов
- `modify_workflow` — добавление/удаление блоков RouteScheme
- `validate_workflow` — валидация RouteScheme: мёртвые блоки, тупики, переходы без условий
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей System.resx
- `search_metadata filterType=Task` — поиск эталонных задач в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Приоритет 1 — Targets MapApprovalTask (production Task с workflow):
Read targets/source/DirRX.Targets/DirRX.Targets.Shared/MapApprovalTask/MapApprovalTask.mtd
Read targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskBlockHandlers.cs

# Приоритет 2 — Платформа:
MCP: search_metadata type=TaskMetadata

# Приоритет 3 — CRM:
Read CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalApprovalTask/ProposalApprovalTask.mtd
```

## Reference: Production Task (MapApprovalTask из Targets)

| Файл | Путь |
|------|------|
| **Task MTD (13KB)** | `targets/source/DirRX.Targets/DirRX.Targets.Shared/MapApprovalTask/MapApprovalTask.mtd` |
| **BlockHandlers** | `targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskBlockHandlers.cs` |
| **AdditionalApprovers** | `targets/source/DirRX.Targets/.../MapApprovalTask@AdditionalApprovers/` |
| **Observers** | `targets/source/DirRX.Targets/.../MapApprovalTask@Observers/` |

**Паттерны из MapApprovalTask:**
- **AssignmentBlock с Properties** — `BooleanBlockPropertyMetadata` (чекбокс в задании), доступ через `_block.PropertyName`
- **Дополнительная коллекция** — `AdditionalApprovers` помимо стандартных Observers
- **Forms** — `WorkflowEntityStandaloneFormMetadata` с `ControlGroupMetadata`, `DataBinderTypeName`
- **Actions** — Abort с `Overridden` (переопределение платформенного действия)

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля (должен уже существовать)
- **TaskName** — имя задачи (PascalCase, например, `ApprovalTask`)
- **DisplayNameRu** — отображаемое имя на русском
- **Assignments** — список заданий (имя, результаты выполнения)
- **WorkflowBlocks** — краткое описание логики маршрута

## КРИТИЧНО — правильные BaseGuid

| Тип | $type | BaseGuid |
|-----|-------|----------|
| Task | `Sungero.Metadata.TaskMetadata, Sungero.Workflow.Shared` | `d795d1f6-45c1-4e5e-9677-b53fb7280c7e` |
| Assignment | `Sungero.Metadata.AssignmentMetadata, Sungero.Workflow.Shared` | `91cbfdc8-5d5d-465e-95a4-3235b8c01d5b` |
| Notice | `Sungero.Metadata.NoticeMetadata, Sungero.Workflow.Shared` | `ef79164b-2ce7-451b-9ba6-eb59dd9a4a74` |

**ВНИМАНИЕ:** Платформенные модули (base/Sungero.*) через MCP: `search_metadata`. См. `docs/platform/REFERENCE_CODE.md`. НЕ используй другие значения!

## Что создаётся

Задача — это 3+ сущности:
1. **Task** (задача) — инициирует процесс
2. **Assignment** (задание) — конкретная работа для исполнителя
3. **Notice** (уведомление) — информирование без требования действий

Плюс:
- Дочерняя сущность `{TaskName}@Observers/` для наблюдателей
- Блоки workflow в Module.mtd
- BlockHandlers в Server

## Алгоритм

### 1. MCP генерация (СНАЧАЛА попробуй MCP)

Создай Task, Assignment и Notice через scaffold_entity:
```
MCP: scaffold_entity outputPath={путь} entityName={TaskName} moduleName={CompanyCode}.{ModuleName} baseType=Task properties="{свойства}"
MCP: scaffold_entity outputPath={путь} entityName={AssignmentName} moduleName={CompanyCode}.{ModuleName} baseType=Assignment properties="{свойства}"
MCP: scaffold_entity outputPath={путь} entityName={NoticeName} moduleName={CompanyCode}.{ModuleName} baseType=Notice
```
Затем добавь блоки workflow и валидируй:
```
MCP: modify_workflow path={путь_к_Task.mtd} action=add_block blockName=ReviewBlock blockType=Assignment dryRun=false
MCP: validate_workflow path={путь_к_Task.mtd}
MCP: check_package packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```
Если MCP недоступен — генерируй вручную по шаблону ниже.

### 2. Сгенерируй GUID (ручной fallback)
- TaskGuid, AssignmentGuid, NoticeGuid — NameGuid сущностей
- ObserversGuid — дочерняя коллекция наблюдателей
- BlockGuid-ы — для каждого блока workflow-схемы
- PropertyGuid-ы — для свойств
- ActionGuid-ы — для результатов выполнения задания
- AttachmentGroupGuid-ы — для групп вложений
- SchemeGuid — для RouteScheme

### 2. Создай файлы задачи

```
source/{CompanyCode}.{ModuleName}/
  ...Shared/
    {TaskName}/
      {TaskName}.mtd
      {TaskName}.resx / .ru.resx
      {TaskName}Constants.cs
      {TaskName}SharedFunctions.cs
      {TaskName}Structures.cs
      {TaskName}Handlers.cs
    {TaskName}@Observers/
      {TaskName}Observers.mtd
      {TaskName}Observers.resx / .ru.resx
  ...Server/
    {TaskName}/
      {TaskName}ServerFunctions.cs
      {TaskName}Handlers.cs
      {TaskName}Queries.xml
  ...ClientBase/
    {TaskName}/
      {TaskName}ClientFunctions.cs
      {TaskName}Handlers.cs
      {TaskName}Actions.cs
```

### 3. Task.mtd

```json
{
  "$type": "Sungero.Metadata.TaskMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<TaskGuid>",
  "Name": "<TaskName>",
  "AccessRightsMode": "Both",
  "AttachmentGroups": [
    {
      "NameGuid": "<AttachmentGroupGuid>",
      "Name": "DocumentGroup",
      "Constraints": [
        {
          "NameGuid": "<GUID>",
          "Name": "OfficialDocuments",
          "ConstraintTypeId": "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
          "Limit": 1,
          "Versions": []
        }
      ],
      "HandledEvents": ["AddedShared", "DeletedShared"],
      "IsEnabled": false,
      "IsRequired": true,
      "Versions": []
    },
    {
      "NameGuid": "<AttachmentGroupGuid2>",
      "Name": "OtherGroup",
      "Constraints": [],
      "Versions": []
    }
  ],
  "BaseGuid": "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
  "BlockIds": [],
  "CanBeNavigationPropertyType": true,
  "CanBeSearch": false,
  "Code": "<TaskName>",
  "CreationAreaMetadata": {
    "NameGuid": "<GUID>",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "bd0a4ce3-3467-48ad-b905-3820bf6b9da6",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "FormViewUuid": "<GUID>",
  "HandledEvents": [
    "BeforeStartServer"
  ],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><TaskName>",
  "IsVisibleThreadText": true,
  "NeverLinkToParentWhenCreated": true,
  "OperationsClass": "",
  "Overridden": ["CanBeSearch", "UseSchemeFromSettings"],
  "Properties": [
    {
      "$type": "Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata",
      "NameGuid": "3364c324-c4c4-4ccb-a81c-53653255a022",
      "Name": "Observers",
      "EntityGuid": "<ObserversGuid>",
      "IsAncestorMetadata": true,
      "Overridden": ["EntityGuid"],
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": [],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>",
    "Name": "RibbonCard",
    "Categories": [],
    "Elements": [],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "RibbonKind": "Card",
    "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>",
    "Name": "RibbonCollection",
    "Categories": [],
    "Elements": [],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "Versions": []
  },
  "Scheme": {
    "NameGuid": "c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1",
    "Name": "RouteScheme",
    "CurrentVersionGuid": "<UNIQUE-GUID>",
    "IsAncestorMetadata": true,
    "Overridden": ["CurrentVersionGuid"],
    "VersionsCounter": 1
  },
  "UseSchemeFromSettings": true,
  "Versions": [
    { "Type": "TaskMetadata", "Number": 4 },
    { "Type": "WorkflowEntityMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

### 4. Assignment.mtd

```json
{
  "$type": "Sungero.Metadata.AssignmentMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<AssignmentGuid>",
  "Name": "<AssignmentName>",
  "AccessRightsMode": "Instance",
  "Actions": [
    {
      "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<ActionGuid-Approve>",
      "Name": "Approve",
      "ActionArea": "Card",
      "GenerateHandler": true,
      "LargeIconName": "Action_Approve_large_<GUID-без-дефисов>.png",
      "SmallIconName": "Action_Approve_small_<GUID-без-дефисов>.png",
      "Versions": []
    },
    {
      "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<ActionGuid-Reject>",
      "Name": "ForRevision",
      "ActionArea": "Card",
      "GenerateHandler": true,
      "LargeIconName": "Action_ForRevision_large_<GUID-без-дефисов>.png",
      "SmallIconName": "Action_ForRevision_small_<GUID-без-дефисов>.png",
      "Versions": []
    }
  ],
  "AssociatedGuid": "<TaskGuid>",
  "AttachmentGroups": [
    {
      "NameGuid": "<AttachmentGroupGuid-ИЗ-TASK>",
      "Name": "<ИмяГруппыИзTask>",
      "Constraints": [],
      "IsAncestorMetadata": true,
      "IsAssociatedEntityGroup": true,
      "IsAutoGenerated": true,
      "Overridden": ["PreviousGroupId", "Title", "Description", "IsEnabled"],
      "Versions": []
    }
  ],
  "BaseGuid": "91cbfdc8-5d5d-465e-95a4-3235b8c01d5b",
  "CanBeNavigationPropertyType": true,
  "Code": "<AssignmentName>",
  "CreationAreaMetadata": {
    "NameGuid": "<GUID>",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "23d98c0f-b348-479d-b1fb-ccdcf2096bd2",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "FormViewUuid": "<GUID>",
  "HandledEvents": [
    "BeforeCompleteServer",
    "ShowingClient",
    "RefreshClient"
  ],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><AssignmentName>",
  "IsVisibleThreadText": true,
  "NeverLinkToParentWhenCreated": true,
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.EnumPropertyMetadata, Sungero.Metadata",
      "NameGuid": "14fda39b-c81c-4e1c-8fc4-bf3144460f57",
      "Name": "Result",
      "DirectValues": [
        { "NameGuid": "<GUID>", "Name": "Approve", "Code": "Approve", "Versions": [] },
        { "NameGuid": "<GUID>", "Name": "ForRevision", "Code": "ForRevision", "Versions": [] }
      ],
      "IsAncestorMetadata": true,
      "Overridden": ["Values"],
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": [],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>",
    "Name": "RibbonCard",
    "Categories": [],
    "Elements": [
      {
        "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
        "NameGuid": "<GUID>",
        "Name": "Approve",
        "ActionGuid": "<ActionGuid-Approve>",
        "ButtonSize": "Large",
        "Index": 1,
        "IsAutoGenerated": true,
        "ParentGuid": "ac82503a-7a47-49d0-b90c-9bb512c4559c",
        "Versions": []
      },
      {
        "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
        "NameGuid": "<GUID>",
        "Name": "ForRevision",
        "ActionGuid": "<ActionGuid-Reject>",
        "ButtonSize": "Large",
        "Index": 2,
        "IsAutoGenerated": true,
        "ParentGuid": "ac82503a-7a47-49d0-b90c-9bb512c4559c",
        "Versions": []
      }
    ],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "RibbonKind": "Card",
    "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>",
    "Name": "RibbonCollection",
    "Categories": [],
    "Elements": [],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "Versions": []
  },
  "Versions": [
    { "Type": "AssignmentMetadata", "Number": 1 },
    { "Type": "AssignmentBaseMetadata", "Number": 2 },
    { "Type": "WorkflowEntityMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

**Ключевые правила для Assignment (КРИТИЧНО):**
- **AssociatedGuid** — GUID задачи. ОБЯЗАТЕЛЬНО!
- **AttachmentGroups** — используют ТЕ ЖЕ NameGuid что и в Task + `IsAssociatedEntityGroup: true`, `IsAutoGenerated: true`. **НЕ генерировать свои GUID!**
- `Actions` — кнопки результата. Каждая = `ExecutionResultActionMetadata`
- `Result` (Enum, `14fda39b-...`) — наследуемое свойство. **Overridden = `["Values"]`**, НЕ `["DirectValues"]`!
- **DirectValues** — каждое значение ОБЯЗАНО иметь `"Versions": []`
- Имена в `Actions` и `DirectValues` ДОЛЖНЫ совпадать
- **RibbonCard кнопки** — `ParentGuid: "ac82503a-..."` (AsgGroup), `IsAutoGenerated: true`, Groups: [] (НЕ переопределять AsgGroup!)
- **DomainApi:2** в Versions обязательно
- **НЕТ секции Forms** (Assignment не имеет собственной карточки, наследует от базового)

### 5. Notice.mtd

```json
{
  "$type": "Sungero.Metadata.NoticeMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<NoticeGuid>",
  "Name": "<NoticeName>",
  "AccessRightsMode": "Instance",
  "AssociatedGuid": "<TaskGuid>",
  "AttachmentGroups": [
    {
      "NameGuid": "<AttachmentGroupGuid-ИЗ-TASK>",
      "Name": "<ИмяГруппыИзTask>",
      "Constraints": [],
      "IsAncestorMetadata": true,
      "IsAssociatedEntityGroup": true,
      "IsAutoGenerated": true,
      "Overridden": ["PreviousGroupId", "Title", "Description", "IsEnabled"],
      "Versions": []
    }
  ],
  "BaseGuid": "ef79164b-2ce7-451b-9ba6-eb59dd9a4a74",
  "CanBeNavigationPropertyType": true,
  "Code": "<NoticeName>",
  "FilterPanel": {
    "NameGuid": "8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "IsVisibleThreadText": true,
  "NeverLinkToParentWhenCreated": true,
  "Versions": [
    { "Type": "NoticeMetadata", "Number": 1 },
    { "Type": "AssignmentBaseMetadata", "Number": 2 },
    { "Type": "WorkflowEntityMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

**Ключевые правила для Notice (КРИТИЧНО):**
- **AssociatedGuid** — GUID задачи. ОБЯЗАТЕЛЬНО!
- **AttachmentGroups** — ТЕ ЖЕ GUIDs что и в Task + `IsAssociatedEntityGroup: true`
- **FilterPanel.NameGuid** = `8b3cedfe-...` (фиксированный для Notice)
- **DomainApi:2** в Versions обязательно

### 6. Observers.mtd (дочерняя коллекция) — ПОЛНАЯ СТРУКТУРА

Файл: `{TaskName}@Observers/{TaskName}Observers.mtd`

**КРИТИЧНО:** Минимальные заглушки → NullReferenceException в InterfacesGenerator!
BaseGuid ОБЯЗАН быть `ac08b548-e666-4d9b-816f-a6c5e08e360f` (НЕ `c4043e74`!)

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<ObserversGuid>",
  "Name": "<TaskName>Observers",
  "BaseGuid": "ac08b548-e666-4d9b-816f-a6c5e08e360f",
  "CanBeNavigationPropertyType": true,
  "CreationAreaMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "IsAutoGenerated": true,
  "IsChildEntity": true,
  "IsVisible": false,
  "NonVisualAuthorizationMode": "FullAccess",
  "Overridden": ["IsVisible", "AccessRightsMode", "NonVisualAuthorizationMode"],
  "Properties": [
    {
      "$type": "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
      "NameGuid": "f2124770-2128-42c4-8dba-baba806c77e6",
      "Name": "Task",
      "EntityGuid": "<TaskGuid>",
      "IsAncestorMetadata": true,
      "IsReferenceToRootEntity": true,
      "Overridden": ["EntityGuid"],
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": [],
  "RibbonCardMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "RibbonCard",
    "Categories": [],
    "Elements": [],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "RibbonKind": "Card",
    "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "RibbonCollection",
    "Categories": [],
    "Elements": [],
    "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "Versions": []
  },
  "Versions": [
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

### 7. Workflow-блоки в Module.mtd

Добавь блоки в секцию `Blocks` задачи (Task.mtd). **`BlockIds` должен быть `[]`** — НЕ GUID-ы!

```json
"Blocks": [
  {
    "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Workflow.Shared",
    "NameGuid": "<BlockGuid>",
    "Name": "ReviewBlock",
    "AssignmentType": "<AssignmentGuid>",
    "AssociatedGuid": "<TaskGuid>",
    "HandledEvents": [
      "ReviewBlockStartAssignment",
      "ReviewBlockCompleteAssignment"
    ],
    "Versions": []
  },
  {
    "$type": "Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared",
    "NameGuid": "<BlockGuid>",
    "Name": "CheckResultBlock",
    "AssociatedGuid": "<TaskGuid>",
    "HandledEvents": [
      "CheckResultBlockExecute"
    ],
    "Versions": []
  },
  {
    "$type": "Sungero.Metadata.NoticeBlockMetadata, Sungero.Workflow.Shared",
    "NameGuid": "<BlockGuid>",
    "Name": "NotifyBlock",
    "AssociatedGuid": "<TaskGuid>",
    "NoticeType": "<NoticeGuid>",
    "Versions": []
  }
]
```

**AssociatedGuid** — GUID задачи-владельца блока.

### Типы блоков

| Тип | $type | Назначение |
|-----|-------|-----------|
| Assignment | `AssignmentBlockMetadata, Sungero.Workflow.Shared` | Задание |
| Script | `ScriptBlockMetadata, Sungero.Workflow.Shared` | C# логика |
| Notice | `NoticeBlockMetadata, Sungero.Workflow.Shared` | Уведомление |
| Task | `TaskBlockMetadata, Sungero.Workflow.Shared` | Подзадача |
| Decision | `DecisionBlockMetadata, Sungero.Workflow.Shared` | Ветвление |
| Monitoring | `MonitoringBlockMetadata, Sungero.Workflow.Shared` | Ожидание |
| Wait | `WaitBlockMetadata, Sungero.Workflow.Shared` | Пауза |
| AccessRights | `AccessRightsBlockMetadata, Sungero.Workflow.Shared` | Права |

### 8. BlockHandlers — ПРАВИЛЬНЫЙ namespace

**КРИТИЧНО:** Namespace для BlockHandlers = `{CompanyCode}.{ModuleName}.Server.{ModuleName}Blocks`

Файл: `ModuleBlockHandlers.cs` (в Server/)
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Workflow;

namespace {CompanyCode}.{ModuleName}.Server.{ModuleName}Blocks
{
  partial class ReviewBlockHandlers
  {
    public virtual void ReviewBlockStartAssignment(
      {CompanyCode}.{ModuleName}.IReviewAssignment assignment,
      {CompanyCode}.{ModuleName}.Server.ReviewBlockStartAssignmentArguments e)
    {
      assignment.Deadline = _obj.Deadline;
      assignment.Performer = _obj.Assignee;
    }

    public virtual void ReviewBlockCompleteAssignment(
      {CompanyCode}.{ModuleName}.IReviewAssignment assignment,
      {CompanyCode}.{ModuleName}.Server.ReviewBlockCompleteAssignmentArguments e)
    {
      // Обработка результата
    }
  }

  partial class CheckResultBlockHandlers
  {
    public virtual void CheckResultBlockExecute(
      {CompanyCode}.{ModuleName}.Server.CheckResultBlockExecuteArguments e)
    {
      // Логика ветвления
    }
  }
}
```

### 9. Обработчики задачи (Server)

```csharp
namespace {CompanyCode}.{ModuleName}.Server
{
  partial class {TaskName}ServerHandlers
  {
    public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
    {
      base.BeforeStart(e);
      // Валидация перед стартом
      if (!_obj.DocumentGroup.OfficialDocuments.Any())
        e.AddError(Resources.DocumentRequired);
    }

    public override void Created(Sungero.Domain.CreatedEventArgs e)
    {
      base.Created(e);
      // Значения по умолчанию
      _obj.Deadline = Calendar.Now.AddDays(3);
    }
  }
}
```

### 10. Обработчики задания (Server)

```csharp
namespace {CompanyCode}.{ModuleName}.Server
{
  partial class {AssignmentName}ServerHandlers
  {
    public override void BeforeComplete(
      Sungero.Workflow.Server.BeforeCompleteEventArgs e)
    {
      base.BeforeComplete(e);
      // Валидация при завершении
      if (_obj.Result == {AssignmentName}.Result.ForRevision &&
          string.IsNullOrEmpty(_obj.ActiveText))
        e.AddError(Resources.CommentRequired);
    }
  }
}
```

### 11. Actions задания (Client)

```csharp
namespace {CompanyCode}.{ModuleName}.Client
{
  partial class {AssignmentName}Actions
  {
    public virtual void Approve(Sungero.Workflow.Client.ExecuteResultActionArgs e)
    {
      // Подтверждение и выдача прав
      if (!Docflow.PublicFunctions.Module.ShowDialogGrantAccessRightsWithConfirmationDialog(
        _obj, _obj.OtherGroup.All.ToList(), e.Action, Constants.{TaskName}.ApproveDialogID))
        e.Cancel();
    }

    public virtual bool CanApprove(Sungero.Workflow.Client.CanExecuteResultActionArgs e)
    {
      return _obj.DocumentGroup.OfficialDocuments.Any();
    }

    public virtual void ForRevision(Sungero.Workflow.Client.ExecuteResultActionArgs e)
    {
      if (string.IsNullOrEmpty(_obj.ActiveText))
      {
        e.AddError(Resources.CommentRequired);
        e.Cancel();
      }
    }

    public virtual bool CanForRevision(Sungero.Workflow.Client.CanExecuteResultActionArgs e)
    {
      return true;
    }
  }
}
```

## Using statements

**Server:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Workflow;
```

**Client (namespace = `.Client`, НЕ `.ClientBase`!):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

## Справочные материалы
DDS known issues → CLAUDE.md
Антипаттерны → /dds-guardrails
Валидация → /validate-all

## DDS-правила
Все правила импорта DDS → см. CLAUDE.md (пункты 1-18). Не дублировать здесь.
