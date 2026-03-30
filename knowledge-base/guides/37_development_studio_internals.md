# DevelopmentStudio (CrossPlatform DS) — Полная внутренняя архитектура

## Обзор

CrossPlatform Development Studio (alias `ds`) — кроссплатформенная IDE для разработки модулей Directum RX, пришедшая на замену старой DDS (alias `dds`).

| Параметр | Значение |
|----------|----------|
| **Версия** | 26.1.0.0073 |
| **Имя компоненты** | `CrossPlatformDevelopmentStudio` |
| **CLI alias** | `ds` |
| **UI-фреймворк** | Electron (Chromium + Node.js) |
| **Бекенд** | .NET 8 (ASP.NET Core) |
| **Порт бекенда по умолчанию** | 7190 |
| **Требования** | .NET SDK 8.0.415 |
| **manifest.json** | `"name": "crossplatformdevelopmentstudio"`, `"runtime": "linux"` |
| **Описание (ru)** | Кроссплатформенный инструмент разработки |
| **Описание (en)** | Cross Platform Development Studio |

Два режима работы:
- **Desktop** — Electron-приложение (Linux, Windows, macOS)
- **Web** — сервис через IIS (только Windows)

---

## Архитектура

### Electron Frontend

- `app.asar` — упакованное Electron-приложение (JavaScript/TypeScript UI)
- Отображает дерево решений, свойства сущностей, сборку, публикацию
- Делегирует редактирование кода VS Code (через расширение `.vsix`)
- Общается с .NET бекендом по HTTP на порту 7190 (настраиваемый через `ds_port`)

### .NET Backend

Точка входа: `Sungero.DevelopmentStudio.Desktop.Host.dll` (ASP.NET Core).
Связь с Electron осуществляется через `ElectronNET.API.dll` (мост .NET <-> JavaScript).

Ключевые DLL (из состава приложения):

| DLL | Назначение |
|-----|-----------|
| `Sungero.DevelopmentStudio.Desktop.Host.dll` | Главный хост бекенда |
| `ElectronNET.API.dll` | Мост .NET <-> Electron (IPC) |
| `GraphX.Standard.dll` | Визуализация workflow и графов зависимостей |
| `DocumentFormat.OpenXml.dll` | Работа с документами OpenXml (docx, xlsx) |
| `Elasticsearch.Net.dll` | Интеграция с Elasticsearch |

Конфигурация бекенда хранится в `_ConfigSettings.xml` (XML-формат), генерируется автоматически из `config.yml`.

### Docker (Web mode)

Dockerfile для контейнерного развертывания (из `PlatformBuilds/26.1.0.0073/linux-x64/DevelopmentStudioDesktop/Dockerfile`):

```dockerfile
ARG docker_tag="registry.directum.ru/directum/rx-base/sdk:26.1.0"
FROM $docker_tag
LABEL "Name"="DevelopmentStudioWeb"
ARG APP_DIR=/app
WORKDIR $APP_DIR
ENV PATH=$PATH:$APP_DIR
COPY DevelopmentStudioDesktop.tar.gz ./
RUN tar -xzf DevelopmentStudioDesktop.tar.gz && rm DevelopmentStudioDesktop.tar.gz && cp -r resources/bin/* . && rm -rf resources
ENTRYPOINT ["dotnet", "Sungero.DevelopmentStudio.Desktop.Host.dll"]
```

Детали:
- **Базовый образ:** `registry.directum.ru/directum/rx-base/sdk:26.1.0` (.NET 8 SDK)
- **Label:** `DevelopmentStudioWeb` (несмотря на имя архива `DevelopmentStudioDesktop.tar.gz` — используется один и тот же артефакт)
- **Entrypoint:** `dotnet Sungero.DevelopmentStudio.Desktop.Host.dll` — тот же хост, что и в Desktop-режиме
- Архив распаковывается, содержимое `resources/bin/` копируется в `/app`, остальное удаляется

---

## Launcher Plugin (ds_plugin/)

### Структура плагина

```
ds_plugin/
├── __init__.py                          # Загрузка плагина (PluginMetadata, is_root=True)
├── crossplatform_development_studio.py  # Главный компонент (555 строк)
├── development_studio_desktop.py        # Desktop-режим (145 строк)
├── development_studio_web.py            # Web-режим через IIS (104 строки)
├── development_studio_common.py         # Общие утилиты
├── iis_tools.py                         # IIS URL Rewrite правила
├── dotnet_requirements.yml              # Требуемая версия .NET SDK
├── extensions/
│   └── development-studio-vscode-extension.vsix  # Расширение VS Code
├── schema/
│   ├── config_schema.py                 # Генерация JSON-схемы с локализацией
│   ├── ds_component.schema.ru.json      # UI-схема (русский)
│   └── ds_component.schema.en.json      # UI-схема (английский)
├── translations/
│   ├── ru/LC_MESSAGES/messages.po       # Русская локализация
│   └── en/LC_MESSAGES/messages.po       # Английская локализация
└── tests/                               # Тесты
```

Плагин регистрируется как `is_root=True` — загружается автоматически при старте Launcher.

### CrossPlatformDevelopmentStudio (главный класс)

**Файл:** `crossplatform_development_studio.py` (555 строк)
**Декоратор:** `@component(alias="ds")`
**Наследует:** `BaseComponent`

#### Константы класса

| Константа | Значение | Описание |
|-----------|----------|----------|
| `_COMPANY_CODE` | `"COMPANY_CODE"` | Настройка кода компании |
| `_LOGS_SHARED_ENABLED_SETTING_NAME` | `"LOGS_SHARED_ENABLED"` | Флаг записи логов в shared-папку |
| `_LOGS_PATH_SECTION_NAME` | `"logs_path"` | Секция логирования в config.yml |
| `_DDS_COMPONENT_NAME` | `"DevelopmentStudio"` | Имя старой DDS-компоненты |
| `_DS_PORT` | `"ds_port"` | Настройка порта бекенда |
| `_DS_DEFAULT_PORT` | `7190` | Порт по умолчанию |
| `_DEVSTAND_CONFIG` | `"devstand_config"` | Секция конфига локального стенда |
| `_DEV_STUDIO_URL` | `"DEV_STUDIO_URL"` | URL среды разработки |
| `_SUNGERO_WEB_SERVER` | `"SungeroWebServer"` | Имя сервиса веб-сервера |
| `_SUNGERO_WEB_CLIENT` | `"SungeroWebClient"` | Имя сервиса веб-клиента |
| `_PLATFORM_COMPONENT_NAME` | `"Platform"` | Имя компоненты платформы |

#### Методы

**`install(**kwargs)`** — Установка компоненты.
- Флаг `--w` определяет Web-режим (по умолчанию Desktop)
- На Linux Web-версия запрещена: `RuntimeError('The web version can only be installed on Windows.')`
- На Linux Desktop проверяет `unprivileged_userns` — если не разрешено, выбрасывает ошибку
- Загружает config.yml, получает шаги установки через `get_install_steps_v2()`, выполняет их

**`uninstall()`** — Деинсталляция.
- Если есть секция `DevelopmentStudioWeb` — останавливает сервис (`DSWeb.down()`), удаляет из YAML
- Если есть секция `DevelopmentStudioDesktop` — удаляет из YAML, удаляет ярлык

**`generate_config_yaml_for_desktop()`** — Генерация секции `DevelopmentStudioDesktop` в `config.yml`.

**`generate_config_yaml_for_web()`** — Генерация секции `DevelopmentStudioWeb` в `config.yml`.

**`generate_config_yaml_for_dev_env_desktop()`** — Генерация для dev-среды, дополнительно добавляет `LOGS_SHARED_ENABLED: False`.

**`_add_section_into_yaml(version_name, section_updater)`** — Центральный метод генерации секции config.yml. Добавляет все параметры с значениями по умолчанию:
- `ds_port` в секцию `variables` (свободный порт начиная с 7190)
- `COMPANY_CODE`: `"Sungero"`
- `GIT_ROOT_DIRECTORY`: `{{ home_path }}/git_repository`
- `REPOSITORIES`: `work` (Work) + `base` (Base)
- `DEPLOY_USERNAME` / `DEPLOY_PASSWORD`: из существующих настроек аутентификации
- `LOCAL_WEB_RELATIVE_PATH`: из настроек WebHost
- `LOCAL_WEB_PROTOCOL`: `https` при наличии SSL-сертификата, иначе `http`
- `LOCAL_SERVER_HTTPS_PORT` / `LOCAL_SERVER_HTTP_PORT`: из настроек IIS
- `HELP_URI`: если задан
- Merge секции `logs_path`
- Вызов `_set_devstand_config()`

**`_set_devstand_config(yaml_config, version_name)`** — Заполняет секцию `devstand_config`:
- Desktop: `DEV_STUDIO_URL: "http://{{ host_fqdn }}:{{ ds_port }}"`
- Web: `DEV_STUDIO_URL: "http://localhost/ds"`
- Устанавливает YAML-anchor `devstand_config` и мержит в `SungeroWebServer`

**`_remove_devstand_config(yaml_config)`** — Удаляет `devstand_config` из YAML и из merge-map `SungeroWebServer`.

**`_delete_settings_from_yaml(version_name)`** — Удаляет секции из config.yml:
1. Секцию версии из `services_config`
2. `ds_port` из `variables`
3. `DEV_STUDIO_URL` из `devstand_config`
4. Если нет ни DDS, ни DS — удаляет `devstand_config` целиком

**`config_up()`** — Создание файла настроек, делегирует в `DSDesktop.config_up()`.

**`configure_ui_variables_v2(data)`** — Настройка UI-переменных для веб-установщика:
- DS и Base/DirectumRX — взаимоисключающие (`disabled_when`, `value_when`)
- При установке DS: `host_fqdn` устанавливается в IP-адрес хоста
- `PRIMARY_TENANT` блокируется на `"Local"`
- `protocol` блокируется на `"http"` (для обеих сред: DS и DDS)

**`get_json_schema(locale)`** — Возвращает JSON-схему компоненты из `config_schema.py`.

**`run()`** — Запуск DS, делегирует в `DSDesktop.run()`.

**`get_min_ui_model_v2(data)`** — Минимальная UI-модель: одно поле `COMPANY_CODE` (oneline_string_control).

**`get_install_steps_v2(data)`** — Формирование списка шагов установки (подробно см. раздел "Installation Flow").

**`validate_v2(data)`** — Валидация:
1. Проверка `unprivileged_userns` на Linux
2. Проверка `inotify_user_watches_limit`
3. Проверка наличия .NET SDK
4. Валидация `COMPANY_CODE`: 2-7 символов, `^[a-zA-Z][a-zA-Z0-9]+$`

**`install_vscode_extension()`** — Статический метод. Вызывает `code --install-extension <path_to_vsix>`. При ошибке — предупреждение в лог.

**`post_install(data)`** — Сообщение после установки: "успешно установлен" / "успешно обновлен".

**`need_allow_unprivileged_userns()`** — Проверяет необходимость разрешения unprivileged user namespaces:
- Только для Linux
- Проверяет `unprivileged_userns_clone == 0` ИЛИ `apparmor_restrict_unprivileged_userns == 1`

**`_find_service(service_name)`** — Поиск сервиса Platform по имени для перезапуска WebServer/WebClient.

### DevelopmentStudioDesktop

**Файл:** `development_studio_desktop.py` (145 строк)
**Декоратор:** `@action(alias="dsdesktop")`
**Наследует:** `ToolContract`

| Атрибут | Значение |
|---------|----------|
| `_tool_name` | `"DevelopmentStudioDesktop"` |
| `_component_name` | `"CrossPlatformDevelopmentStudio"` |
| `_executable` | `"DevelopmentStudio"` |

#### Методы

**`config_up()`** — Генерация файла настроек `_ConfigSettings.xml`, делегирует в `generate_config_settings()`.

**`generate_config_settings()`** — Создание `_ConfigSettings.xml`:
1. Получает маппинг хост-значений (`get_default_tool_host_values_mapping()`)
2. Вычисляет итоговые настройки (`get_result_settings()`)
3. Конвертирует YAML-словарь в XML-формат (`config_yaml_dict_to_config_settings()`)
4. Сохраняет по пути `<_bin_path>/resources/bin/_ConfigSettings.xml`

**`run()`** — Запуск Desktop-приложения:
1. Вызывает `generate_config_settings()` (обновление конфига)
2. Запускает исполняемый файл через `process.try_execute()` с кодировкой `cp1251`
3. При ненулевом exit code — `IOError`

**`_get_exe_path(force_extract=False)`** — Получение пути к исполняемому файлу:
- Если `force_extract` — очищает папку `_bin_path` (таймаут 60 сек)
- Вызывает `get_or_extract()` — извлекает из архива, если еще не извлечен
- Путь к пакету определяется через `_get_package_path()`

**`_get_package_path()`** — Поиск пакета `.tar.gz`:
- Ищет файл по имени (с учетом RID) рекурсивно в папке компоненты
- Пример: `DevelopmentStudioDesktop.tar.gz` для `linux-x64`

**`create_shortcut()`** — Создание ярлыка:
- **Windows:** `.lnk` файл через `create_shortcut()`, команда: `do ds run -sm`
- **Linux:** `.desktop` файл через `create_shortcut_on_linux()`, с иконкой `icon.png`, команда: `do ds run -sm`

**`delete_shortcut()`** — Удаление ярлыка (Windows: `.lnk`, Linux: `.desktop`).

Класс также регистрируется через `service_finder.add_service_class()` и `add_after(config_up)` — для поддержки команды `do all config_up`.

### DevelopmentStudioWeb (Windows only)

**Файл:** `development_studio_web.py` (104 строки)
**Декоратор:** `@action(alias="dsweb")`, `@Help.internal`
**Наследует:** `PlatformService` (из `platform_plugin`)
**Резервный вариант:** Если `platform_plugin` не импортирован — создается класс-заглушка от `ServiceBase`.

| Константа | Значение |
|-----------|----------|
| `WEB_HOST_HTTP_PORT_SETTING_NAME` | `"WEB_HOST_HTTP_PORT"` |
| `DSWEB_RELATIVE_IIS_PATH` | `"ds"` |

#### Методы

**`up_on_servicerunner()`** — Запуск сервиса:
1. Добавляет сервисы компоненты в конфиг (`add_services_to_config_if_need`)
2. Вызывает `super().up_on_servicerunner()` — запуск через ServiceRunner
3. Настраивает IIS-сайт:
   - Создает папку `/ds` на сайте
   - Добавляет правило доступа только для localhost
   - Добавляет правило trailing slash
   - Настраивает reverse proxy на `localhost:{WEB_HOST_HTTP_PORT}`
   - Удаляет заголовок `X-Powered-By`

**`down_on_servicerunner()`** — Остановка сервиса через ServiceRunner.

**`_get_port()`** — Получение порта из настроек (`WEB_HOST_HTTP_PORT`).

**`get_host_values_mapping_on_servicerunner()`** — Автогенерация значений: добавляет маппинг `WEB_HOST_HTTP_PORT` с автоопределением порта.

**`show()`** — Открытие DS Web в браузере: `{protocol}://localhost:{port}/ds`.

**`check()`** — Заглушка: `"Check is not implemented for {service_name}."`.

### development_studio_common.py

Вспомогательный модуль с единственной функцией:

**`add_services_to_config_if_need(config, component_name, service_name, rid)`** — Добавляет информацию о сервисах компоненты в `service_dict_from_build`, если ее там нет. Используется при Web-развертывании для обеспечения доступности сервисов в методах класса-предка `PlatformService`.

### iis_tools.py

IIS URL Rewrite правила для Web-версии DS.

**`add_allow_access_only_for_localhost_rule(site, relative_path)`** — Создает правило IIS:
- Секция: `system.webServer/rewrite/rules`
- Имя правила: `"Allow request only for localhost rule"`
- Условие: `{HTTP_HOST}` НЕ равен `localhost` → `AbortRequest`
- `stopProcessing: True` — прекращает обработку при срабатывании
- Результат: DS Web доступен ТОЛЬКО через `localhost`, внешний доступ запрещен

---

## Конфигурация

### _ConfigSettings.xml

XML-файл, автоматически генерируемый из `config.yml`. Расположение: `<_bin_path>/resources/bin/_ConfigSettings.xml`.

Содержит все параметры из секции `DevelopmentStudioDesktop` или `DevelopmentStudioWeb` в XML-формате. Генерация выполняется методом `generate_config_settings()` через конвертацию YAML -> XML (`config_yaml_dict_to_config_settings()`).

### config.yml — секции

#### Секция `variables`

| Переменная | По умолчанию | Описание |
|------------|-------------|----------|
| `ds_port` | `7190` (или первый свободный) | Порт бекенда Desktop-версии |

#### Секция `services_config/DevelopmentStudioDesktop`

| Параметр | По умолчанию | Описание |
|----------|-------------|----------|
| `COMPANY_CODE` | `"Sungero"` | Код компании (2-7 символов, латиница + цифры, первый символ — буква) |
| `GIT_ROOT_DIRECTORY` | `{{ home_path }}/git_repository` | Корневая папка для git-репозиториев |
| `REPOSITORIES` | `work (Work) + base (Base)` | Список репозиториев решений |
| `DEPLOY_USERNAME` | Из `AUTHENTICATION_USERNAME` | Имя аккаунта для публикации |
| `DEPLOY_PASSWORD` | Из `AUTHENTICATION_PASSWORD` | Пароль для публикации |
| `LOCAL_WEB_RELATIVE_PATH` | Из `WEB_HOST_PATH_BASE` | Путь к отладочному веб-клиенту |
| `LOCAL_WEB_PROTOCOL` | `http` / `https` (по SSL) | Протокол обмена |
| `LOCAL_SERVER_HTTPS_PORT` | Из настроек IIS | Порт HTTPS |
| `LOCAL_SERVER_HTTP_PORT` | Из настроек IIS | Порт HTTP |
| `HELP_URI` | Из общих настроек | URL веб-справки |
| `LOGS_PATH` | Из секции `logs_path` | Папка для логов (обязательный) |
| `WEB_HOST_HTTP_PORT` | `{{ ds_port }}` | Порт HTTP-хоста (Desktop) |

#### Секция `services_config/DevelopmentStudioWeb`

Все те же параметры, что и Desktop, плюс:

| Параметр | По умолчанию | Описание |
|----------|-------------|----------|
| `WEB_HOST_HTTP_PORT` | `auto` (автоопределение) | Порт сервиса (автогенерируемый) |

#### Секция `devstand_config`

| Параметр | Desktop | Web |
|----------|---------|-----|
| `DEV_STUDIO_URL` | `http://{{ host_fqdn }}:{{ ds_port }}` | `http://localhost/ds` |

Эта секция:
- Создается как YAML-anchor `&devstand_config`
- Мержится в `SungeroWebServer` через YAML merge (`<<: *devstand_config`)
- Удаляется только если ни DS, ни DDS не установлены

#### REPOSITORIES — формат

```yaml
REPOSITORIES:
  repository:
    - "@folderName": "work"
      "@solutionType": "Work"
    - "@folderName": "base"
      "@solutionType": "Base"
```

- `Work` — разрабатываемое решение (рабочая копия)
- `Base` — базовое решение (эталон платформы)
- В отличие от старой DDS, нет параметра `@url` — используется системный git

### JSON Schema (UI Installer)

Файлы: `ds_component.schema.ru.json` и `ds_component.schema.en.json`.

Определяют UI-отображение параметров в веб-установщике (`ui_installer`):

**DevelopmentStudioDesktop** (title: "Инструмент разработки" / "Development Studio"):
- Обязательные: `LOGS_PATH`, `GIT_ROOT_DIRECTORY`, `REPOSITORIES`, `COMPANY_CODE`
- Шифруемые (`AvailableForEncrypt: true`): `DEPLOY_USERNAME`, `DEPLOY_PASSWORD`
- Проверка директории (`DirPathShouldExists: true`): `LOGS_PATH`

**DevelopmentStudioWeb** (title: "DevelopmentStudioWeb", тег: `kind/dev`):
- Те же обязательные поля + `WEB_HOST_HTTP_PORT`
- `WEB_HOST_HTTP_PORT` автогенерируемый (`Autogenerated: true`)

Локализация JSON-схем выполняется через `config_schema.py`:
- Первоисточник: `ru`
- Метод `merge_localized_schema(locale)` — мержит русскую и локализованную схемы
- `get_json_schema(locale)` — загружает JSON-файл по пути

---

## Code Generation Pipeline

### Движок

CrossPlatform DS использует тот же движок кодогенерации, что и старая DDS:

- **StringTemplate (.st)** — шаблоны генерации (Antlr4 StringTemplate)
- **Antlr4** — парсинг метаданных
- **Roslyn** (Microsoft.CodeAnalysis) — анализ и компиляция C#

### Вход/Выход

- **Input:** `.mtd` файлы (JSON-метаданные сущностей, модулей, workflow)
- **Output:** `.cs` файлы (сгенерированные обертки, интерфейсы, фабрики, фильтры)

### Генераторы

В DDS явно присутствуют 8+ генераторов (DLL):

| Генератор | Назначение |
|-----------|-----------|
| `Sungero.Generators.Databook` | Справочники (Databook entities) |
| `Sungero.Generators.Document` | Документы |
| `Sungero.Generators.Interfaces` | Интерфейсы сущностей |
| `Sungero.Generators.IsolatedArea` | Изолированные области |
| `Sungero.Generators.LayerModule` | Перекрывающие модули |
| `Sungero.Generators.Module` | Модули |
| `Sungero.Generators.Report` | Отчеты |
| `Sungero.Generators.Workflow` | Бизнес-процессы |

В CrossPlatform DS эти генераторы упакованы внутри приложения и вызываются через HTTP API бекенда. Отдельные `.st` файлы не поставляются (в DDS их 3136 в `AddIns/Sungero/Templates/`).

### Категории шаблонов (16 директорий в DDS)

`Action`, `Blocks`, `Clients`, `Contexts`, `DataBinders`, `Entity`, `EntityHelpers`, `IsolatedArea`, `LayerModules`, `Libraries`, `Module`, `NoCode`, `Report`, `Solution`, `Widgets`, `Workflow`

---

## VS Code Extension

**Имя:** Development Studio
**Издатель:** sungero
**Версия:** 1.0.2
**Формат:** `.vsix` (webpack bundle, `./dist/extension.js`)
**Зависимость:** `ms-dotnettools.csharp` (OmniSharp)

### Установка

Автоматическая при `do ds install`:
```bash
code --install-extension <путь_к_vsix>
```
При ошибке — предупреждение в лог, ручная установка по тому же пути.

### Активация

- `workspaceContains:**/*.csproj` — при наличии C#-проекта в workspace
- `onLanguage:csharp` — при открытии C#-файла

### Возможности

- Навигация к исходному коду Public/Remote функций (вместо сгенерированных оберток)
- Навигация к структурам и их свойствам
- Переход к файлам `.resx` (строки ресурсов)
- Переход к файлам `.sql` (запросы к БД)

### Настройки

| Параметр | По умолчанию | Описание |
|----------|-------------|----------|
| `sungero.resourseLanguageDefault` | `en` | Язык ресурсов: `en` / `ru` |
| `sungero.queryTypeDefault` | `postgres` | Тип БД: `postgres` / `mssql` |

### Горячие клавиши

| Комбинация | Действие |
|------------|----------|
| `Ctrl+Alt+R` | Показать окно просмотра ресурсов и запросов |

---

## Git Integration

### Настройка репозиториев

В `config.yml` секция `REPOSITORIES` определяет git-репозитории:

```yaml
REPOSITORIES:
  repository:
    - "@folderName": "work"
      "@solutionType": "Work"
    - "@folderName": "base"
      "@solutionType": "Base"
```

- **Work** — рабочий репозиторий с разрабатываемым решением
- **Base** — базовый репозиторий с платформенными модулями
- Папки создаются внутри `GIT_ROOT_DIRECTORY` (по умолчанию `~/git_repository`)

### Отличие от DDS

| Аспект | DDS | CrossPlatform DS |
|--------|-----|------------------|
| Git-библиотека | LibGit2Sharp (встроенная) | Системный git |
| Настройка user.name/email | Автоматическая | Ручная (git должен быть установлен заранее) |
| URL репозиториев | Через `@url` в REPOSITORIES | Нет `@url` — управление через git CLI |

---

## Валидация

При установке и конфигурировании выполняются 4 проверки:

### 1. .NET SDK

Проверяется наличие .NET SDK версии `8.0.415` (определено в `dotnet_requirements.yml`):

```yaml
SDK:
  - 8.0.415
```

Реализация: `check_dotnet_requirement_version('sdk', 'dotnet_requirements.yml')`.
При отсутствии — pre-installation message: "Dotnet-SDK не установлен."

### 2. unprivileged_userns (только Linux, только Desktop)

Проверяется возможность создания user namespaces непривилегированными процессами (необходимо для Electron/Chromium sandbox):

- `unprivileged_userns_clone == 0` — user namespaces запрещены
- `apparmor_restrict_unprivileged_userns == 1` — AppArmor ограничивает

Решение: `./do.sh kernelconfig allow_unprivileged_userns`

### 3. inotify_user_watches

Проверяется лимит `inotify` file watchers (Linux). Должен быть >= 100K для корректной работы file watcher в Electron и VS Code.

Реализация: `check_inotify_user_watches_limit(validation_messages)`

### 4. COMPANY_CODE

Валидация кода компании:
- Длина: 2-7 символов
- Допустимые символы: `a-z`, `A-Z`, `0-9`
- Первый символ — обязательно буква
- Regex: `^[a-zA-Z][a-zA-Z0-9]+$`

---

## Installation Flow

### Desktop (6 шагов)

```
do ds install
```

1. **Генерация config.yml** (`generate_config_yaml_for_desktop`)
   - Создает секцию `DevelopmentStudioDesktop` в `services_config`
   - Добавляет `ds_port` в `variables`
   - Создает/обновляет `devstand_config` с `DEV_STUDIO_URL`
   - Параметры: COMPANY_CODE, GIT_ROOT_DIRECTORY, REPOSITORIES, DEPLOY_*, LOCAL_WEB_*, HELP_URI

2. **Распаковка архива** (`_get_exe_path --force_extract`)
   - Ищет `DevelopmentStudioDesktop.tar.gz` в папке компоненты
   - Очищает целевую папку `_bin_path` (таймаут 60 сек)
   - Извлекает исполняемый файл `DevelopmentStudio`

3. **Генерация _ConfigSettings.xml** (`generate_config_settings`)
   - Конвертирует YAML-настройки в XML
   - Сохраняет в `<_bin_path>/resources/bin/_ConfigSettings.xml`

4. **Создание ярлыка** (`create_shortcut`)
   - Windows: `Development studio.lnk` → `do ds run -sm`
   - Linux: `Development studio.desktop` → `do ds run -sm` (с иконкой `icon.png`)

5. **Установка VS Code Extension** (`install_vscode_extension`)
   - `code --install-extension development-studio-vscode-extension.vsix`

6. **Перезапуск WebServer + WebClient** (если установлена Platform)
   - `SungeroWebServer.up()` — для подхвата `devstand_config`
   - `SungeroWebClient.up()` — для обновления клиента

### Web (5 шагов, только Windows)

```
do ds install --w
```

1. **Генерация config.yml** (`generate_config_yaml_for_web`)
   - Создает секцию `DevelopmentStudioWeb` в `services_config`
   - `WEB_HOST_HTTP_PORT` автогенерируемый, `devstand_config.DEV_STUDIO_URL: "http://localhost/ds"`

2. **Запуск сервиса** (`DSWeb.up`)
   - Добавляет сервисы в `service_dict_from_build`
   - Запускает через ServiceRunner (`PlatformService.up_on_servicerunner`)
   - Настраивает IIS: папка `/ds`, reverse proxy, правила безопасности

3. **Установка VS Code Extension** (`install_vscode_extension`)

4. **Перезапуск WebServer** (если есть)

5. **Перезапуск WebClient** (если есть)

---

## Runtime Flow

### Desktop

```
do ds run
```

1. `CrossPlatformDevelopmentStudio.run()` → `DSDesktop.run()`
2. `generate_config_settings()` — обновление `_ConfigSettings.xml`
3. `_get_exe_path()` — если Electron не извлечен из архива, извлекает
4. `process.try_execute('"<exe_path>"', encoding='cp1251')` — запуск Electron
5. Electron запускает .NET бекенд → слушает на порту `ds_port` (7190)
6. Frontend (app.asar) обращается к бекенду по `http://localhost:7190`

### Web

```
do dsweb up
```

1. `DevelopmentStudioWeb.up_on_servicerunner()`
2. `add_services_to_config_if_need()` — регистрация сервисов
3. `PlatformService.up_on_servicerunner()` — запуск через ServiceRunner
4. IIS настраивается как reverse proxy:
   - Внешний URL: `http://localhost/ds`
   - Внутренний URL: `http://localhost:{WEB_HOST_HTTP_PORT}`
   - Доступ: только localhost (IIS URL Rewrite)
5. Открытие в браузере: `do dsweb show`

---

## Cross-Platform Support

### Runtime Identifiers (RID)

Поддерживаемые RID определяются через `get_rid_by_os()`:

| RID | Поддержка Desktop | Поддержка Web |
|-----|-------------------|---------------|
| `linux-x64` | Да | Нет |
| `linux-arm64` | Да (Electron) | Нет |
| `windows-x64` | Да | Да |
| `osx-arm64` | Да (Electron) | Нет |

### Ограничения по платформам

| Ограничение | Платформа | Детали |
|-------------|-----------|--------|
| Web-версия | Windows only | `RuntimeError('The web version can only be installed on Windows.')` |
| unprivileged_userns | Linux only | Требуется для Electron Chromium sandbox |
| inotify watches | Linux only | Лимит file watchers для Electron/VS Code |
| IIS | Windows only | Reverse proxy для Web-версии |
| .desktop shortcuts | Linux only | XDG Desktop Entry |
| .lnk shortcuts | Windows only | Windows Shell Link |
| cp1251 encoding | Desktop run | Передается в `process.try_execute()` |

### Зависимости по платформам

| Платформа | Обязательные зависимости |
|-----------|------------------------|
| **Linux** | .NET SDK 8.0.415, системный git, VS Code (опционально), unprivileged_userns, inotify >= 100K |
| **Windows** | .NET SDK 8.0.415, системный git, VS Code (опционально), IIS (для Web) |
| **macOS** | .NET SDK 8.0.415, системный git, VS Code (опционально) |

---

## DDS (Old) vs CrossPlatform DS (New) — Детальное сравнение

| Аспект | DDS (старая) | CrossPlatform DS (новая) |
|--------|-------------|-------------------------|
| **Alias** | `dds` | `ds` |
| **Версия (26.1)** | 26.1.0.0056 | 26.1.0.0073 |
| **UI-фреймворк** | WPF + DevExpress v12.2 | Electron (Chromium + Node.js) |
| **Бекенд** | .NET Framework 4.8 | .NET 8 (ASP.NET Core) |
| **Редактор кода** | Встроенный (ICSharpCode.AvalonEdit4) | VS Code + Extension (OmniSharp) |
| **IntelliSense** | Свой (NRefactory) | OmniSharp (ms-dotnettools.csharp) |
| **Git** | LibGit2Sharp (встроенный) | Системный git |
| **Отчеты** | FastReport (встроенный) | Отдельный инструмент |
| **Кроссплатформенность** | Windows only | Linux, Windows, macOS |
| **Режимы** | Desktop (единственный) | Desktop + Web |
| **Кодогенерация** | 3136 .st файлов (доступны) | Внутри Electron (HTTP API) |
| **Exe** | `DevelopmentStudio.exe` (~1 MB, WPF) | `DevelopmentStudio` (~200 MB, Electron) |
| **Порт бекенда** | Нет (встроенный) | 7190 (HTTP API) |
| **IIS** | Прямое управление | Reverse proxy для Web |
| **CLI headless** | Невозможен | Потенциально через HTTP API |
| **REPOSITORIES** | Есть `@url` | Нет `@url` |
| **devstand_config** | `DEV_STUDIO_CONFIG_PATH` + доп. | `DEV_STUDIO_URL` |
| **Конфиг** | `DevelopmentStudio` секция | `DevelopmentStudioDesktop` / `DevelopmentStudioWeb` |
| **Зависимости** | .NET Framework 4.8, DevExpress, SharpDevelop | .NET SDK 8.0.415, Electron, VS Code |
| **Ярлык** | `do dds run -sm` | `do ds run -sm` |

### Взаимоисключаемость

- В UI-установщике DS и Base/DirectumRX — взаимоисключающие чекбоксы (`disabled_when`)
- DS и DDS также взаимоисключающие: `protocol` блокируется на `http` для обеих
- При удалении одной среды — `devstand_config` сохраняется, если другая установлена
- При удалении обеих — `devstand_config` удаляется полностью

### Совместимость

- Формат решений (work/base) — одинаковый в обеих средах
- `COMPANY_CODE`, `GIT_ROOT_DIRECTORY`, `REPOSITORIES`, `LOCAL_WEB_*` — общие параметры
- Обе используют `_ConfigSettings.xml` (разная генерация, одинаковый формат)

### Миграция DDS -> CrossPlatform DS

1. Деинсталляция DDS: `do dds uninstall`
2. Установка DS: `do ds install`
3. `devstand_config` пересоздается с новым `DEV_STUDIO_URL`
4. WebServer и WebClient перезапускаются автоматически
5. Git-репозитории (`work`/`base`) переиспользуются без изменений
6. VS Code Extension устанавливается автоматически

---

## Локализация (i18n)

Строки интерфейса хранятся в `translations/{locale}/LC_MESSAGES/messages.po` (gettext формат через Flask-Babel).

### Ключевые строки (русский)

| Ключ | Текст |
|------|-------|
| `ds.var.company_code` | Код компании |
| `ds.var.company_code.description` | Запросите в службе поддержки Directum RX. Используется в именах решений, модулей, сборок, пространств имен |
| `ds.validate.company_code` | Длина 2-7 символов. Только латинские буквы и цифры. Первый символ -- буква |
| `ds.validation.enable_unprivileged_userns_message` | Разрешите функцию 'unprivileged_userns' |
| `ds.validation.dotnet_runtime_not_installed.message` | Dotnet-SDK не установлен |
| `ds.steps.generate_config_yaml_for_desktop` | Генерация секции в config.yml для CrossPlatformDevelopmentStudio |
| `ds.steps.extract_dds_archive` | Распаковка архива CrossPlatformDevelopmentStudio |
| `ds.steps.generate_config_settings` | Генерация _ConfigSettings.xml для CrossPlatformDevelopmentStudio |
| `ds.steps.create_shortcut` | Создание ярлыка для CrossPlatformDevelopmentStudio |
| `ds.steps.install_vscode_extension` | Установка расширения для VSCode |
| `ds.steps.up` | Развертывание сервиса |
| `ds.steps.web_server_reboot` | Перезапуск веб-сервера |
| `ds.steps.web_client_reboot` | Перезапуск веб-клиента |
| `ds.install_complete` | CrossPlatformDevelopmentStudio успешно установлен. При необходимости импортируйте новый пакет разработки |
| `ds.update_complete` | CrossPlatformDevelopmentStudio успешно обновлен. При необходимости импортируйте новый пакет разработки |

---

## Справочник CLI-команд

| Команда | Описание |
|---------|----------|
| `do ds install` | Установка Desktop-версии |
| `do ds install --w` | Установка Web-версии (Windows) |
| `do ds uninstall` | Деинсталляция |
| `do ds run` | Запуск Desktop |
| `do ds config_up` | Генерация _ConfigSettings.xml |
| `do ds generate_config_yaml_for_desktop` | Генерация config.yml для Desktop |
| `do ds generate_config_yaml_for_web` | Генерация config.yml для Web |
| `do ds install_vscode_extension` | Установка расширения VS Code |
| `do dsdesktop run` | Прямой запуск Desktop |
| `do dsdesktop config_up` | Прямая генерация конфига Desktop |
| `do dsdesktop generate_config_settings` | Генерация _ConfigSettings.xml |
| `do dsdesktop create_shortcut` | Создание ярлыка |
| `do dsweb up` | Запуск Web-сервиса |
| `do dsweb down` | Остановка Web-сервиса |
| `do dsweb show` | Открыть DS Web в браузере |
