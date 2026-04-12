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

## Production паттерны (Targets — 13 AsyncHandlers)

> Reference: `targets/CODE_PATTERNS_CATALOG.md` секция 3

### Паттерн 1: Отложенный retry (NextRetryTime)
```csharp
// Файл проверяется раз в 12 часов, пока не будет удалён
public virtual void DeleteCheckFile(/* params */)
{
  var checkFile = DirRX.KPI.CheckFiles.GetAll(f => f.Id == checkFileId).FirstOrDefault();
  if (checkFile == null) return; // удалён — OK
  args.NextRetryTime = Calendar.Now.AddHours(12); // retry через 12ч
  args.Retry = true;
}
```

### Паттерн 2: Batch с Lock/Unlock
```csharp
// Обработка порциями по 100 с блокировкой
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

### Паттерн 3: Fan-out (родитель → дочерние АО)
```csharp
// Родительский АО запускает дочерние для каждого элемента
public virtual void ConvertTargetsMapsDates(/* params */)
{
  var maps = DirRX.Targets.TargetsMaps.GetAll().Where(/* ... */);
  foreach (var map in maps)
    AsyncHandlerInvokeProxy.ExecutorConvertTargetsMapsDates(map.Id);
}
```

### Паттерн 4: Bounded retry
```csharp
args.Retry = args.RetryIteration < args.MaxRetryCount;
```

### Паттерн 5: RepeatedLockException (log only)
```csharp
catch (Sungero.Domain.Shared.Exceptions.RepeatedLockException ex)
{
  Logger.ErrorFormat("...", ex); // НЕ retry — просто лог
}
```

### Паттерн 6: Named Logger
```csharp
var logger = Logger.WithLogger(DTCommons.PublicConstants.Module.LoggerPostfix);
logger.DebugFormat("AsyncHandler started: {0}", nameof(this.MyHandler));
```

### MTD: ExponentialDelayStrategy
```json
{
  "$type": "Sungero.Metadata.AsyncHandlerMetadata",
  "DelayPeriod": 15,
  "DelayStrategy": "ExponentialDelayStrategy"
}
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

> **Примечание:** Поле `MaxRetryCount` НЕ используется в реальных CRM-модулях. Retry управляется через `args.Retry` в коде обработчика.

**Стратегии задержки:**
- `RegularDelayStrategy` — фиксированная задержка (для простых операций)
- `ExponentialDelayStrategy` — экспоненциальная (для внешних API, блокировок)

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
      Logger.DebugFormat("{0}. Start. documentId={1}, actionType={2}",
        nameof(ProcessDocumentAsync), args.documentId, args.actionType);

      var document = Sungero.Docflow.OfficialDocuments.GetAll(d => d.Id == args.documentId).FirstOrDefault();
      if (document == null)
      {
        Logger.DebugFormat("{0}. Document not found, id={1}",
          nameof(ProcessDocumentAsync), args.documentId);
        args.Retry = false;
        return;
      }

      if (Locks.GetLockInfo(document).IsLocked)
      {
        Logger.DebugFormat("{0}. Document locked, id={1}. Retry.",
          nameof(ProcessDocumentAsync), args.documentId);
        args.Retry = true;
        return;
      }

      try
      {
        // Бизнес-логика
        document.Save();
        Logger.DebugFormat("{0}. Done. documentId={1}", nameof(ProcessDocumentAsync), args.documentId);
      }
      catch (Exception ex)
      {
        Logger.ErrorFormat("{0}. Error: {1}", nameof(ProcessDocumentAsync), ex.Message);
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

## Паттерны из ESM (production)

### Retry-стратегия
```csharp
// Не найден объект → НЕ повторять
if (entity == null) { args.Retry = false; return; }

// Заблокирован → повторить
if (Locks.GetLockInfo(entity).IsLocked) { args.Retry = true; return; }

// Ошибка → повторить (если MaxRetryCount не исчерпан)
catch (Exception ex) { args.Retry = true; }
```

### Логирование
```csharp
// Формат: "{MethodName}. {Action}. {Params}"
Logger.DebugFormat("{0}. Start. id={1}", nameof(HandlerName), args.entityId);
Logger.DebugFormat("{0}. End. Success.", nameof(HandlerName));
Logger.ErrorFormat("{0}. Error: {1}", nameof(HandlerName), ex.Message);
```

### Передача списка ID через строку
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
3. Добавь JSON-объект в `AsyncHandlers[]` в Module.mtd
4. Добавь/обнови метод в `ModuleAsyncHandlers.cs`
5. Покажи пользователю пример вызова из серверного кода

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
