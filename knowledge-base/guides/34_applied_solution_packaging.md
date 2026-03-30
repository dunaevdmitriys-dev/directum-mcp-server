# 34. Applied Solution Packaging

Упаковка, деплой и инсталляция прикладных решений Directum RX через `.dat`-пакеты и Python-плагины DirectumLauncher.

---

## 1. Обзор: .dat пакет прикладного решения

`.dat`-файл — это пакет прикладного решения Directum RX, создаваемый через DDS (DevelopmentStudio) или `export-package` в DeploymentToolCore. Содержит скомпилированные сборки, исходники, метаданные и инициализаторы модулей решения. Деплоится через `DeploymentTool deploy --package=...`.

Каждое прикладное решение (OmniApplied, Targets, CRM и т.д.) поставляется как `.dat` + Python-плагин для DirectumLauncher. Плагин автоматизирует полный цикл: установка зависимостей, публикация `.dat`, инициализация настроек, перезапуск сервисов.

---

## 2. Структура .dat

Пакет `.dat` — это ZIP-архив со следующей структурой:

```
MySolution.dat (ZIP)
  shared/           # Shared-сборки (общие для сервера и клиента)
  server/           # Server-сборки и код
  client/           # Client-сборки и статика
  isolated/         # IsolatedArea-сборки (сторонние библиотеки)
  source/           # Исходный код (.mtd, .resx, .cs)
    DirRX.MySolution/
      DirRX.MySolution.Server/
      DirRX.MySolution.ClientBase/
      DirRX.MySolution.Shared/
      Module/
        Module.mtd
        ModuleServerFunctions.cs
        ...
  *.dll             # Корневые сборки решения
```

**Ключевые моменты:**
- `source/` включается при `IncludeSources=true` в PackageInfo.xml
- `IncludeAssemblies=true` добавляет DLL в соответствующие папки
- Структура `source/` идентична структуре проекта в DDS IDE

---

## 3. PackageInfo.xml

Файл `PackageInfo.xml` (в XML-терминологии `DevelopmentPackageInfo`) описывает состав пакета — какие модули включены, их версии и роли.

### Пример: Targets (5 модулей)

```xml
<?xml version="1.0" encoding="utf-8"?>
<DevelopmentPackageInfo>
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <!-- Модуль-решение (корневой) -->
    <PackageModuleItem>
      <Id>a379a7fe-0606-4c4d-92d6-ea986d3b652a</Id>
      <Name>DirRX.DirectumTargets</Name>
      <Version>1.5.261.6</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>true</IsPreviousLayerModule>
    </PackageModuleItem>

    <!-- Дочерний модуль -->
    <PackageModuleItem>
      <Id>c0039791-537c-47d4-ab25-8d68e616b53a</Id>
      <SolutionId>a379a7fe-0606-4c4d-92d6-ea986d3b652a</SolutionId>
      <Name>DirRX.DTCommons</Name>
      <Version>1.5.261.6</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <!-- ... DirRX.KPI, DirRX.Targets, DirRX.TargetsAndKPIsUI -->
  </PackageModules>
</DevelopmentPackageInfo>
```

### Пример: OmniApplied (2 модуля)

```xml
<DevelopmentPackageInfo>
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <PackageModuleItem>
      <Id>27979c25-978a-4779-b2bc-e3238ceffc81</Id>
      <Name>Sungero.Omni</Name>
      <Version>26.1.0.15</Version>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>true</IsPreviousLayerModule>
    </PackageModuleItem>
    <PackageModuleItem>
      <Id>39c3f1ab-3b77-4b5f-8f2e-388d080002d0</Id>
      <SolutionId>27979c25-978a-4779-b2bc-e3238ceffc81</SolutionId>
      <Name>Sungero.OmniIntegration</Name>
      <Version>26.1.0.15</Version>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>true</IsPreviousLayerModule>
    </PackageModuleItem>
  </PackageModules>
</DevelopmentPackageInfo>
```

### Поля PackageModuleItem

| Поле | Описание |
|------|----------|
| `Id` | GUID модуля (уникальный идентификатор) |
| `SolutionId` | GUID родительского решения (для дочерних модулей) |
| `Name` | Полное имя модуля (`Namespace.ModuleName`) |
| `Version` | Версия в формате `Major.Minor.Build.Revision` |
| `IncludeAssemblies` | Включать DLL в пакет |
| `IncludeSources` | Включать исходники (.mtd, .resx, .cs) |
| `IsSolution` | `true` — это корневой модуль-решение |
| `IsPreviousLayerModule` | `true` — модуль предыдущего слоя (платформенный / базовый) |

**Правила:**
- Ровно один `PackageModuleItem` с `IsSolution=true` — корень решения
- Остальные модули ссылаются на него через `SolutionId`
- `IsPreviousLayerModule=true` означает, что модуль является слоем, на который можно наследоваться в перекрытиях

---

## 4. manifest.json

Манифест компоненты для DirectumLauncher. Определяет метаданные, зависимости и путь к Python-плагину.

### Пример: Targets

```json
{
  "name": "targets",
  "version": "1.5.261.6",
  "description": {
    "en": "Directum Targets",
    "ru": "Directum Targets"
  },
  "plugin_path": "./targets_plugin",
  "dependencies": {
    "base": ">=26"
  }
}
```

### Пример: OmniApplied

```json
{
  "name": "omniapplied",
  "version": "26.1.0.15",
  "description": {
    "en": "Omni Applied Component",
    "ru": "Прикладная разработка Omni"
  },
  "runtime": "linux",
  "plugin_path": "./omni_plugin",
  "dependencies": {
    "Platform": ">=26.1",
    "DirectumRX": ">=26.1"
  }
}
```

### Поля manifest.json

| Поле | Описание |
|------|----------|
| `name` | Алиас компоненты (совпадает с `COMPONENT_ALIAS` в Python) |
| `version` | Версия компоненты (совпадает с версией в PackageInfo.xml) |
| `description` | Локализованное описание (`en`, `ru`) |
| `runtime` | Целевая ОС (`linux` — опционально) |
| `plugin_path` | Относительный путь к Python-плагину |
| `dependencies` | Зависимости: `имя_компоненты: ">=версия"` |

**Различия OmniApplied vs Targets:**
- OmniApplied указывает `runtime: "linux"` явно
- OmniApplied зависит от `Platform` и `DirectumRX` (конкретнее)
- Targets зависит от `base` (обобщённо)

---

## 5. BaseRnDComponent installer: жизненный цикл

`BaseRnDComponent` — абстрактный базовый класс для прикладных решений команды RnD. Наследует `BaseComponent` из платформы DirectumLauncher.

### Полный цикл install

```
install(**kwargs)
  |
  +-- parse kwargs: only_client, force_install, do_not_init
  |
  +-- get_install_steps_v2(mode="console_install")
  |     |
  |     +-- [?зависимости]          # Targets: PivotTable.install
  |     +-- deploy_step             # DeploymentTool deploy --package=... --init --settings
  |     +-- [?webclient_up]         # SungeroWebClient up (только в UI-режиме)
  |     +-- [?config_mutation]      # OmniApplied: omni_update_config_settings
  |
  +-- steps_executor.execute_steps(install_steps)
  |
  +-- log success
```

### Конфигурационные константы наследника

Каждый наследник устанавливает в конструкторе:

```python
self.COMPONENT_ALIAS = 'targets'              # алиас для @component(alias=...)
self.PACKAGE_NAME = 'DirRX.DirectumTargets'    # имя решения RX
self.PACKAGE_FILE_NAME = 'DirectumTargets.dat' # имя .dat файла
self.STATIC_URL_PATH = 'targets'               # URL-путь статики
self.UI_INSTALL_BY_DEFAULT = True              # чекбокс в UI Launcher
self.LOCALIZATION_PREFIX = "targets"           # префикс i18n ключей
self.INTEGRATION_SERVICE_SOLUTION = "DirectumTargets"  # решение в СИ
```

### Формирование deploy-команды

```python
deploy_package_path = f"{self.get_install_package_path()};{self.additional_packages}"
dt_init_arg = "--init=False" if self._deploy_without_init_and_settings else "--init"
dt_settings_arg = "--settings=False" if self._deploy_without_init_and_settings else "--settings"
dt_force_arg = "--force" if self._force_install else "--force=False"

# Результат:
# DeploymentTool deploy --package='RX.dat;MySolution.dat' --init --settings --force=False
```

### Аргументы install

| Аргумент | Описание |
|----------|----------|
| `--only_client` | Установить только клиентскую часть (статику) |
| `--force_install` | Принудительная публикация (даже если версия не изменилась) |
| `--do_not_init` | Без инициализации и настроек (опасно при первой установке) |
| `--package` | Кастомный путь к пакету |
| `--package_from_component` | Имена компонент для поиска пакета |

### Uninstall

```python
def uninstall(self, only_client=False):
    if not only_client:
        DeploymentTool().remove_solutions(self._package_name)  # Удалить из БД
    self.remove_static_paths()                                  # Удалить статику
```

### Check (healthcheck)

Проверка доступности через OData endpoint сервиса интеграции:

```python
url = f"https://{tenant.dns}/integration/odata/{self._integration_service_solution}/Check"
resp = send_request(url, attempt_count=3, auth=(user, password))
```

---

## 6. Deploy chain: multi-package через точку с запятой

Ключевая особенность деплоя прикладных решений — составной путь пакетов через `;`. DeploymentTool обрабатывает их последовательно, разворачивая каждый `.dat` в указанном порядке.

### Формирование пути

```python
# Базовый путь (BaseRnDComponent)
def get_install_package_path(self):
    return os.path.join(self._component_path, self._package_file_name)

# Переопределение в OmniApplied/Targets — добавляет RX.dat
def get_install_package_path(self):
    Base = Action.get_class("rx")
    rx_path = Base.get_applied_package_file_name()       # RX.dat
    package_path = os.path.join(self._component_path, self._package_file_name)  # MyApp.dat
    package_path = f"{rx_path};{package_path}"           # RX.dat;MyApp.dat
    extra = self._get_extra_package_path()
    if extra:
        package_path = f"{package_path};{extra}"         # RX.dat;MyApp.dat;Extra.dat
    return package_path
```

### Итоговые пути

| Решение | Deploy chain |
|---------|-------------|
| OmniApplied | `RX.dat;OmniApplied.dat[;OmniApplied.dat из omni-компоненты]` |
| Targets | `RX.dat;DirectumTargets.dat[;DirectumTargets.dat из targets-компоненты]` |

**Важно:** `RX.dat` всегда идёт первым — это базовая платформенная прикладная разработка, от которой зависят все решения.

---

## 7. Smart resolution: SungeroDB проверка развёрнутых решений

Перед деплоем инсталлятор проверяет, какие решения уже развёрнуты в БД. Если решение уже задеплоено, но отсутствует в текущем пакете, его `.dat` добавляется из отдельной компоненты.

### Алгоритм (одинаков для Omni и Targets)

```python
def _get_extra_package_path(self):
    config_path = get_default_config_path()
    db = SungeroDB(config_path)

    # 1. Если БД не существует — первая установка, доп. пакет не нужен
    if not db.is_db_exist(db.db_name):
        return None

    # 2. Получить развёрнутые решения из БД
    deployed_solutions = get_solutions_from_db(config_path)

    # 3. Получить решения из текущего пакета
    package_path = os.path.join(self._component_path, self._package_file_name)
    solutions_from_packages = get_applied_solutions_info_from_packages(package_path)

    # 4. Если решение в БД, но НЕ в текущем пакете — добавить из отдельной компоненты
    deployed_names = set(solution.Name for solution in deployed_solutions)
    if self.SOLUTION_NAME in deployed_names and self.SOLUTION_NAME not in solutions_from_packages:
        component_folder = ComponentManager.get_component_folder(self.FOLDER_NAME)
        return os.path.join(component_folder, self.PACKAGE_FILE_NAME)

    return None
```

### Зачем это нужно

Сценарий: OmniApplied обновляется, но решение `Sungero.Omni` уже в БД из другой компоненты (напр., отдельного пакета Omni). DeploymentTool требует все решения при обновлении — иначе потеряет данные. Smart resolution автоматически подтягивает недостающий `.dat`.

### Используемые функции платформы

| Функция | Источник | Назначение |
|---------|----------|------------|
| `get_solutions_from_db(config_path)` | `platform_plugin.check_incompatibility` | Список решений из таблицы БД |
| `get_applied_solutions_info_from_packages(path)` | `platform_plugin.check_incompatibility` | Список решений из .dat файла |
| `SungeroDB(config_path)` | `sungero_deploy.tools.sungerodb` | Работа с БД Sungero |
| `ComponentManager.get_component_folder(name)` | `components.component_manager` | Путь к папке компоненты |

---

## 8. Config mutation: omni_update_config_settings

OmniApplied мутирует `config.yml` после деплоя — добавляет GUID прикладных действий в `DISALLOWED_ENTITY_ACTION_IDS` сервиса `SungeroPublicApi`.

### Механизм

```python
@action
@Help.hide
def omni_update_config_settings(config_path: str, action_ids: list[str]) -> None:
    yaml_config = yaml_tools.load_yaml_from_file(config_path)
    services_config = yaml_config.get('services_config')
    if not services_config:
        return

    # Находим секцию SungeroPublicApi по qualified name сервиса
    public_api_settings = services_config.get(public_api_service.instance_service.service_qualified_name)
    if not public_api_settings:
        return

    # Объединяем существующие и новые ID через set union
    existing_ids = public_api_settings.get('DISALLOWED_ENTITY_ACTION_IDS', '')
    action_ids_set = set(existing_ids.split(";")) if len(existing_ids) > 0 else set()
    action_ids_set |= set(action_ids)

    # Записываем обратно через ; разделитель
    public_api_settings.update({'DISALLOWED_ENTITY_ACTION_IDS': ";".join(action_ids_set)})
    yaml_tools.yaml_dump_to_file(yaml_config, config_path)
```

### Интеграция в install pipeline

```python
# В OmniAppliedComponent.get_install_steps_v2():
steps = super().get_install_steps_v2(data)  # [deploy_step, ?webclient_up]
steps.append(InstallStep(
    display_name=_("omniapplied.ui.update_config_settings"),
    action=get_action_str(omni_update_config_settings,
                          config_path=data.config_path,
                          action_ids=self.DISALLOWED_ENTITY_ACTION_IDS)
))
```

OmniApplied содержит список из 17 GUID действий для блокировки. Каждый запуск идемпотентен — `set union` гарантирует отсутствие дублей.

---

## 9. Install-time зависимости: PivotTable перед Targets

Targets — единственный пример с runtime-зависимостью на другую компоненту. PivotTable устанавливается **первым шагом**, до деплоя самого Targets.

### Механизм

```python
# targets_installer.py
from pivottable_plugin.pivottable_installer import PivotTableComponent

def get_install_steps_v2(self, data):
    steps = super().get_install_steps_v2(data)  # [deploy_step, ?webclient_up]

    # PivotTable ПЕРЕД всеми шагами
    install_pivot_step = [InstallStep(
        display_name=_("targets.ui.installing_pivot"),
        action=get_action_str(PivotTableComponent,
                              config_path=self.config_path,
                              _args="install")
    )]
    steps = install_pivot_step + steps  # [pivot, deploy, ?webclient]
    return steps
```

### Порядок шагов Targets

1. `PivotTableComponent.install` — установка зависимости
2. `DeploymentTool deploy --package='RX.dat;DirectumTargets.dat' --init --settings` — деплой .dat
3. `SungeroWebClient up` — перезапуск веб-клиента (только UI-режим)

### OmniApplied — неявная зависимость на RX

OmniApplied не импортирует другие компоненты напрямую, но зависит от `RX.dat` через `get_install_package_path()`:

```python
Base = Action.get_class("rx")        # Получить класс RX-компоненты
rx_path = Base.get_applied_package_file_name()  # Путь к RX.dat
package_path = f"{rx_path};{package_path}"      # RX первый в цепочке
```

---

## 10. PublicAPI support: sungero_public_api_up(), _find_service

Targets-версия `base_component.py` расширена поддержкой PublicAPI — создание секции в `config.yml` и запуск сервиса.

### Флаг UP_PUBLIC_API

```python
# base_component.py (Targets-версия)
self.UP_PUBLIC_API = False          # По умолчанию выключен
self.SUNGERO_PUBLIC_API = "SungeroPublicApi"  # Имя сервиса
```

В `TargetsComponent`:
```python
self.UP_PUBLIC_API = True  # Targets включает PublicAPI
```

### sungero_public_api_up()

Создаёт секцию `SungeroPublicApi` в `config.yml` и поднимает сервис:

```python
def sungero_public_api_up(self):
    yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
    services_config = yaml_config["services_config"]
    common_config = yaml_config["common_config"]
    public_api_config = services_config.get(self.SUNGERO_PUBLIC_API)

    if not public_api_config:
        public_api_config = CommentedMap()
        web_host_path_base = common_config.get("WEB_HOST_PATH_BASE")
        services_config[self.SUNGERO_PUBLIC_API] = public_api_config

        # Порт (auto = выбирается автоматически)
        public_api_config["PUBLIC_API_HOST_HTTP_PORT"] = None

        # URL-ы через YAML-шаблоны
        public_api_config["WEB_API_HOST_URI"] = \
            f'{{{{ protocol }}}}://{{{{ host_fqdn }}}}/{web_host_path_base}/api'
        public_api_config["PUBLIC_API_HOST_URI"] = \
            f'{{{{ protocol }}}}://{{{{ host_fqdn }}}}/{web_host_path_base}/api/public'

        # YAML merge anchor для logs
        logs = yaml_config.get('logs_path')
        services_config[self.SUNGERO_PUBLIC_API].add_yaml_merge([(0, logs)])

        yaml_tools.yaml_dump_to_file(yaml_config, self.config_path)

    # Поднять сервис
    config = get_config_model(self.config_path)
    self._service_up(self.SUNGERO_PUBLIC_API, config)
```

### _find_service() / _service_up()

Универсальные методы для поиска и запуска сервисов через `service_finder`:

```python
def _find_service(self, service_name: str) -> Optional[Type[ServiceContract]]:
    services = service_finder.get_all_services(base_type=service_finder.ServiceBaseTypes.Service)
    return next((s for s in services if s.__name__ == service_name), None)

def _service_up(self, service_name: str, config: Config) -> None:
    service_class = self._find_service(service_name)
    if service_class:
        service = service_class(config)
        service.up()
```

---

## 11. Различия base_component.py: Omni (15KB) vs Targets (17KB + PublicAPI)

Оба файла содержат комментарий: *"ВНИМАНИЕ. Содержать базовый компонент одинаковым во всех решениях команды."* — `base_component.py` копируется между решениями.

### Общее (идентичный код)

| Блок | Описание |
|------|----------|
| Конфигурационные `@property` | 8 свойств: `_component_alias`, `_package_name`, `_package_file_name`, `_static_directory_path`, `_static_url_path_abs`, `_static_url_path`, `_ui_install_by_default`, `_localization_prefix`, `_integration_service_solution` |
| `__init__` | Инициализация констант: `COMPONENT_ALIAS`, `PACKAGE_NAME`, `PACKAGE_FILE_NAME`, `STATIC_DIRECTORY_PATH`, `STATIC_URL_PATH`, `UI_INSTALL_BY_DEFAULT`, `LOCALIZATION_PREFIX`, `EXTRA_STATIC_PATHS`, `INTEGRATION_SERVICE_SOLUTION`, `TEMPLATES_FOLDER` |
| `install(**kwargs)` | Парсинг аргументов, `steps_executor.execute_steps()` |
| `uninstall(only_client)` | `DeploymentTool.remove_solutions()` + `remove_static_paths()` |
| `get_install_steps_v2(data)` | Формирование `deploy_step` + `client_up_step` |
| `check(tenant)` | OData healthcheck через `/integration/odata/{solution}/Check` |
| `get_install_package_path()` | Базовый: `component_path / package_file_name` |
| `_remove_applied_solution()` | `DeploymentTool().remove_solutions()` |
| `_get_boolean_cmd_arg()` | Парсинг булевых аргументов |
| `import_templates()` | Импорт шаблонов через `rxcmd` |

### Отличия Targets-версии (+2KB)

| Блок | Omni | Targets |
|------|------|---------|
| `UP_PUBLIC_API` | Отсутствует | `False` (по умолчанию), переопределяется в наследнике |
| `SUNGERO_PUBLIC_API` | Отсутствует | `"SungeroPublicApi"` (имя сервиса) |
| `SUNGERO_PUBLIC_API_PATH_NAME` | Отсутствует | `"window.PUBLIC_API_PATH"` |
| `sungero_public_api_up()` | Отсутствует | Создание секции в config.yml + запуск сервиса |
| `_find_service()` | Отсутствует | Поиск сервиса через `service_finder` |
| `_service_up()` | Отсутствует | Запуск сервиса через `ServiceContract.up()` |
| `_print_help_after_action()` | Есть | Отсутствует |
| Импорт `urlparse` | Нет | Да |
| Импорт `ruamel.yaml.CommentedMap` | Нет | Да |

### Когда что использовать

- **Omni-версия** — базовый Applied-плагин без PublicAPI
- **Targets-версия** — если решению нужен PublicAPI (сводные таблицы, REST-доступ к данным)

---

## 12. Reference-файлы

### PackageInfo.xml

| Решение | Файл | Модулей |
|---------|------|---------|
| Targets | `targets/DirectumTargets.xml` | 5 (DirectumTargets, DTCommons, KPI, Targets, TargetsAndKPIsUI) |
| OmniApplied | `omniapplied/OmniApplied.xml` | 2 (Omni, OmniIntegration) |

### manifest.json

| Решение | Файл | Зависимости |
|---------|------|-------------|
| Targets | `targets/manifest.json` | `base: >=26` |
| OmniApplied | `omniapplied/manifest.json` | `Platform: >=26.1`, `DirectumRX: >=26.1` |

### Python-плагины

| Файл | Описание |
|------|----------|
| `omniapplied/omni_plugin/base_component.py` | BaseRnDComponent (Omni-версия, 15KB) |
| `targets/targets_plugin/base_component.py` | BaseRnDComponent (Targets-версия, 17KB + PublicAPI) |
| `omniapplied/omni_plugin/omni_installer.py` | OmniAppliedComponent — конкретный инсталлятор |
| `targets/targets_plugin/targets_installer.py` | TargetsComponent — инсталлятор с зависимостью PivotTable |
| `omniapplied/omni_plugin/omni_update_config_settings.py` | Мутация config.yml (DISALLOWED_ENTITY_ACTION_IDS) |

### Документация

| Файл | Содержимое |
|------|-----------|
| `docs/platform/PLUGIN_PATTERNS_CATALOG.md` | Полный каталог паттернов: ChatBot, OmniApplied, Targets |
| `knowledge-base/guides/32_rc_plugin_development.md` | RC Plugin Development (основы) |
| `knowledge-base/guides/33_microservice_deployment.md` | Microservice Deployment (Docker/systemd) |
| `knowledge-base/guides/35_deployment_tool_internals.md` | DeploymentToolCore CLI, flags, exit-codes |
| `knowledge-base/guides/36_launcher_internals.md` | DirectumLauncher: Python CLI, @action, config.yml |

---

## Чеклист: создание нового Applied-плагина

1. Создать `.dat` пакет через DDS (export-package)
2. Создать `PackageInfo.xml` — описать все модули решения
3. Создать `manifest.json` — имя, версия, зависимости, `plugin_path`
4. Скопировать `base_component.py` из Omni или Targets (в зависимости от нужды в PublicAPI)
5. Создать `my_installer.py`:
   - `@component(alias='myalias')`
   - Установить все константы в конструкторе
   - При необходимости переопределить `get_install_package_path()` и `get_install_steps_v2()`
6. Создать `translations/{en,ru}/LC_MESSAGES/messages.po` с ключами:
   - `{alias}.ui.install` — чекбокс в UI
   - `{alias}.ui.installing` — шаг деплоя
   - `{alias}.ui.webclient` — шаг перезапуска
7. Создать `__init__.py` с `PluginMetadata(is_root=True)` + `import_package_modules`
8. Проверить: `do myalias install`, `do myalias uninstall`, `do myalias check --tenant=default`
