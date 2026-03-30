# Workflow: задачи, задания, бизнес-процессы

> Источник: `webhelp/WebClient/ru-RU/om_createtask.htm`, `om_scriptschemeblocks.htm`, `om_assignmentblockwrapper.htm`, `Sungero.Workflow.Server.xml`

---

## Основные объекты Workflow

| Класс/Интерфейс | Описание |
|-----------------|----------|
| `ITask` | Задача — контейнер бизнес-процесса |
| `ISimpleTask` | Простая задача (без сложной схемы) |
| `IAssignment` | Задание — конкретное действие для исполнителя |
| `INotice` | Уведомление (не требует действия) |
| `IReviewAssignment` | Задание на ознакомление |
| `IScheme` | Схема бизнес-процесса (граф блоков) |
| `ISchemeBlock` | Блок схемы |
| `IProcessKind` | Вариант процесса — конфигурация маршрута |

---

## Создание задачи

```csharp
// Создать задачу и вложить документ.
[Public]
public static ITask CreateTaskWithDocument(IElectronicDocument document)
{
  var task = Sungero.Workflow.Tasks.Create();

  // Тема задачи.
  task.Subject = string.Format("Ознакомиться с документом: {0}", document.Name);

  // Срок: 3 рабочих дня от сегодня.
  task.Deadline = Calendar.Today.AddWorkingDays(Sungero.Company.Employees.Current, 3);

  // Вложить документ.
  task.Attachments.Add(document);

  return task;
}
```

```csharp
// Создать подзадачу в контексте текущей задачи.
public virtual void CreateSubtaskExecute(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var subtask = _obj.CreateAsSubtask(Sungero.Workflow.Tasks);
  subtask.Subject = "Подзадача для согласования";
  subtask.ShowCard();
}
```

---

## Создание простой задачи

```csharp
// Создать простую задачу с несколькими исполнителями.
[Public]
public static void SendSimpleTask(string subject, List<IEmployee> performers)
{
  var task = Sungero.Workflow.SimpleTasks.Create();
  task.Subject = subject;
  task.ActiveText = "Пожалуйста, ознакомьтесь с материалами.";

  foreach (var performer in performers)
  {
    var assignee = task.Assignees.AddNew();
    assignee.Assignee = performer;
  }

  task.Start();
}
```

---

## Схема бизнес-процесса

Схема — это граф блоков, описывающий маршрут задачи. Доступна через `task.Scheme`.

### Получение блоков схемы

```csharp
// Получить схему текущей задачи.
IScheme scheme = task.Scheme;

// Получить конкретный блок по ID.
var block = SchemeBlocks.Get(scheme, blockId);

// Получить все блоки.
var allBlocks = SchemeBlocks.GetAll(scheme);

// Получить начальный блок.
var startBlock = StartSchemeBlocks.Get(scheme);

// Получить блок завершения.
var finishBlock = FinishSchemeBlocks.Get(scheme);
```

### Блок «Скрипт» (ScriptSchemeBlock)

Блок для выполнения произвольного C# кода в схеме. Доступен **только в серверном коде**.

```csharp
// Получить блок-скрипт по ID.
var scriptBlock = ScriptSchemeBlocks.Get(scheme, blockId);

// Свойства блока.
string description = scriptBlock.Description;
Enumeration result = scriptBlock.ExecutionResult; // Success / CustomResult

// Получить все скрипт-блоки схемы.
var allScripts = ScriptSchemeBlocks.GetAll(scheme);
```

**Реализация кода скрипт-блока:**

```csharp
// Код выполняется в обработчике блока схемы.
public virtual void ScriptBlock1Execute(ISchemeBlockExecutionContext context)
{
  var task = Sungero.Workflow.Tasks.As(context.Task);
  var document = task.DocumentGroup.OfficialDocuments.FirstOrDefault();

  if (document == null)
  {
    context.Result = ScriptBlock1.ExecutionResult.NoDocument;
    return;
  }

  // Основная логика.
  ProcessDocument(document);
  context.Result = ScriptBlock1.ExecutionResult.Success;
}
```

---

## Блок «Задание» (AssignmentBlock)

Свойства блока задания в схеме:

```csharp
// Получить блок задания.
var assignmentBlock = AssignmentSchemeBlocks.Get(scheme, blockId);

// Настройка параметров задания.
assignmentBlock.AbsoluteDeadline = Calendar.Today.AddDays(5);
assignmentBlock.Instruction = "Согласуйте документ и проставьте подпись.";
assignmentBlock.IsCompetitive = false; // не конкурентное

// Исполнитель.
assignmentBlock.Performer = employee;

// Что делать если нет исполнителей.
assignmentBlock.NoPerformersResult = AssignmentBlock.NoPerformersResult.Complete;
```

---

## Варианты процесса (ProcessKinds)

ProcessKind — настройка маршрута согласования для конкретного вида документа. Создаётся в NoCode-режиме, управляется программно.

```csharp
// Получить вариант процесса.
var processKind = Sungero.Docflow.ProcessKinds.GetAll()
  .FirstOrDefault(pk => pk.Name == "Согласование договора");

// Получить схему варианта процесса.
var scheme = processKind.Scheme;

// Получить все скрипт-блоки этой схемы.
var scripts = ScriptSchemeBlocks.GetAll(scheme);
```

---

## Роли в маршруте согласования

Роль определяет, кто является исполнителем конкретного этапа. Переопределяется через `GetRolePerformers`:

```csharp
[Remote(IsPure = true), Public]
public List<Sungero.CoreEntities.IRecipient> GetRolePerformers(
    Sungero.Docflow.IApprovalTask task)
{
  var result = new List<Sungero.CoreEntities.IRecipient>();
  var document = task.DocumentGroup.OfficialDocuments.FirstOrDefault();

  // Получить договор закупки.
  var contract = Trade.Contracts.As(document);
  if (contract != null && _obj.Type == PurchaseRole.Type.Experts)
  {
    // Все эксперты договора.
    foreach (var item in contract.Experts.Where(x => x.Expert != null))
      result.Add(item.Expert);
  }

  return result;
}
```

---

## Получатели этапа согласования

```csharp
[Remote(IsPure = true), Public]
public override List<IRecipient> GetStageRecipients(
    Sungero.Docflow.IApprovalTask task,
    List<IRecipient> additionalApprovers)
{
  // Базовый список + дополнительные согласующие.
  var recipients = base.GetStageRecipients(task, additionalApprovers);

  // Добавить исполнителей пользовательской роли.
  var expertRole = _obj.ApprovalRoles
    .Select(x => PurchaseApprovalRole.As(x.ApprovalRole))
    .Where(x => x != null && x.Type == PurchaseRole.Type.Experts)
    .FirstOrDefault();

  if (expertRole != null)
  {
    recipients.AddRange(
      PublicFunctions.PurchaseApprovalRole.Remote
        .GetRolePerformers(expertRole, task));
  }

  return recipients;
}
```

---

## Вложения задачи (Attachments)

```csharp
// Добавить документ в группу вложений.
task.Attachments.Add(document);

// Добавить в именованную группу.
task.DocumentGroup.OfficialDocuments.Add(officialDoc);

// Получить первый документ из группы.
var document = task.DocumentGroup.OfficialDocuments.FirstOrDefault();

// Перебрать все вложения.
foreach (var attachment in task.Attachments)
{
  Logger.DebugFormat("Вложение: {0}", attachment.Name);
}
```

---

## Запуск и управление задачей

```csharp
// Запустить задачу.
task.Start();

// Проверить статус.
if (task.Status == Sungero.Workflow.Task.Status.InProcess)
{
  Logger.Debug("Задача выполняется.");
}

// Прервать задачу.
task.Abort("Прервано пользователем.");

// Перезапустить задачу (если завершена).
task.Restart();
```

---

## Фоновые задания (Background Jobs)

Статусы фонового процесса (`BackgroundJobExecutionStatus`):

| Статус | Описание |
|--------|----------|
| `New` | Создан, не запущен |
| `Initialization` | Формирование данных |
| `Processing` | Выполняется |
| `Done` | Завершён успешно |
| `Error` | Ошибка |
| `Canceled` | Отменён |

```csharp
// Запустить фоновое задание.
Sungero.Core.BackgroundJobs.Enqueue(() =>
  Functions.Module.ProcessDocumentInBackground(documentId));
```

---

## Ретраи блоков (RetrySettings)

При ошибке в блоке схемы можно настроить повтор:

```csharp
// Проверить настройки повтора блока.
var retry = block.RetrySettings;
if (retry.Retry)
{
  Logger.DebugFormat("Попытка №{0}", retry.RetryIteration);
}
```

---

## Жизненный цикл задачи (Task)

```
Draft → InProcess → Completed
              ↓         ↑
          Suspended → Resume
              ↓
           Aborted
```

| Статус | Описание |
|--------|----------|
| `Draft` | Черновик (до старта) |
| `InProcess` | Задача в работе |
| `Completed` | Задача завершена |
| `Aborted` | Задача прекращена |
| `Suspended` | Задача приостановлена (ошибка) |

### Методы управления задачей

```csharp
task.Start();                    // Запустить
task.Abort("Причина");           // Прекратить
task.Restart();                  // Перезапустить (из Completed/Aborted)
// Статусы задач — read-only
```

---

## Серверные события задачи

| Событие | Метод | Описание |
|---------|-------|----------|
| До старта | `BeforeStart` | Валидация перед стартом. `e.AddError()` блокирует старт |
| До рестарта | `BeforeRestart` | Свойства задачи доступны для изменения |
| До возобновления | `BeforeResume` | Логика перед возобновлением |
| До прекращения | `BeforeAbort` | Проверка возможности прекращения |
| После приостановки | `AfterSuspend` | Реакция на ошибку (например, уведомление) |

```csharp
// Пример BeforeStart: проверить вложения.
public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
{
  if (!_obj.DocumentGroup.OfficialDocuments.Any())
    e.AddError("Вложите документ перед стартом задачи.");
}
```

---

## Серверные события задания

| Событие | Метод | Описание |
|---------|-------|----------|
| До выполнения | `BeforeComplete` | Валидация перед выполнением. `e.AddError()` блокирует выполнение |

```csharp
// Пример BeforeComplete: проверить заполнение перед согласованием.
public override void BeforeComplete(Sungero.Workflow.Server.BeforeCompleteEventArgs e)
{
  base.BeforeComplete(e);
  if (_obj.Result == Result.Approved && string.IsNullOrEmpty(_obj.ActiveText))
    e.AddError("Добавьте комментарий при согласовании.");
}
```

### События конкурентного задания

| Событие | Метод | Описание |
|---------|-------|----------|
| До взятия в работу | `BeforeStartWork` | Валидация. Работает при действии `StartWork` и методе `StartWork()` |
| До возврата невыполненным | `BeforeReturnUncompleted` | Проверка перед возвратом |

### События задания на приёмку

| Событие | Метод | Описание |
|---------|-------|----------|
| До принятия | `BeforeAccept` | Валидация перед принятием |
| До отправки на доработку | `BeforeSendForRework` | Проверка перед отправкой |

---

## Типы блоков схемы (полный список)

| Класс | Описание |
|-------|----------|
| `StartSchemeBlocks` | Начальный блок |
| `FinishSchemeBlocks` | Конечный блок |
| `ScriptSchemeBlocks` | Блок «Скрипт» — произвольный C# код |
| `AssignmentSchemeBlocks` | Блок «Задание» — создание задания исполнителю |
| `TaskSchemeBlocks` | Блок «Подзадача» — старт подзадачи |
| `NoticeSchemeBlocks` | Блок «Уведомление» — отправка уведомления |
| `DecisionSchemeBlocks` | Блок «Условие» — ветвление `True`/`False` |
| `MonitoringSchemeBlocks` | Блок «Мониторинг» — ожидание условия |
| `WaitSchemeBlocks` | Блок «Ожидание» — пауза на заданное время |
| `AccessRightsSchemeBlocks` | Блок «Настройка прав» — изменение прав доступа |

### Блок «Условие» (Decision)

```csharp
var decision = DecisionSchemeBlocks.Get(scheme, blockId);
// Результат: ExecutionResult.True или ExecutionResult.False
decision.ExecutionResult = DecisionSchemeBlock.ExecutionResult.True;
```

### Блок «Уведомление» (Notice)

```csharp
var notice = NoticeSchemeBlocks.Get(scheme, blockId);
notice.Performer = employee;
```

### Блок «Настройка прав» (AccessRights)

```csharp
// Действия: Action.Add, Action.Set, Action.DeleteAll
// Типы: Type.Read, Type.Change, Type.FullAccess, Type.Forbidden
```

---

*Источники: om_createtask.htm · om_scriptschemeblocks.htm · om_assignmentblockwrapper.htm · om_simpletaskblock.htm · Sungero.Workflow.Server.xml · sds_sobytiia_sushchnosti.htm*
