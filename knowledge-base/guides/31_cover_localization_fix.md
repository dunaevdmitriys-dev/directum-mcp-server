# Обложки модулей RX 26.1: заголовки и описания через БД

> Получено опытным путём (несколько сессий отладки). Не документировано Directum.

---

## Как работает разрешение заголовка обложки

### Таблицы

| Таблица | Роль |
|---------|------|
| `sungero_nocode_explorermodule` | Навигационный модуль (uuid = то, что в URL `#/sat/cover/<uuid>`) |
| `sungero_nocode_moduleview` | Представление модуля (cover JSON + связь с explorermodule) |
| `sungero_settingslayer_localization` | Локализация (key=settingsid, value=JSON с заголовками) |

### Цепочка разрешения при запросе `module.cover?explorerModuleUuid=<UUID>`

```
1. explorermodule WHERE uuid = <URL_UUID>   → em.id
2. moduleview WHERE explorermodule = em.id
                AND storeasdefault = true    → mv.uuid, mv.cover (JSON)
3. localization WHERE settingsid = mv.uuid  → data (JSON)
4. Cover.Header.Title   = data["_Title_<HeaderNameGuid_noDashes>"]["ru-RU"]
5. Cover.Header.Description = data["_Description_<HeaderNameGuid_noDashes>"]["ru-RU"]
```

### Ключевой факт: формат ключей в локализации

Заголовок и описание **НЕ** берутся из полей `Name` / `Description` в JSON локализации.
Они берутся из специальных ключей на основе `Header.NameGuid` из cover JSON:

```
_Title_<HeaderNameGuid_без_дефисов>
_Description_<HeaderNameGuid_без_дефисов>
```

**Пример** — cover JSON модуля CRM:
```json
{
  "Header": {
    "NameGuid": "cb7cf944-0c05-4794-ba57-92f5dd9cf923",
    ...
  }
}
```
→ ключ в локализации: `_Title_cb7cf9440c054794ba5792f5dd9cf923`

---

## Почему системные модули показывают пустой заголовок

Для модулей где `moduleview.uuid == moduleview.modulenameguid` (аномалия в CRM-пакете):
- storeasdefault=true установлен на view с `uuid=modulenameguid`
- Локализация этой view не содержит `_Title_` ключей (только Name + Description без них)
- Сервер возвращает пустую строку

Для платформенных модулей (Компания, Делопроизводство) заголовок берётся из satellite DLL потому что в локализации есть `hash` поле — это другой путь.

---

## Правильный способ установить заголовок и описание

### Шаг 1. Найти Header.NameGuid для нужного модуля

```sql
SELECT mv.id, mv.uuid, mv.storeasdefault,
       mv.cover::json->'Header'->>'NameGuid' as header_nameguid
FROM sungero_nocode_explorermodule em
JOIN sungero_nocode_moduleview mv ON mv.explorermodule = em.id AND mv.storeasdefault = true
WHERE em.uuid = '<EXPLORER_MODULE_UUID>';
```

### Шаг 2. Убедиться что storeasdefault=true на правильной записи

Сервер должен выбирать view через `explorermodule FK + storeasdefault=true`.
Если UUID модулей перемешаны — починить:

```sql
-- Пример: убедиться что оригинальная запись является default
UPDATE sungero_nocode_moduleview SET storeasdefault = true  WHERE id = <ORIGINAL_ID>;
UPDATE sungero_nocode_moduleview SET storeasdefault = false WHERE id IN (<COPY_IDS>);
```

### Шаг 3. Добавить _Title_ и _Description_ в локализацию

```sql
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '_Title_<HeaderNameGuid_noDashes>',       jsonb_build_object('ru-RU', 'Заголовок модуля'),
  '_Description_<HeaderNameGuid_noDashes>', jsonb_build_object('ru-RU', 'Описание под заголовком')
))::citext,
lastupdate = NOW()
WHERE settingsid = '<MODULEVIEW_UUID>';
```

### Шаг 4. Перезапустить webserver (сброс кэша)

```bash
docker restart sungerowebserver_directum
```

---

## Готовые данные для CRM-модулей (VPS <YOUR_VPS_IP>)

### CRM (основной модуль)

| Поле | Значение |
|------|----------|
| explorerModuleUuid (URL) | `9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b` |
| moduleview id (default) | 15 |
| moduleview uuid | `9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b` |
| Header.NameGuid | `cb7cf944-0c05-4794-ba57-92f5dd9cf923` |
| локализации settingsid | `9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b` |
| _Title_ ключ | `_Title_cb7cf9440c054794ba5792f5dd9cf923` |
| значение заголовка | `CRM` |
| значение описания | `Продажи и маркетинг в одном модуле` |

### CRM Продажи

| Поле | Значение |
|------|----------|
| explorerModuleUuid (URL) | `35525bec-948a-48a0-92c8-cb840bb21096` |
| moduleview id (default) | 16 |
| moduleview uuid | `35525bec-948a-48a0-92c8-cb840bb21096` |
| Header.NameGuid | `bc046ca3-245e-4c23-9035-2d5949e44b9a` |
| локализации settingsid | `35525bec-948a-48a0-92c8-cb840bb21096` |
| _Title_ ключ | `_Title_bc046ca3245e4c2390352d5949e44b9a` |
| значение заголовка | `CRM Продажи` |
| значение описания | `Сделки и воронки продаж` |

### CRM Маркетинг

| Поле | Значение |
|------|----------|
| explorerModuleUuid (URL) | `889be16f-b1d8-4726-b30e-4bff6e78faa0` |
| moduleview id (default) | 17 |
| moduleview uuid | `889be16f-b1d8-4726-b30e-4bff6e78faa0` |
| Header.NameGuid | `eaf09db6-8d92-49f1-a8fb-2c3ddfd32300` |
| локализации settingsid | `889be16f-b1d8-4726-b30e-4bff6e78faa0` |
| _Title_ ключ | `_Title_eaf09db68d9249f1a8fb2c3ddfd32300` |
| значение заголовка | `CRM Маркетинг` |
| значение описания | `Лиды и маркетинговые кампании` |

---

## Полный SQL для восстановления (на случай сброса)

```sql
-- ШАГ 1: Убедиться что оригинальные views являются default
UPDATE sungero_nocode_moduleview SET storeasdefault = true  WHERE id IN (15, 16, 17);
-- Если есть копии (19, 20, 21) — они должны быть false:
UPDATE sungero_nocode_moduleview SET storeasdefault = false WHERE id IN (19, 20, 21);

-- ШАГ 2: Обновить локализацию CRM (основной)
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '_Title_cb7cf9440c054794ba5792f5dd9cf923',       jsonb_build_object('ru-RU', 'CRM'),
  '_Description_cb7cf9440c054794ba5792f5dd9cf923', jsonb_build_object('ru-RU', 'Продажи и маркетинг в одном модуле')
))::citext, lastupdate = NOW()
WHERE settingsid = '9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b';

-- ШАГ 3: Обновить локализацию CRM Продажи
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '_Title_bc046ca3245e4c2390352d5949e44b9a',       jsonb_build_object('ru-RU', 'CRM Продажи'),
  '_Description_bc046ca3245e4c2390352d5949e44b9a', jsonb_build_object('ru-RU', 'Сделки и воронки продаж')
))::citext, lastupdate = NOW()
WHERE settingsid = '35525bec-948a-48a0-92c8-cb840bb21096';

-- ШАГ 4: Обновить локализацию CRM Маркетинг
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '_Title_eaf09db68d9249f1a8fb2c3ddfd32300',       jsonb_build_object('ru-RU', 'CRM Маркетинг'),
  '_Description_eaf09db68d9249f1a8fb2c3ddfd32300', jsonb_build_object('ru-RU', 'Лиды и маркетинговые кампании')
))::citext, lastupdate = NOW()
WHERE settingsid = '889be16f-b1d8-4726-b30e-4bff6e78faa0';

-- ШАГ 5: Рестарт webserver
-- docker restart sungerowebserver_directum
```

---

## Как НЕ надо делать

1. **Не создавать копии moduleview через UI** — они получают новый UUID и становятся storeasdefault=true, ломая оригинальную запись. Оригинальные копии (id=19, 20, 21) теперь storeasdefault=false и не используются.

2. **Не менять только поле `Name` или `Description` в локализации** — это не влияет на заголовок обложки. Сервер игнорирует `Name.ru-RU` для Cover.Header.Title.

3. **Не добавлять hash в поле Name** — формула hash неизвестна, а CRM-модули не имеют ресурсных строк в satellite DLL.

4. **Не путать settingsid** — после создания копий в системе есть несколько UUID (15/19 для CRM, 16/20 для Продажи, 17/21 для Маркетинг). Всегда работать с теми у кого storeasdefault=true.

---

## Как это было обнаружено

1. Пользователь настроил копию (ID=19) через admin UI — в preview показывался заголовок
2. Сравнение локализации ID=19 (`e3dfc8ad...`) с ID=15 (`9a46d6b8...`) выявило новые ключи
3. UI при сохранении автоматически записывает `_Title_<NameGuid>` и `_Description_<NameGuid>` в JSON локализации
4. Эти ключи и есть то, что сервер читает для Cover.Header.Title/Description

---

## Диагностика

```sql
-- Проверить текущее состояние всех CRM moduleviews
SELECT mv.id, mv.storeasdefault, mv.uuid, mv.modulenameguid,
       mv.cover::json->'Header'->>'NameGuid' as header_guid,
       (sl.data::json->>'_Title_' || replace(mv.cover::json->'Header'->>'NameGuid', '-', '')) as title_key_exists
FROM sungero_nocode_moduleview mv
LEFT JOIN sungero_settingslayer_localization sl ON sl.settingsid::text = mv.uuid::text
WHERE mv.explorermodule IN (
  SELECT id FROM sungero_nocode_explorermodule
  WHERE uuid IN (
    '9a46d6b8-dcb1-41de-87f6-36ccdcdeeb1b',
    '35525bec-948a-48a0-92c8-cb840bb21096',
    '889be16f-b1d8-4726-b30e-4bff6e78faa0'
  )
)
ORDER BY mv.explorermodule, mv.id;
```
