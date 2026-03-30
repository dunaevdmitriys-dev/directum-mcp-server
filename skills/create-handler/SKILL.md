---
description: "Создать обработчик события сущности Directum RX (BeforeSave, Showing, Created и др.)"
---

# Создание обработчика события (Handler) Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `check_code_consistency` -- проверка согласованности MTD HandledEvents и C# кода
- `validate_all` -- полная валидация пакета после изменений
- `search_metadata` -- поиск эталонных обработчиков в платформе
- `suggest_pattern` -- подсказка паттерна для конкретного сценария
- `scaffold_entity` -- если сущность ещё не создана

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

**Реальные обработчики из CRM-пакета (подглядывай!):**

| Тип | Пример файла |
|-----|-------------|
| Created + BeforeSave + AfterSave (Server) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Server/Deal/DealHandlers.cs` |
| Showing (Client) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.ClientBase/Deal/DealHandlers.cs` |
| PropertyChanged (Shared) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Shared/Deal/DealHandlers.cs` |
| BeforeSave (валидация обязательных) | `CRM/crm-package/source/DirRX.CRMMarketing/DirRX.CRMMarketing.Server/Lead/LeadHandlers.cs` |
| Showing (условная видимость) | `CRM/crm-package/source/DirRX.CRMDocuments/DirRX.CRMDocuments.ClientBase/CommercialProposal/CommercialProposalHandlers.cs` |

### Reference: Сложные обработчики (KPI MetricHandlers)

| Файл | Путь |
|------|------|
| **MetricHandlers (10KB, эталон)** | `targets/source/DirRX.KPI/DirRX.KPI.Server/Metric/MetricHandlers.cs` |
| **Документация** | `targets/CODE_PATTERNS_CATALOG.md` секция 6 |

**Паттерны:**
- **Created**: обнуление полей при копировании (UpdatePeriod=Month, IsNew=true)
- **BeforeSave**: проверка дубликатов + валидация JSON из RemoteTable
- **AfterSave**: `e.Params.TryGetValue("tableChangesJson")` → UpdateMetricValues (JSON из RC)
- **Saved**: Revoke old Responsible + Grant new (синхронизация прав при смене ответственного)
- **BeforeDelete**: запрет если есть связанные TargetValues
- **Filtering**: My/MyTeam/MyDepartment фильтры

**Паттерн e.Params для JSON между событиями:**
```csharp
// BeforeSave: сохраняем JSON в Params
e.Params.AddOrUpdate("tableChangesJson", jsonString);
// AfterSave: достаём и обрабатываем
if (e.Params.TryGetValue("tableChangesJson", out var json))
  Functions.Module.UpdateMetricValues(metric, json);
```

```
# Найди обработчики конкретной сущности:
Grep("BeforeSave\|Created\|Showing", "CRM/crm-package/source/**/*Handlers.cs")

# Посмотри HandledEvents в MTD:
Grep("HandledEvents", "CRM/crm-package/source/**/*.mtd")
```

---

## Таблица типов обработчиков и порядок выполнения

→ см. **Guide 11** `knowledge-base/guides/11_events_lifecycle.md`:
- Секция **«Полный список серверных событий»** — все типы сущностей, документы, задачи, задания
- Секция **«Порядок выполнения событий»** — сохранение, удаление, создание, копирование, открытие карточки

**Краткая сводка суффиксов HandledEvents:**
- Server: `Created`, `BeforeSave`, `Saving`, `Saved`, `AfterSave`, `BeforeDelete`, `Deleting`, `AfterDelete`, `CreatingFrom`, `Filtering`
- Client: `Showing`, `Refresh`, `Closing`
- Shared: `<Property>Changed`, `<Property>ValueInput`, `<Property>Filtering`, `<Property>Added`, `<Property>Deleted`

---

## C# сигнатуры (шаблоны)

### Server Handlers (`{Entity}ServerHandlers`)

```csharp
// Файл: {Module}.Server/{Entity}/{Entity}Handlers.cs
// Namespace: {Company}.{Module} (БЕЗ .Server!)
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}
{
  partial class {Entity}ServerHandlers
  {
    public override void Created(Sungero.Domain.CreatedEventArgs e)
    {
      base.Created(e);
      _obj.Status = {Company}.{Module}.{Entity}.Status.Active;
      _obj.CreatedDate = Calendar.Today;
      _obj.Responsible = Sungero.Company.Employees.Current;
    }

    public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
    {
      base.BeforeSave(e);
      if (string.IsNullOrEmpty(_obj.Name))
        e.AddError(_obj.Info.Properties.Name, "Поле обязательно для заполнения.");
    }

    public override void AfterSave(Sungero.Domain.AfterSaveEventArgs e)
    {
      base.AfterSave(e);
      // Выдача прав, логирование (нельзя менять _obj!)
    }

    public override void BeforeDelete(Sungero.Domain.BeforeDeleteEventArgs e)
    {
      base.BeforeDelete(e);
      // e.AddError("Нельзя удалять.") -- блокирует удаление
    }
  }
}
```

### Client Handlers (`{Entity}ClientHandlers`)

```csharp
// Файл: {Module}.ClientBase/{Entity}/{Entity}Handlers.cs
// Namespace: {Company}.{Module} (БЕЗ .Client!)
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}
{
  partial class {Entity}ClientHandlers
  {
    public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
    {
      base.Showing(e);
      _obj.State.Properties.SomeField.IsVisible = _obj.Status != Status.Draft;
      _obj.State.Properties.SomeField.IsRequired = _obj.Status == Status.Active;
    }

    public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
    {
      base.Refresh(e);
      // Обновление видимости/доступности на основе текущих данных
    }
  }
}
```

### Shared Handlers (`{Entity}SharedHandlers`)

```csharp
// Файл: {Module}.Shared/{Entity}/{Entity}Handlers.cs
// Namespace: {Company}.{Module}
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {Company}.{Module}
{
  partial class {Entity}SharedHandlers
  {
    public virtual void {Property}Changed({Company}.{Module}.Shared.{Entity}{Property}ChangedEventArgs e)
    {
      // Пересчёт зависимых полей при изменении свойства
    }
  }
}
```

---

## HandledEvents в MTD (ОБЯЗАТЕЛЬНО!)

Каждый обработчик ДОЛЖЕН быть зарегистрирован в `.mtd` файле сущности.

### Формат записи

```json
"HandledEvents": [
  "CreatedServer",
  "BeforeSaveServer",
  "AfterSaveServer",
  "ShowingClient",
  "RefreshClient",
  "StageChangedShared",
  "PipelineChangedShared"
]
```

### Суффиксы

| Слой | Суффикс | Пример |
|------|---------|--------|
| Server | `Server` | `BeforeSaveServer`, `CreatedServer` |
| Client | `Client` | `ShowingClient`, `RefreshClient` |
| Shared (свойства) | `Shared` | `PipelineChangedShared`, `StageChangedShared` |

### Для свойств коллекции (в секции Properties конкретного свойства)

```json
{
  "Name": "Products",
  "HandledEvents": [
    "ProductsAddedShared",
    "ProductsDeletedShared"
  ]
}
```

**Реальный пример из Deal.mtd:**
```json
"HandledEvents": [
  "CreatedServer",
  "BeforeSaveServer",
  "AfterSaveServer",
  "ShowingClient",
  "StageChangedShared",
  "PipelineChangedShared"
]
```

---

## Дерево решений: какой обработчик использовать?

```
Нужна ВАЛИДАЦИЯ перед сохранением?
  --> BeforeSave (e.AddError блокирует сохранение)

Нужны ЗНАЧЕНИЯ ПО УМОЛЧАНИЮ при создании?
  --> Created

Нужна УСЛОВНАЯ ВИДИМОСТЬ полей?
  --> Showing (Client) для начальной настройки
  --> Refresh (Client) для динамической

Нужен ПЕРЕСЧЁТ при изменении свойства?
  --> {Property}Changed (Shared)

Нужна ВЫДАЧА ПРАВ после сохранения?
  --> AfterSave (после транзакции, безопасно)

Нужна ЛОГИКА В ТРАНЗАКЦИИ (откатываемая)?
  --> Saving

Нужен ЗАПРЕТ УДАЛЕНИЯ?
  --> BeforeDelete (e.AddError блокирует)

Нужна ФИЛЬТРАЦИЯ dropdown (ссылочное свойство)?
  --> {Property}Filtering (Shared)
```

---

## Типичные паттерны

### e.AddError() -- валидация

```csharp
// Ошибка на конкретном свойстве (подсветит поле):
e.AddError(_obj.Info.Properties.Name, "Поле обязательно.");

// Общая ошибка (без привязки к полю):
e.AddError("Дата начала не может быть позже даты окончания.");

// Предупреждение (НЕ блокирует сохранение):
e.AddWarning("Сумма сделки не указана.");
```

### State.Properties -- управление формой

```csharp
// Видимость:
_obj.State.Properties.LossReason.IsVisible = isFinalLost;

// Обязательность:
_obj.State.Properties.Name.IsRequired = true;

// Доступность (read-only):
_obj.State.Properties.Status.IsEnabled = false;
```

### e.Params -- передача данных между событиями

```csharp
// В Showing -- запомнить:
e.Params.AddOrUpdate("OriginalStatus", _obj.Status?.ToString());

// В Refresh -- прочитать:
string originalStatus;
if (e.Params.TryGetValue("OriginalStatus", out originalStatus))
{
  // ...
}
```

### Locks -- проверка блокировок

```csharp
// Проверить заблокирована ли сущность другим пользователем:
if (Locks.GetLockInfo(_obj).IsLocked)
  e.AddError("Запись заблокирована другим пользователем.");
```

### State.IsChanged -- проверка изменений

```csharp
// Проверить изменилось ли конкретное свойство:
if (_obj.State.Properties.Stage.IsChanged)
{
  _obj.LastStageChangeDate = Calendar.Now;
}
```

---

## Чеклист после создания обработчика

1. [ ] `HandledEvents` в `.mtd` содержит запись с правильным суффиксом
2. [ ] Namespace = `{Company}.{Module}` (БЕЗ `.Server` / `.Client`)
3. [ ] Класс = `partial class {Entity}ServerHandlers` / `{Entity}ClientHandlers` / `{Entity}SharedHandlers`
4. [ ] Вызов `base.{Event}(e)` в начале метода
5. [ ] MCP: `check_code_consistency` -- проверка согласованности MTD и C#
6. [ ] MCP: `validate_all` -- полная валидация пакета

---

## Ссылки на гайды (НЕ копируй, читай при необходимости)

- **Guide 11** `knowledge-base/guides/11_events_lifecycle.md` -- полный список событий, порядок, аргументы
- **Guide 25** `knowledge-base/guides/25_code_patterns.md` -- C# сигнатуры, using, namespace, паттерны сравнения
- **Guide 23** `knowledge-base/guides/23_mtd_reference.md` -- формат .mtd, HandledEvents
