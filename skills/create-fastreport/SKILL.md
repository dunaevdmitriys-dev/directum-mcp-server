---
description: "Создать FastReport-отчёт Directum RX: .mtd + .frx + обработчики + resx + PublicStructures"
---

# Create FastReport — Отчёт Directum RX

Полный цикл создания отчёта: метаданные (.mtd), макет (.frx), серверные обработчики, ресурсы.

---

## ШАГ 0: Реальные пути CRM-отчётов

```
WORKSPACE = /Users/dmitrijdunaev/Desktop/Разработка/Директум
MODULE    = $WORKSPACE/CRM/crm-package/source/DirRX.CRM

Существующие отчёты (reference):
  SalesFunnelReport    — воронка продаж (PDF/Excel, этапы + конверсия)
  DealsByManagerReport — сделки по менеджерам (Excel/PDF, группировка)
  LeadSourceReport     — источники лидов
  ActivityReport       — отчёт по активностям
  LossReasonReport     — причины проигрыша сделок
  PlanFactReport       — план-факт

Структура файлов отчёта:
  $MODULE/DirRX.CRM.Shared/Reports/<Name>/
    <Name>.mtd                    — метаданные (параметры, структуры, события)
    <Name>.resx                   — пользовательские ресурсы (EN)
    <Name>.ru.resx                — пользовательские ресурсы (RU)
    <Name>System.resx             — системные ресурсы (EN)
    <Name>System.ru.resx          — системные ресурсы (RU) — DisplayName, Description
  $MODULE/DirRX.CRM.Server/Reports/<Name>/
    <Name>.frx                    — макет FastReport (XML)
    <Name>Handlers.cs             — BeforeExecute / AfterExecute обработчики
    <Name>.g.cs                   — автогенерируемый код (не трогать)
```

---

## ШАГ 1: Report .mtd структура

Полный шаблон метаданных отчёта на основе реальных CRM-отчётов.

```json
{
  "$type": "Sungero.Metadata.ReportMetadata, Sungero.Reporting.Shared",
  "NameGuid": "<НОВЫЙ-GUID>",
  "Name": "<ReportName>",
  "AssociatedGuid": "<MODULE-GUID>",
  "BaseGuid": "cef9a810-3f30-4eca-9fe3-30992af0b818",
  "DataSources": [],
  "DefaultExportFormat": "Pdf",
  "ExportFormats": ["Pdf", "Excel"],
  "HandledEvents": [
    "BeforeExecuteServer",
    "AfterExecuteServer"
  ],
  "IconResourcesKeys": [],
  "Overridden": [
    "PublicStructures",
    "Parameters",
    "HandledEvents",
    "ExportFormats",
    "DefaultExportFormat",
    "DisplayName",
    "Description"
  ],
  "Parameters": [
    {
      "NameGuid": "<GUID>",
      "Name": "ReportSessionId",
      "InternalDataTypeName": "System.String",
      "Versions": []
    }
  ],
  "PublicConstants": [
    {
      "Name": "SourceTableName",
      "ParentClasses": ["<ReportName>"],
      "TypeName": "System.String",
      "Value": "\"Sungero_Reports_<ShortName>\""
    }
  ],
  "PublicStructures": [
    {
      "Name": "<ReportName>TableLine",
      "IsPublic": true,
      "Properties": [
        {
          "Name": "<ColumnName>",
          "IsNullable": true,
          "TypeFullName": "global::System.String"
        },
        {
          "Name": "ReportSessionId",
          "IsNullable": true,
          "TypeFullName": "global::System.String"
        }
      ],
      "StructureNamespace": "<SolutionName>.Structures.<ReportName>"
    }
  ],
  "ResourcesKeys": [
    "ReportName",
    "<ColumnLabel1>",
    "<ColumnLabel2>"
  ],
  "Versions": [
    {
      "Type": "ReportMetadata",
      "Number": 1
    }
  ]
}
```

### Ключевые поля .mtd

| Поле | Описание |
|------|----------|
| `AssociatedGuid` | GUID модуля, к которому привязан отчёт |
| `BaseGuid` | `cef9a810-3f30-4eca-9fe3-30992af0b818` — базовый тип Report |
| `DataSources` | `[]` — для SQL-отчётов (данные через temp-таблицу) |
| `HandledEvents` | `BeforeExecuteServer` + `AfterExecuteServer` — обязательно для temp-таблиц |
| `Parameters` | Входные параметры отчёта. `ReportSessionId` — ОБЯЗАТЕЛЕН для temp-таблиц |
| `PublicConstants.SourceTableName` | Имя временной таблицы в БД (макс. 30 символов) |
| `PublicStructures` | Структура строки temp-таблицы. Каждое свойство = колонка |
| `ResourcesKeys` | Ключи для .resx (локализованные подписи колонок) |

### Типы параметров

| C# тип | `InternalDataTypeName` | `IsSimpleDataType` |
|--------|------------------------|--------------------|
| string | `System.String` | не указывать |
| int/long | `System.Int64` | `true` |
| DateTime | `System.DateTime` | `true` |
| double | `System.Double` | `true` |
| bool | `System.Boolean` | `true` |
| Entity ref | `Sungero.CoreEntities.IRecipient` | не указывать + `EntityTypeId` |

---

## ШАГ 2: .frx макет (FastReport XML)

### Структура .frx

```xml
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp"
        ReferencedAssemblies="System.dll&#13;&#10;System.Core.dll&#13;&#10;...&#13;&#10;DirRX.CRM.Shared&#13;&#10;DirRX.CRM.Server"
        ReportInfo.CreatorVersion="2020.2.12.0">

  <!-- 1. СКРИПТ (C# код внутри отчёта) -->
  <ScriptText>
    using FastReport; using FastReport.Data; ...
    using Resources = DirRX.CRM.Reports.Resources;

    namespace FastReport {
      public class ReportScript { }
    }
  </ScriptText>

  <!-- 2. СЛОВАРЬ ДАННЫХ -->
  <Dictionary>
    <!-- SQL-подключение -->
    <SungeroSqlDataConnection Name="Sungero_Connection"
      Restrictions="DontModify, DontEdit, DontDelete, HideAllProperties"
      ConnectionStringExpression="[SungeroConnectionString]">

      <!-- Источник данных (temp-таблица) -->
      <TableDataSource Name="Table" Alias="SourceTable"
        DataType="System.Int32" Enabled="true" CanEdit="true"
        SelectCommand="${SelectDataFromTable}">
        <Column Name="ColumnName" DataType="System.String" PropName="Column"/>
        <Column Name="Amount" DataType="System.Double" PropName="Column"/>
        <!-- @ReportSessionId — фильтр по сессии -->
        <CommandParameter Name="ReportSessionId" DataType="16"
          IsDbType="true" Expression="[ReportSessionId]"/>
      </TableDataSource>
    </SungeroSqlDataConnection>

    <!-- Параметры (зеркалят Parameters из .mtd) -->
    <SungeroParameter Name="SungeroConnectionString" Restrictions="DontModify, DontEdit, DontDelete, HideAllProperties, DontShow"
      Id="8d5b9efe-b1a6-4cce-8109-b47ea85c1d33" ... />
    <SungeroParameter Name="ReportSessionId" Id="<SAME-GUID-AS-MTD>" ... />
  </Dictionary>

  <!-- 3. СТРАНИЦА ОТЧЁТА -->
  <ReportPage Name="Page1">
    <!-- Заголовок отчёта (один раз) -->
    <ReportTitleBand Name="ReportTitle1" Height="75.6">
      <TextObject Name="Title" Text="[Resources.MyReport.ReportName]"
        HorzAlign="Center" Font="Arial, 14pt, style=Bold"/>
    </ReportTitleBand>

    <!-- Заголовок страницы (на каждой странице) -->
    <PageHeaderBand Name="PageHeader1" Height="28.35">
      <TableObject Name="HeaderTable">
        <!-- Заголовки колонок из Resources -->
      </TableObject>
    </PageHeaderBand>

    <!-- Данные (для каждой строки источника) -->
    <DataBand Name="Data1" DataSource="Table" CanGrow="true">
      <TextObject Name="Col1" Text="[SourceTable.ColumnName]" />
      <Sort>
        <Sort Expression="[SourceTable.SortField]"/>
      </Sort>
    </DataBand>
  </ReportPage>
</Report>
```

### Ключевые элементы .frx

| Элемент | Назначение |
|---------|-----------|
| `SungeroSqlDataConnection` | Подключение к БД RX (всегда `Sungero_Connection`) |
| `TableDataSource` | Источник данных; `SelectCommand="${SelectDataFromTable}"` — авто-запрос по temp-таблице |
| `CommandParameter ReportSessionId` | Фильтр строк temp-таблицы по сессии отчёта |
| `SungeroParameter` | Параметры; GUID из `Id` **ДОЛЖЕН** совпадать с `NameGuid` в .mtd |
| `ReportTitleBand` | Заголовок (один раз в начале) |
| `PageHeaderBand` | Шапка таблицы (повторяется на каждой странице) |
| `DataBand` | Строки данных; `DataSource="Table"` привязывает к TableDataSource |
| `TextObject` | Текст/значение; `[SourceTable.Column]` — данные, `[Resources.Report.Key]` — локализация |
| `Sort` | Сортировка внутри DataBand |

### Обращение к данным в .frx

```
[SourceTable.ColumnName]                   — значение колонки
[Resources.MyReport.ReportName]            — локализованная строка из .resx
[ParameterName]                            — значение параметра
[Page] / [TotalPages] / [Date]             — системные переменные
```

### GroupHeader (группировка)

```xml
<GroupHeaderBand Name="GroupHeader1" Condition="[SourceTable.DepartmentName]">
  <TextObject Text="[SourceTable.DepartmentName]" Font="Arial, 11pt, style=Bold"/>
</GroupHeaderBand>
<DataBand Name="Data1" DataSource="Table">
  <!-- строки внутри группы -->
</DataBand>
<GroupFooterBand Name="GroupFooter1">
  <TextObject Text="Итого: [Count()]"/>
</GroupFooterBand>
```

---

## ШАГ 3: Server Handlers (BeforeExecute / AfterExecute)

Файл: `<Module>.Server/Reports/<Name>/<Name>Handlers.cs`

### Паттерн temp-таблицы (стандартный для CRM)

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace DirRX.CRM
{
  partial class <ReportName>ServerHandlers
  {
    public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)
    {
      // 1. Создать уникальный ID сессии
      var reportSessionId = System.Guid.NewGuid().ToString();
      <ReportName>.ReportSessionId = reportSessionId;

      // 2. Собрать данные и записать в temp-таблицу
      // Используй Sungero.Docflow.PublicFunctions.Module.WriteReportData(tableName, dataList)
      // или прямой SQL INSERT
    }

    public override void AfterExecute(Sungero.Reporting.Server.AfterExecuteEventArgs e)
    {
      // 3. Очистить temp-таблицу по ReportSessionId
      Sungero.Docflow.PublicFunctions.Module.DeleteReportData(
        "<SourceTableName>",
        <ReportName>.ReportSessionId);
    }
  }
}
```

### Реальный пример (SalesFunnelReport)

```csharp
public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)
{
  var reportSessionId = System.Guid.NewGuid().ToString();
  SalesFunnelReport.ReportSessionId = reportSessionId;
  // Данные собираются в Functions.Module и пишутся в temp-таблицу
}

public override void AfterExecute(Sungero.Reporting.Server.AfterExecuteEventArgs e)
{
  Sungero.Docflow.PublicFunctions.Module.DeleteReportData(
    "Sungero_Reports_SalesFunnel", SalesFunnelReport.ReportSessionId);
}
```

### Порядок выполнения отчёта

```
Client BeforeExecute  -- запрос параметров (диалог), e.Cancel для отмены
    |
Server BeforeExecute  -- подготовка данных, заполнение temp-таблицы
    |
[Сбор данных из источников — SQL по temp-таблице]
    |
[Рендеринг макета .frx]
    |
Server AfterExecute   -- очистка temp-таблицы
    |
[Отображение / экспорт]
```

---

## ШАГ 4: Ресурсы (.resx)

### System.resx (системные — DisplayName, Description)

Файл: `<Name>System.ru.resx`

```xml
<data name="DisplayName" xml:space="preserve">
  <value>Воронка продаж</value>
</data>
<data name="Description" xml:space="preserve">
  <value>Отчёт по воронке продаж с конверсией по этапам</value>
</data>
```

### User .resx (пользовательские — подписи колонок)

Файл: `<Name>.ru.resx`

Ключи = `ResourcesKeys` из .mtd:

```xml
<data name="ReportName" xml:space="preserve">
  <value>Воронка продаж</value>
</data>
<data name="StageName" xml:space="preserve">
  <value>Этап</value>
</data>
<data name="DealCount" xml:space="preserve">
  <value>Кол-во сделок</value>
</data>
<data name="TotalAmount" xml:space="preserve">
  <value>Сумма</value>
</data>
<data name="ConversionRate" xml:space="preserve">
  <value>Конверсия, %</value>
</data>
<data name="Period" xml:space="preserve">
  <value>Период</value>
</data>
```

### Использование в .frx

```
[Resources.<ReportName>.<Key>]
```

Пример: `[Resources.SalesFunnelReport.ReportName]` -> "Воронка продаж"

---

## ШАГ 5: PublicStructures (строка temp-таблицы)

Структура определяется в .mtd -> `PublicStructures`. Каждое свойство = колонка temp-таблицы.

### Обязательные правила

1. Свойство `ReportSessionId` (String, IsNullable=true) — **ОБЯЗАТЕЛЬНО** в каждой структуре
2. Имена свойств **ДОЛЖНЫ** совпадать с именами Column в .frx
3. `StructureNamespace` = `<SolutionName>.Structures.<ReportName>`
4. `IsPublic: true` — чтобы структура была доступна из других модулей

### Маппинг типов Structure -> SQL -> .frx

| Structure TypeFullName | SQL тип (PostgreSQL) | .frx DataType |
|------------------------|---------------------|---------------|
| `global::System.String` | `text` | `System.String` |
| `global::System.Int32` | `integer` | `System.Int32` |
| `global::System.Int64` | `bigint` | `System.Int64` |
| `global::System.Double` | `double precision` | `System.Double` |
| `global::System.DateTime` | `timestamp` | `System.DateTime` |
| `global::System.Boolean` | `boolean` | `System.Boolean` |

---

## ШАГ 6: Программный вызов отчёта

### С клиента (открыть в просмотрщике)

```csharp
var report = DirRX.CRM.Reports.GetSalesFunnelReport();
report.PipelineId = pipeline.Id;
report.PeriodStart = dateFrom;
report.PeriodEnd = dateTo;
report.PipelineName = pipeline.Name;
report.Open();
```

### С сервера (экспорт в Stream)

```csharp
[Remote]
public virtual Stream ExportSalesFunnel(long pipelineId, DateTime from, DateTime to)
{
  var report = DirRX.CRM.Reports.GetSalesFunnelReport();
  report.PipelineId = pipelineId;
  report.PeriodStart = from;
  report.PeriodEnd = to;
  report.ExportFormat = ReportExportFormat.Pdf;
  return report.Export();
}
```

### Сохранить как версию документа

```csharp
var report = DirRX.CRM.Reports.GetSalesFunnelReport();
report.PipelineId = pipeline.Id;
report.ExportTo(document); // Добавит новую версию
document.Save();
```

---

## ШАГ 7: Чеклист создания отчёта

1. [ ] Сгенерировать GUID для NameGuid и каждого параметра
2. [ ] Создать .mtd с Parameters, PublicStructures, PublicConstants, ResourcesKeys
3. [ ] Создать .frx с Dictionary (Column = свойства структуры), ReportPage, DataBand
4. [ ] GUID параметров в .frx `Id` = `NameGuid` в .mtd Parameters
5. [ ] Создать `<Name>Handlers.cs` с BeforeExecute (fill temp) + AfterExecute (cleanup)
6. [ ] Создать 4 .resx файла: System.resx, System.ru.resx, .resx, .ru.resx
7. [ ] System.ru.resx: `DisplayName` + `Description`
8. [ ] User .ru.resx: все ключи из ResourcesKeys
9. [ ] `SourceTableName` в PublicConstants (макс 30 символов, начинается с `Sungero_Reports_`)
10. [ ] Валидация: `validate_report path="<path-to-report-dir>"`
11. [ ] Валидация пакета: `check_package packagePath="<package-path>"`

---

## MCP-инструменты

| Инструмент | Когда использовать |
|------------|-------------------|
| `scaffold_report` | Создать отчёт с нуля (MTD + .frx + Handlers + resx) |
| `validate_report` | Проверить .frx <-> .mtd consistency, датасеты, подключения |
| `check_package` | Проверить весь пакет перед импортом (14 проверок) |

### scaffold_report

```
scaffold_report
  modulePath="$WORKSPACE/CRM/crm-package/source/DirRX.CRM"
  reportName="ConversionReport"
  moduleName="DirRX.CRM"
  russianName="Отчёт по конверсии"
  parameters="PipelineId:long,PeriodStart:DateTime,PeriodEnd:DateTime"
```

### validate_report

```
validate_report path="$WORKSPACE/CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Server/Reports/SalesFunnelReport"
```

---

## Ссылки

- `knowledge-base/guides/13_reports.md` — Guide 13: паттерны отчётов, события, макет, API
- `knowledge-base/guides/30_reports_advanced.md` — Guide 30: продвинутые сценарии (если существует)
- `docs/platform/DDS_KNOWN_ISSUES.md` — Known Issue #13: обязательные DisplayName для отчётов
- Реальные отчёты: `CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Shared/Reports/` (6 отчётов)
