# 23. Справочник форматов .mtd — минимальные шаблоны для каждого типа сущности

## Общая структура .mtd

Каждый .mtd файл — это JSON с обязательными полями:
- `$type` — тип метаданных (определяет вид сущности)
- `NameGuid` — уникальный GUID сущности (генерировать новый!)
- `Name` — имя сущности (PascalCase, латиница)
- `BaseGuid` — GUID родительского типа (см. гайд 22)

## Минимальный Databook (справочник)

> Реальный пример: `CRM/crm-package/source/DirRX.CRMMarketing/.../LeadSource/LeadSource.mtd`
> Другой пример с Actions: `CRM/crm-package/source/DirRX.CRMSales/.../Pipeline/Pipeline.mtd`

**Цепочка наследования:**
```
Entity → EssentialDatabook → DatabookEntry → LeadSource
```
- `DatabookEntry` (BaseGuid `04581d26-0780-4cfd-b3cd-c2cafc5798b0`) — базовый справочник со Status

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "LeadSource",
  "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
  "CanBeNavigationPropertyType": true,
  "Code": "LeadSource",
  "CreationAreaMetadata": {
    "NameGuid": "f7766750-eee2-4fcd-8003-5c06a90d1f44",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "b0125fbd-3b91-4dbb-914a-689276216404",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "Forms": [
    {
      "$type": "Sungero.Metadata.StandaloneFormMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "Card",
      "Controls": [
        {
          "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
          "NameGuid": "<GUID-GROUP>",
          "Name": "ControlGroup",
          "Versions": []
        },
        {
          "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
          "NameGuid": "<GUID>",
          "Name": "Name",
          "ColumnNumber": 0,
          "ColumnSpan": 1,
          "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
          "ParentGuid": "<GUID-GROUP>",
          "PropertyGuid": "<GUID-СВОЙСТВА-Name>",
          "RowNumber": 0,
          "RowSpan": 1,
          "Settings": [],
          "Versions": []
        }
      ],
      "Versions": []
    }
  ],
  "FormViewUuid": "<GUID-ОТДЕЛЬНЫЙ-ОТ-ФОРМЫ>",
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><EntityName>",
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.EnumPropertyMetadata, Sungero.Metadata",
      "NameGuid": "1dcedc29-5140-4770-ac92-eabc212326a1",
      "Name": "Status",
      "IsAncestorMetadata": true,
      "Overridden": ["IsShowedInList", "IsVisibility", "CanBeSearch"],
      "Versions": []
    },
    {
      "$type": "Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata",
      "NameGuid": "<GUID-СВОЙСТВА-Name>",
      "Name": "Name",
      "Code": "Name",
      "IsDisplayValue": true,
      "IsQuickSearchAllowed": true,
      "IsRequired": true,
      "MaxLength": 250,
      "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
      "PreviousPropertyGuid": "1dcedc29-5140-4770-ac92-eabc212326a1",
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
  "Versions": [
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

**КРИТИЧНО — фиксированные ancestor GUIDs (НЕ генерировать новые!):**
- `CreationAreaMetadata.NameGuid`: `f7766750-eee2-4fcd-8003-5c06a90d1f44` (DatabookEntry)
- `FilterPanel.NameGuid`: `b0125fbd-3b91-4dbb-914a-689276216404` (DatabookEntry)
- Status property NameGuid: `1dcedc29-5140-4770-ac92-eabc212326a1` — ОБЯЗАТЕЛЕН как первое свойство
- `DomainApi:2` — ОБЯЗАТЕЛЕН в Versions
- `Versions: []` — ОБЯЗАТЕЛЕН на КАЖДОЙ секции и свойстве
- `FormViewUuid` — отдельный GUID, НЕ совпадает с Form NameGuid

**Ключевые особенности:**
- `$type`: `EntityMetadata, Sungero.Metadata`
- Есть блок `Forms` с `Card` формой и контролами
- Свойство `Name` (string) — единственное обязательное при создании
- `Code` = имя сущности (max 7 символов для Code свойств)
- `IntegrationServiceName` = `<Module><Entity>` (для REST API)
- `IsStatusEnabled: true` + `Overridden: ["IsStatusEnabled"]` — если нужен lifecycle-статус (Active/Closed)
- `HandledEvents` — типичные: `BeforeSaveServer` (валидация), `ShowingClient` (UI), `CreatedServer` (дефолты)

## Минимальный Document (документ)

> Реальный пример: `CRM/crm-package/source/DirRX.CRMDocuments/.../CommercialProposal/CommercialProposal.mtd`

**Цепочка наследования:**
```
Entity → EssentialDatabook → BaseEntity → InternalEntityBase → RecordBase
  → ElectronicDocument → OfficialDocument → SimpleDocument → CommercialProposal
```
- `SimpleDocument` (BaseGuid `030d8d67-9b94-4f0d-bcc6-691016eb70f3`) — документ без регистрации
- `OfficialDocument` (BaseGuid `58cca102-1e97-4f07-b6ac-fd866a8b7cb1`) — документ с регистрацией
- Выбирать BaseGuid в зависимости от того, нужна ли регистрация

```json
{
  "$type": "Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "CommercialProposal",
  "AccessRightsMode": "Both",
  "BaseGuid": "030d8d67-9b94-4f0d-bcc6-691016eb70f3",
  "CanBeNavigationPropertyType": true,
  "CreationAreaMetadata": {
    "NameGuid": "8fdd9ada-0b64-4850-b290-4a9c745d6b52", "Name": "CreationArea",
    "Buttons": [], "IsAncestorMetadata": true, "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f", "Name": "FilterPanel",
    "Controls": [], "IsAncestorMetadata": true, "Versions": []
  },
  "Forms": [
    {
      "$type": "Sungero.Metadata.StandaloneFormMetadata, Sungero.Metadata",
      "NameGuid": "fa03f748-4397-42ef-bdc2-22119af7bf7f",
      "Name": "Card",
      "Controls": [
        {
          "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
          "NameGuid": "<GUID-GROUP>",
          "Name": "Main",
          "SharedNestedGroupsAlignment": true,
          "Versions": []
        },
        {
          "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
          "NameGuid": "<GUID>",
          "Name": "Name",
          "ColumnNumber": 0, "ColumnSpan": 1,
          "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder",
          "ParentGuid": "<GUID-GROUP>",
          "PropertyGuid": "efaae3b3-152c-4470-a9d8-b0c511095ef5",
          "RowNumber": 0, "RowSpan": 1,
          "Settings": [
            { "Name": "Height", "Value": 40 },
            { "Name": "AcceptsReturn", "Value": false }
          ],
          "Versions": []
        }
      ],
      "IsAncestorMetadata": true,
      "Overridden": ["Controls", "SettingsResourceKey"],
      "Versions": []
    }
  ],
  "FormViewUuid": "<GUID-ОТДЕЛЬНЫЙ-ОТ-ФОРМЫ>",
  "HandledEvents": [
    "BeforeSaveServer",
    "ShowingClient"
  ],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><EntityName>",
  "ListViewUuid": "<GUID-ДЛЯ-СПИСКОВОГО-ПРЕДСТАВЛЕНИЯ>",
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata",
      "NameGuid": "56cbe741-880f-4e6f-9567-343d08494b59",
      "Name": "Versions",
      "EntityGuid": "<GUID-VERSIONS-ENTITY>",
      "IsAncestorMetadata": true,
      "Overridden": ["EntityGuid"],
      "Versions": []
    },
    {
      "$type": "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "Deal",
      "Code": "CPDeal",
      "EntityGuid": "<GUID-СВЯЗАННОЙ-СУЩНОСТИ>",
      "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder",
      "PreviousPropertyGuid": "15280407-331e-42f6-b263-041a495b66cd",
      "Versions": []
    },
    {
      "$type": "Sungero.Metadata.DoublePropertyMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "TotalAmount",
      "Code": "TotalAmt",
      "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder",
      "PreviousPropertyGuid": "<GUID-ПРЕДЫДУЩЕГО-СВОЙСТВА>",
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": ["DisplayName"],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCard",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "RibbonKind": "Card", "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCollection",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "Versions": []
  },
  "Versions": [
    { "Type": "DocumentMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

**Ключевые особенности:**
- `$type`: `DocumentMetadata, Sungero.Content.Shared`
- `AccessRightsMode`: `"Both"` (на тип + на экземпляр)
- **Forms**: Card form NameGuid **ОБЯЗАТЕЛЬНО** `fa03f748-4397-42ef-bdc2-22119af7bf7f` с `IsAncestorMetadata: true`
- **HandledEvents**: типичные — `BeforeSaveServer` (валидация), `ShowingClient` (UI-логика)
- Свойство Name документа привязано к `PropertyGuid: "efaae3b3-152c-4470-a9d8-b0c511095ef5"` (ancestor)
- Унаследованная коллекция `Versions` — переопределяем `EntityGuid`. Также бывает `Tracking` (`15280407-331e-42f6-b263-041a495b66cd`)
- Нужно создать дочерние сущности: `{Name}Versions.mtd` в папке `{Name}@Versions/`
- `ListViewUuid` — отдельный GUID для списочного представления
- `DomainApi:2` в Versions обязательно

## Минимальный Task (задача)

> Реальный пример: `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalApprovalTask/ProposalApprovalTask.mtd`

**Цепочка наследования:**
```
Entity → EssentialDatabook → BaseEntity → InternalEntityBase → RecordBase
  → WorkflowEntity → AssignmentBase → Task → SimpleTask → ProposalApprovalTask
```
- `Task` (BaseGuid `d795d1f6-45c1-4e5e-9677-b53fb7280c7e`) — базовый тип задачи с маршрутом
- `SimpleTask` (BaseGuid `4e3f4cf4-641a-4a9a-88bc-dbeaff517ac7`) — простая задача без маршрута

```json
{
  "$type": "Sungero.Metadata.TaskMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "ProposalApprovalTask",
  "AccessRightsMode": "Both",
  "AttachmentGroups": [
    {
      "NameGuid": "<GUID>",
      "Name": "DocumentGroup",
      "Constraints": [],
      "HandledEvents": ["AddedShared", "CreatedShared"],
      "Versions": []
    }
  ],
  "BaseGuid": "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
  "BlockIds": [],
  "CanBeNavigationPropertyType": true,
  "CanBeSearch": false,
  "CreationAreaMetadata": {
    "NameGuid": "7c696893-ac69-42ca-b419-dddeab6d82ed", "Name": "CreationArea",
    "Buttons": [], "IsAncestorMetadata": true, "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "bd0a4ce3-3467-48ad-b905-3820bf6b9da6", "Name": "FilterPanel",
    "Controls": [], "IsAncestorMetadata": true, "Versions": []
  },
  "FormViewUuid": "<GUID>",
  "HandledEvents": [
    "BeforeStartServer"
  ],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><TaskName>",
  "IsVisibleThreadText": true,
  "ListViewUuid": "<GUID-ДЛЯ-СПИСКОВОГО-ПРЕДСТАВЛЕНИЯ>",
  "NeverLinkToParentWhenCreated": true,
  "OperationsClass": "",
  "Overridden": ["CanBeSearch", "UseSchemeFromSettings"],
  "Properties": [
    {
      "$type": "Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata",
      "NameGuid": "3364c324-c4c4-4ccb-a81c-53653255a022",
      "Name": "Observers",
      "EntityGuid": "<GUID-OBSERVERS-ENTITY>",
      "IsAncestorMetadata": true,
      "Overridden": ["EntityGuid"],
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": ["DisplayName", "DocumentGroup", "DocumentRequired"],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCard",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "RibbonKind": "Card", "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCollection",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "Versions": []
  },
  "Scheme": {
    "NameGuid": "c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1",
    "Name": "RouteScheme",
    "CurrentVersionGuid": "<УНИКАЛЬНЫЙ-GUID>",
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

**Ключевые особенности:**
- `$type`: `TaskMetadata, Sungero.Workflow.Shared`
- **AttachmentGroups**: группы вложений задачи. `HandledEvents: ["AddedShared", "CreatedShared"]` — обработчики событий вложений
- **HandledEvents**: типичные — `BeforeStartServer` (валидация перед стартом задачи)
- Блок `Scheme` — описание workflow-схемы. **NameGuid ФИКСИРОВАННЫЙ**: `c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1`. `CurrentVersionGuid` — уникальный, генерировать новый
- `BlockIds` — массив GUID блоков схемы (ссылки на блоки в Module.mtd)
- Унаследованная коллекция `Observers` — нужно создать `{TaskName}@Observers/` дочернюю сущность
- `ResourcesKeys` — типично содержит `DisplayName`, названия групп вложений, ключи сообщений валидации
- `UseSchemeFromSettings`: true, `CanBeSearch`: false
- `DomainApi:2` в Versions обязательно

## Минимальный Assignment (задание)

> Реальный пример: `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalApprovalAssignment/ProposalApprovalAssignment.mtd`

**Цепочка наследования:**
```
Entity → EssentialDatabook → BaseEntity → InternalEntityBase → RecordBase
  → WorkflowEntity → AssignmentBase → Assignment → ProposalApprovalAssignment
```
- `Assignment` (BaseGuid `91cbfdc8-5d5d-465e-95a4-3235b8c01d5b`) — задание с кнопками выполнения

```json
{
  "$type": "Sungero.Metadata.AssignmentMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "ProposalApprovalAssignment",
  "AccessRightsMode": "Instance",
  "Actions": [
    {
      "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<GUID-ACTION-APPROVE>",
      "Name": "Approve",
      "ActionArea": "Card",
      "GenerateHandler": true,
      "LargeIconName": null,
      "SmallIconName": null,
      "Versions": []
    },
    {
      "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<GUID-ACTION-REJECT>",
      "Name": "Reject",
      "ActionArea": "Card",
      "GenerateHandler": true,
      "LargeIconName": null,
      "NeedConfirmation": true,
      "SmallIconName": null,
      "Versions": []
    },
    {
      "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<GUID-ACTION-FORREVISION>",
      "Name": "ForRevision",
      "ActionArea": "Card",
      "GenerateHandler": true,
      "LargeIconName": null,
      "NeedConfirmation": true,
      "SmallIconName": null,
      "Versions": []
    }
  ],
  "AssociatedGuid": "<GUID-ЗАДАЧИ>",
  "AttachmentGroups": [
    {
      "NameGuid": "<GUID-ИЗ-TASK>",
      "Name": "DocumentGroup",
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
  "CreationAreaMetadata": {
    "NameGuid": "0e5344c6-4b4c-4b42-9cc6-513415d3a8e5", "Name": "CreationArea",
    "Buttons": [], "IsAncestorMetadata": true, "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "23d98c0f-b348-479d-b1fb-ccdcf2096bd2",
    "Name": "FilterPanel",
    "Controls": [], "IsAncestorMetadata": true, "Versions": []
  },
  "FormViewUuid": "<GUID>",
  "HandledEvents": [
    "BeforeCompleteServer"
  ],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><AssignmentName>",
  "IsVisibleThreadText": true,
  "ListViewUuid": "<GUID-ДЛЯ-СПИСКОВОГО-ПРЕДСТАВЛЕНИЯ>",
  "NeverLinkToParentWhenCreated": true,
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.EnumPropertyMetadata, Sungero.Metadata",
      "NameGuid": "14fda39b-c81c-4e1c-8fc4-bf3144460f57",
      "Name": "Result",
      "DirectValues": [
        { "NameGuid": "<GUID>", "Name": "Approve", "Code": "Approve", "Versions": [] },
        { "NameGuid": "<GUID>", "Name": "Reject", "Code": "Reject", "Versions": [] },
        { "NameGuid": "<GUID>", "Name": "ForRevision", "Code": "ForRevision", "Versions": [] }
      ],
      "IsAncestorMetadata": true,
      "Overridden": ["Values"],
      "Versions": []
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": ["DisplayName", "Approve", "Reject", "ForRevision", "RejectReasonRequired"],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCard",
    "Categories": [],
    "Elements": [
      {
        "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
        "NameGuid": "<GUID>", "Name": "Approve",
        "ActionGuid": "<GUID-ACTION-APPROVE>",
        "ButtonSize": "Large", "Index": 1,
        "IsAutoGenerated": true,
        "ParentGuid": "ac82503a-7a47-49d0-b90c-9bb512c4559c",
        "Versions": []
      },
      {
        "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
        "NameGuid": "<GUID>", "Name": "Reject",
        "ActionGuid": "<GUID-ACTION-REJECT>",
        "ButtonSize": "Large", "Index": 2,
        "IsAutoGenerated": true,
        "ParentGuid": "ac82503a-7a47-49d0-b90c-9bb512c4559c",
        "Versions": []
      },
      {
        "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
        "NameGuid": "<GUID>", "Name": "ForRevision",
        "ActionGuid": "<GUID-ACTION-FORREVISION>",
        "ButtonSize": "Large", "Index": 3,
        "IsAutoGenerated": true,
        "ParentGuid": "ac82503a-7a47-49d0-b90c-9bb512c4559c",
        "Versions": []
      }
    ],
    "Groups": [],
    "IsAncestorMetadata": true, "Pages": [],
    "RibbonKind": "Card", "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCollection",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "Versions": []
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

**Ключевые особенности:**
- `$type`: `AssignmentMetadata, Sungero.Workflow.Shared`
- `AccessRightsMode`: `"Instance"` (права на каждый экземпляр)
- **AssociatedGuid**: GUID задачи (Task) — ОБЯЗАТЕЛЬНО
- **AttachmentGroups**: используют ТЕ ЖЕ NameGuid что и в Task + `IsAssociatedEntityGroup: true`, `IsAutoGenerated: true`
- **Actions**: кнопки `ExecutionResultActionMetadata`. Имя Action **ДОЛЖНО совпадать** с именем DirectValue в Result. `NeedConfirmation: true` для деструктивных действий (Reject, ForRevision)
- **HandledEvents**: типичные — `BeforeCompleteServer` (валидация перед завершением задания)
- `Result.DirectValues`: каждое значение ОБЯЗАНО иметь `Versions: []`
- `Result.Overridden`: `["Values"]` (НЕ `["DirectValues"]`!)
- **RibbonCard**: каждая кнопка-Action — отдельный `RibbonActionButtonMetadata` через `ParentGuid: "ac82503a-..."` (AsgGroup), `IsAutoGenerated: true`, Groups: []
- **ResourcesKeys**: имена Actions для локализации + ключи сообщений валидации
- `FilterPanel.NameGuid`: `23d98c0f-b348-479d-b1fb-ccdcf2096bd2` (фиксированный)
- `IsVisibleThreadText`: true, `NeverLinkToParentWhenCreated`: true
- `DomainApi:2` в Versions

## Минимальный Notice (уведомление)

> Реальный пример: `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalNotice/ProposalNotice.mtd`

**Цепочка наследования:**
```
Entity → EssentialDatabook → BaseEntity → InternalEntityBase → RecordBase
  → WorkflowEntity → AssignmentBase → Notice → ProposalNotice
```
- `Notice` (BaseGuid `ef79164b-2ce7-451b-9ba6-eb59dd9a4a74`) — уведомление, read-only для получателя

```json
{
  "$type": "Sungero.Metadata.NoticeMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "ProposalNotice",
  "AccessRightsMode": "Instance",
  "AssociatedGuid": "<GUID-ЗАДАЧИ>",
  "AttachmentGroups": [
    {
      "NameGuid": "<GUID-ИЗ-TASK>",
      "Name": "DocumentGroup",
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
  "CreationAreaMetadata": {
    "NameGuid": "382c1362-76ca-4ddb-a3d3-70d8668e993e", "Name": "CreationArea",
    "Buttons": [], "IsAncestorMetadata": true, "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f",
    "Name": "FilterPanel",
    "Controls": [], "IsAncestorMetadata": true, "Versions": []
  },
  "FormViewUuid": "<GUID>",
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><NoticeName>",
  "IsVisibleThreadText": true,
  "ListViewUuid": "<GUID-ДЛЯ-СПИСКОВОГО-ПРЕДСТАВЛЕНИЯ>",
  "NeverLinkToParentWhenCreated": true,
  "OperationsClass": "",
  "PublicStructures": [],
  "ResourcesKeys": [],
  "RibbonCardMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCard",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "RibbonKind": "Card", "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<GUID>", "Name": "RibbonCollection",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true, "Pages": [], "Versions": []
  },
  "Versions": [
    { "Type": "NoticeMetadata", "Number": 1 },
    { "Type": "AssignmentBaseMetadata", "Number": 2 },
    { "Type": "WorkflowEntityMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

**Ключевые особенности:**
- `$type`: `NoticeMetadata, Sungero.Workflow.Shared`
- Самый простой из workflow-типов — **нет Actions, нет Result, нет HandledEvents** (обычно)
- **AssociatedGuid**: GUID задачи — ОБЯЗАТЕЛЬНО
- **AttachmentGroups**: ТЕ ЖЕ NameGuid что и в Task + `IsAssociatedEntityGroup: true`, `IsAutoGenerated: true`
- **Нет свойства Properties** (обычно) — Notice не добавляет собственных свойств
- `CreationAreaMetadata.NameGuid`: `382c1362-76ca-4ddb-a3d3-70d8668e993e` (фиксированный для Notice)
- `FilterPanel.NameGuid`: `8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f` (фиксированный для Notice)
- `IsVisibleThreadText`: true, `NeverLinkToParentWhenCreated`: true
- `DomainApi:2` в Versions

## Минимальный Report (отчёт)

```json
{
  "$type": "Sungero.Metadata.ReportMetadata, Sungero.Reporting.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "SalesFunnelReport",
  "AssociatedGuid": "<GUID-МОДУЛЯ>",
  "BaseGuid": "cef9a810-3f30-4eca-9fe3-30992af0b818",
  "DataSources": [],
  "DefaultExportFormat": "Pdf",
  "ExportFormats": ["Pdf"],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><ReportName>",
  "Overridden": ["PublicConstants", "PublicStructures"],
  "Parameters": [],
  "PublicStructures": [],
  "ResourcesKeys": [],
  "Versions": [
    { "Type": "ReportMetadata", "Number": 1 }
  ]
}
```

**Ключевые особенности:**
- `$type`: `ReportMetadata, Sungero.Reporting.Shared`
- `AssociatedGuid` — GUID модуля-владельца
- `Parameters` — входные параметры отчёта
- `DataSources` — источники данных
- `DefaultExportFormat` / `ExportFormats` — формат вывода

## Дочерняя коллекция (auto-generated)

Для документов DDS создаёт Versions, Tracking, Milestones. Для задач — Observers.

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "<ParentName>Versions",
  "BaseGuid": "c7a89d8e-e835-42f5-81af-6c741c43d259",
  "IsChildEntity": true,
  "IsAutoGenerated": true,
  "IsVisible": false,
  "Versions": [
    { "Type": "EntityMetadata", "Number": 13 }
  ]
}
```

## $type для типов свойств

| Тип свойства | $type |
|---|---|
| String | `Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata` |
| Integer | `Sungero.Metadata.IntegerPropertyMetadata, Sungero.Metadata` |
| Double | `Sungero.Metadata.DoublePropertyMetadata, Sungero.Metadata` |
| Boolean | `Sungero.Metadata.BooleanPropertyMetadata, Sungero.Metadata` |
| DateTime | `Sungero.Metadata.DateTimePropertyMetadata, Sungero.Metadata` |
| Enum | `Sungero.Metadata.EnumPropertyMetadata, Sungero.Metadata` |
| Navigation | `Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata` |
| Collection | `Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata` |
| Text (длинная строка) | `Sungero.Metadata.TextPropertyMetadata, Sungero.Metadata` |

## DataBinder-ы для контролов формы

| Тип свойства | DataBinderTypeName |
|---|---|
| String | `Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder` |
| Integer | `Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder` |
| Double | `Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder` |
| Boolean | `Sungero.Presentation.CommonDataBinders.BooleanEditorToBooleanBinder` |
| DateTime | `Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder` |
| Enum | `Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumBinder` |
| Navigation | `Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder` |
| Text | `Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder` |

## КРИТИЧНО: FilterPanel.NameGuid — ФИКСИРОВАННЫЙ по базовому типу

> **FilterPanel с `IsAncestorMetadata: true` ДОЛЖЕН иметь NameGuid, совпадающий с базовым типом!**
> DDS выдаёт ошибку синхронизации если GUID не совпадает.
> **НЕ генерировать новый GUID** — брать из таблицы:

| Базовый тип | BaseGuid | FilterPanel.NameGuid |
|-------------|----------|----------------------|
| DatabookEntry | `04581d26-0780-4cfd-b3cd-c2cafc5798b0` | **`b0125fbd-3b91-4dbb-914a-689276216404`** |
| OfficialDocument | `58cca102-1e97-4f07-b6ac-fd866a8b7cb1` | **`80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f`** |
| Task | `d795d1f6-45c1-4e5e-9677-b53fb7280c7e` | **`bd0a4ce3-3467-48ad-b905-3820bf6b9da6`** |
| Assignment | `91cbfdc8-5d5d-465e-95a4-3235b8c01d5b` | **`23d98c0f-b348-479d-b1fb-ccdcf2096bd2`** |
| Notice | `ef79164b-2ce7-451b-9ba6-eb59dd9a4a74` | **`8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f`** |

RibbonCardMetadata и RibbonCollectionMetadata — уникальны для каждой сущности (генерировать новый GUID).

## Версии метаданных (актуальные номера)

| Type | Number |
|---|---|
| EntityMetadata | 13 |
| DocumentMetadata | 2 |
| TaskMetadata | 4 |
| AssignmentMetadata | 1 |
| AssignmentBaseMetadata | 2 |
| NoticeMetadata | 1 |
| WorkflowEntityMetadata | 2 |
| ReportMetadata | 1 |
| ModuleMetadata | 11 |
| DomainApi | 2 |

## Forms / Controls — реальные структуры

### ControlGroupMetadata (контейнер)
```json
{
  "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "MainGroup",
  "ColumnDefinitions": [
    { "Percentage": 55.08 },
    { "Percentage": 44.92 }
  ],
  "SharedNestedGroupsAlignment": true,
  "Versions": []
}
```

### ControlGroupMetadata (Expander)
```json
{
  "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "DetailsGroup",
  "ColumnDefinitions": [
    { "Percentage": 55.12 },
    { "Percentage": 44.88 }
  ],
  "ColumnNumber": 0,
  "ColumnSpan": 2,
  "GroupType": "Expander",
  "ParentGuid": "<GUID-РОДИТЕЛЯ>",
  "RowNumber": 9,
  "RowSpan": 1,
  "SharedNestedGroupsAlignment": true,
  "Versions": []
}
```

### ControlMetadata (StringEditor)
```json
{
  "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "Name",
  "ColumnNumber": 0,
  "ColumnSpan": 2,
  "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
  "ParentGuid": "<GUID-ГРУППЫ>",
  "PropertyGuid": "<GUID-СВОЙСТВА>",
  "RowNumber": 0,
  "RowSpan": 1,
  "Settings": [],
  "Versions": []
}
```

### ControlMetadata (TextEditor с высотой)
```json
{
  "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "Description",
  "ColumnNumber": 0,
  "ColumnSpan": 2,
  "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder",
  "ParentGuid": "<GUID-ГРУППЫ>",
  "PropertyGuid": "<GUID-СВОЙСТВА>",
  "RowNumber": 1,
  "RowSpan": 1,
  "Settings": [
    { "Name": "Height", "Value": 40 }
  ],
  "Versions": []
}
```

### ControlMetadata (Navigation/Dropdown)
```json
{
  "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "Department",
  "ColumnNumber": 0,
  "ColumnSpan": 1,
  "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder",
  "ParentGuid": "<GUID-ГРУППЫ>",
  "PropertyGuid": "<GUID-СВОЙСТВА>",
  "RowNumber": 2,
  "RowSpan": 1,
  "Settings": [],
  "Versions": []
}
```

## Actions — реальные структуры

### ActionMetadata (обычное действие)
```json
{
  "NameGuid": "<GUID>",
  "Name": "Register",
  "ActionArea": "Card",
  "GenerateHandler": true,
  "KeyGesture": "Ctrl+R",
  "LargeIconName": "Action_Register_large_<GUID-без-дефисов>.png",
  "SmallIconName": "Action_Register_small_<GUID-без-дефисов>.png",
  "Versions": []
}
```

### ExecutionResultActionMetadata (результат workflow)
```json
{
  "$type": "Sungero.Workflow.Shared.ExecutionResultActionMetadata, Sungero.Workflow.Shared",
  "NameGuid": "<GUID>",
  "Name": "Approved",
  "ActionArea": "Card",
  "GenerateHandler": true,
  "LargeIconName": "Action_Approved_large_<GUID-без-дефисов>.png",
  "SmallIconName": "Action_Approved_small_<GUID-без-дефисов>.png",
  "Versions": []
}
```

## AttachmentGroups — реальные структуры

### С ограничением типа
```json
{
  "NameGuid": "<GUID>",
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
}
```

**Ключевые поля:**
- `ConstraintTypeId` — BaseGuid типа документа (OfficialDocument = `58cca102...`)
- `Limit` — максимум вложений (0 = без ограничений)
- `IsRequired` — обязательна ли группа
- `IsEnabled` — можно ли добавлять вложения вручную
- `HandledEvents` — `AddedShared`, `DeletedShared`

### Associated Entity Group (автогенерируемая)
```json
{
  "NameGuid": "<GUID>",
  "Name": "AddendaGroup",
  "Constraints": [],
  "IsAncestorMetadata": true,
  "IsAssociatedEntityGroup": true,
  "IsAutoGenerated": true,
  "Overridden": ["PreviousGroupId", "Title", "Description", "IsEnabled"],
  "PreviousGroupId": "<GUID-предыдущей-группы>",
  "Versions": []
}
```

## RibbonCardMetadata / RibbonCollectionMetadata

### RibbonCardMetadata (лента на карточке)
```json
{
  "NameGuid": "<GUID>",
  "Name": "RibbonCard",
  "Categories": [],
  "Elements": [
    {
      "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "RegisterButton",
      "ActionGuid": "<GUID-ACTION>",
      "ButtonSize": "Large",
      "Index": 0,
      "ParentGuid": "<GUID-ГРУППЫ-НА-ЛЕНТЕ>",
      "Versions": []
    }
  ],
  "Groups": [],
  "IsAncestorMetadata": true,
  "Pages": [],
  "RibbonKind": "Card",
  "Versions": []
}
```

### RibbonCollectionMetadata (лента на списке)
```json
{
  "NameGuid": "<GUID>",
  "Name": "RibbonCollection",
  "Categories": [],
  "Elements": [
    {
      "$type": "Sungero.Metadata.RibbonActionButtonMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "ActionButton",
      "ActionGuid": "<GUID-ACTION>",
      "ButtonSize": "Large",
      "Index": 0,
      "IsVisible": false,
      "ParentGuid": "<GUID-ГРУППЫ>",
      "Versions": []
    }
  ],
  "Groups": [],
  "IsAncestorMetadata": true,
  "Pages": [],
  "Versions": []
}
```

## HandledEvents — полный справочник

### Уровень сущности
| Событие | Контекст | Описание |
|---------|----------|----------|
| `BeforeSaveServer` | Server | Перед сохранением — валидация |
| `AfterSaveServer` | Server | После сохранения — побочные эффекты |
| `SavedServer` | Server | После коммита — обновление связей |
| `SavingServer` | Server | В процессе сохранения |
| `CreatedServer` | Server | После создания нового экземпляра |
| `CreatingFromServer` | Server | При создании из другой сущности |
| `DeletingServer` | Server | Перед удалением |
| `AfterDeleteServer` | Server | После удаления |
| `BeforeStartServer` | Server | Перед стартом задачи |
| `BeforeCompleteServer` | Server | Перед завершением задания |
| `ShowingClient` | Client | При открытии карточки |
| `RefreshClient` | Client | При обновлении формы |
| `UiFilteringServer` | Server | Фильтрация списка в UI |

### Уровень свойства
| Событие | Контекст | Описание |
|---------|----------|----------|
| `ChangedShared` | Shared | Значение изменилось |
| `ValueInputClient` | Client | Пользователь ввёл значение |
| `LookupServer` | Server | Фильтрация выпадающего списка |

### Уровень вложений (AttachmentGroup)
| Событие | Контекст | Описание |
|---------|----------|----------|
| `AddedShared` | Shared | Вложение добавлено |
| `DeletedShared` | Shared | Вложение удалено |

### Уровень блока workflow
| Событие | Формат | Описание |
|---------|--------|----------|
| `{BlockName}StartAssignment` | Server | Создание задания в блоке |
| `{BlockName}CompleteAssignment` | Server | Завершение задания в блоке |

## PreviousPropertyGuid — цепочка свойств

Каждое свойство ссылается на предыдущее через `PreviousPropertyGuid`:
```
Status (наследуемое) → Name → Description → Department → ...
```

Первое пользовательское свойство ссылается на последнее наследуемое:
- `"PreviousPropertyGuid": "1dcedc29-5140-4770-ac92-eabc212326a1"` — после Status

Каждое следующее свойство: `PreviousPropertyGuid` = `NameGuid` предыдущего свойства.

## Дочерние коллекции (@ директории)

Директория с `@` содержит дочернюю сущность:
- `Document@Versions/` → `DocumentVersions.mtd`
- `Document@Tracking/` → `DocumentTracking.mtd`
- `Task@Observers/` → `TaskObservers.mtd`

Ключевые поля дочерней сущности:
```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "MyEntityTracking",
  "BaseGuid": "2d7f6507-6d0a-4bb7-b2a1-2f4248b962e7",
  "IsChildEntity": true,
  "Properties": [
    {
      "$type": "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "ParentEntity",
      "EntityGuid": "<GUID-РОДИТЕЛЬСКОЙ-СУЩНОСТИ>",
      "IsAncestorMetadata": true,
      "IsReferenceToRootEntity": true,
      "Overridden": ["EntityGuid"]
    }
  ]
}
```

## Module.mtd — расширенная структура

Помимо базовых полей, Module.mtd содержит:

### AsyncHandlers
```json
"AsyncHandlers": [
  {
    "NameGuid": "<GUID>",
    "Name": "ProcessDocument",
    "DelayPeriod": 15,
    "DelayStrategy": "RegularDelayStrategy",
    "IsHandlerGenerated": true,
    "Parameters": [
      { "NameGuid": "<GUID>", "Name": "DocumentId", "ParameterType": "LongInteger" }
    ]
  }
]
```

### Dependencies
```json
"Dependencies": [
  { "Id": "e4fe1153-919e-4732-aadc-2c8e9e5bd67d" },
  { "Id": "df83a2ea-8d43-4ec4-a34a-2e61863014df" }
]
```

### Cover
```json
"Cover": {
  "NameGuid": "<GUID>",
  "Actions": [
    {
      "$type": "Sungero.Metadata.CoverFunctionActionMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "CreateEntity",
      "FunctionName": "CreateEntity",
      "GroupId": "<GUID-ГРУППЫ>",
      "Versions": []
    },
    {
      "$type": "Sungero.Metadata.CoverEntityListActionMetadata, Sungero.Metadata",
      "NameGuid": "<GUID>",
      "Name": "OpenDatabook",
      "EntityTypeId": "<GUID-ТИПА>",
      "GroupId": "<GUID-ГРУППЫ>",
      "Versions": []
    }
  ],
  "Background": null,
  "Footer": { "NameGuid": "<GUID>", "BackgroundPosition": "Stretch", "Versions": [] },
  "Groups": [
    { "NameGuid": "<GUID>", "Name": "MainGroup", "BackgroundPosition": "Stretch",
      "TabId": "<GUID-ТАБА>", "Versions": [] }
  ],
  "Header": { "NameGuid": "<GUID>", "BackgroundPosition": "Stretch", "Versions": [] },
  "RemoteControls": [],
  "Tabs": [{ "NameGuid": "<GUID>", "Name": "Tab" }],
  "Versions": []
}
```
**ВАЖНО: CoverFunctionActionMetadata** требует клиентскую функцию в `ModuleClientFunctions.cs`:
```csharp
[LocalizeFunction("CreateEntityFunctionName", "CreateEntityFunctionDescription")]
public virtual void CreateEntity()
{
    var entity = MyEntities.Create();
    entity.Show();
}
```
Ресурсные ключи `...FunctionName` и `...FunctionDescription` — в Module.resx + Module.mtd ResourcesKeys.

### Jobs
```json
"Jobs": [
  {
    "NameGuid": "<GUID>",
    "Name": "SyncData",
    "Daily": true,
    "GenerateHandler": true,
    "MonthSchedule": "Monthly"
  }
]
```

---

## Child Collection MTD — ФИКСИРОВАННЫЕ GUIDs

Child collections (Versions, Tracking, Observers) наследуют от базовых типов.
ВСЕ секции с `IsAncestorMetadata: true` используют ФИКСИРОВАННЫЕ GUIDs.

### Document Versions (BaseGuid: `6180769e-de94-43ff-8894-e32cb5260789`)
| Секция | ФИКСИРОВАННЫЙ NameGuid |
|--------|----------------------|
| CreationArea | `8cc0db4d-d33e-4631-ad19-01e80fc8f71b` |
| ElectronicDocument (property) | `9db5ead2-a918-4f50-8aa4-f75a44cfce07` |
| RibbonCard | `a1d0f296-7ace-4cda-b8b1-d6e63ff3588e` |
| RibbonCollection | `46da8e4a-823b-4011-aab5-849abaa29d81` |

### Document Tracking (BaseGuid: `2d7f6507-6d0a-4bb7-b2a1-2f4248b962e7`)
| Секция | ФИКСИРОВАННЫЙ NameGuid |
|--------|----------------------|
| CreationArea | `4d08fccd-8eaf-487e-bf81-7535b0448dcf` |
| OfficialDocument (property) | `68863851-86ec-4a3d-aaa9-ac4d7fcbba89` |
| RibbonCard | `22e0f737-a2cf-4ea4-8949-21cb996e4fd3` |
| RibbonCollection | `f0c2a232-7f87-4b3e-bd3e-bd1b0899964f` |

### Task Observers (BaseGuid: `ac08b548-e666-4d9b-816f-a6c5e08e360f`)
| Секция | ФИКСИРОВАННЫЙ NameGuid |
|--------|----------------------|
| CreationArea | `0f51df86-ace5-4999-b599-468d1ef72526` |
| Task (property) | `f2124770-2128-42c4-8dba-baba806c77e6` |
| Observer (property) | `f1d398c9-8618-4f8f-abd5-f1e5f05aa5ce` |
| RibbonCard | `f66b3169-e202-4148-a06f-850d15f5751d` |
| RibbonCollection | `79afeb7b-6a3b-470b-a516-5c15e1658cb9` |

### Assignment Ribbon — AsgGroup
| Элемент | ФИКСИРОВАННЫЙ GUID |
|---------|-------------------|
| AsgGroup (группа кнопок) | `ac82503a-7a47-49d0-b90c-9bb512c4559c` |
| MainPage (страница) | `21a31627-5f58-4416-8c4a-90f538ee2e57` |
| SubTaskGroup | `a5a72dbc-1ff5-42ad-b56e-a94b1c51ac3a` |

**Правило:** НЕ переопределять AsgGroup в Groups[]. Кнопки ссылаются на неё через `ParentGuid`.

### Task RouteScheme
| Поле | Значение |
|------|----------|
| Scheme.NameGuid | `c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1` |
| CurrentVersionGuid | Уникальный (генерировать) |
| IsAncestorMetadata | `true` |
| Overridden | `["CurrentVersionGuid"]` |

---

## Полная таблица фиксированных ancestor GUIDs по базовому типу

| Базовый тип | FilterPanel | CreationArea |
|------------|-------------|--------------|
| DatabookEntry (`04581d26`) | `b0125fbd-3b91-4dbb-914a-689276216404` | `f7766750-eee2-4fcd-8003-5c06a90d1f44` |
| OfficialDocument (`58cca102`) | `80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f` | `8fdd9ada-0b64-4850-b290-4a9c745d6b52` |
| Task (`d795d1f6`) | `bd0a4ce3-3467-48ad-b905-3820bf6b9da6` | `7c696893-ac69-42ca-b419-dddeab6d82ed` |
| Assignment (`91cbfdc8`) | `23d98c0f-b348-479d-b1fb-ccdcf2096bd2` | `0e5344c6-4b4c-4b42-9cc6-513415d3a8e5` |
| Notice (`ef79164b`) | `8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f` | `382c1362-76ca-4ddb-a3d3-70d8668e993e` |

---

## Custom Child Entity (табличная часть / дочерняя коллекция)

BaseGuid: `a3d38bf5-0414-41f6-bb33-a4621d2e5a60`

**КРИТИЧНО:** Минимальные заглушки → NullReferenceException в InterfacesGenerator!

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<UNIQUE-GUID>",
  "Name": "<ChildEntityName>",
  "BaseGuid": "a3d38bf5-0414-41f6-bb33-a4621d2e5a60",
  "CanBeNavigationPropertyType": true,
  "Code": "<CODE-7>",
  "CreationAreaMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "IsChildEntity": true,
  "IsVisible": false,
  "NonVisualAuthorizationMode": "FullAccess",
  "Overridden": ["IsVisible", "AccessRightsMode", "NonVisualAuthorizationMode"],
  "Properties": [
    {
      "$type": "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
      "NameGuid": "<UNIQUE-GUID>",
      "Name": "<ParentName>",
      "Code": "<CODE>",
      "EntityGuid": "<ParentEntityGuid>",
      "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder",
      "Versions": [],
      "IsRequired": true,
      "IsReferenceToRootEntity": true
    }
  ],
  "PublicStructures": [],
  "ResourcesKeys": [],
  "RibbonCardMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "RibbonCard",
    "Categories": [], "Elements": [], "Groups": [],
    "IsAncestorMetadata": true,
    "Pages": [],
    "RibbonKind": "Card",
    "Versions": []
  },
  "RibbonCollectionMetadata": {
    "NameGuid": "<UNIQUE-GUID>",
    "Name": "RibbonCollection",
    "Categories": [], "Elements": [], "Groups": [],
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

### Правила custom child entity:
- **Первое свойство** = IsReferenceToRootEntity **БЕЗ PreviousPropertyGuid** (Status `1dcedc29` НЕ существует в ChildEntity!)
- **НЕ IsAutoGenerated** (auto-generated = только Versions/Tracking/Observers)
- В родительском .mtd → `CollectionPropertyMetadata` с EntityGuid дочерней
- CreationArea/Ribbon/DomainApi:2 — **ОБЯЗАТЕЛЬНЫ**

---

## DDS Import — Каталог ошибок и решений

| # | Ошибка | Причина | Решение |
|---|--------|---------|---------|
| 1 | NullRef в InterfacesGenerator | Child без CreationArea/Ribbon | Полная структура обязательна |
| 2 | NullRef в InterfacesGenerator | PreviousPropertyGuid → Status в ChildEntity | Первое свойство child — без PreviousPropertyGuid |
| 3 | NullRef в InterfacesGenerator | Неверный EntityGuid платформы | Верифицировать по archive/base/ |
| 4 | RouteScheme sync | Scheme.NameGuid ≠ c7ae4ee8 | Фиксированный: `c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1` |
| 5 | ReferenceToRootEntity not found | Child .mtd — заглушка | Полная структура с Properties |
| 6 | Observers BaseGuid | BaseGuid ≠ ac08b548-e666 | `ac08b548-e666-4d9b-816f-a6c5e08e360f` |
| 7 | Error converting Months | MonthSchedule="Daily" | Monthly/January.../December |
| 8 | Cover $type | CoverComputableActionMetadata | EntityList/Function/Report/ComputableFolder |

### BaseGuid дочерних коллекций

| Тип | BaseGuid |
|-----|----------|
| Versions (документ) | `38660ede-96d3-4c57-87a5-8fe53a47615c` |
| Tracking (документ) | `2d7f6507-6d0a-4bb7-b2a1-2f4248b962e7` |
| Observers (задача) | `ac08b548-e666-4d9b-816f-a6c5e08e360f` |
| Custom ChildEntity | `a3d38bf5-0414-41f6-bb33-a4621d2e5a60` |

### EntityGuid платформенных сущностей

| Сущность | NameGuid |
|----------|----------|
| Employee | `b7905516-2be5-4931-961c-cb38d5677565` |
| Department | `61b1c19f-26e2-49a5-b3d3-0d3618151e12` |
| Counterparty | `294767f1-009f-4fbd-80fc-f98c49ddc560` |
| Contact | `c8daaef9-a679-4a29-ac01-b93c1637c72e` |
| OfficialDocument | `58cca102-1e97-4f07-b6ac-fd866a8b7cb1` |

---

### LayerModuleMetadata (перекрытие модуля)
```json
{
  "$type": "Sungero.Metadata.LayerModuleMetadata, Sungero.Metadata",
  "NameGuid": "<GUID>",
  "Name": "LayerModule",
  "BaseGuid": "<GUID-БАЗОВОГО-МОДУЛЯ>",
  "ExplorerTreeOrder": [...],
  "IsVisible": true,
  "Overridden": ["ExplorerTreeOrder", "IsVisible"]
}
```
