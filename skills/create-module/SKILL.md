---
description: "Создать новый модуль Directum RX с нуля"
---

# Создание модуля Directum RX

Подробнее: docs/platform/REFERENCE_CODE.md

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `analyze_solution action=health` — проверка текущего состояния решения перед добавлением модуля
- `dependency_graph` — визуализация зависимостей и проверка на циклы
- `check_package` — валидация пакета после создания
- `check_code_consistency` — проверка согласованности MTD и C#
- `sync_resx_keys` — синхронизация ключей System.resx из MTD
- `build_dat` — сборка .dat пакета из директории модуля
- `search_metadata scope=modules` — поиск эталонных модулей в платформе

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Посмотри как устроен платформенный модуль:
# Платформенные модули находятся в base/ внутри контейнеров RX
# Используй MCP: search_metadata name=Company для поиска эталонов

# Найди модуль в текущем проекте:
# Glob("{package_path}/source/*/*.Shared/Module.mtd") — найти существующие модули
# MCP: search_metadata scope=modules — поиск эталонных модулей

# Посмотри Dependencies, Cover, AsyncHandlers, Jobs — используй как образец.
```

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании (например, `Acme`)
- **ModuleName** — имя модуля (PascalCase, например, `PurchaseManagement`)
- **SolutionName** — имя решения (по умолчанию = CompanyCode)
- **Dependencies** — зависимости (от каких платформенных модулей зависит)

## Алгоритм

### 1. MCP проверка (СНАЧАЛА проверь состояние решения)

```
MCP: analyze_solution action=health
MCP: dependency_graph action=cycles
```
Убедись что нет конфликтов перед созданием нового модуля.

После создания всех файлов:
```
MCP: check_package packagePath={путь_к_пакету}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: sync_resx_keys packagePath={путь_к_пакету} dryRun=false
MCP: build_dat packagePath={путь_к_пакету}
```

### 2. Сгенерируй GUID
Нужны уникальные GUID для:
- `ModuleGuid` — NameGuid модуля
- `CoverGuid`, `CoverFooterGuid`, `CoverHeaderGuid` — обложка
- `JobGuid` (если нужен job)
- `AsyncHandlerGuid` (если нужен async handler)

### 3. Создай файловую структуру

Размещение: папка проекта В КОРНЕ workspace (не в `projects/`).
Если проект уже существует (например `{ProjectName}/{package-name}/`), клади модуль туда.
Если проект новый — создай `{ProjectName}/` в корне, внутри `{ProjectName}/CLAUDE.md` с описанием.

```
{ProjectName}/
  CLAUDE.md                                    # Описание проекта, модули, статус
  {package-name}/                              # DDS-пакет (например esm-package/)
    source/{CompanyCode}.{ModuleName}/
      {CompanyCode}.{ModuleName}.Server/
        ModuleServerFunctions.cs
        ModuleHandlers.cs
        ModuleInitializer.cs
        ModuleJobs.cs
        ModuleAsyncHandlers.cs
        ModuleBlockHandlers.cs
        ModuleWidgetHandlers.cs
        ModuleQueries.xml
      {CompanyCode}.{ModuleName}.ClientBase/
        ModuleClientFunctions.cs
        ModuleHandlers.cs
      {CompanyCode}.{ModuleName}.Shared/
        Module.mtd
        Module.resx
        Module.ru.resx
        ModuleConstants.cs
        ModuleSharedFunctions.cs
        ModuleStructures.cs
    PackageInfo.xml
```

> **Примечание**: папка `settings/` НЕ входит в стандартную структуру DDS-пакета.
> Если нужна — создавай только при явном требовании.

### 4. Module.mtd

```json
{
  "$type": "Sungero.Metadata.ModuleMetadata, Sungero.Metadata",
  "NameGuid": "<ModuleGuid>",
  "Name": "<ModuleName>",
  "AsyncHandlers": [],
  "ClientAssemblyName": "<CompanyCode>.<ModuleName>.Client",
  "ClientBaseAssemblyName": "<CompanyCode>.<ModuleName>.ClientBase",
  "Code": "<Code>",
  "CompanyCode": "<CompanyCode>",
  "Cover": {
    "NameGuid": "<CoverGuid>",
    "Actions": [],
    "Background": null,
    "Footer": {
      "NameGuid": "<CoverFooterGuid>",
      "BackgroundPosition": "Stretch"
    },
    "Groups": [],
    "Header": {
      "NameGuid": "<CoverHeaderGuid>",
      "BackgroundPosition": "Stretch"
    },
    "RemoteControls": [],
    "Tabs": [
      {
        "NameGuid": "<TabGuid>",
        "Name": "Tab"
      }
    ]
  },
  "Dependencies": [
    { "Id": "e4fe1153-919e-4732-aadc-2c8e9e5bd67d" },
    { "Id": "df83a2ea-8d43-4ec4-a34a-2e61863014df" },
    { "Id": "d534e107-a54d-48ec-85ff-bc44d731a82f" },
    { "Id": "<SolutionGuid>", "IsSolutionModule": true, "MaxVersion": "", "MinVersion": "" }
  ],
  "ExplorerTreeOrder": [],
  "IconResourcesKeys": [],
  "Importance": "Medium",
  "InterfaceAssemblyName": "Sungero.Domain.Interfaces",
  "InterfaceNamespace": "<CompanyCode>.<ModuleName>",
  "IsolatedAssemblyName": "<CompanyCode>.<ModuleName>.Isolated",
  "IsolatedNamespace": "<CompanyCode>.<ModuleName>.Isolated",
  "IsVisible": true,
  "Jobs": [],
  "Libraries": [],
  "Overridden": [
    "IsVisible"
  ],
  "PublicConstants": [],
  "PublicFunctions": [],
  "PublicStructures": [],
  "ResourceInterfaceAssemblyName": "Sungero.Domain.Interfaces",
  "ResourceInterfaceNamespace": "<CompanyCode>.<ModuleName>",
  "ResourcesKeys": [],
  "ServerAssemblyName": "<CompanyCode>.<ModuleName>.Server",
  "ServerNamespace": "<CompanyCode>.<ModuleName>.Server",
  "SharedAssemblyName": "<CompanyCode>.<ModuleName>.Shared",
  "SharedNamespace": "<CompanyCode>.<ModuleName>.Shared",
  "ClientNamespace": "<CompanyCode>.<ModuleName>.Client",
  "ClientBaseNamespace": "<CompanyCode>.<ModuleName>.ClientBase",
  "SpecialFolders": [],
  "Version": "0.0.1.0",
  "Widgets": [],
  "Versions": [
    { "Type": "ModuleMetadata", "Number": 12 },
    { "Type": "DomainApi", "Number": 3 }
  ]
}
```

**Поле `Code`:** максимум 7 символов! Сокращение ModuleName.

**Dependencies — типовые GUID модулей:**
| Модуль | GUID |
|--------|------|
| Shell | `e4fe1153-919e-4732-aadc-2c8e9e5bd67d` |
| Company | `d534e107-a54d-48ec-85ff-bc44d731a82f` |
| Docflow | `df83a2ea-8d43-4ec4-a34a-2e61863014df` |
| Parties | `ec0d4a56-3516-4735-b040-dd22d4de84b4` |
| Commons | `e69f01cf-b6b1-4f3e-b86b-16cc04138bf8` |
| CoreEntities | `11f54776-5c2a-4637-8f37-bfafbb57c9e6` |
| RecordManagement | `4e25caec-c722-4740-bcfd-c4f803840ac6` |
| Contracts | `59887258-7c48-4e28-b749-34f7df12940e` |

### 5. AsyncHandlers (если нужны)

Добавь в `AsyncHandlers` массив Module.mtd:
```json
{
  "NameGuid": "<AsyncHandlerGuid>",
  "Name": "ProcessDocument",
  "DelayPeriod": 15,
  "DelayStrategy": "RegularDelayStrategy",
  "IsHandlerGenerated": true,
  "Parameters": [
    {
      "NameGuid": "<GUID>",
      "Name": "DocumentId",
      "ParameterType": "LongInteger"
    }
  ]
}
```

### 6. Jobs (если нужны)

Добавь в `Jobs` массив Module.mtd:
```json
{
  "NameGuid": "<JobGuid>",
  "Name": "SyncData",
  "GenerateHandler": true,
  "MonthSchedule": "Monthly",
  "StartAt": "1753-01-01T02:00:00",
  "Versions": []
}
```

### 7. Using statements для .cs файлов

**ModuleServerFunctions.cs:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain;
using Sungero.Domain.Shared;

namespace {CompanyCode}.{ModuleName}.Server
{
  partial class ModuleFunctions
  {
  }
}
```

**ModuleInitializer.cs:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain.Initialization;

namespace {CompanyCode}.{ModuleName}.Server
{
  public partial class ModuleInitializer
  {
    public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
    {
      Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
        this.FirstInitializing,
        Constants.Module.Init.{ModuleName}.Name,
        Version.Parse(Constants.Module.Init.{ModuleName}.FirstInitVersion));
    }

    public virtual void FirstInitializing()
    {
      InitializationLogger.Debug("Init: Create roles.");
      // Создание ролей, прав, справочных данных
    }
  }
}
```

**ModuleJobs.cs:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}.Server
{
  public partial class ModuleJobs
  {
  }
}
```

**ModuleAsyncHandlers.cs:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}.Server
{
  public partial class ModuleAsyncHandlers
  {
  }
}
```

**ModuleHandlers.cs (Server):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}
{
  partial class ModuleServerHandlers
  {
  }
}
```

**ModuleClientFunctions.cs:**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}.Client
{
  partial class ModuleFunctions
  {
  }
}
```

**ModuleHandlers.cs (Client):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}
{
  partial class ModuleClientHandlers
  {
  }
}
```

**ModuleConstants.cs (Shared):**
```csharp
using System;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}.Constants
{
  public static class Module
  {
    // Константы модуля

    public static class Init
    {
      public static class {ModuleName}
      {
        public static readonly string Name = "{CompanyCode}.{ModuleName}";
        public static readonly string FirstInitVersion = "0.0.1.0";
      }
    }
  }
}
```

**ModuleSharedFunctions.cs / ModuleStructures.cs (Shared):**
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;

namespace {CompanyCode}.{ModuleName}.Shared
{
  partial class ModuleFunctions
  {
  }
}
```

### 8. PackageInfo.xml (DevelopmentPackageInfo формат)
```xml
<?xml version="1.0"?>
<DevelopmentPackageInfo xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <PackageModuleItem>
      <Id>{SolutionGuid}</Id>
      <Name>{CompanyCode}.Solution</Name>
      <Version>0.0.1.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <PackageModuleItem>
      <Id>{ModuleGuid}</Id>
      <SolutionId>{SolutionGuid}</SolutionId>
      <Name>{CompanyCode}.{ModuleName}</Name>
      <Version>0.0.1.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
  </PackageModules>
</DevelopmentPackageInfo>
```

**Сборка .dat:**
```bash
cd {project_path}
zip -r "{ModuleName}.dat" PackageInfo.xml source/
cp PackageInfo.xml {ModuleName}.xml
```

### 9. System.resx файлы (системные ресурсы модуля)

Создай `ModuleSystem.resx` и `ModuleSystem.ru.resx` в Shared/ для системных ресурсов модуля:
```xml
<!-- Используй формат Property_<PropertyName> для свойств -->
<!-- Используй DisplayName для заголовка модуля -->
<data name="DisplayName" xml:space="preserve"><value>Название модуля</value></data>
```

**КРИТИЧНО**: Для каждой сущности также нужны `{Entity}System.resx` / `{Entity}System.ru.resx` с ключами `Property_<PropertyName>`. Формат `Resource_<GUID>` НЕ работает в runtime DDS 26.1.

### 10. Обложка (Cover) — локализация

Действия обложки используют ресурсы из `Module.resx` / `Module.ru.resx`. Убедись, что:
- Каждое действие обложки (CoverEntityListActionMetadata, CoverFunctionActionMetadata) имеет соответствующий ключ в ResourcesKeys Module.mtd
- Все ключи из ResourcesKeys присутствуют в Module.resx и Module.ru.resx с переводами
- Группы обложки (CoverGroupMetadata) ссылаются на ресурсы через `LocalizedName`

**CoverFunctionActionMetadata** — вызов клиентских функций с обложки:
- Поле `FunctionName` ОБЯЗАНО точно совпадать с именем метода в `ModuleClientFunctions.cs`
- Функция должна быть объявлена как `public virtual void FunctionName()` (без параметров)
- Если функция не найдена — действие упадёт с ошибкой `Can not find method`

### 11. .resx файлы
Пустой шаблон для Module.resx:
```xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
  <resheader name="version"><value>2.0</value></resheader>
  <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
  <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
</root>
```

### 12. Queries.xml (пустой шаблон)
```xml
<?xml version="1.0" encoding="utf-8"?>
<queries>
</queries>
```

## Справочные материалы
DDS known issues и чеклисты → CLAUDE.md
Антипаттерны Claude → /dds-guardrails
Валидация → /validate-all

## Валидация
Запусти /validate-all после создания.

## DDS-правила
Все правила импорта DDS → см. CLAUDE.md (пункты 1-18). Не дублировать здесь.
