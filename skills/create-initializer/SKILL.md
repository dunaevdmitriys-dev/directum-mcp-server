---
description: "Создать ModuleInitializer с версионной инициализацией, ролями, правами и справочниками"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание ModuleInitializer

## ШАГ 0: Посмотри рабочий пример

### Приоритет 1 — DTCommons (эталон версионного initializer, 678 строк, 5 версий)

| Файл | Путь |
|------|------|
| **ModuleInitializer (эталон, 29KB)** | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Server/ModuleInitializer.cs` |
| **ModuleConstants (роли, версии)** | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Shared/ModuleConstants.cs` |
| **Targets Initializer (9KB)** | `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleInitializer.cs` |
| **Документация** | `targets/CODE_PATTERNS_CATALOG.md` секция 4 |

**Ключевые паттерны DTCommons:**

```csharp
// Версионная инициализация через ModuleVersionInit
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  var init = Sungero.Docflow.PublicInitializationFunctions.Module.Remote
    .ModuleVersionInit(Constants.Module.InitVersions.Name);
  // v1.1 — создание ролей и базовых справочников
  if (!init.ContainsKey(Constants.Module.InitVersions.V1_1))
    InitV1_1();
  // v1.2 — добавление периодов и прав
  if (!init.ContainsKey(Constants.Module.InitVersions.V1_2))
    InitV1_2();
  // v1.3-1.5 — миграция данных, обновление прав
  // ...
}
```

```csharp
// ExternalLink для предопределенных записей (идемпотентно!)
private void CreatePeriod(string name, Enumeration measure, int amount, int relativeSize)
{
  var externalLink = Sungero.Docflow.PublicFunctions.Module.GetExternalLink(
    typeof(IDTCommonsPeriod), Guid.Parse(periodGuid));
  if (externalLink != null) return; // уже создан

  var period = DTCommonsPeriods.Create();
  period.Name = name;
  period.Measure = measure;
  period.Save();
  Sungero.Docflow.PublicFunctions.Module.CreateExternalLink(
    period, Guid.Parse(periodGuid));
}
```

```csharp
// Cross-module гранты (из DTCommons выдаются права на Targets, KPI)
Sungero.Docflow.PublicInitializationFunctions.Module.GrantRightOnType(
  DirRX.Targets.Targets.Info, role, DefaultAccessRightsTypes.Read);
```

### Приоритет 2 — CRM (простой initializer, 449 строк)

**Эталон: DirRX.CRM.Server/ModuleInitializer.cs** — 449 строк, 4 роли, 2 вида документов, права на 15+ типов, начальные данные (воронка, этапы, причины проигрыша, источники лидов).

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **ModuleInitializer (полный)** | `DirRX.CRM/DirRX.CRM.Server/ModuleInitializer.cs` |
| **ModuleConstants (роли, GUID-ы)** | `DirRX.CRM/DirRX.CRM.Shared/ModuleConstants.cs` |
| **ModuleInitializer (пустой)** | `DirRX.CRMDocuments/DirRX.CRMDocuments.Server/ModuleInitializer.cs` |

**Реальная структура Initializing из CRM:**
```csharp
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  // v7.0 — First initialization (roles, document kinds, rights, default data)
  FirstInitializing();
  // v8.0 — WebAPI endpoints, enhanced initialization
  InitializingV80();
}

private void FirstInitializing()
{
  CreateRoles();
  CreateDocumentKinds();
  GrantRights();
  CreateDefaultPipeline();
  CreateDefaultLossReasons();
  CreateDefaultLeadSources();
}
```

**Реальное создание роли (идемпотентно):**
```csharp
private static void CreateRole(Guid roleGuid, string roleName, string roleDescription)
{
  if (Roles.GetAll().Where(r => r.Sid == roleGuid).Any())
    return;

  InitializationLogger.DebugFormat("Init: Creating role '{0}'.", roleName);
  Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(roleName, roleDescription, roleGuid);
}
```

**Реальная выдача прав (из GrantRightsToCRMAdmin):**
```csharp
var role = Roles.GetAll().Where(r => r.Sid == Constants.Module.Roles.CRMAdmin).FirstOrDefault();
if (role == null) return;

DirRX.CRMSales.Pipelines.AccessRights.Grant(role, DefaultAccessRightsTypes.FullAccess);
DirRX.CRMSales.Pipelines.AccessRights.Save();

DirRX.CRMSales.Deals.AccessRights.Grant(role, DefaultAccessRightsTypes.Create);
DirRX.CRMSales.Deals.AccessRights.Save();
```

**Реальные Constants (из ModuleConstants.cs):**
```csharp
namespace DirRX.CRM.Constants
{
  public static class Module
  {
    public static class Roles
    {
      public static readonly Guid CRMAdmin = new Guid("54905cf8-6144-4d30-9efb-9a4df5d73c69");
      public static readonly Guid SalesManager = new Guid("efa0dcef-24de-42ae-962d-ff93062a7a10");
      public static readonly Guid SalesHead = new Guid("9b3974fa-67e8-4c0d-a95c-762874aa3a56");
      public static readonly Guid Marketer = new Guid("cf1cf77f-54bb-4bcc-9d49-48fe10f6679c");
    }
  }
}
```

**Реальное создание начальных данных (идемпотентно):**
```csharp
private static void CreateStageIfNotExists(
  DirRX.CRMSales.IPipeline pipeline,
  string name, int position, int probability,
  string color, bool isFinal, bool isWon, int maxIdleDays)
{
  var exists = DirRX.CRMSales.Stages.GetAll()
    .Where(s => s.Pipeline != null && s.Pipeline.Id == pipeline.Id && s.Name == name)
    .Any();
  if (exists) return;

  var stage = DirRX.CRMSales.Stages.Create();
  stage.Pipeline = pipeline;
  stage.Name = name;
  stage.Position = position;
  stage.Save();
}
```

Перед созданием нового инициализатора — **обязательно прочитай** `ModuleInitializer.cs` из DirRX.CRM.

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `check_code_consistency` — проверка согласованности MTD Constants и C# ModuleInitializer
- `check_package` — валидация пакета после создания
- `check_permissions` — проверка AccessRights в MTD: пустые права, дубликаты, неизвестные роли
- `sync_resx_keys` — синхронизация ключей resx (ресурсы ролей, справочников)
- `analyze_solution action=health` — проверка здоровья решения после инициализации

## Входные данные

Спроси у пользователя (если не указано):
- **CompanyCode** — код компании
- **ModuleName** — имя модуля
- **Roles** — роли для создания (имя, описание)
- **AccessRights** — какие права на какие типы для каких ролей
- **DefaultData** — справочники/типы для создания по умолчанию
- **Versions** — список версий инициализации (опционально)

## Что создаётся

```
source/{Company}.{Module}/
  {Company}.{Module}.Server/
    ModuleInitializer.cs            # Инициализатор с версионированием
  {Company}.{Module}.Shared/
    ModuleConstants.cs              # Константы ролей, версий, GUID-ов
    Module.resx / Module.ru.resx    # Ресурсы (имена ролей и т.д.)
```

## Алгоритм

### 0. MCP валидация (ПОСЛЕ создания)

После создания ModuleInitializer.cs и ModuleConstants.cs:
```
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: check_package packagePath={путь_к_пакету}
MCP: check_permissions path={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
```

### 1. ModuleInitializer.cs (паттерн из DirRX.CRM)

```csharp
// Реальный паттерн: CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Server/ModuleInitializer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain.Initialization;

namespace {Company}.{Module}.Server
{
  public partial class ModuleInitializer
  {
    public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
    {
      // v1.0 — First initialization
      FirstInitializing();
    }

    private void FirstInitializing()
    {
      CreateRoles();
      GrantRights();
      CreateDefaultData();
    }

    #region Создание ролей

    public static void CreateRoles()
    {
      InitializationLogger.Debug("Init: Create roles.");

      CreateRole(Constants.Module.Roles.{RoleName},
        {Company}.{Module}.Resources.RoleName_{RoleName},
        {Company}.{Module}.Resources.RoleDescription_{RoleName});
    }

    // Идемпотентно — проверка по Sid (как в CRM)
    private static void CreateRole(Guid roleGuid, string roleName, string roleDescription)
    {
      if (Roles.GetAll().Where(r => r.Sid == roleGuid).Any())
        return;

      InitializationLogger.DebugFormat("Init: Creating role '{0}'.", roleName);
      Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(roleName, roleDescription, roleGuid);
    }

    #endregion

    #region Выдача прав

    public static void GrantRights()
    {
      InitializationLogger.Debug("Init: Grant rights.");

      var role = Roles.GetAll().Where(r => r.Sid == Constants.Module.Roles.{RoleName}).FirstOrDefault();
      if (role == null)
        return;

      // Паттерн: Grant + Save для каждого типа отдельно
      {Company}.{Module}.MyEntities.AccessRights.Grant(role, DefaultAccessRightsTypes.FullAccess);
      {Company}.{Module}.MyEntities.AccessRights.Save();

      {Company}.{Module}.MyDatabooks.AccessRights.Grant(role, DefaultAccessRightsTypes.Read);
      {Company}.{Module}.MyDatabooks.AccessRights.Save();
    }

    #endregion

    #region Начальные данные

    public static void CreateDefaultData()
    {
      InitializationLogger.Debug("Init: Create default data.");

      // Идемпотентно — проверка по Name
      CreateIfNotExists("Значение 1");
      CreateIfNotExists("Значение 2");
    }

    private static void CreateIfNotExists(string name)
    {
      if ({Company}.{Module}.MyEntities.GetAll().Where(e => e.Name == name).Any())
        return;
      InitializationLogger.DebugFormat("Init: Creating '{0}'.", name);
      var entity = {Company}.{Module}.MyEntities.Create();
      entity.Name = name;
      entity.Save();
    }

    #endregion
  }
}
```

### 2. Версионная инициализация (ESM-паттерн)

```csharp
public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
{
  Dictionary<long, byte[]> licenses = null;
  try
  {
    licenses = Sungero.Docflow.PublicFunctions.Module.ReadLicense();
    Sungero.Docflow.PublicFunctions.Module.DeleteLicense();

    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.FirstInitializing,
      Constants.Module.Init.ModuleName,
      Version.Parse(Constants.Module.Init.FirstVersion));

    Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
      this.Initializing_v2,
      Constants.Module.Init.ModuleName,
      Version.Parse(Constants.Module.Init.Version2));
  }
  finally
  {
    Sungero.Docflow.PublicFunctions.Module.RestoreLicense(licenses);
  }
}

public virtual void FirstInitializing()
{
  CreateRoles();
  GrantAccessRightsToRoles();
  CreateDefaultData();
}

public virtual void Initializing_v2()
{
  // Добавления/изменения в версии 2
  CreateNewEntityTypes();
  UpdateExistingRoles();
}
```

### 3. Constants (паттерн из DirRX.CRM/ModuleConstants.cs)

```csharp
// Реальный паттерн: CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Shared/ModuleConstants.cs
namespace {Company}.{Module}.Constants
{
  public static class Module
  {
    // GUID ролей — new Guid(...), НЕ Guid.Parse(...)
    public static class Roles
    {
      public static readonly Guid {RoleName} = new Guid("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
    }

    // Дополнительные константы (GUID типов документов, пороги, цвета)
    public static class DocumentTypeGuids
    {
      public const string MyDocType = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
    }

    public const int DefaultThresholdDays = 14;
  }
}
```

**ВАЖНО:** В CRM используется `new Guid(...)`, а не `Guid.Parse(...)`. Оба валидны, но следуй стилю проекта.

### 4. Ресурсы

**Module.resx:**
```xml
<data name="RoleName_{RoleName}" xml:space="preserve"><value>{RoleName}</value></data>
<data name="RoleDescription_{RoleName}" xml:space="preserve"><value>Role for managing ...</value></data>
<data name="StatusActive" xml:space="preserve"><value>Active</value></data>
<data name="StatusClosed" xml:space="preserve"><value>Closed</value></data>
```

## Валидация

- [ ] ModuleInitializer — `public partial class` БЕЗ базового класса
- [ ] `using Sungero.Domain.Initialization;` для `InitializationLogger`
- [ ] Ресурсы через полный путь: `{Company}.{Module}.Resources.Key`
- [ ] `Roles.GetAll(r => r.Sid == guid)` — поиск роли по Sid (Guid)
- [ ] Idempotent: проверка `if (Roles.GetAll().Any())` перед созданием
- [ ] `role.IsSystem = true` для системных ролей
- [ ] Constants = `public static class` (НЕ partial!)
- [ ] `[Public]` на Guid-ах которые нужны из других модулей

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- После создания артефакта: `/validate-all`
