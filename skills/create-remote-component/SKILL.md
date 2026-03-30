---
description: "Создать Remote Component (сторонний React-контрол) для веб-клиента Directum RX"
user_invocable: true
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание Remote Component для Directum RX

## ШАГ 0: Посмотри рабочий пример

### Приоритет reference-кода

| Приоритет | Источник | Что брать | Путь |
|-----------|----------|-----------|------|
| **1 (эталон)** | Targets RC (6 компонентов) | metadata.json, chunk splitting, CSS-переменные, scope-паттерны | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/` |
| **2 (эталон)** | OmniApplied RC (1 компонент) | Application scope, displayNames (i18n), hostApiVersion 1.0.1 | `omniapplied/source/Sungero.Omni/Sungero.Omni.Components/Matrix/` |
| **3 (рабочий)** | CRM RC (5 контролов) | Loader-паттерны (tsx), webpack.config.js, component.manifest.js | `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/` |

> **CRM RC — рабочий проект с багами, НЕ эталон.** Бери оттуда только структуру loader/webpack. Для metadata.json, CSS, chunk splitting — смотри Targets.

### Reference: Production RC (Targets — 6 компонентов, OmniApplied — 1)

| RC | Scope | Размер | Ключевая особенность | metadata.json |
|----|-------|--------|---------------------|---------------|
| **ChartsControl** | Card | 1.8 MB | Canvas-графики, minimap | `targets/.../ChartsControl/metadata.json` |
| **GoalsMap** | Card + Cover | 3.3 MB | **Единственный** dual-scope RC | `targets/.../GoalsMap/metadata.json` |
| **TableControl** | Card | 3.6 MB | Иерархическая таблица, 29 SVG | `targets/.../TableControl/metadata.json` |
| **PeriodControl** | Card | 1.2 MB | Самый компактный RC | `targets/.../PeriodControl/metadata.json` |
| **RichMarkdownEditor** | Card | 11 MB | WYSIWYG, 40 chunks | `targets/.../RichMarkdownEditor/metadata.json` |
| **AnalyticsControl** | Card | 4.7 MB | Без moment.js | `targets/.../AnalyticsControl/metadata.json` |
| **Matrix (Omni)** | Application | 25 MB | Полноэкранное приложение, WASM | `omniapplied/.../Matrix/metadata.json` |

Полный каталог с деталями: `targets/RC_COMPONENTS_CATALOG.md`

### CRM RC (рабочий проект)

| Файл | Путь (от `CRM/crm-package/source/`) |
|------|------|
| **component.manifest.js** | `DirRX.CRM/DirRX-CRMComponents/component.manifest.js` |
| **webpack.config.js** | `DirRX.CRM/DirRX-CRMComponents/webpack.config.js` |
| **component.loaders.ts** | `DirRX.CRM/DirRX-CRMComponents/component.loaders.ts` |
| **package.json** | `DirRX.CRM/DirRX-CRMComponents/package.json` |
| **Cover Loader** | `DirRX.CRM/DirRX-CRMComponents/src/loaders/pipeline-kanban-loader.tsx` |
| **Card Loader** | `DirRX.CRM/DirRX-CRMComponents/src/loaders/customer360-loader.tsx` |
| **React-компонент** | `DirRX.CRM/DirRX-CRMComponents/src/controls/pipeline-kanban/PipelineKanban.tsx` |

Перед созданием нового RC — **обязательно прочитай** metadata.json из Targets + loader-файлы из CRM и адаптируй.

## metadata.json — формат (из production RC)

### Card scope (ChartsControl — типовой)
```json
{
  "vendorName": "Directum",
  "componentName": "ChartsControl",
  "componentVersion": "1.1",
  "controls": [
    {
      "name": "CardIntegrationService",
      "loaders": [
        {
          "name": "card-integration-service-chart-loader",
          "scope": "Card"
        }
      ]
    }
  ],
  "publicName": "Directum_ChartsControl_1_1",
  "hostApiVersion": "1.0.0"
}
```

### Card + Cover dual scope (GoalsMap — единственный пример!)
```json
{
  "vendorName": "Directum",
  "componentName": "GoalsMap-Component",
  "componentVersion": "1.1",
  "controls": [
    {
      "name": "GoalsMap",
      "loaders": [
        { "name": "GoalsMap-card-loader", "scope": "Card" },
        { "name": "GoalsMap-cover-loader", "scope": "Cover" }
      ]
    }
  ],
  "publicName": "Directum_GoalsMap_Component_1_1",
  "hostApiVersion": "1.0.0"
}
```

### Application scope (Matrix — полноэкранное приложение)
```json
{
  "vendorName": "Directum",
  "componentName": "OmniComponent",
  "componentVersion": "1.0",
  "controls": [
    {
      "name": "OmniApplication",
      "loaders": [
        { "name": "omni-application-loader", "scope": "Application" }
      ],
      "displayNames": [
        { "locale": "en", "name": "Omni messenger" },
        { "locale": "ru", "name": "Omni мессенджер" }
      ]
    }
  ],
  "publicName": "Directum_OmniComponent_1_0",
  "hostApiVersion": "1.0.1"
}
```

### Правила metadata.json

| Поле | Формат | Обязательное |
|------|--------|-------------|
| `vendorName` | Всегда `"Directum"` (или код компании) | Да |
| `componentName` | PascalCase, допустимы дефисы | Да |
| `componentVersion` | semver-like: `"1.0"`, `"1.1"` | Да |
| `publicName` | `{vendor}_{componentName}_{version}` (точки и дефисы -> подчёркивания) | Да |
| `hostApiVersion` | `"1.0.0"` (Targets, стандарт) или `"1.0.1"` (OmniApplied, расширенный) | Да |
| `controls[].name` | PascalCase имя контрола | Да |
| `controls[].loaders[].name` | kebab-case идентификатор загрузчика | Да |
| `controls[].loaders[].scope` | `"Card"` / `"Cover"` / `"Application"` | Да |
| `controls[].displayNames` | Массив `{locale, name}` — для Application scope (i18n в навигации) | Нет |

### hostApiVersion

| Версия | Где используется | Отличие |
|--------|-----------------|---------|
| `"1.0.0"` | Targets (все 6 RC) | Стандартный API хоста — Card/Cover scope |
| `"1.0.1"` | OmniApplied Matrix | Расширенный API — Application scope, displayNames |

Используй `"1.0.0"` для Card/Cover. `"1.0.1"` — только если нужен Application scope или расширенные возможности хоста.

## Входные данные
Спроси у пользователя (если не указано):
- **VendorName** — код компании (например, `Acme`)
- **ComponentName** — имя компонента (PascalCase, например, `CRMComponents`)
- **Controls** — список контролов: имя, scope (Card/Cover/Application), описание
- **ProjectPath** — путь к проекту (если привязан к модулю RX)

## Алгоритм

### 1. Определи структуру
Для каждого контрола:
- Имя (PascalCase): `MyControl`
- Имя загрузчика (kebab-case): `my-control-card-loader`
- Scope: `Card`, `Cover` или `Application`
- Данные (для Card: какие свойства сущности)

### 2. Создай файловую структуру

Путь: `{ProjectPath}/remote-components/{VendorName}-{ComponentName}/`

```
src/
  controls/
    {control-name}/
      {control-name}.tsx          # Основной компонент (логика)
      {control-name}-view.tsx     # Презентационный (UI)
      {control-name}-view.css     # Стили
  loaders/
    {control-name}-{scope}-loader.tsx
locales/
  en/translation.json
  ru/translation.json
metadata.json
component.manifest.js
component.loaders.ts
host-api-stub.ts
host-context-stub.ts
webpack.config.js
tsconfig.json
package.json
i18n.js
index.js
index.html
public-path.js
.gitignore
```

### 3. Генерируй файлы по шаблонам

**Порядок:**
1. `metadata.json` — описание компонента (формат см. выше, бери пример из Targets)
2. `package.json` — зависимости (react 18.2.0, @directum/sungero-remote-component-types 1.0.1)
3. `tsconfig.json`, `webpack.config.js` — конфигурация сборки
4. `component.manifest.js` — описание контролов
5. `component.loaders.ts` — реестр загрузчиков
6. `i18n.js`, `public-path.js`, `index.js` — инфраструктура
7. Для каждого контрола:
   - `src/loaders/{name}-{scope}-loader.tsx`
   - `src/controls/{name}/{name}.tsx`
   - `src/controls/{name}/{name}-view.tsx`
   - `src/controls/{name}/{name}-view.css`
8. `host-api-stub.ts`, `host-context-stub.ts` — заглушки для отладки
9. `locales/en/translation.json`, `locales/ru/translation.json`
10. `index.html` — для standalone режима
11. `.gitignore`

### 4. Card-контрол (из customer360-loader.tsx)

**Loader** (реальный пример — `customer360-loader.tsx`):
```tsx
// CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/src/loaders/customer360-loader.tsx
import * as React from 'react';
import { createRoot } from 'react-dom/client';
import {
  ControlCleanupCallback,
  ILoaderArgs,
  IRemoteComponentCardApi,
} from '@directum/sungero-remote-component-types';
import Customer360 from '../controls/customer360/Customer360';

export default (args: ILoaderArgs): Promise<ControlCleanupCallback> => {
  const root = createRoot(args.container);
  root.render(
    <Customer360
      initialContext={args.initialContext}
      api={args.api as IRemoteComponentCardApi}
      controlInfo={args.controlInfo}
    />
  );
  return Promise.resolve(() => root.unmount());
};
```

**Control** (реальный паттерн из `PipelineKanban.tsx` — применим к Card):
```tsx
import * as React from 'react';
import { useTranslation } from 'react-i18next';
import {
  IRemoteComponentContext,
  IRemoteComponentCardApi,
  IRemoteControlInfo,
  ControlUpdateHandler,
} from '@directum/sungero-remote-component-types';
import '../../../i18n';

interface IProps {
  initialContext: IRemoteComponentContext;
  api: IRemoteComponentCardApi;
  controlInfo: IRemoteControlInfo;
}

const MyCardControl: React.FC<IProps> = ({ initialContext, api, controlInfo }) => {
  const [context, setContext] = React.useState(initialContext);

  // i18n — точно как в PipelineKanban.tsx
  const { t, i18n } = useTranslation();
  React.useEffect(() => {
    i18n.changeLanguage(context.currentCulture ?? 'en');
  }, [context.currentCulture]);

  // Обработка обновлений от хоста
  const handleControlUpdate: ControlUpdateHandler = React.useCallback(
    (updatedContext) => { setContext(updatedContext); }, []
  );
  api.onControlUpdate = handleControlUpdate;

  return <div>{/* UI */}</div>;
};

export default MyCardControl;
```

### 5. Cover-контрол (из pipeline-kanban-loader.tsx)

**Loader** (реальный пример — `pipeline-kanban-loader.tsx`):
```tsx
// CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/src/loaders/pipeline-kanban-loader.tsx
import * as React from 'react';
import { createRoot } from 'react-dom/client';
import {
  ControlCleanupCallback,
  ILoaderArgs,
  IRemoteComponentCoverApi,
} from '@directum/sungero-remote-component-types';
import PipelineKanban from '../controls/pipeline-kanban/PipelineKanban';

export default (args: ILoaderArgs): Promise<ControlCleanupCallback> => {
  const root = createRoot(args.container);
  root.render(
    <PipelineKanban
      initialContext={args.initialContext}
      api={args.api as IRemoteComponentCoverApi}
    />
  );
  return Promise.resolve(() => root.unmount());
};
```

**Ключевое отличие Cover от Card:** Cover-loader использует `IRemoteComponentCoverApi` и НЕ передаёт `controlInfo`.

### 6. Dual-scope контрол (GoalsMap паттерн)

Один контрол с **двумя** loader'ами — для карточки и обложки:

```typescript
// component.loaders.ts — регистрация двух loader'ов
import goalsMapCardLoader from './src/loaders/goalsmap-card-loader';
import goalsMapCoverLoader from './src/loaders/goalsmap-cover-loader';

export default {
  'GoalsMap-card-loader': goalsMapCardLoader,
  'GoalsMap-cover-loader': goalsMapCoverLoader,
};
```

Компонент адаптирует поведение через prop `scope`:
```tsx
const GoalsMap: React.FC<IProps> = ({ scope, initialContext, api }) => {
  if (scope === 'Card') {
    // Режим карточки: читаем свойства сущности
    const cardApi = api as IRemoteComponentCardApi;
    const entityId = cardApi.controlInfo?.entityId;
  } else {
    // Режим обложки: показываем сводку по модулю
    const coverApi = api as IRemoteComponentCoverApi;
  }
};
```

### 7. Application-scope контрол (Matrix паттерн)

Полноэкранное приложение внутри RX. Отличия:
- `hostApiVersion: "1.0.1"` (обязательно)
- `displayNames` в metadata.json (для навигации RX)
- Нет `controlInfo` — приложение управляет своим routing
- Полный доступ к window, может использовать WASM

### 8. Scope-specific API

| | Card | Cover | Application |
|--|------|-------|-------------|
| **API интерфейс** | `IRemoteComponentCardApi` | `IRemoteComponentCoverApi` | Полный window |
| **controlInfo** | Да (entityId, properties) | Нет | Нет |
| **context** | Сущность | Модуль | Приложение |
| **hostApiVersion** | `"1.0.0"` | `"1.0.0"` | `"1.0.1"` |
| **displayNames** | Нет | Нет | Обязательно |
| **Когда** | Контрол на карточке | Контрол на обложке | Отдельное приложение |

## CSS-переменные — дизайн-система RNDX

### ПРАВИЛЬНО vs НЕПРАВИЛЬНО
```css
/* ✅ ПРАВИЛЬНО: платформенные переменные */
color: var(--rndx-theme_text-primary);
background: var(--rndx-theme_background-body);
border: 1px solid var(--rndx-theme_border-light);

/* ❌ НЕПРАВИЛЬНО: кастомные переменные (сломаются при смене темы) */
color: var(--crm-text-primary);
background: var(--my-bg-color);
```

> Кастомные CSS-переменные допустимы ТОЛЬКО для значений, которых нет в `--rndx-theme_*` (например, специфичные для компонента размеры). Для цветов, фонов, границ, теней — ВСЕГДА платформенные.

Все production RC (Targets, OmniApplied) используют единую систему CSS-переменных `--rndx-theme_*`. **Обязательно** используй их вместо хардкода цветов — иначе сломается night theme.

### Основные переменные (~70 шт)
```css
/* === Текст === */
--rndx-theme_text-primary: #121416;
--rndx-theme_text-inverse: #ffffff;
--rndx-theme_text-header: #13406d;
--rndx-theme_text-disabled: #8b8b8b;
--rndx-theme_text-link: #0063bd;
--rndx-theme_text-placeholder: #8b8b8b;

/* === Фон === */
--rndx-theme_background-body: #ffffff;
--rndx-theme_background-dark: #13406d;
--rndx-theme_background-popup: var(--rndx-theme_background-body);
--rndx-theme_kanban-column-background-color: #fbfbfb;
--rndx-theme_ribbon-background: #f8f8f8;

/* === Границы === */
--rndx-theme_border-light: #e6e6e6;
--rndx-theme_border-dark: #c2c2c2;
--rndx-theme_border-color-dark: #0054a0;
--rndx-theme_border-color-tertiary: #e6e6e6;

/* === Таблицы/гриды === */
--rndx-theme_grid-background-selected: #dde8f2;
--rndx-theme_grid-background-hover: #cfdfed;
--rndx-theme_table-header-text: #727272;

/* === Тени === */
--rndx-theme_base_shadow: 0 2px 3px 0 rgba(0,0,0,0.05), 0 5px 10px rgba(0,0,0,0.2);
--rndx-theme_second_shadow: 0 1px 3px 0 rgba(0,0,0,0.05), 0 2px 6px rgba(0,0,0,0.1);

/* === Скроллбар === */
--rndx-theme_scrollbar-thumb-background: #c2c2c2;
--rndx-theme_scrollbar-track-background: rgba(0,0,0,0.03);

/* === Шрифты === */
--rndx-theme_text-font: "Segoe UI", -apple-system, "BlinkMacSystemFont", "Roboto", ...;
--rndx-theme_monotext-font: "Segoe UI Mono", "SFMono-Regular", consolas, ...;
```

### Night theme
Переключатель: `html.night-theme` или `body[theme=night]`. Значения переменных автоматически меняются — если ты используешь `var(--rndx-theme_*)`, ночная тема работает бесплатно.

```css
/* Пример: стиль совместимый с night theme */
.my-control {
  color: var(--rndx-theme_text-primary);
  background: var(--rndx-theme_background-body);
  border: 1px solid var(--rndx-theme_border-light);
}
```

### CSS-модули (паттерн именования классов)
Формат: `{Component}-module__{element}__{Package}_{RC}__{hash}`
Пример: `ChartTooltip-module__tooltipContainer__Targets_ChartsControl__YdBm8`

## Chunk splitting — паттерн из Targets

### Shared dependencies → отдельные chunks
В production RC (Targets) shared зависимости (react, react-dom, moment) выносятся в отдельные chunks через Module Federation shared scope:

| Chunk | Размер | Содержимое | В каких RC |
|-------|--------|-----------|------------|
| **294** | ~6.5 KB | `react` shared module | Все 6 Targets |
| **935** | ~133 KB | `react-dom` shared module | Все 6 Targets |
| **208** | ~299 KB | `moment` 2.30.1 | 5 из 6 (кроме Analytics) |
| **762** | ~294 KB | `moment` 2.29.1 | 5 из 6 (кроме Analytics) |

### Стратегия splitting в webpack.config.js
- **Shared dependencies** (react, react-dom, moment) — в отдельных chunks, дедуплицируются через Module Federation shared scope
- **Per-component chunks** — уникальная логика компонента
- **CSS chunks** — привязаны к JS-чанкам по ID (напр. CSS `338.895e0f15.css` загружается с JS chunk 338)
- **Именование:** `{chunkId}_{version}_{hash}.js` / `css/{chunkId}.{hash}.css`

### Рекомендации по splitting
- `react@18.2.0` и `react-dom@18.2.0` — **обязательно** shared (совпадает с хостом RX)
- `moment` — опционален (AnalyticsControl обходится без него)
- Целевой размер RC: 1-3 MB (диапазон PeriodControl — GoalsMap)

## Валидация
- [ ] `metadata.json` — формат соответствует Targets-эталону (vendorName, publicName, hostApiVersion)
- [ ] `component.manifest.js` описывает все контролы
- [ ] Каждый контрол имеет loader в `component.loaders.ts`
- [ ] Loader возвращает cleanup callback (`root.unmount()`)
- [ ] `host-api-stub.ts` содержит тестовые данные
- [ ] react/react-dom = 18.2.0 (совпадает с хостом)
- [ ] i18next = shared singleton в webpack.config.js
- [ ] TypeScript: `createRoot` (React 18), не `ReactDOM.render`
- [ ] Локализация: `en` + `ru` для всех строк UI
- [ ] `.gitignore` включает `node_modules/` и `dist/`
- [ ] CSS использует `var(--rndx-theme_*)` вместо хардкода цветов
- [ ] Night theme работает (`html.night-theme`)
- [ ] Shared deps (react, react-dom) в отдельных chunks

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- Полный каталог RC с деталями: `targets/RC_COMPONENTS_CATALOG.md`
- После создания артефакта: `/validate-all`
