# Работа с электронными документами

> Источник: `webhelp/WebClient/ru-RU/om_electronicdocument*.htm`, `om_versions*.htm`, `Sungero.Content.xml`

---

## Основные типы документов

В Sungero все документы — это сущности, унаследованные от базовых типов:

| Тип | Namespace | Описание |
|-----|-----------|----------|
| `IElectronicDocument` | `Sungero.Content` | Базовый электронный документ |
| `IOfficialDocument` | `Sungero.Docflow` | Официальный документ (с реквизитами) |
| `IAccountingDocumentBase` | `Sungero.Docflow` | Бухгалтерский документ |
| `IContract` | `Sungero.Contracts` | Договор |
| `IIncomingDocumentBase` | `Sungero.Docflow` | Входящий документ |
| `IOutgoingDocumentBase` | `Sungero.Docflow` | Исходящий документ |
| `IMemo` | `Sungero.Docflow` | Служебная записка |

---

## Получение и работа с документом

```csharp
// Получить официальный документ по ID.
var document = Sungero.Docflow.OfficialDocuments.Get(documentId);

// Привести к конкретному типу.
var contract = Sungero.Contracts.Contracts.As(document);
if (contract != null)
{
  var amount = contract.TotalAmount;
  var counterparty = contract.Counterparty;
}

// Получить все договоры контрагента.
var contracts = Sungero.Contracts.Contracts.GetAll(c =>
  c.Counterparty != null &&
  c.Counterparty.Id == counterpartyId &&
  c.LifeCycleState == Sungero.Contracts.Contract.LifeCycleState.Active);
```

---

## Версии документа

Документ может иметь несколько версий. Работа с версиями через `document.Versions`:

```csharp
// Последняя версия документа.
var lastVersion = document.LastVersion;

// Номер последней версии.
var versionNumber = lastVersion?.Number;

// Размер файла в байтах.
var sizeBytes = lastVersion?.Body?.Size;

// Расширение файла.
var extension = lastVersion?.AssociatedApplication?.Extension?.ToLower();

// Получить все версии.
var versions = document.Versions.OrderByDescending(v => v.Number).ToList();

// Проверить: заблокирована ли хотя бы одна версия.
var hasLockedVersion = document.Versions.Any(v => v.IsLocked == true);
```

### Чтение и запись содержимого версии

```csharp
// Прочитать содержимое версии в поток.
using (var stream = lastVersion.Body.Read())
{
  // Обработать поток...
  var bytes = new byte[stream.Length];
  stream.Read(bytes, 0, bytes.Length);
}

// Записать новую версию из файла.
using (var fileStream = System.IO.File.OpenRead(filePath))
{
  document.CreateVersion(fileStream, "pdf");
}
```

---

## Подписи и ЭЦП

```csharp
// Получить подписи документа.
var signatures = Sungero.Domain.Shared.Signatures.Get(document);

// Проверить: есть ли действующая квалифицированная подпись.
var hasQualifiedSignature = signatures.Any(s =>
  s.SignCertificate != null &&
  s.SignatureType == Sungero.Domain.Shared.SignatureType.Qualified &&
  s.IsValid);

// Получить дату подписания.
foreach (var signature in signatures)
{
  Logger.DebugFormat(
    "Подписал: {0}, дата: {1}, тип: {2}",
    signature.SignatoryFullName,
    signature.SigningDate,
    signature.SignatureType);
}
```

---

## Связи между документами (Relations)

```csharp
// Добавить связь "Основание" между документами.
document.Relations.AddFrom("Basis", relatedDocument);

// Получить все связанные документы.
var related = document.Relations.GetRelated();

// Получить только документы типа "Приложение".
var attachments = document.Relations.GetRelated("Addendum");

// Удалить связь.
document.Relations.Remove("Basis", relatedDocument);
```

---

## Шаблоны документов

```csharp
// Получить шаблон по имени.
var template = Sungero.Docflow.DocumentTemplates.GetAll()
  .FirstOrDefault(t => t.Name == "Договор поставки");

// Создать документ из шаблона.
if (template != null)
{
  var doc = Sungero.Contracts.Contracts.Create();
  doc.DocumentTemplate = template;
  doc.CreateVersionFromTemplate(template);
  doc.Save();
}
```

---

## Конвертация в PDF

```csharp
// Проверить: можно ли конвертировать интерактивно.
if (document.CanConvertToPdfInteractively())
{
  document.ConvertToPdf();
}

// Конвертация с отметками согласования.
document.ConvertToPdfWithMarks();

// Переопределение условия конвертации (в прикладном коде).
public override bool CanConvertToPdfInteractively()
{
  if (!base.CanConvertToPdfInteractively())
    return false;

  // JPG меньше 1 МБ — конвертировать интерактивно.
  var ext = _obj.LastVersion?.AssociatedApplication?.Extension?.ToLower();
  if (ext == "jpg" || ext == "jpeg")
    return _obj.LastVersion?.Body?.Size < 1024 * 1024;

  return true;
}
```

---

## Права на работу с документом (PrivacyOptions)

```csharp
// Ограничить работу с документом вне системы.
public override ElectronicDocumentPrivacyOptions GetDocumentPrivacyOptions()
{
  var options = ElectronicDocumentPrivacyOptions.Create();

  // Документ конфиденциальный — запретить открытие во внешних редакторах.
  if (_obj.IsConfidential == true)
    options.IsConfidential = true;

  return options;
}
```

---

## Хранилище документов (Storage)

```csharp
// Получить хранилище версии документа.
var storage = lastVersion?.Body?.Storage;
if (storage != null)
{
  Logger.DebugFormat("Хранилище: {0}", storage.Name);
}

// Проверить доступность хранилища.
var availableStorages = Sungero.CoreEntities.Storages.GetAll(s =>
  s.IsEnabled == true).ToList();
```

---

## Отображение документа пользователю

```csharp
// Открыть карточку документа (клиентский код).
document.ShowCard();

// Открыть список документов.
Sungero.Contracts.Contracts.Show();

// Открыть список с фильтром.
Sungero.Contracts.Contracts.GetAll(c =>
  c.Status == Sungero.Contracts.Contract.Status.Active)
  .Show("Активные договоры");

// Диалог выбора документа.
var dialog = Dialogs.CreateSelectDialog<IOfficialDocument>(
  "Выберите основание",
  Sungero.Docflow.OfficialDocuments.GetAll());
if (dialog.Show())
{
  _obj.BasisDocument = dialog.Selected;
}
```

---

## Работа с вложениями задачи

```csharp
// Получить документ из группы вложений задачи.
var document = task.DocumentGroup.OfficialDocuments.FirstOrDefault();

// Получить все документы из группы.
var docs = task.DocumentGroup.OfficialDocuments.ToList();

// Добавить документ в задачу.
task.Attachments.Add(document);

// Проверить тип документа в группе.
foreach (var att in task.Attachments)
{
  var officialDoc = Sungero.Docflow.OfficialDocuments.As(att);
  if (officialDoc != null)
    ProcessOfficialDocument(officialDoc);
}
```

---

## Ключевые паттерны

### Проверка типа через As()

```csharp
// Безопасное приведение (As возвращает null если не совпадает).
var invoice = Sungero.Docflow.AccountingDocumentBases.As(document);
if (invoice != null)
{
  // Работать как со счётом.
}

// Is() — только проверка типа.
if (Sungero.Contracts.Contracts.Is(document))
{
  Logger.Debug("Это договор.");
}
```

### Получение последней подписанной версии

```csharp
public static bool IsDocumentSigned(IOfficialDocument document)
{
  var signatures = Sungero.Domain.Shared.Signatures.Get(document);
  return signatures.Any(s =>
    s.SignatureType == Sungero.Domain.Shared.SignatureType.Qualified &&
    s.IsValid &&
    !s.IsExternal);
}
```

---

## Жизненный цикл документа (LifeCycleState)

| Состояние | Описание |
|-----------|----------|
| `Draft` | Черновик — документ создан, но не в обороте |
| `Active` | Действующий — документ в работе |
| `Obsolete` | Устаревший — архивный, не используется |

```csharp
// Проверить состояние.
if (document.LifeCycleState == OfficialDocument.LifeCycleState.Active)
  Logger.Debug("Документ действующий.");

// Сменить состояние.
document.LifeCycleState = OfficialDocument.LifeCycleState.Obsolete;
document.Save();
```

---

## Регистрация и нумерация документов

### Регистрационные данные

```csharp
// Регистрационный номер и дата.
var regNumber = document.RegistrationNumber;   // строка
var regDate = document.RegistrationDate;       // DateTime?

// Журнал регистрации.
var register = document.DocumentRegister;      // IDocumentRegister

// Состояние регистрации.
var regState = document.RegistrationState;
// Значения: NotRegistered, Registered, Reserved
```

### Проверка прав на регистрацию

```csharp
// Может ли текущий пользователь зарегистрировать документ.
if (document.AccessRights.CanRegister())
{
  // Регистрация доступна.
}
```

---

## Жизненный цикл со стороны Exchange (ЭДО)

Документы, полученные через ЭДО, имеют дополнительные свойства:

```csharp
// Проверить: является ли документ обменным.
var exchangeDoc = Sungero.Exchange.ExchangeDocuments.As(document);
if (exchangeDoc != null)
{
  var info = Sungero.Exchange.ExchangeDocumentInfos.GetAll()
    .FirstOrDefault(x => x.Document != null && x.Document.Id == document.Id);
}
```

---

## Дополнительные операции с документами

### Переадресация (Forward)

```csharp
// Переадресовать задание другому исполнителю.
assignment.Forward(newPerformer, ForwardingRule.CreateAssignment);
```

### Действия в истории (BeforeSaveHistory)

```csharp
// Добавить запись в историю документа.
public override void BeforeSaveHistory(Sungero.Content.DocumentHistoryEventArgs e)
{
  if (e.Action == Sungero.CoreEntities.History.Action.Create &&
      _obj.RegistrationState != RegistrationState.NotRegistered)
  {
    var operation = new Enumeration(Constants.OfficialDocument.Operation.Registration);
    e.Write(operation, operationDetailed, comment, null);
  }
}
```

---

*Источники: om_electronicdocument.htm · om_versions_body.htm · om_electronicdocumentprivacyoptions.htm · om_canregister.htm · sds_sobytiia_sushchnosti.htm · Sungero.Content.xml*
