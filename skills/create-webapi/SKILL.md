---
description: "Создать WebAPI endpoint Directum RX — GET/POST, DTO-структуры, ролевая проверка"
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

**Паттерн ICommonResponse (ЛУЧШЕ чем JSON строки):**
```csharp
[Public(WebApiRequestType = RequestType.Post)]
public Structures.Module.ICommonResponse SaveMetricActualValues(string requestData)
{
  var response = Structures.Module.CommonResponse.Create();
  response.IsSuccess = true;
  try
  {
    // бизнес-логика
  }
  catch (Exception ex)
  {
    response.IsSuccess = false;
    response.Message = ex.Message;
    Logger.Error("SaveMetricActualValues failed", ex);
  }
  return response;
}

// GET — возвращает типизированную структуру (не строку!)
[Public(WebApiRequestType = RequestType.Get)]
public Structures.Module.IChartData GetChartData(long metricId, string period)
```

**Кросс-модульные DTO:** `DirRX.Targets.Structures.Module.ICommonResponse` — можно использовать DTO из другого модуля.

### Приоритет 2 — CRM (JSON строки, 30+ endpoints)

**Эталон: DirRX.CRM.Server/ModuleServerFunctions.cs** — 30+ WebAPI endpoints (GET/POST), DTO через Structures, JSON сериализация.

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **WebAPI endpoints** | `DirRX.CRM/DirRX.CRM.Server/ModuleServerFunctions.cs` |
| **PublicStructures (22 DTO)** | `DirRX.CRM/DirRX.CRM.Shared/Module.mtd` — PipelineDto, StageDto, DealDto, ... |
| **ModuleConstants** | `DirRX.CRM/DirRX.CRM.Shared/ModuleConstants.cs` — Roles GUIDs |

**Реальные примеры из ModuleServerFunctions.cs:**
```csharp
// GET — LoadPipeline: возвращает JSON строку с полным DTO воронки
[Public(WebApiRequestType = RequestType.Get)]
public string LoadPipeline(long pipelineId, long responsibleId, string periodStart, string periodEnd)

// POST — MoveDealToStage: изменяет данные, возвращает JSON
[Public(WebApiRequestType = RequestType.Post)]
public string MoveDealToStage(long dealId, long newStageId, int position)

// GET — GetCustomer360: Card-данные для RC
[Public(WebApiRequestType = RequestType.Get)]
public string GetCustomer360(long counterpartyId)

// POST — GetDashboardKPIs: сложная аналитика
[Public(WebApiRequestType = RequestType.Post)]
public string GetDashboardKPIs(long pipelineId, string periodStart, string periodEnd, long departmentId)
```

**Паттерн доступа (из CRM):**
```csharp
if (!HasCRMAccess())
  return "{\"error\":\"Access denied\"}";
```

**Паттерн DTO (из CRM):**
```csharp
var dto = Structures.Module.StageDto.Create();
dto.Id = stage.Id;
dto.Name = stage.Name;
// ... заполнение полей
```

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
    Module.mtd                      # PublicStructures секция (если нужны DTO)
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

### 1. Определи тип DTO

Если возвращается сложный объект — создай Public Structure:

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

**Module.mtd (PublicStructures):**
```json
"PublicStructures": [
  {
    "Name": "{DtoName}",
    "IsPublic": true,
    "Properties": [
      { "Name": "Id", "TypeFullName": "global::System.Int64" },
      { "Name": "Name", "IsNullable": true, "TypeFullName": "global::System.String" }
    ],
    "StructureNamespace": "{Company}.{Module}.Structures.Module"
  }
]
```

### 2. Создай WebAPI функцию

**GET-эндпоинт** (реальный паттерн из `GetCustomer360`):
```csharp
/// <summary>
/// Get Customer 360 data.
/// </summary>
[Public(WebApiRequestType = RequestType.Get)]
public string GetCustomer360(long counterpartyId)
{
  if (!HasCRMAccess())
    return "{\"error\":\"Access denied\"}";

  var counterparty = Sungero.Parties.Counterparties.Get(counterpartyId);
  if (counterparty == null)
    return string.Empty;

  var deals = DirRX.CRMSales.Deals.GetAll()
    .Where(d => d.Counterparty != null && d.Counterparty.Id == counterpartyId)
    .ToList();

  var dto = Structures.Module.Customer360Dto.Create();
  dto.CounterpartyId = counterpartyId;
  dto.CounterpartyName = counterparty.Name;
  dto.TotalDeals = deals.Count;
  // ... заполнение полей

  return Customer360DtoToJson(dto);
}
```

**POST-эндпоинт** (реальный паттерн из `MoveDealToStage`):
```csharp
/// <summary>
/// Move deal to a new stage (drag-and-drop).
/// </summary>
[Public(WebApiRequestType = RequestType.Post)]
public string MoveDealToStage(long dealId, long newStageId, int position)
{
  if (!HasCRMAccess())
    return "{\"error\":\"Access denied\"}";

  var deal = DirRX.CRMSales.Deals.Get(dealId);
  if (deal == null)
    return string.Empty;

  var newStage = DirRX.CRMSales.Stages.Get(newStageId);
  if (newStage == null)
    return string.Empty;

  deal.Stage = newStage;
  deal.LastStageChangeDate = Calendar.Now;
  deal.Save();

  // Fire async handler
  var asyncHandler = DirRX.CRM.AsyncHandlers.DealStageChanged.Create();
  asyncHandler.DealId = deal.Id;
  asyncHandler.ExecuteAsync();

  return DealDtoToJson(BuildDealDto(deal));
}
```

### 3. Ролевая проверка (если нужна)

```csharp
[Public(WebApiRequestType = Sungero.Core.RequestType.Post)]
public List<Structures.Module.I{DtoName}> {FunctionName}({params})
{
  var adminGuid = PublicConstants.Module.RolesGroup.Administrators;
  if (!Users.Current.IncludedIn(adminGuid))
    return new List<Structures.Module.I{DtoName}>();

  // ... основная логика
}
```

### 4. Типы возврата

| Тип | Пример | Паттерн |
|-----|--------|---------|
| Примитив | `long?`, `string` | Прямой return |
| Список примитивов | `List<string>` | `.Select().ToList()` |
| DTO | `Structures.Module.I{Dto}` | `{Dto}.Create(...)` |
| Список DTO | `List<Structures.Module.I{Dto}>` | `.Select(e => {Dto}.Create(...)).ToList()` |
| JSON строка | `string` | `JsonConvert.SerializeObject(result)` |

## Валидация

- [ ] `[Public(WebApiRequestType = ...)]` на функции
- [ ] DTO: `[Public] partial class` + MTD PublicStructures
- [ ] Return тип — `I{DtoName}` (интерфейс, не класс)
- [ ] `GetAll()` с `.Where()` (никогда без фильтра)
- [ ] Ролевая проверка через `Users.Current.IncludedIn(guid)`, НЕ `is`/`as`
- [ ] `using Newtonsoft.Json;` если используется сериализация

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`