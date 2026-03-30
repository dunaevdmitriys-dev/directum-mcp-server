# 32. RC Plugin Development — Плагины для DirectumLauncher

> Разработка плагинов (компонент) для системы `do` (DirectumLauncher).
> Три архетипа: Service Plugin, Applied Plugin, Applied Plugin + Deps.
> Основано на анализе production-решений: ChatBot, OmniApplied, Targets.

---

## Оглавление

1. [Обзор: 3 архетипа](#1-обзор-3-архетипа)
2. [Структура manifest.json](#2-структура-manifestjson)
3. [BaseComponent vs BaseRnDComponent vs DotNetServiceBase](#3-basecomponent-vs-baserndcomponent-vs-dotnetservicebase)
4. [Lifecycle: install/update/uninstall хуки](#4-lifecycle-installupdateuninstall-хуки)
5. [Config generation: wrap_variable, default_settings](#5-config-generation-wrap_variable-default_settings)
6. [Schema UI: JSON Schema для Launcher](#6-schema-ui-json-schema-для-launcher)
7. [Publish: создание .tar.gz](#7-publish-создание-targz)
8. [i18n: .po файлы](#8-i18n-po-файлы)
9. [Config mutation: omni_update_config_settings паттерн](#9-config-mutation-omni_update_config_settings-паттерн)
10. [Зависимости: manifest-time, install-time](#10-зависимости-manifest-time-install-time)
11. [Сравнительная таблица 3 архетипов](#11-сравнительная-таблица-3-архетипов)
12. [Reference-файлы](#12-reference-файлы)

---

## 1. Обзор: 3 архетипа

Каждый плагин Launcher — Python-пакет с декоратором `@component(alias=...)`, наследующий один из базовых классов платформы.

| Архетип | Базовый класс | Назначение | Пример |
|---------|---------------|------------|--------|
| **Service Plugin** | `BaseComponent` + `DotNetServiceBase` | Автономный .NET-сервис (Docker/systemd), HAProxy, healthcheck | ChatBot |
| **Applied Plugin** | `BaseRnDComponent` (наследник `BaseComponent`) | Прикладная разработка RX (.dat), статика, DeploymentTool | OmniApplied |
| **Applied + Deps** | `BaseRnDComponent` + зависимость на другую компоненту | То же + установка зависимостей перед deploy | Targets |

### Иерархия наследования

```
BaseComponent (платформа)
  |
  +-- ChatBotComponent              # Service Plugin (прямой наследник)
  |     |-- ChatBot(DotNetServiceBase)  # сервис-обёртка
  |
  +-- BaseRnDComponent              # Абстрактный RnD-компонент (15-17KB)
        |
        +-- OmniAppliedComponent    # Applied Plugin
        +-- TargetsComponent        # Applied Plugin + deps
```

### Минимальная структура файлов

**Service Plugin** (ChatBot):

```
chatbot_plugin/
  __init__.py              # PluginMetadata(is_root=True) + import chatbot_service
  common_consts.py         # COMPONENT_NAME, ALIAS, DEFAULT_PORT
  chatbot_component.py     # ChatBotComponent(BaseComponent) — компонент Launcher
  chatbot_service.py       # ChatBot(DotNetServiceBase) — сервис-обёртка
  default_settings.py      # Генерация config.yml: env, variables, wrap_variable
  publish.py               # Сборка .tar.gz с манифестом
  schema/
    __init__.py
    config_schema.py       # Загрузка JSON Schema по локали
    chatbot_component.schema.en.json
    chatbot_component.schema.ru.json
  translations/
    en/LC_MESSAGES/messages.po
    ru/LC_MESSAGES/messages.po
```

**Applied Plugin** (OmniApplied):

```
omni_plugin/
  __init__.py                      # PluginMetadata(is_root=True) + import_package_modules
  base_component.py                # BaseRnDComponent(BaseComponent) — 15KB абстрактный базовый
  omni_installer.py                # OmniAppliedComponent(BaseRnDComponent) — installer
  omni_update_config_settings.py   # Мутация config.yml (DISALLOWED_ENTITY_ACTION_IDS)
  translations/
    en/LC_MESSAGES/messages.po
    ru/LC_MESSAGES/messages.po
```

**Applied + Deps** (Targets):

```
targets_plugin/
  __init__.py              # PluginMetadata(is_root=True) + import_package_modules
  base_component.py        # BaseRnDComponent — копия из omniapplied (17KB, +PublicAPI)
  targets_installer.py     # TargetsComponent(BaseRnDComponent)
  translations/
    en/LC_MESSAGES/messages.po
    ru/LC_MESSAGES/messages.po
```

> **ВАЖНО:** `base_component.py` копируется между решениями RnD (omni и targets). Комментарий в файле: "ВНИМАНИЕ. Содержать базовый компонент одинаковым во всех решениях команды."

---

## 2. Структура manifest.json

Манифест создаётся при публикации через `ComponentManifest` и попадает в .tar.gz как `component_manifest.json`.

```python
from sungero_publish.component_manifest import (
    ComponentManifest, save_manifest_to_folder,
    COMPONENT_MANIFEST_FILE_NAME, ManifestRuntime
)

manifest = ComponentManifest(
    name='ChatBot',                           # Имя компоненты
    version=version,                          # Семантическая версия
    description={                             # Мультиязычное описание
        "ru": "Чат бот",
        "en": "ChatBot"
    },
    dependencies={},                          # Зависимости на другие компоненты
    plugin_path="./chatbot_plugin",           # Относительный путь к Python-плагину
    runtime=ManifestRuntime.LINUX             # LINUX | WINDOWS | ALL
)
```

### Поля ManifestRuntime

| Значение | Что означает |
|----------|-------------|
| `LINUX` | Только Linux (Docker/systemd). Используется для .NET-сервисов |
| `WINDOWS` | Только Windows |
| `ALL` | Обе платформы |

Applied-плагины (OmniApplied, Targets) **не создают** явный манифест через `publish.py` — они публикуются через платформенный механизм (Launcher автоматически создаёт неявный манифест).

---

## 3. BaseComponent vs BaseRnDComponent vs DotNetServiceBase

### BaseComponent (платформа)

Базовый класс для **всех** компонент Launcher. Предоставляет:

- `config_path` — путь к config.yml
- `config` — загруженный YAML
- `install()`, `uninstall()`, `up()`, `down()` — точки расширения
- `get_install_steps_v2(data)` — шаги установки для UI Launcher
- `get_min_ui_model_v2(data)` — модель настроек для UI
- `generate_config_v2(data)` — генерация конфигурации
- `post_install(data)` — сообщение после установки
- `get_json_schema(locale)` — JSON Schema для UI настроек

### DotNetServiceBase (платформа)

Обёртка для .NET-сервисов. Наследники получают:

- Управление Docker-контейнером: `up()`, `down()`, `start()`, `stop()`, `restart()`
- Healthcheck по HTTP endpoint
- Настройку HAProxy (backend, frontend, routing)
- Регистрацию в `service_finder`

```python
from sungero_deploy.base_dotnet_service import DotNetServiceBase
from sungero_deploy import service_finder

@Help.show_only_linux
class ChatBot(DotNetServiceBase):
    default_port = 44324
    component_name = 'ChatBot'

    @property
    def healthcheck_endpoint(self) -> str:
        return f"{self._host_authority}/chatbot/{self.health_url}"

    def add_haproxy_settings(self) -> None:
        haproxy_config_path = get_haproxy_config_path(self.config)
        backend_settings = [
            ('balance', 'roundrobin'),
            ('http-request', 'set-path %[path,regsub(^/chatbot/,/,i)]'),
        ]
        add_backend(
            get_default_backend_server_settings(self.instance_service, self.service_config.port),
            backend_settings,
            "{ path_beg -i /chatbot/ } ",
            haproxy_config_path,
            frontend_name=get_frontend_name("omni")
        )

# Регистрация сервиса в `do all` (up/down/restart)
service_finder.add_service_class(service_finder.ServiceClassInfo(type=ChatBot))
```

### BaseRnDComponent (shared)

Абстрактный базовый класс для **прикладных** решений RnD-команды. Наследник `BaseComponent`, добавляет:

| Константа | Тип | Назначение |
|-----------|-----|------------|
| `COMPONENT_ALIAS` | str | Алиас `@component(alias=...)` |
| `PACKAGE_NAME` | str | Имя решения RX (напр. `Sungero.Omni`) |
| `PACKAGE_FILE_NAME` | str | Имя .dat файла (напр. `OmniApplied.dat`) |
| `STATIC_DIRECTORY_PATH` | str | Папка статики (по умолчанию `"client"`) |
| `STATIC_URL_PATH` | str | URL-часть для статики |
| `UI_INSTALL_BY_DEFAULT` | bool | Чекбокс в UI по умолчанию |
| `LOCALIZATION_PREFIX` | str | Префикс для ключей i18n |
| `INTEGRATION_SERVICE_SOLUTION` | str | Имя решения в сервисе интеграции |
| `TEMPLATES_FOLDER` | str/None | Папка с шаблонами документов |
| `EXTRA_STATIC_PATHS` | dict | Доп. папки статики |
| `UP_PUBLIC_API` | bool | Нужно ли поднимать PublicAPI (только Targets) |

**Минимальный наследник:**

```python
@component(alias='myalias')
class MyComponent(BaseRnDComponent):
    def __init__(self, config_path=None):
        super().__init__(config_path)
        self.COMPONENT_ALIAS = 'myalias'
        self.PACKAGE_NAME = 'MyCompany.MySolution'
        self.PACKAGE_FILE_NAME = 'MySolution.dat'
        self.STATIC_URL_PATH = 'myalias'
        self.UI_INSTALL_BY_DEFAULT = True
        self.LOCALIZATION_PREFIX = "myalias"
        self.INTEGRATION_SERVICE_SOLUTION = "MySolution"
```

---

## 4. Lifecycle: install/update/uninstall хуки

### Install

#### Service Plugin (ChatBot)

```
install() ->
  1. backup config.yml (scripts_config.backup)
  2. load yaml config
  3. get_install_steps_v2() -> [up_step, check_step]
  4. steps_executor.execute_steps()
     -> ChatBotComponent.up() -> ChatBot.up() (DotNetServiceBase, Docker)
     -> ChatBotComponent.check() -> ChatBot.check() (healthcheck)
```

```python
def install(self, **kwargs):
    scripts_config.backup(self.config_path)
    yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
    install_steps = self.get_install_steps_v2(
        ComponentData(config_path=self.config_path,
                      install_mode=self._get_install_mode(),
                      yaml_config=yaml_config, ui_vars=[])
    )
    steps_executor.execute_steps(install_steps)
```

#### Applied Plugin (OmniApplied / Targets)

```
install(**kwargs) ->
  1. parse kwargs: only_client, force_install, do_not_init
  2. get_install_steps_v2(mode="console_install") ->
     [?pivot_install, deploy_step, ?webclient_up, ?update_config]
  3. steps_executor.execute_steps()
     -> DeploymentTool deploy --package='...' --init --settings
     -> SungeroWebClient up (только в UI-режиме)
  4. log success
```

### Update

Определяется автоматически — тот же `install()`, но:
- **ChatBot:** `_get_install_mode()` проверяет `get_installed_component(COMPONENT_NAME)` и возвращает `InstallModes.Update`. `post_install()` выдаёт разный текст для install/update.
- **RnD:** DeploymentTool сам определяет update vs first install по состоянию БД.

```python
def _get_install_mode(self) -> str:
    if get_installed_component(self.get_component_name()):
        return InstallModes.Update
    return InstallModes.Install
```

### Uninstall

#### Service Plugin (ChatBot)

```python
def uninstall(self, **kwargs):
    self.down()  # -> ChatBot.down() — stop + remove Docker container
```

#### Applied Plugin (OmniApplied / Targets)

```python
def uninstall(self, only_client=False):
    if not only_client:
        DeploymentTool.remove_solutions(PACKAGE_NAME)
    remove_static_paths()
```

### Шаги установки (InstallStep)

Каждый шаг — объект `InstallStep(display_name, action)`:

```python
from components.ui_models import InstallStep
from py_common.actions import get_action_str

InstallStep(
    display_name=_("chatbot.steps.up"),           # Локализованная строка
    action=get_action_str(ChatBotComponent,       # Сериализованный вызов
                          config_path=data.config_path,
                          _args='up')
)
```

---

## 5. Config generation: wrap_variable, default_settings

### wrap_variable

`wrap_variable(name)` из `sungero_deploy.scripts_config` — оборачивает имя переменной в YAML-шаблон `{{ name }}`, который Launcher резолвит при deploy.

```python
from sungero_deploy.scripts_config import wrap_variable

'GeneralSettings__ChatBotSecurityToken': wrap_variable('omni_chatbot_token')
# В config.yml: GeneralSettings__ChatBotSecurityToken: '{{ omni_chatbot_token }}'
```

### Три слоя конфигурации (ChatBot)

**1. `env`** — переменные окружения .NET-сервиса:

```python
env = {
    'GeneralSettings__ChatBotSecurityToken': wrap_variable('omni_chatbot_token'),
    'MatrixSettings__MatrixToken': wrap_variable('omni_chatbot_matrix_token'),
    'ConnectionStrings__IdentityService': f'Name=Directum.Core.IdentityService;'
        f'Host={wrap_variable("omni_ids_host")};'
        f'Port={wrap_variable("omni_ids_port")};'
        f'User ID={wrap_variable("omni_ids_service_user_name")};'
        f'Password={wrap_variable("omni_ids_service_user_password")};',
    'QueueSettings__0__ConnectionString': "hostName=;port=;userName=;password=;",
    'RedisSettings__Host': "",
    # ...
}
```

**2. `variables`** — секция `variables:` в config.yml:

```python
variables = {
    'omni_chatbot_token': format(random.getrandbits(256), '064x'),   # Токен генерируется
    'omni_chatbot_matrix_token': format(random.getrandbits(256), '064x'),
    'omni_chatbot_user': 'OmniBot',
    'omni_chatbot_user_password': '',
    # ...
}
```

**3. `chatbot_default_settings`** — секция `services_config.ChatBot`:

```python
chatbot_default_settings = {
    'ChatBot': {
        'port': 44324,
        'environments': CommentedMap(env)
    }
}
```

### Паттерн add_settings_to_config

```python
def add_settings_to_config(yaml_config):
    # 1. Получить/создать omni_common_config с anchors
    omni_common_config = get_omni_common_config(yaml_config, create_not_exists=True,
        anchors={SERVICE_ENVIRONMENTS_SECTION_NAME: OMNI_COMMON_ENVIRONMENTS_ANCHOR_NAME})

    # 2. Получить/создать services_config
    services_config = yaml_tools.get_or_create_dict_by_key('services_config', yaml_config)

    # 3. Идемпотентность: пропустить если ChatBot уже есть
    if not set(services_config.keys()).isdisjoint(set(chatbot_default_settings.keys())):
        return

    # 4. Добавить variables
    add_settings_to_config_section(variables, variables_section)

    # 5. Добавить services_config с merge для logs
    add_settings_to_config_section(chatbot_default_settings, services_config,
        common_configs_to_be_merged=[yaml_config.get('logs_path')])

    # 6. YAML merge anchor для environments (наследование общих переменных)
    env_section.add_yaml_merge([(0, omni_common_environments_section)])

    # 7. YAML-комментарии
    yaml_tools.add_comments_to_section(env_section, env_comments)
```

**Ключевые принципы:**
- **Идемпотентность** — проверка `isdisjoint` перед добавлением. Повторный вызов не дублирует конфиг.
- **YAML merge anchors** — `add_yaml_merge` для наследования общих переменных.
- **Комментарии** — `yaml_tools.add_comments_to_section` добавляет пояснения в YAML.

---

## 6. Schema UI: JSON Schema для Launcher

Только **Service Plugin** предоставляет JSON Schema для UI Launcher. Applied-плагины используют простой чекбокс.

### JSON Schema (ChatBot)

Файлы: `chatbot_component.schema.{en,ru}.json` — отдельная схема на каждую локаль.

```json
{
  "title": "ChatBot",
  "type": "object",
  "properties": {
    "services_config": {
      "ui:control": "ListWithDetail",
      "type": "object",
      "properties": {
        "ChatBot": {
          "title": "ChatBot",
          "properties": {
            "port": {
              "type": "integer",
              "default": 44324
            },
            "ssl_cert_pfx_path": {
              "type": "string"
            },
            "ssl_cert_pfx_password": {
              "type": "string",
              "ui:options": { "Hidden": true }
            },
            "environments": {
              "title": "Настройки окружения",
              "type": "object",
              "properties": {}
            }
          }
        }
      }
    }
  }
}
```

### Ключевые ui-атрибуты

| Атрибут | Назначение |
|---------|------------|
| `"ui:control": "ListWithDetail"` | Рендер секции как список с детализацией |
| `"ui:options": {"Hidden": true}` | Скрытое поле (для паролей, токенов) |
| `"default": 44324` | Значение по умолчанию |

### Загрузка и мерж схем

```python
# config_schema.py
def get_json_schema(locale):
    schema_path = f"chatbot_component.schema.{locale}.json"
    return json_tools.load(schema_path)

def merge_localized_schema(locale):
    default = get_json_schema('en')
    localized = get_json_schema(locale)
    return gen_config_schema._merge_localized_schema_dict(default, localized)
```

Метод `get_json_schema(locale)` вызывается Launcher для отображения настроек в UI.

### UI Model для Applied-плагинов

RnD-компоненты **не предоставляют** JSON Schema. Вместо этого — один чекбокс:

```python
def get_min_ui_model_v2(self, data):
    return [UIVariable(
        name="need_installSungero.Omni",
        value=True,                              # UI_INSTALL_BY_DEFAULT
        control=UIControls.boolean_control,
        display_name=_("omniapplied.ui.install")
    )]
```

Service Plugin (ChatBot) возвращает `empty_variable()` — значит UI настройки берутся целиком из JSON Schema.

---

## 7. Publish: создание .tar.gz

Только ChatBot имеет явный `publish.py`. Applied-плагины публикуются через платформенный механизм.

### Структура .tar.gz (ChatBot)

```
ChatBot.tar.gz
  ChatBot/                    # Бинарники .NET-сервиса (MatrixBotService)
  chatbot_plugin/             # Python-плагин целиком
  version.txt                 # Версия (строка)
  component_manifest.json     # Манифест компоненты
```

### Код публикации

```python
@action
@Help.hide
def publish_chatbot_component(build_folder_path: str, version: str) -> None:
    target_path = os.path.join(common_paths.etc_path, 'dist', COMPONENT_NAME)
    pathlib.Path(target_path).mkdir(parents=True, exist_ok=True)
    zip_file_name = os.path.join(target_path, f"{COMPONENT_NAME}.tar.gz")

    with create_tmp_path() as tmp_publish:
        chatbot_plugin_path = pathlib.Path(os.path.realpath(__file__)).parent

        with tarfile.open(zip_file_name, "w:gz") as package:
            # Бинарники .NET-сервиса
            package.add(os.path.join(build_folder_path, "MatrixBotService"), COMPONENT_NAME)
            # Python-плагин
            package.add(chatbot_plugin_path, "chatbot_plugin")
            # Версия
            version_file_path = os.path.join(tmp_publish, 'version.txt')
            with open(version_file_path, 'w') as f:
                f.write(str(version))
            package.add(version_file_path, 'version.txt')
            # Манифест
            manifest = ComponentManifest(COMPONENT_NAME, version, description,
                                         dependencies={},
                                         plugin_path="./chatbot_plugin",
                                         runtime=ManifestRuntime.LINUX)
            manifest_file_path = save_manifest_to_folder(manifest, tmp_publish)
            package.add(manifest_file_path, COMPONENT_MANIFEST_FILE_NAME)

    # Инвалидация кэша Launcher
    package_state_path = get_package_state_file_path(zip_file_name)
    json_tools.dump(package_state_path, dataclasses.asdict(ComponentPackageState(changed=True)))
```

Декоратор `@action` регистрирует функцию как команду `do`:

```bash
do publish_chatbot_component --build_folder_path=/path/to/build --version=1.0.0
```

### Кэширование

После создания .tar.gz записывается `ComponentPackageState(changed=True)` — это инвалидирует кэш Launcher, чтобы при следующей установке он заново распаковал архив.

---

## 8. i18n: .po файлы

### Формат

GNU gettext `.po` файлы. Используются через `flask_babel`:

```python
from flask_babel import _

display_name = _("chatbot.steps.up")      # -> "Развертывание ChatBot" (ru)
display_name = _("targets.ui.install")     # -> "Установить решение «Directum Targets»" (ru)
```

### Расположение

```
translations/
  en/LC_MESSAGES/
    messages.po       # Исходные строки (en)
    messages.mo       # Скомпилированный бинарный файл
  ru/LC_MESSAGES/
    messages.po
    messages.mo
```

### Ключи по плагинам

**ChatBot** (4 ключа):

```
chatbot.steps.up          = "Развертывание ChatBot"
chatbot.steps.check       = "Проверка работоспособности ChatBot"
chatbot.update_complete   = "ChatBot успешно обновлен"
chatbot.install_complete  = "ChatBot успешно установлен"
```

**OmniApplied** (4 ключа):

```
omniapplied.ui.install                = "Установить прикладную разработку «Directum Omni»"
omniapplied.ui.installing             = "Установка прикладной разработки «Directum Omni»"
omniapplied.ui.webclient              = "Перезапуск веб-клиента"
omniapplied.ui.update_config_settings = "Изменение настроек конфигурационного файла"
```

**Targets** (6 ключей):

```
targets.ui.install           = "Установить решение «Directum Targets»"
targets.ui.installing        = "Установка решения «Directum Targets»"
targets.ui.webclient         = "Перезапуск веб-клиента"
targets.ui.installing_pivot  = "Установка компонента для работы со сводными таблицами"
targets.ui.add_static        = "Updating static path config"
targets.ui.update_config     = "Updating general component config"
```

### Соглашение по именованию ключей

```
{component_alias}.{category}.{action}
```

| `category` | Когда использовать |
|------------|-------------------|
| `steps` | Шаги установки сервиса (Service Plugin) |
| `ui` | Элементы UI Launcher (Applied Plugin) |

---

## 9. Config mutation: omni_update_config_settings паттерн

Паттерн для модификации `config.yml` **после** основной установки. Используется OmniApplied для блокировки прикладных действий в PublicAPI.

### Реализация

```python
@action
@Help.hide
def omni_update_config_settings(config_path: str, action_ids: list[str]) -> None:
    _update_public_api_config(config_path, action_ids)

def _update_public_api_config(config_path: str, action_ids: list[str]) -> None:
    public_api_service = SungeroPublicApi(config_path)
    yaml_config = yaml_tools.load_yaml_from_file(config_path)
    services_config = yaml_config.get('services_config')
    if not services_config:
        return

    public_api_settings = services_config.get(
        public_api_service.instance_service.service_qualified_name
    )
    if not public_api_settings:
        return

    delimiter = ";"
    existing_ids = public_api_settings.get('DISALLOWED_ENTITY_ACTION_IDS', '')
    action_ids_set = set(existing_ids.split(delimiter)) if len(existing_ids) > 0 else set()
    action_ids_set |= set(action_ids)

    public_api_settings.update({
        'DISALLOWED_ENTITY_ACTION_IDS': str(delimiter.join(action_ids_set))
    })
    yaml_tools.yaml_dump_to_file(yaml_config, config_path)
```

### Интеграция в install flow

```python
# omni_installer.py
def get_install_steps_v2(self, data):
    steps = super().get_install_steps_v2(data)          # Базовые шаги
    steps.append(InstallStep(
        display_name=_("omniapplied.ui.update_config_settings"),
        action=get_action_str(omni_update_config_settings,
                              config_path=data.config_path,
                              action_ids=self.DISALLOWED_ENTITY_ACTION_IDS)
    ))
    return steps
```

### Паттерн создания секции (Targets PublicAPI)

Targets использует другой подход — создание целой секции `SungeroPublicApi` в config.yml:

```python
def sungero_public_api_up(self):
    yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
    services_config = yaml_config.get('services_config')
    if not services_config.get('SungeroPublicApi'):
        public_api_config = CommentedMap()
        services_config['SungeroPublicApi'] = public_api_config
        public_api_config['PUBLIC_API_HOST_HTTP_PORT'] = None     # auto
        public_api_config['WEB_API_HOST_URI'] = f'{{{{ protocol }}}}://...'
        # YAML merge anchor для logs
        public_api_config.add_yaml_merge([(0, yaml_config.get('logs_path'))])
        yaml_tools.yaml_dump_to_file(yaml_config, self.config_path)

    self._service_up('SungeroPublicApi', config)
```

---

## 10. Зависимости: manifest-time, install-time

### Уровень манифеста (publish-time)

Указываются в `ComponentManifest.dependencies`. ChatBot использует пустой `dependencies={}`.

```python
manifest = ComponentManifest(
    name='ChatBot',
    version=version,
    dependencies={},              # Нет зависимостей
    # или:
    # dependencies={'OtherComponent': '>=1.0.0'},
    plugin_path="./chatbot_plugin",
    runtime=ManifestRuntime.LINUX
)
```

### Уровень install-time (код)

**Targets** — единственный пример с runtime-зависимостью на другой плагин:

```python
from pivottable_plugin.pivottable_installer import PivotTableComponent

def get_install_steps_v2(self, data):
    steps = super().get_install_steps_v2(data)
    install_pivot_step = [InstallStep(
        display_name=_("targets.ui.installing_pivot"),
        action=get_action_str(PivotTableComponent,
                              config_path=self.config_path,
                              _args="install")
    )]
    steps = install_pivot_step + steps    # ПЕРЕД основными шагами
    return steps
```

**OmniApplied** — зависимость на RX через `get_install_package_path()`:

```python
def get_install_package_path(self):
    from py_common.actions import Action
    Base = Action.get_class("rx")
    rx_path = Base.get_applied_package_file_name()
    package_path = os.path.join(self._component_path, self._package_file_name)
    package_path = f"{rx_path};{package_path}"    # RX.dat ВСЕГДА первый
    return package_path
```

### Уровень БД (smart resolution)

OmniApplied и Targets проверяют, нужно ли добавлять пакет из отдельной компоненты. Если решение уже в БД, но не в текущем пакете — добавляется путь из другой компоненты:

```python
def _get_omni_package_path(self):
    config_path = get_default_config_path()
    db = SungeroDB(config_path)
    if not db.is_db_exist(db.db_name):
        return None
    deployed_solutions = get_solutions_from_db(config_path)
    package_path = os.path.join(self._component_path, self._package_file_name)
    solutions_from_packages = get_applied_solutions_info_from_packages(package_path)
    deployed_solutions_names = set(solution.Name for solution in deployed_solutions)
    if self.OMNI_SOLUTION_NAME in deployed_solutions_names \
       and self.OMNI_SOLUTION_NAME not in solutions_from_packages:
        omni_component_folder = ComponentManager.get_component_folder(self.OMNI_FOLDER_NAME)
        return os.path.join(omni_component_folder, self.OMNI_PACKAGE_FILE_NAME)
    return None
```

---

## 11. Сравнительная таблица 3 архетипов

| Аспект | ChatBot (Service) | OmniApplied (Applied) | Targets (Applied+Deps) |
|--------|-------------------|----------------------|----------------------|
| **Базовый класс** | `BaseComponent` | `BaseRnDComponent` | `BaseRnDComponent` |
| **Сервис-обёртка** | `DotNetServiceBase` | Нет | Нет |
| **Что деплоит** | .NET Docker/systemd | .dat через DeploymentTool | .dat через DeploymentTool |
| **HAProxy** | Да, backend routing | Нет | Нет |
| **Healthcheck** | `/chatbot/{health_url}` | OData `/Check` | OData `/Check` |
| **Config generation** | `default_settings.py` + `wrap_variable` | Нет (только мутация) | `sungero_public_api_up()` |
| **Config mutation** | Нет | `DISALLOWED_ENTITY_ACTION_IDS` | Нет |
| **JSON Schema** | Да (2 локали) | Нет | Нет |
| **UI Model** | `empty_variable()` | `boolean_control` чекбокс | `boolean_control` чекбокс |
| **Publish** | `publish.py` -> .tar.gz | Платформа | Платформа |
| **Manifest** | Явный `ComponentManifest` | Неявный | Неявный |
| **Deps** | Нет | RX.dat обязателен | RX.dat + PivotTable |
| **Статика** | Нет | Да (via StaticController) | Да (via StaticController) |
| **Шаблоны документов** | Нет | Нет | Закомментировано (TODO) |
| **i18n ключей** | 4 | 4 | 6 |
| **Runtime** | Linux only | Linux + Windows | Linux + Windows |
| **base_component.py** | Нет (прямой наследник) | 15KB shared | 17KB shared (+PublicAPI) |

---

## 12. Reference-файлы

### Исходники плагинов

| Плагин | Путь |
|--------|------|
| ChatBot component | `CRM/.pipeline/chatbot/chatbot_plugin/chatbot_component.py` |
| ChatBot service | `CRM/.pipeline/chatbot/chatbot_plugin/chatbot_service.py` |
| ChatBot default_settings | `CRM/.pipeline/chatbot/chatbot_plugin/default_settings.py` |
| ChatBot publish | `CRM/.pipeline/chatbot/chatbot_plugin/publish.py` |
| ChatBot __init__ | `CRM/.pipeline/chatbot/chatbot_plugin/__init__.py` |
| OmniApplied installer | `omniapplied/omni_plugin/omni_installer.py` |
| OmniApplied config mutation | `omniapplied/omni_plugin/omni_update_config_settings.py` |
| Targets installer | `targets/targets_plugin/targets_installer.py` |

### Документация

| Документ | Путь |
|----------|------|
| Полный каталог паттернов | `docs/platform/PLUGIN_PATTERNS_CATALOG.md` |
| DirectumLauncher internals | `knowledge-base/guides/36_launcher_internals.md` |
| DeploymentTool internals | `knowledge-base/guides/35_deployment_tool_internals.md` |
| Applied Solution Packaging | `knowledge-base/guides/34_applied_solution_packaging.md` |
| Microservice Deployment | `knowledge-base/guides/33_microservice_deployment.md` |
| Карта интеграций платформы | `knowledge-base/guides/38_platform_integration_map.md` |

### Рецепт: новый Service Plugin

```
my_plugin/
  __init__.py                    # PluginMetadata(is_root=True)
  common_consts.py               # COMPONENT_NAME, ALIAS, DEFAULT_PORT
  my_component.py                # MyComponent(BaseComponent) + @component(alias=...)
  my_service.py                  # MyService(DotNetServiceBase) + service_finder.add_service_class
  default_settings.py            # env + variables + add_settings_to_config()
  publish.py                     # @action publish_my_component(build_folder, version)
  schema/
    __init__.py
    config_schema.py
    my_component.schema.en.json
    my_component.schema.ru.json
  translations/{en,ru}/LC_MESSAGES/messages.{po,mo}
```

### Рецепт: новый Applied Plugin

```
my_plugin/
  __init__.py                    # PluginMetadata(is_root=True) + import_package_modules
  base_component.py              # КОПИЯ из omniapplied/targets (держать в синхре!)
  my_installer.py                # MyComponent(BaseRnDComponent) + @component(alias=...)
  translations/{en,ru}/LC_MESSAGES/messages.{po,mo}
```
