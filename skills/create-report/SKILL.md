---
description: "Создать отчёт Directum RX — MTD, FastReport .frx, Queries.xml, обработчики, ресурсы"
---

# Создание отчёта Directum RX

Подробнее: docs/platform/REFERENCE_CODE.md

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `validate_report` — валидация отчёта: .frx и Queries.xml, датасеты, подключения
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей resx из MTD
- `generate_structures_cs` — генерация ModuleStructures.g.cs из PublicStructures в MTD
- `search_metadata filterType=Report` — поиск эталонных отчётов в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Найди отчёт в платформе:
MCP: search_metadata type=ReportMetadata

# Найди .frx шаблоны в текущем проекте:
# Glob("{project}/source/**/*.frx") — найти существующие шаблоны
# Платформенные .frx шаблоны находятся в base/ внутри контейнеров RX

# Посмотри структуру: MTD + Queries.xml + .frx + Server handler.
```

## Входные данные

Спроси у пользователя (если не указано):
- **CompanyCode** — код компании (например, `Acme`)
- **ModuleName** — имя модуля (например, `CRM`)
- **ReportName** — имя отчёта (например, `SalesFunnelReport`)
- **DisplayName** — отображаемое имя (например, `Отчёт по воронке продаж`)
- **Parameters** — параметры отчёта (имя, тип, обязательность)
- **Columns** — колонки данных для отчёта (имя, тип, заголовок)
- **Grouping** — группировка данных (опционально)
- **HasDialog** — нужен ли диалог ввода параметров (по умолчанию: да)

## Что создаётся

```
source/{Company}.{Module}/
  {Company}.{Module}.Server/
    Reports/{ReportName}/
      {ReportName}Handlers.cs          # Server: BeforeExecute + AfterExecute
      {ReportName}.frx                 # FastReport шаблон
      {ReportName}Queries.xml          # SQL: создание и выборка из temp-таблицы
  {Company}.{Module}.ClientBase/
    Reports/{ReportName}/
      {ReportName}Handlers.cs          # Client: BeforeExecute (диалог параметров)
  {Company}.{Module}.Shared/
    Reports/{ReportName}/
      {ReportName}.mtd                 # Метаданные отчёта
      {ReportName}Constants.cs         # Константы (имя temp-таблицы)
      {ReportName}.resx                # Ресурсы (EN)
      {ReportName}.ru.resx             # Ресурсы (RU)
```

## Алгоритм

### 0. MCP валидация (ПОСЛЕ создания всех файлов отчёта)

После создания MTD, .frx, Queries.xml, Handlers и resx:
```
MCP: validate_report path={путь_к_директории_отчёта}
MCP: generate_structures_cs moduleMtdPath={путь_к_Module.mtd} save=true
MCP: check_package packagePath={путь_к_пакету}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```

### 1. Сгенерируй GUIDs
- `ReportGuid` — для отчёта
- `ParameterGuids` — по одному на каждый параметр + служебные (ReportSessionId, SourceDataTableName, ReportDate)

### 2. {ReportName}.mtd

```json
{
  "$type": "Sungero.Metadata.ReportMetadata, Sungero.Reporting.Shared",
  "NameGuid": "<ReportGuid>",
  "Name": "<ReportName>",
  "AssociatedGuid": "<ModuleGuid>",
  "BaseGuid": "cef9a810-3f30-4eca-9fe3-30992af0b818",
  "DataSources": [],
  "DefaultExportFormat": "Pdf",
  "ExportFormats": ["Pdf", "Excel"],
  "HandledEvents": ["BeforeExecuteServer", "AfterExecuteServer"],
  "IconResourcesKeys": [],
  "Parameters": [
    {
      "NameGuid": "<Guid>",
      "Name": "ReportSessionId",
      "InternalDataTypeName": "System.String",
      "Versions": []
    },
    {
      "NameGuid": "<Guid>",
      "Name": "SourceDataTableName",
      "InternalDataTypeName": "System.String",
      "Versions": []
    },
    {
      "NameGuid": "<Guid>",
      "Name": "ReportDate",
      "InternalDataTypeName": "System.DateTime",
      "IsSimpleDataType": true,
      "Versions": []
    },
    {
      "NameGuid": "<ParamGuid>",
      "Name": "<ParamName>",
      "InternalDataTypeName": "<InternalType>",
      "IsSimpleDataType": true,
      "Versions": []
    }
  ],
  "PublicStructures": [
    {
      "Name": "TableLine",
      "IsPublic": true,
      "Properties": [
        {
          "Name": "ReportSessionId",
          "IsNullable": true,
          "TypeFullName": "global::System.String"
        },
        {
          "Name": "<ColumnName>",
          "IsNullable": true,
          "TypeFullName": "global::System.<Type>"
        }
      ]
    }
  ],
  "ResourcesKeys": [
    "ReportName",
    "DialogTitle",
    "<ParamDisplayName>",
    "<ColumnHeader>"
  ],
  "Overridden": [
    "PublicStructures",
    "Parameters",
    "HandledEvents",
    "ExportFormats",
    "DefaultExportFormat",
    "DisplayName",
    "Description"
  ],
  "Versions": [
    { "Type": "ReportMetadata", "Number": 1 }
  ]
}
```

> **ВАЖНО**: В `Versions` указывать ТОЛЬКО `ReportMetadata`. НЕ добавлять `DomainApi` — реальные отчёты его не содержат.
> В `Overridden` НЕ указывать `IsPublic`, `DataSources`, `ResourcesKeys` — эти поля не перекрываются в реальных отчётах.
> В `ExportFormats` допустимые значения: `"Pdf"`, `"Excel"`, `"Word"`. Реальный SalesFunnelReport использует `["Pdf", "Excel"]`.
> `BeforeExecuteClient` в `HandledEvents` — добавляй только если нужен диалог параметров на клиенте.

**InternalDataTypeName маппинг:**
| C# тип | InternalDataTypeName |
|--------|---------------------|
| `string` | `System.String` |
| `int` | `System.Int32` |
| `long` | `System.Int64` |
| `DateTime` | `System.DateTime` |
| `bool` | `System.Boolean` |
| `double` | `System.Double` |
| Navigation (entity) | `Sungero.{Module}.I{Entity}, Sungero.Domain.Interfaces` |

**PublicStructures TypeFullName маппинг:**
| C# тип | TypeFullName |
|--------|-------------|
| `string` | `global::System.String` |
| `int` | `global::System.Int32` |
| `long` | `global::System.Int64` |
| `DateTime` | `global::System.Nullable<global::System.DateTime>` |
| `bool` | `global::System.Boolean` |
| `double` | `global::System.Nullable<global::System.Double>` |
| Entity | `global::Sungero.{Module}.I{Entity}` (+ `"IsEntity": true`) |

### 3. {ReportName}.frx — FastReport шаблон

```xml
<?xml version="1.0" encoding="utf-8"?>
<Report ScriptLanguage="CSharp"
  ReferencedAssemblies="System.dll, System.Drawing.dll, System.Windows.Forms.dll, System.Data.dll, System.Xml.dll, System.Core.dll, FastReport.Compat.dll, FastReport.DataVisualization.dll"
  ReportInfo.CreatorVersion="2020.2.12.0">
  <ScriptText>
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows.Forms;
    using System.Drawing;
    using System.Data;
    using FastReport;
    using FastReport.Data;
    using FastReport.Dialog;
    using FastReport.Barcode;
    using FastReport.Table;
    using FastReport.Utils;

    namespace FastReport
    {
      public class ReportScript
      {
      }
    }
  </ScriptText>
  <Dictionary>
    <SungeroSqlDataConnection Name="Sungero_Connection" ConnectionString="">
      <TableDataSource Name="Sungero_Reports_{ReportName}" DataType="System.Int32"
        Enabled="true" ConnectionString="" SelectCommand="{SelectQuery}">
        <Column Name="<ColumnName>" DataType="System.String"/>
        <Column Name="<ColumnName>" DataType="System.Int32"/>
        <Column Name="<ColumnName>" DataType="System.DateTime"/>
      </TableDataSource>
    </SungeroSqlDataConnection>
    <SungeroParameter Name="ReportDate" DataType="System.DateTime"/>
    <SungeroParameter Name="ReportName" DataType="System.String"
      Expression="[Reports.Resources.{ReportName}.ReportName]"/>
    <SungeroParameter Name="<ParamName>" DataType="System.String"/>
  </Dictionary>
  <ReportPage Name="Page1" LeftMargin="10" TopMargin="10" RightMargin="10" BottomMargin="10">
    <ReportTitleBand Name="ReportTitle1" Width="718.2" Height="47.25">
      <TextObject Name="Text1" Width="718.2" Height="28.35"
        Text="[ReportName]"
        HorzAlign="Center"
        Font="Arial, 14pt, style=Bold"/>
      <TextObject Name="Text2" Top="28.35" Width="718.2" Height="18.9"
        Text="[ReportDate]"
        HorzAlign="Center"
        Font="Arial, 9pt"
        Format="Date" Format.Format="d"/>
    </ReportTitleBand>
    <PageHeaderBand Name="PageHeader1" Top="51.25" Width="718.2" Height="28.35">
      <TextObject Name="Header_Col1" Width="200" Height="28.35"
        Text="[Reports.Resources.{ReportName}.<ColumnHeader1>]"
        Border.Lines="All" Border.Color="LightGray"
        Fill.Color="WhiteSmoke"
        VertAlign="Center"
        Font="Arial, 9pt, style=Bold"/>
      <TextObject Name="Header_Col2" Left="200" Width="200" Height="28.35"
        Text="[Reports.Resources.{ReportName}.<ColumnHeader2>]"
        Border.Lines="All" Border.Color="LightGray"
        Fill.Color="WhiteSmoke"
        VertAlign="Center"
        Font="Arial, 9pt, style=Bold"/>
    </PageHeaderBand>
    <DataBand Name="Data1" Top="83.6" Width="718.2" Height="18.9"
      DataSource="Sungero_Reports_{ReportName}">
      <TextObject Name="Cell_Col1" Width="200" Height="18.9"
        Text="[Sungero_Reports_{ReportName}.<ColumnName1>]"
        Border.Lines="All" Border.Color="LightGray"
        VertAlign="Center"
        Font="Arial, 8pt"/>
      <TextObject Name="Cell_Col2" Left="200" Width="200" Height="18.9"
        Text="[Sungero_Reports_{ReportName}.<ColumnName2>]"
        Border.Lines="All" Border.Color="LightGray"
        VertAlign="Center"
        Font="Arial, 8pt"/>
    </DataBand>
    <PageFooterBand Name="PageFooter1" Top="106.5" Width="718.2" Height="18.9">
      <TextObject Name="FooterPageNo" Width="718.2" Height="18.9"
        Text="[Page] / [TotalPages]"
        HorzAlign="Right"
        Font="Arial, 8pt"/>
    </PageFooterBand>
  </ReportPage>
</Report>
```

**Вариации .frx:**

#### С группировкой (GroupHeaderBand):
```xml
<GroupHeaderBand Name="Group1" Top="83.6" Width="718.2" Height="28.35"
  Condition="[Sungero_Reports_{ReportName}.GroupColumn]"
  SortOrder="None">
  <TextObject Name="GroupHeader" Width="718.2" Height="28.35"
    Text="[Sungero_Reports_{ReportName}.GroupColumn]"
    Fill.Color="WhiteSmoke"
    Font="Arial, 10pt, style=Bold"/>
  <DataBand Name="Data1" Top="115.95" Width="718.2" Height="18.9"
    DataSource="Sungero_Reports_{ReportName}">
    <!-- Ячейки данных -->
  </DataBand>
</GroupHeaderBand>
```

#### С условным форматированием (Highlight):
```xml
<DataBand Name="Data1" DataSource="Sungero_Reports_{ReportName}">
  <TextObject Name="StatusCell" Text="[Sungero_Reports_{ReportName}.Status]">
    <Highlight>
      <Condition Expression="[Sungero_Reports_{ReportName}.IsOverdue]==true"
        Font="Arial, 8pt" TextFill.Color="Red"/>
    </Highlight>
  </TextObject>
</DataBand>
```

#### С гиперссылками:
```xml
<TextObject Name="LinkedName"
  Text="[Sungero_Reports_{ReportName}.EntityName]"
  Hyperlink.Expression="[Sungero_Reports_{ReportName}.HyperlinkUri]"
  Font="Arial, 8pt, style=Underline" TextFill.Color="Blue"/>
```

### 4. {ReportName}Queries.xml — SQL для temp-таблицы

```xml
<?xml version="1.0" encoding="utf-8"?>
<queries>
  <query key="CreateSourceTable">
    <mssql><![CDATA[CREATE TABLE {0}
(ReportSessionId VARCHAR(256) NOT NULL,
 Id INT NOT NULL,
 <ColumnName> NVARCHAR(MAX) NULL,
 <ColumnName> INT NULL,
 <ColumnName> DATETIME NULL,
 <ColumnName> BIT NULL)]]></mssql>
    <postgres><![CDATA[CREATE TABLE {0}
(ReportSessionId citext NOT NULL,
 Id int NOT NULL,
 <ColumnName> citext NULL,
 <ColumnName> int NULL,
 <ColumnName> timestamp NULL,
 <ColumnName> boolean NULL)]]></postgres>
  </query>
  <query key="SourceQuery">
    <default><![CDATA[SELECT *
FROM {0}
WHERE ReportSessionId = @ReportSessionId]]></default>
  </query>
</queries>
```

**SQL-типы маппинг:**
| C# тип | MSSQL | PostgreSQL |
|--------|-------|-----------|
| `string` | `NVARCHAR(MAX)` | `citext` |
| `int` | `INT` | `int` |
| `long` | `BIGINT` | `bigint` |
| `DateTime` | `DATETIME` | `timestamp` |
| `bool` | `BIT` | `boolean` |
| `double` | `FLOAT` | `double precision` |
| `decimal` | `DECIMAL(18,2)` | `numeric(18,2)` |

**Шаблон SelectQuery для .frx:**
`SelectCommand` в `<TableDataSource>` должен совпадать с `SourceQuery`:
```
SELECT * FROM {TableName} WHERE ReportSessionId = @ReportSessionId
```
Где `{TableName}` = значение из `Constants.{ReportName}.SourceTableName`.

### 5. {ReportName}Constants.cs

**Альтернатива (рекомендуемая):** Вместо файла Constants.cs можно объявить `PublicConstants` прямо в MTD:
```json
"PublicConstants": [
  {
    "Name": "SourceTableName",
    "ParentClasses": ["{ReportName}"],
    "TypeName": "System.String",
    "Value": "\"Sungero_Reports_{ShortName}\""
  }
]
```
Это подход из реального SalesFunnelReport. Если используешь `PublicConstants` в MTD — файл Constants.cs НЕ нужен.

**Ручной файл (если PublicConstants в MTD не используется):**
```csharp
using System;

namespace {Company}.{Module}.Constants
{
  public static class {ReportName}
  {
    /// <summary>
    /// Имя временной таблицы для данных отчёта.
    /// </summary>
    public const string SourceTableName = "Sungero_Reports_{ShortName}";

    /// <summary>
    /// Код диалога справки.
    /// </summary>
    public const string HelpCode = "{Company}_{Module}_{ReportName}Dialog";
  }
}
```

**Правила именования SourceTableName:**
- Формат: `Sungero_Reports_{ShortName}` (без слова Report)
- Максимум ~50 символов (ограничение PostgreSQL)
- Примеры: `Sungero_Reports_RegSettings`, `Sungero_Reports_ExchangeOrder`

### 6. Server Handler — {ReportName}Handlers.cs

> **ВАЖНО**: Handler class = `{ReportName}ServerHandlers` (НЕ `{ReportName}Handlers`).
> Реальный пример: `SalesFunnelReportServerHandlers` в CRM.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}
{
  partial class {ReportName}ServerHandlers
  {
    public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)
    {
      // 1. Инициализация сессии
      var reportSessionId = System.Guid.NewGuid().ToString();
      if (string.IsNullOrWhiteSpace({ReportName}.ReportSessionId))
        {ReportName}.ReportSessionId = reportSessionId;

      {ReportName}.ReportDate = Calendar.UserToday;
      {ReportName}.SourceDataTableName = Constants.{ReportName}.SourceTableName;

      // 2. Получение данных
      var tableData = this.GetReportData(reportSessionId);

      // 3. Запись во временную таблицу
      Functions.Module.WriteStructuresToTable(
        {ReportName}.SourceDataTableName, tableData);
    }

    public override void AfterExecute(Sungero.Reporting.Server.AfterExecuteEventArgs e)
    {
      // Очистка временной таблицы
      Sungero.Docflow.PublicFunctions.Module.DeleteReportData(
        {ReportName}.SourceDataTableName,
        {ReportName}.ReportSessionId);
    }

    /// <summary>
    /// Получить данные отчёта.
    /// </summary>
    /// <param name="reportSessionId">Идентификатор сессии.</param>
    /// <returns>Данные для отчёта.</returns>
    public virtual List<Structures.{ReportName}.ITableLine> GetReportData(string reportSessionId)
    {
      var result = new List<Structures.{ReportName}.ITableLine>();
      var id = 0;

      var entities = MyEntities.GetAll()
        .Where(e => e.Status == Status.Active);

      // Фильтрация по параметрам отчёта
      if ({ReportName}.PeriodBegin.HasValue)
        entities = entities.Where(e => e.Created >= {ReportName}.PeriodBegin.Value);
      if ({ReportName}.PeriodEnd.HasValue)
        entities = entities.Where(e => e.Created <= {ReportName}.PeriodEnd.Value);

      foreach (var entity in entities.ToList())
      {
        var line = Structures.{ReportName}.TableLine.Create();
        line.Id = id++;
        line.ReportSessionId = reportSessionId;
        line.Name = entity.Name;
        // Заполнение остальных колонок
        result.Add(line);
      }

      return result;
    }
  }
}
```

**Паттерн с гиперссылками (кликабельные ссылки в отчёте):**
```csharp
// Добавить в GetReportData:
line.EntityName = entity.Name;
line.HyperlinkUri = Hyperlinks.Get(entity);
```

**Паттерн WriteStructuresToTable (вспомогательная функция модуля):**
```csharp
/// <summary>
/// Записать структуры во временную таблицу.
/// </summary>
[Public]
public static void WriteStructuresToTable<T>(string tableName, List<T> data)
{
  // Создать таблицу если не существует
  var query = Queries.{ReportName}.CreateSourceTable;
  Sungero.Docflow.PublicFunctions.Module.CreateReportTable(
    string.Format(query, tableName));

  // Вставить данные
  foreach (var item in data)
  {
    // INSERT строки
  }
}
```

**Альтернатива — использовать Docflow.PublicFunctions:**
```csharp
// Если модуль зависит от Docflow, можно использовать готовые функции:
Sungero.Docflow.PublicFunctions.Module.CreateReportTable(tableName);
Functions.Module.WriteStructuresToTable(tableName, tableData);
Sungero.Docflow.PublicFunctions.Module.DeleteReportData(tableName, sessionId);
```

### 7. Client Handler — {ReportName}Handlers.cs

#### Простой диалог (2-3 параметра):
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}
{
  partial class {ReportName}Handlers
  {
    public override void BeforeExecute(Sungero.Reporting.Client.BeforeExecuteEventArgs e)
    {
      // Если параметры уже установлены (вызов из кода) — пропустить диалог
      if ({ReportName}.PeriodBegin.HasValue && {ReportName}.PeriodEnd.HasValue)
        return;

      var dialog = Dialogs.CreateInputDialog(
        {Company}.{Module}.Reports.Resources.{ReportName}.DialogTitle);
      dialog.HelpCode = Constants.{ReportName}.HelpCode;
      dialog.Buttons.AddOkCancel();

      var periodBegin = dialog.AddDate(
        {Company}.{Module}.Reports.Resources.{ReportName}.PeriodBegin,
        true, Calendar.Today.BeginningOfMonth());
      var periodEnd = dialog.AddDate(
        {Company}.{Module}.Reports.Resources.{ReportName}.PeriodEnd,
        true, Calendar.Today);

      dialog.SetOnRefresh((args) =>
      {
        if (periodBegin.Value > periodEnd.Value)
          args.AddError(
            {Company}.{Module}.Reports.Resources.{ReportName}.InvalidPeriod);
      });

      if (dialog.Show() != DialogButtons.Ok)
      {
        e.Cancel = true;
        return;
      }

      {ReportName}.PeriodBegin = periodBegin.Value;
      {ReportName}.PeriodEnd = periodEnd.Value;
    }
  }
}
```

#### Расширенный диалог (динамическая видимость, связанные поля):
```csharp
public override void BeforeExecute(Sungero.Reporting.Client.BeforeExecuteEventArgs e)
{
  if ({ReportName}.PeriodBegin.HasValue)
    return;

  var dialog = Dialogs.CreateInputDialog(
    {Company}.{Module}.Reports.Resources.{ReportName}.DialogTitle);
  dialog.HelpCode = Constants.{ReportName}.HelpCode;

  // Навигация к сущности
  var department = dialog.AddSelect(
    {Company}.{Module}.Reports.Resources.{ReportName}.Department,
    false, Sungero.Company.Departments.Null);

  // Выпадающий список
  var periods = new[] { "Месяц", "Квартал", "Год" };
  var period = dialog.AddSelect(
    {Company}.{Module}.Reports.Resources.{ReportName}.Period,
    true).From(periods);
  period.Value = "Месяц";

  // Даты
  var periodBegin = dialog.AddDate(
    {Company}.{Module}.Reports.Resources.{ReportName}.PeriodBegin,
    true, Calendar.Today.BeginningOfMonth());
  var periodEnd = dialog.AddDate(
    {Company}.{Module}.Reports.Resources.{ReportName}.PeriodEnd,
    true, Calendar.Today);

  // Динамическое обновление дат при смене периода
  period.SetOnValueChanged((arg) =>
  {
    var today = Calendar.Today;
    if (arg.NewValue == "Месяц")
    {
      periodBegin.Value = today.BeginningOfMonth();
      periodEnd.Value = today;
    }
    else if (arg.NewValue == "Квартал")
    {
      periodBegin.Value = today.BeginningOfQuarter();
      periodEnd.Value = today;
    }
    else if (arg.NewValue == "Год")
    {
      periodBegin.Value = today.BeginningOfYear();
      periodEnd.Value = today;
    }
  });

  // Динамическая видимость
  dialog.SetOnRefresh((arg) =>
  {
    periodBegin.IsVisible = period.Value != null;
    periodEnd.IsVisible = period.Value != null;

    if (periodBegin.Value > periodEnd.Value)
      arg.AddError(
        {Company}.{Module}.Reports.Resources.{ReportName}.InvalidPeriod);
  });

  if (dialog.Show() != DialogButtons.Ok)
  {
    e.Cancel = true;
    return;
  }

  {ReportName}.Department = department.Value;
  {ReportName}.PeriodBegin = periodBegin.Value;
  {ReportName}.PeriodEnd = periodEnd.Value;
}
```

### 8. Ресурсы

**{ReportName}.resx** (EN):
```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="0" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <data name="ReportName" xml:space="preserve"><value>{DisplayName EN}</value></data>
  <data name="DialogTitle" xml:space="preserve"><value>Report parameters</value></data>
  <data name="PeriodBegin" xml:space="preserve"><value>Period begin</value></data>
  <data name="PeriodEnd" xml:space="preserve"><value>Period end</value></data>
  <data name="InvalidPeriod" xml:space="preserve"><value>Start date cannot be later than end date</value></data>
  <data name="{ColumnHeader}" xml:space="preserve"><value>{Column Display Name EN}</value></data>
</root>
```

**{ReportName}.ru.resx** (RU):
Аналогичная структура, но с русскими значениями.

### 9. Обнови Module.mtd

Добавь GUID отчёта в секцию `Reports` модуля:
```json
"Reports": [
  "<ReportGuid>"
]
```

### 10. Функция открытия отчёта

**Из Action** (ClientBase/{Entity}Actions.cs):
```csharp
public virtual void ShowReport(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var report = Reports.Get{ReportName}();
  report.PeriodBegin = Calendar.Today.BeginningOfMonth();
  report.PeriodEnd = Calendar.Today;
  report.Open();
}
```

**Из Cover** (ModuleClientFunctions.cs):
```csharp
[LocalizeFunction]
public virtual void Show{ReportName}()
{
  var report = Reports.Get{ReportName}();
  report.Open();
}
```

**Из Server** (ModuleServerFunctions.cs):
```csharp
[Public, Remote]
public static void Open{ReportName}()
{
  var report = Reports.Get{ReportName}();
  report.Open();
}
```

## Pipeline выполнения отчёта

```
1. Client BeforeExecute → показать диалог → установить параметры
2. Server BeforeExecute → Guid.NewGuid() → получить данные → WriteStructuresToTable
3. FastReport (.frx) → SELECT из temp-таблицы → рендер PDF/Word
4. Server AfterExecute → DeleteReportData (очистка temp-таблицы)
5. Отчёт показывается пользователю
```

## Валидация

- [ ] .mtd — валидный JSON, BaseGuid = `cef9a810-3f30-4eca-9fe3-30992af0b818`
- [ ] Versions: ТОЛЬКО `ReportMetadata` (без DomainApi)
- [ ] Server handler class = `{ReportName}ServerHandlers` (НЕ `{ReportName}Handlers`)
- [ ] Все классы — `partial class`
- [ ] Handler namespace = `{Company}.{Module}` (без `.Server`/`.Client`)
- [ ] Constants — `public static class` (НЕ partial!)
- [ ] Constants namespace = `{Company}.{Module}.Constants`
- [ ] .resx пара: `.resx` + `.ru.resx` с полными resheader (reader + writer!)
- [ ] Параметры в .mtd совпадают с использованием в handlers
- [ ] Ресурсные ключи в .mtd ResourcesKeys совпадают с .resx
- [ ] .frx колонки совпадают с SQL в Queries.xml
- [ ] .frx DataSource Name = `Sungero_Reports_{ReportName}`
- [ ] .frx SelectCommand = SELECT из temp-таблицы с @ReportSessionId
- [ ] Queries.xml: отдельные ветки `<mssql>` и `<postgres>` для CreateSourceTable
- [ ] SQL типы: `NVARCHAR(MAX)` → `citext`, `BIT` → `boolean`, `DATETIME` → `timestamp`
- [ ] AfterExecute вызывает DeleteReportData (очистка!)
- [ ] PublicStructures.Properties типы с `global::` префиксом
- [ ] Overridden НЕ содержит `IsPublic` и `DataSources`

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
