# SQL, блокировки и работа с базой данных

> Источник: `om_rabota_sql.htm`, `om_blokirovki.htm`, `om_blokirovki_zablokirovat_lock.htm`, `om_blokirovki_poputatsya_blokirovat_trylock.htm`, `om_blokirovki_razblokirovat_unlock.htm`, `om_blokirovki_polichit_info_getlockinfo.htm`, `sds_blokirovka_suschnosti_pered_izmeneniem.htm`, `sds_proverka_blokirovki.htm`, `sds_indeksu.htm`, `Sungero.Domain.xml`

---

## Класс SQL

**Доступен только в серверном коде.** Точка входа: `Sungero.Core.SQL`.

### Методы

| Метод | Описание |
|-------|----------|
| `SQL.CreateConnection()` | Создать подключение к текущей БД |
| `SQL.GetCurrentConnection()` | Получить текущее подключение (в рамках сессии) |
| `SQL.CreateBulkCopy()` | Создать объект пакетной передачи данных |
| `SQL.AddParameter(cmd, name, value, type)` | Добавить параметр к команде |
| `SQL.AddArrayParameter<T>(cmd, name, values, type)` | Добавить параметр-список |
| `SQL.AddCurrentDateParameter(cmd)` | Добавить параметр с текущей датой/временем |

### Создание подключения

```csharp
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  command.CommandText = string.Format(format, args);
  command.ExecuteScalar();
}
```

### Использование текущего подключения

```csharp
var connection = SQL.GetCurrentConnection();
using (var command = connection.CreateCommand())
{
  command.CommandText = "select count(*) from Sungero_Core_Login";
  var count = (int)command.ExecuteScalar();
}
```

**Важно:** `GetCurrentConnection()` бросает исключение, если сессия не открыта.

### Параметризованные запросы

```csharp
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  command.CommandText = "SELECT Name FROM MyTable WHERE Id = @id AND Status = @status";
  SQL.AddParameter(command, "@id", entityId, DbType.Int64);
  SQL.AddParameter(command, "@status", "Active", DbType.String);

  using (var reader = command.ExecuteReader())
  {
    while (reader.Read())
    {
      var name = reader.GetString(0);
    }
  }
}
```

### Массовый параметр (список)

```csharp
using (var connection = SQL.CreateConnection())
using (var command = connection.CreateCommand())
{
  var ids = new List<long> { 1, 2, 3, 4, 5 };
  command.CommandText = "SELECT * FROM MyTable WHERE Id = ANY(@ids)";
  SQL.AddArrayParameter<long>(command, "@ids", ids, DbType.Int64);
  command.ExecuteReader();
}
```

### Пакетная передача данных (BulkCopy)

```csharp
using (var bulkCopy = SQL.CreateBulkCopy())
{
  bulkCopy.Write<MyDataType>("TargetTableName", dataCollection);
}
```

### SQL-запросы в модуле (Queries)

SQL-запросы хранятся в файле `ModuleQueries.xml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<queries>
  <query name="GetDocumentCount">
    <![CDATA[
    SELECT COUNT(*) FROM Sungero_Content_EDoc
    WHERE Discriminator = @discriminator
      AND Created >= @dateFrom
    ]]>
  </query>
</queries>
```

Доступ из кода:
```csharp
var queryText = Queries.Module.GetDocumentCount;
```

Для типа сущности — `Document1Queries.xml`:
```csharp
var queryText = Queries.Document1.MyQuery;
```

---

## Блокировки (Locks)

### Концепция

Блокировка — запрет на редактирование объекта для всех пользователей, кроме установившего блокировку. Защищает от потери изменений при одновременном редактировании.

**Что можно блокировать:**
- Сущности (карточки)
- Бинарные данные (тела документов)

**Точка входа:** `Sungero.Core.Locks`

### Lock — заблокировать

```csharp
// Заблокировать сущность. Бросает исключение, если уже заблокирована.
Locks.Lock(document);

// Заблокировать тело документа.
Locks.Lock(document.LastVersion.Body);
```

### TryLock — попытаться заблокировать

```csharp
// Возвращает true, если блокировка успешна. НЕ бросает исключение.
bool locked = Locks.TryLock(document);
```

**Важно:** после `TryLock()` обязательно вызвать `Unlock()`.

### Unlock — разблокировать

```csharp
// Снять блокировку. Можно снять только свою блокировку в текущем процессе.
Locks.Unlock(document);
```

### GetLockInfo — информация о блокировке

```csharp
var lockInfo = Locks.GetLockInfo(document);
```

**Свойства `LockInfo`:**

| Свойство | Тип | Описание |
|----------|-----|----------|
| `IsLocked` | `bool` | Заблокирована ли сущность |
| `IsLockedByMe` | `bool` | Заблокирована текущим пользователем |
| `IsLockedByOther` | `bool` | Заблокирована другим пользователем |
| `LockedMessage` | `string` | Текст сообщения о блокировке |
| `LockTime` | `DateTime` | Время установки блокировки |
| `OwnerName` | `string` | Имя заблокировавшего пользователя |

### Проверка блокировки версий документа

```csharp
public static bool VersionIsLocked(
    List<Sungero.Content.IElectronicDocumentVersions> versions)
{
  foreach (var version in versions)
  {
    var lockInfo = version.Body != null
        ? Locks.GetLockInfo(version.Body)
        : null;
    if (lockInfo != null && lockInfo.IsLocked)
      return true;
  }
  return false;
}
```

---

## Паттерны блокировки

### Паттерн TryLock + try/finally

```csharp
var isLocked = false;
try
{
  isLocked = Locks.TryLock(document);

  if (isLocked)
  {
    document.CreateVersion();
    document.Save();
  }
  else
  {
    Dialogs.ShowMessage(
        "Не удалось заблокировать документ",
        "Документ заблокирован другим пользователем.",
        MessageType.Error,
        "Ошибка");
  }
}
finally
{
  if (isLocked)
    Locks.Unlock(document);
}
```

### Паттерн блокировки в асинхронном обработчике

```csharp
public virtual void ProcessDocument(
    Sungero.MyModule.AsyncHandlers.ProcessDocumentInvokeArgs args)
{
  var document = OfficialDocuments.Get(args.DocumentId);
  if (document == null)
    return;

  // Блокируем сразу после получения из БД.
  if (!Locks.TryLock(document))
  {
    args.Retry = true;
    args.RetryReason = "Document is locked by another process";
    return;
  }

  try
  {
    document.Name = "Updated";
    document.Save();
  }
  finally
  {
    Locks.Unlock(document);
  }
}
```

### Проверка блокировки в действиях

```csharp
// РЕКОМЕНДУЕТСЯ: проверять блокировку внутри действия, а не в CanExecute.
// CanExecute вызывается при выделении записи, когда блокировка ещё не загружена.
public virtual void ReturnDocument(Sungero.Domain.Client.ExecuteActionArgs e)
{
  if (Functions.Module.IsLockedByOther(_obj, e))
    return;

  // Основная логика действия.
}
```

---

## Версионирование сущностей (без блокировки)

### Проблема

Без блокировки платформа использует версионирование:

```
Поток 1: получает сущность (version=5) → изменяет → сохраняет (version 5→6) ✓
Поток 2: получает сущность (version=5) → изменяет → сохраняет (version 5 != 6) ✗
```

Результат:
- **На веб-сервере:** ошибка в клиенте, пользователь повторяет действие
- **На Worker:** ошибка не показывается, действие уходит на повтор

### Исключения

| Исключение | Когда |
|------------|-------|
| `StaleEntityException` | Сущность изменена в параллельном потоке |
| `StaleEntityNotFoundException` | Сущность удалена в параллельном потоке |

### Включение лога версионирования

В `config.yml`:
```yaml
common_config:
  ENTITY_VERSIONING_LOG_ENABLED: true
```

Логгер `VersioningLogger` записывает: `Type`, `Id`, `Version`, `OldVersion`.

---

## Индексы

### Когда использовать

- Тяжёлые запросы СУБД, снижающие быстродействие
- Виджеты, отчёты, папки (включая вычисляемые)

### Создание индекса (SQL)

```sql
CREATE NONCLUSTERED INDEX idx_Asg_Disc_Perf_Auth_MTask_ComplBy_Created
ON Sungero_WF_Assignment
(
    [Discriminator],
    [Performer],
    [Author],
    [MainTask],
    [CompletedBy],
    [Created]
)
```

### Создание при инициализации модуля

```csharp
public static void CreateAssignmentIndices()
{
  var tableName = Constants.Module.SungeroWFAssignmentTableName;
  var indexName = "idx_Asg_Disc_Perf_Auth_MTask_ComplBy_Created";
  var indexQuery = string.Format(
      Queries.Module.SungeroWFAssignmentIndex0Query,
      tableName, indexName);
  Functions.Module.CreateIndexOnTable(tableName, indexName, indexQuery);
}
```

### Рекомендации по индексам

| Правило | Пояснение |
|---------|----------|
| Не индексировать маленькие таблицы | Индексы неэффективны для малых объёмов |
| Уникальные индексы предпочтительнее | Уникальность повышает производительность |
| В составных индексах: WHERE-поля первыми | Затем — по убыванию уникальности |
| Имя индекса ≤ 63 символа | Ограничение PostgreSQL |
| Имя отражает таблицу и ключевые поля | Для удобства идентификации |

### Двухэтапные запросы для больших списков (>10 млн записей)

1. Событие `PreFiltering` — фильтрация по базовым критериям
2. Событие `Filtering` — фильтрация по оставшимся критериям

Работает только на веб-сервере с PostgreSQL.

---

## Получение данных без учёта прав доступа

```csharp
[Remote]
public double InvoicesAmount()
{
  var amount = 0.0;
  AccessRights.AllowRead(() =>
  {
    amount = (double)Sungero.Contracts.IncomingInvoices
      .GetAll()
      .Where(d => Sungero.Parties.Counterparties.Equals(d.Counterparty, _obj))
      .ToList()
      .Sum(t => t.TotalAmount);
  });
  return amount;
}
```

**Доступен только в серверном коде.** Внутри `AllowRead()` проверки прав не выполняются.

---

## Структура пакета разработки (.dat)

На основе анализа реального экспорта из DDS:

```
package.dat (ZIP-архив)
├── PackageInfo.xml              — информация о пакете (модули, версии)
├── source/                      — исходный код
│   └── Sungero.Module1/
│       ├── Sungero.Module1.Server/
│       │   ├── ModuleJobs.cs            — фоновые процессы
│       │   ├── ModuleAsyncHandlers.cs   — асинхронные обработчики
│       │   ├── ModuleServerFunctions.cs — серверные функции
│       │   ├── ModuleInitializer.cs     — инициализация
│       │   ├── ModuleHandlers.cs        — обработчики модуля
│       │   ├── ModuleBlockHandlers.cs   — обработчики блоков workflow
│       │   ├── ModuleWidgetHandlers.cs  — обработчики виджетов
│       │   ├── ModuleQueries.xml        — SQL-запросы
│       │   └── Document1/               — серверный код типа сущности
│       │       ├── Document1ServerFunctions.cs
│       │       ├── Document1Handlers.cs
│       │       └── Document1Queries.xml
│       ├── Sungero.Module1.ClientBase/
│       │   ├── ModuleClientFunctions.cs — клиентские функции
│       │   ├── ModuleHandlers.cs        — клиентские обработчики
│       │   ├── ModuleBlockHandlers.cs   — блоки (клиент)
│       │   ├── ModuleWidgetHandlers.cs  — виджеты (клиент)
│       │   └── Document1/
│       │       ├── Document1Actions.cs
│       │       ├── Document1ClientFunctions.cs
│       │       ├── Document1Handlers.cs
│       │       └── Generated/DefaultCardView.xml
│       └── Sungero.Module1.Shared/
│           ├── Module.mtd               — метаданные модуля (JSON)
│           ├── Module.resx              — ресурсы (англ.)
│           ├── Module.ru.resx           — ресурсы (рус.)
│           ├── ModuleConstants.cs       — константы
│           ├── ModuleStructures.cs      — структуры
│           ├── ModuleSharedFunctions.cs — shared-функции
│           └── Document1/
│               ├── Document1.mtd        — метаданные типа сущности
│               ├── Document1.resx / .ru.resx
│               ├── Document1Constants.cs
│               ├── Document1Structures.cs
│               ├── Document1Handlers.cs
│               └── Document1SharedFunctions.cs
├── server/    — скомпилированные серверные сборки (.dll)
├── client/    — скомпилированные клиентские сборки
├── shared/    — скомпилированные shared-сборки
├── isolated/  — изолированные сборки
└── settings/  — настройки модуля, обложки, виды
    └── Sungero.Module1/
        ├── Module.json
        ├── ExplorerModule/              — настройки навигации
        └── ModuleView/                  — настройки обложки (cover)
```

---

*Источники: om_rabota_sql.htm · om_blokirovki.htm · om_blokirovki_zablokirovat_lock.htm · om_blokirovki_poputatsya_blokirovat_trylock.htm · om_blokirovki_razblokirovat_unlock.htm · om_blokirovki_polichit_info_getlockinfo.htm · sds_blokirovka_suschnosti_pered_izmeneniem.htm · sds_proverka_blokirovki.htm · sds_indeksu.htm · Sungero.Domain.xml · archive/extracted/*
