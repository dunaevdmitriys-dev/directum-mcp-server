---
description: "Составить OData-запрос к Directum RX Integration Service"
---

# Создание OData-запроса к Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `predict_odata_name` -- предсказать имя OData entity set по имени сущности
- `extract_entity_schema` -- получить схему свойств сущности (имена, типы, навигации)
- `search_metadata` -- найти IntegrationServiceName в MTD
- `check_code_consistency` -- проверка после изменений

## ШАГ 0: Реальные OData-примеры (найди аналог в своём проекте)

**Файлы-образцы (подглядывай!):**

| Что | Файл |
|-----|------|
| ODataService (GET/POST/PATCH/DELETE) | `{project_path}/Services/ODataService.cs` (если есть OData-обёртка в проекте) |
| ODataQueryBuilder (fluent) | `{project_path}/Services/ODataQueryBuilder.cs` (если есть fluent builder) |
| OData entity set константы | `{project_path}/Services/Constants.cs` (OData entity set константы) |
| GET с $filter/$expand | `{project_path}/Endpoints/` (примеры GET с $filter/$expand) |
| GET $select/$top | `{project_path}/Endpoints/` (примеры GET $select/$top, PATCH) |
| POST с @odata.bind | `{project_path}/Services/` (пример POST с @odata.bind) |
| PATCH с @odata.bind | `{project_path}/Endpoints/` (примеры GET $select/$top, PATCH) |

**Реальные запросы из CRM:**
```
# Сделки по воронке с expand:
GET ICRMSalesDeals?$filter=Pipeline/Id eq 5&$select=Id,Name,Amount&$expand=Stage,Counterparty&$top=50

# Сотрудники отдела:
GET IEmployees?$filter=Department/Id eq 12&$select=Id,Name&$top=100

# История сделки:
GET ICRMSalesDealHistories?$filter=Deal/Id eq 42&$orderby=ChangedAt desc&$top=50&$expand=Deal,FromStage,ToStage,ChangedBy

# Поиск по имени:
GET IEmployees?$filter=Login/LoginName eq 'admin'&$select=Id,Name&$top=1

# Настройки CRM:
GET ICRMCommonCRMSettingss?$top=1
```

---

## IntegrationServiceName в .mtd

Каждая сущность, доступная через OData, ДОЛЖНА иметь `IntegrationServiceName` в `.mtd`:

```json
"IntegrationServiceName": "CRMSalesDeal"
```

### Формула OData entity set name

```
I{IntegrationServiceName}s
```

Примеры:
| IntegrationServiceName | OData Entity Set |
|----------------------|-----------------|
| `CRMSalesDeal` | `ICRMSalesDeals` |
| `CRMSalesPipeline` | `ICRMSalesPipelines` |
| `CRMMarketingLead` | `ICRMMarketingLeads` |
| `CRMDocumentsCommercialProposal` | `ICRMDocumentsCommercialProposals` |
| `CRMDocumentsInvoice` | `ICRMDocumentsInvoices` |
| `CRMSalesActivity` | `ICRMSalesActivities` |
| `CRMSalesStage` | `ICRMSalesStages` |
| `CRMSalesLossReason` | `ICRMSalesLossReasons` |
| `CRMMarketingLeadSource` | `ICRMMarketingLeadSources` |
| `CRMMarketingCampaign` | `ICRMMarketingCampaigns` |

**Платформенные сущности (без CRM-префикса):**
| Сущность | OData Entity Set |
|----------|-----------------|
| Employee | `IEmployees` |
| Department | `IDepartments` |
| Counterparty | `ICounterparties` |
| Contact | `IContacts` |
| Contract | `IContracts` |
| SimpleDocument | `ISimpleDocuments` |

```
# Найти IntegrationServiceName:
Grep("IntegrationServiceName", "{package_path}/source/**/*.mtd")

# Или через MCP:
MCP: predict_odata_name entity=Deal
MCP: extract_entity_schema entity=Deal
```

---

## OData Query Syntax

### Базовый URL

```
http://localhost:8080/Integration/odata/{EntitySet}
```

### $filter -- фильтрация

| Оператор | Пример |
|----------|--------|
| Равенство | `$filter=Status eq 'Active'` |
| Не равно | `$filter=Status ne 'Closed'` |
| Больше | `$filter=Amount gt 10000` |
| Меньше | `$filter=Amount lt 50000` |
| Больше/равно | `$filter=Amount ge 10000` |
| Меньше/равно | `$filter=Amount le 50000` |
| Содержит | `$filter=contains(Name,'test')` |
| Начинается | `$filter=startswith(Name,'CRM')` |
| По навигации | `$filter=Pipeline/Id eq 5` |
| Логическое И | `$filter=Pipeline/Id eq 5 and Amount gt 0` |
| Логическое ИЛИ | `$filter=Status eq 'Active' or Status eq 'Draft'` |
| NULL-проверка | `$filter=LossReason eq null` |
| NOT NULL | `$filter=LossReason ne null` |

### $select -- выбор полей

```
$select=Id,Name,Amount,Status
```

### $expand -- раскрытие навигационных свойств

```
$expand=Pipeline,Stage,Counterparty,Responsible
```

### $orderby -- сортировка

```
$orderby=Name
$orderby=Amount desc
$orderby=Created desc,Name
```

### $top / $skip -- пагинация

```
$top=50
$skip=100&$top=50
```

### Комбинированный запрос

```
ICRMSalesDeals?$filter=Pipeline/Id eq 5 and Amount gt 0&$select=Id,Name,Amount&$expand=Stage&$orderby=Amount desc&$top=50
```

---

## CRUD через OData

### GET (чтение)

```
GET {BaseUrl}{EntitySet}?{query}           -- список
GET {BaseUrl}{EntitySet}({id})              -- по ID
GET {BaseUrl}{EntitySet}({id})/{NavProp}    -- навигационное свойство
```

### POST (создание)

```json
POST {BaseUrl}{EntitySet}
Content-Type: application/json

{
  "Name": "Новая сделка",
  "Amount": 100000,
  "Pipeline@odata.bind": "ICRMSalesPipelines(5)",
  "Stage@odata.bind": "ICRMSalesStages(12)",
  "Counterparty@odata.bind": "ICounterparties(42)"
}
```

**Навигационные свойства при POST/PATCH:**
```
{NavPropertyName}@odata.bind: "{TargetEntitySet}({Id})"
```

### PATCH (обновление)

```json
PATCH {BaseUrl}{EntitySet}({id})
Content-Type: application/json

{
  "Amount": 150000,
  "Stage@odata.bind": "ICRMSalesStages(15)"
}
```

### DELETE (удаление)

```
DELETE {BaseUrl}{EntitySet}({id})
```

---

## Параметризованные запросы (защита от инъекций)

**НИКОГДА не конкатенируй пользовательский ввод напрямую в OData-запрос!**

### Правильно: ODataQueryBuilder (CRM API)

```csharp
// Файл: {project_path}/Services/ODataQueryBuilder.cs (если есть)
var query = ODataQuery.For(CrmConstants.ODataSets.Deals)
    .Select("Id", "Name", "Amount")
    .FilterEq("Pipeline/Id", pipelineId)  // long -- безопасно
    .FilterContains("Name", searchTerm)    // экранирует кавычки
    .OrderBy("Amount", descending: true)
    .Expand("Stage")
    .Top(50)
    .Build();

var result = await query.ExecuteAsync<JsonElement>(odata);
```

### Правильно: экранирование строк

```csharp
// ODataQueryBuilder.EscapeOData заменяет ' на ''
private static string EscapeOData(string value) =>
    value.Replace("'", "''");

// Пример: O'Brien --> O''Brien
FilterEq("Name", "O'Brien")  // --> $filter=Name eq 'O''Brien'
```

### Неправильно: конкатенация

```csharp
// ОПАСНО! SQL/OData injection:
var query = $"$filter=Name eq '{userInput}'";  // НЕ ДЕЛАЙ ТАК!
```

---

## OData vs Direct SQL -- когда что

| Критерий | OData | Direct SQL (Npgsql) |
|----------|-------|-------------------|
| Простой CRUD одной сущности | OData | -- |
| Чтение с $expand (JOIN) | OData | -- |
| Сложные агрегации (SUM, AVG, COUNT) | -- | SQL |
| JOIN 3+ таблиц | -- | SQL |
| Полнотекстовый поиск | -- | SQL (LIKE/trgm) |
| Массовые операции (1000+ записей) | -- | SQL |
| Кастомные таблицы (crm_*) | -- | SQL |
| Сущности RX (справочники, документы) | OData | -- |
| Права доступа учитываются автоматически | OData | Нет (нужно вручную) |

**Золотое правило:** Для сущностей RX (те, что в DDS) -- всегда OData. Для кастомных PostgreSQL-таблиц -- всегда SQL.

---

## @odata.bind для навигационных свойств

При POST/PATCH навигационные свойства передаются через `@odata.bind`:

```csharp
// Реальный пример из CRM API (PipelineService.cs):
var patchPayload = new Dictionary<string, object?>
{
    ["Stage"] = new ODataBind(CrmConstants.ODataSets.Stages, newStageId),
    ["LastStageChangeDate"] = DateTime.UtcNow.ToString("o"),
};
var result = await odata.PatchWithBindAsync(
    CrmConstants.ODataSets.Deals, dealId, patchPayload);

// ODataBind автоматически конвертируется в:
// "Stage@odata.bind": "ICRMSalesStages(15)"
```

```csharp
// record из ODataService.cs:
public record ODataBind(string EntitySet, long Id);
```

---

## Polly Resilience (обязательно для CRM API)

OData-запросы к RX ВСЕГДА оборачиваются в Polly:
- Retry 3x exponential backoff (1s, 2s, 4s)
- Circuit breaker (50% failure rate / 15s break)
- Graceful degradation при недоступности RX

Настроено через `IHttpClientFactory` в `Program.cs` -- не нужно добавлять вручную.

---

## Чеклист OData-запроса

1. [ ] `IntegrationServiceName` есть в `.mtd` сущности
2. [ ] Entity set name соответствует формуле `I{IntegrationServiceName}s`
3. [ ] MCP: `predict_odata_name` -- подтвердить имя
4. [ ] Навигационные свойства через `@odata.bind` при POST/PATCH
5. [ ] Строковые значения в $filter экранированы (кавычки `'` --> `''`)
6. [ ] $top указан (иначе RX вернёт ВСЕ записи!)
7. [ ] Для CRM API: используй `ODataQueryBuilder` вместо конкатенации
8. [ ] Для CRM API: используй `CrmConstants.ODataSets.*` вместо хардкода

---

## Ссылки на гайды (НЕ копируй, читай при необходимости)

- **Guide 15** `knowledge-base/guides/15_sql_locking_database.md` -- SQL, блокировки, прямой доступ к БД
- **Guide 18** `knowledge-base/guides/18_integrations_isolated.md` -- IntegrationServiceName, изолированные области
- **Guide 25** `knowledge-base/guides/25_code_patterns.md` -- C# паттерны, Remote-функции
