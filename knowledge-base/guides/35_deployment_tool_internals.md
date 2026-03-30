# DeploymentToolCore — Полная внутренняя архитектура

## Обзор

**DeploymentToolCore (DTC)** — утилита платформы Directum RX 26.1 для публикации прикладной разработки в продуктивную систему. Обеспечивает:

- Публикацию пакетов разработки (`.dat`) с инициализацией модулей и применением настроек
- Экспорт/импорт пакетов разработки и настроек бизнес-процессов
- Объединение нескольких пакетов в один
- Управление версиями решений
- Публикацию в распределённую систему (zero-downtime)
- Удаление решений

**Расположение в дистрибутиве:**
- Dockerfile: `дистрибутив/platform/DeploymentTool/Dockerfile`
- Python-обёртка Launcher: `дистрибутив/launcher/plugins/platform_plugin/deployment_tool.py`
- JSON-схема конфигурации: `дистрибутив/launcher/plugins/platform_plugin/schema/services/deployment_tool.json`
- Тесты: `дистрибутив/launcher/plugins/platform_plugin/tests/platform_deploy_tests/deployment_tool_tests.py`

**Вызов через Directum Launcher:**
- Linux: `./do.sh dt <команда>`
- Windows: `do dt <команда>`

Alias класса в Launcher: `dt` (декоратор `@action(alias="dt")`).

---

## Docker-архитектура

### Dockerfile

```dockerfile
ARG docker_tag="registry.directum.ru/directum/rx-base/sdk:26.1.0"
FROM $docker_tag
LABEL "Name"="DeploymentTool"
ARG APP_DIR=/app
ARG USER_ID=0
ARG ISOLATED_HOST=Sungero.IsolatedArea.Host
WORKDIR $APP_DIR
ENV PATH=$PATH:$APP_DIR
ENV COMPANY_NAME="Directum Company"
ENV PRODUCT_NAME="DirectumRX"
COPY DeploymentTool.tar.gz ./
RUN tar -xzf DeploymentTool.tar.gz && rm DeploymentTool.tar.gz
RUN [ -f $ISOLATED_HOST ] && chmod +x $ISOLATED_HOST || :
ARG USER_NAME
RUN \
 if [ $USER_ID = 0 ] ; then \
   echo USER_ID is not provided, root user will be used ; \
 else \
   groupadd -g $USER_ID $USER_NAME \
   && useradd -m -u $USER_ID -g $USER_ID $USER_NAME \
   && chown -R $USER_ID:$USER_ID $APP_DIR ; \
 fi
USER $USER_ID:$USER_ID
RUN [ $USER_ID -ne 0 ] && mkdir -p "Directum Company/DirectumRX" /home/$USER_NAME/.dotnet || :
ENTRYPOINT ["DeploymentToolCore"]
```

**Ключевые особенности:**
- Base image: `rx-base/sdk:26.1.0` (SDK-образ с .NET 8)
- ENTRYPOINT: `DeploymentToolCore` (бинарник .NET)
- Поддержка non-root пользователя через `USER_ID` / `USER_NAME`
- Автоматическое создание домашней директории для non-root: `"Directum Company/DirectumRX"` и `.dotnet`
- Isolated area host получает `chmod +x` при наличии

### Volumes (точки монтирования)

Volumes формируются динамически методом `get_total_volumes()` в зависимости от выполняемой операции:

| Путь в контейнере | Путь на хосте | Режим | Назначение |
|---|---|---|---|
| `/app/<filename>.dat` | Путь к пакету на хосте | `ro` | Пакет разработки для публикации. Монтируется для каждого пакета из `self.packages` |
| `/app/interface` | `<etc_path>/interface` | `rw` | Интерфейсная сборка (выходная директория при сборке интерфейса) |
| `/app/settings_export/` | Директория экспорта настроек на хосте | `rw` | Экспорт/импорт настроек бизнес-процессов |
| `/app/package_export/` | Директория экспорта пакета на хосте | `rw` | Экспорт пакета разработки и XML-конфигурации |
| `/app/repositories/` | Корневая папка репозиториев на хосте | `rw` | Репозитории исходного кода для экспорта/версионирования |
| `/app/<settings>.datx` | Путь к файлу настроек на хосте | `rw` | Импортируемый пакет настроек |

### Трансформация путей (host → container)

На Linux пути трансформируются для работы внутри контейнера:

| Операция | Хостовой путь | Путь в контейнере |
|---|---|---|
| Пакет разработки | `/home/user/packages/Dev.dat` | `/app/Dev.dat` (только basename) |
| Интерфейсная сборка | `<etc_path>/interface` | `/app/interface` |
| Экспорт настроек (файл) | `/home/user/export/settings.datx` | `/app/settings_export/settings.datx` |
| Экспорт настроек (папка) | `/home/user/export/` | `/app/settings_export/` |
| Экспорт пакета | `/home/user/export/package.dat` | `/app/package_export/package.dat` |
| Репозитории | `/home/user/repos` | `/app/repositories/` |

На Windows пути передаются as-is (без трансформации).

### Параметры контейнера

Метод `run_options()` задаёт:
- `restart_policy = None` — контейнер не перезапускается
- `stream_logs_after_run = True` — вывод логов в stdout
- `auto_remove = True` — автоматическое удаление контейнера после завершения

---

## CLI Reference (полный)

### Основные команды (через Directum Launcher)

| Команда | Описание | Пример |
|---|---|---|
| `dt deploy --package="<пути>"` | Публикация + инициализация + настройки | `./do.sh dt deploy --package="/srv/Dev.dat"` |
| `dt deploy --package="<путь>" --dev` | Только публикация разработки (без init/settings) | `./do.sh dt deploy --package="/srv/Dev.dat" --dev` |
| `dt deploy --package="<путь>" --init` | Публикация + инициализация | `./do.sh dt deploy --package="/srv/Dev.dat" --init` |
| `dt deploy --package="<путь>" --init=False` | Публикация без инициализации и настроек | `./do.sh dt deploy --package="/srv/Dev.dat" --init=False` |
| `dt deploy --package="<путь>" --settings` | Публикация + применение настроек | `./do.sh dt deploy --package="/srv/Dev.dat" --settings` |
| `dt deploy --package="<путь>" --force` | Принудительная повторная публикация | `./do.sh dt deploy --package="/srv/Dev.dat" --force` |
| `dt deploy --package="<путь>" --distributed` | Публикация в распределённую систему | `./do.sh dt deploy --package="/srv/Dev.dat" --distributed` |
| `dt deploy --ls` | Список опубликованных решений | `./do.sh dt deploy --ls` |
| `dt deploy --init` | Только инициализация | `./do.sh dt deploy --init` |
| `dt deploy --settings` | Только применение настроек | `./do.sh dt deploy --settings` |
| `dt init` | Инициализация модулей | `./do.sh dt init` |
| `dt init_and_apply_settings` | Инициализация + настройки | `./do.sh dt init_and_apply_settings` |
| `dt get_deployed_solutions` | Список опубликованных решений | `./do.sh dt get_deployed_solutions` |
| `dt get_applied_solutions_info --package_path="<путь>"` | Информация о решениях из пакета | `./do.sh dt get_applied_solutions_info --package_path="/srv/Dev.dat"` |
| `dt export-package` | Экспорт пакета разработки | см. раздел "export-package" |
| `dt merge_packages` | Объединение пакетов | см. раздел "merge_packages" |
| `dt increment_version` | Увеличить номер версии | `./do.sh dt increment_version --root /home/user --repositories Base` |
| `dt set_version` | Задать номер версии | `./do.sh dt set_version --version 0.0.0.1 --root /home/user --repositories Base` |
| `dt remove_solutions` | Удалить решения | `./do.sh dt remove_solutions --solution_names="Solution1 Solution2"` |
| `dt import_settings` | Импорт настроек | `./do.sh dt import_settings --path="/srv/settings.datx"` |
| `dt export_settings` | Экспорт настроек | `./do.sh dt export_settings --path="/srv/export/"` |
| `dt run --command="<параметры>"` | Произвольная команда DTC | `do dt run --command="-n user -p pass -x"` |

### Все CLI-флаги DeploymentToolCore

| Флаг | Длинная форма | Тип | Описание |
|---|---|---|---|
| `-d` | `--development-package` | string | Путь к пакету(ам) разработки |
| `-x` | `--initialize` | flag/string | Инициализация (опционально — имена решений через пробел) |
| `-s` | `--apply-settings` | flag | Применить настройки бизнес-процессов по умолчанию |
| `-n` | `--name` | string | Имя пользователя |
| `-p` | `--password` | string | Пароль пользователя |
| `-b` | — | string | Путь до папки интерфейсной сборки |
| `-l` | `--list-deployed-solutions` | flag | Показать список опубликованных решений |
| `-m` | `--merge-only` | string | Путь для сохранения объединённого пакета (без публикации) |
| `-r` | `--remove-solutions` | string | Имена решений для удаления (через пробел) |
| `-e` | — | string | Путь к создаваемому пакету экспорта |
| `-c` | — | string | Путь к XML-конфигурации пакета |
| `-v` | — | string | Номер версии для присвоения |
| `-y` | `--settings` | string | Путь до конфигурационного файла утилиты |
| `-f` | — | int | Идентификатор папки для экспорта настроек |
| `-z` | — | string | Имя тенанта |
| — | `--root` | string | Корневая папка репозиториев |
| — | `--repositories` | string | Относительные пути до папок репозиториев |
| — | `--work` | string | Относительные пути до папок work-репозиториев |
| — | `--distributed` | flag | Публикация в распределённую систему |
| — | `--settings-localization` | string | Путь до XLSX-файла со строками локализации |
| — | `--import-settings` | string | Путь до пакета настроек для импорта |
| — | `--export-settings` | string | Путь для сохранения экспортированных настроек |
| — | `--increment-version` | flag | Увеличить номер версии |
| — | `--parallel-tenants` | int | Количество параллельно обрабатываемых тенантов |
| — | `--force` | flag | Принудительная публикация |
| — | `--help` | flag | Справка по параметрам |

### Exit-коды

| Код | Значение | Причина |
|---|---|---|
| 0 | Успех | Публикация завершилась без ошибок |
| 1 | Ошибка до отправки | Ошибка при проверке аргументов, запросе метаданных, чтении пакета, формировании пакета развёртывания |
| 2 | Ошибка передачи/публикации | Сетевая ошибка, сервер недоступен, несовпадение версий DTC и веб-сервера (должны совпадать на уровне 26.1.0.XXXX) |
| 3 | Ошибка инициализации | Ошибка при отправке запроса или выполнении инициализации модулей на сервере |
| 4 | Ошибка настроек | Ошибка при применении настроек по умолчанию из пакета разработки |
| 5 | Ошибка импорта настроек | Ошибка при импорте пользовательских настроек |
| 6 | Ошибка экспорта настроек | Ошибка при экспорте настроек |
| 7 | Ошибка экспорта пакета | Ошибка при экспорте пакета разработки |
| 8 | Ошибка версионирования | Ошибка при изменении номера версии модулей и решений |

---

## Команды детально

### deploy

Основная команда. Поведение зависит от комбинации флагов:

**Поведение по умолчанию (только `--package`):**
Если указан только `--package` без `--dev`, `--init`, `--settings`, `--interface`, `--distributed`, то автоматически включаются инициализация (`init=True`) и настройки (`settings=True`).

```python
# Из deployment_tool.py, метод deploy():
if package and not interface and not dev and init is None and settings is None and distributed is None:
    init = "True"
    settings = "True"
```

**Защита от дублирования:**
Если в БД уже есть пакет с такой же или более новой версией (semver-сравнение), публикация отклоняется:
```
Solutions are already in database. Deployment rejected.
```
Обход: `--force`.

**Проверка несовместимости:**
Перед публикацией проверяется совместимость зависимых решений. Если обнаружена несовместимость:
```
Incompatibility check. Applied packages incompatibility. You must re-deploy all dependent solutions.
```

**Примеры:**

```bash
# Полная публикация (deploy + init + settings)
./do.sh dt deploy --package="/srv/CustomDev/Dev1.dat;/srv/CustomDev/Dev2.dat"

# Только публикация кода
./do.sh dt deploy --package="/srv/Dev.dat" --dev

# Публикация + инициализация конкретных решений
./do.sh dt deploy --package="/srv/Dev.dat" --init="DirectumRX Memo"

# Принудительная перепубликация
./do.sh dt deploy --package="/srv/Dev.dat" --force

# С другим пользователем
./do.sh dt deploy --package="/srv/Dev.dat" --user="Administrator" --password="11111"

# С параллельной обработкой тенантов
./do.sh dt deploy --package="/srv/Dev.dat" --parallel_tenants=5

# В распределённую систему
./do.sh dt deploy --package="/srv/Dev.dat" --distributed

# Показать опубликованные решения
./do.sh dt deploy --ls

# Интерфейсная сборка
./do.sh dt deploy --package="/srv/Dev.dat" --interface="/output/interface"
```

### export-package

Создаёт пакет разработки (`.dat`) из исходных кодов.

**Параметры:**

| Параметр | Обязательный | Описание |
|---|---|---|
| `--export_package` | Да | Путь к создаваемому `.dat` файлу |
| `--root` | Да | Полный путь к корневой папке репозиториев |
| `--configuration` | Нет | Путь к XML-конфигурации пакета |
| `--repositories` | Нет* | Относительные пути до папок base-репозиториев (через `;`) |
| `--work` | Нет* | Относительные пути до папок work-репозиториев (через `;`) |

*Должен быть задан хотя бы один из `--repositories` или `--work`.

**Пример:**
```bash
./do.sh dt export-package \
    --export_package /home/user/CustomDev/DevRX.dat \
    --configuration /home/user/CustomDev/DevRX.xml \
    --root /home/user \
    --repositories Base
```

**Формат XML-конфигурации пакета:**

```xml
<?xml version="1.0"?>
<DevelopmentPackageInfo
  xmlns:xsd="http://www.w3.org/2001/XMLSchema"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <!-- Решение -->
    <PackageModuleItem>
      <Id>{GUID решения}</Id>
      <Name>{КодКомпании.ИмяРешения}</Name>
      <Version>{Версия}</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>false</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <!-- Модуль -->
    <PackageModuleItem>
      <Id>{GUID решения}</Id>
      <SolutionId>{GUID модуля}</SolutionId>
      <Name>{КодКомпании.ИмяМодуля}</Name>
      <Version>{Версия}</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>false</IncludeSources>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
  </PackageModules>
</DevelopmentPackageInfo>
```

**Реальный пример XML:**
```xml
<?xml version="1.0"?>
<DevelopmentPackageInfo
  xmlns:xsd="http://www.w3.org/2001/XMLSchema"
  xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <PackageModuleItem>
      <Id>ccaf5fa7-4108-422d-bed4-1d4ea46488af</Id>
      <Name>DEV.RentSolution</Name>
      <Version>0.0.1.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>false</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <PackageModuleItem>
      <Id>9dc3d9e2-9698-4643-ad95-d72cb55a2bb8</Id>
      <SolutionId>ccaf5fa7-4108-422d-bed4-1d4ea46488af</SolutionId>
      <Name>DEV.RentModule</Name>
      <Version>0.0.1.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>false</IncludeSources>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
  </PackageModules>
</DevelopmentPackageInfo>
```

**Результат экспорта:**
- Файл пакета `*.dat`
- XML-файл с информацией о пакете (PackageInfo.xml)

### merge_packages

Объединяет несколько пакетов разработки в один без публикации. Два режима:

**Режим 1: `--packages` (явное указание пакетов)**
```bash
./do.sh dt merge_packages "/app/union.dat" --packages="/app/first.dat;/app/second.dat"
```

**Режим 2: `--package_from_component` (из компонентов)**
```bash
./do.sh dt merge_packages "/app/union.dat" --package_from_component="base;memo;agile;genai"
```

**Ограничения:**
- `output_path` должен иметь расширение `.dat`
- Нельзя использовать оба параметра одновременно (`--packages` и `--package_from_component`)
- Должен быть указан хотя бы один из них

**Зачем:** Предварительное объединение пакетов экономит время при публикации. Объединение выполняется заранее, а при плановых работах публикуется один готовый пакет.

### init / init_and_apply_settings

**`init`** — инициализация модулей:
```bash
# Инициализация всех модулей
./do.sh dt init

# Инициализация конкретного тенанта
./do.sh dt init --tenant_name="MyTenant"

# С другим пользователем
./do.sh dt init --user="Admin" --password="pass"

# Параллельная обработка тенантов
./do.sh dt init --parallel_tenants=5
```

**`init_and_apply_settings`** — инициализация + применение настроек:
```bash
./do.sh dt init_and_apply_settings
./do.sh dt init_and_apply_settings --user="Admin" --password="pass"
./do.sh dt init_and_apply_settings --parallel_tenants=10
```

Внутренне `init_and_apply_settings` вызывает `init()` с дополнительным флагом `-s`.

### export_settings / import_settings

**Экспорт настроек:**
```bash
# Экспорт всех действующих настроек
./do.sh dt export_settings --path="/srv/export/"

# Экспорт в конкретный файл
./do.sh dt export_settings --path="/srv/export/my_settings.datx"

# Экспорт настроек из конкретной папки
./do.sh dt export_settings --path="/srv/export/" --folder_id=123
```

Если имя файла не указано (только папка), генерируется имя формата: `settings-<yyyy-MM-dd_HH-mm-ss>.datx`

**Импорт настроек:**
```bash
./do.sh dt import_settings --path="/srv/settings.datx"
./do.sh dt import_settings --path="/srv/settings.datx" --user="Admin" --password="pass"
```

Формат файла настроек: `.datx`.

### get_deployed_solutions / get_applied_solutions_info

**`get_deployed_solutions`** — список опубликованных решений:
```bash
./do.sh dt get_deployed_solutions
```

Возвращает список `DeployedSolutionInfo(Name, Version)`. Два режима:
- Из вывода DTC (парсинг stdout, формат: `"22:24:29.607  - Sungero.DirectumRX, 4.6.24.0"`)
- Из БД напрямую (метод `get_deployed_solutions_from_db()`, SQL-запрос к `sungero_ide_fulldeploy`)

**Regex парсинга вывода:**
Ищется строка формата: `<время>  - <Имя.Решения>, <Версия>` (4 элемента, разделённых пробелами, второй — дефис).

**`get_applied_solutions_info`** — информация из пакета:
```bash
./do.sh dt get_applied_solutions_info --package_path="/srv/Dev.dat"
```

Возвращает `AppliedSolutionInfo(Name, Version, IncludeSources, PackagePath)`.

**`get_dat_packages_from_dir`** — поиск всех `.dat` файлов в директории:
```python
packages = DeploymentTool.get_dat_packages_from_dir("/srv/builds/")
# По умолчанию ищет в ./etc/_builds
```

### remove_solutions

```bash
./do.sh dt remove_solutions --solution_names="Solution1 Solution2"
./do.sh dt remove_solutions --solution_names="Solution1" --user="Admin" --password="pass"
```

Имена решений перечисляются через пробел.

### increment_version / set_version

**Увеличить версию:**
```bash
./do.sh dt increment_version --root /home/user --repositories Base
```

**Задать конкретную версию:**
```bash
./do.sh dt set_version --version 0.0.0.1 --root /home/user --repositories Base
```

Параметр `--repositories` принимает несколько значений через `;`.

---

## Конфигурация (config.yml)

### Секция DeploymentTool

В `services_config` файла `config.yml`:

```yaml
services_config:
    DeploymentTool:
        <<: *logs                          # Наследует LOGS_PATH
        WEB_RELATIVE_PATH: 'Client'        # Относительный путь до веб-клиента
        SERVER_HTTP_PORT: '8080'            # HTTP-порт веб-сервера
        SERVER_HTTPS_PORT: '8443'           # HTTPS-порт веб-сервера
        SERVER_ROOT: 'sungerohaproxy_directum'  # Адрес домена сервера с Directum RX
        WEB_PROTOCOL: 'http'               # Протокол: http или https
```

### Все переменные (из JSON-схемы)

| Переменная | Тип | Обязательна | Описание | Пример |
|---|---|---|---|---|
| `LOGS_PATH` | string | Да | Папка для хранения логов | `${basedir}/../../../../../log` |
| `WEB_RELATIVE_PATH` | string | Да | Относительный путь до веб-клиента | `Client` |
| `SERVER_HTTP_PORT` | string | Да | HTTP-порт веб-сервера | `80` |
| `SERVER_HTTPS_PORT` | string | Да | HTTPS-порт веб-сервера | `443` |
| `SERVER_ROOT` | string | Да | Адрес домена сервера | `localhost` |
| `WEB_PROTOCOL` | string | Да | Протокол обмена (HTTP/HTTPS) | `https` |
| `PARALLEL_TENANTS` | integer | Нет | Количество тенантов для параллельной обработки | `1` |

### Связанные секции config.yml

DTC использует переменные из других секций через `common_config`:

| Секция | Переменные для DTC |
|---|---|
| `common_config` → `DATABASE_ENGINE` | Тип СУБД (`postgres`) |
| `common_config` → `CONNECTION_STRING` | Строка подключения к БД |
| `common_config` → `QUEUE_CONNECTION_STRING` | Строка подключения к RabbitMQ |
| `common_config` → `AUTHENTICATION_USERNAME` | Пользователь для публикации (по умолчанию) |
| `common_config` → `AUTHENTICATION_PASSWORD` | Пароль пользователя (по умолчанию) |
| `variables` → `home_path` | Корневая папка данных |
| `variables` → `http_port` / `https_port` | Порты доступа к системе |
| `variables` → `protocol` | Протокол (`http` / `https`) |

---

## Python-обёртка Launcher

### Архитектура класса DeploymentTool

```
PlatformService ──┐
                  ├── DeploymentTool (alias="dt")
PlatformToolBase ─┘
```

Класс `DeploymentTool` наследует:
- **`PlatformService`** — управление Docker-контейнером (up/down/start/stop), генерация конфигов
- **`PlatformToolBase`** — базовая логика утилит (exe path, config generation)

> TODO в коде: «Отказаться от наследования от PlatformService, сейчас наследование и от сервиса и от утилиты выглядит плохо».

### Ключевые атрибуты

```python
self.rid                  # RuntimeIdentifier: linux | win
self.command              # Текущая команда DTC
self.packages             # Список путей к пакетам
self.interface            # Путь к интерфейсной сборке
self.settingsPackage      # Путь к пакету настроек (import)
self.exportSettings       # Путь для экспорта настроек
self.export_package_path  # Путь для экспорта пакета
self.repositories_root_folder_path  # Корневая папка репозиториев
self.export_package_info_path       # Путь к XML-конфигурации пакета
self.filter               # Функция фильтрации логов
self.image_kind           # ImageKinds.SDK
self.executable           # "DeploymentToolCore"
```

### Пути в контейнере (Linux)

```python
self.interface_dir_in_container = '/app/interface'
self.settings_export_dir_in_container = '/app/settings_export/'
self.package_export_dir_in_container = '/app/package_export/'
self.repositories_root_folder_path_in_container = '/app/repositories/'
```

### _get_deploy_command_line() — маппинг аргументов

Метод собирает итоговую командную строку из словаря параметров:

```python
deploy_params: Dict[Optional[str], str] = {
    package:                f' -d {packages_with_spaces_and_quotes}',
    init:                   f' -x {solution_names}'.rstrip(),
    settings:               ' -s',
    username:               f' -n "{username}"',
    userpassword:           f' -p "{userpassword}"',
    interface:              f' -b "{self.interface}"',
    ls:                     ' -l',
    command:                f' {command}',
    settings_localization:  f' --settings-localization "{settings_localization}"'
}
```

Ключевые трансформации:
- `init="True"` или `init="init"` → инициализация всех модулей (`-x`)
- `init="DirectumRX Memo"` → инициализация конкретных решений (`-x DirectumRX Memo`)
- `ls=True` → `-l`
- Множественные пакеты: каждый путь оборачивается в кавычки, разделяется пробелом
- `parallel_tenants` берётся из аргумента или из конфига (`PARALLEL_TENANTS`)

### Трансформация путей (runtime dispatch)

Методы с суффиксами `_on_docker` и `_on_servicerunner` вызываются через `@call_runtime_method` в зависимости от `rid`:

| Метод | Linux (Docker) | Windows (ServiceRunner) |
|---|---|---|
| `_get_run_package_param` | `/app/{basename}` | Путь as-is |
| `_get_interface_path` | `/app/interface` | `<etc>/interface` или пользовательский |
| `_get_export_path` | `/app/settings_export/{basename}` | Путь as-is |
| `_get_path` | Вызывает `path_getter_func_on_docker` | Вызывает `path_getter_func_on_servicerunner` |

### Docker volumes generation

Метод `get_total_volumes()` динамически формирует словарь volumes:

```python
base = super().get_total_volumes()  # Базовые volumes от PlatformService

# Пакеты разработки (read-only)
if self.packages:
    for path in self.packages:
        base[path] = {'bind': '/app/<basename>', 'mode': 'ro'}

# Интерфейсная сборка (read-write)
if self.interface:
    base['<etc>/interface'] = {'bind': '/app/interface', 'mode': 'rw'}

# Импорт настроек (read-write)
if self.settingsPackage:
    base[path] = {'bind': '/app/<basename>', 'mode': 'rw'}

# Экспорт настроек (read-write)
if self.exportSettings:
    base[dir_or_file] = {'bind': '/app/settings_export/', 'mode': 'rw'}

# Экспорт пакета (read-write)
if self.export_package_path:
    base[dirname] = {'bind': '/app/package_export/', 'mode': 'rw'}

# Репозитории (read-write)
if self.repositories_root_folder_path:
    base[path] = {'bind': '/app/repositories/', 'mode': 'rw'}
```

### Config reading

Учётные данные берутся из конфига:
```python
username = user if user else get_first_var_value('AUTHENTICATION_USERNAME', self.config)
userpassword = password if password else try_get_first_var_value('AUTHENTICATION_PASSWORD', self.config, '')
```

`PARALLEL_TENANTS` может задаваться через аргумент командной строки (приоритет) или через конфиг.

---

## Distributed Deployment

### Принцип работы

Публикация с параметром `--distributed` обновляет узлы кластера **последовательно**, обеспечивая непрерывную доступность системы:

1. Выбирается первый узел кластера
2. Узел помечается как недоступный (перестаёт отвечать на HTTP healthcheck)
3. Балансировщик (HAProxy) перенаправляет трафик на оставшиеся узлы
4. Выполняется публикация на недоступном узле
5. Узел возвращается в работу
6. Процесс повторяется для следующего узла
7. Завершается после обновления всех узлов

### Ограничения

- **Недоступна** при изменениях, требующих генерацию структуры данных (создание типа сущности, изменение типа данных и т.п.)
- Нельзя использовать совместно с ключами `-x` (инициализация) и `-r` (удаление решений)
- Подходит только для «малых» изменений: обработчики, функции, строки локализации, иконки

### Предусловия

Настройка healthcheck в HAProxy для каждого backend:

```
backend rx-nodes-api
    option httpchk
    http-check send meth GET uri /client/api/health hdr host rx.example.com
    balance roundrobin
    server rx1 192.168.1.30:443 check
    server rx2 192.168.1.31:443 check

backend integration-nodes-backend
    option httpchk
    http-check send meth GET uri /integration/health hdr host rx.example.com
    balance roundrobin
    server rx1 192.168.1.30:443 check
    server rx2 192.168.1.31:443 check
```

### Конфигурация HAProxy в config.yml

```yaml
services_config:
    SungeroHaproxy:
        haproxy_config: '{{ home_path }}/haproxy/haproxy.cfg'
        ssl_cert: ''
        http_port: '{{ http_port }}'
        https_port: '{{ https_port }}'
        stats_user: 'User'
        stats_password: '11111'
        stats_port: '8080'
        use_prometheus_metrics: true
```

### Запуск

```bash
# Linux
./do.sh dt deploy --package="/srv/CustomDev/DevPackage.dat" --distributed

# Windows
do dt deploy --package="D:\CustomDev\DevPackage.dat" --distributed

# Через run
do dt run --command="-n Administrator -p 11111 -d \"D:\CustomDev\DevPackage.dat\" --distributed"
```

При обнаружении изменений, требующих генерацию структуры данных, выводится предупреждение и публикация не запускается. Необходимо использовать стандартную публикацию без `--distributed`.

---

## Тестирование

Тестовый файл `deployment_tool_tests.py` содержит 20+ тест-кейсов, покрывающих:

### Генерация командной строки

**Linux:**
```python
# Инициализация всех модулей
dt._get_deploy_command_line(init=True)
# → ' -x -n "Service User" --parallel-tenants 10'

# Инициализация конкретных решений
dt._get_deploy_command_line(init='DirectumRX Memo')
# → ' -x DirectumRX Memo -n "Service User" --parallel-tenants 10'

# Пакет + инициализация (пути трансформируются)
dt._get_deploy_command_line(package="/home/vm-operator/test.dat", init=True)
# → ' -d "/app/test.dat" -x -n "Service User" --parallel-tenants 10'

# Список решений
dt._get_deploy_command_line(ls=True)
# → ' -n "Service User" -l --parallel-tenants 10'

# Merge
dt._get_deploy_command_line(package="/app/first.dat;/app/second.dat", merge="/app/union.dat")
# → ' -d "/app/first.dat" "/app/second.dat" -n "Service User" -m "/app/package_export/union.dat" --parallel-tenants 10'
```

### Unicode-пути и пробелы

```python
# Множественные пакеты с Unicode и пробелами
dt._get_deploy_command_line(
    package="/home/vm operator/test.dat;/etc/data/pack.dat;/home/Мой юзер/dev pack.dat",
    init='DirectumRX Memo'
)
# → ' -d "/app/test.dat" "/app/pack.dat" "/app/dev pack.dat" -x DirectumRX Memo -n "Service User" --parallel-tenants 10'
```

### Разделение пакетов (_split_packages)

```python
# Несколько пакетов через ";"
_split_packages(r"D:\RX dir\пакет 1.dat;С:\разработка\package2.dat;\\share\pack 3.dat")
# → [r'D:\RX dir\пакет 1.dat', r'С:\разработка\package2.dat', r'\\share\pack 3.dat']

# Пустые значения и пробелы
_split_packages(";a 1; ;a 2; ; ")
# → ['a 1', 'a 2']

_split_packages("")  # → []
_split_packages(";;;")  # → []
_split_packages(None)  # → []
```

### Парсинг вывода DTC

```python
output = [
    "22:24:29.607  - DirRX.ProjectPlanning, 2.8.4600.0",
    "22:24:29.607  - DirRX.TeamsCommon, 1.0.4600.1",
    "22:24:29.607  - Sungero.DirectumRX, 4.6.24.0"
]
# Фильтрация даёт:
# {"DirRX.ProjectPlanning 2.8.4600.0", "DirRX.TeamsCommon 1.0.4600.1", "Sungero.DirectumRX 4.6.24.0"}
```

### Parallel tenants

```python
# Аргумент parallel_tenants имеет приоритет над конфигом
config.services_config['DeploymentTool']['PARALLEL_TENANTS'] = 10
dt._get_deploy_command_line(init=True, parallel_tenants=20)
# → ' -x -n "Service User" --parallel-tenants 20'
```

### Проверка существования решения в БД

Сравнение версий через semver:
- Версия в БД >= версии пакета → решение считается установленным (`True`)
- Версия в БД < версии пакета → решение не установлено (`False`)
- Решение отсутствует в БД → не установлено (`False`)

### Генерация Dockerfile

Тест подтверждает точное совпадение генерируемого Dockerfile с эталоном:
```python
dockerfile_content = dt._create_dockerfile_generator("test_package.tar.gz").generate("4.0.0")
# Проверяется побайтовое совпадение с ожидаемым Dockerfile
```

### Data classes

- `DeployedSolutionInfo(Name, Version)` — hashable, поддерживает set-операции
- `AppliedSolutionInfo(Name, Version, IncludeSources, PackagePath)` — hashable, конвертация в `DeployedSolutionInfo` через `.as_deployed_solution()`
- Проверки: equality, sets, subset, conversion

---

## Known Issues & Limitations

### Поведенческие особенности

1. **Автоматическое включение init/settings.** При указании только `--package` без явных `--dev`, `--init`, `--settings` автоматически включаются инициализация и настройки. Это может быть неожиданным.

2. **Отклонение повторной публикации.** Если решение уже в БД с такой же или более новой версией — публикация молча отклоняется без ошибки. Используйте `--force` для перепубликации.

3. **Проверка несовместимости.** DTC проверяет совместимость зависимых решений. При обнаружении несовместимости требуется передеплоить все зависимые решения вместе.

4. **Версии DTC и веб-сервера.** Версии должны совпадать на уровне редакции релиза (26.1.0.XXXX). Несовпадение приводит к exit code 2.

5. **Спецсимволы в логинах/паролях.** Логины и пароли со спецсимволами и пробелами оборачиваются в двойные кавычки. Кавычки в пароле экранируются удвоением: `pas~!@"88` → `"pas~!@""88"`.

6. **Distributed deployment.** Невозможна при изменениях структуры данных (DDL). Нельзя сочетать с инициализацией (`-x`) и удалением (`-r`).

7. **Имя merge-пакета.** Если при `--merge-only` указана только папка (без имени файла), генерируется имя: `MergedPackage-<yyyy-MM-dd_HH-mm-ss>.dat`.

8. **Настройки после инициализации.** Параметр `-s` (apply-settings) всегда выполняется после `-x` (инициализация), так как настройки могут ссылаться на сущности, создаваемые при инициализации.

9. **Двойное наследование.** Класс `DeploymentTool` наследует и от `PlatformService`, и от `PlatformToolBase`, что признано авторами проблемным (TODO в коде).

10. **Путь к интерфейсной сборке.** По умолчанию: `<etc_path>/interface`. Создаётся автоматически при отсутствии.

---

## Примеры End-to-End

### Workflow: Экспорт → Объединение → Публикация → Верификация

#### Linux

```bash
# 1. Экспорт пакета из исходного кода
./do.sh dt export-package \
    --export_package /home/user/builds/MyModule.dat \
    --root /home/user/repos \
    --repositories Base \
    --work Work

# 2. Объединение пакетов (если несколько решений)
./do.sh dt merge_packages \
    "/home/user/builds/merged.dat" \
    --packages="/home/user/builds/MyModule.dat;/home/user/builds/BaseSolution.dat"

# 3. Публикация
./do.sh dt deploy --package="/home/user/builds/merged.dat"

# 4. Верификация
./do.sh dt get_deployed_solutions
```

#### Windows

```bat
REM 1. Экспорт пакета
do dt export-package ^
    --export_package D:\builds\MyModule.dat ^
    --root D:\repos ^
    --repositories Base ^
    --work Work

REM 2. Объединение
do dt merge_packages "D:\builds\merged.dat" ^
    --packages="D:\builds\MyModule.dat;D:\builds\BaseSolution.dat"

REM 3. Публикация
do dt deploy --package="D:\builds\merged.dat"

REM 4. Верификация
do dt get_deployed_solutions
```

### Workflow: Экспорт и импорт настроек

```bash
# Экспорт текущих настроек
./do.sh dt export_settings --path="/home/user/settings/" --user="Administrator" --password="11111"

# Импорт настроек на другой стенд
./do.sh dt import_settings --path="/home/user/settings/settings-2026-03-28_10-00-00.datx"
```

### Workflow: Публикация в распределённую систему

```bash
# 1. Предварительно объединить пакеты
./do.sh dt merge_packages "/srv/builds/merged.dat" \
    --packages="/srv/builds/Dev1.dat;/srv/builds/Dev2.dat"

# 2. Публикация в распределённую систему (zero-downtime)
./do.sh dt deploy --package="/srv/builds/merged.dat" --distributed
```

### Workflow: Обновление версии и экспорт

```bash
# 1. Увеличить версию
./do.sh dt increment_version --root /home/user/repos --repositories Base

# Или задать конкретную
./do.sh dt set_version --version 1.0.0.1 --root /home/user/repos --repositories Base

# 2. Экспорт
./do.sh dt export-package \
    --export_package /home/user/builds/Release_1.0.0.1.dat \
    --configuration /home/user/config/export.xml \
    --root /home/user/repos \
    --repositories Base

# 3. Публикация
./do.sh dt deploy --package="/home/user/builds/Release_1.0.0.1.dat" --force
```
