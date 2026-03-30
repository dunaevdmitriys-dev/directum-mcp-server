# Права доступа (Access Rights)

> Источник: `webhelp/WebClient/ru-RU/om_accessrights.htm`, `om_prava_dostupa_accessrights.htm`, `Sungero.Domain.xml`

---

## Основные концепции

В Sungero права доступа определяют, что пользователь может делать с объектом.

**Базовые типы прав:**

| Тип | Константа | Описание |
|-----|-----------|----------|
| Просмотр | `DefaultAccessRightsTypes.Read` | Читать запись |
| Изменение | `DefaultAccessRightsTypes.Change` | Редактировать |
| Полный доступ | `DefaultAccessRightsTypes.FullAccess` | Все операции включая удаление |
| Запрет | (специальный) | Явный запрет доступа |

---

## Класс AccessRights

`Sungero.Core.AccessRights` — статический класс для программного управления правами. **Доступен только в серверном коде.**

### Методы

| Метод | Описание |
|-------|----------|
| `AllowRead(Action)` | Выполнить блок кода с правами на просмотр (даже если у пользователя нет прав) |
| `SuppressSecurityEvents(Action)` | Выполнить без логирования событий безопасности |
| `CopyAsync(fromUser, toUser, delegateStrictRights)` | Запустить копирование прав между пользователями |
| `CancelCopy(processId)` | Отменить процесс копирования |
| `CopyingStatus(processId)` | Получить статус процесса копирования |

---

## AllowRead — выполнить без проверки прав

Позволяет временно повысить права до уровня просмотра для выполнения конкретного блока:

```csharp
// Получить данные, на которые у пользователя нет прав.
IEmployee manager = null;
Sungero.Core.AccessRights.AllowRead(() =>
{
  manager = Sungero.Company.Employees.GetAll(e =>
    e.Department.Id == departmentId &&
    e.IsManager == true)
    .FirstOrDefault();
});

// Использовать полученные данные.
if (manager != null)
  Logger.DebugFormat("Руководитель: {0}", manager.Name);
```

```csharp
// Получить документ без проверки прав на него.
IOfficialDocument document = null;
Sungero.Core.AccessRights.AllowRead(() =>
{
  document = Sungero.Docflow.OfficialDocuments.Get(documentId);
});
```

---

## SuppressSecurityEvents — без логирования безопасности

Используется для служебных операций, которые не должны оставлять следов в журнале безопасности:

```csharp
Sungero.Core.AccessRights.SuppressSecurityEvents(() =>
{
  // Служебная операция без записи в лог безопасности.
  var systemDocument = Sungero.Docflow.OfficialDocuments.Get(systemDocId);
  systemDocument.LifeCycleState = Sungero.Docflow.OfficialDocument.LifeCycleState.Archived;
  systemDocument.Save();
});
```

---

## Права на уровне сущности

### Назначение прав

```csharp
// Дать пользователю право на просмотр документа.
document.AccessRights.Grant(
  user,
  DefaultAccessRightsTypes.Read);

// Дать группе право на изменение.
document.AccessRights.Grant(
  group,
  DefaultAccessRightsTypes.Change);

// Дать полный доступ.
document.AccessRights.Grant(
  adminUser,
  DefaultAccessRightsTypes.FullAccess);

// Сохранить изменения прав.
document.AccessRights.Save();
```

### Отзыв прав

```csharp
// Отозвать право у пользователя.
document.AccessRights.Revoke(user, DefaultAccessRightsTypes.Read);
document.AccessRights.Save();
```

### Проверка прав

```csharp
// Проверить: есть ли у текущего пользователя право на изменение.
if (document.AccessRights.CanUpdate())
{
  // Разрешено изменять.
}

// Проверить право на удаление.
if (document.AccessRights.CanDelete())
{
  Sungero.Docflow.OfficialDocuments.Delete(document);
}

// Проверить право на просмотр.
if (document.AccessRights.CanRead())
{
  document.ShowCard();
}
```

---

## Копирование прав между пользователями

Используется при замещении: скопировать права замещаемого пользователя новому.

```csharp
// Запустить асинхронное копирование прав.
var processId = Sungero.Core.AccessRights.CopyAsync(
  fromUser: substitutedEmployee.Login,
  toUser: substituteEmployee.Login,
  delegateStrictRights: false);

// Проверить статус.
var status = Sungero.Core.AccessRights.CopyingStatus(processId);
Logger.DebugFormat("Статус копирования прав: {0}", status);

// Отменить копирование при необходимости.
Sungero.Core.AccessRights.CancelCopy(processId);
```

---

## Проверка карточки пользователя

```csharp
// Проверить, можно ли показать карточку субъекта прав текущему пользователю.
if (role.IsCardVisibleForCurrentUser())
{
  role.ShowCard();
}
```

---

## Вычисляемые роли (ComputedRoles)

Вычисляемые роли определяют динамический список субъектов прав в зависимости от контекста. Используются в схемах бизнес-процессов.

```csharp
// Вычислить субъекты роли для конкретной сущности.
var role = Sungero.CoreEntities.ComputedRoles.GetAll()
  .FirstOrDefault(r => r.Name == "Руководители подразделений");

if (role != null)
{
  var subjects = role.Compute(
    entity: document,
    withAuthorization: true);

  foreach (var subject in subjects)
    Logger.DebugFormat("Субъект роли: {0}", subject.Name);
}
```

### Создание вычисляемой роли через NoCode

Вычисляемые роли создаются в Проводнике → Настройки → Вычисляемые роли. Разработчик реализует логику вычисления в серверном методе `Compute`:

```csharp
// Реализация вычисляемой роли "Эксперты договора".
public override IEnumerable<IRecipient> Compute(IEntity entity, bool withAuthorization)
{
  var document = Sungero.Docflow.OfficialDocuments.As(entity);
  if (document == null)
    return Enumerable.Empty<IRecipient>();

  var contract = Trade.Contracts.As(document);
  if (contract == null)
    return Enumerable.Empty<IRecipient>();

  return contract.Experts
    .Where(e => e.Expert != null)
    .Select(e => e.Expert)
    .Cast<IRecipient>();
}
```

---

## Блоки прав в схеме процесса (AccessRightsSchemeBlocks)

В схемах бизнес-процессов есть специальные блоки для управления правами:

| Действие блока | Описание |
|---------------|----------|
| `Action.Add` | Добавить права |
| `Action.Set` | Установить права (заменить существующие) |
| `Action.DeleteAll` | Удалить все права |

| Тип прав | Описание |
|----------|----------|
| `Type.Read` | Просмотр |
| `Type.Change` | Изменение |
| `Type.FullAccess` | Полный доступ |
| `Type.Forbidden` | Запрет |

---

## Пользователи и роли

```csharp
// Текущий пользователь.
var currentUser = Sungero.CoreEntities.Users.Current;

// Текущий сотрудник.
var currentEmployee = Sungero.Company.Employees.GetAll(e =>
  e.Login != null && e.Login.Id == currentUser.Id)
  .FirstOrDefault();

// Получить всех пользователей группы.
var adminGroup = Sungero.CoreEntities.Groups.GetAll(g =>
  g.Name == "Администраторы").FirstOrDefault();

if (adminGroup != null)
{
  var admins = adminGroup.RecipientLinks
    .Select(rl => rl.Member)
    .ToList();
}

// Проверить: входит ли пользователь в роль.
var isAdmin = Sungero.CoreEntities.Roles.GetAll(r =>
  r.Name == "Администраторы системы")
  .Any(r => r.RecipientLinks.Any(rl => Equals(rl.Member, currentUser)));
```

---

## Замещения (Substitutions)

```csharp
// Получить активные замещения текущего пользователя.
var substitutions = Sungero.CoreEntities.Substitutions.GetAll(s =>
  s.IsActive == true &&
  Equals(s.User, Users.Current));

// Получить всех, кого замещает текущий пользователь.
var substituted = substitutions.Select(s => s.Substitute).ToList();
```

---

## Связи с ведущей сущностью (EntitySecureLinks)

Ведущая сущность — родительский объект, от которого наследуются права:

```csharp
// Установить ведущую сущность (права наследуются от неё).
Sungero.Core.EntitySecureLinks.SetLeadingEntity(
  entity: attachment,
  leadingEntity: parentDocument);

// Получить ведущую сущность.
var parent = Sungero.Core.EntitySecureLinks.GetLeadingEntity(attachment);
```

---

*Источники: om_accessrights.htm · om_prava_dostupa_accessrights.htm · om_accessrightstypes.htm · Sungero.Domain.xml · Sungero.CoreEntities.Server.xml*
