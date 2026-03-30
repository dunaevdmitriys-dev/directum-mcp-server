---
description: "Создать DotNetServiceBase микросервис-обёртку для DirectumLauncher"
---

# Создание микросервис-компоненты (DotNetServiceBase)

Reference: Guide 33. Обёртка вокруг .NET-сервиса с HAProxy, healthcheck, Docker-контейнером.

## MCP Tools
Нет специфичных MCP-инструментов. Scaffold вручную по шаблону.

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Эталон: chatbot/chatbot_plugin/chatbot_service.py
# Также: chatbot_plugin/default_settings.py, chatbot_plugin/schema/config_schema.py
# Схемы: chatbot_plugin/schema/chatbot_component.schema.ru.json
```

## Входные данные
- **service_name** — PascalCase имя сервиса (например, `NotificationHub`)
- **plugin_name** — snake_case имя плагина (например, `notificationhub`)
- **port** — порт по умолчанию (например, `44350`)
- **health_endpoint** — путь healthcheck (по умолчанию: `/health`)
- **haproxy_path_prefix** — префикс маршрута HAProxy (например, `notificationhub`)
- **env_vars** — словарь переменных окружения для сервиса

## Алгоритм

### 1. Структура файлов

```
{plugin_name}_plugin/
  common_consts.py
  {plugin_name}_service.py
  default_settings.py
  schema/
    __init__.py
    config_schema.py
    {plugin_name}_component.schema.en.json
    {plugin_name}_component.schema.ru.json
```

### 2. common_consts.py

```python
""" Модуль с общими константами. """

COMPONENT_NAME = '{{ServiceName}}'   # PascalCase ключ в services_config YAML
ALIAS = '{{plugin_name}}'            # snake_case alias для @component
DEFAULT_PORT = {{port}}
```

### 3. {plugin_name}_service.py — ГЛАВНЫЙ ФАЙЛ

```python
""" Модуль сервиса {{ServiceName}}. """
from {{plugin_name}}_plugin import common_consts
from py_common.help_levels import Help
from sungero_deploy import service_finder
from sungero_deploy.base_dotnet_service import DotNetServiceBase, add_frontend_function
from sungero_deploy.generate_haproxy_config import (
    get_haproxy_config_path, remove_backend,
    get_default_backend_server_settings, add_backend, get_frontend_name
)
from sungero_deploy.scripts_config import PredefinedVariables
from typing import Callable


@Help.show_only_linux
class {{ServiceName}}(DotNetServiceBase):
    """ Класс для работы с {{ServiceName}}. """
    default_port = common_consts.DEFAULT_PORT
    component_name = common_consts.COMPONENT_NAME

    add_frontend: Callable[[], None] = add_frontend_function

    @property
    def healthcheck_endpoint(self) -> str:
        """ Вернуть URL для healthcheck через HAProxy. """
        return f"{self._host_authority}/{{haproxy_path_prefix}}/{self.health_url}"

    def add_haproxy_settings(self) -> None:
        """ Настроить HAProxy backend для сервиса. """
        haproxy_config_path = get_haproxy_config_path(self.config)

        # Удалить старый backend если был
        remove_backend(
            get_default_backend_server_settings(
                self.instance_service, self.service_config.port
            ),
            haproxy_config_path
        )

        # Настройки backend
        backend_settings = [
            ('balance', 'roundrobin'),
            ('http-request', 'set-path %[path,regsub(^/{{haproxy_path_prefix}}/,/,i)]'),
        ]

        # ACL для маршрутизации по path prefix
        add_backend(
            get_default_backend_server_settings(
                self.instance_service,
                self.service_config.port,
                use_ssl=self.protocol == "https"
            ),
            backend_settings,
            "{ path_beg -i /{{haproxy_path_prefix}}/ } ",
            haproxy_config_path,
            frontend_name=get_frontend_name("omni")
        )


# ОБЯЗАТЕЛЬНО: регистрация сервиса в service_finder для do all up/down
service_finder.add_service_class(service_finder.ServiceClassInfo(type={{ServiceName}}))
```

**Ключевые точки DotNetServiceBase:**
- `default_port` — порт по умолчанию если не задан в config.yml
- `component_name` — ключ секции в `services_config` YAML
- `healthcheck_endpoint` — URL для проверки здоровья через HAProxy
- `add_haproxy_settings()` — вызывается при `up()`, настраивает reverse proxy
- `add_frontend` — callback регистрации frontend в HAProxy
- Наследуются методы: `up()`, `down()`, `restart()`, `start()`, `stop()`, `check()`

### 4. default_settings.py

```python
""" Модуль для работы с настройками по умолчанию. """
from typing import Any
from common_plugin import yaml_tools
from py_common.logger import log
from ruamel.yaml import CommentedMap
from sungero_deploy import common_consts as sungero_common_consts
from sungero_deploy.base_dotnet_service import add_omni_config_frontend_section
from sungero_deploy.scripts_config import wrap_variable, add_settings_to_config_section
from sungero_deploy.services_config import get_omni_common_config
from . import common_consts


# Переменные окружения контейнера сервиса
env: dict[str, Any] = {
    # Подставь реальные env-переменные сервиса:
    # 'ConnectionStrings__Default': 'Host=...;Port=...;Database=...',
    # 'SomeSettings__ApiKey': '',
}

# Комментарии к YAML-секциям (опционально)
env_comments = [
    # yaml_tools.YamlComment('KEY', 'comment text',
    #     yaml_tools.YamlCommentPosition.BEFORE, indent=12)
]

# Переменные для секции variables в config.yml
variables: dict[str, Any] = {
    # sungero_common_consts.SOME_VAR: 'default_value',
}

# Настройки сервиса по умолчанию
default_settings: dict[str, Any] = {
    common_consts.COMPONENT_NAME: {
        'port': common_consts.DEFAULT_PORT,
        'environments': CommentedMap(env)
    }
}


def add_settings_to_config(yaml_config: dict[str, Any] | CommentedMap) -> None:
    """ Добавить настройки сервиса в config.yml. """
    omni_common_config_section = get_omni_common_config(
        yaml_config, create_not_exists=True,
        anchors={
            sungero_common_consts.SERVICE_ENVIRONMENTS_SECTION_NAME:
                sungero_common_consts.OMNI_COMMON_ENVIRONMENTS_ANCHOR_NAME
        }
    )
    services_config_section = yaml_tools.get_or_create_dict_by_key(
        sungero_common_consts.SERVICES_CONFIG_SECTION_NAME, yaml_config
    )
    add_omni_config_frontend_section(yaml_config)

    # Не перезаписывать если уже есть
    if (
        isinstance(services_config_section, (CommentedMap, dict))
        and not set(services_config_section.keys()).isdisjoint(set(default_settings.keys()))
    ):
        log.info(f"No need to generate config. {common_consts.COMPONENT_NAME} already exists.")
        return

    variables_section = yaml_config[sungero_common_consts.VARIABLES_SECTION_NAME]
    omni_common_environments_section = \
        omni_common_config_section[sungero_common_consts.SERVICE_ENVIRONMENTS_SECTION_NAME]

    add_settings_to_config_section(
        settings_to_be_added=variables,
        section_to_add_settings=variables_section,
    )
    add_settings_to_config_section(
        settings_to_be_added=default_settings,
        section_to_add_settings=services_config_section,
        common_configs_to_be_merged=[yaml_config.get('logs_path')]
    )

    env_section = yaml_config[sungero_common_consts.SERVICES_CONFIG_SECTION_NAME][
        common_consts.COMPONENT_NAME]['environments']
    if len(env_section.merge) == 0:
        env_section.add_yaml_merge([(0, omni_common_environments_section)])
    yaml_tools.add_comments_to_section(env_section, env_comments)
```

### 5. schema/config_schema.py

```python
""" Генерация json схемы. """
import os
from typing import Any, Dict, cast
from common_plugin import json_tools, locale_tools
from py_common.actions import action
from py_common.help_levels import Help
from py_common.logger import log
from sungero_deploy.schema import gen_config_schema

_current_script_path: str = os.path.dirname(os.path.realpath(__file__))


def _get_json_schema_filepath(locale: str) -> str:
    return os.path.join(_current_script_path,
                        f"{{plugin_name}}_component.schema.{locale}.json")


def get_json_schema(locale: str | None) -> Dict[str, Any]:
    """ Вернуть JSON схему компоненты. """
    if not locale:
        locale = locale_tools.origin_locale
    schema_path = _get_json_schema_filepath(locale)
    schema = json_tools.load(schema_path) or {}
    if not schema:
        log.warning(f'Cannot load json schema from file "{schema_path}"')
    return cast(Dict[str, Any], schema)


@action
@Help.internal
def merge_localized_schema(locale: str | None = None) -> None:
    """ Актуализировать локализованную json схему. """
    if not locale:
        locale = locale_tools.origin_locale
    default_schema = get_json_schema(locale_tools._default_locale)
    localized_schema = get_json_schema(locale)
    merged = gen_config_schema._merge_localized_schema_dict(
        default_schema, localized_schema)
    path = _get_json_schema_filepath(locale)
    json_tools.dump(path, merged)
    log.info(f"'{locale}' localized schema merged to: '{path}'")
```

### 6. schema/__init__.py

```python
# coding: utf-8
```

### 7. HAProxy — шаблон настроек для config.yml

```yaml
# В services_config добавляется секция:
{{ServiceName}}:
  port: {{port}}
  environments:
    <<: *omni_common_environments
    # Специфичные env-переменные сервиса
```

HAProxy маршрутизирует запросы `path_beg -i /{{haproxy_path_prefix}}/` на backend,
который strip-ит prefix через `regsub(^/{{haproxy_path_prefix}}/,/,i)`.

## Валидация после создания

- [ ] `service_finder.add_service_class()` вызван — сервис виден в `do all up`
- [ ] `COMPONENT_NAME` совпадает с ключом в `services_config` YAML
- [ ] `DEFAULT_PORT` не конфликтует с существующими (проверь chatbot=44324)
- [ ] `haproxy_path_prefix` уникален и не пересекается с другими backend
- [ ] `healthcheck_endpoint` возвращает 200 OK когда сервис запущен
- [ ] `add_settings_to_config` не перезаписывает существующие настройки

## Частые ошибки
- `frontend_name=get_frontend_name("omni")` — если сервис НЕ omni, используй другой frontend
- Забыли `<<: *omni_common_environments` в YAML — сервис не получает общие переменные
- `regsub` паттерн не совпадает с `path_beg` — 404 от backend
- Порт занят другим сервисом — контейнер не стартует
