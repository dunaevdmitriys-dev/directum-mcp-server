---
description: "Создать виджет (Widget) для обложки модуля Directum RX — MTD, обработчик, ресурсы"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

> Приоритет reference: **Targets production-виджеты** > платформенные модули > knowledge-base > MCP scaffold
> **ВНИМАНИЕ**: Виджеты в CRM могут быть сломаны — НЕ копируй слепо.

## Reference: Production виджеты (Targets + KPI)

| Файл | Путь |
|------|------|
| **Targets WidgetHandlers (Gauge + Pie)** | `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleWidgetHandlers.cs` |
| **Targets ClientBase WidgetHandlers (Execute)** | `targets/source/DirRX.Targets/DirRX.Targets.ClientBase/ModuleWidgetHandlers.cs` |
| **KPI WidgetHandlers (Plot/Line, 25KB)** | `targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleWidgetHandlers.cs` |
| **Widget MTD** | `targets/source/DirRX.Targets/DirRX.Targets.Shared/Module.mtd` секция Widgets |
| **Документация** | `targets/CODE_PATTERNS_CATALOG.md` секция 5 |

### Когда виджет, когда Remote Component?

| Критерий | Widget | Remote Component |
|----------|--------|-----------------|
| Простой график/счётчик | ✅ | Overkill |
| Интерактивный UI (drag, zoom, edit) | ❌ | ✅ (GoalsMap, TableControl) |
| Данные из одного модуля | ✅ | ✅ |
| Сложная визуализация (дерево, канбан) | ❌ | ✅ |
| Cover scope | ✅ | ✅ (GoalsMap — dual Card+Cover) |

### Реальные сигнатуры обработчиков (из Targets production)

**Сигнатуры GetValue — зависят от ChartType:**

| ChartType | EventArgs | Пример из Targets |
|-----------|-----------|-------------------|
| `HorizontalBar` / `VerticalBar` | `Sungero.Domain.GetWidgetBarChartValueEventArgs` | — |
| `Pie` | `Sungero.Domain.GetWidgetPieChartValueEventArgs` | `GetTargetStatusesChartValue` |
| `Line` / `Plot` | `Sungero.Domain.GetWidgetPlotChartValueEventArgs` | `GetWidgetAchievingMetricValuesChartValue` |
| `Gauge` | `Sungero.Domain.GetWidgetGaugeChartValueEventArgs` | `GetWidgetAchievementTargetChartTargetsMapValue` |

**Сигнатура Filtering (для chart-виджетов с кликом по сегменту):**

| Тип | Сигнатура | Пример из Targets |
|-----|-----------|-------------------|
| Pie Filtering | `IQueryable<{Entity}> {Widget}{Chart}Filtering(IQueryable<{Entity}> query, Sungero.Domain.WidgetPieChartFilteringEventArgs e)` | `TargetStatusesChartFiltering` |
| Action Filtering | `IQueryable<{Entity}> {Widget}{Action}Filtering(IQueryable<{Entity}> query, Sungero.Domain.UiFilteringEventArgs e)` | — |

**Сигнатура Execute (клиентский обработчик клика):**
```
void Execute{Widget}{Chart}Action()
```

### Production MTD (WidgetParameters)
```json
{
  "$type": "Sungero.Metadata.WidgetChartMetadata",
  "NameGuid": "...",
  "Name": "WidgetAchievingMetricValues",
  "WidgetParameters": [
    { "NameGuid": "...", "Name": "MetricId", "ParameterType": "LongInteger" },
    { "NameGuid": "...", "Name": "Period", "ParameterType": "String" },
    { "NameGuid": "...", "Name": "ShowPrevPeriod", "ParameterType": "Boolean" }
  ],
  "ChartType": "Plot"
}
```

### Production handler (алгоритм из KPI)
```csharp
// ModuleWidgetHandlers.cs — GetValue для Plot-виджета:
// 1. Получить метрику по MetricId из _parameters
// 2. Определить период (текущий + опционально предыдущий)
// 3. Загрузить TargetValues (план) и ActualValues (факт) из БД
// 4. e.Chart.Axis.X.AxisType = AxisType.DateTime
// 5. e.Chart.AddNewSeries("Факт", Colors.Charts.Green) → серия.AddValue(date, value)
// 6. Линейная интерполяция для промежуточных значений
```

# Создание виджета (Widget)

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей resx из MTD
- `analyze_solution action=cover` — проверка обложек модулей (виджеты на обложке)
- `search_metadata query=Widgets scope=modules` — поиск эталонных виджетов в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Найди виджет в платформе:
MCP: search_metadata type=WidgetMetadata

# Эталонные виджеты:
# targets/source/DirRX.Targets/.../ModuleWidgetHandlers.cs — Gauge + PieChart
# targets/source/DirRX.KPI/.../ModuleWidgetHandlers.cs — Plot/Line chart

# ВНИМАНИЕ: В CRM виджетов пока нет (Widgets: []).
# Используй ТОЛЬКО платформенные эталоны.
```

## Входные данные

Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля
- **WidgetName** — имя виджета (например, `ActiveLeadsCounter`)
- **DisplayName** — отображаемое имя
- **WidgetType** — тип: action-виджет (счётчик/список) или chart-виджет (график)
- **DataSource** — откуда данные (сущность для action-виджета, серверная функция для chart)
- **Parameters** — параметры фильтрации (период, подразделение и т.д.)

## Что создаётся

```
source/{Company}.{Module}/
  {Company}.{Module}.Server/
    ModuleWidgetHandlers.cs         # Filtering/GetValue обработчики
  {Company}.{Module}.ClientBase/
    ModuleWidgetHandlers.cs         # Execute обработчик (клик по chart)
  {Company}.{Module}.Shared/
    Module.mtd                      # Widget в секции Widgets
    Module.resx / Module.ru.resx    # Ресурсы виджета
```

## Алгоритм

### 0. MCP валидация (ПОСЛЕ создания)

После добавления виджета в Module.mtd и ModuleWidgetHandlers.cs:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
MCP: analyze_solution action=cover
```

### 1. Сгенерируй GUIDs
- `WidgetGuid` — для виджета
- `WidgetItemGuid` — для каждого WidgetItem

### 2. Добавь в Module.mtd

Реальные виджеты используют `WidgetItems` с `$type`: `WidgetActionMetadata` или `WidgetChartMetadata`.

#### Action-виджет (счётчик/список сущностей):
```json
"Widgets": [
  {
    "NameGuid": "<WidgetGuid>",
    "Name": "<WidgetName>",
    "Color": "WidgetColor2",
    "Versions": [],
    "WidgetItems": [
      {
        "$type": "Sungero.Metadata.WidgetActionMetadata, Sungero.Metadata",
        "NameGuid": "<WidgetItemGuid>",
        "Name": "<ActionName>",
        "EntityGuid": "<EntityGuid>",
        "HandledEvents": [
          "FilteringServer"
        ],
        "IsMain": true,
        "Versions": []
      }
    ]
  }
]
```

#### Chart-виджет (график/диаграмма):
```json
{
  "NameGuid": "<WidgetGuid>",
  "Name": "<WidgetName>",
  "Color": "WidgetColor4",
  "ColumnSpan": 20,
  "Versions": [],
  "WidgetItems": [
    {
      "$type": "Sungero.Metadata.WidgetChartMetadata, Sungero.Metadata",
      "NameGuid": "<WidgetItemGuid>",
      "Name": "<ChartName>",
      "ChartHeight": 11,
      "ChartType": "HorizontalBar",
      "HandledEvents": [
        "GetValueServer",
        "ExecuteClient"
      ],
      "SpecificationType": "Custom",
      "Versions": []
    }
  ]
}
```

**Типы ChartType и соответствующие EventArgs:**
| Тип | Отображение | EventArgs для GetValue |
|-----|-------------|----------------------|
| `HorizontalBar` | Горизонтальная столбчатая | `GetWidgetBarChartValueEventArgs` |
| `VerticalBar` | Вертикальная столбчатая | `GetWidgetBarChartValueEventArgs` |
| `Pie` | Круговая | `GetWidgetPieChartValueEventArgs` |
| `Line` / `Plot` | Линейная | `GetWidgetPlotChartValueEventArgs` |
| `Gauge` | Индикатор (шкала) | `GetWidgetGaugeChartValueEventArgs` |

### 3. Server Handler — ModuleWidgetHandlers.cs

> **ВАЖНО**: Handler class = `{WidgetName}WidgetHandlers` (НЕ `{WidgetName}Handlers`).
> Namespace = `{Company}.{Module}.Server`.
> Параметры виджета доступны через `_parameters`.

#### Пример 1: PieChart — из Targets (GetTargetStatusesChartValue)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Фильтрация при клике на сегмент круговой диаграммы.
    /// Вызывается когда пользователь кликает на сегмент — открывается список сущностей.
    /// </summary>
    public virtual IQueryable<{IEntity}> {WidgetName}{ChartName}Filtering(
        IQueryable<{IEntity}> query,
        Sungero.Domain.WidgetPieChartFilteringEventArgs e)
    {
      // e.ValueId — идентификатор сегмента, по которому кликнули
      var statusId = long.Parse(e.ValueId);
      query = query.Where(x => x.SomeProperty.Id == statusId);
      return query;
    }

    /// <summary>
    /// Получение данных для круговой диаграммы.
    /// Сигнатура: Get{WidgetName}{ChartName}Value(GetWidgetPieChartValueEventArgs e)
    /// </summary>
    public virtual void Get{WidgetName}{ChartName}Value(
        Sungero.Domain.GetWidgetPieChartValueEventArgs e)
    {
      var items = {Entities}.GetAll();

      // Группируем и добавляем сегменты
      // e.Chart.AddValue(id, displayName, count, color)
      var statuses = Functions.Module.GetGroupedStatuses(items);
      foreach (var status in statuses)
      {
        var color = Functions.Module.GetColorByStatus(status);
        if (status.Count > 0)
          e.Chart.AddValue(status.Id.ToString(), status.Name, status.Count, color);
      }
    }
  }
}
```

#### Пример 2: BarChart (столбчатая диаграмма)

```csharp
namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Получение данных для столбчатой диаграммы (HorizontalBar/VerticalBar).
    /// Сигнатура: Get{WidgetName}{ChartName}Value(GetWidgetBarChartValueEventArgs e)
    /// </summary>
    public virtual List<Sungero.Core.WidgetBarChartValue> Get{WidgetName}{ChartName}Value(
        Sungero.Domain.GetWidgetBarChartValueEventArgs e)
    {
      var items = {Entities}.GetAll()
        .Where(x => x.Status == {Entity}.Status.Active);

      // Группируем по категории
      var groups = items
        .GroupBy(x => x.Category)
        .Select(g => new { Label = g.Key?.Name ?? "Без категории", Count = g.Count() })
        .OrderByDescending(g => g.Count)
        .ToList();

      foreach (var group in groups)
      {
        e.AddValue(group.Label, group.Count, Colors.Charts.Color1);
      }

      return null;
    }
  }
}
```

#### Пример 3: Plot/Line chart — из KPI (GetWidgetAchievingMetricValuesChartValue)

```csharp
namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Получение данных для линейного графика.
    /// Сигнатура: Get{WidgetName}{ChartName}Value(GetWidgetPlotChartValueEventArgs e)
    /// Параметры доступны через _parameters.
    /// </summary>
    public virtual void Get{WidgetName}{ChartName}Value(
        Sungero.Domain.GetWidgetPlotChartValueEventArgs e)
    {
      // Настройка осей
      e.Chart.Axis.X.AxisType = AxisType.DateTime;
      e.Chart.Axis.X.Title = "Период";

      // Серия "Факт" — e.Chart.AddNewSeries(name, color)
      var factSeries = e.Chart.AddNewSeries(Resources.FactDataType, Colors.Charts.Green);
      var factData = GetFactValues(); // Dictionary<DateTime, double?>
      foreach (var key in factData.Keys.OrderBy(k => k))
      {
        if (factData[key].HasValue)
          factSeries.AddValue(key, Math.Round(factData[key].Value, 2, MidpointRounding.AwayFromZero));
      }

      // Серия "План"
      var planSeries = e.Chart.AddNewSeries(Resources.PlanDataType, Colors.Charts.Color1);
      var planData = GetPlanValues();
      foreach (var key in planData.Keys.OrderBy(k => k))
      {
        if (planData[key].HasValue)
          planSeries.AddValue(key, Math.Round(planData[key].Value, 2, MidpointRounding.AwayFromZero));
      }
    }
  }
}
```

#### Пример 4: Gauge (индикатор) — из Targets (GetWidgetAchievementTargetChartTargetsMapValue)

```csharp
namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Получение значения для индикатора (Gauge).
    /// Сигнатура: Get{WidgetName}{ChartName}Value(GetWidgetGaugeChartValueEventArgs e)
    /// </summary>
    public virtual void Get{WidgetName}{ChartName}Value(
        Sungero.Domain.GetWidgetGaugeChartValueEventArgs e)
    {
      var period = _parameters.Period;
      if (period == null || Users.Current.IsSystem == true)
        return;

      var value = CalculateAchievement();

      if (value.HasValue)
      {
        value = Math.Round(value.Value, 0, MidpointRounding.AwayFromZero);
        e.Chart.AddValue("Достижение", value.Value, Colors.Charts.Color1);
      }
    }
  }
}
```

#### Пример 5: Action-виджет (Filtering handler)

```csharp
namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Фильтрация для action-виджета.
    /// Сигнатура: {WidgetName}{ActionName}Filtering(IQueryable, UiFilteringEventArgs)
    /// </summary>
    public virtual IQueryable<{IEntity}> {WidgetName}{ActionName}Filtering(
        IQueryable<{IEntity}> query,
        Sungero.Domain.UiFilteringEventArgs e)
    {
      var employee = Sungero.Company.Employees.As(Users.Current);
      if (employee == null)
        return query.Where(x => false);

      return query.Where(x =>
        x.Status == Sungero.CoreEntities.DatabookEntry.Status.Active &&
        Equals(x.Responsible, employee));
    }
  }
}
```

### 4. ClientBase Handler — ModuleWidgetHandlers.cs (для ExecuteClient)

Если в `HandledEvents` есть `ExecuteClient`, создай обработчик клика в ClientBase:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}.Client
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Обработчик клика по chart-виджету.
    /// Сигнатура: Execute{WidgetName}{ChartName}Action()
    /// </summary>
    public virtual void Execute{WidgetName}{ChartName}Action()
    {
      // Пример из Targets: открыть карту целей
      var entity = {Entities}.GetAll()
        .Where(x => x.Id == _parameters.EntityId)
        .FirstOrDefault();

      if (entity != null)
        entity.ShowModal();
    }
  }
}
```

### 5. Ресурсы

**Module.resx** (добавь ключи):
```xml
<data name="{WidgetName}Label" xml:space="preserve"><value>Active requests</value></data>
<data name="{WidgetName}Title" xml:space="preserve"><value>Requests by Status</value></data>
```

**Module.ru.resx**:
```xml
<data name="{WidgetName}Label" xml:space="preserve"><value>Активные обращения</value></data>
<data name="{WidgetName}Title" xml:space="preserve"><value>Обращения по статусам</value></data>
```

## Валидация

- [ ] Handler class = `{WidgetName}WidgetHandlers` (partial class)
- [ ] Namespace = `{Company}.{Module}.Server` для серверных, `{Company}.{Module}.Client` для клиентских
- [ ] Widget в секции `Widgets` Module.mtd (НЕ в Reports/AsyncHandlers)
- [ ] WidgetItems содержит `$type` = `WidgetActionMetadata` или `WidgetChartMetadata`
- [ ] HandledEvents: `FilteringServer` для action, `GetValueServer` + `ExecuteClient` для chart
- [ ] EventArgs соответствует ChartType (см. таблицу выше)
- [ ] Ресурсные ключи совпадают с кодом
- [ ] `GetAll()` с `.Where()` — производительность
- [ ] `_parameters` для доступа к WidgetParameters

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
