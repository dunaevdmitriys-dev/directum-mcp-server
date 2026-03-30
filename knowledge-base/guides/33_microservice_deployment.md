# Microservice Deployment — Микросервис-компонент DirectumLauncher

## 1. Обзор

**Микросервис-компонент** (Service Plugin) — автономный .NET-сервис, управляемый DirectumLauncher.
В отличие от Applied Plugin (OmniApplied, Targets), который деплоит `.dat`-пакет через DeploymentTool, Service Plugin запускает отдельный процесс (Docker-контейнер или systemd unit), маршрутизирует трафик через HAProxy и предоставляет healthcheck.

Эталонная реализация: **ChatBot** (`chatbot/chatbot_plugin/`).

| Параметр | Значение |
|----------|----------|
| Базовый класс компоненты | `BaseComponent` (платформа) |
| Базовый класс сервиса | `DotNetServiceBase` (платформа) |
| Маршрутизация | HAProxy (frontend/backend) |
| Healthcheck | HTTP через HAProxy |
| Config | `config.yml` (секция `services_config`) |
| Публикация | `.tar.gz` + `ComponentManifest` |
| Runtime | Linux only |
| Порт по умолчанию | 44324 |

---

## 2. Архитектура

### Иерархия классов

```
BaseComponent (платформа, sungero_deploy)
  |
  +-- ChatBotComponent             # @component(alias='chatbot')
        |-- ChatBot(DotNetServiceBase)  # сервис-обёртка
```

### Поток запроса

```
Клиент -> HAProxy (frontend "omni")
              |
              +-- ACL: path_beg -i /chatbot/
              |       -> backend ChatBot (roundrobin)
              |            -> regsub: /chatbot/ -> /
              |            -> localhost:44324
              |
              +-- остальные пути -> стандартные бэкенды RX
```

### Поток установки

```
do chatbot install
  -> ChatBotComponent.install()
       1. backup config.yml
       2. load yaml_config
       3. get_install_steps_v2() -> [up_step, check_step]
       4. steps_executor.execute_steps()
            -> ChatBotComponent.up() -> ChatBot.up() (DotNetServiceBase)
            -> ChatBotComponent.check() -> ChatBot.check() (healthcheck)
```

---

## 3. BaseComponent: chatbot_component.py

`ChatBotComponent` наследует `BaseComponent` из платформы. Декоратор `@component(alias='chatbot')` регистрирует компоненту в системе `do`.

### Конструктор

```python
@component(alias=common_consts.ALIAS)
class ChatBotComponent(base_component.BaseComponent):
    def __init__(self, config_path=None):
        super().__init__(config_path)
        self.service = ChatBot(self.config)              # DotNetServiceBase
        self._component_path = ComponentManager.get_component_folder(COMPONENT_NAME)
```

### Методы жизненного цикла

| Метод | Назначение |
|-------|-----------|
| `install(**kwargs)` | Backup config, load YAML, execute steps (up + check) |
| `uninstall(**kwargs)` | Вызывает `down()` (stop + remove container) |
| `up()` | Делегирует `service.up()` |
| `down()` | Делегирует `service.down()` |
| `restart()` | Делегирует `service.restart()` |
| `start()` / `stop()` | Делегирует `service.start()` / `service.stop()` |
| `check()` | Делегирует `service.check()` (healthcheck) |

### install()

```python
def install(self, **kwargs):
    scripts_config.backup(self.config_path)
    yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
    install_steps = self.get_install_steps_v2(
        ComponentData(config_path=self.config_path,
                      install_mode=self._get_install_mode(),
                      yaml_config=yaml_config,
                      ui_vars=[])
    )
    steps_executor.execute_steps(install_steps)
```

### get_install_steps_v2()

Возвращает два шага:

1. **up** — `get_action_str(ChatBotComponent, config_path=..., _args='up')`
2. **check** — `get_action_str(ChatBotComponent, config_path=..., _args='check')`

В UI-режиме шаги пропускаются, если `CurrentStateModel.need_install_component()` возвращает `False`.

### _get_install_mode()

```python
def _get_install_mode(self):
    if get_installed_component(self.get_component_name()):
        return InstallModes.Update
    return InstallModes.Install
```

Проверяет БД установленных компонент. Результат влияет на `post_install()` — разный текст для Install/Update.

### post_install()

```python
def post_install(self, data):
    message = _("chatbot.update_complete") if data.install_mode == InstallModes.Update \
              else _("chatbot.install_complete")
    return PostInstallMessage(text=message)
```

### generate_config_v2() / generate_config_yaml()

Генерация секции ChatBot в `config.yml`:

```python
def generate_config_v2(self, data):
    base_component.add_omni_common_vars_to_config(data.yaml_config)
    add_settings_to_config(data.yaml_config)

def generate_config_yaml(self):
    yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
    self.generate_config_v2(ComponentData(...))
    yaml_tools.wrap_yaml_string_values_in_single_qoutes(yaml_config)
    yaml_tools.yaml_dump_to_file(yaml_config, self.config_path)
```

### get_json_schema()

Возвращает JSON Schema для UI Launcher (см. секцию 7).

### get_min_ui_model_v2()

Возвращает `[empty_variable()]` — у Service Plugin нет чекбокса "Установить", т.к. он управляется через JSON Schema.

---

## 4. DotNetServiceBase: chatbot_service.py

`ChatBot` наследует `DotNetServiceBase` — платформенный класс для .NET-сервисов, управляемых Docker/systemd.

### Определение сервиса

```python
@Help.show_only_linux
class ChatBot(DotNetServiceBase):
    default_port = 44324
    component_name = 'ChatBot'
    add_frontend: Callable[[], None] = add_frontend_function
```

- `default_port` — порт .NET-сервиса (Kestrel)
- `component_name` — имя в `services_config` секции `config.yml`
- `add_frontend` — функция добавления frontend-секции HAProxy
- `@Help.show_only_linux` — команда доступна только на Linux

### Healthcheck

```python
@property
def healthcheck_endpoint(self):
    return f"{self._host_authority}/chatbot/{self.health_url}"
```

Healthcheck идёт через HAProxy: `http(s)://host/chatbot/{health_url}`. Это проверяет всю цепочку: HAProxy -> rewrite -> .NET-сервис.

### Методы DotNetServiceBase (наследуемые)

| Метод | Назначение |
|-------|-----------|
| `up()` | Создать и запустить контейнер/unit |
| `down()` | Остановить и удалить |
| `start()` / `stop()` | Запустить/остановить без удаления |
| `restart()` | `stop()` + `start()` |
| `check()` | HTTP GET на `healthcheck_endpoint` |
| `status()` | Статус контейнера/unit |

### HAProxy Settings

```python
def add_haproxy_settings(self):
    haproxy_config_path = get_haproxy_config_path(self.config)
    # Удалить старый backend (если есть)
    remove_backend(get_default_backend_server_settings(
        self.instance_service, self.service_config.port), haproxy_config_path)

    backend_settings = [
        ('balance', 'roundrobin'),
        ('http-request', 'set-path %[path,regsub(^/chatbot/,/,i)]'),
    ]

    add_backend(
        get_default_backend_server_settings(
            self.instance_service, self.service_config.port,
            use_ssl=self.protocol == "https"
        ),
        backend_settings,
        "{ path_beg -i /chatbot/ } ",    # ACL
        haproxy_config_path,
        frontend_name=get_frontend_name("omni")
    )
```

### Регистрация в service_finder

```python
service_finder.add_service_class(service_finder.ServiceClassInfo(type=ChatBot))
```

Это позволяет командам `do all up`, `do all status` находить и управлять сервисом ChatBot.

---

## 5. Config generation: default_settings.py

Три слоя конфигурации для `config.yml`:

### 5.1 Переменные окружения (env)

Словарь `env` определяет переменные окружения .NET-сервиса:

```python
env = {
    'GeneralSettings__ChatBotSecurityToken': wrap_variable('omni_chatbot_token'),
    'MatrixSettings__MatrixToken': wrap_variable('omni_chatbot_matrix_token'),
    'AISettings__AIUrl': "",
    'AISettings__AIToken': "",
    'AISettings__Enabled': "",
    'ConnectionStrings__IdentityService': f'Name=...;Host={wrap_variable("omni_ids_host")};...',
    'ConnectionStrings__Synapse': f'Name=...;Host=;Port=8008;User ID={wrap_variable("omni_chatbot_user")};...',
    'QueueSettings__0__ConnectionString': "hostName=;port=;userName=;password=;exchange=;virtualhost=",
    'QueueSettings__0__SystemName': "DirectumRX",
    'QueueSettings__0__Enabled': "",
    'RedisSettings__Host': "",
    'RedisSettings__Port': "",
    'RedisSettings__Password': "",
    'RedisSettings__Enabled': ""
}
```

### 5.2 Функция wrap_variable

`wrap_variable(name)` из `sungero_deploy.scripts_config` оборачивает имя переменной в YAML-шаблон `{{ name }}`, который Launcher резолвит при deploy:

```python
wrap_variable('omni_chatbot_token')
# Результат в config.yml: '{{ omni_chatbot_token }}'
```

### 5.3 Variables (секция variables)

```python
variables = {
    'omni_chatbot_token': format(random.getrandbits(256), '064x'),     # 256-bit random hex
    'omni_chatbot_matrix_token': format(random.getrandbits(256), '064x'),
    'omni_chatbot_user': 'OmniBot',
    'omni_chatbot_user_password': '',
    'omni_ids_host': '',
    'omni_ids_port': '443',
    'omni_ids_service_user_name': '',
    'omni_ids_service_user_password': '',
}
```

Токены генерируются автоматически при первой установке через `random.getrandbits(256)`.

### 5.4 Services config (секция services_config)

```python
chatbot_default_settings = {
    'ChatBot': {
        'port': 44324,
        'environments': CommentedMap(env)
    }
}
```

### 5.5 add_settings_to_config() — основная функция

```python
def add_settings_to_config(yaml_config):
    # 1. Создать/получить omni_common_config с anchors
    omni_common_config_section = get_omni_common_config(yaml_config,
        create_not_exists=True,
        anchors={'environments': 'omni_common_environments'})

    # 2. Получить/создать services_config
    services_config_section = get_or_create_dict_by_key('services_config', yaml_config)

    # 3. Добавить frontend секцию HAProxy
    add_omni_config_frontend_section(yaml_config)

    # 4. Идемпотентность: пропустить если ChatBot уже есть
    if not set(services_config_section.keys()).isdisjoint(set(chatbot_default_settings.keys())):
        return

    # 5. Добавить variables
    add_settings_to_config_section(variables, variables_section)

    # 6. Добавить services_config с merge для logs_path
    add_settings_to_config_section(chatbot_default_settings, services_config_section,
        common_configs_to_be_merged=[yaml_config.get('logs_path')])

    # 7. YAML merge anchor для environments
    env_section.add_yaml_merge([(0, omni_common_environments_section)])

    # 8. Комментарии
    yaml_tools.add_comments_to_section(env_section, env_comments)
```

Ключевые принципы:
- **Идемпотентность** — если ChatBot уже есть в `services_config`, генерация пропускается
- **YAML merge anchors** — `environments` наследует общие Omni-переменные через `<<: *omni_common_environments`
- **Комментарии** — добавляются через `YamlComment` (например, пояснение про Redis)

### 5.6 Результат в config.yml

```yaml
variables:
  omni_chatbot_token: 'a1b2c3...64hex...'
  omni_chatbot_matrix_token: 'f4e5d6...64hex...'
  omni_chatbot_user: 'OmniBot'
  omni_chatbot_user_password: ''

services_config:
  ChatBot:
    port: 44324
    environments:
      <<: *omni_common_environments
      GeneralSettings__ChatBotSecurityToken: '{{ omni_chatbot_token }}'
      MatrixSettings__MatrixToken: '{{ omni_chatbot_matrix_token }}'
      ConnectionStrings__IdentityService: 'Name=...;Host={{ omni_ids_host }};...'
      ConnectionStrings__Synapse: 'Name=...;User ID={{ omni_chatbot_user }};...'
      QueueSettings__0__ConnectionString: 'hostName=;port=;...'
      # Redis нужен только в случае нескольких экземпляров бота
      RedisSettings__Host: ''
      RedisSettings__Port: ''
```

---

## 6. HAProxy: regsub routing

### Механизм

HAProxy — единая точка входа для всех HTTP-сервисов RX. Каждый микросервис регистрирует свой backend.

### Frontend

Frontend `omni` слушает на порту 8080 (HTTP) или 443 (HTTPS). ACL-правила маршрутизируют запросы по path:

```
frontend omni
    bind *:8080
    # ...
    acl chatbot_path path_beg -i /chatbot/
    use_backend chatbot_backend if chatbot_path
```

### Backend

```
backend chatbot_backend
    balance roundrobin
    http-request set-path %[path,regsub(^/chatbot/,/,i)]
    server chatbot_1 127.0.0.1:44324
```

### regsub — перезапись пути

`regsub(^/chatbot/,/,i)` — удаляет префикс `/chatbot/` из пути:

| Входящий запрос | После regsub |
|-----------------|--------------|
| `/chatbot/api/status` | `/api/status` |
| `/chatbot/health` | `/health` |
| `/chatbot/` | `/` |

Это позволяет .NET-сервису не знать о своём URL-префиксе.

### Roundrobin

`balance roundrobin` — для масштабирования. Можно поднять несколько экземпляров ChatBot на разных портах. В этом случае потребуется Redis для синхронизации токенов между экземплярами.

---

## 7. JSON Schema UI для Launcher

### Файлы

```
schema/
  __init__.py
  config_schema.py                      # Загрузка и мерж схем
  chatbot_component.schema.en.json      # English
  chatbot_component.schema.ru.json      # Русский
```

### Структура схемы

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
          "type": "object",
          "properties": {
            "port": {
              "description": "Порт сервиса",
              "type": "integer",
              "default": 44324
            },
            "ssl_cert_pfx_path": {
              "description": "Путь к PFX-сертификату",
              "type": "string"
            },
            "ssl_cert_pfx_password": {
              "description": "Пароль PFX-сертификата",
              "type": "string",
              "ui:options": { "Hidden": true }
            },
            "environments": {
              "title": "Переменные окружения",
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

### ui-атрибуты

| Атрибут | Назначение | Где используется |
|---------|-----------|------------------|
| `"ui:control": "ListWithDetail"` | Рендер секции как список с детализацией | `services_config` |
| `"ui:options": {"Hidden": true}` | Скрытое поле (пароли, токены) | `ssl_cert_pfx_password` |
| `"default": 44324` | Значение по умолчанию | `port` |

### Загрузка схемы (config_schema.py)

```python
def get_json_schema(locale):
    schema_path = f"chatbot_component.schema.{locale}.json"
    return json_tools.load(schema_path) or {}

def _merge_localized_schema(locale):
    default = get_json_schema('en')           # English — базовая
    localized = get_json_schema(locale)       # Локализованная
    return gen_config_schema._merge_localized_schema_dict(default, localized)
```

English-схема — базовая. Локализованные схемы мержатся поверх. Метод `get_json_schema()` компоненты вызывается Launcher для отображения настроек в UI.

---

## 8. Publish: .tar.gz + ComponentManifest

### Структура архива

```
ChatBot.tar.gz
  ChatBot/                    # .NET-бинарники (MatrixBotService)
  chatbot_plugin/             # Python-плагин (весь каталог)
  version.txt                 # Версия (строка)
  component_manifest.json     # Манифест компоненты
```

### ComponentManifest

```python
ComponentManifest(
    name='ChatBot',
    version=version,
    description={"ru": "Чат бот", "en": "ChatBot"},
    dependencies={},
    plugin_path="./chatbot_plugin",
    runtime=ManifestRuntime.LINUX
)
```

| Поле | Назначение |
|------|-----------|
| `name` | Имя компоненты (совпадает с `COMPONENT_NAME`) |
| `version` | Версия (передаётся при сборке) |
| `description` | Локализованное описание |
| `dependencies` | Зависимости на другие компоненты (пустой dict у ChatBot) |
| `plugin_path` | Относительный путь к Python-плагину внутри архива |
| `runtime` | `LINUX` — только Linux |

### manifest.json (корневой)

```json
{
    "name": "chatbot",
    "version": "26.1.0.9349",
    "description": {"ru": "Чат бот", "en": "ChatBot"},
    "plugin_path": "./chatbot_plugin",
    "runtime": "linux"
}
```

### Процесс публикации (publish.py)

```python
@action
@Help.hide
def publish_chatbot_component(build_folder_path: str, version: str):
    target_path = os.path.join(common_paths.etc_path, 'dist', COMPONENT_NAME)
    zip_file_name = os.path.join(target_path, f"{COMPONENT_NAME}.tar.gz")

    with create_tmp_path() as tmp_publish:
        with tarfile.open(zip_file_name, "w:gz") as package:
            # 1. .NET-бинарники
            package.add(os.path.join(build_folder_path, "MatrixBotService"), COMPONENT_NAME)
            # 2. Python-плагин
            package.add(chatbot_plugin_path, "chatbot_plugin")
            # 3. version.txt
            package.add(version_file_path, 'version.txt')
            # 4. Манифест
            manifest = ComponentManifest(COMPONENT_NAME, version, ...)
            package.add(manifest_file_path, COMPONENT_MANIFEST_FILE_NAME)

    # Инвалидация кэша Launcher
    package_state_path = get_package_state_file_path(zip_file_name)
    json_tools.dump(package_state_path, asdict(ComponentPackageState(changed=True)))
```

Вызов: `do publish_chatbot_component --build_folder_path=/path/to/build --version=26.1.0.9349`

### Кэширование

После создания `.tar.gz` записывается `ComponentPackageState(changed=True)`. Launcher проверяет этот файл и пересобирает компоненту при следующей установке, если `changed=True`. Имя архива не меняется между версиями, поэтому механизм state-файла обязателен.

---

## 9. Docker integration

### Volumes

.NET-сервис запускается как Docker-контейнер. Типичные volumes:

```yaml
volumes:
  - ./ChatBot:/app                          # бинарники сервиса
  - /var/log/directum/ChatBot:/app/logs     # логи
  - /etc/ssl/certs:/etc/ssl/certs:ro        # сертификаты (если HTTPS)
```

### Networks

Контейнер подключается к Docker network `directum` (bridge), где доступны:
- PostgreSQL (5432)
- RabbitMQ (5672)
- Elasticsearch (9200)
- IdentityService (HTTPS)
- Другие сервисы RX

### Ports

```yaml
ports:
  - "44324:44324"     # Kestrel .NET
```

Порт маппится на хост для HAProxy. В production HAProxy и сервис на одном хосте, поэтому `127.0.0.1:44324` достаточно.

### Переменные окружения

Все переменные из секции `environments` в `config.yml` передаются контейнеру через `-e` флаги Docker или `environment:` в docker-compose. Launcher резолвит `{{ variable }}` перед запуском.

### Health check (Docker)

```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:44324/health"]
  interval: 30s
  timeout: 10s
  retries: 3
```

Но основной healthcheck идёт через HAProxy endpoint `/chatbot/{health_url}` — он проверяет всю цепочку.

---

## 10. Reference-файлы

### ChatBot Plugin (эталон Service Plugin)

| Файл | Назначение |
|------|-----------|
| `chatbot_plugin/__init__.py` | `PluginMetadata(is_root=True)` + импорт `chatbot_service` |
| `chatbot_plugin/common_consts.py` | `COMPONENT_NAME='ChatBot'`, `ALIAS='chatbot'`, `DEFAULT_PORT=44324` |
| `chatbot_plugin/chatbot_component.py` | `ChatBotComponent(BaseComponent)` — компонент Launcher |
| `chatbot_plugin/chatbot_service.py` | `ChatBot(DotNetServiceBase)` — сервис-обёртка |
| `chatbot_plugin/default_settings.py` | Генерация `config.yml`: env, variables, wrap_variable |
| `chatbot_plugin/publish.py` | Сборка `.tar.gz` с манифестом |
| `chatbot_plugin/schema/config_schema.py` | Загрузка JSON Schema по локали |
| `chatbot_plugin/schema/chatbot_component.schema.en.json` | JSON Schema (English) |
| `chatbot_plugin/schema/chatbot_component.schema.ru.json` | JSON Schema (русский) |
| `manifest.json` | Корневой манифест компоненты |

### Платформенные базовые классы

| Класс | Модуль | Назначение |
|-------|--------|-----------|
| `BaseComponent` | `components.base_component` | Базовый компонент Launcher |
| `DotNetServiceBase` | `sungero_deploy.base_dotnet_service` | Базовый .NET-сервис (Docker/systemd) |
| `ComponentManifest` | `sungero_publish.component_manifest` | Манифест для публикации |
| `service_finder` | `sungero_deploy.service_finder` | Реестр сервисов (all up/down/status) |

### Смежные гайды

| Гайд | Связь |
|------|-------|
| [32_rc_plugin_development.md](32_rc_plugin_development.md) | Общие паттерны плагинов Launcher |
| [34_applied_solution_packaging.md](34_applied_solution_packaging.md) | Applied Plugin (альтернативный тип) |
| [36_launcher_internals.md](36_launcher_internals.md) | Внутренности DirectumLauncher |
| [38_platform_integration_map.md](38_platform_integration_map.md) | Карта интеграций: Launcher <-> HAProxy <-> сервисы |
| `docs/platform/PLUGIN_PATTERNS_CATALOG.md` | Сравнение трёх архетипов плагинов (ChatBot, OmniApplied, Targets) |

---

## Рецепт: создание нового Service Plugin

### Минимальная структура

```
my_plugin/
  __init__.py                    # PluginMetadata(is_root=True)
  common_consts.py               # COMPONENT_NAME, ALIAS, DEFAULT_PORT
  my_component.py                # MyComponent(BaseComponent) + @component(alias=...)
  my_service.py                  # MyService(DotNetServiceBase) + service_finder.add
  default_settings.py            # env + variables + add_settings_to_config()
  publish.py                     # @action publish_my_component(build_folder, version)
  schema/
    __init__.py
    config_schema.py
    my_component.schema.en.json
    my_component.schema.ru.json
  translations/{en,ru}/LC_MESSAGES/messages.{po,mo}
```

### Чеклист

1. **common_consts.py** — задать `COMPONENT_NAME`, `ALIAS`, `DEFAULT_PORT`
2. **my_service.py** — наследовать `DotNetServiceBase`, переопределить `healthcheck_endpoint` и `add_haproxy_settings()`
3. **my_component.py** — наследовать `BaseComponent`, реализовать `install()`, `get_install_steps_v2()`, `generate_config_v2()`
4. **default_settings.py** — определить `env`, `variables`, `add_settings_to_config()` с идемпотентностью
5. **schema/** — JSON Schema для каждой локали, `config_schema.py` для загрузки
6. **publish.py** — `@action` для сборки `.tar.gz` с `ComponentManifest`
7. **translations/** — `.po` файлы с ключами `{alias}.steps.*`, `{alias}.install_complete`, `{alias}.update_complete`
8. **manifest.json** — корневой манифест с `plugin_path`, `runtime`, `version`
