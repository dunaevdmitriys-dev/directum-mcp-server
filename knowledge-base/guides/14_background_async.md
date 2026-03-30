# Фоновые процессы и асинхронные обработчики

> Источник: `om_fonovye_processu.htm`, `om_executeasync.htm`, `ds_baseeditor_jobstab.htm`, `ds_baseeditor_asynchandlerstab.htm`, `ds_redaktor_async_obrabotchika.htm`, `sds_optimizaciya_processov.htm`, `Sungero.Domain.Shared.xml`

---

## Обзор

Платформа предоставляет два механизма для выполнения тяжёлых операций вне основного потока запроса:

| Механизм | Где выполняется | Запуск | Когда использовать |
|----------|----------------|--------|-------------------|
| Фоновый процесс (Job) | Worker | По расписанию или `Enqueue()` | Периодические задачи: индексация, рассылки, очистка |
| Асинхронный обработчик | Worker | `ExecuteAsync()` из серверного кода | Разовые тяжёлые операции: конвертация PDF, выдача прав |

**Важно:** оба механизма выполняются на сервисе Worker (сервис асинхронных событий), а не на веб-сервере. Таймаут веб-сервера (5 минут) к ним не применяется.

---

## Фоновые процессы (Jobs)

### Создание в DDS

1. Открыть редактор модуля → вкладка «Фоновые процессы»
2. Нажать кнопку создания → откроется редактор процесса
3. Вкладка «Основное» → задать имя и параметры
4. Вкладка «Обработчики» → написать код события «Выполнение»

### Структура кода

```csharp
// Файл: ModuleJobs.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.Module1.Server
{
  public partial class ModuleJobs
  {
    /// <summary>
    /// Описание задачи.
    /// </summary>
    public virtual void Job()
    {
      // Логика фонового процесса.
    }
  }
}
```

### Метаданные в Module.mtd

```json
"Jobs": [
  {
    "NameGuid": "aa456c28-b932-41d3-82c8-7dc3e1917f2f",
    "Name": "Job",
    "GenerateHandler": true,
    "MonthSchedule": "Monthly",
    "StartAt": "1753-01-01T08:30:00"
  }
]
```

### Запуск вне расписания

```csharp
// Программный запуск фонового процесса.
Jobs.SyncCounterparties.Enqueue();
```

**Важно:** если администратор отключил процесс — вызов `Enqueue()` не выполнит его.

### Перекрытие стандартных процессов

При перекрытии модуля можно изменить:
- Отображаемое имя и описание
- Расписание
- Обработчик события «Выполнение»
- Отключить процесс

### Стандартные фоновые процессы

**Модуль Docflow:**

| Процесс | Назначение |
|---------|-----------|
| `GrantAccessRightsToDocuments` | Автоматическая выдача прав на документы |
| `IndexDocumentsForFullTextSearch` | Индексация для полнотекстового поиска |
| `SendMailNotification` | Email-уведомления о заданиях |
| `SendSummaryMailNotifications` | Сводные email-уведомления |
| `TransferDocumentsByStoragePolicy` | Перемещение тел документов в хранилище |
| `DeleteComparisonInfos` | Удаление результатов сравнения документов |
| `SyncFormalizedPowerOfAttorneyState` | Синхронизация статусов МЧД |

**Модуль Company:**

| Процесс | Назначение |
|---------|-----------|
| `DeleteObsoleteSystemSubstitutions` | Удаление устаревших системных замещений |
| `TransferSubstitutedAccessRights` | Копирование прав руководителю |

### Использование транзакций в фоновых процессах

```csharp
public virtual void ProcessMessages()
{
  var queueItemsIds = queueItems.Select(q => q.Id).ToList();

  foreach (var message in messages)
  {
    // Каждое сообщение обрабатывается в отдельной транзакции.
    Transactions.Execute(() =>
    {
      // ВАЖНО: внутри транзакции заново получаем сущности по ID.
      var transactionQueueItems = ExchangeCore.MessageQueueItems
        .GetAll(q => queueItemsIds.Contains(q.Id))
        .ToList();

      if (this.ProcessMessage(client, transactionQueueItems, message))
      {
        var queueItem = queueItems
          .Single(x => x.ExternalId == message.ServiceMessageId);
        queueItem.ProcessingStatus =
          ExchangeCore.MessageQueueItem.ProcessingStatus.Processed;
        queueItem.Save();
      }
    });
  }
}
```

**Ключевые правила транзакций:**
- `Transactions.Execute(Action)` возвращает `bool` — успешность
- Внутрь транзакции можно передавать только простые типы (ID, строки)
- Для передачи сущности — передавайте её `Id`, затем получайте заново внутри транзакции
- При ошибке внутри транзакции — все операции откатываются

---

## Асинхронные обработчики (AsyncHandlers)

### Концепция

1. Серверный код создаёт обработчик, заполняет параметры, вызывает `ExecuteAsync()`
2. Обработчик передаётся на Worker
3. Worker выполняет вычисления в отдельном потоке
4. При ошибке — автоматический повтор по настроенному расписанию

### Создание в DDS

1. Редактор модуля → вкладка «Асинхронные обработчики» → создать
2. Вкладка «Основное» → задать имя и параметры
3. Вкладка «Обработчики» → написать код события «Выполнение»

### Структура кода

```csharp
// Файл: ModuleAsyncHandlers.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace Sungero.Module1.Server
{
  public partial class ModuleAsyncHandlers
  {
    // Обработчики определяются здесь.
  }
}
```

### Параметры обработчика

**Допустимые типы параметров:**

| Тип | Описание |
|-----|----------|
| `String` | Строка |
| `Integer` | Целое число |
| `Date` | Дата |
| `Boolean` | Логическое значение |
| `Identifier` | Идентификатор (long) |
| `Decimal` | Десятичное число |

**Ограничение:** бинарные данные нельзя передавать в параметрах — это создаёт большую нагрузку на Worker. Для работы с файлами используйте потоки.

### Паттерн вызова

```csharp
// Создание и вызов асинхронного обработчика.
var asyncHandler = Docflow.AsyncHandlers.GrantAccessRightsToDocument.Create();
asyncHandler.DocumentId = _obj.Id;
asyncHandler.ExecuteAsync();
```

### Полный пример: вызов из AfterSave

```csharp
public override void AfterSave(Sungero.Domain.AfterSaveEventArgs e)
{
  // Создать асинхронный обработчик.
  var asyncRightsHandler = Docflow.AsyncHandlers.GrantAccessRightsToDocument.Create();

  // Заполнить параметры.
  asyncRightsHandler.DocumentId = _obj.Id;

  // Вызвать.
  asyncRightsHandler.ExecuteAsync();
}
```

### Обработчик с механизмом повтора

```csharp
public virtual void GrantAccessRightsToDocument(
    Sungero.Docflow.AsyncHandlers.GrantAccessRightsToDocumentInvokeArgs args)
{
  long documentId = args.DocumentId;
  long ruleId = args.RuleId;

  Logger.DebugFormat("GrantRights: start for document {0}, rule {1}",
      documentId, ruleId);

  var isGranted = Docflow.Functions.Module.GrantRightsToDocument(documentId, ruleId);

  if (!isGranted)
  {
    Logger.DebugFormat("GrantRights: cannot grant for document {0}, rule {1}",
        documentId, ruleId);
    args.Retry = true;  // Запланировать повтор.
  }
  else
  {
    Logger.DebugFormat("GrantRights: success for document {0}, rule {1}",
        documentId, ruleId);
  }
}
```

### Аргументы обработчика (InvokeArgs)

| Аргумент | Тип | Описание |
|----------|-----|----------|
| `args.Retry` | `bool` | Установить `true` для повторного выполнения |
| `args.RetryIteration` | `int` | Текущий номер повтора |
| `args.MaxRetryCount` | `int` | Максимальное число повторов |
| `args.NextRetryTime` | `DateTime?` | Дата/время следующего повтора |
| `args.RetryReason` | `string` | Причина повтора (пишется в лог вместо `DebugFormat`) |

### Настройки повторов

**Вариант 1 — «С увеличивающимся интервалом до 1 часа, потом 2»:**
- Первый час: интервал экспоненциально растёт
- После 1 часа: каждый час
- После 12 часов: каждые 2 часа
- По умолчанию: останавливается после 50 попыток

**Вариант 2 — «С равными интервалами»:**
- Интервал задаётся в минутах (свойство «Период (в минутах)»)
- По умолчанию: каждые 15 минут, максимум 100 попыток

### Обработка необработанных исключений

Если в обработчике возникает unhandled exception:
- Обработчик автоматически уходит на повтор
- В логе: `systemRetry = true`, `retryReason` = класс исключения + сообщение
- Значение `args.RetryReason`, установленное разработчиком, **НЕ записывается**

### Транзакции в асинхронных обработчиках

Можно использовать `Transactions.Execute()`, но:
- При неудаче транзакции обработчик **НЕ уходит на повтор автоматически**
- Нужна программная проверка успешности:

```csharp
var success = Transactions.Execute(() =>
{
  // Операции внутри транзакции.
  entity.Property = newValue;
  entity.Save();
});

if (!success)
{
  args.Retry = true;
  args.RetryReason = "Transaction failed";
}
```

---

## ExecuteAsync — все перегрузки

### Для одного обработчика

```csharp
handler.ExecuteAsync();
handler.ExecuteAsync(completedNotification);
handler.ExecuteAsync(completedNotification, timeout);
handler.ExecuteAsync(completedNotification, user);
handler.ExecuteAsync(completedNotification, user, timeout);
handler.ExecuteAsync(startedNotification, completedNotification);
handler.ExecuteAsync(startedNotification, completedNotification, timeout);
handler.ExecuteAsync(startedNotification, completedNotification, user);
handler.ExecuteAsync(startedNotification, completedNotification, user, timeout);
handler.ExecuteAsync(startedNotification, completedNotification, errorNotification, user);
handler.ExecuteAsync(startedNotification, completedNotification, errorNotification, user, timeout);
```

### Для коллекции обработчиков

```csharp
handlers.ExecuteAsync(startedNotification, completedNotification, errorNotification, user);
```

### Параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| `startedNotification` | `string` | Сообщение при запуске |
| `completedNotification` | `string` | Сообщение при успешном завершении |
| `errorNotification` | `string` | Сообщение при ошибке |
| `user` | `IUser` | Пользователь для уведомления |
| `timeout` | `TimeSpan` | Время ожидания синхронного завершения (по умолчанию 2 сек) |

### Пример: несколько обработчиков с уведомлениями

```csharp
var handlers = new List<IAsyncHandler>();
var handler1 = ConvertPdfAsyncHandler.Create();
var handler2 = ConvertPdfAsyncHandler.Create();
handlers.Add(handler1);
handlers.Add(handler2);

var startMsg = "Конвертация документов началась.";
var completeMsg = string.Format(
    "Конвертация завершена: {0}, {1}",
    Hyperlinks.Get(document1),
    Hyperlinks.Get(document2));
var errorMsg = string.Format(
    "Конвертация завершена с ошибкой: {0}, {1}",
    Hyperlinks.Get(document1),
    Hyperlinks.Get(document2));

handlers.ExecuteAsync(startMsg, completeMsg, errorMsg, Users.Current);
```

### Управление таймаутом

```csharp
// Отключить ожидание (fire and forget).
handler.ExecuteAsync("message", user, TimeSpan.Zero);

// Пользовательский таймаут 500 мс.
handler.ExecuteAsync("message", user, TimeSpan.FromMilliseconds(500));
```

**Логика отображения сообщений:**
- Сообщение **не показывается**, если обработчик завершился быстрее таймаута
- Сообщение **не показывается**, если пользователь вышел из системы до завершения
- В текст сообщения можно добавлять гиперссылки на объекты: `Hyperlinks.Get(entity)`

---

## Паттерн: перенос тяжёлой работы в асинхронный обработчик

### Проблема
Операция в действии или событии занимает >3 сек → пользователь ждёт.

### Решение

```csharp
// Серверная функция — быстро создаёт обработчик.
[Remote]
public static void StartHeavyProcessing(long documentId)
{
  var handler = MyModule.AsyncHandlers.HeavyProcessing.Create();
  handler.DocumentId = documentId;
  handler.ExecuteAsync(
    "Обработка документа началась.",
    "Обработка документа завершена.",
    Users.Current);
}

// Асинхронный обработчик — выполняет тяжёлую работу.
public virtual void HeavyProcessing(
    Sungero.MyModule.AsyncHandlers.HeavyProcessingInvokeArgs args)
{
  var document = OfficialDocuments.Get(args.DocumentId);
  if (document == null)
    return;

  // Блокируем сущность.
  if (!Locks.TryLock(document))
  {
    args.Retry = true;
    args.RetryReason = "Document is locked";
    return;
  }

  try
  {
    // Тяжёлая работа.
    DoExpensiveWork(document);
    document.Save();
  }
  finally
  {
    Locks.Unlock(document);
  }
}
```

---

## Статусы выполнения фонового процесса

| Статус | Описание |
|--------|----------|
| `New` | Процесс создан |
| `Initialization` | Формирование данных для обработки |
| `Processing` | Процесс выполняется |
| `Done` | Успешно завершён |
| `Canceled` | Отменён |
| `Error` | Ошибка при выполнении |

---

## Стандартные асинхронные обработчики

**Модуль Commons:**
- `IndexEntity` — индексация сущности
- `RemoveEntityFromIndex` — удаление из индекса

**Модуль Docflow:**
- `GrantAccessRightsToDocument` — выдача прав на документ
- `GrantAccessRightsToDocumentsBulk` — массовая выдача прав
- `ConvertDocumentToPdf` — конвертация в PDF
- `CompareDocuments` — сравнение документов
- `IndexDocumentForFullTextSearch` — полнотекстовая индексация
- `AddRegistrationStamp` — добавление штампа регистрации

**Модуль Company:**
- `TransferEmployeeToDepartment` — перевод сотрудника в подразделение
- `UpdateEmployeeName` — обновление ФИО сотрудника
- `ConnectUsersToExternalApps` — подключение к внешним приложениям

---

## Production-паттерны AsyncHandlers (Targets/KPI)

> Reference: `targets/CODE_PATTERNS_CATALOG.md` секция 3 | `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleAsyncHandlers.cs`

### 1. Fan-out: родитель → дочерние АО

Родительский обработчик порождает дочерние через `ExecuteAsync()`. Используется, когда одна операция должна рекурсивно обработать дерево сущностей.

```csharp
// ConvertTargetsMapsDates — конвертирует даты карты целей,
// затем запускает дочерние АО для каждой вложенной карты.
public virtual void ConvertTargetsMapsDates(
    DirRX.Targets.Server.AsyncHandlerInvokeArgs.ConvertTargetsMapsDatesInvokeArgs args)
{
  var targetsMap = TargetsMaps.GetAll(m => m.Id == args.TargetsMapId).FirstOrDefault();
  if (targetsMap == null)
    return;

  // Обработать текущую карту.
  Functions.TargetsMap.ConvertDates(targetsMap);

  // Fan-out: запустить дочерние АО для вложенных карт.
  foreach (var childMap in targetsMap.ChildMaps)
  {
    var childHandler = AsyncHandlers.ConvertTargetsMapsDates.Create();
    childHandler.TargetsMapId = childMap.Id;
    childHandler.ExecuteAsync();
  }
}
```

**Когда применять:** дерево сущностей (карты целей → подкарты), каскадная выдача прав, массовая конвертация.

### 2. Отложенный retry через NextRetryTime

Вместо стандартного экспоненциального backoff — явное указание времени следующей попытки. Полезно для операций, привязанных к внешним системам или расписанию.

```csharp
// DeleteCheckFile — проверяет наличие файла-флага,
// если файл ещё нужен — повторить через 12 часов.
public virtual void DeleteCheckFile(
    DirRX.Targets.Server.AsyncHandlerInvokeArgs.DeleteCheckFileInvokeArgs args)
{
  var checkFile = CheckFiles.GetAll(f => f.Id == args.CheckFileId).FirstOrDefault();
  if (checkFile == null)
    return;

  if (!Functions.Module.CanDeleteCheckFile(checkFile))
  {
    // Отложенный retry: не раньше чем через 12 часов.
    args.Retry = true;
    args.NextRetryTime = Calendar.Now.AddHours(12);
    return;
  }

  CheckFiles.Delete(checkFile);
}
```

**Когда применять:** ожидание внешнего события, проверка по расписанию, rate-limiting внешних API.

### 3. Batch import с блокировками

Обработка записей пакетами по N штук с `Locks.TryLock/Unlock`. Если остались необработанные — `args.Retry = true` для продолжения в следующей итерации.

```csharp
// ImportActualValuesInMetric — импорт фактических значений метрик пакетами по 100.
public virtual void ImportActualValuesInMetric(
    DirRX.Targets.Server.AsyncHandlerInvokeArgs.ImportActualValuesInMetricInvokeArgs args)
{
  const int batchSize = 100;
  var metrics = Metrics.GetAll(m => m.Status == Status.PendingImport)
    .Take(batchSize)
    .ToList();

  if (!metrics.Any())
    return;

  var hasErrors = false;
  foreach (var metric in metrics)
  {
    if (!Locks.TryLock(metric))
    {
      hasErrors = true;
      continue;
    }

    try
    {
      Functions.Metric.ImportActualValues(metric);
      metric.Status = Status.Imported;
      metric.Save();
    }
    catch (Exception ex)
    {
      Logger.ErrorFormat("ImportActualValues: error for metric {0}: {1}",
        metric.Id, ex.Message);
      hasErrors = true;
    }
    finally
    {
      Locks.Unlock(metric);
    }
  }

  // Если есть ещё необработанные или были ошибки — повторить.
  var remaining = Metrics.GetAll(m => m.Status == Status.PendingImport).Any();
  if (remaining || hasErrors)
    args.Retry = true;
}
```

**Когда применять:** массовый импорт данных, пакетная обработка очередей, миграция.

### 4. Bounded retry с проверкой RetryIteration

Ограничение количества попыток через `args.RetryIteration < args.MaxRetryCount`. Защита от бесконечного цикла повторов.

```csharp
// GrantAccessForProjectPlans — выдача прав на планы проекта
// с ограниченным числом попыток.
public virtual void GrantAccessForProjectPlans(
    DirRX.Targets.Server.AsyncHandlerInvokeArgs.GrantAccessForProjectPlansInvokeArgs args)
{
  var plan = ProjectPlans.GetAll(p => p.Id == args.ProjectPlanId).FirstOrDefault();
  if (plan == null)
    return;

  var granted = Functions.ProjectPlan.TryGrantAccess(plan);
  if (!granted)
  {
    // Bounded retry: не превышать максимум попыток.
    if (args.RetryIteration < args.MaxRetryCount)
    {
      args.Retry = true;
      args.RetryReason = string.Format(
        "GrantAccess failed for plan {0}, attempt {1}/{2}",
        plan.Id, args.RetryIteration, args.MaxRetryCount);
    }
    else
    {
      Logger.ErrorFormat(
        "GrantAccessForProjectPlans: max retries reached for plan {0}", plan.Id);
    }
  }
}
```

**Когда применять:** операции с внешними зависимостями, где бесконечный retry нежелателен.

### Дополнительные паттерны из Targets

#### ExponentialDelayStrategy в MTD

В метаданных обработчика можно задать стратегию повторов с начальным периодом:

```json
{
  "NameGuid": "...",
  "Name": "ImportActualValuesInMetric",
  "DelayStrategy": "ExponentialDelay",
  "DelayPeriod": 15,
  "MaxRetryCount": 50,
  "GenerateHandler": true
}
```

`DelayPeriod: 15` — начальный интервал 15 минут, далее экспоненциальный рост до потолка платформы.

#### Named Logger

Для группировки логов асинхронных обработчиков — именованный логгер через константу модуля:

```csharp
// В начале обработчика.
var logger = Logger.WithLogger(Constants.Module.LoggerPostfix);
logger.DebugFormat("ImportActualValues: processing metric {0}", args.MetricId);
```

Позволяет фильтровать логи по `LoggerPostfix` в Kibana/Seq без пересечения с основным логом модуля.

#### RepeatedLockException — только логирование

При попытке заблокировать уже заблокированную сущность платформа выбрасывает `RepeatedLockException`. В production-коде Targets — перехват без retry, только запись в лог:

```csharp
try
{
  if (!Locks.TryLock(entity))
  {
    logger.DebugFormat("Entity {0} is locked, skipping", entity.Id);
    return;
  }
  // ... работа с entity ...
}
catch (RepeatedLockException ex)
{
  // Логируем, но НЕ ставим args.Retry — блокировка временная.
  logger.DebugFormat("RepeatedLockException for entity {0}: {1}",
    entity.Id, ex.Message);
}
finally
{
  Locks.Unlock(entity);
}
```

**Почему без retry:** `RepeatedLockException` означает, что другой поток уже работает с сущностью. Retry создаст конкуренцию и нагрузку. Лучше пропустить — следующий плановый запуск обработает.

---

*Источники: om_fonovye_processu.htm · om_executeasync.htm · ds_baseeditor_jobstab.htm · ds_baseeditor_asynchandlerstab.htm · ds_redaktor_async_obrabotchika.htm · sds_optimizaciya_processov.htm · Sungero.Domain.Shared.xml · Sungero.Domain.xml*
