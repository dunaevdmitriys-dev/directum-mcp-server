# Сравнение сред разработки: DDS (старая) vs CrossPlatform DS (новая)

## Общие сведения

| Параметр | DDS (Development Studio) | CrossPlatform DS |
|----------|--------------------------|-------------------|
| **Алиас компоненты** | `dds` | `ds` |
| **Версия (26.1)** | 26.1.0.0056 | 26.1.0.0060 |
| **Runtime** | Windows only | Windows (+ Linux для Desktop*) |
| **Плагин** | `dds_plugin/development_studio.py` | `ds_plugin/crossplatform_development_studio.py` |
| **Описание** | "Среда разработки Directum RX" | "Кроссплатформенный инструмент разработки" |

> *Desktop-версия поддерживает Linux через Electron + `unprivileged_userns`

---

## Архитектура

### DDS (старая)
- **UI-фреймворк**: WPF + DevExpress v12.2 + SharpDevelop 4.x (форк ICSharpCode)
- **Платформа**: .NET Framework 4.8 (Windows-only)
- **Exe**: `DevelopmentStudio.exe` (~1 МБ) — нативное WPF-приложение
- **Расширения**: .addin XML-манифесты (ICSharpCode AddIn Tree)
  - `Sungero.DevelopmentStudio.addin` — основной плагин (~28 КБ)
  - `Development.addin`, `ResourceEditor.addin` — дополнительные
- **Редактор кода**: ICSharpCode.AvalonEdit4 (встроенный)
- **Кодогенерация**: Antlr4 StringTemplate (~3136 .st шаблонов)
- **Графы workflow**: GraphSharp + GraphX WPF + OxyPlot
- **Git**: LibGit2Sharp (встроенный)
- **IntelliSense**: Собственный на базе ICSharpCode.SharpDevelop.Dom + NRefactory
- **Reporting**: FastReport (встроенный редактор отчётов)
- **DLL в bin/**: ~94 файлов в корне + ~288 в AddIns/Sungero/

### CrossPlatform DS (новая)
- **UI-фреймворк**: Electron (Chromium + Node.js) — кроссплатформенный
- **Бекенд**: .NET 8 (ASP.NET, порт 7190 по умолчанию)
- **Exe**: `DevelopmentStudio` (из ZIP ~200 МБ, Electron-приложение)
- **Расширения**: VS Code Extension (.vsix) для навигации по коду
- **Редактор кода**: НЕТ встроенного — работает совместно с VS Code / другими IDE
- **Кодогенерация**: На стороне бекенда (тот же движок, вызывается через HTTP API)
- **Git**: Системный git (настраивается отдельно)
- **IntelliSense**: Через ms-dotnettools.csharp (OmniSharp) в VS Code
- **Два режима**: Desktop (Electron app) / Web (через IIS, путь `/ds`)
- **Reporting**: Не встроен (отдельный инструмент)

---

## Установка и конфигурация

### DDS
```
do dds install
```
**Шаги установки:**
1. Генерация `config.yaml` секции `DevelopmentStudio`
2. Генерация `_ConfigSettings.xml`
3. Установка Git (если не установлен) + настройка `user.name` / `user.email`
4. Создание ярлыка "Development studio.lnk" → `do dds run -sm`
5. Перезапуск WebServer + WebClient

**Ключевые настройки:**
- `COMPANY_CODE` — код компании (2-7 символов, латиница)
- `SYSTEM_NAME` — имя системы (по умолчанию "Develop")
- `GIT_ROOT_DIRECTORY` — корневая папка репозиториев
- `REPOSITORIES` — список репозиториев (work + base)
- `LOCAL_WEB_RELATIVE_PATH` — адрес отладочного веб-клиента
- `LOCAL_WEB_PROTOCOL` / `LOCAL_SERVER_HTTPS_PORT` / `LOCAL_SERVER_HTTP_PORT`
- `SERVICE_RUNNER_CONFIG_PATH` — путь к конфигу ServiceRunner
- `QUEUE_CONNECTION_STRING` — строка подключения к очереди
- `UNIQUE_NAMES_IN_OVERRIDES` — уникальные имена при перекрытиях
- `DEV_STUDIO_CONFIG_PATH` — путь к `_ConfigSettings.xml`
- `SAVE_NOCODE_SETTINGS_TO_SOURCES` — сохранять NoCode-настройки
- `SHOW_DETAILED_INTERNAL_SERVER_EXCEPTION` — детальные ошибки

**Секция devstand_config:**
```yaml
devstand_config:
  SAVE_NOCODE_SETTINGS_TO_SOURCES: 'true'
  SHOW_DETAILED_INTERNAL_SERVER_EXCEPTION: 'true'
  DEV_STUDIO_CONFIG_PATH: <path_to_configsettings>
```

### CrossPlatform DS
```
do ds install        # Desktop (по умолчанию)
do ds install --w    # Web-версия (только Windows)
```
**Шаги установки (Desktop):**
1. `generate_config_yaml_for_desktop` — генерация секции `DevelopmentStudioDesktop`
2. `extract_dds_archive` — распаковка Electron-приложения из ZIP
3. `generate_config_settings` — генерация `_ConfigSettings.xml`
4. `create_shortcut` — создание ярлыка
5. `install_vscode_extension` — установка `.vsix` через `code --install-extension`
6. Перезапуск WebServer + WebClient

**Шаги установки (Web):**
1. `generate_config_yaml_for_web` — генерация секции `DevelopmentStudioWeb`
2. `up` — запуск web-сервиса через ServiceRunner + настройка IIS
3. `install_vscode_extension`
4. Перезапуск WebServer + WebClient

**Ключевые настройки:**
- `COMPANY_CODE` — код компании
- `GIT_ROOT_DIRECTORY` — корневая папка репозиториев
- `REPOSITORIES` — список репозиториев (work + base, **без @url**)
- `DEPLOY_USERNAME` / `DEPLOY_PASSWORD` — аккаунт для публикации
- `LOCAL_WEB_RELATIVE_PATH` / `LOCAL_WEB_PROTOCOL` / порты
- `HELP_URI` — адрес веб-справки
- `LOGS_PATH` — папка логов (обязательно)
- `WEB_HOST_HTTP_PORT` — порт web-версии (автогенерируемый)
- `ds_port` (в variables) — порт бекенда Desktop (по умолчанию 7190)

**Секция devstand_config:**
```yaml
devstand_config:
  DEV_STUDIO_URL: 'http://{{ host_fqdn }}:{{ ds_port }}'  # Desktop
  # или
  DEV_STUDIO_URL: 'http://localhost/ds'                     # Web
```

---

## Работа с репозиториями

### DDS
```yaml
REPOSITORIES:
  repository:
    - "@folderName": "work"
      "@solutionType": "Work"
      "@url": ""                    # <-- есть URL репозитория
    - "@folderName": "base"
      "@solutionType": "Base"
      "@url": ""
```
- Git встроен через LibGit2Sharp
- Автоматическая настройка `git config --global user.name/email`
- Типы решений: `Work` (разрабатываемое) и `Base` (базовое)

### CrossPlatform DS
```yaml
REPOSITORIES:
  repository:
    - "@folderName": "work"
      "@solutionType": "Work"      # <-- нет URL
    - "@folderName": "base"
      "@solutionType": "Base"
```
- Использует системный git (не встроенный)
- Git должен быть установлен заранее
- Те же типы решений: Work / Base

---

## Режимы работы

### DDS
- **Единственный режим**: Desktop WPF-приложение
- Запуск: `do dds run` → `DevelopmentStudio.exe`
- Кодировка: `cp1251` (Windows)
- Всё встроено: редактор кода, дерево решения, свойства, сборка, публикация, отчёты

### CrossPlatform DS
- **Desktop**: Electron-приложение
  - Запуск: `do ds run` → извлечение из ZIP + запуск `DevelopmentStudio`
  - Бекенд слушает на порту `ds_port` (7190)
  - URL: `http://{host_fqdn}:{ds_port}`
- **Web**: Сервис через IIS
  - Запуск: `do dsweb up`
  - URL: `http://localhost/ds` (или `https://` при наличии SSL)
  - IIS-правила: `allow_access_only_for_localhost`, `trailing_slash`, `removing_powered_by_header`
  - Запрет Web на Linux: `The web version can only be installed on Windows`
- **VS Code Extension**: Дополнение к обоим режимам
  - Навигация к исходникам Public/Remote функций
  - Переход к .resx и .sql
  - Горячая клавиша `Ctrl+Alt+R`

---

## Кодогенерация и шаблоны

### DDS
- **3136 файлов** StringTemplate (.st) в `AddIns/Sungero/Templates/`:
  - 16 директорий: `Action`, `Blocks`, `Clients`, `Contexts`, `DataBinders`, `Entity`, `EntityHelpers`, `IsolatedArea`, `LayerModules`, `Libraries`, `Module`, `NoCode`, `Report`, `Solution`, `Widgets`, `Workflow`
  - Каждый шаблон генерирует конкретный .cs файл (обёртки, интерфейсы, фабрики)
- **Генераторы** (.dll): `Sungero.Generators.Databook`, `.Document`, `.Interfaces`, `.IsolatedArea`, `.LayerModule`, `.Module`, `.Report`, `.Workflow`
- **IntelliSense XML**: `AddIns/Sungero/Libraries/` — XML-документация для автодополнения
- Библиотеки: `Sungero.Development.Common.dll`, `Sungero.Development.Services.dll`, `Sungero.Development.dll`, `Sungero.Metadata.DesignTime.dll`

### CrossPlatform DS
- Шаблоны — на стороне .NET-бекенда (вероятно, те же StringTemplate, но внутри Electron/backend)
- Конкретные .st файлы **не поставляются** в виде отдельных файлов — упакованы в Electron-приложение
- Генерация вызывается через HTTP API бекенда

---

## Публикация (Deploy)

### DDS
- Публикация через встроенный UI DevelopmentStudio.exe
- Параметры передаются из devstand_config:
  - `SAVE_NOCODE_SETTINGS_TO_SOURCES`
  - `SHOW_DETAILED_INTERNAL_SERVER_EXCEPTION`
  - `DEV_STUDIO_CONFIG_PATH` → `_ConfigSettings.xml` → ServiceRunner
- Автоматический перезапуск WebServer + WebClient при установке

### CrossPlatform DS
- Параметры публикации:
  - `DEPLOY_USERNAME` / `DEPLOY_PASSWORD` — аккаунт (поддерживает шифрование `AvailableForEncrypt`)
  - URL через devstand_config → `DEV_STUDIO_URL`
- Публикация через бекенд на порту 7190 (Desktop) или IIS (Web)
- Автоматический перезапуск WebServer + WebClient при установке

---

## IIS-интеграция

### DDS
- Прямое управление IIS: создание сайтов, настройка портов
- devstand_config прокидывается в WebServer через YAML merge

### CrossPlatform DS (Web)
- `DevelopmentStudioWeb` — полноценный PlatformService
- Создаёт подпапку `/ds` в IIS-сайте
- Настраивает:
  - Ограничение доступа (только localhost)
  - Trailing slash redirect
  - Убирает заголовок `X-Powered-By`
  - Reverse proxy к бекенду (`localhost:{WEB_HOST_HTTP_PORT}`)
- Desktop-версия: IIS не используется, прямое подключение к бекенду

---

## VS Code Extension (только CrossPlatform DS)

| Параметр | Значение |
|----------|----------|
| **Имя** | Development Studio |
| **Издатель** | sungero |
| **Версия** | 1.0.2 |
| **Зависимости** | ms-dotnettools.csharp (OmniSharp) |
| **Активация** | `workspaceContains:**/*.csproj` или `onLanguage:csharp` |
| **Точка входа** | `./dist/extension.js` (webpack bundle) |

**Возможности:**
- Навигация к исходному коду Public/Remote функций (вместо сгенерированных обёрток)
- Навигация к структурам и их свойствам
- Переход к файлам .resx (строки ресурсов)
- Переход к файлам .sql (запросы к БД)

**Настройки:**
- `sungero.resourseLanguageDefault`: `en` (default) / `ru`
- `sungero.queryTypeDefault`: `postgres` (default) / `mssql`

**Горячие клавиши:**
- `Ctrl+Alt+R` — показать окно просмотра ресурсов и запросов

---

## Совместимость и миграция

- Обе среды знают о существовании друг друга:
  - DDS проверяет `CrossPlatformDevelopmentStudio` при удалении настроек
  - DS проверяет `DevelopmentStudio` при удалении devstand_config
- Обе используют одинаковые параметры: `COMPANY_CODE`, `GIT_ROOT_DIRECTORY`, `REPOSITORIES`, `LOCAL_WEB_*`
- **Нельзя** установить обе одновременно в UI (взаимоисключающие чекбоксы через `disabled_when`)
- При миграции: devstand_config сохраняется, если хоть одна среда установлена
- Формат решений (work/base) — **одинаковый** в обеих средах

---

## Сравнение для автоматизации (Claude Code)

| Критерий | DDS | CrossPlatform DS |
|----------|-----|-------------------|
| **CLI-доступ** | `do dds run` (GUI-only) | Бекенд на порту 7190 (HTTP API) |
| **Headless-сборка** | Невозможна без GUI | Потенциально через HTTP API бекенда |
| **Публикация через CLI** | Только через GUI DDS | Через `DEPLOY_USERNAME/PASSWORD` + API |
| **Интеграция с IDE** | Встроенный редактор | VS Code + Extension |
| **Доступ к шаблонам** | 3136 .st файлов доступны | Упакованы внутри Electron |
| **Формат пакетов** | .dat (через GUI) | .dat + git repos |
| **Возможность скриптинга** | Ограничена (`do dds run -sm`) | Больше возможностей через API |

### Рекомендации для автоматизации
1. **CrossPlatform DS Desktop** — предпочтительнее для автоматизации:
   - HTTP API бекенда (порт 7190) потенциально позволяет вызывать сборку/публикацию без GUI
   - Требует исследования API-эндпоинтов бекенда
2. **DDS** — подходит для ручной работы, но ограничена для скриптинга
3. **Оба** используют git-репозитории с одинаковой структурой work/base

---

## Зависимости

### DDS (ключевые)
- .NET Framework 4.8
- DevExpress v12.2 (WPF controls)
- ICSharpCode.SharpDevelop 4.x (IDE core)
- LibGit2Sharp (Git)
- FastReport (отчёты)
- NHibernate (ORM)
- Microsoft.CodeAnalysis (Roslyn)
- Antlr4.StringTemplate (кодогенерация)

### CrossPlatform DS (ключевые)
- Electron (Chromium + Node.js)
- .NET 8 (бекенд)
- VS Code + ms-dotnettools.csharp (IDE)
- Системный Git
- webpack + TypeScript (Extension)

---

## Резюме

**DDS** — монолитная WPF-среда с полным набором встроенных инструментов (редактор, сборка, публикация, отчёты, workflow-дизайнер, git). Работает только на Windows. Все операции — через GUI.

**CrossPlatform DS** — модульная Electron-среда, которая делегирует редактирование кода VS Code, а собственную логику (дерево решений, сборка, публикация) предоставляет через UI и HTTP API бекенда. Поддерживает Linux (Desktop) и два режима работы. Разделение на IDE (VS Code) и инструмент разработки (DS) открывает возможности для автоматизации через CLI/API.

Переход с DDS на CrossPlatform DS — это переход от **монолитной IDE** к **микросервисной архитектуре** с внешним редактором кода.
