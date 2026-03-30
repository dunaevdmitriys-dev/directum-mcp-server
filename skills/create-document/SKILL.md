---
description: "Создать новый тип документа Directum RX"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

# Создание типа документа Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `scaffold_entity baseType=Document` — генерация MTD + Versions + Tracking + resx + C# стабов за один вызов
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей System.resx из MTD
- `validate_guid_consistency` — проверка GUID в Controls/Properties/resx
- `search_metadata filterType=Document` — поиск эталонных документов в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Найди похожий документ в платформе:
MCP: search_metadata type=DocumentMetadata

# Эталоны:
# Платформенные документы находятся в base/ внутри контейнеров RX
# Используй MCP: search_metadata type=DocumentMetadata для поиска эталонов

# Посмотри Versions, Tracking дочерние сущности — используй как образец.
```

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля (должен уже существовать)
- **EntityName** — имя типа документа (PascalCase, например, `PurchaseOrder`)
- **BaseType** — базовый тип (по умолчанию `OfficialDocument`)
- **Properties** — список свойств (имя, тип, обязательность)
- **DisplayNameRu** — отображаемое имя на русском

## КРИТИЧНО — правильные BaseGuid и $type

| Базовый тип | $type | BaseGuid |
|---|---|---|
| OfficialDocument | `Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared` | `58cca102-1e97-4f07-b6ac-fd866a8b7cb1` |
| InternalDocumentBase | `Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared` | `09636f5c-51da-4f85-8c23-c74f7e920771` |
| ContractualDocumentBase | `Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared` | `306da7fa-dc27-437c-bb83-42c92436b7e2` |
| AccountingDocumentBase | `Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared` | `96c4f4f3-dc74-497a-b347-e8faf4afe320` |
| SimpleDocument | `Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared` | `09636f5c-51da-4f85-8c23-c74f7e920771` |

**Замечание:** `$type` для ВСЕХ документов = `DocumentMetadata, Sungero.Content.Shared`

## Алгоритм

### 1. MCP генерация (СНАЧАЛА попробуй MCP)

```
MCP: scaffold_entity outputPath={путь_к_модулю} entityName={EntityName} moduleName={CompanyCode}.{ModuleName} baseType=Document properties="Name:string,{другие_свойства}" russianName="{DisplayNameRu}"
```
Если MCP доступен — используй результат (включая Versions и Tracking). Затем проверь:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
MCP: validate_guid_consistency modulePath={путь_к_модулю}
```
Если MCP недоступен — генерируй вручную по шаблону ниже.

### 2. Сгенерируй GUID (ручной fallback)
- `EntityGuid` — NameGuid сущности
- `VersionsEntityGuid` — дочерняя коллекция версий
- `TrackingEntityGuid` — дочерняя коллекция выдачи
- GUID для свойств, контролов, формы, FilterPanel, RibbonCard, RibbonCollection

### 2. Создай файлы

```
source/{CompanyCode}.{ModuleName}/
  ...Shared/
    {EntityName}/
      {EntityName}.mtd
      {EntityName}.resx / .ru.resx
      {EntityName}Constants.cs
      {EntityName}SharedFunctions.cs
      {EntityName}Structures.cs
      {EntityName}Handlers.cs
    {EntityName}@Versions/
      {EntityName}Versions.mtd
      {EntityName}Versions.resx / .ru.resx
    {EntityName}@Tracking/
      {EntityName}Tracking.mtd
      {EntityName}Tracking.resx / .ru.resx
  ...Server/
    {EntityName}/
      {EntityName}ServerFunctions.cs
      {EntityName}Handlers.cs
      {EntityName}Queries.xml
  ...ClientBase/
    {EntityName}/
      {EntityName}ClientFunctions.cs
      {EntityName}Handlers.cs
      {EntityName}Actions.cs
```

### 3. Entity.mtd

```json
{
  "$type": "Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared",
  "NameGuid": "<EntityGuid>",
  "Name": "<EntityName>",
  "AccessRightsMode": "Both",
  "BaseGuid": "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
  "CanBeNavigationPropertyType": true,
  "Code": "<EntityName>",
  "CreationAreaMetadata": {
    "NameGuid": "<GUID>",
    "Name": "CreationArea",
    "Buttons": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "ExtraSearchProperties": [],
  "FilterPanel": {
    "NameGuid": "80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "Forms": [
    {
      "$type": "Sungero.Metadata.StandaloneFormMetadata, Sungero.Metadata",
      "NameGuid": "fa03f748-4397-42ef-bdc2-22119af7bf7f",
      "Name": "Card",
      "Controls": [
        {
          "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
          "NameGuid": "<ControlGroupGuid>",
          "Name": "Main",
          "SharedNestedGroupsAlignment": true,
          "Versions": []
        }
      ],
      "IsAncestorMetadata": true,
      "Overridden": ["Controls", "SettingsResourceKey"],
      "Versions": []
    }
  ],
  "FormViewUuid": "<GUID>",
  "HandledEvents": [],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><EntityName>",
  "OpenCardByDefaultInCollection": true,
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata",
      "NameGuid": "56cbe741-880f-4e6f-9567-343d08494b59",
      "Name": "Versions",
      "EntityGuid": "<VersionsEntityGuid>",
      "IsAncestorMetadata": true,
      "Overridden": ["EntityGuid"],
      "Versions": []
    },
    {
      "$type": "Sungero.Metadata.CollectionPropertyMetadata, Sungero.Metadata",
      "NameGuid": "15280407-331e-42f6-b263-041a495b66cd",
      "Name": "Tracking",
      "EntityGuid": "<TrackingEntityGuid>",
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
  "Versions": [
    { "Type": "DocumentMetadata", "Number": 2 },
    { "Type": "EntityMetadata", "Number": 13 },
    { "Type": "DomainApi", "Number": 2 }
  ]
}
```

### 4. Versions.mtd (дочерняя коллекция) — ПОЛНАЯ СТРУКТУРА

Файл: `{EntityName}@Versions/{EntityName}Versions.mtd`

**КРИТИЧНО:** Дочерние сущности ОБЯЗАНЫ содержать ВСЕ секции (CreationArea, Ribbon, DomainApi:2).
Минимальные заглушки вызывают NullReferenceException в InterfacesGenerator!

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<VersionsEntityGuid>",
  "Name": "<EntityName>Versions",
  "BaseGuid": "38660ede-96d3-4c57-87a5-8fe53a47615c",
  "CanBeAncestor": true,
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
      "NameGuid": "9db5ead2-a918-4f50-8aa4-f75a44cfce07",
      "Name": "ElectronicDocument",
      "EntityGuid": "<EntityGuid-ДОКУМЕНТА>",
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

### 5. Tracking.mtd (дочерняя коллекция) — ПОЛНАЯ СТРУКТУРА

Файл: `{EntityName}@Tracking/{EntityName}Tracking.mtd`
```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<TrackingEntityGuid>",
  "Name": "<EntityName>Tracking",
  "BaseGuid": "2d7f6507-6d0a-4bb7-b2a1-2f4248b962e7",
  "CanBeAncestor": true,
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
      "NameGuid": "68863851-86ec-4a3d-aaa9-ac4d7fcbba89",
      "Name": "OfficialDocument",
      "EntityGuid": "<EntityGuid-ДОКУМЕНТА>",
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

### 6. Обработчики документа

**Server — BeforeSave:**
```csharp
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  base.BeforeSave(e);
  // Валидация обязательных полей
}
```

**Server — CreatingFrom (создание из другого документа):**
```csharp
public override void CreatingFrom(Sungero.Domain.CreatingFromEventArgs e)
{
  base.CreatingFrom(e);
  e.Without(_obj.Info.Properties.RegistrationNumber);
  e.Without(_obj.Info.Properties.RegistrationDate);
}
```

**Client — Showing:**
```csharp
public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
{
  base.Showing(e);
  // Настройка видимости свойств
}
```

### 7. Добавление свойств

Для каждого свойства:
1. Добавь в `Properties` с правильным `$type` и `PreviousPropertyGuid`
2. Добавь контрол в `Forms[0].Controls` с `PropertyGuid` и `DataBinderTypeName`

**DataBinder-ы:**
| Тип | DataBinderTypeName |
|---|---|
| String | `Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder` |
| Text | `Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder` |
| Integer/Double | `Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder` |
| Boolean | `Sungero.Presentation.CommonDataBinders.BooleanEditorToBooleanBinder` |
| DateTime | `Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder` |
| Enum | `Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumerationBinder` |
| Navigation | `Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder` |

### 8. System.resx файлы (системные ресурсы)

Создай `{EntityName}System.resx` и `{EntityName}System.ru.resx` с подписями свойств:
```xml
<!-- Ключи ОБЯЗАТЕЛЬНО в формате Property_<PropertyName> -->
<data name="Property_Name" xml:space="preserve"><value>Наименование</value></data>
<data name="DisplayName" xml:space="preserve"><value>Тип документа</value></data>
<data name="CollectionDisplayName" xml:space="preserve"><value>Типы документов</value></data>
```

**КРИТИЧНО**: Использовать `Property_<PropertyName>`, НЕ `Resource_<GUID>`. Runtime DDS 26.1 резолвит подписи только по формату `Property_<PropertyName>`.

### 9. Обнови Module.mtd
Добавь EntityGuid в `ExplorerTreeOrder` модуля.

### 9. Using statements

**Server:** `using System; using System.Collections.Generic; using System.Linq; using Sungero.Core; using Sungero.CoreEntities;`
**Client (namespace `.Client`):** то же
**Shared:** `using System; using Sungero.Core; using Sungero.CoreEntities;`

## Справочные материалы
DDS known issues и чеклисты → CLAUDE.md
Антипаттерны Claude → /dds-guardrails
Валидация → /validate-all

## DDS-правила
Все правила импорта DDS → см. CLAUDE.md (пункты 1-18). Не дублировать здесь.

## Валидация
Запусти /validate-all после создания.