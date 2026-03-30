# Интеграции и изолированные области (IsolatedArea)

> Источник: `sds_isolatedarea.htm`, `om_rabota_s_giperssylkami_hyperlinks.htm`

---

## Изолированные области (IsolatedArea)

### Назначение

Изолированная область позволяет выполнять код сторонних библиотек в **отдельном процессе**, изолированном от других сервисов. Если ошибка возникает в изолированном коде — остальные сервисы продолжают работать.

**Типичные сценарии:**
- Интеграция со сторонними библиотеками
- Ресурсоёмкие вычисления (конвертация документов, обработка PDF)
- Работа с внешними API, где возможны непредсказуемые ошибки

### Создание в DDS

1. Редактор модуля → узел «Изолированные области»
2. Добавить → задать имя (латиница + цифры)
3. Через ссылку «Функции» — создать вызываемые функции
4. Через ссылку «Код области» — написать код, выполняемый в изолированном сервисе

После сборки создаётся сборка `<ModuleName>.Isolated.dll`.

### Типы параметров функций

Изолированные функции принимают и возвращают:
- Простые типы (`string`, `int`, `bool`, `DateTime` и др.)
- `Stream` (`System.IO.Stream`)
- Списки (`List<T>`)
- Словари (`Dictionary<K,V>`)
- Структуры изолированного модуля

### Публичная структура для IsolatedArea

```csharp
// Атрибут [Public(Isolated=true)] делает структуру доступной в изолированной области.
[Public(Isolated=true)]
partial class DpadSignaturInfo
{
  public int SignIcon { get; set; }
  public string Status { get; set; }
}
```

### Публичная функция

```csharp
// Атрибут [Public] делает функцию вызываемой из client/server/shared кода.
[Public]
public Stream AddStamp(Stream pdfDocumentStream, string stamp)
{
  var pdfStamper = new PdfConverter.PdfStamper();
  return pdfStamper.AddStamp(pdfDocumentStream, stamp);
}
```

### Формат вызова

```csharp
Sungero.<ModuleName>.IsolatedFunctions.<AreaName>.<MethodName>(<arguments>)
```

### Полный пример: добавление штампа в PDF

```csharp
// Изолированная функция (код области).
[Public]
public Stream AddStamp(Stream pdfDocumentStream, string stamp)
{
  var pdfStamper = new PdfConverter.PdfStamper();
  return pdfStamper.AddStamp(pdfDocumentStream, stamp);
}

// Вызов из серверной функции модуля.
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

### Правила работы со Stream

| Правило | Описание |
|---------|----------|
| Аргументы | Только базовый `System.IO.Stream` |
| Внутри тела функции | Любой тип потока (FileStream, MemoryStream и т.д.) |
| Возвращаемое значение | Только **один** Stream |
| Чтение | Возвращённый Stream можно прочитать **один раз** |

### Ограничения

| Ограничение | Описание |
|------------|----------|
| Нет доступа к сущностям | Внутри изолированной области нельзя работать с сущностями, репозиториями или БД |
| Нет новых областей при перекрытии | В перекрывающих модулях нельзя добавлять новые изолированные области |
| Нет hot-publication | При изменении кода области нужен перезапуск веб-сервера |
| .NET 8 runtime | Изолированные области выполняются в .NET 8 runtime (ранее .NET 6) |
| Отладка | IDE-отладка недоступна — используйте Microsoft Visual Studio |

### Сторонние библиотеки

Можно использовать библиотеки со scope «Сервер и изолированные области». По умолчанию доступны: Aspose, Newtonsoft.Json и другие стандартные библиотеки.

---

## Гиперссылки (Hyperlinks)

### Класс Sungero.Core.Hyperlinks

| Метод | Описание |
|-------|----------|
| `Hyperlinks.Get(entity)` | Получить ссылку на экземпляр сущности |
| `Hyperlinks.Open(uri)` | Открыть гиперссылку |

### Примеры

```csharp
// Получить ссылку на документ.
var link = Hyperlinks.Get(document);

// Открыть email.
Hyperlinks.Open("mailto:" + email);

// В тексте уведомления.
var message = string.Format("Документ создан: {0}", Hyperlinks.Get(document));
```

### Гиперссылки в UserDigest (всплывающая подсказка)

```csharp
var digest = UserDigest.Create(_obj);
digest.AddHyperlink("mailto://" + _obj.Email, _obj.Email);
digest.AddHyperlink("https://mycompany.com", "ООО Компания", "Сайт:");
```

### Гиперссылки в диалогах

```csharp
var dialog = Dialogs.CreateInputDialog("Резервирование номера");
var hyperlink = dialog.AddHyperlink("Пропущенные номера");

hyperlink.SetOnExecute(() => {
  var report = Reports.GetSkippedNumbersReport();
  report.DocumentRegisterId = register.Value.Id;
  report.Open();
});
```

---

## Интеграционный сервис (IntegrationServiceName)

Каждый тип сущности может иметь свойство `IntegrationServiceName` в метаданных (`.mtd`), определяющее имя сервиса для внешних интеграций:

```json
"IntegrationServiceName": "Module1Document1"
```

Это имя используется для доступа к сущности через OData-сервис платформы.

---

*Источники: sds_isolatedarea.htm · om_rabota_s_giperssylkami_hyperlinks.htm · om_addhyperlink.htm · om_giperssylka_addhyperlink.htm · archive/extracted/Document1.mtd*
