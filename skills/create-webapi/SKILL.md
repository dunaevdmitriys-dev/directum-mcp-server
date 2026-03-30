---
description: "Создать WebAPI endpoint Directum RX — GET/POST, CommonResponse обёртка, DTO-структуры, ролевая проверка"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание WebAPI Endpoint

## ШАГ 0: Посмотри рабочий пример

### Приоритет 1 — Targets/KPI (типизированный ответ ICommonResponse)

| Файл | Путь |
|------|------|
| **WebAPI (8 endpoints, эталон)** | `targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleServerFunctions.cs` |
| **CommonResponse структура** | `targets/source/DirRX.Targets/DirRX.Targets.Shared/Module.mtd` секция PublicStructures |
| **Документация** | `targets/CODE_PATTERNS_CATALOG.md` секция 2 |

### Приоритет 2 — CRM (JSON строки, 30+ endpoints)

**Эталон: DirRX.CRM.Server/ModuleServerFunctions.cs** — 30+ WebAPI endpoints (GET/POST), DTO через Structures, JSON сериализация.

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **WebAPI endpoints** | `DirRX.CRM/DirRX.CRM.Server/ModuleServerFunctions.cs` |
| **PublicStructures (22 DTO)** | `DirRX.CRM/DirRX.CRM.Shared/Module.mtd` — PipelineDto, StageDto, DealDto, ... |
| **ModuleConstants** | `DirRX.CRM/DirRX.CRM.Shared/ModuleConstants.cs` — Roles GUIDs |

**IntegrationServiceName** в MTD (определяет URL WebAPI): `"CRMDocumentsProposalApprovalTask"` → `/Integration/odata/CRMDocumentsProposalApprovalTask`.
Используй `MCP: predict_odata_name` для предсказания.

Перед созданием нового WebAPI — **обязательно прочитай** `ModuleServerFunctions.cs` и адаптируй.

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `generate_crud_api` — генерация C# CRUD endpoints (OData/SQL) из MTD сущности
- `generate_structures_cs` — генерация ModuleStructures.g.cs из PublicStructures в MTD
- `check_code_consistency` — проверка согласованности MTD и C#
- `check_package` — валидация пакета после создания
- `predict_odata_name` — предсказание OData EntitySet и имени таблицы БД
- `analyze_solution action=api` — карта всех WebAPI эндпоинтов решения
- `extract_entity_schema` — просмотр схемы сущности для DTO

## Входные данные

Спроси у пользователя (если не указано):
- **CompanyCode** — код компании (например, `rosa`)
- **ModuleName** — имя модуля (например, `ESM`)
- **FunctionName** — имя функции (например, `GetAvailableServices`)
- **HttpMethod** — GET или POST
- **Parameters** — входные параметры (имя, тип)
- **ReturnType** — тип возврата (примитив, структура, список структур)
- **RequiresRoleCheck** — нужна ли проверка роли (опционально)
- **Description** — описание эндпоинта

## Что создаётся

```
source/{Company}.{Module}/
  {Company}.{Module}.Server/
    ModuleServerFunctions.cs        # WebAPI функция [Public(WebApiRequestType = ...)]
  {Company}.{Module}.Shared/
    ModuleStructures.cs             # [Public] partial class DTO (если нужны)
    Module.mtd                      # PublicStructures секция (CommonResponse + бизнес-DTO)
```

## Алгоритм

### 0. MCP генерация CRUD (СНАЧАЛА попробуй MCP)

Если нужен стандартный CRUD для существующей сущности:
```
MCP: generate_crud_api entityMtdPath={путь_к_Entity.mtd} style=odata
MCP: predict_odata_name entityName={EntityName} moduleName={CompanyCode}.{ModuleName}
```
Если нужен кастомный эндпоинт — создавай вручную по шаблону ниже.

После создания:
```
MCP: generate_structures_cs moduleMtdPath={путь_к_Module.mtd} save=true
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: check_package packagePath={путь_к_пакету}
MCP: analyze_solution action=api
```

### 1. Создай CommonResponse структуру (ОБЯЗАТЕЛЬНО)

> **Production-паттерн из Targets.** Все WebAPI endpoints ДОЛЖНЫ возвращать обёрнутый ответ с `IsSuccess` + `Message`, а не голые типы или JSON-строки.

**Module.mtd (PublicStructures) — добавь CommonResponse:**
```json
"PublicStructures": [
  {
    "Name": "CommonResponse",
    "IsPublic": true,
    "Properties": [
      { "Name": "IsSuccess", "TypeFullName": "global::System.Boolean" },
      { "Name": "Message", "IsNullable": true, "TypeFullName": "global::System.String" }
    ],
    "StructureNamespace": "{Company}.{Module}.Structures.Module"
  }
]
```

**ModuleStructures.cs:**
```csharp
namespace {Company}.{Module}.Structures.Module
{
  [Public]
  partial class CommonResponse
  {
    public bool IsSuccess { get; set; }
    public string Message { get; set; }
  }
}
```

Если endpoint возвращает данные (не только статус), создай расширенную структуру с полем `Data`:
```json
{
  "Name": "DataResponse",
  "IsPublic": true,
  "Properties": [
    { "Name": "IsSuccess", "TypeFullName": "global::System.Boolean" },
    { "Name": "Message", "IsNullable": true, "TypeFullName": "global::System.String" },
    { "Name": "Data", "IsNullable": true, "TypeFullName": "global::System.String" }
  ],
  "StructureNamespace": "{Company}.{Module}.Structures.Module"
}
```

### 2. Создай бизнес-DTO (если нужны)

Если endpoint работает со сложными объектами — создай Public Structure для входных/выходных данных:

**Module.mtd (PublicStructures) — бизнес-DTO:**
```json
{
  "Name": "{DtoName}",
  "IsPublic": true,
  "Properties": [
    { "Name": "Id", "TypeFullName": "global::System.Int64" },
    { "Name": "Name", "IsNullable": true, "TypeFullName": "global::System.String" }
  ],
  "StructureNamespace": "{Company}.{Module}.Structures.Module"
}
```

**ModuleStructures.cs:**
```csharp
namespace {Company}.{Module}.Structures.Module
{
  [Public]
  partial class {DtoName}
  {
    public long Id { get; set; }
    public string Name { get; set; }
    // ... свойства
  }
}
```

**Кросс-модульные DTO:** `DirRX.Targets.Structures.Module.ICommonResponse` — можно использовать DTO из другого модуля.

### 3. Создай WebAPI функцию (production-паттерн)

> **КЛЮЧЕВОЕ ПРАВИЛО:** Каждый endpoint оборачивает результат в CommonResponse. Null-check + return с ошибкой (не exception). Логирование через `Logger.WithLogger()`.

**POST-эндпоинт (production-паттерн из Targets):**
```csharp
/// <summary>
/// Описание что делает endpoint.
/// </summary>
[Public(WebApiRequestType = RequestType.Post)]
public virtual Structures.Module.ICommonResponse {FunctionName}({params})
{
  var response = Structures.Module.CommonResponse.Create();
  response.IsSuccess = true;

  // 1. Null-check + ранний возврат с ошибкой (НЕ бросай exception)
  var entity = {Company}.{Module}.{Entities}.GetAll(e => e.Id == entityId).FirstOrDefault();
  if (entity == null)
  {
    response.IsSuccess = false;
    response.Message = {Company}.{Module}.Resources.EntityNotFound;
    Logger.WithLogger("{Module}").Debug("{FunctionName}: entity not found, id={0}", entityId);
    return response;
  }

  // 2. Ролевая проверка (если нужна)
  if (!Users.Current.IncludedIn(PublicConstants.Module.RolesGroup.Administrators))
  {
    response.IsSuccess = false;
    response.Message = {Company}.{Module}.Resources.AccessDenied;
    Logger.WithLogger("{Module}").Debug("{FunctionName}: access denied for user {0}", Users.Current.Name);
    return response;
  }

  // 3. Бизнес-логика в try/catch
  try
  {
    // ... бизнес-логика ...
    entity.Save();
    response.Message = "OK";
  }
  catch (Exception ex)
  {
    response.IsSuccess = false;
    response.Message = ex.Message;
    Logger.WithLogger("{Module}").Error("{FunctionName}: {0}", ex.Message);
  }

  return response;
}
```

**GET-эндпоинт (возвращает данные через DataResponse):**
```csharp
/// <summary>
/// Получить данные сущности.
/// </summary>
[Public(WebApiRequestType = RequestType.Get)]
public virtual Structures.Module.IDataResponse {FunctionName}(long entityId)
{
  var response = Structures.Module.DataResponse.Create();
  response.IsSuccess = true;

  var entity = {Company}.{Module}.{Entities}.GetAll(e => e.Id == entityId).FirstOrDefault();
  if (entity == null)
  {
    response.IsSuccess = false;
    response.Message = {Company}.{Module}.Resources.EntityNotFound;
    Logger.WithLogger("{Module}").Debug("{FunctionName}: not found {0}", entityId);
    return response;
  }

  try
  {
    var dto = Structures.Module.{DtoName}.Create();
    dto.Id = entity.Id;
    dto.Name = entity.Name;
    // ... заполнение полей

    // Сериализуем данные в Data
    response.Data = JsonConvert.SerializeObject(dto);
  }
  catch (Exception ex)
  {
    response.IsSuccess = false;
    response.Message = ex.Message;
    Logger.WithLogger("{Module}").Error("{FunctionName}: {0}", ex.Message);
  }

  return response;
}
```

**GET-эндпоинт (возвращает типизированную структуру напрямую):**
```csharp
/// <summary>
/// Получить данные графика.
/// </summary>
[Public(WebApiRequestType = RequestType.Get)]
public virtual Structures.Module.I{DtoName} {FunctionName}(long entityId, string period)
{
  // Для GET с типизированным DTO — тоже можно,
  // но предпочтительнее CommonResponse/DataResponse обёртка
  var dto = Structures.Module.{DtoName}.Create();
  // ... заполнение
  return dto;
}
```

### 4. Логирование (ОБЯЗАТЕЛЬНО)

> **Паттерн Logger.WithLogger() из Targets.** НЕ используй `Logger.Error(...)` без WithLogger.

```csharp
// Правильно — WithLogger с именем модуля:
Logger.WithLogger("{Module}").Debug("{FunctionName}: started, id={0}", entityId);
Logger.WithLogger("{Module}").Error("{FunctionName}: failed — {0}", ex.Message);

// Неправильно — без WithLogger:
// Logger.Error("...");  // НЕ ДЕЛАЙ ТАК
```

### 5. Ролевая проверка (если нужна)

```csharp
// Через GUID роли из ModuleConstants:
var roleGuid = PublicConstants.Module.RolesGroup.{RoleName};
if (!Users.Current.IncludedIn(roleGuid))
{
  response.IsSuccess = false;
  response.Message = Resources.AccessDenied;
  return response;
}
```

### 6. Типы возврата

| Тип | Return type | Когда использовать |
|-----|-------------|-------------------|
| **CommonResponse** | `Structures.Module.ICommonResponse` | POST без данных (создание, обновление, удаление) |
| **DataResponse** | `Structures.Module.IDataResponse` | GET/POST с данными (Data = JSON-строка) |
| **Типизированный DTO** | `Structures.Module.I{DtoName}` | GET с конкретной структурой |
| **Список DTO** | `List<Structures.Module.I{DtoName}>` | GET — список объектов |

> **ПРЕДПОЧТИТЕЛЬНО:** CommonResponse/DataResponse обёртка. Голые `string` return-ы с JSON — устаревший паттерн CRM, НЕ копировать.

## Антипаттерны (НЕ ДЕЛАЙ ТАК)

```csharp
// ПЛОХО: голая строка без обёртки
[Public(WebApiRequestType = RequestType.Post)]
public string DoSomething(long id)
{
  return "{\"error\":\"not found\"}";  // НЕТ! Используй CommonResponse
}

// ПЛОХО: exception вместо раннего return
[Public(WebApiRequestType = RequestType.Post)]
public virtual Structures.Module.ICommonResponse DoSomething(long id)
{
  var entity = Entities.Get(id);
  if (entity == null)
    throw new Exception("Not found");  // НЕТ! Верни response.IsSuccess = false
}

// ПЛОХО: Logger без WithLogger
Logger.Error("something failed");  // НЕТ! Logger.WithLogger("Module").Error(...)
```

## Валидация

- [ ] `[Public(WebApiRequestType = ...)]` на функции
- [ ] Возвращает `ICommonResponse` / `IDataResponse` (не голую строку)
- [ ] CommonResponse PublicStructure есть в Module.mtd
- [ ] DTO: `[Public] partial class` + MTD PublicStructures
- [ ] Return тип — `I{DtoName}` (интерфейс, не класс)
- [ ] Null-check + `response.IsSuccess = false` + return (не exception)
- [ ] `Logger.WithLogger("{Module}")` для всех логов
- [ ] `GetAll()` с `.Where()` (никогда без фильтра)
- [ ] Ролевая проверка через `Users.Current.IncludedIn(guid)`, НЕ `is`/`as`
- [ ] `using Newtonsoft.Json;` если используется сериализация

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
