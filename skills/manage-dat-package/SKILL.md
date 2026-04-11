---
description: "Полный lifecycle .dat пакета: валидация, исправление, сборка, деплой на стенд Directum RX"
---

# Manage DAT Package — Lifecycle .dat пакета

Валидация, исправление, сборка и деплой .dat пакета Directum RX.

---

## ШАГ 0: Реальные пути CRM-пакета

```
# WORKSPACE — корень рабочего пространства (определи через pwd в корне репозитория)
# PACKAGE_PATH — путь к DDS-пакету (содержит PackageInfo.xml и source/)
# Найди пакет: Glob("*/PackageInfo.xml") или Glob("*-package/source/")

Структура пакета:
  {package_path}/                                     — корень пакета
  {package_path}/PackageInfo.xml                       — манифест пакета
  {package_path}/source/                               — исходники (модули)

Модули: определи GUID из PackageInfo.xml и Module.mtd (NameGuid).
  Используй: Read {package_path}/PackageInfo.xml для списка модулей и их GUID.

Launcher:
  Определи путь через $LAUNCHER_PATH или найди: Glob("дистрибутив/*/do.sh")

Выходной .dat:
  {package_path}/{SolutionName}.dat                    — собранный пакет
```

---

## ШАГ 1: Формат PackageInfo.xml

PackageInfo.xml — манифест пакета. Определяет состав модулей.

```xml
<?xml version="1.0" encoding="utf-8"?>
<DevelopmentPackageInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                        xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <!-- Корневое решение (IsSolution=true, без SolutionId) -->
    <PackageModuleItem>
      <Id>f5a3bed1-283c-4462-8a50-1e1b3fb2b86e</Id>
      <Name>DirRX.Solution</Name>
      <Version>2.0.0.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>true</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <!-- Модуль (IsSolution=false, SolutionId указывает на решение) -->
    <PackageModuleItem>
      <Id>9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b</Id>
      <SolutionId>f5a3bed1-283c-4462-8a50-1e1b3fb2b86e</SolutionId>
      <Name>DirRX.CRM</Name>
      <Version>2.0.0.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>true</IncludeSources>
      <IsSolution>false</IsSolution>
      <IsPreviousLayerModule>false</IsPreviousLayerModule>
    </PackageModuleItem>
    <!-- ... остальные модули аналогично -->
  </PackageModules>
</DevelopmentPackageInfo>
```

### Правила PackageInfo.xml

| Поле | Описание |
|------|----------|
| `Id` | GUID модуля (из Module.mtd → `NameGuid`) |
| `SolutionId` | GUID корневого решения (только для модулей, НЕ для решения) |
| `Name` | Полное имя модуля (совпадает с именем директории в `source/`) |
| `Version` | Семантическая версия (`Major.Minor.Patch.Build`) |
| `IsSolution` | `true` только для корневого решения |
| `IncludeAssemblies` | `true` — включить скомпилированные DLL |
| `IncludeSources` | `true` — включить исходный код |
| `IsPreviousLayerModule` | `true` — модуль из предыдущего слоя (перекрытие платформенного) |

---

## ШАГ 2: Lifecycle — цепочка операций

Полный цикл: **validate -> fix -> build -> deploy**

```
check_package  -->  fix_package (dryRun)  -->  fix_package (apply)  -->  build_dat  -->  deploy_to_stand
     |                    |                          |                      |                  |
  14 проверок        план фиксов             применение фиксов        zip -> .dat      DeploymentTool
```

---

## ШАГ 3: Валидация (check_package)

**MCP:** `check_package packagePath="{package_path}"`

14 автоматических проверок:
1. CollectionProperty в DatabookEntry
2. Cross-module NavigationProperty references
3. Enum values = C# reserved words
4. Duplicate DB column Code
5. System.resx ключи (`Property_<Name>` vs `Resource_<GUID>`)
6. Analyzers directory
7. GUID consistency
8. DisplayName presence
9. Controls в Document (пустые при Overridden)
10. CoverFunction matching
11. FormTabs support
12. PublicStructures consistency
13. DomainApi validation
14. AttachmentGroup Constraints

### Ручная быстрая валидация (fallback)

```bash
# Проверка JSON-валидности всех .mtd
find "{package_path}/source/" -name "*.mtd" \
  -exec sh -c 'python3 -m json.tool "$1" > /dev/null 2>&1 || echo "INVALID: $1"' _ {} \;

# Проверка partial class
grep -rn "public class\|^class " "{package_path}/source/" \
  --include="*.cs" | grep -v "partial" | grep -v "static class" | grep -v "Constants"

# Проверка устаревших resx-ключей
grep -r "Resource_" "{package_path}/source/"**/*System*.resx
```

---

## ШАГ 4: Исправление (fix_package)

**MCP (dry-run):** `fix_package packagePath="{package_path}" dryRun=true`
**MCP (apply):** `fix_package packagePath="{package_path}" dryRun=false`

Автоисправления:
- `Resource_<GUID>` -> `Property_<Name>` в System.resx
- Дубли DB column Code
- Enum values = reserved words (переименование)
- AttachmentGroup Constraints синхронизация

**ВСЕГДА** сначала dry-run, потом apply.

---

## ШАГ 5: Сборка .dat (build_dat)

**MCP:** `build_dat packagePath="{package_path}"`

Это создаст `DirRX.Solution.dat` в родительской директории.

### Ручная сборка (fallback)

```bash
cd "{package_path}"
rm -f "DirRX.Solution.dat"

# Собрать список файлов
find source -type f | sort > /tmp/filelist.txt
echo "PackageInfo.xml" >> /tmp/filelist.txt
[ -d settings ] && find settings -type f | sort >> /tmp/filelist.txt

# Упаковать
zip -@ "DirRX.Solution.dat" < /tmp/filelist.txt
```

### Структура .dat (zip-архив)

```
DirRX.Solution.dat
  PackageInfo.xml
  source/
    DirRX.Solution/
      DirRX.Solution.Shared/
        Module/Module.mtd
        ...
    DirRX.CRM/
      DirRX.CRM.Shared/
        ...
      DirRX.CRM.Server/
        ...
      DirRX.CRM.ClientBase/
        ...
    ...
  settings/            (опционально — настройки модуля)
```

---

## ШАГ 6: Деплой на стенд (deploy_to_stand)

**MCP (dry-run):** `deploy_to_stand dat_path="{package_path}/DirRX.Solution.dat"`
**MCP (apply):** `deploy_to_stand dat_path="..." confirm=true dry_run=false`

### Ручной деплой через DirectumLauncher

```bash
LAUNCHER="${LAUNCHER_PATH:-дистрибутив/launcher}"

# Режим dev (быстрый, только код):
$LAUNCHER/do.sh dt deploy --package="{package_path}/DirRX.Solution.dat" --force --dev

# Режим full (код + init + settings):
$LAUNCHER/do.sh dt deploy --package="{package_path}/DirRX.Solution.dat" --force

# Режим init (только инициализация данных):
$LAUNCHER/do.sh dt deploy --package="..." --force --init

# Режим settings (только настройки):
$LAUNCHER/do.sh dt deploy --package="..." --force --settings
```

### Режимы деплоя

| Режим | Флаги | Когда |
|-------|-------|-------|
| `dev` | `--force --dev` | Итеративная разработка (быстро, без init/settings) |
| `full` | `--force` | Первая публикация или после изменений init/settings |
| `init` | `--force --init` | Только инициализация данных |
| `settings` | `--force --settings` | Только применение настроек |

---

## ШАГ 7: Pre-Import Checklist

> Полный список: `docs/platform/DDS_KNOWN_ISSUES.md` (19 Known Issues)

Критичные проверки перед импортом .dat в DDS:

1. DatabookEntry без CollectionPropertyMetadata
2. NavigationProperty EntityGuid — в рамках объявленных Dependencies
3. Enum values — не C# reserved words
4. Property Code — уникальные в иерархии наследования
5. AttachmentGroup Constraints — одинаковые в Task/Assignment/Notice
6. `.sds/Libraries/Analyzers/` — существует с DLL
7. System.resx ключи — `Property_<Name>`, не `Resource_<GUID>`
8. DisplayName — у каждого Module, Entity, Report, AsyncHandler, Job
9. Document Controls — не пустые при `"Overridden": ["Controls"]`
10. Внешние библиотеки — через DDS UI, не в .csproj

---

## ШАГ 8: Post-Publish Verification

После публикации пакета на стенд:

```bash
LAUNCHER="${LAUNCHER_PATH:-дистрибутив/launcher}"

# 1. Проверить что решение развёрнуто
$LAUNCHER/do.sh dt get-deployed-solutions

# 2. Проверить логи на ошибки
grep -iE "ERROR|FAIL|Exception" $LAUNCHER/log/current.log

# 3. Проверить здоровье платформы
$LAUNCHER/do.sh platform check

# 4. Проверить satellite assembly (ru)
docker compose -f deploy/docker-compose.rx.yml exec web \
  ls /app/AppliedModules/ru/*.resources.dll 2>/dev/null
```

**Визуальная проверка (localhost:8080/Client):**
- [ ] Подписи свойств на карточках — не пустые
- [ ] Заголовки списков — DisplayName на русском
- [ ] Обложки модулей — группы и действия отображаются
- [ ] Действия обложки — клик без ошибок
- [ ] Отчёты — открываются без exceptions

---

## ШАГ 9: Сравнение пакетов (diff_packages)

**MCP:** `diff_packages pathA="old/DirRX.Solution.dat" pathB="new/DirRX.Solution.dat" scope="all"`

Области сравнения:
- `metadata` — только .mtd файлы
- `resources` — только .resx файлы
- `code` — только .cs файлы
- `all` — все изменения

---

## Типичные ошибки и решения

| Ошибка | Причина | Решение |
|--------|---------|---------|
| "Missing area" NullRef при импорте | CollectionProperty в DatabookEntry | Использовать Document вместо DatabookEntry |
| "Can't resolve function" на обложке | FunctionName в MTD не совпадает с C# методом | Проверить точное совпадение имён |
| Пустые подписи свойств | `Resource_<GUID>` вместо `Property_<Name>` | `fix_package` или ручная замена в System.resx |
| Циклическая зависимость | Cross-module NavigationProperty | Перенести тип в общий модуль (CRMCommon) |
| File locks после падения импорта | dotnet.exe держит .csproj | Перезапустить DDS |
| .dat не содержит settings/ | `find` не нашёл settings/ | Проверить что `[ -d settings ]` истинно |
| Enum "new" или "default" | C# reserved word | Переименовать: `NewLead`, `DefaultValue` |
| Пустая форма после импорта | `"Overridden": ["Controls"]` с `"Controls": []` | Убрать Controls из Overridden или заполнить |

---

## MCP Tool Chain (полная цепочка)

```
1. check_package     — 14 автоматических проверок
2. fix_package       — автоисправление (dryRun=true, потом false)
3. build_dat         — zip source/ + PackageInfo.xml -> .dat
4. deploy_to_stand   — деплой через DeploymentTool (dry_run, потом confirm)
5. diff_packages     — сравнение двух версий пакета
```

---

## ШАГ 10: Расширенные операции с пакетами

### Merge нескольких .dat

```bash
LAUNCHER="${LAUNCHER_PATH:-дистрибутив/launcher}"

# Объединить несколько пакетов в один
$LAUNCHER/do.sh dt merge_packages "{output_path}/Full.dat" \
  --packages="Base.dat;Custom.dat;CRM.dat"

# Из компонентов Launcher
$LAUNCHER/do.sh dt merge_packages "{output_path}/Full.dat" \
  --package_from_component="base;agile;crm"
```

### Получить список решений из БД

```bash
# Стандартный (из кэша DTC)
$LAUNCHER/do.sh dt get_deployed_solutions

# Напрямую из БД
$LAUNCHER/do.sh dt get_deployed_solutions_from_db
```

### Удалить решения

```bash
$LAUNCHER/do.sh dt remove_solutions --solution_names="DirRX.OldModule DirRX.Deprecated"
```

### Экспорт из git (полная компиляция)

```bash
$LAUNCHER/do.sh dt export_package \
  --export_package="{output_path}/CRM.dat" \
  --configuration="{output_path}/export-config.xml" \
  --root="$(pwd)" \
  --repositories="{package_relative_path}"
```

### Export/Import настроек (.datx)

```bash
# Экспорт всех настроек
$LAUNCHER/do.sh dt export_settings --path="{output_path}/settings.datx"

# Импорт
$LAUNCHER/do.sh dt import_settings --path="{output_path}/settings.datx"
```

### Exit-коды DeploymentToolCore

| Код | Значение | Действие |
|-----|----------|----------|
| 0 | Успех | — |
| 1 | Pre-deploy | Невалидные аргументы, corrupt .dat |
| 2 | Deploy | Сеть, WebServer, версии DTC ≠ WebServer |
| 3 | Init | ModuleInitializer failed |
| 4 | Settings | Default settings failed |
| 5 | Import settings | .datx corrupt |
| 6 | Export settings | Ошибка экспорта |
| 7 | Export package | Corrupt sources в git |
| 8 | Version | increment/set version failed |

---

## Ссылки

- `docs/platform/DDS_KNOWN_ISSUES.md` — 19 Known Issues + чеклисты
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная reference DTC
- `knowledge-base/guides/38_platform_integration_map.md` — карта интеграций
- `.claude/skills/deploy/SKILL.md` — деплой .dat на стенд
- `.claude/skills/export-package/SKILL.md` — экспорт .dat из git
- `.claude/skills/settings-management/SKILL.md` — export/import настроек
- `.claude/skills/launcher-service/SKILL.md` — управление сервисами
- `knowledge-base/guides/09_getting_started.md` — первый контакт с DDS
- `knowledge-base/guides/23_mtd_reference.md` — .mtd JSON шаблоны
