# Сущности (Entities) в Sungero

> Источник: `webhelp/WebClient/ru-RU/om_sozdanie_udalenie_izmenenie_sushchnosti.htm`, `om_state.htm`, `om_properties.htm`

---

## Что такое сущность

Сущность (entity) — базовый объект платформы Sungero. Каждый справочник, документ, задача — это сущность. Сущности хранятся в БД и доступны через репозитории (`Sungero.Company.Employees`, `Sungero.Parties.People` и т.д.).

---

## Создание, копирование, удаление

> **Важно:** Create() и Delete() доступны **только в серверном коде**. Вызов в клиентском или общем (Shared) коде вызовет исключение. Изменение свойств доступно в любом коде.

### Create()

```csharp
// Создать персону.
var person = Sungero.Parties.People.Create();

// Заполнить обязательные поля.
person.Name = "Иван";
person.LastName = "Петров";

// Сохранить.
person.Save();
```

### Copy()

```csharp
// Получить сотрудника с ID 5.
var source = Sungero.Company.Employees.Get(5);

// Создать копию.
var copy = Sungero.Company.Employees.Copy(source);
copy.Name = "Копия";
copy.Save();
```

### Delete()

```csharp
// Получить подразделение с ID 15.
var department = Sungero.Company.Departments.Get(15);

// Удалить.
Sungero.Company.Departments.Delete(department);
```

### Save() / Reload()

```csharp
// Сохранить изменения в БД.
entity.Save();

// Перезагрузить свойства из БД (сбросить несохранённые изменения).
entity.Reload();
```

---

## Получение сущностей

### Get(id)

Получить одну сущность по ID:

```csharp
var employee = Sungero.Company.Employees.Get(42);
```

### GetAll()

Получить все записи (LINQ-запрос к БД):

```csharp
// Все активные сотрудники.
var active = Sungero.Company.Employees.GetAll(e => e.Status == Sungero.CoreEntities.DatabookEntry.Status.Active);
```

### GetAllCached()

Получить из кэша (только для кэшируемых справочников). Работает в сервере и клиенте:

```csharp
// Проверить, что справочник кэшируемый.
if (Employees.Info.IsCacheable)
{
  var list = Employees.GetAllCached(e => e.Id > 15);
}
```

```csharp
// Получить действующие сертификаты текущего пользователя.
public static List<ICertificate> GetCertificates()
{
  var now = Calendar.Now;
  return Certificates.GetAllCached(c => Users.Current.Equals(c.Owner) &&
                                         (c.Enabled == true) &&
                                         (!c.NotBefore.HasValue || c.NotBefore <= now) &&
                                         (!c.NotAfter.HasValue || c.NotAfter >= now))
                     .ToList();
}
```

> `GetAllCached` — устаревший метод, оставлен для совместимости. Предпочтительнее `GetAll` с кэшированием через платформенный Cache API.

---

## Валидация

Методы `AddError`, `AddWarning`, `AddInformation` добавляют сообщения пользователю. Обычно вызываются в событиях сущности (Saving, Showing, PropertyChanged).

```csharp
// Ошибка — блокирует сохранение.
e.AddError("Заполните поле «Контрагент».");

// Ошибка на конкретном свойстве.
e.AddError(_obj.State.Properties.Counterparty, "Контрагент обязателен.");

// Предупреждение — не блокирует, но предупреждает.
e.AddWarning("Дата договора в прошлом.");

// Информация — просто сообщение.
e.AddInformation("Документ уже согласован ранее.");
```

Типичный паттерн в событии `Saving`:

```csharp
public override void Saving(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Saving(e);

  if (_obj.Amount <= 0)
    e.AddError(_obj.State.Properties.Amount, "Сумма должна быть больше нуля.");

  if (_obj.Counterparty == null)
    e.AddError(_obj.State.Properties.Counterparty, "Укажите контрагента.");
}
```

---

## Состояние сущности (State)

`entity.State` — информация о текущем статусе объекта в памяти.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `IsInserted` | bool | Новая запись, ещё не сохранена в БД |
| `IsCopied` | bool | Создана копированием |
| `IsChanged` | bool | Есть хотя бы одно изменённое свойство |
| `IsConverted` | bool | Создана конвертацией из другого типа |
| `IsEnabled` | bool | Запись активна (не заблокирована) |
| `IsBinaryDataTransferring` | bool | Идёт передача бинарных данных |

```csharp
// Инициализировать поля только при создании или копировании.
if (_obj.State.IsInserted || _obj.State.IsCopied)
{
  _obj.Status = MyModule.MyEntity.Status.Draft;
  _obj.CreatedDate = Calendar.Today;
}
```

```csharp
// Запретить действие на несохранённой записи.
if (_obj.State.IsInserted)
{
  e.AddError("Сохраните запись перед выполнением этого действия.");
  return;
}
```

---

## Состояние свойств (State.Properties)

Через `entity.State.Properties.<ИмяСвойства>` управляем видимостью, доступностью, обязательностью полей на форме.

| Свойство | Тип | Описание |
|----------|-----|----------|
| `IsVisible` | bool | Поле видимо на форме |
| `IsEnabled` | bool | Поле доступно для редактирования |
| `IsRequired` | bool | Поле обязательно для заполнения |
| `IsChanged` | bool | Значение изменено с момента загрузки |
| `HighlightColor` | Color | Цвет подсветки контрола |
| `OriginalValue` | T | Значение до всех изменений |
| `PreviousValue` | T | Значение до последнего изменения |

```csharp
// Все поля только для чтения.
foreach (var property in _obj.State.Properties)
  property.IsEnabled = false;
```

```csharp
// Управление полями в зависимости от состояния.
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);

  // Новая запись — поле обязательно и редактируемо.
  if (_obj.State.IsInserted)
  {
    _obj.State.Properties.Communication.IsRequired = true;
    _obj.State.Properties.Communication.IsEnabled = true;
  }
  else
  {
    // Существующая — скрыть черновое поле.
    _obj.State.Properties.DraftNote.IsVisible = false;
  }
}
```

### Коллекции в свойствах

```csharp
// Запретить удаление строк из дочерней коллекции.
_obj.State.Properties.Lines.CanDelete = false;

// Какие строки добавлены с момента загрузки.
var newLines = _obj.State.Properties.Lines.Added;

// Какие строки изменены.
var changedLines = _obj.State.Properties.Lines.Changed;
```

---

## Метаданные сущности (Info)

```csharp
// Имя типа сущности.
var typeName = Sungero.Company.Employees.Info.Name;

// Можно ли кэшировать.
var cacheable = Sungero.Company.Employees.Info.IsCacheable;
```

---

## История (History)

```csharp
// Получить историю изменений сущности.
var history = _obj.History.GetAll();

foreach (var record in history)
{
  Logger.Debug($"{record.HistoryDate}: {record.UserName} — {record.Action}");
}
```

---

## Паттерны использования

### CreateIfNotExists (инициализация данных модуля)

```csharp
/// <summary>
/// Создать вид договора «Закупочный», если не существует.
/// </summary>
public static void CreatePurchaseContractKind()
{
  var kind = Docflow.DocumentKinds.GetAll()
    .FirstOrDefault(k => k.ShortName == "Закупочный");

  if (kind == null)
  {
    kind = Docflow.DocumentKinds.Create();
    kind.ShortName = "Закупочный";
    kind.Name = "Договор закупки";
    kind.DocumentType = Contracts.DocumentTypes.ContractualDocumentType;
    kind.Save();
  }
}
```

### Работа через As() — приведение типов

```csharp
// Проверить, является ли документ договором.
var contract = Contracts.Contracts.As(document);
if (contract != null)
{
  // Работаем с типизированным объектом.
  var amount = contract.TotalAmount;
}
```

---

## Полный перечень серверных событий сущности

Все события выполняются на веб-сервере.

### События, общие для всех типов сущностей

| Событие | Метод | Когда | Транзакция |
|---------|-------|-------|------------|
| До сохранения | `BeforeSave` | Перед SQL-транзакцией сохранения | Нет |
| До сохранения (в транзакции) | `Saving` | В транзакции перед записью в БД | Да |
| После сохранения (в транзакции) | `Saved` | В транзакции после записи в БД | Да |
| После сохранения | `AfterSave` | После коммита транзакции | Нет |
| До сохранения истории | `BeforeSaveHistory` | Перед записью в историю | — |
| До удаления | `BeforeDelete` | Перед SQL-транзакцией удаления | Нет |
| До удаления (в транзакции) | `Deleting` | В транзакции перед удалением | Да |
| После удаления | `AfterDelete` | После коммита транзакции | Нет |
| Копирование | `CreatingFrom` | При создании сущности копированием | — |
| Создание | `Created` | При создании новой сущности | — |
| Предварительная фильтрация | `PreFiltering` | При получении списка (оптимизация) | — |
| Фильтрация | `Filtering` | При получении списка | — |

### Дополнительные события для документов

| Событие | Метод | Описание |
|---------|-------|----------|
| До подписания | `BeforeSigning` | Валидация при подписании. Аргументы: `e.Certificate`, `e.Signature`, `e.SignatureTargetFormat` |
| Смена типа | `ConvertingFrom` | Правила переноса свойств при смене типа. `e.Map()`, `e.Without()` |

### Дополнительные события для задач

| Событие | Метод | Описание |
|---------|-------|----------|
| До старта | `BeforeStart` | Валидация перед стартом задачи |
| До рестарта | `BeforeRestart` | Заполнение свойств перед рестартом |
| До возобновления | `BeforeResume` | Логика перед возобновлением |
| До прекращения | `BeforeAbort` | Проверка возможности прекращения |
| После приостановки | `AfterSuspend` | Логика при ошибке (например, уведомление) |

### Дополнительные события для заданий

| Событие | Метод | Описание |
|---------|-------|----------|
| До выполнения | `BeforeComplete` | Валидация перед выполнением задания |

### События для конкурентного задания

| Событие | Метод | Описание |
|---------|-------|----------|
| До взятия в работу | `BeforeStartWork` | Валидация перед взятием в работу |
| До возврата невыполненным | `BeforeReturnUncompleted` | Проверка перед возвратом |

### События для справочников

| Событие | Метод | Описание |
|---------|-------|----------|
| UI-фильтрация | `UIFiltering` | Фильтрация оргструктуры в веб-клиенте |
| Показ подсказки | `GetDigest` | Только для наследников `Sungero.CoreEntities.User` |

---

## События свойств сущности

| Событие | Уровень | Описание |
|---------|---------|----------|
| `<Свойство>Changed` | Shared | Изменение значения свойства (в т.ч. программное). Args: `e.NewValue`, `e.OldValue`, `e.OriginalValue` |
| `<Свойство>ValueInput` | Client | Изменение значения в контроле пользователем. **Не вызывать Remote-функции!** |
| `<Свойство>Filtering` | Server | Фильтрация выбора из списка (для ссылок) или выпадающего списка (для перечислений) |
| `<Свойство>SearchDialogFiltering` | Server | Фильтрация при поиске. Args: `e.EntityType` (GUID типа) |
| `<Свойство>Added` | Shared | Добавление записи в коллекцию. Args: `_added`, `_source` |
| `<Свойство>Deleted` | Shared | Удаление записи из коллекции. Args: `_deleted` |

---

## Клиентские события формы

| Событие | Метод | Описание |
|---------|-------|----------|
| Показ формы | `Showing` | Первоначальная настройка формы. Args: `e.Instruction`, `e.Title`, `e.HideAction()` |
| Закрытие формы | `Closing` | Логика перед закрытием |
| Обновление формы | `Refresh` | Обновление видимости/доступности. **Не вызывать Remote-функции!** |
| До показа диалога подписания | `ShowingSignDialog` | Только для документов. Args: `e.CanApprove`, `e.CanEndorse`, `e.Hint` |

> **Важно:** В событиях `Showing`, `Refresh`, `ValueInput` **не рекомендуется** вызывать `[Remote]`-функции — это приводит к избыточным запросам на сервер.

---

## Блокировки (Locks)

Блокировка — запрет на редактирование объекта для всех пользователей кроме того, кто установил блокировку. Точка входа — класс `Locks`.

```csharp
// Заблокировать сущность.
Locks.Lock(document);

// Попытаться заблокировать (без исключения).
var locked = Locks.TryLock(document);

// Разблокировать.
Locks.Unlock(document);

// Информация о блокировке.
var lockInfo = Locks.GetLockInfo(document);
if (lockInfo.IsLocked)
  Logger.DebugFormat("Заблокировано: {0}, дата: {1}", lockInfo.OwnerName, lockInfo.LockTime);
```

### Блокировка содержимого документа

```csharp
// Заблокировать содержимое (тело) документа.
Locks.Lock(document.LastVersion.Body);
```

### Типичный паттерн TryLock/Unlock

```csharp
var locked = false;
try
{
  locked = Locks.TryLock(document);
  if (locked)
  {
    // Работа с документом...
    document.Save();
  }
  else
  {
    Logger.Warn("Документ заблокирован другим пользователем.");
  }
}
finally
{
  if (locked)
    Locks.Unlock(document);
}
```

---

## Событие CreatingFrom — копирование

```csharp
public override void CreatingFrom(Sungero.Domain.CreatingFromEventArgs e)
{
  base.CreatingFrom(e);

  // Исключить свойство из копирования.
  e.Without(_info.Properties.Note);

  // Переопределить правило копирования.
  e.Map(_info.Properties.Status, _source.Status == Status.Draft ? Status.Draft : Status.Active);

  // Создать копию без прав доступа.
  e.WithoutAccessRights();
}
```

---

## Событие Filtering — фильтрация списков

```csharp
public override IQueryable<T> Filtering(IQueryable<T> query, Sungero.Domain.FilteringEventArgs e)
{
  // Показывать только активные шаблоны при создании документа.
  if (_createFromTemplateContext != null)
  {
    query = query.Where(d => d.Status == Status.Active);

    if (Docflow.OfficialDocuments.Is(_createFromTemplateContext))
    {
      var document = Docflow.OfficialDocuments.As(_createFromTemplateContext);
      query = query.Where(t => t.DocumentKind.Equals(document.DocumentKind));
    }
  }
  return query;
}
```

---

## Событие Created — инициализация при создании

```csharp
public override void Created(Sungero.Domain.CreatedEventArgs e)
{
  base.Created(e);

  // ВАЖНО: Если здесь выдать права через AccessRights.Grant(),
  // автор НЕ получит автоматически полные права.
  // Нужно явно добавить: _obj.AccessRights.Grant(Users.Current, DefaultAccessRightsTypes.FullAccess);
}
```

---

*Источники: om_sozdanie_udalenie_izmenenie_sushchnosti.htm · om_get_getall_getallcached.htm · om_adderror.htm · om_sostoianie_sushchnosti_state.htm · om_sostoianie_svoistva_sushchnosti_properties.htm · sds_sobytiia_sushchnosti.htm · sds_sobytiia_svoistv_sushchnosti.htm · sds_sobytiia_formy_sushchnosti.htm · om_blokirovki.htm*
