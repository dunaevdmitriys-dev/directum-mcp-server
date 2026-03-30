---
description: "Создать асинхронный обработчик (AsyncHandler) для модуля Directum RX"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md`

# Создание AsyncHandler для Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей resx из MTD
- `extract_entity_schema` — просмотр схемы Module.mtd для проверки секции AsyncHandlers
- `search_metadata query=AsyncHandlers scope=modules` — поиск эталонных AsyncHandlers в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Приоритет 1 — Targets (9 production AH с продвинутыми retry-паттернами):
Read targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleAsyncHandlers.cs
Read targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleAsyncHandlers.cs

# Приоритет 2 — Платформа:
MCP: search_metadata query=AsyncHandlers scope=modules

# Приоритет 3 — CRM (базовые):
Read CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Server/ModuleAsyncHandlers.cs
```

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля (должен уже существовать)
- **HandlerName** — имя обработчика (PascalCase, например, `ProcessDocumentAsync`)
- **Parameters** — список параметров (имя + тип: LongInteger, String, Boolean)
- **Description** — что делает обработчик

## Поддерживаемые типы параметров

| Тип | ParameterType в MTD | C# тип |
|-----|---------------------|--------|
| long | `LongInteger` | `long` |
| string | `String` | `string` |
| bool | `Boolean` | `bool` |
| DateTime | `DateTime` | `DateTime` |

## Что создаётся / обновляется

### 1. Module.mtd — секция AsyncHandlers

Схема AsyncHandler в MTD содержит **только** эти поля:

```json
{
  "NameGuid": "<новый-GUID>",
  "Name": "ProcessDocumentAsync",
  "DelayPeriod": 15,
  "DelayStrategy": "RegularDelayStrategy",
  "IsHandlerGenerated": true,
  "Parameters": [
    {
      "NameGuid": "<новый-GUID>",
      "Name": "documentId",
      "ParameterType": "LongInteger"
    },
    {
      "NameGuid": "<новый-GUID>",
      "Name": "actionType",
      "ParameterType": "String"
    }
  ]
}
```

> **ВАЖНО:** Поля `MaxRetryCount` в DDS-схеме AsyncHandler **НЕ СУЩЕСТВУЕТ**.
> Retry-логика управляется ТОЛЬКО через код (`args.Retry`, `args.RetryIteration`, `args.NextRetryTime`).

**Стратегии задержки (DelayStrategy):**
- `RegularDelayStrategy` — фиксированная задержка (для простых операций)
- `ExponentialDelayStrategy` — экспоненциальная (для внешних API, блокировок)

**DelayPeriod** — начальная задержка в секундах перед retry.

### 2. ModuleAsyncHandlers.cs — обработчик

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}.Server
{
  public partial class ModuleAsyncHandlers
  {
    public virtual void ProcessDocumentAsync(
      {Company}.{Module}.Server.AsyncHandlerInvokeArgs.ProcessDocumentAsyncInvokeArgs args)
    {
      var logger = Logger.WithLogger("{Company}.{Module}");
      logger.DebugFormat("{0}. Start. documentId={1}, actionType={2}",
        nameof(ProcessDocumentAsync), args.documentId, args.actionType);

      var document = Sungero.Docflow.OfficialDocuments.GetAll(d => d.Id == args.documentId).FirstOrDefault();
      if (document == null)
      {
        logger.DebugFormat("{0}. Document not found, id={1}",
          nameof(ProcessDocumentAsync), args.documentId);
        return; // не найден — не повторять
      }

      if (Locks.GetLockInfo(document).IsLocked)
      {
        logger.DebugFormat("{0}. Document locked, id={1}. Retry.",
          nameof(ProcessDocumentAsync), args.documentId);
        args.Retry = true;
        return;
      }

      try
      {
        // Бизнес-логика
        document.Save();
        logger.DebugFormat("{0}. Done. documentId={1}", nameof(ProcessDocumentAsync), args.documentId);
      }
      catch (Exception ex)
      {
        logger.ErrorFormat("{0}. Error: {1}", nameof(ProcessDocumentAsync), ex.Message);
        args.Retry = true;
      }
    }
  }
}
```

### 3. Вызов из серверного кода

```csharp
// В любой серверной функции:
var async = AsyncHandlers.ProcessDocumentAsync.Create();
async.documentId = document.Id;
async.actionType = "approve";
async.ExecuteAsync();
```

## Retry-паттерны (ВСЕ — в коде, НЕ в MTD)

### Паттерн 1: Простой retry
```csharp
// Объект заблокирован или временная ошибка — повторить
args.Retry = true;
```

### Паттерн 2: Bounded retry (ограничение числа попыток)
```csharp
// Ограничение попыток — через args.RetryIteration В КОДЕ, не через MTD
if (args.RetryIteration < 5)
{
    args.Retry = true;
    return;
}
// Исчерпали попытки — логируем и выходим
Logger.ErrorFormat("{0}. Max retries reached ({1}). Giving up.",
  nameof(MyHandler), args.RetryIteration);
```

### Паттерн 3: NextRetryTime (scheduler-aware, отложенный retry)
```csharp
// Повтор через определённый интервал (учитывает рабочий календарь)
args.NextRetryTime = Calendar.Now.AddMinutes(5);
args.Retry = true;

// Targets example: проверка файла раз в 12 часов
public virtual void DeleteCheckFile(/* params */)
{
  var checkFile = DirRX.KPI.CheckFiles.GetAll(f => f.Id == checkFileId).FirstOrDefault();
  if (checkFile == null) return; // удалён — OK
  args.NextRetryTime = Calendar.Now.AddHours(12);
  args.Retry = true;
}
```

### Паттерн 4: Lock-aware retry
```csharp
// Проверить блокировку перед обработкой
if (Locks.GetLockInfo(entity).IsLocked)
{
    args.Retry = true;
    return;
}

// Targets: batch с Lock/Unlock
public virtual void ImportActualValuesInMetric(/* params */)
{
  var processLimit = 100;
  var entities = GetUnprocessed().Take(processLimit);
  foreach (var entity in entities)
  {
    if (!Locks.TryLock(entity)) continue;
    try { Process(entity); entity.Save(); }
    finally { Locks.Unlock(entity); }
  }
  args.Retry = GetUnprocessed().Any(); // повторить если остались
}
```

### Паттерн 5: Fan-out (родитель порождает дочерние обработчики)
```csharp
// Родительский АО запускает дочерние для каждого элемента
foreach (var item in items)
{
    var handler = AsyncHandlers.ProcessItem.Create();
    handler.ItemId = item.Id;
    handler.ExecuteAsync();
}

// Targets example:
public virtual void ConvertTargetsMapsDates(/* params */)
{
  var maps = DirRX.Targets.TargetsMaps.GetAll().Where(/* ... */);
  foreach (var map in maps)
    AsyncHandlerInvokeProxy.ExecutorConvertTargetsMapsDates(map.Id);
}
```

## Production паттерны (Targets — 13 AsyncHandlers)

> Reference: `targets/CODE_PATTERNS_CATALOG.md` секция 3

### Logger.WithLogger() — именованный логгер (Targets паттерн)
```csharp
// Targets используют именованный логгер через WithLogger()
// Это выделяет логи конкретного модуля в отдельный поток
var logger = Logger.WithLogger(DTCommons.PublicConstants.Module.LoggerPostfix);
logger.DebugFormat("AsyncHandler started: {0}", nameof(this.MyHandler));

// Формат логов: "{HandlerName}. {Action}. {Params}"
logger.DebugFormat("{0}. Start. id={1}", nameof(HandlerName), args.entityId);
logger.DebugFormat("{0}. End. Success.", nameof(HandlerName));
logger.ErrorFormat("{0}. Error: {1}", nameof(HandlerName), ex.Message);
```

### RepeatedLockException (log only, без retry)
```csharp
catch (Sungero.Domain.Shared.Exceptions.RepeatedLockException ex)
{
  Logger.ErrorFormat("...", ex); // НЕ retry — просто лог
}
```

### MTD: ExponentialDelayStrategy
```json
{
  "$type": "Sungero.Metadata.AsyncHandlerMetadata",
  "DelayPeriod": 15,
  "DelayStrategy": "ExponentialDelayStrategy"
}
```

## Передача списка ID через строку
```csharp
// Вызов:
async.documentIdsList = string.Join(";", documentList.Select(d => d.Id.ToString()));
async.ExecuteAsync();

// В обработчике:
var ids = args.documentIdsList.Split(';').Select(long.Parse).ToList();
```

## Файлы

```
source/{Company}.{Module}/
  ...Shared/Module.mtd           # Добавить в AsyncHandlers[]
  ...Server/ModuleAsyncHandlers.cs  # Добавить обработчик
```

## Алгоритм

### MCP валидация (ПОСЛЕ создания)

После добавления AsyncHandler в Module.mtd и ModuleAsyncHandlers.cs:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```

### Ручное создание

1. Прочитай Module.mtd — найди секцию `AsyncHandlers`
2. Сгенерируй GUID для обработчика и каждого параметра
3. Добавь JSON-объект в `AsyncHandlers[]` в Module.mtd (только `NameGuid`, `Name`, `DelayPeriod`, `DelayStrategy`, `Parameters`)
4. Добавь/обнови метод в `ModuleAsyncHandlers.cs`
5. Покажи пользователю пример вызова из серверного кода

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
