---
description: "Создать BaseRnDComponent-based installer для прикладного решения DirectumLauncher"
---

# Создание инсталлера прикладного решения (BaseRnDComponent)

Reference: Guide 34. Компонент для публикации .dat-пакетов через DeploymentTool.

## MCP Tools
Нет специфичных MCP-инструментов. Scaffold вручную по шаблону.

## ШАГ 0: Найди рабочий пример (ОБЯЗАТЕЛЬНО)

```
# Эталоны:
#   omniapplied/omni_plugin/omni_installer.py — OmniAppliedComponent
#   targets/targets_plugin/targets_installer.py — TargetsComponent
#
# Общий базовый класс (КОПИРОВАТЬ в каждый плагин):
#   omniapplied/omni_plugin/base_component.py — BaseRnDComponent
#   targets/targets_plugin/base_component.py — BaseRnDComponent (идентичен)
#
# Манифесты:
#   omniapplied/manifest.json — с dependencies на Platform + DirectumRX
#   targets/manifest.json — с dependency на base
```

## Входные данные
- **solution_name** — имя решения в RX (например, `DirRX.MyModule`)
- **plugin_name** — snake_case имя плагина (например, `mymodule`)
- **alias** — alias для @component (обычно = plugin_name)
- **package_file** — имя .dat файла (например, `MyModule.dat`)
- **display_name_ru** / **display_name_en** — описание для manifest.json
- **version** — версия (например, `1.0.261.1`)
- **dependencies** — зависимости manifest.json (например, `{"base": ">=26"}`)
- **has_templates** — есть ли папка с шаблонами документов
- **has_public_api** — нужно ли поднимать PublicAPI
- **extra_packages** — доп. пакеты для deploy chain (опционально)
- **integration_service_solution** — имя решения в сервисе интеграции

## Алгоритм

### 1. Структура файлов

```
{plugin_name}/
  manifest.json
  {package_file}           # .dat файл прикладной разработки
  client/                  # статика для веб-клиента (если есть)
  templates/               # шаблоны документов (если has_templates)
  {plugin_name}_plugin/
    __init__.py
    base_component.py      # КОПИЯ из omniapplied (идентичная!)
    {plugin_name}_installer.py
```

### 2. manifest.json

```json
{
  "name": "{{plugin_name}}",
  "version": "{{version}}",
  "description": {
    "en": "{{display_name_en}}",
    "ru": "{{display_name_ru}}"
  },
  "plugin_path": "./{{plugin_name}}_plugin",
  "dependencies": {{dependencies_json}}
}
```
Типичные dependencies: `{"base": ">=26"}` или `{"Platform": ">=26.1", "DirectumRX": ">=26.1"}`.

### 3. __init__.py

```python
# coding: utf-8
""" Модуль загрузки плагина. """
from py_common.plugins import PluginMetadata, import_package_modules


def plugin_metadata() -> PluginMetadata:
    """ Метаданные плагина """
    return PluginMetadata(is_root=True)


import_package_modules(__file__, __package__)
```

### 4. base_component.py — СКОПИРОВАТЬ ИДЕНТИЧНО

Копировать файл `omniapplied/omni_plugin/base_component.py` без изменений.
Содержит класс `BaseRnDComponent(BaseComponent)` с полной логикой:
- `install()` / `uninstall()` — установка/удаление через DeploymentTool
- `get_install_steps_v2()` — шаги для UI-инсталлера (deploy + webclient up)
- `get_min_ui_model_v2()` — чекбокс "установить" в UI
- `check(tenant)` — проверка через IntegrationService OData
- `import_templates()` — импорт шаблонов документов
- `sungero_public_api_up()` — поднятие PublicAPI (если UP_PUBLIC_API=True)

**ВАЖНО:** Этот файл должен быть одинаковым во всех решениях команды.

### 5. {plugin_name}_installer.py — ГЛАВНЫЙ ФАЙЛ

```python
# coding: utf-8
""" Модуль плагина для {{display_name_en}}. """
import os
import os.path
from typing import List, Any
from .base_component import BaseRnDComponent
from components.base_component import ComponentData
from components.component_manager import component, ComponentManager
from py_common.actions import get_action_str
from py_common.help_levels import Help
from sungero_deploy.scripts_config import get_default_config_path, ConfigSourceType
from platform_plugin.check_incompatibility import (
    get_solutions_from_db, get_applied_solutions_info_from_packages
)
from sungero_deploy.tools.sungerodb import SungeroDB

try:
    from platform_plugin.services.sungero_web_client import SungeroWebClient
except ImportError:
    from sungero_deploy.services.sungero_web_client import SungeroWebClient

# Чтобы не падало на версиях без UI инсталлера
try:
    from components.ui_models import InstallStep
    from flask_babel import _
except ImportError:
    pass

{{UPPER_ALIAS}}_ALIAS = '{{alias}}'


@component(alias={{UPPER_ALIAS}}_ALIAS)
class {{ComponentClass}}(BaseRnDComponent):
    """ Компонент {{display_name_en}}. """

    def __init__(self, config_path: ConfigSourceType = None) -> None:
        super(self.__class__, self).__init__(config_path)

        # === Константы BaseRnDComponent ===
        self.COMPONENT_ALIAS = {{UPPER_ALIAS}}_ALIAS
        self.PACKAGE_NAME = '{{solution_name}}'
        self.PACKAGE_FILE_NAME = '{{package_file}}'
        self.STATIC_URL_PATH = '{{plugin_name}}'
        self.UI_INSTALL_BY_DEFAULT = True
        self.LOCALIZATION_PREFIX = "{{plugin_name}}"
        self.INTEGRATION_SERVICE_SOLUTION = "{{integration_service_solution}}"
        # self.TEMPLATES_FOLDER = "templates"  # раскомментировать если has_templates
        # self.UP_PUBLIC_API = True             # раскомментировать если has_public_api

        # === Собственные константы решения ===
        self.SOLUTION_PACKAGE_FILE = '{{package_file}}'
        self.SOLUTION_NAME = '{{solution_name}}'
        self.FOLDER_NAME = '{{plugin_name}}'

    def get_install_steps_v2(self, data: ComponentData) -> List[InstallStep]:
        """ Получить список шагов установки. """
        steps = super().get_install_steps_v2(data)
        # Добавить доп. шаги если нужно (например, update_config_settings):
        # steps.append(InstallStep(
        #     display_name=_("{{plugin_name}}.ui.custom_step"),
        #     action=get_action_str(custom_action, config_path=data.config_path)
        # ))
        return steps

    @Help.hide
    def get_install_package_path(self) -> str:
        """ Получить путь файла прикладной разработки. """
        # Multi-package deploy chain: RX base + наш пакет
        from py_common.actions import Action
        Base = Action.get_class("rx")
        rx_path = Base.get_applied_package_file_name()
        package_path = os.path.join(self._component_path, self._package_file_name)
        package_path = f"{rx_path};{package_path}"

        # Дополнительный пакет если решение уже развёрнуто отдельно
        extra = self._get_extra_package_path()
        if extra:
            package_path = f"{package_path};{extra}"
        return package_path

    def _get_extra_package_path(self) -> str:
        """ Получить доп. пакет если решение уже развёрнуто. """
        config_path = get_default_config_path()
        db = SungeroDB(config_path)
        if not db.is_db_exist(db.db_name):
            return None

        deployed_solutions = get_solutions_from_db(config_path)
        package_path = os.path.join(self._component_path, self._package_file_name)
        solutions_from_packages = get_applied_solutions_info_from_packages(package_path)
        deployed_names = set(s.Name for s in deployed_solutions)

        if (self.SOLUTION_NAME in deployed_names
                and self.SOLUTION_NAME not in solutions_from_packages):
            folder = ComponentManager.get_component_folder(self.FOLDER_NAME)
            return os.path.join(folder, self.SOLUTION_PACKAGE_FILE)
        return None
```

### 6. Multi-package deploy chain (важно!)

При deploy нескольких пакетов через `;`:
```
rx_base.dat;MyModule.dat;ExtraDep.dat
```
DeploymentTool публикует их в порядке перечисления. Порядок важен для зависимостей.

Паттерн из omniapplied — `get_install_package_path()` строит цепочку:
1. RX base (обязательный) — через `Action.get_class("rx").get_applied_package_file_name()`
2. Основной пакет — `{component_folder}/{package_file}`
3. Доп. пакет (если решение уже развёрнуто отдельно) — проверка через `SungeroDB`

### 7. Локализация (translations)

Создать ключи в файлах локализации DirectumLauncher:
```
# sungero_settingslayer_localization / messages.po:
{{plugin_name}}.ui.install = "Установить {{display_name_ru}}"
{{plugin_name}}.ui.installing = "Установка {{display_name_ru}}..."
{{plugin_name}}.ui.webclient = "Обновление веб-клиента..."
{{plugin_name}}.update_complete = "{{display_name_ru}} обновлён"
{{plugin_name}}.install_complete = "{{display_name_ru}} установлен"
```

## Валидация после создания

- [ ] `base_component.py` идентичен файлу из omniapplied
- [ ] `COMPONENT_ALIAS` совпадает со значением в `@component(alias=...)`
- [ ] `PACKAGE_NAME` совпадает с именем решения в RX (точно как в DDS)
- [ ] `PACKAGE_FILE_NAME` совпадает с реальным .dat файлом в папке компоненты
- [ ] `manifest.json` — `plugin_path` начинается с `./`
- [ ] `manifest.json` — dependencies указаны корректно
- [ ] Deploy chain: порядок пакетов соблюдён (base RX первый)
- [ ] Ключи локализации добавлены

## Частые ошибки
- `PACKAGE_NAME` не совпадает с реальным именем решения в БД — deploy ломается
- `base_component.py` отличается от других решений — нарушен контракт команды
- Нет зависимости `DirectumRX` в manifest.json — плагин может загрузиться раньше RX
- `get_install_package_path` не включает RX base — deploy не проходит из-за зависимостей
- `self.TEMPLATES_FOLDER` указан но папки templates нет — import_templates падает
- `super(self.__class__, self).__init__` — используется именно такой вызов, НЕ `super().__init__`
