---
description: "Создать фоновое задание (Background Job) Directum RX — MTD, обработчик, расписание"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

# Создание фонового задания (Job)

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `scaffold_job` — генерация Job: MTD-секция + обработчик + resx за один вызов
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей resx из MTD
- `search_metadata query=Jobs scope=modules` — поиск эталонных Jobs в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Найди Job в существующем модуле:
MCP: search_metadata query=Jobs scope=modules

# Прочитай секцию Jobs в Module.mtd:
# DirRX.CRM Module.mtd содержит 2 реальных Job-а (StaleDealJob, LeadAssignmentJob)
# Посмотри структуру: GenerateHandler, MonthSchedule, StartAt.
```

## Входные данные

Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля
- **JobName** — имя задания (например, `SyncWidgetData`)
- **DisplayName** — отображаемое имя
- **Schedule** — расписание: `Monthly` / `January`...`December` / `Period: N` (минуты)
- **Description** — что делает задание
- **NeedsLocking** — нужна ли блокировка для обрабатываемых записей

## Что создаётся

```
source/{Company}.{Module}/
  {Company}.{Module}.Server/
    ModuleJobs.cs                   # Обработчик фонового задания
  {Company}.{Module}.Shared/
    Module.mtd                      # Job в секции Jobs
    Module.resx / Module.ru.resx    # Ресурсы (DisplayName задания)
```

## Алгоритм

### 1. MCP генерация (СНАЧАЛА попробуй MCP)

```
MCP: scaffold_job outputPath={путь_к_модулю} jobName={JobName} moduleName={CompanyCode}.{ModuleName} cronSchedule="0 4 * * *"
```
Если MCP доступен — используй результат. Затем проверь:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```
Если MCP недоступен — генерируй вручную по шаблону ниже.

### 2. Сгенерируй GUID (ручной fallback)
- `JobGuid` — для задания

### 2. Добавь в Module.mtd

```json
"Jobs": [
  {
    "NameGuid": "<JobGuid>",
    "Name": "<JobName>",
    "GenerateHandler": true,
    "MonthSchedule": "Monthly",
    "StartAt": "1753-01-01T02:00:00",
    "Versions": []
  }
]
```

**Расписания:**
| Расписание | MTD поля |
|-----------|----------|
| Каждый день в 4:00 | `"MonthSchedule": "Monthly", "StartAt": "1753-01-01T04:00:00"` |
| Каждый час | `"Period": 60` |
| Каждые 15 мин | `"Period": 15` |
| Только в январе | `"MonthSchedule": "January", "StartAt": "1753-01-01T04:00:00"` |

**КРИТИЧНО — MonthSchedule валидные значения:**
`"Monthly"`, `"January"`, `"February"`, `"March"`, `"April"`, `"May"`, `"June"`,
`"July"`, `"August"`, `"September"`, `"October"`, `"November"`, `"December"`.
**НЕ `"Daily"`!** `"Daily"` → `Error converting value "Daily" to type Months`.
Для ежедневного запуска используй `"MonthSchedule": "Monthly"` — это значит "каждый месяц = каждый день".

### 3. Server Handler — ModuleJobs.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}.Server
{
  partial class ModuleJobs
  {
    /// <summary>
    /// {Description}.
    /// </summary>
    public virtual void {JobName}()
    {
      Logger.DebugFormat("{0}. Start.", nameof({JobName}));

      var entities = MyEntities.GetAll()
        .Where(e => e.Status == Status.Active)
        .Where(e => e.NeedsSync == true)
        .ToList();

      Logger.DebugFormat("{0}. Found {1} entities to process.", nameof({JobName}), entities.Count);

      foreach (var entity in entities)
      {
        try
        {
          if (!Locks.TryLock(entity))
          {
            Logger.DebugFormat("{0}. Entity {1} is locked, skipping.", nameof({JobName}), entity.Id);
            continue;
          }

          try
          {
            // Обработка
            entity.NeedsSync = false;
            entity.Save();
            Logger.DebugFormat("{0}. Processed entity {1}.", nameof({JobName}), entity.Id);
          }
          finally
          {
            Locks.Unlock(entity);
          }
        }
        catch (Exception ex)
        {
          Logger.ErrorFormat("{0}. Error processing entity {1}: {2}",
            nameof({JobName}), entity.Id, ex.Message);
        }
      }

      Logger.DebugFormat("{0}. End.", nameof({JobName}));
    }
  }
}
```

### 4. Паттерн batch-обработки с лимитом (Targets/KPI)

```csharp
public virtual void ImportActualValues()
{
  const int batchLimit = 100;
  var unprocessed = GetUnprocessedRecords().Take(batchLimit).ToList();

  foreach (var record in unprocessed)
  {
    if (Locks.TryLock(record))
    {
      try
      {
        ProcessRecord(record);
        record.Save();
      }
      finally { Locks.Unlock(record); }
    }
  }

  // Если ещё остались — запустить async для продолжения
  if (GetUnprocessedRecords().Any())
  {
    var handler = AsyncHandlers.ContinueImport.Create();
    handler.ExecuteAsync();
  }
}
```

### 5. Паттерн с агрегацией данных для виджетов (ESM)

```csharp
public virtual void SyncWidgetData()
{
  Logger.Debug("SyncWidgetData. Start.");

  var today = Calendar.Today;
  var monthAgo = today.AddMonths(-1);

  // Агрегация
  var activeCount = Requests.GetAll()
    .Where(r => r.Status == RequestStatus.Active)
    .Count();

  var closedCount = Requests.GetAll()
    .Where(r => r.Status == RequestStatus.Closed)
    .Where(r => r.ClosedDate >= monthAgo)
    .Count();

  // Сохранение в кэш-сущность
  var cache = WidgetCaches.GetAll().FirstOrDefault()
    ?? WidgetCaches.Create();
  cache.ActiveRequests = activeCount;
  cache.ClosedRequests = closedCount;
  cache.LastSync = Calendar.Now;
  cache.Save();

  Logger.Debug("SyncWidgetData. End.");
}
```

### 6. Ресурсы

**Module.resx:**
```xml
<data name="{JobName}Name" xml:space="preserve"><value>Sync widget data</value></data>
```

**Module.ru.resx:**
```xml
<data name="{JobName}Name" xml:space="preserve"><value>Синхронизация данных виджетов</value></data>
```

## Валидация

- [ ] Handler в `ModuleJobs.cs` — `partial class ModuleJobs`
- [ ] Namespace = `{Company}.{Module}.Server` (Jobs — единственный с `.Server`!)
- [ ] Job в секции `Jobs` Module.mtd с `GenerateHandler: true`
- [ ] `Logger.Debug/Error` с `nameof()` для трассировки
- [ ] `Locks.TryLock/Unlock` для записей
- [ ] Batch-обработка с лимитом для больших объёмов
- [ ] try-catch на каждой записи (одна ошибка не ломает весь цикл)

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
