# Настройка Windows-стенда для автономной работы Claude Code

## Цель
Claude Code на Windows автономно: генерирует код, собирает .dat, публикует на стенд, проверяет ошибки, исправляет, повторяет.

---

## Уровень 1: МИНИМУМ (генерация + сборка .dat)

### Что нужно установить
1. **Claude Code** (VS Code Extension или CLI)
2. **Git** (из redist/ или `winget install Git.Git`)
3. Перенести папку проекта как есть

### Что сможет Claude Code
- Генерировать .cs, .mtd, .resx, PackageInfo.xml
- Собирать .dat пакет: `zip -D -r Module.dat PackageInfo.xml source/ settings/`
- Работать с git-репозиториями
- Проверять структуру (валидация через скилл `/validate-package`)

### Чего НЕ сможет
- Компилировать C# (нет MSBuild/Roslyn)
- Публиковать на сервер
- Видеть ошибки компиляции DDS

---

## Уровень 2: СБОРКА (компиляция C#)

### Дополнительно установить
1. **.NET SDK 8.0** (из `redist/master/win7-x64/dotnet-sdk-8.0.415-win-x64.exe`)
2. **.NET Framework 4.8 Developer Pack** (из `redist/master/win7-x64/ndp48-devpack-enu.exe`)

### Что даёт
- `dotnet build` / `dotnet msbuild` для компиляции C#
- Раннее обнаружение ошибок (CS1503, CS0029 и т.д.) без DDS
- Проверка типов, интерфейсов, ссылок

### Проблема
- Для сборки нужны reference-сборки платформы (Sungero.Domain.dll, Sungero.CoreEntities.dll и т.д.)
- Они лежат в `developmentstudio/AddIns/Sungero/` и `platform/` (суммарно ~200 DLL)
- Нужен .csproj с правильными references

### Решение
Создать минимальный .csproj, ссылающийся на DLL из `developmentstudio/AddIns/Sungero/`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Sungero.Domain">
      <HintPath>..\..\developmentstudio\AddIns\Sungero\Sungero.Domain.dll</HintPath>
    </Reference>
    <!-- + остальные Sungero.*.dll -->
  </ItemGroup>
</Project>
```

---

## Уровень 3: ПОЛНЫЙ ЦИКЛ (сборка + публикация)

### Вариант A: Через DirectumLauncher + DeploymentTool (РЕКОМЕНДУЕТСЯ)

#### Установить
1. Всё из Уровня 2
2. **DirectumLauncher** — уже есть (`directumlauncher/`)
3. **PostgreSQL** (или развернуть через Docker)
4. **RabbitMQ** + **Erlang** (из `redist/`)
5. **IIS** + URL Rewrite + ARR (из `redist/`)

#### Развернуть стенд
```batch
cd directumlauncher

rem 1. Создать config.yml (из etc/config.yml.example)
copy etc\config.yml.example etc\config.yml

rem 2. Отредактировать config.yml:
rem    - CONNECTION_STRING (PostgreSQL)
rem    - AUTHENTICATION_USERNAME / PASSWORD
rem    - home_path
rem    - host_fqdn

rem 3. Установить компоненты
do.bat components install platform
do.bat components install base
do.bat components install crossplatformdevelopmentstudio

rem 4. Запустить платформу
do.bat platform install

rem 5. Установить DS
do.bat ds install
```

#### Команды для Claude Code
```batch
rem Публикация .dat пакета
do.bat dt deploy --package="C:\projects\Acme.CRM\Acme.CRM.dat"

rem Только публикация (без init/settings) — быстрее
do.bat dt deploy --package="Acme.CRM.dat" --dev

rem Публикация + инициализация + настройки
do.bat dt deploy --package="Acme.CRM.dat" --init --settings

rem Принудительная публикация (если уже есть в БД)
do.bat dt deploy --package="Acme.CRM.dat" --force

rem Проверить что опубликовано
do.bat dt deploy --ls

rem Список решений в БД
do.bat dt get-deployed-solutions

rem Проверить здоровье сервисов
do.bat platform check
```

#### Цикл автономной разработки Claude Code
```
1. Генерация кода (.cs, .mtd, .resx)
2. Сборка .dat: zip -D -r Acme.CRM.dat PackageInfo.xml source/ settings/
3. Публикация: do.bat dt deploy --package="Acme.CRM.dat" --force
4. Проверка логов: type log\current.log | findstr ERROR
5. Если ошибки → исправить код → повторить с шага 2
6. Если успех → do.bat platform check
```

### Вариант B: Через CrossPlatform DS (Electron)

#### Установить
1. Всё из Варианта A
2. CrossPlatform DS Desktop (`do.bat ds install`)

#### Что даёт сверх Варианта A
- Electron UI для визуального контроля (дерево решений, свойства)
- HTTP API бекенда на порту 7190 (потенциально для автоматизации)
- VS Code Extension для навигации по коду

#### Ограничение
- API бекенда CrossPlatform DS пока недокументировано
- Для полной автоматизации пока лучше `do.bat dt deploy`

### Вариант C: Через старую DDS (только Windows)

#### Установить
1. Всё из Варианта A
2. DDS (`do.bat dds install`)

#### Что даёт
- Полная IDE (редактор, свойства, дерево решений)
- Компиляция встроенным Roslyn
- Визуальное редактирование .mtd
- Встроенный workflow-дизайнер

#### Ограничение
- GUI-only, Claude Code не может напрямую вызвать сборку/публикацию через CLI
- Подходит для ручного контроля

---

## Уровень 4: ПРОДВИНУТЫЙ (export-package из git)

### Что даёт
Вместо ручной сборки .dat через `zip`, использовать DeploymentTool для экспорта из git-репозитория:

```batch
rem Экспорт из git-репозитория
do.bat dt export-package ^
  --export_package="C:\output\Acme.CRM.dat" ^
  --root="C:\git_repository" ^
  --repositories="Acme.CRM" ^
  --work="work"
```

### Требования
- Исходники должны лежать в git-репозитории с правильной структурой
- `GIT_ROOT_DIRECTORY` в config.yml должен указывать на корень
- Структура: `{GIT_ROOT_DIRECTORY}/work/source/Acme.CRM/...`

### Преимущество
- DeploymentTool сам упакует, проверит метаданные, установит версию
- Меньше шансов на ошибки формата .dat

---

## Требования к оборудованию

| Компонент | Минимум | Рекомендуемое |
|-----------|---------|---------------|
| **RAM** | 8 ГБ | 16 ГБ |
| **CPU** | 4 ядра | 8 ядер |
| **Диск** | 50 ГБ (SSD) | 100 ГБ (NVMe SSD) |
| **ОС** | Windows 10/11 | Windows 11 |
| **Сеть** | Не требуется* | Для Nexus / Git remote |

*Стенд может работать полностью локально.

---

## Что нужно перенести на Windows

### Обязательно (весь проект)
```
Директум/
├── .claude/                    # Агенты, скиллы, хуки
├── CLAUDE.md                   # Инструкции для Claude Code
├── knowledge-base/             # 28 гайдов
├── archive/                    # Примеры и справочные данные
├── projects/                   # Текущие проекты (Acme.CRM)
├── esm/                        # ESM production-пример
├── directumlauncher/           # Оркестратор (do.bat)
├── platform/                   # Платформа (26 сервисов)
├── base/                       # Базовое решение
├── crossplatformdevelopmentstudio/  # Новая DS
├── developmentstudio/          # Старая DDS (для справки)
├── redist/                     # Пререквизиты
└── .vscode/                    # Настройки VS Code
```

### Нужно обновить пути в CLAUDE.md
- Все пути `/Users/dima/Desktop/Директум/` → `C:\Директум\` (или куда положишь)
- Путь memory: обновится автоматически через Claude Code

### Нужно обновить в memory/
- Пути к файлам (макро, если есть абсолютные)

---

## Порядок действий на Windows

### Шаг 1: Установить пререквизиты
```batch
rem Из redist/master/win7-x64/
start /wait dotnet-sdk-8.0.415-win-x64.exe /quiet
start /wait ndp48-devpack-enu.exe /quiet
start /wait Git-install.exe /VERYSILENT
start /wait otp_win64_27.2.4.exe /S
start /wait rabbitmq-server-4.0.6.exe /S
msiexec /i Rewrite.msi /quiet
msiexec /i requestRouter_amd64.msi /quiet
```

### Шаг 2: Перенести проект
```batch
rem Скопировать всю папку
xcopy /E /I "\\mac\Директум" "C:\Директум"
```

### Шаг 3: Настроить DirectumLauncher
```batch
cd C:\Директум\directumlauncher
copy etc\config.yml.example etc\config.yml
rem Отредактировать config.yml (CONNECTION_STRING, пути, порты)
```

### Шаг 4: Установить платформу
```batch
do.bat components install platform
do.bat platform install
```

### Шаг 5: Установить базовое решение
```batch
do.bat components install base
do.bat rx install
```

### Шаг 6: Установить CrossPlatform DS
```batch
do.bat components install crossplatformdevelopmentstudio
do.bat ds install
```

### Шаг 7: Установить Claude Code
```batch
rem VS Code Extension — из Marketplace
rem Или CLI — npm install -g @anthropic-ai/claude-code
```

### Шаг 8: Проверить
```batch
rem Проверка стенда
do.bat platform check

rem Проверка DS
do.bat ds run

rem Пробная публикация
do.bat dt deploy --package="C:\Директум\projects\Acme.CRM\Acme.CRM.dat" --force
```

---

## Команды для автоматизации (Claude Code на Windows)

### Сборка .dat
```batch
cd C:\Директум\projects\Acme.CRM
powershell Compress-Archive -Path PackageInfo.xml,source,settings -DestinationPath Acme.CRM.zip -Force
ren Acme.CRM.zip Acme.CRM.dat
```

Или через 7z (если установлен):
```batch
7z a -tzip -mx=0 Acme.CRM.dat PackageInfo.xml source\ settings\
```

### Публикация
```batch
C:\Директум\directumlauncher\do.bat dt deploy --package="C:\Директум\projects\Acme.CRM\Acme.CRM.dat" --force --dev
```

### Проверка ошибок
```batch
type C:\Директум\directumlauncher\log\current.log | findstr /I "ERROR FAIL Exception"
```

### Проверка опубликованных решений
```batch
C:\Директум\directumlauncher\do.bat dt get-deployed-solutions
```

### Перезапуск сервисов
```batch
C:\Директум\directumlauncher\do.bat platform down
C:\Директум\directumlauncher\do.bat platform up
```

---

## Что пригодится из текущего проекта

| Ресурс | Файл | Зачем |
|--------|------|-------|
| **28 гайдов** | `knowledge-base/guides/` | Вся документация платформы |
| **11 агентов** | `.claude/agents/` | Мультиагентная система разработки |
| **14 скиллов** | `.claude/skills/` | /pipeline, /commit, /validate-package и др. |
| **CLAUDE.md** | `CLAUDE.md` | Все правила и паттерны |
| **Memory** | `.claude/projects/.../memory/` | Накопленные инсайты (20+ файлов) |
| **ESM** | `esm/ESM.dat` | Production-эталон решения |
| **archive/base/** | `archive/base/` | 29 модулей платформы (11800+ .mtd) |
| **archive/extracted/** | `archive/extracted/` | Примеры экспорта из DDS |
| **Acme.CRM** | `projects/Acme.CRM/` | Готовый проект для тестирования |
| **DDS DLLs** | `developmentstudio/AddIns/Sungero/` | Reference-сборки для компиляции |
| **Шаблоны .st** | `developmentstudio/AddIns/Sungero/Templates/` | 3136 шаблонов кодогенерации |

---

## Итоговая схема автономного цикла

```
Claude Code (Windows)
    │
    ├── 1. ГЕНЕРАЦИЯ КОДА
    │   ├── Читает knowledge-base/ и archive/base/
    │   ├── Использует CLAUDE.md правила
    │   ├── Генерирует .cs, .mtd, .resx
    │   └── Валидирует через /validate-package
    │
    ├── 2. СБОРКА .DAT
    │   ├── zip -D -r Module.dat PackageInfo.xml source/ settings/
    │   └── (опционально) dotnet build для проверки компиляции
    │
    ├── 3. ПУБЛИКАЦИЯ
    │   ├── do.bat dt deploy --package="Module.dat" --force
    │   └── Парсит stdout/log на ошибки
    │
    ├── 4. ПРОВЕРКА
    │   ├── do.bat dt get-deployed-solutions
    │   ├── do.bat platform check
    │   └── Парсит логи: findstr ERROR log\current.log
    │
    └── 5. ИСПРАВЛЕНИЕ (если ошибки)
        ├── Анализирует ошибку
        ├── Исправляет код
        └── Повторяет с шага 2
```
