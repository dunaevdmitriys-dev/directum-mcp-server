# Серверный, клиентский и разделяемый код

> Источник: `webhelp/WebClient/ru-RU/sds_vidy_koda.htm`, `sds_logika_initcializatcii_modulia.htm`

---

## Три уровня кода

В Sungero код чётко разделён по месту выполнения. Это фундаментальное ограничение платформы.

```
┌─────────────────────────────────────────┐
│           Браузер / WebClient           │
│  (отображение UI, вызов клиентских      │
│   обработчиков через HTTP-запрос)       │
└──────────────────┬──────────────────────┘
                   │ HTTP
┌──────────────────▼──────────────────────┐
│            Веб-сервер (Server)          │
│  Весь код выполняется здесь.            │
│  Client-код = запрос инициирован из UI  │
│  Server-код = фоновые задания, workflow │
└──────────────────┬──────────────────────┘
                   │ ORM / SQL
┌──────────────────▼──────────────────────┐
│         PostgreSQL / БД                 │
└─────────────────────────────────────────┘
```

---

## Серверный код (Server)

**Где выполняется:** на веб-сервере, вне UI-контекста.

**Доступно:**
- Создание, изменение, удаление сущностей (`Create()`, `Delete()`)
- Прямая работа с БД (LINQ-запросы через ORM)
- Файловая система (с ограничениями прав учётной записи сервера)
- SQL-запросы через `Sungero.Domain.Shared.SQL` (CreateConnection / GetCurrentConnection)
- Отправка email через `Sungero.Core.Mail`
- Фоновые операции

**Недоступно:**
- Показ диалогов (`Dialogs.CreateTaskDialog`, `Dialogs.ShowMessage`)
- Управление формой (`IsVisible`, `IsRequired`)

**Примеры серверного кода:**

```csharp
// Серверная функция — создать поручение по документу.
[Public]
public static ITask CreateAssignmentTask(IEmployee performer, IOfficialDocument document)
{
  var task = Sungero.Workflow.Tasks.Create();
  task.Subject = string.Format("Ознакомиться с документом: {0}", document.Name);
  task.Deadline = Calendar.Today.AddWorkingDays(performer, 3);
  task.Attachments.Add(document);
  task.Save();
  return task;
}
```

```csharp
// Remote-метод: серверная функция, вызываемая из клиента.
[Remote(IsPure = true), Public]
public static List<IEmployee> GetDepartmentEmployees(int departmentId)
{
  return Sungero.Company.Employees.GetAll(e =>
    e.Department.Id == departmentId &&
    e.Status == Sungero.CoreEntities.DatabookEntry.Status.Active)
    .ToList();
}
```

**Рекомендация:** объединяйте несколько мелких серверных функций в одну, чтобы минимизировать количество HTTP-обращений к серверу.

---

## Клиентский код (Client)

**Где выполняется:** на сервере, но в контексте запроса от пользователя (из UI).

**Доступно:**
- Диалоги (`Dialogs.ShowMessage`, `Dialogs.CreateInputDialog`)
- Управление состоянием формы (`IsVisible`, `IsEnabled`, `IsRequired`)
- Добавление сообщений (`AddError`, `AddWarning`, `AddInformation`)
- Вызов `[Remote]`-методов (серверных из клиентского кода)

**Недоступно:**
- Прямое создание/удаление сущностей (используйте `[Remote]`-методы)
- Прямые SQL-запросы

**Типичные события клиентского кода:**

| Событие | Когда срабатывает |
|---------|------------------|
| `Showing` | При открытии формы карточки |
| `Refresh` | При обновлении формы |
| `BeforeExecute` | Перед выполнением действия |
| `PropertyChanged` (клиентский) | При изменении значения в контроле |

```csharp
// Клиентский обработчик события "открытие формы".
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);

  // Скрыть поле "Причина отклонения" на новых записях.
  if (_obj.State.IsInserted)
  {
    _obj.State.Properties.RejectReason.IsVisible = false;
    _obj.State.Properties.RejectReason.IsRequired = false;
  }

  // Показать предупреждение, если срок истёк.
  if (_obj.Deadline.HasValue && _obj.Deadline < Calendar.Today)
  {
    e.AddWarning("Срок выполнения истёк.");
  }
}
```

```csharp
// Вызов серверного метода из клиентского кода.
public virtual void CreateTaskButtonExecute(Sungero.Domain.Client.ExecuteActionArgs e)
{
  // Вызов [Remote]-метода.
  var employees = Functions.Module.Remote.GetDepartmentEmployees(_obj.Department.Id);

  // Показать диалог выбора.
  var dialog = Dialogs.CreateSelectDialog<IEmployee>(
    "Выберите исполнителя", employees);
  if (dialog.Show())
  {
    var task = Functions.Module.Remote.CreateAssignmentTask(dialog.Selected, _obj);
    task.ShowCard();
  }
}
```

---

## Разделяемый код (Shared)

**Где выполняется:** доступен в обоих контекстах (server и client).

**Доступно:**
- Вычисления, не требующие БД и диалогов
- Работа с данными загруженного объекта (`_obj.Property`)
- Форматирование строк, дат
- Константы, перечисления

**Недоступно (ограничения и сервера и клиента одновременно):**
- Диалоги
- Прямые запросы к БД
- Файловая система

**Типичные обработчики Shared:**

| Обработчик | Когда |
|-----------|-------|
| `PropertyValueChanged` | При изменении значения свойства |
| `CollectionItemAdding` | При добавлении элемента в коллекцию |
| `CollectionItemDeleted` | При удалении элемента из коллекции |

```csharp
// Shared-обработчик: пересчёт суммы при изменении количества или цены.
public virtual void QuantityChanged(Sungero.Domain.Shared.IntegerPropertyChangedEventArgs e)
{
  if (_obj.Quantity.HasValue && _obj.Price.HasValue)
    _obj.TotalAmount = _obj.Quantity.Value * _obj.Price.Value;
}

public virtual void PriceChanged(Sungero.Domain.Shared.DoublePropertyChangedEventArgs e)
{
  if (_obj.Quantity.HasValue && _obj.Price.HasValue)
    _obj.TotalAmount = _obj.Quantity.Value * _obj.Price.Value;
}
```

---

## Инициализация модуля

Метод `Initializing` вызывается при первом запуске или после обновления модуля. Используется для создания предустановленных данных.

```csharp
/// <summary>
/// Обработчик инициализации модуля.
/// </summary>
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  // Создать стандартные данные.
  CreateDefaultPurchaseKinds();
  CreateDocumentTypes();
  GrantDefaultRights();
}

/// <summary>
/// Создать виды закупок по умолчанию.
/// </summary>
public static void CreateDefaultPurchaseKinds()
{
  InitializationLogger.Debug("Init: Create default purchase kinds.");
  CreatePurchaseKind("Закупка у единственного поставщика");
  CreatePurchaseKind("Закупка по прямому договору");
  CreatePurchaseKind("Конкурентные переговоры");
}

/// <summary>
/// Создать вид закупки (если не существует).
/// </summary>
public static void CreatePurchaseKind(string name)
{
  InitializationLogger.DebugFormat("Init: Create purchase kind '{0}'", name);

  // Паттерн CreateIfNotExists: не дублировать при повторной инициализации.
  var existing = PurchaseKinds.GetAll()
    .Where(p => Equals(p.Name, name))
    .FirstOrDefault();

  if (existing != null)
    return;

  var kind = PurchaseKinds.Create();
  kind.Name = name;
  kind.Save();
}
```

**Ключевой паттерн:** всегда проверяйте существование записи перед созданием (`CreateIfNotExists`). Инициализация может запускаться повторно при обновлении.

---

## Атрибуты функций

| Атрибут | Значение |
|---------|----------|
| `[Public]` | Функция доступна из других модулей |
| `[Remote]` | Серверная функция, вызываемая из клиентского кода |
| `[Remote(IsPure = true)]` | Remote без побочных эффектов (только чтение) |
| `[Obsolete]` | Устаревший метод |

```csharp
// Функция доступна извне + вызываемая из клиента (без изменений данных).
[Remote(IsPure = true), Public]
public static IEmployee GetCurrentEmployee()
{
  return Sungero.Company.Employees.GetAll(e =>
    e.Login != null &&
    e.Login.Id == Users.Current.Id)
    .FirstOrDefault();
}
```

---

## Вызов Remote-метода: паттерн

```csharp
// Из клиентского обработчика:
var result = Functions.Module.Remote.MyServerFunction(param1, param2);

// Из другого модуля (если функция [Public]):
var result = OtherModule.PublicFunctions.Module.Remote.MyServerFunction(param);

// Из серверного кода (Remote не нужен — вызываем напрямую):
var result = Functions.Module.MyServerFunction(param1, param2);
```

---

## Логирование

```csharp
// Стандартный логгер (в серверных функциях):
Logger.Debug("Начинаю обработку документа.");
Logger.DebugFormat("Документ ID={0}, имя='{1}'", document.Id, document.Name);
Logger.Error("Ошибка при обработке.", exception);

// В инициализации — специальный логгер:
InitializationLogger.Debug("Init: Создаю стандартные данные.");
```

---

## Работа с SQL (серверный код)

Прямые SQL-запросы выполняются через класс `Sungero.Domain.Shared.SQL`:

```csharp
// Получить текущее соединение (в рамках транзакции).
using (var command = SQL.GetCurrentConnection().CreateCommand())
{
  command.CommandText = "SELECT COUNT(*) FROM ...";
  var count = (long)command.ExecuteScalar();
}

// Создать новое соединение (вне транзакции, для длительных операций).
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  SQL.AddParameter(command, "@id", System.Data.DbType.Int64, entityId);
  command.CommandText = "UPDATE ... WHERE Id = @id";
  command.ExecuteNonQuery();
}
```

> **Важно:** `SQL.GetCurrentConnection()` работает в контексте текущей ORM-транзакции. `SQL.CreateConnection()` — новое соединение вне транзакции.

---

## Исключения: AppliedCodeException

Для корректного выброса ошибок из прикладного кода используйте `AppliedCodeException`:

```csharp
throw new AppliedCodeException("Невозможно обработать документ: отсутствует вложение.");
```

Платформа перехватывает это исключение и показывает пользователю понятное сообщение вместо стектрейса.

---

## Async Handlers (краткая справка)

Асинхронные обработчики выполняются вне HTTP-запроса на отдельном сервисе. Определяются в модуле и запускаются из серверного кода:

```csharp
// Серверный код: создать и запустить асинхронный обработчик.
var asyncHandler = AsyncHandlers.MyHandler.Create();
asyncHandler.DocumentId = document.Id;
asyncHandler.ExecuteAsync();
```

```csharp
// Реализация (файл ModuleAsyncHandlers.cs):
public virtual void MyHandler(Sungero.Module1.Server.AsyncHandlerInvokeArgs.MyHandlerInvokeArgs args)
{
  var documentId = args.DocumentId;
  // Обработка...
  args.Retry = false; // Не повторять при ошибке.
}
```

Подробнее: см. **гайд 14 (background_async.md)**.

---

## Параметры передачи данных между событиями (Params)

```csharp
// Установить параметр (в любом обработчике).
e.Params.AddOrUpdate("MyFlag", true);

// Прочитать параметр (в другом обработчике той же сущности).
bool myFlag;
if (e.Params.TryGetValue("MyFlag", out myFlag) && myFlag)
{
  // Логика...
}
```

Params — словарь `string → object`, доступный во всех событиях одного запроса. Используется для передачи флагов между BeforeSave → Saving → Saved, между Showing → Refresh и т.д.

---

## CallContext — определение источника вызова

```csharp
// Проверить: была ли карточка открыта из справочника "Подразделения".
if (CallContext.CalledFrom(Departments.Info))
{
  _obj.State.Properties.Department.IsVisible = false;
}
```

---

*Источники: sds_vidy_koda.htm · sds_logika_initcializatcii_modulia.htm · sds_server_functions.htm · om_blokirovki.htm · sds_sobytiia_sushchnosti.htm*
