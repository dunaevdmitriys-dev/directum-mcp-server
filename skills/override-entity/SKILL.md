---
description: "Перекрыть существующий тип сущности или модуль Directum RX"
---

# Перекрытие типа сущности или модуля Directum RX

---

## ЧАСТЬ 1: Override сущности

### ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Найди примеры перекрытий в текущем проекте:
Glob("{project_path}/source/**/*.mtd")

# Эталон перекрытия Counterparty:
MCP: search_metadata name=Counterparty

# Изучи паттерн перекрытия через MCP:
MCP: extract_entity_schema entity=Counterparty
```

### Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля (должен уже существовать)
- **OriginalEntityName** — имя базовой сущности (например, `ContractBase`)
- **OriginalModuleName** — модуль базовой сущности (например, `Sungero.Contracts`)
- **BaseGuid** — GUID перекрываемого типа
- **NewProperties** — новые свойства (если нужны)
- **OverriddenEvents** — какие события переопределить

### Наследование vs Перекрытие

| Наследование | Перекрытие |
|-------------|-----------|
| Создаёт **новый** тип | **Изменяет** существующий |
| Свой NameGuid | BaseGuid = GUID перекрываемого типа |
| Для новых видов документов | Для изменения поведения базовых типов |

**Перекрытие** = тот же тип, но с дополнительной логикой/свойствами.

### Алгоритм

#### 1. Найди BaseGuid перекрываемой сущности
Если пользователь не указал, посмотри в существующих пакетах или спроси.

#### 2. Создай Entity.mtd
```json
{
  "$type": "Sungero.Metadata.DocumentMetadata, Sungero.Content.Shared",
  "NameGuid": "{NewEntityGuid}",
  "Name": "{OriginalEntityName}",
  "BaseGuid": "{OriginalEntityGuid}",
  "OverriddenBaseGuid": "{OriginalEntityGuid}",
  "Properties": [
    // ТОЛЬКО новые свойства — базовые наследуются автоматически
  ],
  "Forms": [{
    "Name": "Card",
    "Controls": [
      // IsAncestorMetadata: true для унаследованных контролов
      // Новые контролы для новых свойств
    ]
  }]
}
```

#### 3. Создай файловую структуру
Такая же как для обычной сущности:
```
...Shared/{OriginalEntityName}/
  {OriginalEntityName}.mtd
  {OriginalEntityName}.resx / .ru.resx
  {OriginalEntityName}Constants.cs
  {OriginalEntityName}SharedFunctions.cs
  {OriginalEntityName}Structures.cs
  {OriginalEntityName}Handlers.cs
...Server/{OriginalEntityName}/
  {OriginalEntityName}ServerFunctions.cs
  {OriginalEntityName}Handlers.cs
  {OriginalEntityName}Queries.xml
...ClientBase/{OriginalEntityName}/
  {OriginalEntityName}ClientFunctions.cs
  {OriginalEntityName}Handlers.cs
  {OriginalEntityName}Actions.cs
```

#### 4. Переопредели события

В Server/{OriginalEntityName}Handlers.cs:
```csharp
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  base.BeforeSave(e);
  // Дополнительная логика
}
```

В ClientBase/{OriginalEntityName}Handlers.cs:
```csharp
public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
{
  base.Showing(e);
  // Управление видимостью новых полей
}
```

#### 5. Добавь Public-функции

```csharp
[Public]
public virtual string GetCustomInfo()
{
  return _obj.NewProperty;
}
```

#### 6. IsAncestorMetadata
Унаследованные элементы в .mtd помечаются `"IsAncestorMetadata": true`. Не создавай их заново — они подтягиваются автоматически.

#### 7. Валидация
- [ ] BaseGuid/OverriddenBaseGuid указывают на существующую сущность
- [ ] Только НОВЫЕ свойства в Properties (не дублировать базовые)
- [ ] `base.EventName(e)` вызывается в каждом переопределённом обработчике
- [ ] Namespace корректный

---

## ЧАСТЬ 2: Override модуля (LayerModuleMetadata)

> Верифицировано на production-примере: **omniapplied** (Sungero.Omni перекрывает платформенный Sungero.Company).

### ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Эталон LayerModule — omniapplied:
Read omniapplied/source/Sungero.Omni/Sungero.Omni.Shared/Sungero.Company/Module.mtd

# Эталон override серверных функций:
Read omniapplied/source/Sungero.Omni/Sungero.Omni.Server/Sungero.Company/ModuleServerFunctions.cs

# Полный каталог паттернов:
Read omniapplied/REFERENCE_CATALOG.md
```

### Входные данные
- **SolutionModuleName** — имя своего solution-модуля (например, `Sungero.Omni`)
- **SolutionModuleGuid** — GUID своего solution-модуля
- **TargetModuleName** — имя перекрываемого платформенного модуля (например, `Sungero.Company`)
- **TargetModuleGuid** — GUID перекрываемого модуля
- **OverriddenHandlers** — какие async-handler callback-ы переопределить
- **OverriddenFolders** — какие SpecialFolders перекрыть (FolderViewUuid)

### Ключевые отличия от обычного ModuleMetadata

| Поле | Обычный модуль | LayerModule (override) |
|------|---------------|----------------------|
| `$type` | `ModuleMetadata` | **`LayerModuleMetadata`** |
| `BaseGuid` | нет или свой parent | **GUID перекрываемого модуля** |
| `LayeredFromGuid` | нет | **GUID перекрываемого модуля** (= BaseGuid) |
| `AssociatedGuid` | нет | **GUID своего solution-модуля** |
| `Code` | свой уникальный | **совпадает с перекрываемым** |
| `IsVisible` | true/false | обычно `false` |

### Алгоритм

#### 1. Создай Module.mtd (LayerModuleMetadata)

Размещение: `{SolutionModule}.Shared/{TargetModuleName}/Module.mtd`

```json
{
  "$type": "Sungero.Metadata.LayerModuleMetadata, Sungero.Metadata",
  "NameGuid": "{NewLayerGuid}",
  "Name": "{TargetModuleShortName}",
  "AssociatedGuid": "{SolutionModuleGuid}",
  "BaseGuid": "{TargetModuleGuid}",
  "LayeredFromGuid": "{TargetModuleGuid}",
  "Code": "{TargetModuleCode}",
  "CompanyCode": "{CompanyCode}",
  "ClientBaseAssemblyName": "{SolutionModule}.ClientBase",
  "ClientBaseNamespace": "{SolutionModule}.Module.{TargetModuleShortName}.ClientBase",
  "ResourceInterfaceAssemblyName": "Sungero.Domain.Interfaces",
  "ResourceInterfaceNamespace": "{SolutionModule}.Module.{TargetModuleShortName}",
  "IsVisible": false,
  "Importance": "Medium",
  "AsyncHandlers": [],
  "Jobs": [],
  "SpecialFolders": [],
  "ExplorerTreeOrder": [],
  "PublicStructures": [],
  "Widgets": [],
  "Version": "26.1.0.1",
  "Versions": [
    { "Type": "LayerModuleMetadata", "Number": 11 },
    { "Type": "ModuleMetadata", "Number": 11 }
  ]
}
```

**Критические правила:**
- `BaseGuid` = `LayeredFromGuid` = GUID платформенного модуля
- `AssociatedGuid` = GUID своего solution-модуля
- `Code` **СОВПАДАЕТ** с кодом перекрываемого модуля (например, `"Company"`)

#### 2. Наследуй AsyncHandlers (если нужно переопределить callback-ы)

Каждый наследуемый handler помечается `"IsAncestorMetadata": true` с ПОЛНЫМ списком Parameters:

```json
"AsyncHandlers": [
  {
    "NameGuid": "{GUID-из-базового-модуля}",
    "Name": "TransferEmployeeToDepartment",
    "DelayPeriod": 15,
    "DelayStrategy": "ExponentialDelayStrategy",
    "IsAncestorMetadata": true,
    "MaxRetryCount": 1000,
    "Parameters": [
      {
        "NameGuid": "{GUID-параметра-из-базового}",
        "Name": "EmployeeId",
        "ParameterType": "LongInteger"
      }
    ]
  }
]
```

#### 3. Наследуй Jobs (если нужно переопределить)

```json
"Jobs": [
  {
    "NameGuid": "{GUID-из-базового-модуля}",
    "Name": "ExcludeEmployeesFromManagementUnits",
    "IsAncestorMetadata": true,
    "MonthSchedule": "Monthly",
    "StartAt": "1753-01-01T01:00:00"
  }
]
```

#### 4. Перекрой SpecialFolders (FolderView)

```json
"SpecialFolders": [
  {
    "NameGuid": "{GUID-из-базового-модуля}",
    "Name": "ManagersAssistants",
    "DisplayType": "Module",
    "FolderViewUuid": "{GUID-нового-FolderView-XML}",
    "IsAncestorMetadata": true,
    "IsShow": false,
    "NeedShowMarkedCount": false,
    "Overridden": [
      "FolderViewUuid"
    ]
  }
]
```

**Важно:** `Overridden: ["FolderViewUuid"]` — явно указывает какое поле перекрыто. FolderView XML лежит в `ClientBase/{TargetModuleName}/`.

#### 5. Создай файловую структуру

```
{SolutionModule}.Shared/
  {TargetModuleName}/
    Module.mtd                      # LayerModuleMetadata
    Module.resx / Module.ru.resx    # Локализация (может быть пустой)

{SolutionModule}.Server/
  {TargetModuleName}/
    ModuleServerFunctions.cs        # override-функции
    ModuleInitializer.cs            # (может быть пустой)

{SolutionModule}.ClientBase/
  {TargetModuleName}/
    *.xml                           # FolderView XML (если перекрываешь списки)
```

Пример из omniapplied:
```
Sungero.Omni.Shared/Sungero.Company/Module.mtd
Sungero.Omni.Server/Sungero.Company/ModuleServerFunctions.cs
Sungero.Omni.Server/Sungero.Company/ModuleInitializer.cs
Sungero.Omni.ClientBase/Sungero.Company/*.xml
```

#### 6. Override серверных функций

В `Server/{TargetModuleName}/ModuleServerFunctions.cs`:

```csharp
namespace {SolutionNamespace}.Module.{TargetModuleShortName}.Server
{
  partial class ModuleFunctions
  {
    public override bool ProcessCreatedIdentityServiceUser(
      Sungero.Company.Server.AsyncHandlerInvokeArgs.ConnectUsersToExternalAppsInvokeArgs args,
      Sungero.Company.IUserBase user,
      IdentityServiceExtensions.UserCredentials userCredentials)
    {
      // ОБЯЗАТЕЛЬНО: вызов base — цепочка перекрытий
      var result = base.ProcessCreatedIdentityServiceUser(args, user, userCredentials);

      // Своя логика
      if (!this.ShouldProcess(args))
        return result;

      MyModule.PublicFunctions.Module.DoSomething(user);
      return true;
    }
  }
}
```

**КРИТИЧНО:** `base.Method()` вызывается ВСЕГДА. Без этого ломается цепочка перекрытий.

#### 7. Валидация

- [ ] `$type` = `LayerModuleMetadata` (НЕ `ModuleMetadata`)
- [ ] `BaseGuid` = `LayeredFromGuid` = GUID платформенного модуля
- [ ] `AssociatedGuid` = GUID своего solution-модуля
- [ ] `Code` совпадает с кодом перекрываемого модуля
- [ ] Все наследуемые AsyncHandlers/Jobs имеют `IsAncestorMetadata: true`
- [ ] SpecialFolders с перекрытием имеют `Overridden: ["FolderViewUuid"]`
- [ ] Все override-методы вызывают `base.Method()`
- [ ] Namespace: `{SolutionNamespace}.Module.{TargetModuleShortName}.Server`
- [ ] `Versions` содержит оба типа: `LayerModuleMetadata` и `ModuleMetadata`

---

## Reference-файлы

| Паттерн | Файл |
|---------|------|
| LayerModuleMetadata (эталон) | `omniapplied/source/Sungero.Omni/Sungero.Omni.Shared/Sungero.Company/Module.mtd` |
| Override серверных функций | `omniapplied/source/Sungero.Omni/Sungero.Omni.Server/Sungero.Company/ModuleServerFunctions.cs` |
| Полный каталог паттернов | `omniapplied/REFERENCE_CATALOG.md` |

## Справка
- Правила DDS-импорта и валидации (partial classes, ancestor GUIDs, DomainApi:2, .resx формат, Structures, Result.Overridden): см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
