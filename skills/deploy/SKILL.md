---
description: "Собрать .dat пакет и опубликовать на стенд Directum RX через DeploymentTool"
---

# Deploy — Сборка и публикация пакета

Быстрая команда: собирает .dat → публикует через DeploymentTool → проверяет результат.

## Входные данные

Определи автоматически (или спроси):
- **project_path** — путь к проекту (где лежит `PackageInfo.xml`)
- **launcher_path** — путь к DirectumLauncher (где лежит `do.sh`)
- **module_name** — имя модуля (из PackageInfo.xml `<Name>`)
- **mode** — режим публикации: `dev` (по умолчанию), `full`, `init`, `settings`

## Алгоритм

### 1. Определи параметры
```bash
# Прочитай имя модуля из PackageInfo.xml
grep -o '<Name>[^<]*</Name>' {project_path}/PackageInfo.xml
```

### 2. Валидация (быстрая)
Проверь критичное:
- [ ] `PackageInfo.xml` существует
- [ ] `source/` директория существует
- [ ] Все `.mtd` — валидный JSON
- [ ] Все `.cs` содержат `partial class` (кроме Constants)

```bash
# Проверка JSON-валидности .mtd
find {project_path}/source/ -name "*.mtd" -exec sh -c 'python3 -m json.tool "$1" > /dev/null 2>&1 || echo "INVALID: $1"' _ {} \;

# Проверка partial class
grep -rn "public class\|^class " {project_path}/source/ --include="*.cs" | grep -v "partial" | grep -v "static class" | grep -v "Constants"
```

Если ошибки найдены — **ОСТАНОВИ** публикацию, покажи ошибки.

### 3. Сборка .dat
```bash
cd {project_path}
rm -f {module_name}.dat
# settings/ включай ТОЛЬКО если директория существует
find source -type f | sort > /tmp/filelist.txt
echo "PackageInfo.xml" >> /tmp/filelist.txt
[ -d settings ] && find settings -type f | sort >> /tmp/filelist.txt
zip -@ "{module_name}.dat" < /tmp/filelist.txt
```
> На Mac/Linux: `do.sh`, на Windows: `do.bat`
> Docker-логи: `deploy/data/logs/`. Launcher логи: `{launcher_path}/log/`

### 4. Публикация
```bash
# Режим dev (только код, без init/settings) — быстро:
{launcher_path}/do dt deploy --package="{project_path}/{module_name}.dat" --force --dev

# Режим full (код + init + settings):
{launcher_path}/do dt deploy --package="{project_path}/{module_name}.dat" --force

# Режим init (только инициализация):
{launcher_path}/do dt deploy --package="{project_path}/{module_name}.dat" --force --init

# Режим settings (только настройки):
{launcher_path}/do dt deploy --package="{project_path}/{module_name}.dat" --force --settings
```

### 5. Проверка результата
```bash
# Проверить что решение появилось
{launcher_path}/do dt get-deployed-solutions

# Проверить логи на ошибки
grep -iE "ERROR|FAIL|Exception" {launcher_path}/log/current.log

# Проверить здоровье платформы
{launcher_path}/do platform check
```

### 6. Пост-публикационная проверка ресурсов

После успешной публикации проверь, что локализованные ресурсы развёрнуты корректно:

```bash
# Проверить наличие satellite assembly (ru) для каждого модуля
# Satellite DLL находятся внутри контейнеров RX в AppliedModules/ru/
docker compose -f deploy/docker-compose.rx.yml exec web ls /app/AppliedModules/ru/*.resources.dll 2>/dev/null

# Проверить что System.resx содержит Property_<Name> ключи (не Resource_<GUID>)
grep -r "Resource_" {project_path}/source/**/*System*.resx && echo "[ОШИБКА] Найдены устаревшие ключи Resource_<GUID>" || echo "[OK] Формат ключей корректен"
```

**Если найдены ключи `Resource_<GUID>`** — .resx файлы содержат неправильный формат. Нужно:
1. Заменить `Resource_<GUID>` → `Property_<PropertyName>` в `*System.resx` / `*System.ru.resx`
2. Пересобрать satellite assembly (см. CLAUDE.md секция «Satellite Assembly Rebuild»)
3. Остановить контейнеры RX: `docker compose stop` (из deploy/)
4. Скопировать новые satellite DLL в `AppliedModules/ru/` внутри контейнера
5. Перезапустить контейнеры: `docker compose up -d` (из deploy/)

### 7. При ошибке
Если публикация упала — вызови агента **Debugger** (`.claude/agents/debugger.md`):
- Передай текст ошибки
- Он классифицирует, найдёт причину, исправит
- После исправления — повтори с шага 3

## Режимы

| Режим | Флаги | Когда использовать |
|-------|-------|--------------------|
| `dev` | `--force --dev` | Итеративная разработка (быстро) |
| `full` | `--force` | Первая публикация или после изменений init/settings |
| `init` | `--force --init` | Только инициализация данных |
| `settings` | `--force --settings` | Только применение настроек |
| `distributed` | `--distributed` | Zero-downtime в кластере (HAProxy) |
| `init-selective` | `--force --init="ModuleName"` | Инициализация конкретного модуля |
| `parallel` | `--parallel-tenants=N` | Мультитенант — параллельная обработка |

## Exit-коды DeploymentToolCore

| Код | Значение | Действие |
|-----|----------|----------|
| 0 | Успех | — |
| 1 | Ошибка до деплоя | Невалидные аргументы, corrupt пакет, ошибка генерации |
| 2 | Ошибка деплоя | Сеть, сервер недоступен, несовпадение версий DTC ≠ WebServer |
| 3 | Ошибка инициализации | Init request failed, модуль не смог инициализироваться |
| 4 | Ошибка настроек | Не удалось применить default settings |
| 5 | Ошибка импорта настроек | import_settings failed |
| 6 | Ошибка экспорта настроек | export_settings failed |
| 7 | Ошибка экспорта пакета | export-package failed (corrupt sources) |
| 8 | Ошибка версионирования | increment_version / set_version failed |

## Distributed Deployment (Zero-Downtime)

Для кластеров с HAProxy. Обновляет узлы по одному без простоя.

### Ограничения
- НЕ работает с `--init` или `--remove-solutions`
- Только изменения, НЕ требующие генерации DB-схемы:
  - Допустимо: обновление обработчиков, новые функции, текстовые изменения
  - Недопустимо: новые сущности, изменение типов колонок, новые таблицы

### Команда
```bash
{launcher_path}/do.sh dt deploy --package="..." --distributed
```

## Merge пакетов

Объединить несколько .dat в один:
```bash
{launcher_path}/do.sh dt merge_packages "/output/Full.dat" --packages="Base.dat;Custom.dat;CRM.dat"
```

## Версионирование

```bash
# Инкремент версии
{launcher_path}/do.sh dt increment_version --root="/git" --repositories="work"

# Установить конкретную версию
{launcher_path}/do.sh dt set_version --version="2.0.0.1" --root="/git" --repositories="work"
```

## Ссылки
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная reference DTC
- `knowledge-base/guides/38_platform_integration_map.md` — карта интеграций
- `.claude/skills/export-package/SKILL.md` — экспорт .dat из git
- `.claude/skills/settings-management/SKILL.md` — export/import настроек
- `.claude/skills/launcher-service/SKILL.md` — управление сервисами
