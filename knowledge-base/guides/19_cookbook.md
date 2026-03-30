# Рецепты и типовые паттерны

> Сборник готовых решений для типичных задач разработки на платформе Directum RX

---

## 1. Создание и сохранение сущности

```csharp
// Создать новый документ.
var document = Sungero.Docflow.SimpleDocuments.Create();
document.Name = "Новый документ";
document.DocumentKind = Sungero.Docflow.DocumentKinds.GetAll()
    .FirstOrDefault(k => k.Name == "Простой документ");
document.Save();
```

---

## 2. Поиск сущности с проверкой типа

```csharp
// ПРАВИЛЬНО: проверка типа через Is/As.
if (Sungero.Company.Employees.Is(recipient))
{
  var employee = Sungero.Company.Employees.As(recipient);
  Logger.DebugFormat("Employee: {0}", employee.Name);
}

// НЕПРАВИЛЬНО: через is/as C# — не работает с NHibernate.
// if (recipient is IEmployee) — ЗАПРЕЩЕНО
```

---

## 3. Remote-функция с возвратом данных

```csharp
// Серверная функция.
[Remote]
public static List<string> GetDocumentNames(long counterpartyId)
{
  return Sungero.Docflow.OfficialDocuments.GetAll()
    .Where(d => d.Counterparty != null && d.Counterparty.Id == counterpartyId)
    .Select(d => d.Name)
    .ToList();
}

// Вызов из клиентского кода.
var names = Functions.Module.Remote.GetDocumentNames(counterparty.Id);
```

---

## 4. Создание задачи с вложениями

```csharp
// Создать простую задачу.
var task = Sungero.Workflow.SimpleTasks.Create();
task.Subject = "Ознакомиться с документом";

// Добавить исполнителя.
task.RouteSteps.AddNew().Performer = employee;

// Добавить вложение.
task.Attachments.Add(document);

// Запустить задачу.
task.Start();
```

---

## 5. Работа с правами доступа

```csharp
// Выдать права на чтение.
document.AccessRights.Grant(employee, DefaultAccessRightsTypes.Read);
document.AccessRights.Save();

// Проверить права.
if (document.AccessRights.CanUpdate())
  document.Name = "Обновлённое имя";

// Получить данные без учёта прав (серверный код).
AccessRights.AllowRead(() =>
{
  var allDocs = OfficialDocuments.GetAll().ToList();
});
```

---

## 6. Диалог с зависимыми полями

```csharp
var dialog = Dialogs.CreateInputDialog("Создание документа");
var docKind = dialog.AddSelect("Вид документа", true)
    .From(Sungero.Docflow.DocumentKinds.GetAll().ToArray());
var category = dialog.AddSelect("Категория", false)
    .From(new Sungero.Docflow.IDocumentGroupBase[0]);

// При смене вида — обновить список категорий.
docKind.SetOnValueChanged((arg) =>
{
  if (arg.NewValue != null)
  {
    var categories = Sungero.Docflow.DocumentGroupBases.GetAll()
        .Where(c => c.DocumentKinds.Any(k => Equals(k.DocumentKind, arg.NewValue)))
        .ToArray();
    category.From(categories);
  }
});

if (dialog.Show() == DialogButtons.Ok)
{
  // Использовать выбранные значения.
}
```

---

## 7. Блокировка сущности перед изменением

```csharp
var isLocked = false;
try
{
  isLocked = Locks.TryLock(document);
  if (!isLocked)
  {
    Dialogs.ShowMessage("Документ заблокирован другим пользователем.",
        MessageType.Warning);
    return;
  }

  document.Name = "Новое имя";
  document.Save();
}
finally
{
  if (isLocked)
    Locks.Unlock(document);
}
```

---

## 8. Асинхронная обработка из действия

```csharp
// Действие (клиентский код).
public virtual void ConvertToPdf(Sungero.Domain.Client.ExecuteActionArgs e)
{
  Functions.Module.Remote.StartConversion(_obj.Id);
}

// Серверная функция.
[Remote]
public static void StartConversion(long documentId)
{
  var handler = MyModule.AsyncHandlers.ConvertDocument.Create();
  handler.DocumentId = documentId;
  handler.ExecuteAsync(
      "Конвертация началась.",
      "Конвертация завершена.",
      Users.Current);
}

// Асинхронный обработчик.
public virtual void ConvertDocument(
    Sungero.MyModule.AsyncHandlers.ConvertDocumentInvokeArgs args)
{
  var doc = OfficialDocuments.Get(args.DocumentId);
  if (doc == null) return;

  if (!Locks.TryLock(doc))
  {
    args.Retry = true;
    return;
  }

  try
  {
    // Конвертация.
    ConvertDocumentToPdf(doc);
    doc.Save();
  }
  finally
  {
    Locks.Unlock(doc);
  }
}
```

---

## 9. SQL-запрос с параметрами

```csharp
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  command.CommandText = @"
    SELECT COUNT(*)
    FROM Sungero_Content_EDoc
    WHERE Discriminator = @disc
      AND Created >= @dateFrom
      AND Created <= @dateTo";

  SQL.AddParameter(command, "@disc", discriminator, DbType.String);
  SQL.AddParameter(command, "@dateFrom", dateFrom, DbType.DateTime);
  SQL.AddParameter(command, "@dateTo", dateTo, DbType.DateTime);

  var count = (int)command.ExecuteScalar();
  Logger.DebugFormat("Found {0} documents", count);
}
```

---

## 10. Кэширование в Params между событиями формы

```csharp
// В Refresh — вычислить и сохранить.
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  bool hasPermission;
  if (!e.Params.TryGetValue("HasPermission", out hasPermission))
  {
    hasPermission = Functions.Module.Remote.CheckPermission(_obj.Id);
    e.Params.AddOrUpdate("HasPermission", hasPermission);
  }
}

// В CanExecute — прочитать без Remote-вызова.
public override bool CanDoAction(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  bool hasPermission;
  return e.Params.TryGetValue("HasPermission", out hasPermission)
      && hasPermission;
}
```

---

## 11. Фильтрация списка в событии Filtering

```csharp
public override IQueryable<Sungero.MyModule.IMyEntity> Filtering(
    IQueryable<Sungero.MyModule.IMyEntity> query,
    Sungero.Domain.FilteringEventArgs e)
{
  // По подразделению текущего пользователя.
  var employee = Sungero.Company.Employees.As(Users.Current);
  if (employee != null && employee.Department != null)
    query = query.Where(x => Equals(x.Department, employee.Department));

  // По дате создания за последний месяц.
  query = query.Where(x => x.Created >= Calendar.Today.AddMonths(-1));

  return query;
}
```

---

## 12. Работа с версиями документа

```csharp
// Получить последнюю версию.
var lastVersion = document.LastVersion;

// Создать новую версию.
document.CreateVersion();
var newVersion = document.LastVersion;

// Записать содержимое в версию.
using (var stream = new MemoryStream(bytes))
{
  newVersion.Body.Write(stream);
}
document.Save();
```

---

## 13. Отправка email-уведомления

```csharp
// Серверный код.
var message = Mail.CreateMailMessage();
message.To.Add(employee.Email);
message.Subject = "Уведомление о задаче";
message.Body = string.Format(
    "Вам назначена задача: {0}\nСсылка: {1}",
    task.Subject,
    Hyperlinks.Get(task));
Mail.Send(message);
```

---

## 14. Логирование

```csharp
// Уровни логирования.
Logger.Debug("Отладочное сообщение");
Logger.DebugFormat("Документ {0}, ID={1}", document.Name, document.Id);
Logger.Error("Ошибка при обработке");
Logger.ErrorFormat("Ошибка для документа {0}: {1}", document.Id, ex.Message);

// В асинхронных обработчиках — используйте args.RetryReason вместо Logger.
args.RetryReason = "Entity is locked by another process";
```

---

## 15. Создание индекса при инициализации

```csharp
// В ModuleInitializer.cs.
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  CreateCustomIndices();
}

public static void CreateCustomIndices()
{
  var tableName = "Sungero_Content_EDoc";
  var indexName = "idx_EDoc_Disc_Created";
  var query = string.Format(
      "CREATE INDEX IF NOT EXISTS {1} ON {0} (Discriminator, Created)",
      tableName, indexName);

  using (var connection = SQL.CreateConnection())
  using (var command = connection.CreateCommand())
  {
    command.CommandText = query;
    command.ExecuteNonQuery();
  }
}
```

---

## 16. Транзакции в фоновом процессе

```csharp
public virtual void ProcessItems()
{
  var itemIds = GetPendingItemIds();

  foreach (var itemId in itemIds)
  {
    var success = Transactions.Execute(() =>
    {
      // Внутри транзакции — заново получаем сущность по ID.
      var item = MyEntities.Get(itemId);
      if (item == null) return;

      item.Status = Status.Processed;
      item.Save();
    });

    if (!success)
      Logger.ErrorFormat("Failed to process item {0}", itemId);
  }
}
```

---

## 17. Изолированная функция со Stream

```csharp
// Изолированная функция.
[Public]
public Stream ProcessDocument(Stream inputStream, string options)
{
  var processor = new ThirdPartyLibrary.Processor();
  return processor.Process(inputStream, options);
}

// Вызов.
using (var input = version.Body.Read())
using (var result = Sungero.MyModule.IsolatedFunctions.MyArea.ProcessDocument(input, "options"))
{
  version.Body.Write(result);
  document.Save();
}
```

---

## 18. StateView для отображения статуса

```csharp
public StateView GetDocumentStateView()
{
  var stateView = StateView.Create();

  if (_obj.LifeCycleState == LifeCycleState.Active)
  {
    var block = stateView.AddBlock();
    block.AssignIcon(StateBlockIconType.OftenUsed, StateBlockIconSize.Large);
    var style = StateBlockLabelStyle.Create();
    style.FontWeight = FontWeight.Bold;
    block.AddLabel("Документ активен", style);
    block.AddLineBreak();
    block.AddLabel(string.Format("Создан: {0}", _obj.Created.Value.ToShortDateString()));
  }
  else
  {
    stateView.AddDefaultLabel("Документ не активен");
  }

  return stateView;
}
```

---

## Рецепты из Production (Targets/OmniApplied)

### 19. WebAPI endpoint с типизированным ответом
```csharp
// Паттерн ICommonResponse (лучше чем JSON строки)
[Public(WebApiRequestType = RequestType.Post)]
public Structures.Module.ICommonResponse SaveData(string requestData)
{
  var response = Structures.Module.CommonResponse.Create();
  response.IsSuccess = true;
  try { /* логика */ }
  catch (Exception ex) { response.IsSuccess = false; response.Message = ex.Message; }
  return response;
}
```
Reference: `targets/CODE_PATTERNS_CATALOG.md` секция 2

### 20. Fan-out AsyncHandler
```csharp
// Родительский АО запускает дочерние для каждого элемента
public virtual void ProcessAll(/* params */)
{
  var items = GetItemsToProcess();
  foreach (var item in items)
    AsyncHandlerInvokeProxy.ProcessSingle(item.Id);
}
```
Reference: `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleAsyncHandlers.cs`

### 21. ExternalLink для предопределенных записей
```csharp
// Идемпотентное создание справочника через ExternalLink (не Sid!)
var link = Sungero.Docflow.PublicFunctions.Module.GetExternalLink(typeof(IPeriod), periodGuid);
if (link != null) return; // уже создан
var period = Periods.Create();
period.Name = name;
period.Save();
Sungero.Docflow.PublicFunctions.Module.CreateExternalLink(period, periodGuid);
```
Reference: `targets/source/DirRX.DTCommons/DirRX.DTCommons.Server/ModuleInitializer.cs`

### 22. Override метода платформенного модуля
```csharp
// ОБЯЗАТЕЛЬНО: base.Method() первой строкой!
public override void ProcessCreatedIdentityServiceUser(/* params */)
{
  base.ProcessCreatedIdentityServiceUser(/* params */);
  // Доп. логика после платформенной
  OmniIntegration.PublicFunctions.Module.Remote.CreateUserInOmni(user);
}
```
Reference: `omniapplied/source/Sungero.Omni/.../Sungero.Company/ModuleServerFunctions.cs`

---

## Антипаттерны (что НЕ делать)

| Антипаттерн | Правильный подход |
|------------|-------------------|
| `entity is IEmployee` | `Employees.Is(entity)` |
| `entity as IEmployee` | `Employees.As(entity)` |
| `DateTime.Now` | `Calendar.Now` |
| `DateTime.Today` | `Calendar.Today` |
| `new Tuple<>()` | Структуры через `Create()` |
| `catch (Exception) { }` | `catch (Exception) { throw; }` |
| Remote в CanExecute | Кэширование через Params |
| `ToList()` на больших выборках | Использовать `IQueryable<T>` |
| Изменение без блокировки | `Locks.TryLock()` → изменение → `Locks.Unlock()` |
| Тяжёлая логика в Refresh | Вынос в асинхронный обработчик |

---

*Источники: все гайды 01-21 · rx-examples · webhelp-страницы*
