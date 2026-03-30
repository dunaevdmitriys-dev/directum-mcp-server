# /create-entity-action -- Добавление действий (кнопок) на карточку/список сущности RX

> Skill для добавления Actions в .mtd сущности + C# обработчик в *Actions.cs + System.resx локализация.

---

## ШАГ 0: Реальные примеры (ОБЯЗАТЕЛЬНО прочитать перед работой)

| Что | Путь |
|-----|------|
| Deal Actions MTD (6 actions: CreateProposal, CreateContract, CreateInvoice, ShowDealDocuments, AddActivity, OpenInCrmSpa) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Shared/Deal/Deal.mtd` строки 5-59 |
| Lead ConvertLead Action MTD | `CRM/crm-package/source/DirRX.CRMMarketing/DirRX.CRMMarketing.Shared/Lead/Lead.mtd` строки 5-14 |
| DealActions.cs (C# обработчики) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.ClientBase/Deal/DealActions.cs` |
| LeadActions.cs (ConvertLead с диалогом) | `CRM/crm-package/source/DirRX.CRMMarketing/DirRX.CRMMarketing.ClientBase/Lead/LeadActions.cs` |
| DealSystem.resx (Action_ ключи) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Shared/Deal/DealSystem.resx` строки 142-195 |
| DealSystem.ru.resx (ru локализация) | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Shared/Deal/DealSystem.ru.resx` строка 57, 67 |
| Правила Actions (теория) | Гайд `knowledge-base/guides/25_code_patterns.md` раздел 8 |

---

## Типы ActionArea

| ActionArea | Где видна кнопка | Когда использовать |
|------------|-----------------|-------------------|
| `Card` | Только на карточке (форме) сущности | Действия с текущей записью (CreateProposal, ConvertLead) |
| `Collection` | Только в списке сущностей | Массовые операции, экспорт |
| `CardAndCollection` | И на карточке, и в списке | Универсальные действия |

**По умолчанию** (если ActionArea не указан) -- `CardAndCollection`.

---

## MTD JSON шаблон для Action

### Минимальный (из реального Deal.mtd)

```json
{
  "NameGuid": "<NEW-GUID>",
  "Name": "CreateProposal",
  "ActionArea": "Card",
  "GenerateHandler": true,
  "LargeIconName": null,
  "SmallIconName": null,
  "Versions": []
}
```

### С иконками

```json
{
  "NameGuid": "<NEW-GUID>",
  "Name": "ExportToExcel",
  "ActionArea": "Collection",
  "GenerateHandler": true,
  "LargeIconName": "Action_ExportToExcel_Large",
  "SmallIconName": "Action_ExportToExcel_Small",
  "Versions": []
}
```

**Обязательные поля:**
- `NameGuid` -- уникальный GUID
- `Name` -- латиница, PascalCase (глагол + существительное)
- `GenerateHandler` -- `true` для генерации скелета обработчика

**Опциональные поля:**
- `ActionArea` -- Card / Collection / CardAndCollection (default)
- `LargeIconName` / `SmallIconName` -- имена иконок из ресурсов (null если без иконки)
- `NeedConfirmation` -- `true` для показа диалога подтверждения
- `IsVisibleInDesktopClient` -- `false` чтобы скрыть в desktop-клиенте

---

## C# обработчик (*Actions.cs)

### Расположение файла

```
<Module>/<Module>.ClientBase/<Entity>/<Entity>Actions.cs
```

Пример: `DirRX.CRMSales/DirRX.CRMSales.ClientBase/Deal/DealActions.cs`

### Шаблон обработчика

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace <CompanyCode>.<ModuleName>.Client
{
  partial class <Entity>Actions
  {
    /// <summary>
    /// Выполнение действия.
    /// </summary>
    public virtual void <ActionName>(Sungero.Domain.Client.ExecuteActionArgs e)
    {
      // Логика действия
    }

    /// <summary>
    /// Доступность действия.
    /// </summary>
    public virtual bool Can<ActionName>(Sungero.Domain.Client.CanExecuteActionArgs e)
    {
      return !_obj.State.IsInserted;
    }
  }
}
```

### Паттерны из реального кода

**Создание связанной сущности (CreateProposal):**
```csharp
public virtual void CreateProposal(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var proposal = DirRX.CRMDocuments.CommercialProposals.Create();
  proposal.Deal = _obj;
  proposal.TotalAmount = _obj.Amount;
  proposal.Name = _obj.Name;
  proposal.Show();
}

public virtual bool CanCreateProposal(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return !_obj.State.IsInserted;
}
```

**Действие с диалогом (ConvertLead):**
```csharp
public virtual void ConvertLead(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var dialog = Dialogs.CreateInputDialog(DirRX.CRMMarketing.Leads.Resources.ConvertLead);
  var pipelineField = dialog.AddSelect("Pipeline", true, DirRX.CRMSales.Pipelines.Null);
  if (dialog.Show() == DialogButtons.Ok)
  {
    var pipeline = pipelineField.Value;
    // ... создание сущности, сохранение ...
  }
}

public virtual bool CanConvertLead(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return _obj.LeadStatus == DirRX.CRMMarketing.Lead.LeadStatus.Qualified &&
         !_obj.State.IsInserted;
}
```

**Открытие списка документов (ShowDealDocuments):**
```csharp
public virtual void ShowDealDocuments(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var proposals = DirRX.CRMDocuments.CommercialProposals.GetAll()
    .Where(p => p.Deal != null && Equals(p.Deal, _obj));
  var invoices = DirRX.CRMDocuments.Invoices.GetAll()
    .Where(i => i.Deal != null && Equals(i.Deal, _obj));

  var documents = new List<Sungero.Content.IElectronicDocument>();
  documents.AddRange(proposals.Cast<Sungero.Content.IElectronicDocument>());
  documents.AddRange(invoices.Cast<Sungero.Content.IElectronicDocument>());

  if (documents.Any())
    documents.AsQueryable().Show();
  else
    Dialogs.ShowMessage("Нет документов", "Сообщение", MessageType.Information);
}
```

**Открытие внешнего URL (OpenInCrmSpa):**
```csharp
public virtual void OpenInCrmSpa(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var spaUrl = string.Format("/Client/content/crm/#/deals/{0}", _obj.Id);
  Sungero.Core.Hyperlinks.Open(spaUrl);
}
```

---

## Правила из Guide 25

- `Can*` -- определяет доступность кнопки. НЕ должен содержать тяжёлой логики (запросы к БД минимальны).
- Execute-метод может вызывать `[Remote]` функции сервера.
- `e.AddError("text")` -- показать ошибку пользователю.
- `e.Cancel()` -- отменить действие без ошибки.
- `_obj` -- текущая сущность.
- `_obj.State.IsInserted` -- true если запись ещё не сохранена (нет Id).

---

## System.resx ключи для Actions

### Файлы

```
<Module>.Shared/<Entity>/<Entity>System.resx       -- EN (обязательно)
<Module>.Shared/<Entity>/<Entity>System.ru.resx     -- RU (обязательно для ru)
```

### Формат ключей (из реального DealSystem.resx)

```xml
<!-- Название кнопки (ОБЯЗАТЕЛЬНО) -->
<data name="Action_<ActionName>" xml:space="preserve">
  <value>Create proposal</value>
</data>

<!-- Подсказка при наведении (опционально) -->
<data name="Action_<ActionName>Hint" xml:space="preserve">
  <value>Create a commercial proposal for this deal</value>
</data>

<!-- Описание в меню (опционально) -->
<data name="Action_<ActionName>Description" xml:space="preserve">
  <value>Creates a new commercial proposal linked to the deal</value>
</data>
```

**Обязательный ключ:** `Action_<ActionName>` -- без него кнопка отобразится как техническое имя.

**Опциональные ключи:**
- `Action_<ActionName>Hint` -- tooltip при наведении
- `Action_<ActionName>Description` -- расширенное описание

### Пример для ru.resx

```xml
<data name="Action_CreateProposal" xml:space="preserve"><value>Создать КП</value></data>
<data name="Action_CreateProposalHint" xml:space="preserve"><value>Создать КП</value></data>
<data name="Action_CreateProposalDescription" xml:space="preserve"><value>Создать КП</value></data>
```

---

## Алгоритм добавления Entity Action

### 1. Определить параметры

- Имя действия (PascalCase, глагол + существительное)
- ActionArea: Card / Collection / CardAndCollection
- Нужна ли иконка
- Логика Can* (когда кнопка доступна)
- Логика Execute (что делает)

### 2. Добавить в .mtd

В массив `Actions[]` в .mtd файле сущности:

```json
{
  "NameGuid": "<NEW-GUID>",
  "Name": "<ActionName>",
  "ActionArea": "Card",
  "GenerateHandler": true,
  "LargeIconName": null,
  "SmallIconName": null,
  "Versions": []
}
```

### 3. Создать/обновить *Actions.cs

В `<Module>.ClientBase/<Entity>/<Entity>Actions.cs` добавить два метода:
- `public virtual void <ActionName>(ExecuteActionArgs e)` -- логика
- `public virtual bool Can<ActionName>(CanExecuteActionArgs e)` -- доступность

### 4. Добавить локализацию

В `<Entity>System.resx` (EN):
```xml
<data name="Action_<ActionName>" xml:space="preserve">
  <value>English name</value>
</data>
```

В `<Entity>System.ru.resx` (RU):
```xml
<data name="Action_<ActionName>" xml:space="preserve">
  <value>Русское название</value>
</data>
```

### 5. Валидация

```
MCP: check_code_consistency
MCP: sync_resx_keys
MCP: validate_all
```

---

## MCP Tools

| Tool | Когда использовать |
|------|--------------------|
| `check_code_consistency` | Проверить соответствие .mtd и .cs |
| `sync_resx_keys` | Проверить что все Action_ ключи есть в .resx |
| `validate_all` | Полная валидация решения |
| `extract_entity_schema entity=<Name>` | Получить текущие Actions сущности |
| `search_metadata name=<EntityName>` | Найти .mtd файл сущности |

---

## Чеклист

- [ ] Прочитан реальный пример из Deal.mtd / LeadActions.cs (ШАГ 0)
- [ ] GUID сгенерирован (uuidgen)
- [ ] Name -- PascalCase, глагол + существительное
- [ ] ActionArea выбран корректно
- [ ] GenerateHandler = true
- [ ] *Actions.cs содержит оба метода: Execute и Can
- [ ] Can* не содержит тяжёлой логики
- [ ] Action_<Name> ключ в System.resx (EN)
- [ ] Action_<Name> ключ в System.ru.resx (RU)
- [ ] `check_code_consistency` пройден
- [ ] `sync_resx_keys` пройден
- [ ] `validate_all` пройден
