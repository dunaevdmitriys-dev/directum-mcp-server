---
description: "Создать новый справочник (DatabookEntry) Directum RX"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

# Создание справочника Directum RX

## Reference: Targets (эталоны сложных DatabookEntry)

| Файл | Путь |
|------|------|
| **Period (18KB, эталон)** | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Shared/Period/Period.mtd` |
| **Metric (35KB, KPI)** | `targets/source/DirRX.KPI/DirRX.KPI.Shared/Metric/Metric.mtd` |
| **ResultGrade** | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Shared/ResultGrade/ResultGrade.mtd` |
| **Каталог** | `targets/REFERENCE_CATALOG.md` |

**Паттерны:**
- **Period**: ExternalLink для предопределенных записей (7 периодов), Enum (Measure), int (RelativeSize, Amount)
- **Metric**: сложнейший справочник (35KB) с RemoteTableControl, JSON-данными, FilterPanel
- **ResultGrade**: цвет (Color Enum: Green/Blue/Yellow/Red), границы (MinValue/MaxValue)

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `scaffold_entity baseType=DatabookEntry` — генерация MTD + resx + C# стабов за один вызов
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей System.resx из MTD
- `validate_guid_consistency` — проверка GUID в Controls/Properties/resx
- `search_metadata` — поиск эталонных сущностей в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

**Перед генерацией — подглядывай в платформу.** Не генерируй из головы.

```
# Найди похожий справочник в платформе:
MCP: search_metadata name=<ключевое_слово_похожей_сущности>

# Или прочитай эталонный:
# Платформенные справочники находятся в base/ внутри контейнеров RX
# Используй MCP: search_metadata name=Employee для поиска эталонов

# Посмотри как устроены свойства, формы, контролы у рабочей сущности.
# Используй как образец при генерации.
```

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля (должен уже существовать)
- **EntityName** — имя справочника (PascalCase, например, `MaterialType`)
- **DisplayNameRu** — отображаемое имя на русском
- **Properties** — список свойств (имя, тип, обязательность)
- **HasHierarchy** — иерархический справочник? (по умолчанию: нет)

## КРИТИЧНО — правильные значения

### BaseGuid и $type
| Тип | $type | BaseGuid |
|-----|-------|----------|
| DatabookEntry | `Sungero.Metadata.EntityMetadata, Sungero.Metadata` | `04581d26-0780-4cfd-b3cd-c2cafc5798b0` |

**ВНИМАНИЕ:**
- `$type` = `Sungero.Metadata.EntityMetadata, Sungero.Metadata` (НЕ `Sungero.Domain.Shared`!)
- BaseGuid = `04581d26-0780-4cfd-b3cd-c2cafc5798b0` (НЕ `04581d26-0571-...`!)

### DataBinder типы для контролов

| Тип свойства | DataBinderTypeName (контрол) | ListDataBinderTypeName (список) |
|---|---|---|
| String | `Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder` | то же |
| Text (многострочный) | `Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder` | `StringEditorToStringBinder` |
| Integer | `Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder` | то же |
| Double | `Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder` | то же |
| Boolean | `Sungero.Presentation.CommonDataBinders.BooleanEditorToBooleanBinder` | то же |
| DateTime | `Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder` | то же |
| Enum | `Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumerationBinder` | то же |
| Navigation | `Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder` | то же |

**ВНИМАНИЕ:** Для String используй `StringEditorToStringBinder` (НЕ `TextEditorToTextBinder`!)

## Алгоритм

### 1. MCP генерация (СНАЧАЛА попробуй MCP)

```
MCP: scaffold_entity outputPath={путь_к_модулю} entityName={EntityName} moduleName={CompanyCode}.{ModuleName} baseType=DatabookEntry properties="Name:string,{другие_свойства}" russianName="{DisplayNameRu}"
```
Если MCP доступен — используй результат. Затем проверь:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
MCP: validate_guid_consistency modulePath={путь_к_модулю}
```
Если MCP недоступен — генерируй вручную по шаблону ниже.

### 2. Сгенерируй GUID (ручной fallback)
Нужны уникальные GUID для:
- `EntityGuid` — NameGuid сущности
- GUID для каждого свойства (PropertyGuid)
- GUID для формы Card (FormGuid)
- GUID для группы контролов (ControlGroupGuid)
- GUID для каждого контрола (ControlGuid)
- GUID для FilterPanel, RibbonCard, RibbonCollection, CreationArea, FormViewUuid

### 2. Создай файлы

```
source/{CompanyCode}.{ModuleName}/
  {CompanyCode}.{ModuleName}.Shared/
    {EntityName}/
      {EntityName}.mtd
      {EntityName}.resx
      {EntityName}.ru.resx
      {EntityName}Constants.cs
      {EntityName}SharedFunctions.cs
      {EntityName}Structures.cs
      {EntityName}Handlers.cs
  {CompanyCode}.{ModuleName}.Server/
    {EntityName}/
      {EntityName}ServerFunctions.cs
      {EntityName}Handlers.cs
      {EntityName}Queries.xml
  {CompanyCode}.{ModuleName}.ClientBase/
    {EntityName}/
      {EntityName}ClientFunctions.cs
      {EntityName}Handlers.cs
      {EntityName}Actions.cs
```

### 3. Заполни Entity.mtd

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "NameGuid": "<EntityGuid>",
  "Name": "<EntityName>",
  "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
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
    "NameGuid": "b0125fbd-3b91-4dbb-914a-689276216404",
    "Name": "FilterPanel",
    "Controls": [],
    "IsAncestorMetadata": true,
    "Versions": []
  },
  "Forms": [
    {
      "$type": "Sungero.Metadata.StandaloneFormMetadata, Sungero.Metadata",
      "NameGuid": "<FormGuid>",
      "Name": "Card",
      "Controls": [
        {
          "$type": "Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata",
          "NameGuid": "<ControlGroupGuid>",
          "Name": "Main",
          "SharedNestedGroupsAlignment": true,
          "Versions": []
        },
        {
          "$type": "Sungero.Metadata.ControlMetadata, Sungero.Metadata",
          "NameGuid": "<ControlGuid>",
          "Name": "Name",
          "ColumnNumber": 0,
          "ColumnSpan": 2,
          "DataBinderTypeName": "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
          "ParentGuid": "<ControlGroupGuid>",
          "PropertyGuid": "<PropertyGuid-Name>",
          "RowNumber": 0,
          "RowSpan": 1,
          "Settings": [],
          "Versions": []
        }
      ],
      "Versions": []
    }
  ],
  "FormViewUuid": "<GUID>",
  "HandledEvents": [],
  "IconResourcesKeys": [],
  "IntegrationServiceName": "<ModuleName><EntityName>",
  "OperationsClass": "",
  "Properties": [
    {
      "$type": "Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata",
      "NameGuid": "<PropertyGuid-Name>",
      "Name": "Name",
      "Code": "Name",
      "IsDisplayValue": true,
      "IsQuickSearchAllowed": true,
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

### 4. Добавление свойств

Для каждого пользовательского свойства добавь:
1. В `Properties` — метаданные свойства с `PreviousPropertyGuid` = GUID предыдущего
2. В `Forms[0].Controls` — контрол с `PropertyGuid` и `DataBinderTypeName`

**Цепочка PreviousPropertyGuid:**
```
Status (наследуемый, 1dcedc29-...) → Name → Свойство1 → Свойство2 → ...
```

**Пример Navigation свойства:**
```json
{
  "$type": "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
  "NameGuid": "<PropertyGuid>",
  "Name": "Department",
  "Code": "Department",
  "EntityGuid": "<GUID-целевой-сущности>",
  "HandledEvents": [],
  "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder",
  "PreviousPropertyGuid": "<GUID-предыдущего-свойства>",
  "Versions": []
}
```

**Пример Enum свойства:**
```json
{
  "$type": "Sungero.Metadata.EnumPropertyMetadata, Sungero.Metadata",
  "NameGuid": "<PropertyGuid>",
  "Name": "Priority",
  "Code": "Priority",
  "DirectValues": [
    { "NameGuid": "<GUID>", "Name": "Low", "Code": "Low" },
    { "NameGuid": "<GUID>", "Name": "Normal", "Code": "Normal" },
    { "NameGuid": "<GUID>", "Name": "High", "Code": "High" }
  ],
  "ListDataBinderTypeName": "Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumerationBinder",
  "PreviousPropertyGuid": "<GUID-предыдущего>",
  "Versions": []
}
```

### 5. Using statements для .cs файлов

**Server (.Server/):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

**Client (.ClientBase/) — namespace `.Client` (НЕ `.ClientBase`!):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
```

**Shared:**
```csharp
using System;
using Sungero.Core;
using Sungero.CoreEntities;
```

### 6. HandledEvents

Добавляй события в `HandledEvents` сущности:
- `"BeforeSaveServer"` — валидация при сохранении
- `"ShowingClient"` — настройка видимости при открытии
- `"RefreshClient"` — обновление формы
- `"CreatedServer"` — заполнение значений по умолчанию

Для свойств — в `HandledEvents` свойства:
- `"ChangedShared"` — реакция на изменение значения
- `"ValueInputClient"` — валидация ввода

### 7. System.resx файлы (системные ресурсы)

Помимо пользовательских `.resx`, DDS генерирует `*System.resx` / `*System.ru.resx` для подписей свойств, действий и перечислений. При ручном создании пакета ОБЯЗАТЕЛЬНО создай их:

**{EntityName}System.resx** и **{EntityName}System.ru.resx** — содержат:
```xml
<!-- Подписи свойств — ОБЯЗАТЕЛЬНО формат Property_<PropertyName> -->
<data name="Property_Name" xml:space="preserve"><value>Наименование</value></data>
<data name="Property_Status" xml:space="preserve"><value>Состояние</value></data>
<!-- DisplayName и CollectionDisplayName — БЕЗ префикса Property_ -->
<data name="DisplayName" xml:space="preserve"><value>Тип справочника</value></data>
<data name="CollectionDisplayName" xml:space="preserve"><value>Типы справочников</value></data>
<!-- Перечисления — формат Enum_<EnumName>_<Value> -->
<data name="Enum_Priority_High" xml:space="preserve"><value>Высокий</value></data>
```

**КРИТИЧНО**: Ключи ОБЯЗАНЫ быть `Property_<PropertyName>`, НЕ `Resource_<GUID>`. Runtime DDS 26.1 ищет ресурсы только по формату `Property_<PropertyName>`.

### 8. Обнови Module.mtd
Добавь EntityGuid в `ExplorerTreeOrder` модуля.

## Справочные материалы
DDS known issues и чеклисты → CLAUDE.md
Антипаттерны Claude → /dds-guardrails
Валидация → /validate-all

## DDS-правила
Все правила импорта DDS → см. CLAUDE.md (пункты 1-18). Не дублировать здесь.

