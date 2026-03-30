---
description: "Export/import бизнес-настроек Directum RX (.datx) через DeploymentToolCore"
---

# Settings Management — Управление бизнес-настройками

Export и import настроек бизнес-процессов Directum RX через DeploymentToolCore.

## Что такое Settings

Бизнес-настройки — это конфигурации модулей, которые применяются ПОСЛЕ публикации:
- Роли и права доступа
- Параметры модулей
- Варианты процессов (NoCode)
- Настройки по умолчанию
- Организационная структура (шаблоны)

Формат: `.datx` (ZIP с XML + приложениями)

## Экспорт настроек

### Все настройки
```bash
LAUNCHER="$WORKSPACE/дистрибутив/launcher"

$LAUNCHER/do.sh dt export_settings \
  --path="/output/all-settings.datx"
```

### Настройки конкретной папки
```bash
$LAUNCHER/do.sh dt export_settings \
  --path="/output/folder-settings.datx" \
  --folder_id=12345
```

### С указанием учётных данных
```bash
$LAUNCHER/do.sh dt export_settings \
  --path="/output/settings.datx" \
  --user="Administrator" \
  --password="11111"
```

## Импорт настроек

```bash
$LAUNCHER/do.sh dt import_settings \
  --path="/input/settings.datx"
```

### С учётными данными
```bash
$LAUNCHER/do.sh dt import_settings \
  --path="/input/settings.datx" \
  --user="Administrator" \
  --password="11111"
```

## Применение настроек при деплое

При публикации пакета:
```bash
# Деплой + apply default settings
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force --settings

# Деплой + init + settings (полный)
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force

# Только init + settings (без re-deploy)
$LAUNCHER/do.sh dt init_and_apply_settings
```

### Локализация настроек (XLSX)
```bash
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force \
  --settings_localization="/path/to/localization.xlsx"
```

## Exit-коды

| Код | Операция | Значение |
|-----|----------|----------|
| 0 | Любая | Успех |
| 4 | deploy --settings | Ошибка применения настроек |
| 5 | import_settings | Ошибка импорта |
| 6 | export_settings | Ошибка экспорта |

## Workflow

```
1. Разработать модуль → deploy --dev (быстро, без settings)
2. Настроить бизнес-процессы в UI
3. export_settings → сохранить .datx
4. Закоммитить .datx в git
5. На новом стенде: deploy → import_settings (из .datx)
```

## Ссылки
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная документация DTC
- `.claude/skills/deploy/SKILL.md` — деплой .dat на стенд
