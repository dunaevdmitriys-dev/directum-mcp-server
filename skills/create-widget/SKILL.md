---
description: "Создать виджет (Widget) для обложки модуля Directum RX — MTD, обработчик, ресурсы"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

> Приоритет reference: **KPI production-виджет** > платформенные модули > knowledge-base > MCP scaffold
> **ВНИМАНИЕ**: Виджеты в CRM могут быть сломаны — НЕ копируй слепо.

## Reference: Production виджет (KPI WidgetAchievingMetricValues)

| Файл | Путь |
|------|------|
| **WidgetHandlers (25KB, эталон)** | `targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleWidgetHandlers.cs` |
| **Widget MTD** | `targets/source/DirRX.KPI/DirRX.KPI.Shared/Module.mtd` секция Widgets |
| **Документация** | `targets/CODE_PATTERNS_CATALOG.md` секция 5 |

### Когда виджет, когда Remote Component?

| Критерий | Widget | Remote Component |
|----------|--------|-----------------|
| Простой график/счётчик | ✅ | Overkill |
| Интерактивный UI (drag, zoom, edit) | ❌ | ✅ (GoalsMap, TableControl) |
| Данные из одного модуля | ✅ | ✅ |
| Сложная визуализация (дерево, канбан) | ❌ | ✅ |
| Cover scope | ✅ | ✅ (GoalsMap — dual Card+Cover) |

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

### Production handler (алгоритм)
```csharp
// ModuleWidgetHandlers.cs — GetValue для Plot-виджета:
// 1. Получить метрику по MetricId из параметров
// 2. Определить период (текущий + опционально предыдущий)
// 3. Загрузить TargetValues (план) и ActualValues (факт) из БД
// 4. Построить Chart Series: e.EventArgs.AddPlotSeries("Plan", planData)
// 5. Линейная интерполяция для прогноза
// 6. Форматирование дат для оси X
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

# Платформенные виджеты:
# base/Sungero.DirectumRX/.../Module.mtd — секция Widgets
# Посмотри WidgetItems, HandledEvents, Color — используй как образец.

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

> **ВАЖНО**: `"WidgetType": "NumberWidget"` и `"HandledEvents": ["GetData"]` — это выдуманный API, НЕ существует в реальной платформе.
> Реальные виджеты используют `WidgetItems` с `$type: WidgetActionMetadata` (action/counter) или `WidgetChartMetadata` (chart/graph).

**Типы ChartType:**
| Тип | Отображение |
|-----|-------------|
| `HorizontalBar` | Горизонтальная столбчатая |
| `VerticalBar` | Вертикальная столбчатая |
| `Pie` | Круговая |
| `Line` | Линейная |

### 3. Server Handler — ModuleWidgetHandlers.cs

> **ВАЖНО**: Handler class = `{WidgetName}WidgetHandlers` (НЕ `{WidgetName}Handlers`).
> Namespace = `{Company}.{Module}.Server`.

#### Action-виджет (Filtering handler):
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
    /// Фильтрация для action-виджета.
    /// Метод: {WidgetName}{ActionName}Filtering
    /// </summary>
    public virtual IQueryable<{EntityInterface}> {WidgetName}{ActionName}Filtering(
        IQueryable<{EntityInterface}> query)
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

#### Chart-виджет (GetValue handler):
```csharp
namespace {Company}.{Module}.Server
{
  partial class {WidgetName}WidgetHandlers
  {
    /// <summary>
    /// Получение данных для chart-виджета.
    /// Метод: Get{WidgetName}{ChartName}Value
    /// </summary>
    public virtual void Get{WidgetName}{ChartName}Value(Sungero.Domain.GetWidgetBarChartValueEventArgs e)
    {
      // Заполнение данных графика
      var data = GetChartData();
      foreach (var item in data)
      {
        e.AddValue(item.Label, item.Value, item.Color);
      }
    }
  }
}
```

> **ВАЖНО**: `GetData(WidgetGetDataEventArgs e)` — НЕ ВЕРИФИЦИРОВАН.
> Реальные handlers:
> - `Filtering()` — для action-виджетов (возвращает IQueryable)
> - `GetValue()` / `Get{WidgetName}{ChartName}Value()` — для chart-виджетов (заполняет EventArgs)

### 4. Ресурсы

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
- [ ] Namespace = `{Company}.{Module}.Server`
- [ ] Widget в секции `Widgets` Module.mtd (НЕ в Reports/AsyncHandlers)
- [ ] WidgetItems содержит `$type` = `WidgetActionMetadata` или `WidgetChartMetadata`
- [ ] HandledEvents: `FilteringServer` для action, `GetValueServer` для chart
- [ ] Ресурсные ключи совпадают с кодом
- [ ] `GetAll()` с `.Where()` — производительность

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
