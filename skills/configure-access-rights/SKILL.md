# /configure-access-rights

Настройка прав доступа для сущностей Directum RX: роли, Grant на тип, Grant на экземпляр, AccessRightsMode в .mtd, ComputedRoles.

---

## ШАГ 0: Реальные примеры из CRM

### Роли CRM (ModuleConstants.cs)
```csharp
// DirRX.CRM.Shared/ModuleConstants.cs
public static class Roles
{
  public static readonly Guid CRMAdmin = new Guid("54905cf8-6144-4d30-9efb-9a4df5d73c69");
  public static readonly Guid SalesManager = new Guid("efa0dcef-24de-42ae-962d-ff93062a7a10");
  public static readonly Guid SalesHead = new Guid("9b3974fa-67e8-4c0d-a95c-762874aa3a56");
  public static readonly Guid Marketer = new Guid("cf1cf77f-54bb-4bcc-9d49-48fe10f6679c");
}
```

### Создание ролей в ModuleInitializer (идемпотентно)
```csharp
// DirRX.CRM.Server/ModuleInitializer.cs
public static void CreateRoles()
{
  InitializationLogger.Debug("Init: Create CRM roles.");

  CreateRole(Constants.Module.Roles.CRMAdmin,
    DirRX.CRM.Resources.RoleName_CRMAdmin,
    DirRX.CRM.Resources.RoleDescription_CRMAdmin);

  CreateRole(Constants.Module.Roles.SalesManager,
    DirRX.CRM.Resources.RoleName_SalesManager,
    DirRX.CRM.Resources.RoleDescription_SalesManager);
}

private static void CreateRole(Guid roleGuid, string roleName, string roleDescription)
{
  // Идемпотентность: проверка по Sid
  if (Roles.GetAll().Where(r => r.Sid == roleGuid).Any())
    return;

  InitializationLogger.DebugFormat("Init: Creating role '{0}'.", roleName);

  Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(
    roleName,
    roleDescription,
    roleGuid);
}
```

### Grant прав на ТИП сущности (per-type)
```csharp
// CRM Admin -- полный доступ ко всем справочникам
public static void GrantRightsToCRMAdmin()
{
  var role = Roles.GetAll().Where(r => r.Sid == Constants.Module.Roles.CRMAdmin).FirstOrDefault();
  if (role == null)
    return;

  // Полный доступ к типу (все экземпляры)
  DirRX.CRMSales.Pipelines.AccessRights.Grant(role, DefaultAccessRightsTypes.FullAccess);
  DirRX.CRMSales.Pipelines.AccessRights.Save();

  DirRX.CRMSales.Deals.AccessRights.Grant(role, DefaultAccessRightsTypes.FullAccess);
  DirRX.CRMSales.Deals.AccessRights.Save();

  // Только чтение справочника видов документов
  Sungero.Docflow.DocumentKinds.AccessRights.Grant(role, DefaultAccessRightsTypes.Read);
  Sungero.Docflow.DocumentKinds.AccessRights.Save();
}

// SalesManager -- ограниченные права
public static void GrantRightsToSalesManager()
{
  var role = Roles.GetAll().Where(r => r.Sid == Constants.Module.Roles.SalesManager).FirstOrDefault();
  if (role == null)
    return;

  // Создание (включает чтение + создание новых)
  DirRX.CRMSales.Deals.AccessRights.Grant(role, DefaultAccessRightsTypes.Create);
  DirRX.CRMSales.Deals.AccessRights.Save();

  // Только чтение справочников
  DirRX.CRMSales.Pipelines.AccessRights.Grant(role, DefaultAccessRightsTypes.Read);
  DirRX.CRMSales.Pipelines.AccessRights.Save();
}
```

### Grant прав на ЭКЗЕМПЛЯР (per-instance)
```csharp
// DirRX.CRM.Server/ModuleAsyncHandlers.cs -- выдача прав ответственному на сделку
if (!deal.AccessRights.IsGranted(DefaultAccessRightsTypes.Change, user))
{
  deal.AccessRights.Grant(user, DefaultAccessRightsTypes.Change);
  deal.AccessRights.Save();
}
```

---

## Типы прав (DefaultAccessRightsTypes)

| Константа | Описание | Включает |
|-----------|----------|----------|
| `Read` | Просмотр | Чтение |
| `Change` | Изменение | Чтение + редактирование |
| `Create` | Создание | Чтение + создание новых |
| `FullAccess` | Полный доступ | Все операции включая удаление |

---

## AccessRightsMode в .mtd

В метаданных сущности поле `AccessRightsMode` определяет тип контроля прав:

```json
{
  "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
  "Name": "Invoice",
  "AccessRightsMode": "Both"
}
```

| Значение | Описание |
|----------|----------|
| `Type` | Права только на тип (все экземпляры одинаково) |
| `Instance` | Права на каждый экземпляр отдельно |
| `Both` | И на тип, и на экземпляр (документы, сделки) |

Примеры из CRM:
- `CommercialProposal.mtd` -- `"AccessRightsMode": "Both"` (документ)
- `Invoice.mtd` -- `"AccessRightsMode": "Both"` (документ)
- `ProposalApprovalAssignment.mtd` -- `"AccessRightsMode": "Instance"` (задание)
- `ProposalNotice.mtd` -- `"AccessRightsMode": "Instance"` (уведомление)
- `ProposalApprovalTask.mtd` -- `"AccessRightsMode": "Both"` (задача)

---

## Паттерн: Constants для GUID ролей

**Правило**: GUID роли -- это константа в `ModuleConstants.cs`. Генерируй через `Guid.NewGuid()` один раз.

```csharp
// Shared/ModuleConstants.cs
namespace DirRX.MyModule.Constants
{
  public static class Module
  {
    public static class Roles
    {
      // Генерируй GUID один раз: System.Guid.NewGuid().ToString()
      public static readonly Guid MyCustomRole = new Guid("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
    }
  }
}
```

---

## Паттерн: ModuleInitializer полный цикл

Порядок в `Initializing()`:
1. `CreateRoles()` -- создать роли (идемпотентно по Sid)
2. `CreateDocumentKinds()` -- создать виды документов (если нужно)
3. `GrantRights()` -- выдать права ролям на типы
4. `CreateDefaultData()` -- начальные данные

```csharp
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  CreateRoles();
  GrantRights();
}

public static void CreateRoles()
{
  InitializationLogger.Debug("Init: Create roles.");
  CreateRole(Constants.Module.Roles.MyRole, "Моя роль", "Описание роли");
}

private static void CreateRole(Guid roleGuid, string roleName, string roleDescription)
{
  if (Roles.GetAll().Where(r => r.Sid == roleGuid).Any())
    return;
  InitializationLogger.DebugFormat("Init: Creating role '{0}'.", roleName);
  Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(roleName, roleDescription, roleGuid);
}

public static void GrantRights()
{
  var role = Roles.GetAll().Where(r => r.Sid == Constants.Module.Roles.MyRole).FirstOrDefault();
  if (role == null)
    return;

  // Per-type rights
  MyModule.MyEntities.AccessRights.Grant(role, DefaultAccessRightsTypes.FullAccess);
  MyModule.MyEntities.AccessRights.Save();
}
```

---

## AllowRead, SuppressSecurityEvents, проверка/отзыв прав

→ см. **Guide 6** `knowledge-base/guides/06_access_rights.md`:
- Секция **«AllowRead»** — временное повышение прав (AllowRead pattern)
- Секция **«SuppressSecurityEvents»** — служебные операции без логирования
- Секция **«Права на уровне сущности»** — Grant, Revoke, CanUpdate/CanDelete/CanRead, IsGranted

---

## ComputedRoles (вычисляемые роли)

Динамический список субъектов прав, вычисляемый в runtime:

```csharp
// Получить вычисляемую роль
var role = Sungero.CoreEntities.ComputedRoles.GetAll()
  .FirstOrDefault(r => r.Name == "Руководители подразделений");

// Вычислить субъектов для конкретной сущности
var subjects = role.Compute(entity: document, withAuthorization: true);
```

### Реализация вычисляемой роли
```csharp
public override IEnumerable<IRecipient> Compute(IEntity entity, bool withAuthorization)
{
  var contract = Trade.Contracts.As(entity);
  if (contract == null)
    return Enumerable.Empty<IRecipient>();

  return contract.Experts
    .Where(e => e.Expert != null)
    .Select(e => e.Expert)
    .Cast<IRecipient>();
}
```

---

## Ведущая сущность (EntitySecureLinks)

Наследование прав от родительского объекта:

```csharp
// Установить: вложение наследует права от документа
Sungero.Core.EntitySecureLinks.SetLeadingEntity(
  entity: attachment,
  leadingEntity: parentDocument);

// Получить ведущую сущность
var parent = Sungero.Core.EntitySecureLinks.GetLeadingEntity(attachment);
```

---

## Блоки прав в схеме процесса

| Действие | Описание |
|----------|----------|
| `Action.Add` | Добавить права |
| `Action.Set` | Заменить существующие |
| `Action.DeleteAll` | Удалить все права |

| Тип прав | Описание |
|----------|----------|
| `Type.Read` | Просмотр |
| `Type.Change` | Изменение |
| `Type.FullAccess` | Полный доступ |
| `Type.Forbidden` | Явный запрет |

---

## Чеклист настройки прав

- [ ] Определить роли в `ModuleConstants.cs` (GUID)
- [ ] Локализовать имена ролей в `Module.resx` / `Module.ru.resx`
- [ ] Создать роли в `ModuleInitializer.cs` через `CreateRole()` (идемпотентно)
- [ ] Выдать per-type права в `GrantRights()` (Grant + Save)
- [ ] Установить `AccessRightsMode` в .mtd (`Type`, `Instance`, `Both`)
- [ ] Если нужны per-instance права -- добавить Grant в серверной логике
- [ ] Если нужны вычисляемые роли -- создать ComputedRole

---

## MCP: Валидация

```
MCP: check_permissions          -- проверить матрицу прав модуля
MCP: validate_all               -- полная валидация (включая права)
MCP: check_initializer          -- проверить ModuleInitializer
```

---

## Reference

- **Guide 6**: `knowledge-base/guides/06_access_rights.md`
- **CRM Constants**: `CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Shared/ModuleConstants.cs`
- **CRM Initializer**: `CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Server/ModuleInitializer.cs`
- **CRM AsyncHandler (per-instance)**: `CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Server/ModuleAsyncHandlers.cs`
