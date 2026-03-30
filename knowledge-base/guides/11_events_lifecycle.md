# События сущностей и жизненный цикл

> Источник: `sds_sobytiia_sushchnosti.htm`, `sds_sobytiia_formy_sushchnosti.htm`, `sds_sobytiia_svoistv_sushchnosti.htm`, `sds_sobytiia_deistvii_sushchnosti.htm`, `sds_poriadok_vypolneniia_sobytii.htm`

---

## Полный список серверных событий

### Все типы сущностей

| Событие | Имя метода | Когда срабатывает |
|---------|-----------|------------------|
| До сохранения | `BeforeSave` | До SQL-транзакции. Валидация, вычисления, изменение связанных сущностей |
| До сохранения (в транзакции) | `Saving` | В SQL-транзакции. Вычисления, откатываемые при ошибке |
| После сохранения (в транзакции) | `Saved` | В SQL-транзакции, после записи в БД. SQL-запросы, нельзя менять `_obj` |
| После сохранения | `AfterSave` | После завершения транзакции. Логирование, выдача прав. Нельзя менять `_obj` |
| До сохранения истории | `BeforeSaveHistory` | Перед записью в историю. Кастомизация записей истории |
| До удаления | `BeforeDelete` | До SQL-транзакции. Валидация перед удалением |
| До удаления (в транзакции) | `Deleting` | В SQL-транзакции. Изменение связанных сущностей |
| После удаления | `AfterDelete` | После транзакции. Очистка связанных данных |
| Копирование | `CreatingFrom` | При создании копии. Управление копируемыми свойствами |
| Создание | `Created` | При создании новой сущности. Значения по умолчанию |
| Предварительная фильтрация | `PreFiltering` | При получении списка. Оптимизация тяжёлых запросов |
| Фильтрация | `Filtering` | При получении списка. Формирование содержимого списка |

### Только документы

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| До подписания | `BeforeSigning` | Перед подписанием ЭП. Валидация подписи |
| Смена типа | `ConvertingFrom` | При смене типа документа. Правила переноса свойств |

### Только задачи

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| До старта | `BeforeStart` | Перед отправкой задачи. Валидация |
| До рестарта | `BeforeRestart` | Перед повторной отправкой |
| До возобновления | `BeforeResume` | Перед возобновлением приостановленной задачи |
| До прекращения | `BeforeAbort` | Перед прекращением задачи |
| После приостановки | `AfterSuspend` | После приостановки из-за ошибки |

### Только задания

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| До выполнения | `BeforeComplete` | Перед выполнением задания |

### Только задания на приёмку

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| До принятия | `BeforeAccept` | Перед принятием |
| До отправки на доработку | `BeforeSendForRework` | Перед возвратом на доработку |

### Только конкурентные задания

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| До взятия в работу | `BeforeStartWork` | Перед взятием в работу |
| До возврата невыполненным | `BeforeReturnUncompleted` | Перед возвратом |

### Только справочники

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| UI-фильтрация | `UIFiltering` | При показе списка в веб-клиенте |
| Показ подсказки | `GetDigest` | При наведении курсора на пользователя (только для наследников `Sungero.CoreEntities.User`) |

---

## Порядок выполнения событий

### Сохранение сущности

```
1. BeforeSave         ← валидация, вычисления (вне транзакции)
   ↓ e.AddError() → блокирует сохранение
2. [SQL-транзакция начинается]
3. Saving             ← вычисления в транзакции
4. [запись в БД]
5. Saved              ← логика после записи (в транзакции)
6. BeforeSaveHistory  ← кастомизация записи истории
7. [SQL-транзакция завершается]
8. AfterSave          ← логика после транзакции (логирование, права)
```

**Важно:** если в `AfterSave` возникнет исключение — пользователь увидит ошибку, но сущность уже сохранена в БД.

### Удаление сущности

```
1. BeforeDelete       ← валидация перед удалением (вне транзакции)
   ↓ e.AddError() → блокирует удаление
2. [SQL-транзакция начинается]
3. Deleting           ← логика в транзакции
4. [удаление из БД]
5. [SQL-транзакция завершается]
6. AfterDelete        ← логика после удаления
```

### Создание сущности

```
1. Created            ← заполнение значений по умолчанию
```

### Копирование сущности

```
1. CreatingFrom       ← управление копируемыми свойствами
2. Created            ← заполнение значений по умолчанию
```

### Открытие/закрытие карточки

```
1. Showing            ← настройка формы (видимость, доступность полей)
2. Refresh            ← обновление формы
   ...
3. Closing            ← при закрытии карточки
```

---

## Клиентские события (форма)

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| Показ формы | `Showing` | При открытии карточки |
| Закрытие формы | `Closing` | При закрытии карточки |
| Обновление | `Refresh` | При обновлении формы |
| До показа диалога подписания | `ShowingSignDialog` | Только для документов |

### Пример: Showing

```csharp
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);

  // Скрыть поле на новых записях.
  if (_obj.State.IsInserted)
  {
    _obj.State.Properties.RejectReason.IsVisible = false;
    _obj.State.Properties.RejectReason.IsRequired = false;
  }

  // Предупреждение при истёкшем сроке.
  if (_obj.Deadline.HasValue && _obj.Deadline < Calendar.Today)
    e.AddWarning("Срок выполнения истёк.");
}
```

---

## События свойств (Shared)

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| Изменение значения | `<Property>Changed` | При изменении значения свойства |
| Ввод значения | `<Property>ValueInput` | При изменении контрола на форме |
| Фильтрация выбора | `<Property>Filtering` | При фильтрации выпадающего списка |
| Фильтрация при поиске | `<Property>SearchDialogFiltering` | При поиске значения |
| Добавление в коллекцию | `<Property>Added` | При добавлении строки в коллекцию |
| Удаление из коллекции | `<Property>Deleted` | При удалении строки из коллекции |

### Пример: PropertyChanged

```csharp
// Пересчёт суммы при изменении количества.
public virtual void QuantityChanged(Sungero.Domain.Shared.IntegerPropertyChangedEventArgs e)
{
  if (_obj.Quantity.HasValue && _obj.Price.HasValue)
    _obj.TotalAmount = _obj.Quantity.Value * _obj.Price.Value;
}
```

### Пример: Filtering (ссылочное свойство)

```csharp
// Фильтровать список сотрудников по подразделению.
public virtual IQueryable<T> PerformerFiltering(
  IQueryable<T> query,
  Sungero.Domain.PropertyFilteringEventArgs e)
{
  if (_obj.Department != null)
    return query.Where(emp => Equals(emp.Department, _obj.Department));
  return query;
}
```

---

## События действий

| Событие | Имя метода | Когда |
|---------|-----------|-------|
| Выполнение | `<Action>` | При нажатии кнопки |
| Проверка доступности | `Can<Action>` | Определяет, активна ли кнопка |

### Пример: действие с проверкой

```csharp
// Проверка: можно ли выполнить действие.
public virtual bool CanSendForApproval(Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return _obj.Status == MyEntity.Status.Draft &&
         !_obj.State.IsInserted;
}

// Выполнение действия.
public virtual void SendForApproval(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var task = Functions.Module.Remote.CreateApprovalTask(_obj);
  task.Start();
  e.AddInformation("Задача на согласование отправлена.");
}
```

---

## Аргументы событий

### BeforeSave / BeforeDelete (валидация)

| Аргумент | Описание |
|----------|----------|
| `_obj` | Текущая сущность |
| `e.AddError(message)` | Ошибка — блокирует операцию |
| `e.AddError(property, message)` | Ошибка на конкретном свойстве |
| `e.AddWarning(message)` | Предупреждение (не блокирует) |
| `e.AddInformation(message)` | Информационное сообщение |
| `e.IsValid` | `true` если нет ошибок |
| `e.Params` | Дополнительные параметры (для передачи данных) |

### CreatingFrom (копирование)

| Аргумент | Описание |
|----------|----------|
| `_source` | Исходная сущность (копируемая) |
| `_info` | Информация о типе сущности |
| `e.Map(source, target)` | Правило переноса свойств |
| `e.Without(property)` | Исключить свойство из копирования |
| `e.WithoutAccessRights()` | Не копировать права доступа |

### BeforeSaveHistory (история)

| Аргумент | Описание |
|----------|----------|
| `e.Action` | Действие (Create, Update, View, Delete, Sign) |
| `e.Operation` | Логическая операция (перечисление) |
| `e.OperationDetailed` | Детальная информация |
| `e.Comment` | Комментарий к записи |
| `e.Write()` | Записать дополнительную строку в историю |

### BeforeSigning (подписание)

| Аргумент | Описание |
|----------|----------|
| `e.Certificate` | Сертификат подписи |
| `e.Signature` | Подпись |
| `e.SignatureTargetFormat` | Целевой формат усовершенствования |

---

## Передача данных между событиями

### Между событиями одной сущности

Используйте `e.Params` для передачи данных:

```csharp
// В Showing — запомнить значение.
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);
  e.Params.AddOrUpdate("HasIndefiniteDeadline", _obj.HasIndefiniteDeadline == true);
}

// В Refresh — использовать сохранённое значение.
public override void Refresh(Sungero.Domain.Client.RefreshEventArgs e)
{
  base.Refresh(e);
  bool hasIndefiniteDeadline;
  if (e.Params.TryGetValue("HasIndefiniteDeadline", out hasIndefiniteDeadline))
  {
    _obj.State.Properties.Deadline.IsRequired = !hasIndefiniteDeadline;
  }
}
```

### Между событиями разных сущностей

Используйте `entity.Params`:

```csharp
// В событии задачи — пометить документ.
public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
{
  var document = _obj.DocumentGroup.OfficialDocuments.FirstOrDefault();
  if (document != null)
    document.Params["StartedFromApproval"] = true;
}

// В событии документа — проверить пометку.
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  object flag;
  if (_obj.Params.TryGetValue("StartedFromApproval", out flag) && (bool)flag)
  {
    // Не показывать ошибку, если сохранение вызвано из задачи согласования.
    return;
  }
  // Обычная валидация...
}
```

---

## Типичные паттерны

### Условная видимость полей (Showing)

```csharp
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);
  _obj.State.Properties.ApprovalDate.IsVisible = _obj.Status != Status.Draft;
  _obj.State.Properties.RejectReason.IsVisible = _obj.Status == Status.Rejected;
  _obj.State.Properties.RejectReason.IsRequired = _obj.Status == Status.Rejected;
}
```

### Кросс-валидация полей (BeforeSave)

```csharp
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
  base.BeforeSave(e);

  if (_obj.StartDate.HasValue && _obj.EndDate.HasValue &&
      _obj.StartDate > _obj.EndDate)
    e.AddError("Дата начала не может быть позже даты окончания.");

  if (_obj.Amount <= 0)
    e.AddError(_obj.State.Properties.Amount, "Сумма должна быть больше нуля.");
}
```

### Автозаполнение при создании (Created)

```csharp
public override void Created(Sungero.Domain.CreatedEventArgs e)
{
  base.Created(e);
  _obj.Status = MyEntity.Status.Draft;
  _obj.CreatedDate = Calendar.Today;
  _obj.Author = Sungero.Company.Employees.Current;
}
```

### Запрет удаления подписанного документа (BeforeDelete)

```csharp
public override void BeforeDelete(Sungero.Domain.BeforeDeleteEventArgs e)
{
  base.BeforeDelete(e);
  if (_obj.IsSigned)
    e.AddError("Нельзя удалять подписанный документ.");
}
```

### Выдача прав после сохранения (AfterSave)

```csharp
public override void AfterSave(Sungero.Domain.AfterSaveEventArgs e)
{
  base.AfterSave(e);
  if (_obj.Responsible != null && !_obj.AccessRights.CanRead(_obj.Responsible))
    _obj.AccessRights.Grant(_obj.Responsible, DefaultAccessRightsTypes.Read);
}
```

---

*Источники: sds_sobytiia_sushchnosti.htm · sds_sobytiia_formy_sushchnosti.htm · sds_sobytiia_svoistv_sushchnosti.htm · sds_sobytiia_deistvii_sushchnosti.htm · sds_poriadok_vypolneniia_sobytii.htm · sds_peredach_dannykh_mezhdu_sobytiiami_odnoi_sushchnosti.htm · sds_peredach_dannykh_mezhdu_sobytiiami_raznyh_sushchnosti.htm*
