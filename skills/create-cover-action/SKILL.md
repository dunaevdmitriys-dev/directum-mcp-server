# /create-cover-action -- Добавление действий на обложку модуля RX

> Skill для добавления CoverEntityListAction, CoverFunctionAction, CoverReportAction в Module.mtd + локализация.

---

## ШАГ 0: Реальные примеры (ОБЯЗАТЕЛЬНО прочитать перед работой)

| Что | Путь |
|-----|------|
| CoverEntityListAction (7 штук) | `{package_path}/source/{ModuleName}/{ModuleName}.Shared/Module.mtd` (найди аналог через Glob или search_metadata) |
| CoverFunctionAction (OpenSalesFunnelReport, OpenCRMApp) | `{package_path}/source/{ModuleName}/{ModuleName}.Shared/Module.mtd` (CoverFunctionAction секция) |
| Cover Groups + Tabs | `{package_path}/source/{ModuleName}/{ModuleName}.Shared/Module.mtd` (Cover Groups + Tabs секция) |
| ModuleSystem.resx (CoverAction_ / CoverGroup_ ключи) | `{package_path}/source/{ModuleName}/{ModuleName}.Shared/ModuleSystem.resx` (ключи CoverAction_ / CoverGroup_) |
| Локализация через БД (sungero_settingslayer_localization) | Гайд `knowledge-base/guides/31_cover_localization_fix.md` |
| Виджеты и обложки (общая теория) | Гайд `knowledge-base/guides/20_widgets_covers.md` |

---

## Типы Cover Actions

### 1. CoverEntityListAction -- открыть список сущностей

Самый частый тип. Открывает список сущностей по EntityTypeId (NameGuid сущности из .mtd).

```json
{
  "$type": "Sungero.Metadata.CoverEntityListActionMetadata, Sungero.Metadata",
  "NameGuid": "<NEW-GUID>",
  "Name": "ShowDeals",
  "EntityTypeId": "a7f05f7d-19a3-4733-9432-1eb0ff68b56d",
  "GroupId": "<GUID группы из Cover.Groups>",
  "PreviousItemGuid": "<GUID предыдущего action в группе, опционально>",
  "Versions": []
}
```

**Обязательные поля:**
- `NameGuid` -- уникальный GUID действия
- `Name` -- латиница, PascalCase (Show*, Open*)
- `EntityTypeId` -- NameGuid целевой сущности (из её .mtd файла)
- `GroupId` -- NameGuid группы из Cover.Groups

**Как найти EntityTypeId:**
```
MCP: extract_entity_schema entity=Deal
```
Или прочитать первую строку `NameGuid` в .mtd файле сущности.

### 2. CoverFunctionAction -- вызвать клиентскую функцию

Вызывает функцию из ModuleClientFunctions.cs. Используется для отчётов, открытия SPA, кастомной логики.

```json
{
  "$type": "Sungero.Metadata.CoverFunctionActionMetadata, Sungero.Metadata",
  "NameGuid": "<NEW-GUID>",
  "Name": "OpenSalesFunnelReport",
  "FunctionName": "OpenSalesFunnelReport",
  "GroupId": "<GUID группы>",
  "PreviousItemGuid": "<опционально>",
  "Versions": []
}
```

**Обязательные поля:**
- `FunctionName` -- имя метода в `<Module>.ClientBase/ModuleClientFunctions.cs`
- Метод должен быть `public virtual void <FunctionName>()` (без параметров)

**Пример клиентской функции:**
```csharp
// ModuleClientFunctions.cs
public virtual void OpenSalesFunnelReport()
{
  var report = Reports.GetSalesFunnelReport();
  report.Open();
}
```

### 3. CoverReportAction -- открыть отчёт (через FunctionAction)

В RX нет отдельного типа CoverReportAction. Отчёты открываются через CoverFunctionAction, где FunctionName вызывает `Reports.Get*Report().Open()`.

---

## Структура Cover в Module.mtd

```json
"Cover": {
  "NameGuid": "<COVER-GUID>",
  "Actions": [ /* массив CoverEntityListAction / CoverFunctionAction */ ],
  "Background": null,
  "Footer": {
    "NameGuid": "<FOOTER-GUID>",
    "BackgroundPosition": "Stretch"
  },
  "Groups": [
    {
      "NameGuid": "<GROUP-GUID>",
      "Name": "Deals",
      "BackgroundPosition": "Stretch",
      "TabId": "<TAB-GUID>",
      "PreviousItemGuid": "<предыдущая группа, опционально>",
      "Versions": []
    }
  ],
  "Header": {
    "NameGuid": "<HEADER-GUID>",
    "BackgroundPosition": "Stretch"
  },
  "RemoteControls": [],
  "Tabs": [
    {
      "NameGuid": "<TAB-GUID>",
      "Name": "Sales"
    },
    {
      "NameGuid": "<TAB2-GUID>",
      "Name": "Analytics",
      "PreviousItemGuid": "<TAB-GUID>"
    }
  ]
}
```

**Иерархия:** Tab > Group > Action
- Каждый Group привязан к Tab через `TabId`
- Каждый Action привязан к Group через `GroupId`
- Порядок внутри уровня определяется через `PreviousItemGuid` (связный список)

---

## Локализация Cover Actions

### В ModuleSystem.resx / ModuleSystem.ru.resx

Ключи для обложки модуля:

```xml
<!-- Действия -->
<data name="CoverAction_ShowDeals" xml:space="preserve">
  <value>Deals</value>
</data>

<!-- Группы -->
<data name="CoverGroup_Deals" xml:space="preserve">
  <value>Deals</value>
</data>

<!-- Вкладки -->
<data name="CoverTab_Sales" xml:space="preserve">
  <value>Sales</value>
</data>

<!-- Заголовок обложки -->
<data name="CoverTitle" xml:space="preserve">
  <value>CRM Sales</value>
</data>
```

**Формат ключей:**
- `CoverAction_<Name>` -- отображаемое имя действия
- `CoverGroup_<Name>` -- отображаемое имя группы
- `CoverTab_<Name>` -- отображаемое имя вкладки
- `CoverTitle` -- заголовок обложки

### В БД (sungero_settingslayer_localization) -- заголовок и описание

Заголовок обложки в веб-клиенте берётся из БД, НЕ из .resx:

```
Ключ: _Title_<HeaderNameGuid без дефисов>
Ключ: _Description_<HeaderNameGuid без дефисов>
```

Пример для CRM (Header.NameGuid = `cb7cf944-0c05-4794-ba57-92f5dd9cf923`):
```sql
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '_Title_cb7cf9440c054794ba5792f5dd9cf923',       jsonb_build_object('ru-RU', 'CRM'),
  '_Description_cb7cf9440c054794ba5792f5dd9cf923', jsonb_build_object('ru-RU', 'Описание модуля')
))::citext, lastupdate = NOW()
WHERE settingsid = '<MODULEVIEW_UUID>';
```

**Полная документация:** `knowledge-base/guides/31_cover_localization_fix.md`

---

## Алгоритм добавления Cover Action

### 1. Определить тип действия

| Нужно | Тип | Пример |
|-------|-----|--------|
| Открыть список сущностей | CoverEntityListAction | ShowDeals, OpenLeads |
| Вызвать клиентскую функцию | CoverFunctionAction | OpenSalesFunnelReport, OpenCRMApp |
| Открыть отчёт | CoverFunctionAction | OpenPlanFactReport |

### 2. Подготовить данные

- Сгенерировать новый GUID для NameGuid
- Найти EntityTypeId (для EntityListAction): `MCP: extract_entity_schema entity=<Name>`
- Определить GroupId (существующая группа или создать новую)
- Определить PreviousItemGuid (последний action в группе)

### 3. Добавить в Module.mtd

Добавить JSON-объект в массив `Cover.Actions[]`.
Если нужна новая группа -- добавить в `Cover.Groups[]`.
Если нужна новая вкладка -- добавить в `Cover.Tabs[]`.

### 4. Добавить локализацию

В `ModuleSystem.resx` (en) и `ModuleSystem.ru.resx` (ru):
```xml
<data name="CoverAction_<ActionName>" xml:space="preserve">
  <value>Отображаемое имя</value>
</data>
```

### 5. Для CoverFunctionAction -- создать клиентскую функцию

В `<Module>.ClientBase/ModuleClientFunctions.cs`:
```csharp
public virtual void <FunctionName>()
{
  // Логика
}
```

### 6. Валидация

```
MCP: validate_all
MCP: fix_cover_localization
```

---

## MCP Tools

| Tool | Когда использовать |
|------|--------------------|
| `scaffold_cover_action` | Генерация каркаса Cover Action с GUID |
| `fix_cover_localization` | Проверка и исправление локализации обложки |
| `validate_all` | Полная валидация решения |
| `search_metadata name=Cover` | Найти существующие обложки |
| `extract_entity_schema entity=<Name>` | Получить NameGuid сущности для EntityTypeId |

---

## Чеклист

- [ ] Прочитан реальный пример из Module.mtd (ШАГ 0)
- [ ] GUID сгенерирован (uuidgen)
- [ ] EntityTypeId корректен (для EntityListAction)
- [ ] GroupId указывает на существующую группу
- [ ] PreviousItemGuid корректен (или отсутствует для первого в группе)
- [ ] CoverAction_ ключ добавлен в ModuleSystem.resx
- [ ] CoverAction_ ключ добавлен в ModuleSystem.ru.resx
- [ ] Для FunctionAction создана клиентская функция
- [ ] `validate_all` пройден
