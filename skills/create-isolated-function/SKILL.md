# /create-isolated-function

Создание изолированных функций (IsolatedArea) в Directum RX -- .NET 8 песочница для сторонних библиотек, ресурсоемких вычислений, внешних API.

---

## ШАГ 0: Реальные примеры

### Приоритет 1 — KPI (production IsolatedFunctions, XLSX парсинг)

| Файл | Путь |
|------|------|
| **IsolatedFunctions (190 строк, эталон)** | `targets/source/DirRX.KPI/DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/IsolatedFunctions.cs` (если доступен) |
| **IsolatedArea.cs** | `targets/source/DirRX.KPI/DirRX.KPI.Isolated/IsolatedAreas/XLSXParsing/IsolatedArea.cs` (если доступен) |
| **MTD (IsolatedAreas секция)** | `targets/source/DirRX.KPI/DirRX.KPI.Shared/Module.mtd` — искать "IsolatedAreas" (если доступен) |
| **Документация** | `targets/REFERENCE_CATALOG.md` секция IsolatedAreas (если доступен) |

**3 production-функции:**
```csharp
// 1. Парсинг XLSX — принимает IByteArray (не Stream!)
[Public] public virtual Structures.Module.IMetricMassImportResult
  GetMetricActiualDataFromXLSX(Structures.Module.IByteArray file, long? metricId)

// 2. Генерация XLSX с данными
[Public] public virtual Structures.Module.IByteArray
  CreateBodyFromStructure(List<Structures.Module.IMetricMassImportActualValues> data,
                          Structures.Module.ILocalizedTemplateNames names)

// 3. Пустой шаблон
[Public] public virtual Structures.Module.IByteArray
  GenerateEmptyTemplate(bool isExtendedFormat,
                        Structures.Module.ILocalizedTemplateNames names)
```

**Паттерн IByteArray (НЕ Stream!):**
```csharp
// В Isolated нельзя передать Stream — используй структуру:
// MTD PublicStructure:
// { "Name": "ByteArray", "Properties": [{ "Name": "Content", "Type": "BinaryData" }] }
// Использование:
var result = Structures.Module.ByteArray.Create();
result.Content = workbook.SaveToStream().ToArray(); // byte[]
return result;
```

**Паттерн локализации в Isolated:**
```csharp
// Ресурсы недоступны в Isolated — передавай как структуру:
// ILocalizedTemplateNames { MetricName, Period, Value, Comment, Date }
// Заполняй на вызывающей стороне (Server):
var names = Structures.Module.LocalizedTemplateNames.Create();
names.MetricName = DirRX.DTCommons.Resources.MetricName; // из resx
```

### Приоритет 2 — CRM (scaffold-only, нет реализаций)

### Структура Isolated-проектов в CRM

Каждый модуль CRM содержит `*.Isolated/` папку (даже если пустой):

```
{package_path}/source/  (пример — CRM)
  DirRX.CRM/DirRX.CRM.Isolated/
    DirRX.CRM.Isolated.csproj
    AssemblyInfo.cs
    Module.g.cs          -- ModuleIsolatedAreas.g.cs (auto-generated)
    Structures.g.cs      -- auto-generated structures
  DirRX.CRMSales/DirRX.CRMSales.Isolated/
  DirRX.CRMMarketing/DirRX.CRMMarketing.Isolated/
  DirRX.CRMDocuments/DirRX.CRMDocuments.Isolated/
  DirRX.CRMCommon/DirRX.CRMCommon.Isolated/
  DirRX.Solution/DirRX.Solution.Isolated/
```

### .csproj -- доступные библиотеки

```xml
<!-- DirRX.CRM.Isolated.csproj -->
<Reference Include="Sungero.IsolatedArea.Extensions">
  <HintPath>..\..\..\.sds\Libraries\IsolatedArea\Sungero.IsolatedArea.Extensions.dll</HintPath>
</Reference>
<Reference Include="Newtonsoft.Json">
  <HintPath>..\..\..\.sds\Libraries\3dParty\Newtonsoft.Json.dll</HintPath>
</Reference>
<Reference Include="Aspose.Words">
  <HintPath>..\..\..\.sds\Libraries\3dParty\Aspose.Words.dll</HintPath>
</Reference>
<Reference Include="Aspose.Cells">
  <HintPath>..\..\..\.sds\Libraries\3dParty\Aspose.Cells.dll</HintPath>
</Reference>
<Reference Include="Aspose.PDF">
  <HintPath>..\..\..\.sds\Libraries\3dParty\Aspose.PDF.dll</HintPath>
</Reference>
<!-- Также: Aspose.BarCode, Aspose.HTML, Aspose.Imaging, Aspose.Slides, Aspose.Drawing.Common -->
```

### Module.mtd -- IsolatedAssemblyName

Модуль ссылается на Isolated-сборку:

```json
{
  "IsolatedAssemblyName": "DirRX.CRMDocuments.Isolated",
  "IsolatedNamespace": "DirRX.CRMDocuments.Isolated"
}
```

### Module.g.cs (auto-generated, пустая область)

```csharp
// ModuleIsolatedAreas.g.cs -- auto-generated
namespace DirRX.CRM.Isolated
{
}
```

> В CRM пока нет реализованных Isolated-функций -- проекты scaffold-only. Ниже приведены платформенные паттерны.

---

## Концепция IsolatedArea

**Изолированная область** -- отдельный .NET 8 процесс, изолированный от остальных сервисов RX.

**Зачем:**
- Сторонние библиотеки (Aspose, Newtonsoft, REST-клиенты)
- Ресурсоемкие вычисления (конвертация PDF, XLSX, обработка изображений)
- Внешние API-вызовы (HTTP клиенты, парсинг)
- Изоляция ошибок -- если IsolatedArea упадет, остальные сервисы работают

**Ограничения:**
- Нет доступа к сущностям, репозиториям, БД внутри Isolated-кода
- В перекрывающих модулях нельзя добавлять новые области
- Нет hot-publication -- нужен перезапуск веб-сервера
- IDE-отладка недоступна (используйте Visual Studio attach to process)

---

## Создание в DDS

1. Открыть модуль в DDS
2. Узел "Изолированные области" -> Добавить -> задать имя (латиница + цифры)
3. "Функции" -- создать вызываемые функции (контракт)
4. "Код области" -- написать реализацию
5. Собрать -- создаётся `<ModuleName>.Isolated.dll`

---

## Типы параметров функций

Изолированные функции работают **только** с сериализуемыми типами:

| Тип | Поддержка |
|-----|-----------|
| `string`, `int`, `bool`, `DateTime`, `double`, `decimal` | Да |
| `System.IO.Stream` | Да (аргумент и возврат) |
| `List<T>` | Да (T -- простой тип или структура) |
| `Dictionary<K,V>` | Да |
| Структуры модуля `[Public(Isolated=true)]` | Да |
| Сущности, интерфейсы (`IEntity`, `IDocument`) | **НЕТ** |
| Репозитории (`Employees.GetAll()`) | **НЕТ** |

---

## Паттерн: Публичная структура для IsolatedArea

```csharp
// Shared код модуля -- структура, доступная в Isolated
[Public(Isolated=true)]
partial class MyDataInfo
{
  public int Id { get; set; }
  public string Name { get; set; }
  public string Status { get; set; }
}
```

---

## Паттерн: Функция в Isolated Area

### Код области (Isolated)
```csharp
// Внутри Isolated Area -- имеет доступ к сторонним библиотекам
[Public]
public Stream AddStamp(Stream pdfDocumentStream, string stamp)
{
  var pdfStamper = new PdfConverter.PdfStamper();
  return pdfStamper.AddStamp(pdfDocumentStream, stamp);
}
```

### Вызов из серверного кода
```csharp
// Формат: Sungero.<ModuleName>.IsolatedFunctions.<AreaName>.<MethodName>(<args>)
public virtual void LoadVersionAndAddStamp(
    IElectronicDocumentVersions version, string path, string stamp)
{
  using (FileStream fileStream = File.OpenRead(path))
  using (Stream documentWithStamp =
      Sungero.StampModule.IsolatedFunctions.StampArea.AddStamp(fileStream, stamp))
  {
    version.Body.Write(documentWithStamp);
  }
}
```

---

## Паттерн: XLSX генерация через Aspose.Cells

### Isolated Area: GenerateReport
```csharp
[Public]
public Stream GenerateXlsxReport(List<MyModule.Structures.Module.IReportRow> rows, string title)
{
  var workbook = new Aspose.Cells.Workbook();
  var sheet = workbook.Worksheets[0];
  sheet.Name = title;

  // Header
  sheet.Cells["A1"].PutValue("Название");
  sheet.Cells["B1"].PutValue("Сумма");
  sheet.Cells["C1"].PutValue("Дата");

  // Data
  for (int i = 0; i < rows.Count; i++)
  {
    sheet.Cells[$"A{i+2}"].PutValue(rows[i].Name);
    sheet.Cells[$"B{i+2}"].PutValue(rows[i].Amount);
    sheet.Cells[$"C{i+2}"].PutValue(rows[i].Date);
  }

  var stream = new MemoryStream();
  workbook.Save(stream, Aspose.Cells.SaveFormat.Xlsx);
  stream.Position = 0;
  return stream;
}
```

### Server: вызов
```csharp
var rows = GetReportData(); // List<Structures.Module.IReportRow>
using (var xlsxStream = MyModule.IsolatedFunctions.ReportArea.GenerateXlsxReport(rows, "Отчет"))
{
  version.Body.Write(xlsxStream);
  version.AssociatedApplication = Sungero.Content.AssociatedApplications.GetByExtension("xlsx");
}
```

---

## Паттерн: Word-генерация через Aspose.Words

### Isolated Area: GenerateDocument
```csharp
[Public]
public Stream GenerateFromTemplate(Stream templateStream, string jsonData)
{
  var doc = new Aspose.Words.Document(templateStream);
  var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonData);

  foreach (var kvp in data)
  {
    doc.Range.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
  }

  var output = new MemoryStream();
  doc.Save(output, Aspose.Words.SaveFormat.Docx);
  output.Position = 0;
  return output;
}
```

---

## Паттерн: HTTP-вызов внешнего API

### Isolated Area: ExternalApi
```csharp
[Public]
public string CallExternalApi(string url, string requestBody, string authToken)
{
  using (var client = new System.Net.Http.HttpClient())
  {
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");
    var content = new System.Net.Http.StringContent(
      requestBody,
      System.Text.Encoding.UTF8,
      "application/json");

    var response = client.PostAsync(url, content).Result;
    return response.Content.ReadAsStringAsync().Result;
  }
}
```

---

## Правила работы со Stream

| Правило | Описание |
|---------|----------|
| Аргументы | Только базовый `System.IO.Stream` |
| Внутри функции | Любой тип (`FileStream`, `MemoryStream` и т.д.) |
| Возврат | Только **один** Stream |
| Чтение | Возвращённый Stream можно прочитать **один раз** |

---

## Структура проекта *.Isolated/

```
DirRX.MyModule/
  DirRX.MyModule.Isolated/
    DirRX.MyModule.Isolated.csproj    -- ссылки на библиотеки
    AssemblyInfo.cs                   -- auto-generated
    Module.g.cs                       -- auto-generated (namespace + areas)
    Structures.g.cs                   -- auto-generated (структуры)
    MyArea.cs                         -- КОД ОБЛАСТИ (ваш код!)
```

### .csproj шаблон (ключевые ссылки)

```xml
<Reference Include="Sungero.IsolatedArea.Extensions">
  <HintPath>..\..\..\.sds\Libraries\IsolatedArea\Sungero.IsolatedArea.Extensions.dll</HintPath>
  <Private>False</Private>
</Reference>
<!-- Сторонние библиотеки: scope "Сервер и изолированные области" -->
<Reference Include="Newtonsoft.Json">
  <HintPath>..\..\..\.sds\Libraries\3dParty\Newtonsoft.Json.dll</HintPath>
  <Private>False</Private>
</Reference>
```

---

## Доступные сторонние библиотеки

По умолчанию в `.sds/Libraries/3dParty/`:

| Библиотека | Назначение |
|------------|-----------|
| `Newtonsoft.Json` | JSON сериализация |
| `Aspose.Words` | Word-документы |
| `Aspose.Cells` | Excel-файлы |
| `Aspose.PDF` | PDF-документы |
| `Aspose.HTML` | HTML-конвертация |
| `Aspose.Imaging` | Обработка изображений |
| `Aspose.BarCode` | Штрих-коды |
| `Aspose.Slides` | PowerPoint |
| `Aspose.Drawing.Common` | Графика |
| `Aspose.Words.Shaping.HarfBuzz` | Шрифты/типография |

---

## Чеклист создания Isolated-функции

- [ ] Определить, нужна ли IsolatedArea (сторонние библиотеки? ресурсоемкие вычисления?)
- [ ] Создать область в DDS: модуль -> "Изолированные области" -> Добавить
- [ ] Определить контракт функций (параметры: только простые типы + Stream + структуры)
- [ ] Если нужны структуры -- пометить `[Public(Isolated=true)]` в Shared-коде
- [ ] Реализовать код области (нет доступа к сущностям!)
- [ ] Вызвать из Server через `IsolatedFunctions.<AreaName>.<Method>()`
- [ ] Не забыть `using` для Stream (возвращённый Stream одноразовый)

---

## Типичные ошибки

| Ошибка | Причина | Решение |
|--------|---------|---------|
| `Cannot access entity` | Попытка работать с сущностями в Isolated | Передавать данные через параметры/структуры |
| `Stream already disposed` | Повторное чтение возвращённого Stream | Читать Stream один раз, копировать в MemoryStream если нужно |
| `Type not found` | Структура без `[Public(Isolated=true)]` | Добавить атрибут |
| Изменения не применились | Нет hot-publication для Isolated | Перезапустить веб-сервер |

---

## MCP: Валидация

```
MCP: validate_isolated_areas    -- проверить Isolated-области модуля
MCP: check_code_consistency     -- проверить согласованность Server/Isolated кода
MCP: validate_all               -- полная валидация
```

---

## Reference

- **Guide 18**: `knowledge-base/guides/18_integrations_isolated.md`
- **Isolated .csproj**: `{package_path}/source/{ModuleName}/{ModuleName}.Isolated/{ModuleName}.Isolated.csproj`
- **Module.mtd (IsolatedNamespace)**: `{package_path}/source/{ModuleName}/{ModuleName}.Shared/Module.mtd`
- **Guide 25**: `knowledge-base/guides/25_code_patterns.md` (IsolatedArea-паттерны)
