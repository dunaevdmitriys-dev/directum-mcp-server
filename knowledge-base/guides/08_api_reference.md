# API Reference: ключевые классы и методы

> Источник: XML API docs из `developmentstudio/AddIns/Sungero/Libraries/`

---

## Sungero.Core.AccessRights

**Сборка:** `Sungero.Domain`
**Доступность:** только серверный код

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `AllowRead` | `static void AllowRead(Action executor)` | Выполнить с правами на просмотр |
| `SuppressSecurityEvents` | `static void SuppressSecurityEvents(Action executor)` | Выполнить без логирования безопасности |
| `CopyAsync` | `static Guid CopyAsync(IUser fromUser, IUser toUser, bool delegateStrictRights)` | Запустить копирование прав. Возвращает ID процесса |
| `CancelCopy` | `static void CancelCopy(Guid processId)` | Отменить копирование прав |
| `CopyingStatus` | `static string CopyingStatus(Guid processId)` | Получить статус копирования |

---

## Sungero.Core.Cache

**Сборка:** `Sungero.Domain`

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `AddOrUpdate<T>` | `static void AddOrUpdate(string key, T value, DateTime deathDate)` | Добавить или обновить значение в кэше |
| `TryGetValue<T>` | `static bool TryGetValue(string key, out T value)` | Получить значение из кэша. Возвращает `true` если найдено |
| `Remove` | `static void Remove(string key)` | Удалить из кэша |

```csharp
// Добавить в кэш на 10 минут.
Sungero.Core.Cache.AddOrUpdate("key", value, Calendar.Now.AddMinutes(10));

// Получить из кэша.
string cached;
if (Sungero.Core.Cache.TryGetValue("key", out cached))
{
  Logger.Debug("Из кэша: " + cached);
}
```

---

## Sungero.Core.Mail

**Сборка:** `Sungero.Domain`

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `CreateMailMessage` | `static IEmailMessage CreateMailMessage()` | Создать email-сообщение |
| `Send` | `static void Send(IEmailMessage message)` | Отправить сообщение |

```csharp
var message = Sungero.Core.Mail.CreateMailMessage();
message.To.AddRange(recipients.Select(r => r.Email));
message.Subject = "Уведомление о задаче";
message.Body = "Вам назначена задача: " + task.Subject;
Sungero.Core.Mail.Send(message);
```

---

## Sungero.Core.IdentityService

**Сборка:** `Sungero.Domain`

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `GetServiceUserToken` | `static string GetServiceUserToken()` | Токен сервисного пользователя для IDS |
| `GetUserToken` | `static string GetUserToken(string loginName, string password, string audience)` | Токен пользователя для IDS |

---

## Sungero.Core.Calendar

**Сборка:** `Sungero.Domain`
Работа с датами с учётом рабочего времени.

| Метод/Свойство | Описание |
|----------------|----------|
| `Calendar.Now` | Текущее дата+время |
| `Calendar.Today` | Текущая дата |
| `date.AddWorkingDays(employee, n)` | Добавить N рабочих дней с учётом календаря сотрудника |
| `date.IsWorkingDay(employee)` | Является ли дата рабочим днём |
| `Calendar.GetWorkingTime(employee, from, to)` | Рабочее время в интервале |

```csharp
// Срок: 5 рабочих дней от сегодня.
var deadline = Calendar.Today.AddWorkingDays(employee, 5);

// Проверить: рабочий ли день.
if (!Calendar.Today.IsWorkingDay(employee))
  Logger.Debug("Сегодня выходной.");
```

---

## Sungero.Core.Logger

**Сборка:** `Sungero.Domain`

```csharp
Logger.Debug("Сообщение отладки");
Logger.DebugFormat("Документ ID={0}", document.Id);
Logger.Info("Информационное сообщение");
Logger.Warn("Предупреждение");
Logger.Error("Ошибка", exception);
Logger.Fatal("Критическая ошибка", exception);
```

---

## Sungero.Core.SchemeBlocks

**Сборка:** `Sungero.Domain`

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Get` | `static ISchemeBlock Get(IScheme scheme, string blockId)` | Блок по ID |
| `GetAll` | `static IEnumerable<ISchemeBlock> GetAll(IScheme scheme)` | Все блоки схемы |
| `Is` | `static bool Is(ISchemeBlock block)` | Проверить тип блока |
| `As<T>` | `static T As<T>(ISchemeBlock block)` | Привести к типу |

---

## Sungero.Core.ScriptSchemeBlocks

**Сборка:** `Sungero.Domain`
**Доступность:** только серверный код

| Метод | Описание |
|-------|----------|
| `Get(IScheme, string blockId)` | Блок «Скрипт» по ID |
| `GetAll(IScheme)` | Все блоки «Скрипт» в схеме |
| `Is(ISchemeBlock)` | Проверить что блок — скрипт |
| `As(ISchemeBlock)` | Привести к `IScriptSchemeBlock` |

**Свойства IScriptSchemeBlock:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Id` | `long` | Идентификатор блока (read-only) |
| `Description` | `string` | Описание блока |
| `ExecutionResult` | `Enumeration` | Результат выполнения (`Success` или кастомный) |

---

## Sungero.Workflow.Blocks.AssignmentBlockWrapper

**Сборка:** `Sungero.Workflow.Server`

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Id` | `long` | ID блока (read-only) |
| `Description` | `string` | Описание (read-only) |
| `Instruction` | `string` | Инструкция для исполнителя |
| `AbsoluteDeadline` | `DateTime?` | Срок задания |
| `AbsoluteStopAssignmentsDeadline` | `DateTime?` | Срок остановки |
| `DefaultViewForm` | `IFormView` | Форма отображения карточки |
| `EmbeddedViewForm` | `IFormView` | Форма в списке |
| `IsCompetitive` | `bool` | Конкурентное задание |
| `CompetitivePerformer` | `IRecipient` | Исполнитель конкурентного задания |
| `NoPerformersResult` | `Enumeration` | Результат при отсутствии исполнителей |

---

## Sungero.Workflow.Blocks.TaskBlockWrapper

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Author` | `IUser` | Автор/инициатор |
| `MaxDeadline` | `DateTime?` | Максимальный срок задачи |
| `WaitForCompletion` | `bool` | Ждать завершения задачи |
| `DefaultViewForm` | `IFormView` | Форма карточки |
| `EmbeddedViewForm` | `IFormView` | Форма в списке |

---

## Sungero.Workflow.Blocks.BlockRetrySettings

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Retry` | `bool` | Повторить выполнение блока при ошибке |
| `RetryIteration` | `int` | Счётчик попыток |

---

## Sungero.Core.BackgroundJobExecutionStatus (Enum)

| Значение | Описание |
|----------|----------|
| `New` | Задание создано, не запущено |
| `Initialization` | Формирование данных |
| `Processing` | Выполняется |
| `Done` | Завершено успешно |
| `Error` | Ошибка выполнения |
| `Canceled` | Отменено |

---

## Sungero.Core.Mail.MailPriority (Enum)

| Значение | Описание |
|----------|----------|
| `Normal` | Обычный приоритет |
| `High` | Высокий приоритет |
| `Low` | Низкий приоритет |

---

## CommonLibrary.LocalizedString

**Сборка:** `Sungero.Localization`
Работа с многоязычными строками.

| Метод | Описание |
|-------|----------|
| `Append(LocalizedString)` | Объединить с другой локализованной строкой |
| `AppendFormat(string format, params object[] args)` | Объединить с форматом |
| `ToString()` | Получить строку в текущей локали |

```csharp
// Использование локализованных ресурсов модуля.
var message = MyModule.Resources.DocumentProcessedFormat(document.Name);
Logger.Debug(message.ToString());
```

---

## Sungero.NoCode.Server.ComputedRole

| Метод | Сигнатура | Описание |
|-------|-----------|----------|
| `Compute` | `IEnumerable<IRecipient> Compute(IEntity entity, bool withAuthorization)` | Вычислить субъекты роли для сущности |

---

## Sungero.Core.EntitySecureLinks

| Метод | Описание |
|-------|----------|
| `GetLeadingEntity(IEntity entity)` | Получить ведущую сущность |
| `SetLeadingEntity(IEntity entity, IEntity leadingEntity)` | Установить ведущую сущность |

---

## Sungero.Core.HistoryExtensions

| Метод | Описание |
|-------|----------|
| `WhereHistory<T>(IQueryable<T>, Expression<Func<IHistory, bool>>)` | Фильтрация запроса по истории |

```csharp
// Найти документы, которые изменял конкретный пользователь.
var docs = Sungero.Docflow.OfficialDocuments.GetAll()
  .WhereHistory(h => h.UserId == userId)
  .ToList();
```

---

## Workflow Scheme Block Results

### AccessRightsSchemeBlock Actions

| Константа | Описание |
|-----------|----------|
| `Action.Add` | Добавить права |
| `Action.Set` | Установить (заменить) |
| `Action.DeleteAll` | Удалить все |

### AccessRightsSchemeBlock Types

| Константа | Описание |
|-----------|----------|
| `Type.Read` | Просмотр |
| `Type.Change` | Изменение |
| `Type.FullAccess` | Полный доступ |
| `Type.Forbidden` | Запрет |

### DecisionSchemeBlock Results

| Константа | Описание |
|-----------|----------|
| `ExecutionResult.True` | Результат «Да» |
| `ExecutionResult.False` | Результат «Нет» |

---

## Sungero.Domain.Shared.SQL

**Сборка:** `Sungero.Domain.Shared`
**Доступность:** серверный код

| Метод | Описание |
|-------|----------|
| `SQL.CreateConnection()` | Создать новое соединение (вне транзакции) |
| `SQL.GetCurrentConnection()` | Получить соединение текущей ORM-транзакции |
| `SQL.AddParameter(cmd, name, type, value)` | Добавить параметр к DbCommand |
| `SQL.BulkCopy(dataTable, tableName)` | Массовая вставка данных |

```csharp
// Новое соединение (для длительных операций, вне транзакции).
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  SQL.AddParameter(command, "@docId", System.Data.DbType.Int64, documentId);
  command.CommandText = "UPDATE MyTable SET Processed = 1 WHERE DocId = @docId";
  command.ExecuteNonQuery();
}

// В рамках текущей транзакции.
using (var command = SQL.GetCurrentConnection().CreateCommand())
{
  command.CommandText = "SELECT COUNT(*) FROM Sungero_Docflow_OfficialDocument WHERE Id > 0";
  var count = (long)command.ExecuteScalar();
}
```

---

## Sungero.Domain.Shared.Locks

**Сборка:** `Sungero.Domain.Shared`

| Метод | Описание |
|-------|----------|
| `Lock(IEntity)` | Заблокировать сущность. Бросает исключение если уже заблокирована |
| `Lock(IBinaryData)` | Заблокировать бинарные данные (тело документа) |
| `TryLock(IEntity)` | Попытаться заблокировать. Возвращает `bool` |
| `TryLock(IBinaryData)` | Попытаться заблокировать бинарные данные |
| `Unlock(IEntity)` | Разблокировать сущность |
| `Unlock(IBinaryData)` | Разблокировать бинарные данные |
| `GetLockInfo(IEntity)` | Информация о блокировке |

**Свойства LockInfo:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| `IsLocked` | bool | Заблокирована ли сущность |
| `IsLockedByMe` | bool | Заблокирована текущим пользователем |
| `OwnerName` | string | Имя пользователя, заблокировавшего |
| `LockTime` | DateTime? | Время установки блокировки |

---

## Sungero.Core.Dialogs

**Сборка:** `Sungero.Domain`
**Доступность:** только клиентский код

| Метод | Описание |
|-------|----------|
| `Dialogs.ShowMessage(text, description, type, title)` | Показать сообщение. MessageType: Information, Warning, Error, Question |
| `Dialogs.CreateInputDialog(title)` | Создать диалог ввода |
| `Dialogs.CreateSelectDialog<T>(title, source)` | Диалог выбора сущности |
| `Dialogs.CreateTaskDialog(title)` | Диалог задачи |
| `Dialogs.CreateConfirmDialog(text)` | Диалог подтверждения Yes/No |

```csharp
// Диалог ввода.
var dialog = Dialogs.CreateInputDialog("Параметры");
var dateFrom = dialog.AddDate("Дата от", true);
var dateTo = dialog.AddDate("Дата до", true);
var employee = dialog.AddSelect("Сотрудник", false, Sungero.Company.Employees.Null);

if (dialog.Show() == DialogButtons.Ok)
{
  var from = dateFrom.Value;
  var to = dateTo.Value;
  var emp = employee.Value;
}
```

---

## Sungero.Core.Reports

**Сборка:** `Sungero.Domain`

| Метод | Описание |
|-------|----------|
| `Reports.<ReportName>.Open()` | Открыть отчёт в браузере |
| `Reports.<ReportName>.Export(format)` | Экспорт в поток (PDF, DOCX, XLSX) |
| `Reports.<ReportName>.ExportTo(stream, format)` | Экспорт в конкретный поток |

```csharp
// Открыть отчёт (клиентский код).
var report = Reports.GetMyReport();
report.Employee = employee;
report.Period = period;
report.Open();

// Экспорт в PDF (серверный код).
using (var stream = report.Export(ReportFormat.Pdf))
{
  // Работа с потоком...
}
```

---

## AppliedCodeException

**Сборка:** `Sungero.Domain`

Базовое исключение для прикладного кода. Платформа показывает пользователю текст ошибки вместо стектрейса:

```csharp
throw new AppliedCodeException("Документ не может быть обработан.");
```

---

## Sungero.Core.Hyperlinks

**Сборка:** `Sungero.Domain`

| Метод | Описание |
|-------|----------|
| `Hyperlinks.Get(entity)` | Гиперссылка на сущность |
| `Hyperlinks.Get(repository, entityId)` | Гиперссылка по типу и ID |
| `Hyperlinks.Get(url, text)` | Гиперссылка на внешний URL |

```csharp
// Гиперссылка на документ (для вставки в текст инструкции, StateView и т.д.).
var link = Hyperlinks.Get(Sungero.Docflow.OfficialDocuments, documentId);
```

---

## Sungero.Core.Params (параметры между событиями)

**Сборка:** `Sungero.Domain`

Передача данных между событиями одной сущности в рамках одного запроса:

```csharp
// Установить.
e.Params.AddOrUpdate("MyKey", someValue);

// Прочитать.
int value;
if (e.Params.TryGetValue("MyKey", out value))
  Logger.DebugFormat("Значение: {0}", value);
```

---

## Sungero.Core.Users / Roles

**Сборка:** `Sungero.Domain`

```csharp
Users.Current            // Текущий пользователь
Users.Current.Id         // ID текущего пользователя
Roles.AllUsers           // Роль «Все пользователи»
Roles.Administrators     // Роль «Администраторы»
```

---

*Источники: Sungero.Domain.xml · Sungero.Workflow.Server.xml · Sungero.NoCode.Server.xml · Sungero.CoreEntities.Server.xml · Sungero.Localization.xml · om_blokirovki.htm · sds_sobytiia_sushchnosti.htm*
