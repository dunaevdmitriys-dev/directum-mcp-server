---
description: "Export/import бизнес-настроек Directum RX (.datx) через DeploymentToolCore"
---

# Settings Management — Управление бизнес-настройками

Export и import настроек бизнес-процессов Directum RX через DeploymentToolCore.

## Что такое Settings

Бизнес-настройки — конфигурации модулей, которые применяются ПОСЛЕ публикации кода:
- **Роли и права доступа** — кто может видеть/редактировать сущности
- **Видимость модулей** — sungero_nocode_modulerec (Who can view), modulesvisible
- **Бизнес-процессы (NoCode)** — варианты процессов, маршруты согласования
- **Карточки/списки** — настройки отображения, обязательность полей, значения по умолчанию
- **Параметры модулей** — бизнес-параметры, шаблоны документов
- **Организационная структура** — шаблоны, подразделения

## Формат .datx

`.datx` = ZIP-архив, внутри:
- `metadata.json` — версия, дата экспорта, список модулей
- `settings/` — JSON-файлы с настройками каждого модуля
- `attachments/` — приложенные файлы (шаблоны, XLSX и т.д.)

Можно распаковать `unzip settings.datx -d settings-dir/` для инспекции.

## Полный Workflow

```
1. Разработать модуль (MTD, CS, RESX)
2. build_dat → deploy --dev (быстро, без settings)
3. Настроить в веб-UI: права, процессы, видимость, карточки
4. export_settings → .datx
5. git add settings/*.datx && git commit
6. На новом стенде: deploy .dat → import_settings .datx
```

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
  --user="$RX_USERNAME" \
  --password="$RX_PASSWORD"
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
  --user="$RX_USERNAME" \
  --password="$RX_PASSWORD"
```

## Коммит настроек в git

```bash
# Структура в репозитории
CRM/
  settings/
    crm-settings-2026-04-04.datx    # экспорт настроек
    README.txt                       # описание что внутри

# Коммит
git add CRM/settings/crm-settings-*.datx
git commit -m "settings: export CRM business settings (rights, processes, visibility)"
```

Рекомендации:
- Именовать `.datx` с датой: `crm-settings-YYYY-MM-DD.datx`
- Хранить в `settings/` директории модуля
- В коммите указывать что изменилось в настройках
- НЕ коммитить `.datx` с паролями/токенами

## Импорт на новом стенде

```bash
# 1. Деплой кода (без settings)
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force

# 2. Импорт бизнес-настроек
$LAUNCHER/do.sh dt import_settings \
  --path="settings/crm-settings-latest.datx" \
  --user="$RX_USERNAME" --password="$RX_PASSWORD"

# 3. Или всё сразу: деплой + apply default settings
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force --settings

# 4. Только init + settings (без re-deploy)
$LAUNCHER/do.sh dt init_and_apply_settings
```

### Локализация настроек (XLSX)
```bash
$LAUNCHER/do.sh dt deploy --package="Module.dat" --force \
  --settings_localization="/path/to/localization.xlsx"
```

## Что экспортируется / НЕ экспортируется

| Экспортируется | НЕ экспортируется |
|----------------|-------------------|
| Роли и права доступа | Структура сущностей (MTD) |
| Видимость модулей | Код обработчиков (CS) |
| Бизнес-процессы NoCode | WebAPI функции |
| Карточки/списки настройки | Локализация кода (RESX) |
| Значения по умолчанию | Фоновые задания (определения) |
| Обязательность полей | Виджеты/отчёты (определения) |
| Параметры модулей | Данные справочников |

## Exit-коды

| Код | Операция | Значение |
|-----|----------|----------|
| 0 | Любая | Успех |
| 4 | deploy --settings | Ошибка применения настроек |
| 5 | import_settings | Ошибка импорта |
| 6 | export_settings | Ошибка экспорта |

## Ссылки
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная документация DTC
- `.claude/skills/deploy/SKILL.md` — деплой .dat на стенд
- `docs/platform/DDS_KNOWN_ISSUES.md` — известные проблемы платформы
