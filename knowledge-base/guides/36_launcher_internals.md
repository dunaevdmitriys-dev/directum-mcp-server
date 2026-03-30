# DirectumLauncher — Полная внутренняя архитектура

## Обзор

**DirectumLauncher** — Python-based CLI для установки, конфигурации и управления всеми сервисами Directum RX.

| Параметр | Значение |
|----------|----------|
| Версия | 26.1.2264.e865482b6c |
| Python | 3.14.2 (CPython, встроенный) |
| CLI-фреймворк | Google Fire 0.7.1 |
| Платформы | Linux x64 (GNU), Windows x64 (MSVC) |
| Точка входа | `./do.sh` (Linux) / `do.bat` (Windows) |

---

## Bootstrap (do.sh / do.bat)

### Linux: do.sh

```
dp0 = директория скрипта (readlink -f)
```

Последовательность:
1. Определяет `python_version_name` = `cpython-3.14.2-linux-gnu`
2. Проверяет наличие `tools/Python/cpython-3.14.2-linux-gnu/bin/python3` и `lib/python3.14/subprocess.py`
3. Если Python не извлечён — распаковывает `tools/CPython-linux.7z` через `tools/7zzs` (встроенный 7-Zip)
4. Запускает: `"$python_bin_path" -s "$dp0/src/main.py" "$@"`

Флаг `-s` отключает загрузку `site-packages` из пользовательского окружения.

### Windows: do.bat

Аналогичная логика, но:
- `python_version_name` = `cpython-3.14.2-windows-msvc`
- Архив: `CPython-windows.7z`, распаковщик: `tools/7z.exe`
- Поддержка **silent mode** (`-sm`): запускает `pythonw.exe` через `start ""` (без консольного окна)
- Поддержка **symlink-алиасов**: если `do.bat` — символическая ссылка, определяет корневой путь через переменную окружения с именем ссылки
- `PYTHONIOENCODING=UTF-8` выставляется явно

---

## Core Framework (src/py_common/)

### main.py — Точка входа

```python
if __name__ == "__main__":
    load_plugins()   # загрузка всех плагинов
    fire_actions()   # передача управления Google Fire
    sys.exit(get_exit_code())
```

Глобальные `@action` в main.py:
- `log_env` — лог версии Python, аргументов, cwd (internal)
- `install_plugin(path)` — установка плагина по пути
- `uninstall_plugin(name)` — удаление плагина по имени
- `assign_path(alias)` — создание entrypoint (symlink в /usr/bin или переменная окружения в Windows)
- `unassign_path(alias)` — удаление entrypoint
- `reconnect_completion` — переподключение автодополнения bash
- `refresh_completion` — обновление скрипта автодополнения

### actions.py — Система действий

Центральный модуль CLI. Реализует паттерн реестра команд.

**Ключевые структуры:**
- `_actions: Dict[str, ActionCallableType]` — глобальный реестр всех действий (вложенный словарь: namespace -> {action_name -> callable})
- `_actions["do"] = _actions` — self-reference для корневого доступа

**Декоратор `@action`:**
```python
@action                          # без alias
@action(alias="pg")              # с alias (короткое имя)
class SungeroPostgres(ServiceBase): ...
```

Логика регистрации:
1. Определяет `component_name` через `__qualname__`
2. Определяет `package_name` через `plugin_metadata().name` модуля
3. Если `is_root=True` — действие регистрируется на верхнем уровне `_actions`
4. Если у плагина есть `name` — создаётся namespace (подсловарь)
5. Alias добавляется как дубликат в тот же namespace

**Класс `Action` — DI-контейнер:**
```python
Action.get_class('DeploymentTool')      # получить класс
Action.get_instance('DeploymentTool')    # создать экземпляр
Action.get_function('log_env')           # получить функцию
```

**Вспомогательные функции:**
- `fire_actions()` — вызывает `execute_before_fire_actions()`, затем `fire.Fire(_actions)`
- `fire_action_str(action_str)` — программный вызов action по строке
- `get_action_str(component, **kwargs)` — генерация строки команды
- `replace_action(component, new_component)` — замена зарегистрированного action (включая alias)
- `generate_bash_completion_script(command, script_path)` — генерация скрипта автодополнения bash
- `Display()` — переопределяет `fire.core.Display` для вывода справки через `log.info`

### plugins.py — Загрузка плагинов

**Класс `PluginMetadata`:**
```python
class PluginMetadata:
    name: Optional[str]   # имя namespace в CLI (None = без namespace)
    is_root: bool         # True = actions регистрируются на верхнем уровне
```

**Порядок загрузки плагинов:**
1. `load_plugins()` — точка входа
2. Маскирует пароли в sys.argv (--password, --pass, --pwd, --secret, encrypt/decrypt)
3. Определяет контекст: deletion / update / test-run — для этих контекстов **не** загружает плагины из `etc/plugins`
4. Загружает плагины из `common_paths.plugin_paths` = `[plugins/, etc/plugins/]`
5. Для каждого каталога:
   - Добавляет путь в `sys.path`
   - Сортирует содержимое через `_sorted_plugin_path` (по умолчанию `iterdir()`, может переопределяться компонентами)
   - Вызывает `importlib.import_module(plugin_path.name)` для каждого Python-пакета
   - Если у плагина есть `lib/` — добавляет в `sys.path`

**Установка/удаление плагинов:**
- `install(plugin_path)` — создаёт symlink/junction в `etc/plugins/`, загружает модуль
- `uninstall(plugin_dir_name)` — удаляет symlink, очищает `_actions` от экшенов этого плагина
- `set_sorted_plugins_path(func)` — переопределяет порядок загрузки (используется `components` для топологической сортировки)

### logger.py — Логирование

**Класс `CustomLogger`:**

Три обработчика:
1. **current.log** — `FileHandler`, уровень DEBUG, перезаписывается при каждом запуске
2. **all.log** — `RotatingFileHandler`, уровень DEBUG, макс. 100 MB, 5 бэкапов
3. **Console (stdout)** — `StreamHandler`, уровень INFO, цветной вывод через `ColoredConsoleFormatter`

Формат: `%(asctime)s\t%(levelname)s\t%(message)s`

**Маскирование чувствительных данных:**
- Аргументы CLI с `--password`, `--pass`, `--pwd`, `--secret` маскируются `***`
- Значения для `encrypt`/`decrypt`/`to_base64` подкоманд маскируются
- Работает как для позиционных, так и для named-аргументов (с `=` и без)

**Потоковый контекст:**
```python
with log.executing_context("prefix"):
    log.info("message")  # => "prefixmessage"
```

### colored_console_formatter.py — Цветной вывод

| Уровень | Цвет |
|---------|------|
| DEBUG | Серый (LIGHTBLACK_EX) |
| INFO | Белый |
| WARNING | Жёлтый |
| ERROR / CRITICAL | Красный |

Используется `colorama.init()` для кросс-платформенной поддержки ANSI-кодов. Можно отключить через `ANSI_COLORS_DISABLED=True`.

### fire_tools.py — Парсинг CLI-аргументов

**Класс `CliArg`:**
```python
CliArg('--password')                               # full_form='--password', short_form='-p'
CliArg('-dp')                                       # short_form='-dp'
CliArg('--value', subcommand='encrypt')             # привязка к подкоманде
CliArg(subcommand='encrypt', index_relative_to_subcommand=0)  # позиционный аргумент
```

**Утилиты:**
- `strtobool(val)` — конвертация строки в bool ('y','yes','t','true','on','1' → True)
- `to_non_empty_str(raw_value)` — строка, ValueError если пусто
- `list_to_comma_separated_str(raw_value)` — список/tuple → "a,b,c"
- `strip_to_list(raw_value)` — "a, b, c" → ["a", "b", "c"]

### common_paths.py — Пути

| Переменная | Путь |
|-----------|------|
| `root_path` | Корень Launcher (родитель `src/`) |
| `src_path` | `root_path/src` |
| `plugins_path` | `root_path/plugins` |
| `etc_path` | `root_path/etc` |
| `etc_plugins_path` | `root_path/etc/plugins` |
| `etc_completion_path` | `root_path/etc/completion` |
| `tools_path` | `root_path/tools` |
| `tests_path` | `root_path/tests` |
| `docs_path` | `root_path/docs` |
| `pyproject_toml_path` | `root_path/pyproject.toml` |
| `plugin_paths` | `[plugins_path, etc_plugins_path]` |

**Утилиты:**
- `get_do_file_path()` — `do.sh` / `do.bat` в зависимости от ОС
- `get_7z_path()` — путь к `7zzs` / `7z.exe`
- `get_bsdtar_path()` — путь к `bsdtar`
- `create_tmp_path()` — контекстный менеджер для временных каталогов (авто-удаление)

### process.py — Работа с процессами

**Функция `try_execute()`:**
```python
try_execute(cmd_line, cwd='.', log_stdout=True, log_cmd_line=False,
            silent=False, encoding=None, shell=True,
            filter=None, attempt_count=1, env=None) -> int
```

- Запускает subprocess с `Popen`, stdin закрывается сразу (предотвращает зависание)
- Потоковое чтение stdout с поддержкой фильтров
- Повторные попытки через `attempt_count`
- Поддержка кодировок: `dos_cp` (OEM), `windows_cp` (ANSI), `utf8_cp`

**Класс `Event`** — паттерн подписки (список callable, вызов всех при `__call__`).

**Фильтры stdout:**
- `save_stdout_message(message, last_message)` — сохраняет в список
- `ignore_not_error_message(message)` — пропускает не-ошибки
- `filter_stdout(filter)` — контекстный менеджер для временной подписки

### errors.py — Коды возврата

Глобальный `_exit_code: int = 0`:
- `set_exit_code(code)` — установить (нельзя обнулить)
- `get_exit_code()` — получить
- `errors_exists()` — True если != 0

### help_levels.py — Уровни видимости в справке

```python
class HelpLevels(IntEnum):
    hide = 1       # не показывать
    internal = 2   # только для разработчиков
    public = 3     # показывать всем (default)
```

**Декораторы:**
```python
@Help.internal   # скрыть от пользователей
@Help.hide       # полностью скрыть
@Help.show_only_linux    # только на Linux
@Help.show_only_windows  # только на Windows
```

Уровень фильтрации управляется переменной окружения `LAUNCHER_HELP_LVL`.

### help_extension.py — Переопределение справки Fire

Полностью переопределяет `fire.helptext.HelpText` и `fire.helptext.UsageText`.

Формат вывода:
```
<module> commands:
    <name>                                  <description>

Arguments:
    --<param>                               <description> (по умолчанию: <default>)
```

Описания парсятся из Google-style docstrings (Args:, Kwargs:, Returns:, Raises:).

### runtime_methods.py — Платформозависимый код

**`RuntimeIdentifiersBase`:**
- `WINDOWS = "win7-x64"`
- `LINUX = "linux-x64"`

**Декораторы:**
- `@windows_only` — выбрасывает `NotImplementedError` на Linux
- `@not_for_windows` — выбрасывает `NotImplementedError` на Windows

### io_tools.py — Файловые операции

- `try_create_or_clean_dir(dir_path)` — очистка с retry и timeout (5 сек по умолчанию)
- `create_archive(archive_file_name, source_path)` — ZIP-архивация
- `extract_archive(archive_file_name, target_path)` — распаковка ZIP
- `read_text(file_path)` / `write_text(file_path, data)` — UTF-8 ввод/вывод
- `create_directory_link(src, dst)` — symlink (Linux) или junction point (Windows через `mklink /J`)
- `write_generated_script(script_path, content)` — запись + chmod 775
- `change_permission(path, permission=0o644)` — установка прав

### string_tools.py — Работа со строками

- `randstring(length=8)` — случайная строка (a-z, 0-9)
- `shorthash(value, trim_after=8)` — SHA1 хэш, обрезанный до N символов
- `camel_to_snake(s)` — CamelCase → snake_case
- `escape_str_for_shell(s, is_in_quotes)` — экранирование для shell (различает Linux/Windows)
- `byte_to_humanreadable_format(num)` — байты → "1.5 GiB"
- `str_to_list(param_string, delimiters)` — строка → список (lowercase)
- `double_quoted_value_if_is_numeric(value)` — обёртка числовых строк в кавычки (workaround для Fire, который конвертирует "01" в int 1)

### hash_tools.py — Хэширование

- `file_hash(path)` — SHA1 с поддержкой ZIP и TAR архивов (хэш содержимого, не обёртки)
- `directory_files_hash(path)` — SHA1 всех файлов директории (многопоточный через `ThreadPoolExecutor`)

### package_file.py — Работа с пакетами

Абстрактный класс `PackageFile` с методами: `open`, `close`, `add`, `extract_all`, `read`, `check`.

Реализации:
- `ZipPackageFile` — ZIP через `zipfile.ZipFile`, извлечение через 7z (для скорости)
- `TarPackageFile` — TAR через `tarfile`, поддержка GNU-формата

Фабрика: `get_package_class_for_file(file_name)` — определяет тип по содержимому.

### update_entrypoint.py — Управление entrypoints

- `assign_path(alias)` — создаёт symlink `do.sh` → `/usr/bin/<alias>` (Linux, через `sudo ln -s`) или env variable + symlink (Windows)
- `unassign_path(alias)` — удаляет entrypoint
- `get_assigned_aliases_linux()` — поиск всех symlinks на `do.sh` в `/usr/bin`

### duration.py — Замер длительности

Декоратор `@log_duration(s_msg="...")` — логирует время выполнения функции через `time.perf_counter`.

### date_tools.py — Даты

`str_to_ru_date(date)` — конвертация `mm/dd/yyyy` → `dd.mm.yyyy`.

### validators.py — Валидация словарей

`dict_validate(data, template_dict, required_keys)` — валидация данных по шаблону, удаление пустых ключей.

### host_tools.py — Сетевые утилиты

- `get_ip()` — IP текущей машины (через UDP socket trick)
- `get_fqdn()` — Fully Qualified Domain Name
- `get_machine_name()` — имя хоста
- `get_ip_by_host_name(host_name)` — DNS resolve

---

## Plugin System

### Архитектура

Плагины — Python-пакеты в `plugins/` или `etc/plugins/` (для persistent-установок).

Каждый плагин:
1. Содержит `__init__.py` с функцией `plugin_metadata() -> PluginMetadata`
2. `is_root=True` — actions регистрируются на верхнем уровне CLI
3. `name="tools"` — actions группируются под namespace `tools` (вызов: `do tools <action>`)
4. Импортирует свои модули, каждый из которых может содержать `@action`-декорированные классы/функции

### Lifecycle плагина

1. `load_plugins()` определяет пути: `[plugins_path, etc_plugins_path]`
2. Плагины сортируются (по умолчанию `iterdir()`, components переопределяет на топологическую сортировку)
3. Для каждого пакета:
   - `importlib.import_module(name)` → `__init__.py` → импорт модулей → `@action` регистрация
   - Если плагин содержит `init_plugin()` — вызывается при импорте
4. Результат: все `@action` зарегистрированы в `_actions` dict

### Приоритет загрузки

Плагин `components` переопределяет `set_sorted_plugins_path()` на `sorted_plugins_paths.get_sorted_plugin_paths`, который учитывает зависимости между компонентами (через `mixology` — SAT-solver для разрешения зависимостей).

### Extension Points — как создать свой плагин

1. Создать каталог: `etc/plugins/my_plugin/`
2. Создать `__init__.py`:
```python
from py_common.plugins import PluginMetadata

def plugin_metadata() -> PluginMetadata:
    return PluginMetadata(name="myplugin")  # или is_root=True

from . import my_commands
```
3. Создать `my_commands.py`:
```python
from py_common.actions import action

@action
class MyCommand:
    """Описание команды"""
    def do_something(self, param: str = "default") -> None:
        """Описание метода.
        Args:
            param: Параметр
        """
        print(f"Hello, {param}!")
```
4. Установить: `./do.sh install-plugin /path/to/my_plugin`
5. Вызвать: `./do.sh myplugin my-command do-something --param=world`

Для расширения существующих сервисов:
- `service_finder.add_service_class(ServiceClassInfo(...))` — регистрация нового сервиса
- `replace_action(old_component, new_component)` — замена существующего action
- `add_after(func)` — добавление post-up хука (вызывается после `all up`)
- `add_before_fire_actions(func)` — пре-обработка аргументов CLI

---

## Все 13 плагинов — Reference

### Обзорная таблица

| Плагин | `is_root` | `name` | Назначение |
|--------|-----------|--------|-----------|
| sungero_deploy | True | — | Управление сервисами, Docker, конфигурация |
| platform_plugin | True | — | 24 платформенных сервиса, DeploymentTool |
| common_plugin | True | — | Общие утилиты (docker, yaml, git, IIS) |
| components | True | — | Управление компонентами, зависимости |
| sungero_publish | True | — | Публикация пакетов, версионирование |
| sungero_update | True | — | Обновление платформы |
| base_plugin | True | — | Базовые утилиты, RxCmd |
| ds_plugin | True | — | DDS IDE интеграция |
| test_runner | True | — | Качество кода: pylint, mypy, InspectCode |
| ui_installer | — | `installer` | Веб-UI установки (Flask + React) |
| tools_plugin | — | `tools` | Инструменты (base64) |
| ansible | True | — | Ansible-оркестрация |
| kubernetes | True | — | K8s деплой |

---

### sungero_deploy (детально)

Основной плагин управления сервисами. `is_root=True` — все actions на верхнем уровне.

#### Модули и классы

| Модуль | Класс/@action | Alias | Описание |
|--------|--------------|-------|----------|
| all.py | `All` | — | Пакетное управление всеми сервисами |
| sungerorabbitmq.py | `SungeroRabbitMQ` | `rabbitmq` | RabbitMQ контейнер |
| sungeropostgres.py | `SungeroPostgres` | `pg` | PostgreSQL контейнер (только тест!) |
| sungerohaproxy.py | `SungeroHaproxy` | `haproxy` | HAProxy reverse proxy |
| sungerodockerlogger.py | `SungeroDockerLogger` | — | Docker logging |
| elasticsearch_service.py | `SungeroElasticSearch` | `es` | Elasticsearch |
| kibana_service.py | `SungeroKibana` | `kibana` | Kibana UI |
| sungerominio.py | `SungeroMinIO` | `minio` | MinIO S3 |
| pgbouncer_service.py | `SungeroPgBouncer` | — | PgBouncer connection pooler |
| sungeromongodb.py | `SungeroMongodb` | — | MongoDB |
| encrypted_config.py | `Encryptor` | `enc` | Шифрование/расшифровка конфига |
| iis.py | — | — | IIS конфигурация (Windows) |
| servicerunner.py | `ServiceRunner` | — | ServiceRunner (Windows) |
| docker_mirror.py | — | — | Зеркалирование Docker-образов |
| builds.py | — | — | Управление билдами |
| schema.py | — | — | JSON-схемы конфигурации |
| logs_cleaner.py | — | — | Очистка логов |
| version_file.py | — | — | Файл версий |
| sysctl.py | — | — | Настройка sysctl (Linux) |
| tools/sungerodb.py | `SungeroDB` | `db` | Управление БД |
| tools/db_converter.py | `DBConverter` | — | Миграция схемы БД |
| certificates/ | `GenerateSelfSigned`, `Convert`, `DataProtection` | — | Сертификаты |

#### Иерархия классов сервисов

```
ServiceContract (ABC)
  └── ServiceBase
        ├── SungeroRabbitMQ
        ├── SungeroPostgres
        ├── SungeroHaproxy
        ├── SungeroElasticSearch
        ├── SungeroKibana
        ├── SungeroMinIO
        ├── SungeroPgBouncer
        ├── SungeroMongodb
        ├── SungeroDockerLogger
        └── PlatformService (platform_plugin)
              ├── SungeroWebServer
              ├── SungeroWebClient
              ├── StorageService
              ├── ...все 24 сервиса платформы
              └── DeploymentTool
```

#### ServiceContract — публичный контракт

```python
class ServiceContract(ABC):
    up()              # развернуть сервис
    up_with_pull()    # развернуть с docker pull
    down()            # остановить и удалить
    start()           # запустить
    stop()            # остановить
    restart()         # перезапустить
    config_up()       # сгенерировать _ConfigSettings.xml
    check()           # проверить работоспособность
    check_k8s()       # проверить в Kubernetes
    get_host_values_mapping() -> List[HostValueMap]
```

#### ServiceBase — базовая реализация

Основной класс (~500 строк). Ключевые возможности:
- Чтение конфига через `scripts_config.Config`
- Docker-контейнеры: создание, запуск, остановка через `docker_tools`
- Генерация `_ConfigSettings.xml` из `config.yml`
- Health checks через `ServiceChecker`
- Rootless-режим: создание каталогов с правами 0o777
- Memory limit calculator для Docker
- Хуки `add_after(func)` / `execute_after_up(config)` — пост-обработка

#### All — пакетный менеджер

```python
All(config).up(exclude="")      # Развернуть все сервисы
All(config).down(exclude="")    # Остановить все
All(config).start()             # Запустить все
All(config).stop()              # Остановить все
All(config).restart()           # Перезапустить
All(config).check()             # Проверить все
All(config).config_up()         # Сгенерировать конфиги для всех
```

Порядок `up`:
1. `down(exclude)` — останавливает всё (кроме исключений)
2. `create_dirs_for_rootless(config)` — создаёт каталоги (если rootless)
3. Итерация по `all_services_from_config` в порядке `services_config` из YAML
4. `service(config).up()` для каждого
5. `execute_after_up(config)` — вызов хуков

#### service_finder.py — Поиск сервисов

Реестр сервисных классов с позициями:
- `ServiceClassPositions.First` — в начало списка (например, ServiceRunner)
- `ServiceClassPositions.Last` — в конец (например, HAProxy)
- `ServiceClassPositions.SkipWithWarning` — пропустить с предупреждением
- `ServiceClassPositions.Ignore` — игнорировать

`ServiceBaseTypes.Service` / `ServiceBaseTypes.Tool` — два базовых типа.

#### scripts_config.py — Работа с конфигом

**Предопределённые переменные (`PredefinedVariables`):**

| Переменная | Описание |
|-----------|----------|
| `instance_name` | Имя инстанса (multi-instance поддержка) |
| `rid` | Runtime identifier (linux/win) |
| `host_fqdn` | FQDN хоста |
| `host_ip` | IP хоста (auto) |
| `home_path` | Путь хранения данных сервисов |
| `volume_dir` | Readonly volume для контейнеров |
| `volume_dir_rw` | Read-write volume |
| `is_rootless` | Rootless-режим контейнеров |
| `config_password` | Пароль шифрования конфига |
| `config_backup_path` | Путь для бэкапа конфига |
| `image.name` / `image.version` | Базовый Docker-образ |
| `protocol` | http/https |
| `http_port` / `https_port` | Порты |

#### generate_haproxy_config.py — Генерация конфига HAProxy

Программная генерация `haproxy.cfg` через библиотеку `pyhaproxy`:
- Дефолтный конфиг с resolver `docker_resolver` (127.0.0.11:53)
- SSL/TLS поддержка: TLS 1.2+, redirect http→https, X-Forwarded-Proto
- Frontend/Backend/Userlist управление
- Stats page с аутентификацией

---

### platform_plugin (детально)

24 платформенных сервиса Directum RX. Каждый — наследник `PlatformService(ServiceBase)`.

#### Модули сервисов (`services/`)

| Модуль | Класс | Описание |
|--------|-------|----------|
| sungero_web_server.py | SungeroWebServer | Основной ASP.NET Core сервер |
| sungero_web_client.py | SungeroWebClient | SPA фронтенд |
| sungero_public_api.py | SungeroPublicApi | OData REST API |
| storage_service.py | StorageService | Файловое/S3 хранилище |
| integration_service.py | IntegrationService | Внешние интеграции |
| sungero_worker.py | SungeroWorker | Фоновые процессы |
| job_scheduler.py | JobScheduler | Cron-задачи |
| workflow_block_service.py | WorkflowBlockService | Выполнение блоков workflow |
| workflow_process_service.py | WorkflowProcessService | Жизненный цикл процессов |
| delayed_operations_service.py | DelayedOperationsService | Отложенные операции |
| report_service.py | ReportService | Генерация отчётов |
| indexing_service.py | IndexingService | Индексация в Elasticsearch |
| preview_service.py | PreviewService | Превью документов |
| preview_storage.py | PreviewStorage | Хранение превью |
| log_service.py | LogService | Централизованное логирование |
| generic_service.py | GenericService | Мультирольный сервис |
| sungero_widgets.py | SungeroWidgets | Orleans-виджеты |
| sungero_centrifugo.py | SungeroCentrifugo | WebSocket push |
| client_connection_service.py | ClientsConnectionService | Пул подключений |
| key_derivation_service.py | KeyDerivationService | Криптоключи |
| platform_crypto_service.py | PlatformCryptoService | Криптография |
| s3tool.py | S3Tool | Миграция S3 |

#### Дополнительные модули

| Модуль | Описание |
|--------|----------|
| deployment_tool.py | `DeploymentTool` (alias `dt`) — оркестрация деплоя пакетов |
| sungero_tenants.py | Multi-tenancy управление |
| initialIndexing.py | Инициализация индексов Elasticsearch |
| dockerfile_generator.py | Генерация Dockerfile для сервисов |
| package_info_generator.py | Генерация PackageInfo.json (Windows) |
| haproxy_common.py | Общие утилиты HAProxy |
| default_settings.py | Дефолтные настройки сервисов |
| check_incompatibility.py | Проверка несовместимостей версий |
| k8s_settings.py | Настройки для Kubernetes |

#### DeploymentTool (alias="dt")

```python
@action(alias="dt")
class DeploymentTool(PlatformService, PlatformToolBase):
```

Основной инструмент деплоя пакетов разработки. Множественное наследование от `PlatformService` (контейнер) и `PlatformToolBase` (утилита).

---

### common_plugin (детально)

Общие утилиты. `is_root=True`, без namespace — модули не являются CLI-действиями, а предоставляют API для других плагинов.

| Модуль | Описание |
|--------|----------|
| docker_tools.py | Docker SDK обёртка: pull, tag, build, run, stop, remove; управление контейнерами, сетями, образами |
| yaml_tools.py | Jinja2-шаблонизация YAML, кэширование, ruamel.yaml парсинг |
| git_tools.py | Git-операции: archive, branch, commit |
| git_head_branch_parser.py | Парсинг HEAD и веток |
| json_tools.py | JSON утилиты |
| dotnet_tools.py | .NET SDK утилиты |
| dotnet_version.py | Определение версии .NET |
| docker_version.py | LooseVersion для сравнения версий Docker |
| openssl_tools.py | OpenSSL обёртка |
| iis_tools.py | IIS управление (Windows) |
| iis_config_editor.py | Редактор конфигов IIS |
| iis_site_configurator.py | Конфигуратор IIS сайтов |
| yaml_merger.py | Слияние YAML-конфигов |
| yaml_editor.py | Редактирование YAML |
| yaml_value_converters.py | Конвертеры значений YAML |
| process_tools.py | Расширенные утилиты процессов |
| locale_tools.py | Локализация |
| brand_config.py | Конфигурация бренда |
| brand_consts.py | Константы бренда |
| host_mappings.py | Маппинг хостов для Docker |
| runtime_util.py | Runtime-утилиты |
| spinner.py | Анимированный спиннер для CLI |
| class_tools.py | Утилиты для работы с классами |
| shortcut.py | Ярлыки |
| netsh.py | Netsh (Windows) |
| ntrights.py | Ntrights (Windows) |
| mage_tools.py | Mage-утилиты |
| deprecated.py | Декоратор `@deprecated` |
| deprecated_modules.py | Устаревшие модули |

#### docker_tools.py — Ключевые функции

```python
class docker_client(ContextDecorator):  # DockerClient из docker SDK, timeout=300
pull_image(image_name, all_tags, skip_name_check, silent)
tag_image(source, target, remove_old_tag)
# + build, run, stop, remove, inspect, logs, exec, networks, volumes
```

#### yaml_tools.py — Ключевые функции

```python
yaml_dump_to_str(obj)           # YAML → строка
yaml_dump_to_file(obj, path)    # YAML → файл
# Кэширование: yml_cache, dict_cache (по хэшу источника)
# Jinja2-шаблонизация через Template + StrictUndefined
```

---

### components (детально)

Управление компонентами (плагинами-расширениями платформы).

| Модуль | Описание |
|--------|----------|
| component_manager.py | `Components` (@action) — основной менеджер: install, uninstall, list, update |
| base_component.py | `BaseComponent` — базовый класс компонента |
| component_searcher.py | Поиск компонентов в каталогах и Nexus |
| component_sorter.py | Топологическая сортировка по зависимостям |
| component_version.py | Управление версиями |
| component_paths.py | Пути компонентов |
| component_search_rules.py | Правила поиска (конвенции) |
| component_rename.py | Переименование компонентов |
| mixology_dependency_resolver.py | SAT-solver для разрешения зависимостей (библиотека `mixology`) |
| sorted_plugins_paths.py | Топологическая сортировка плагинов |
| components_finder.py | Поиск компонентов в файловой системе |
| nexus.py | Интеграция с Nexus (загрузка/публикация) |
| pretty_table_tools.py | Табличный вывод (библиотека `tabulate`) |
| base_operation.py | Базовая операция |
| ui_models.py | UI-модели |

При инициализации (`__init__.py`):
```python
plugins.set_sorted_plugins_path(sorted_plugins_paths.get_sorted_plugin_paths)
```
— переопределяет порядок загрузки плагинов с учётом зависимостей.

---

### sungero_publish (детально)

Публикация и версионирование пакетов.

| Модуль | Описание |
|--------|----------|
| self_publish.py | Самопубликация Launcher (git archive → tar.gz) |
| publish_tools.py | Утилиты публикации (RID нормализация) |
| version_finder.py | Определение и сравнение версий |
| component_manifest.py | Манифест компонента (JSON, runtime, зависимости) |
| tfs_tools.py | TFS интеграция |
| mm_tools.py | Mattermost интеграция |
| config_types.py | Типы конфигурации (RuntimeIdentifiers, EvaluatedVersion) |
| builds_path_resolver.py | Разрешение путей к билдам |
| self_publish_path_resolver.py | Разрешение путей для самопубликации |
| local_builds_path.py | Локальные билды |
| abstract_package_cache.py | Абстрактный кэш пакетов |
| locked_package_cache.py | Кэш с блокировками |

---

### Остальные плагины (кратко)

#### sungero_update
Обновление платформы. Единственный модуль `update.py` — проверяет архив (is_system=true), runtime, извлекает и применяет обновление.

#### base_plugin
Базовые утилиты: `rxcmd.py` (RxCmd CLI), `rx_component.py` (компонент RX), `plugin_tool_base.py` (базовый класс инструмента).

#### ds_plugin
DDS IDE интеграция. Alias `ds`. Установка, запуск, конфигурация Development Studio (Desktop и Web).

#### test_runner
Качество кода:
- `test_run.py` — `check-all` / `test-run`: запуск unittest, pylint, mypy, InspectCode
- `pylint_tool.py` — PyLint интеграция
- `mypy_tool.py` — MyPy интеграция
- `inspectcode_tool.py` — JetBrains InspectCode для C#

#### ui_installer
Веб-UI установки. Namespace `installer`:
- `main.py` — Flask-приложение
- `app.py` — React SPA
- `publish.py` — публикация
- `generate_config.py` — генерация конфига
- `localization.py` — i18n

#### tools_plugin
Namespace `tools`:
- `base64_tools.py` — Base64 кодирование/декодирование

#### ansible
Ansible-оркестрация. Roles: `dl_role`, `component_role`. Playbooks.

#### kubernetes
Kubernetes деплой. Helm charts, `KubeTool`.

---

## Конфигурация

### Структура config.yml

```yaml
variables:          # Глобальные переменные (подстановка через Jinja2)
builds:             # Пути к билдам инструментов
components:         # Каталоги с компонентами
extra_hosts:        # Дополнительные записи /etc/hosts
logs_path: &logs    # YAML-якорь для путей логов
common_config: &base  # YAML-якорь для общих настроек
services_config:    # Настройки каждого сервиса
```

### Верхнеуровневые переменные (`variables`)

| Переменная | Тип | Default | Описание |
|-----------|-----|---------|----------|
| `instance_name` | str | auto | Имя инстанса (multi-instance) |
| `rid` | str | auto | Runtime ID (linux/win) |
| `host_fqdn` | str | DNS name | FQDN сервера |
| `home_path` | str | — | Путь хранения данных |
| `config_backup_path` | str | — | Путь бэкапа конфига |
| `volume_dir` | list | — | Readonly Docker-volumes |
| `volume_dir_rw` | list | — | Read-write Docker-volumes |
| `http_port` | int | 80 | HTTP порт |
| `https_port` | int | 443 | HTTPS порт |
| `protocol` | str | https | Протокол (http/https) |
| `config_password` | str | — | Пароль шифрования |
| `use_system_docker_log_settings` | bool | false | Использовать системные настройки логов Docker |
| `use_host_fqdn_for_check` | bool | false | Использовать FQDN для health checks |
| `is_rootless` | bool | — | Rootless Docker |
| `image.name` | str | registry.directum.ru/directum/rx-base | Базовый Docker-образ |
| `image.version` | str | — | Версия образа |

### builds — Пути к билдам

| Билд | Описание |
|------|----------|
| `dbconverter_builds` | DBConverter — миграция БД |
| `encryptor_builds` | Encryptor — шифрование конфигов |
| `certificate_tool_builds` | CertificateTool — сертификаты |
| `scripts_builds` | SungeroScripts — скрипты |
| `redist_builds` | Redistributed packages |
| `ansible_builds` | Ansible |
| `servicerunner_builds` | ServiceRunner (Windows) |
| `inspectcode_builds` | InspectCode (C#) |
| `pylint_builds` | PyLint |
| `mypy_builds` | MyPy |

Предопределённая переменная `{{ local_builds_path }}` = `./etc/_builds/`.

### services_config — Все сервисы

#### Инфраструктурные (Docker-контейнеры)

| Сервис | Порт(ы) | Docker-образ | Ключевые настройки |
|--------|---------|-------------|-------------------|
| SungeroRabbitMQ | 5672, 15672, 15692 | rabbitmq:4.0-management-alpine | rabbitmq_data_path, management_panel_disabled |
| SungeroHaproxy | http_port, https_port | haproxy:3.3-alpine | haproxy_config, ssl_cert, stats_user/password/port |
| SungeroPostgres | auto | postgres:15.3-alpine | postgres_data_path, password (тест only!) |
| SungeroElasticSearch | 9200, 9300 | elasticsearch:7.17.23 | es_data_path, synonyms_file_path, cluster settings |
| SungeroKibana | 5601 | kibana:7.17.23 | kibana_data_path, ELASTICSEARCH_HOSTS |
| SungeroMinIO | 9000, 9001 | minio:2024.8.3 | minio_data_path, ROOT_USER/PASSWORD, DEFAULT_BUCKETS |
| SungeroPgBouncer | 6432 | pgbouncer:1.23.1 | pool_mode, max_client_conn, default_pool_size |

#### Платформенные сервисы (наследуют common_config)

| Сервис | Порт | Описание |
|--------|------|----------|
| SungeroWebServer | 44310 | Веб-сервер, health check: `/health` |
| SungeroWebClient | 44320 | Веб-клиент, health check: `/health` |
| StorageService | 44330 | Хранилище файлов (File/S3) |
| IntegrationService | 44340 | Внешние интеграции |
| SungeroWorker | — | Фоновые процессы |
| JobScheduler | — | Планировщик задач |
| DeploymentTool | — | Деплой пакетов |

#### Windows-only

| Сервис | Описание |
|--------|----------|
| IIS | site_name, http_port, https_port, ssl_cert_thumbprint, arr_timeout |
| ServiceRunner | CONFIGS_PATH, PACKAGES_ZIP_PATH, SERVICE_RUNNER_PORT |

#### Утилиты

| Сервис | Описание |
|--------|----------|
| RxCmd | INTEGRATION_SERVICE_URL (auto) |
| NomadService | HOST_HTTP_PORT, CLIENT_LOGS_PATH, USERS_PATH, PLUGINS_PATH |

### common_config (&base) — Общие переменные сервисов

| Переменная | Описание |
|-----------|----------|
| `DATABASE_ENGINE` | Тип СУБД: `postgres` / `mssql` |
| `DEFAULT_DATABASE` | БД по умолчанию (для PostgreSQL, default: template1) |
| `CONNECTION_STRING` | Строка подключения ADO.NET |
| `QUEUE_CONNECTION_STRING` | RabbitMQ connection string |
| `CHECKS_ATTEMPT_COUNT` | Количество попыток health check (default: 250) |
| `LOGS_PATH` | Путь к логам (`{{ home_path }}/logs`) |
| `STATUS_FILE_PATH` | Путь к файлам статуса |
| `ELASTICSEARCH_URL` | URL Elasticsearch |
| `LANGUAGE` / `LOG_LANGUAGE` | Языковые настройки |
| `MIN_LOG_LEVEL` | Минимальный уровень лога |
| `LOG_TO` | Куда логировать (`file`) |
| `AUTHENTICATION_USERNAME` / `PASSWORD` | Сервисный пользователь |
| `ENABLE_SCALING` | Масштабирование |
| `DATA_PROTECTION_CERTIFICATE_FILE` | Сертификат шифрования |
| `UTC_OFFSET` | Смещение UTC |
| `HYPERLINK_SERVER` | URL для гиперссылок |
| `INTEGRATION_SERVICE_URL` | URL интеграционного сервиса |

### Jinja2 Templating

Конфиг обрабатывается Jinja2 перед парсингом YAML.

**Встроенные переменные:**
- `{{ host_ip }}` — IP текущей машины (auto-detect)
- `{{ host_fqdn }}` — FQDN машины
- `{{ home_path }}` — путь к данным
- `{{ http_port }}` / `{{ https_port }}` — порты
- `{{ protocol }}` — протокол
- `{{ local_builds_path }}` — путь к `./etc/_builds/`

**Функции:**
- `{{ getenv("LAUNCHER_CONFIG_PASSWORD") }}` — переменная окружения

**Примеры использования:**
```yaml
LOGS_PATH: '{{ home_path }}/logs'
haproxy_config: '{{ home_path }}/haproxy/haproxy.cfg'
SERVER_PUBLICBASEURL: 'http://{{ host_fqdn }}:5601'
config_password: '{{ getenv("LAUNCHER_CONFIG_PASSWORD") }}'
```

### Шифрование конфига

```bash
# Зашифровать значение
./do.sh enc encrypt --value="secret"

# Расшифровать значение
./do.sh enc decrypt --value="encrypted_string"

# Шифрование всего конфига (AvailableForEncrypt поля)
./do.sh enc encrypt-config
```

Используется `EncryptorTool` (`etc/_builds/Encryptor`) с паролем из `variables.config_password`.

---

## Сервисный Lifecycle

### all up — полная последовательность

```
1. All(config).__init__()
   ├── ServiceBase(config) — загрузка config.yml
   ├── get_all_services_from_config() — сервисы из services_config (в порядке YAML)
   └── get_all_services() — все зарегистрированные сервисы

2. All.up(exclude="")
   ├── down(exclude) — остановка всех (кроме исключений)
   │   └── service(config).down() для каждого
   ├── create_dirs_for_rootless(config) — каталоги с правами 0o777
   ├── for service in all_services_from_config:
   │   └── service(config).up()
   │       ├── create_dockerfile()    — генерация Dockerfile
   │       ├── docker build          — сборка образа
   │       ├── docker run            — запуск контейнера
   │       │   ├── volumes (home_path, logs, configs)
   │       │   ├── environment vars из _ConfigSettings
   │       │   ├── network: docker bridge
   │       │   ├── extra_hosts из конфига
   │       │   └── ulimits: nofile=1048576
   │       └── check()               — health check
   └── execute_after_up(config) — пост-хуки
       └── clear_evaluated_relative_paths()
```

### Docker container management

Каждый сервис наследующий `ServiceBase`:
- **up()**: `create_dockerfile()` → `docker build` → `docker run` с volumes, env, ports
- **down()**: `docker stop` + `docker rm`
- **start()**: `docker start`
- **stop()**: `docker stop`
- **restart()**: `docker restart`
- **check()**: HTTP health check через `ServiceChecker`

### HAProxy config generation

При `all up` с `platform_plugin`:
1. Загружается базовый конфиг HAProxy (defaults, resolvers)
2. Для каждого сервиса добавляется frontend/backend
3. SSL: TLS 1.2+, redirect http→https, сертификат из `ssl_cert`
4. Stats page на `stats_port` с аутентификацией
5. Генерируется `haproxy.cfg` в `{{ home_path }}/haproxy/`

---

## Все команды CLI

### Управление сервисами

| Команда | Описание |
|---------|----------|
| `do all up [--exclude=...]` | Развернуть все сервисы |
| `do all down [--exclude=...]` | Остановить и удалить все |
| `do all start` | Запустить все |
| `do all stop` | Остановить все |
| `do all restart` | Перезапустить все |
| `do all check` | Проверить работоспособность |
| `do all config-up` | Сгенерировать конфиги |
| `do <service> up` | Развернуть сервис |
| `do <service> down` | Остановить сервис |
| `do <service> start` | Запустить сервис |
| `do <service> stop` | Остановить сервис |
| `do <service> restart` | Перезапустить сервис |
| `do <service> check` | Проверить сервис |

Где `<service>`: `rabbitmq`, `pg`, `haproxy`, `es`, `kibana`, `minio`, и все платформенные сервисы.

### База данных

| Команда | Описание |
|---------|----------|
| `do db migrate` | Миграция БД |
| `do db converter up/down` | Управление DBConverter |

### Деплой (platform_plugin)

| Команда | Описание |
|---------|----------|
| `do dt deploy` | Деплой пакетов |
| `do dt apply` | Применить пакеты |

### Шифрование

| Команда | Описание |
|---------|----------|
| `do enc encrypt --value=...` | Зашифровать значение |
| `do enc decrypt --value=...` | Расшифровать значение |

### Сертификаты

| Команда | Описание |
|---------|----------|
| `do generate-self-signed certificate` | Генерация self-signed сертификата |
| `do convert ...` | Конвертация сертификатов |
| `do data-protection ...` | Data protection сертификат |

### Компоненты

| Команда | Описание |
|---------|----------|
| `do components install --path=...` | Установить компонент |
| `do components delete --name=...` | Удалить компонент |
| `do components delete-all` | Удалить все компоненты |
| `do components list` | Список компонентов |

### Обновление

| Команда | Описание |
|---------|----------|
| `do create-update-script --version-path=...` | Создать скрипт обновления |

### Тестирование

| Команда | Описание |
|---------|----------|
| `do check-all` | Все проверки: unit-тесты + pylint + mypy + InspectCode |
| `do test-run` | Запуск unit-тестов |

### Плагины

| Команда | Описание |
|---------|----------|
| `do install-plugin --path=...` | Установить плагин |
| `do uninstall-plugin --name=...` | Удалить плагин |
| `do assign-path --alias=...` | Создать entrypoint |
| `do unassign-path --alias=...` | Удалить entrypoint |

### UI Installer

| Команда | Описание |
|---------|----------|
| `do installer ...` | Веб-интерфейс установки |

### Инструменты

| Команда | Описание |
|---------|----------|
| `do tools to-base64 --value=...` | Base64 кодирование |
| `do tools from-base64 --value=...` | Base64 декодирование |

### Системные

| Команда | Описание |
|---------|----------|
| `do sysctl ...` | Настройка sysctl (Linux) |
| `do logs-cleaner ...` | Очистка логов |
| `do version-file ...` | Управление файлом версий |
| `do schema ...` | JSON-схемы конфигурации |

---

## Dependencies (Poetry)

Из `pyproject.toml` — 21 пакет:

| Пакет | Версия | Назначение |
|-------|--------|-----------|
| fire | 0.7.1 | CLI-фреймворк Google |
| docker | 7.1.0 | Docker SDK для Python |
| Jinja2 | 3.1.6 | Шаблонизация конфигов |
| ruamel.yaml | 0.17.21 | YAML парсинг с комментариями |
| colorama | 0.4.6 | Цветной вывод в консоль |
| dacite | 1.9.2 | Dataclass из dict |
| filelock | 3.20.1 | Файловые блокировки |
| mixology | 0.2.0 | SAT-solver для зависимостей |
| pika | 1.3.2 | RabbitMQ AMQP клиент |
| pymongo | 4.15.5 | MongoDB клиент |
| tabulate | 0.9.0 | Табличный вывод в CLI |
| xmltodict | 1.0.2 | XML ↔ dict конвертация |
| dockerfile-parse | 2.0.1 | Парсинг Dockerfile |
| pyhaproxy | 0.3.7 | Парсинг/генерация конфигов HAProxy |
| genson | 1.3.0 | Генерация JSON Schema |
| yamlpath | 3.8.2 | YAML path queries |
| pefile | 2024.8.26 | Парсинг PE-файлов (Windows) |
| requests-toolbelt | 1.0.0 | Утилиты для requests |
| psutil | 7.2.0 | Системные метрики (CPU, RAM) |
| packaging | 25.0 | Сравнение версий |
| distro | 1.9.0 | Информация о дистрибутиве Linux |
| fissix | 24.4.24 | Python source transformation |

### Инструменты качества

| Инструмент | Конфигурация |
|-----------|-------------|
| mypy | `strict = true`, `ignore_missing_imports = true`, исключения: lib/, tests/, .venv/ |

---

## Тестирование

### Структура

```
tests/           # Unit-тесты
tests/lib/       # Библиотеки для тестов
```

### Запуск

```bash
./do.sh check-all          # Все проверки
./do.sh test-run            # Только unit-тесты
```

### Quality gates (check-all)

1. **Unit-тесты** — `unittest` с паттерном `*_tests.py`
2. **PyLint** — статический анализ Python
3. **MyPy** — проверка типов (strict mode)
4. **InspectCode** — JetBrains InspectCode для C# (если доступен)
5. **Кастомные проверки** — через `test_run.add_check(func)`

Контекст обнаружения: если в `sys.argv` есть `check-all` или `test-run`, плагины из `etc/plugins` НЕ загружаются (`DL_DISABLE_LOAD_PLUGINS_FROM_ETC=1`).

---

## manifest.json

Содержимое:
```json
{
    "name": "directumlauncher",
    "version": "26.1.2264.e865482b6c",
    "runtime": "linux"
}
```

Используется для идентификации версии Launcher, проверки совместимости при обновлении и публикации.
