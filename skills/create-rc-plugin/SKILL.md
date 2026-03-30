---
description: "Создать Python RC-плагин для DirectumLauncher (компонент do.sh)"
---

# Создание RC-плагина DirectumLauncher

Reference: Guide 32. Два типа плагинов: Type A (микросервис) и Type B (applied solution).

## MCP Tools
Нет специфичных MCP-инструментов. Scaffold выполняется вручную по шаблону.

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Type A (микросервис — DotNetServiceBase):
#   chatbot/chatbot_plugin/ — __init__.py, chatbot_component.py, chatbot_service.py,
#     default_settings.py, common_consts.py, schema/config_schema.py

# Type B (applied solution — BaseRnDComponent):
#   omniapplied/omni_plugin/ — __init__.py, omni_installer.py, base_component.py
#   targets/targets_plugin/ — __init__.py, targets_installer.py, base_component.py

# Манифесты:
#   chatbot/manifest.json, omniapplied/manifest.json, targets/manifest.json
```

## Входные данные
- **plugin_name** — snake_case имя (например, `mywidget`)
- **plugin_type** — `A` (микросервис) или `B` (applied solution)
- **display_name_ru** / **display_name_en** — описание для manifest.json
- **version** — версия (например, `26.1.0.1`)
- Для Type A: **port**, **health_endpoint**, **haproxy_path_prefix**
- Для Type B: **package_name**, **package_file**, **solution_name**, **dependencies**

## Алгоритм

### 1. Создать директорию плагина

```
{plugin_name}/
  manifest.json
  {plugin_name}_plugin/
    __init__.py
    common_consts.py          # только Type A
    {plugin_name}_component.py  # только Type A
    {plugin_name}_service.py    # только Type A
    default_settings.py         # только Type A
    schema/
      __init__.py               # только Type A
      config_schema.py          # только Type A
    base_component.py           # только Type B (скопировать из omniapplied)
    {plugin_name}_installer.py  # только Type B
```

### 2. manifest.json

```json
{
  "name": "{{plugin_name}}",
  "version": "{{version}}",
  "description": {
    "ru": "{{display_name_ru}}",
    "en": "{{display_name_en}}"
  },
  "plugin_path": "./{{plugin_name}}_plugin",
  "runtime": "linux",
  "dependencies": {
    "Platform": ">=26.1"
  }
}
```
Для Type A добавь `"runtime": "linux"`. Для Type B добавь зависимости `"DirectumRX": ">=26.1"`.

### 3. __init__.py (общий для обоих типов)

```python
# coding: utf-8
""" Модуль загрузки плагина. """
from py_common.plugins import PluginMetadata, import_package_modules


def plugin_metadata() -> PluginMetadata:
    """ Метаданные плагина """
    return PluginMetadata(is_root=True)


# Type A: явный импорт сервиса для регистрации в service_finder
# from . import {plugin_name}_service

import_package_modules(__file__, __package__)
```

### 4. common_consts.py (Type A only)

```python
""" Модуль с общими константами. """

COMPONENT_NAME = '{{ComponentName}}'  # PascalCase, ключ в services_config
ALIAS = '{{plugin_name}}'             # snake_case, alias для @component
DEFAULT_PORT = {{port}}
```

### 5. {plugin_name}_service.py (Type A only)

```python
""" Модуль сервиса {{ComponentName}}. """
from {{plugin_name}}_plugin import common_consts
from py_common.help_levels import Help
from sungero_deploy import service_finder
from sungero_deploy.base_dotnet_service import DotNetServiceBase, add_frontend_function
from sungero_deploy.generate_haproxy_config import (
    get_haproxy_config_path, remove_backend,
    get_default_backend_server_settings, add_backend, get_frontend_name
)
from typing import Callable


@Help.show_only_linux
class {{ComponentName}}(DotNetServiceBase):
    """ Класс для работы с {{ComponentName}}. """
    default_port = common_consts.DEFAULT_PORT
    component_name = common_consts.COMPONENT_NAME

    add_frontend: Callable[[], None] = add_frontend_function

    @property
    def healthcheck_endpoint(self) -> str:
        """ Вернуть URL для healthcheck через HAProxy. """
        return f"{self._host_authority}/{{haproxy_path_prefix}}/{self.health_url}"

    def add_haproxy_settings(self) -> None:
        haproxy_config_path = get_haproxy_config_path(self.config)
        remove_backend(
            get_default_backend_server_settings(self.instance_service, self.service_config.port),
            haproxy_config_path
        )
        backend_settings = [
            ('balance', 'roundrobin'),
            ('http-request', 'set-path %[path,regsub(^/{{haproxy_path_prefix}}/,/,i)]'),
        ]
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


# Регистрация сервиса
service_finder.add_service_class(service_finder.ServiceClassInfo(type={{ComponentName}}))
```

### 6. {plugin_name}_component.py (Type A only)

```python
""" Модуль плагина для {{ComponentName}}. """
from typing import Any
from common_plugin import yaml_tools
from components import base_component, ui_models
from components.component_manager import component, ComponentManager
from components.component_version import get_installed_component
from flask_babel import _
from py_common.actions import get_action_str
from sungero_deploy import scripts_config
from ui_installer import steps_executor
from ui_installer.install_modes import InstallModes

from {{plugin_name}}_plugin import common_consts
from {{plugin_name}}_plugin.default_settings import add_settings_to_config
from {{plugin_name}}_plugin.{{plugin_name}}_service import {{ComponentName}} as Service
from .common_consts import COMPONENT_NAME
from .schema import config_schema


@component(alias=common_consts.ALIAS)
class {{ComponentName}}Component(base_component.BaseComponent):
    """ Группа команд для работы с {{ComponentName}}. """

    def __init__(self, config_path: scripts_config.ConfigSourceType = None):
        super().__init__(config_path)
        self.service = Service(self.config)
        self._component_path = ComponentManager.get_component_folder(COMPONENT_NAME)

    def install(self, **kwargs: Any) -> None:
        scripts_config.backup(self.config_path)
        yaml_config = yaml_tools.load_yaml_from_file(self.config_path)
        install_steps = self.get_install_steps_v2(
            base_component.ComponentData(
                config_path=self.config_path,
                install_mode=self._get_install_mode(),
                yaml_config=yaml_config, ui_vars=[])
        )
        steps_executor.execute_steps(install_steps)

    def get_install_steps_v2(self, data: base_component.ComponentData) -> list[ui_models.InstallStep]:
        install_steps: list[ui_models.InstallStep] = []
        install_steps.extend([
            ui_models.InstallStep(
                display_name=_("{{plugin_name}}.steps.up"),
                action=get_action_str({{ComponentName}}Component,
                                      config_path=data.config_path, _args='up')
            ),
            ui_models.InstallStep(
                display_name=_("{{plugin_name}}.steps.check"),
                action=get_action_str({{ComponentName}}Component,
                                      config_path=data.config_path, _args='check')
            )
        ])
        return install_steps

    def up(self) -> None:
        self.service.up()

    def down(self) -> None:
        self.service.down()

    def restart(self) -> None:
        self.service.restart()

    def start(self) -> None:
        self.service.start()

    def stop(self) -> None:
        self.service.stop()

    def check(self) -> None:
        self.service.check()

    def generate_config_v2(self, data: base_component.ComponentData) -> None:
        base_component.add_omni_common_vars_to_config(data.yaml_config)
        add_settings_to_config(data.yaml_config)

    def get_json_schema(self, locale: str | None) -> dict[str, Any]:
        return config_schema.get_json_schema(locale)

    def get_component_name(self) -> str:
        return COMPONENT_NAME

    def _get_install_mode(self) -> str:
        if get_installed_component(self.get_component_name()):
            return InstallModes.Update
        return InstallModes.Install
```

### 7. {plugin_name}_installer.py (Type B only)

См. skill `create-solution-installer` для полного шаблона BaseRnDComponent-based installer.

### 8. Валидация после создания

- Проверить `manifest.json` содержит корректный `plugin_path`
- Проверить `__init__.py` импортирует сервис (Type A) или модули (Type B)
- Проверить `@component(alias=...)` совпадает с `ALIAS` / `{PLUGIN}_ALIAS`
- Для Type A: `service_finder.add_service_class()` вызван в конце `_service.py`
- Для Type B: `base_component.py` скопирован идентично из omniapplied

## Частые ошибки
- Забыли `service_finder.add_service_class()` — сервис не виден в `do all up/down`
- `COMPONENT_NAME` в `common_consts.py` не совпадает с ключом в `services_config` YAML
- Нет `from . import {service_module}` в `__init__.py` — модуль сервиса не загружается
- `manifest.json` — `plugin_path` должен начинаться с `./`
