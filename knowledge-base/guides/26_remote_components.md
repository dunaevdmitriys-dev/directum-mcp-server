# 26. Remote Components (Сторонние компоненты)

## Что это

Remote Components — сторонние UI-компоненты для веб-клиента Directum RX, загружаемые динамически через **Webpack Module Federation**. Позволяют расширять интерфейс карточек и обложек модулей кастомными React-контролами без модификации ядра платформы.

## Архитектура

```
Directum RX Web Client (Host)
  ├── Module Federation Container
  │     ├── remoteEntry.js ← собранный remote-компонент
  │     │     ├── loaders (монтирование в DOM)
  │     │     └── controls (React-компоненты)
  │     └── Shared: react, react-dom, i18next, react-i18next
  └── Host API → IRemoteComponentCardApi / IRemoteComponentCoverApi
```

**Ключевые концепции:**
- **Host** — веб-клиент Directum RX, загружает remote-компоненты
- **Remote** — ваш компонент, собранный как Module Federation remote
- **Loader** — функция, монтирующая React-компонент в DOM-элемент
- **Control** — React-компонент, реализующий UI
- **Manifest** — описание компонента, контролов и загрузчиков

## Production Reference: Targets RC (6 компонентов) + OmniApplied (Matrix)

Помимо CRM-компонентов, в workspace есть **7 production-ready RC** из решений Targets и OmniApplied. Это наиболее зрелые примеры — используй их как эталон.

| RC | Scope | Размер | Путь |
|----|-------|--------|------|
| ChartsControl | Card | 1.8 MB | `targets/source/DirRX.DirectumTargets/.../Components/ChartsControl/` |
| GoalsMap | Card+Cover | 3.3 MB | `.../GoalsMap/` |
| TableControl | Card | 3.6 MB | `.../TableControl/` |
| PeriodControl | Card | 1.2 MB | `.../PeriodControl/` |
| RichMarkdownEditor | Card | 11 MB | `.../RichMarkdownEditor/` |
| AnalyticsControl | Card | 4.7 MB | `.../AnalyticsControl/` |
| Matrix (Omni) | Application | 25 MB | `omniapplied/source/Sungero.Omni/.../Components/Matrix/` |

### metadata.json — эталоны по scope

**Card** (большинство компонентов):
```json
{ "scope": "Card", "loaders": [{ "name": "charts-control-card-loader", "scope": "Card" }] }
```

**Card+Cover** (GoalsMap — единственный пример dual scope):
```json
{
  "controls": [
    { "loaders": [{ "name": "goals-map-card-loader", "scope": "Card" }] },
    { "loaders": [{ "name": "goals-map-cover-loader", "scope": "Cover" }] }
  ]
}
```

**Application** (Matrix — единственный пример полноэкранного приложения):
```json
{ "scope": "Application" }
```

### CSS-переменные платформы

Targets RC используют ~70 CSS-переменных `--rndx-theme_*` для интеграции с темами RX:

```css
/* Основные */
--rndx-theme_background-color
--rndx-theme_text-color
--rndx-theme_primary-color
--rndx-theme_border-color

/* Night theme — автоматически через Theme.Dark в context.theme */
--rndx-theme_dark-background-color
--rndx-theme_dark-text-color
```

### Ключевые паттерны

- **GoalsMap** — единственный пример dual scope (Card+Cover). Изучи его для создания RC, работающих и на карточке, и на обложке
- **Matrix** — единственный пример Application scope (полноэкранное приложение, не привязанное к карточке/обложке)
- **RichMarkdownEditor** — самый большой Card RC (11 MB), пример сложного редактора
- **TableControl** — пример табличного RC с сортировкой, фильтрацией, виртуализацией

> **Подробный каталог:** `targets/RC_COMPONENTS_CATALOG.md`

## Структура проекта

> **Reference:** `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/`

```
{component-name}/
  src/
    controls/                     # React-компоненты
      {control-name}/
        {control-name}.tsx        # Основной компонент (логика + данные)
        {control-name}-view.tsx   # Презентационный компонент (UI)
        {control-name}-view.css   # Стили
    loaders/                      # Загрузчики (монтирование в DOM)
      {control-name}-card-loader.tsx    # Для контекста карточки
      {control-name}-cover-loader.tsx   # Для контекста обложки
  locales/                        # Файлы локализации (i18next)
    en/translation.json
    ru/translation.json
  component.manifest.js           # Манифест компонента
  component.loaders.ts            # Реестр загрузчиков
  host-api-stub.ts                # Заглушка API хоста (standalone отладка)
  host-context-stub.ts            # Заглушка контекста (standalone отладка)
  webpack.config.js               # Конфигурация Webpack + Module Federation
  tsconfig.json                   # TypeScript конфигурация
  package.json                    # Зависимости
  i18n.js                         # Инициализация i18next
  index.js                        # Точка входа
  index.html                      # HTML (standalone режим)
  public-path.js                  # Динамический publicPath
```

## Типы SDK (@directum/sungero-remote-component-types)

### Интерфейсы

| Интерфейс | Описание |
|-----------|----------|
| `IRemoteComponentContext` | Контекст хоста: userId, currentCulture, theme, tenant, logger, moduleLicenses |
| `IRemoteComponentApi` | Базовый API (Cover) |
| `IRemoteComponentCardApi` | API карточки: getEntity, executeAction, canExecuteAction, onControlUpdate |
| `IRemoteComponentCoverApi` | API обложки: getActionsMetadata, executeAction |
| `IRemoteControlLoader` | Интерфейс загрузчика |
| `ILoaderArgs` | Аргументы загрузчика: container, initialContext, api, controlInfo |
| `IRemoteControlInfo` | Информация о контроле (propertyName для Card) |
| `IEntity` | Базовая сущность: Id, DisplayValue |
| `ILogger` | Логгер: error, warning, info, debug |
| `ControlUpdateHandler` | Callback при обновлении контекста |
| `ControlCleanupCallback` | Callback при размонтировании |
| `Theme` | Enum: Default, Dark и т.д. |

### IRemoteComponentCardApi (API карточки)

```typescript
interface IRemoteComponentCardApi {
  // Получить текущую сущность карточки
  getEntity<T extends IEntity>(): T;

  // Выполнить действие карточки (Action)
  executeAction(actionName: string): Promise<void>;

  // Можно ли выполнить действие
  canExecuteAction(actionName: string): boolean;

  // Callback при обновлении контрола (вызывается хостом)
  onControlUpdate?: ControlUpdateHandler;
}
```

### IRemoteComponentCoverApi (API обложки)

```typescript
interface IRemoteComponentCoverApi {
  // Получить метаданные действий обложки
  getActionsMetadata(): IActionMetadata[];

  // Выполнить действие обложки
  executeAction(actionId: string): Promise<void>;

  // Callback при обновлении
  onControlUpdate?: ControlUpdateHandler;
}
```

### IRemoteComponentContext

```typescript
interface IRemoteComponentContext {
  userId: number;
  currentCulture: string;      // 'ru', 'en'
  tenant: string | null;
  theme: Theme;                 // Theme.Default, Theme.Dark
  clientId: string;
  logger: ILogger;
  moduleLicenses: { name: string; version: string }[];
}
```

### IEntity (сущность из карточки)

```typescript
interface IEntity {
  Id: number;
  DisplayValue: string;
  // Динамические свойства:
  [property: string]: any;
  // Доступны:
  State: { IsEnabled: boolean };
  LockInfo: { IsLocked: boolean; IsLockedByMe: boolean; IsLockedHere: boolean };
  Info: { properties: { name: string; displayValue: string }[] };
  // Метод изменения свойства:
  changeProperty(propertyName: string, newValue: any): Promise<void>;
}
```

## component.manifest.js

> **Reference:** `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/component.manifest.js`

Описывает компонент и его контролы.

```javascript
module.exports = {
  vendorName: 'Acme',                    // Код компании
  componentName: 'CRMComponents',        // Имя компонента (PascalCase)
  componentVersion: '1.0',               // Версия
  controls: [
    {
      name: 'MyControl',                 // Имя контрола
      loaders: [
        {
          name: 'my-control-card-loader',   // Имя загрузчика (kebab-case)
          scope: 'Card'                     // Контекст: 'Card' или 'Cover'
        }
      ],
      displayNames: [
        { locale: 'en', name: 'My Control' },
        { locale: 'ru', name: 'Мой контрол' }
      ]
    }
  ]
};
```

**Scope:**
- `Card` — контрол для карточки сущности (получает IRemoteComponentCardApi)
- `Cover` — контрол для обложки модуля (получает IRemoteComponentCoverApi)

## component.loaders.ts

Реестр загрузчиков — маппинг имён на реализации.

```typescript
import { IRemoteControlLoader } from '@directum/sungero-remote-component-types';
import myControlCardLoader from './src/loaders/my-control-card-loader';

const loaders: Record<string, IRemoteControlLoader> = {
  'my-control-card-loader': myControlCardLoader,
};

export default loaders;
```

## Loader (загрузчик)

Функция, монтирующая React-компонент в DOM-контейнер.

### Loader для карточки (Card)

```tsx
import * as React from 'react';
import { createRoot } from 'react-dom/client';
import { ControlCleanupCallback, ILoaderArgs, IRemoteComponentCardApi } from '@directum/sungero-remote-component-types';
import MyControl from '../controls/my-control/my-control';

export default (args: ILoaderArgs): Promise<ControlCleanupCallback> => {
  const root = createRoot(args.container);
  root.render(
    <MyControl
      initialContext={args.initialContext}
      api={args.api as IRemoteComponentCardApi}
      controlInfo={args.controlInfo}
    />
  );
  return Promise.resolve(() => root.unmount());
};
```

### Loader для обложки (Cover)

```tsx
import * as React from 'react';
import { createRoot } from 'react-dom/client';
import { ControlCleanupCallback, ILoaderArgs } from '@directum/sungero-remote-component-types';
import MyCoverControl from '../controls/my-cover-control/my-cover-control';

export default (args: ILoaderArgs): Promise<ControlCleanupCallback> => {
  const root = createRoot(args.container);
  root.render(
    <MyCoverControl initialContext={args.initialContext} api={args.api} />
  );
  return Promise.resolve(() => root.unmount());
};
```

## Control (React-компонент)

### Паттерн: контрол на карточке (Card)

```tsx
import * as React from 'react';
import { useTranslation } from 'react-i18next';
import {
  IRemoteComponentContext,
  IRemoteComponentCardApi,
  IRemoteControlInfo,
  IEntity,
  ControlUpdateHandler
} from '@directum/sungero-remote-component-types';

import '../../../i18n';
import MyControlView from './my-control-view';

interface IEntityWithProperty extends IEntity {
  [property: string]: any;
}

interface IProps {
  initialContext: IRemoteComponentContext;
  api: IRemoteComponentCardApi;
  controlInfo: IRemoteControlInfo;
}

const MyControl: React.FC<IProps> = ({ initialContext, api, controlInfo }) => {
  const propertyName = controlInfo.propertyName;

  // Получаем сущность из карточки
  const [entity, setEntity] = React.useState<IEntityWithProperty>(
    () => api.getEntity<IEntityWithProperty>()
  );

  // Контекст (культура, тема)
  const [context, setContext] = React.useState(initialContext);

  // Локализация
  const currentCulture = context.currentCulture ?? 'en';
  const { t, i18n } = useTranslation();
  React.useEffect(() => {
    i18n.changeLanguage(currentCulture);
  }, [currentCulture]);

  // Обработка обновлений от хоста
  const handleControlUpdate: ControlUpdateHandler = React.useCallback(
    (updatedContext) => {
      setEntity(api.getEntity<IEntityWithProperty>());
      setContext(updatedContext);
    },
    [api]
  );
  api.onControlUpdate = handleControlUpdate;

  // Изменение свойства сущности
  const handleChange = React.useCallback(
    async (newValue: string) => {
      await entity.changeProperty(propertyName, newValue);
    },
    [entity]
  );

  // Проверка блокировки
  const isLocked = entity.LockInfo &&
    entity.LockInfo.IsLocked &&
    (!entity.LockInfo.IsLockedByMe || !entity.LockInfo.IsLockedHere);
  const isEnabled = entity.State.IsEnabled && !isLocked;

  return (
    <MyControlView
      value={entity[propertyName]}
      onChange={handleChange}
      isEnabled={isEnabled}
      theme={context.theme}
    />
  );
};

export default MyControl;
```

### Паттерн: контрол на обложке (Cover)

```tsx
import * as React from 'react';
import { useTranslation } from 'react-i18next';
import {
  IRemoteComponentContext,
  IRemoteComponentCoverApi,
  ControlUpdateHandler
} from '@directum/sungero-remote-component-types';

import '../../../i18n';
import MyCoverView from './my-cover-view';

interface IProps {
  initialContext: IRemoteComponentContext;
  api: IRemoteComponentCoverApi;
}

const MyCoverControl: React.FC<IProps> = ({ initialContext, api }) => {
  const [context, setContext] = React.useState(initialContext);

  const currentCulture = context.currentCulture ?? 'en';
  const { i18n } = useTranslation();
  React.useEffect(() => {
    i18n.changeLanguage(currentCulture);
  }, [currentCulture]);

  const handleControlUpdate: ControlUpdateHandler = React.useCallback(
    (updatedContext) => {
      setContext(updatedContext);
    },
    [api]
  );
  api.onControlUpdate = handleControlUpdate;

  // Получение действий обложки
  const actions = api.getActionsMetadata();

  const handleAction = React.useCallback(
    async (actionId: string) => {
      await api.executeAction(actionId);
    },
    [api]
  );

  return <MyCoverView actions={actions} onAction={handleAction} />;
};

export default MyCoverControl;
```

## webpack.config.js (шаблон)

> **Reference-файл:** `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/webpack.config.js`

```javascript
const path = require('path');
const webpack = require('webpack');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const MiniCssExtractPlugin = require('mini-css-extract-plugin');
const TerserPlugin = require('terser-webpack-plugin');
const CssMinimizerPlugin = require('css-minimizer-webpack-plugin');
// ВАЖНО: в 26.1 плагин может экспортироваться по-разному — используйте fallback-паттерн:
const _pluginPkg = require('@directum/sungero-remote-component-metadata-plugin');
const SungeroRemoteComponentMetadataPlugin =
  _pluginPkg.SungeroRemoteComponentMetadataPlugin || _pluginPkg.default || _pluginPkg;

const manifest = require('./component.manifest');
const pkg = require('./package.json');

const remoteEntryName = `${manifest.vendorName}-${manifest.componentName}`;

module.exports = (env, argv) => {
  const isProduction = argv.mode === 'production';
  const isStandalone = env?.mode === 'standalone';

  return {
    entry: {
      index: './index.js',
      [remoteEntryName]: './public-path.js',
    },
    output: {
      path: path.resolve(__dirname, 'dist'),
      filename: isProduction ? '[name].[contenthash].js' : '[name].js',
      publicPath: 'auto',
      clean: true,
    },
    resolve: {
      extensions: ['.tsx', '.ts', '.jsx', '.js'],
    },
    module: {
      rules: [
        {
          test: /\.(ts|tsx|js|jsx)$/,
          exclude: /node_modules/,
          use: {
            loader: 'babel-loader',
            options: {
              presets: ['@babel/preset-react', '@babel/preset-typescript'],
            },
          },
        },
        {
          test: /\.css$/,
          use: [
            isProduction ? MiniCssExtractPlugin.loader : 'style-loader',
            'css-loader',
          ],
        },
        {
          test: /\.(png|jpg|gif|svg)$/,
          type: 'asset/resource',
        },
      ],
    },
    plugins: [
      new webpack.container.ModuleFederationPlugin({
        // name: уникальный идентификатор контейнера
        // Формат: {VendorName}_{ComponentName}_{Version_underscored}
        // Пример: 'DirRX_CRMComponents_2_0_0'
        name: 'VendorName_ComponentName_1_0_0',
        filename: 'remoteEntry.js',
        exposes: {
          // Вариант A: один loader-файл (простой компонент)
          // './loaders': './component.loaders',

          // Вариант B: отдельные loader-модули (предпочтительный в 26.1)
          './my-control-loader': './src/loaders/my-control-loader',
        },
        shared: {
          react: { singleton: true, requiredVersion: pkg.dependencies['react'] },
          'react-dom': { singleton: true, requiredVersion: pkg.dependencies['react-dom'] },
          i18next: { singleton: true, requiredVersion: pkg.dependencies['i18next'] },
          'react-i18next': { singleton: true, requiredVersion: pkg.dependencies['react-i18next'] },
        },
      }),
      // ВАЖНО: в 26.1 плагин принимает manifest как аргумент
      new SungeroRemoteComponentMetadataPlugin(manifest),
      ...(isProduction
        ? [new MiniCssExtractPlugin({ filename: '[name].[contenthash].css' })]
        : []),
      ...(isStandalone
        ? [new HtmlWebpackPlugin({ template: './index.html' })]
        : []),
    ],
    optimization: {
      minimizer: [new TerserPlugin(), new CssMinimizerPlugin()],
    },
    devServer: {
      port: 3001,
      hot: true,
      headers: {
        'Access-Control-Allow-Origin': '*',
      },
    },
    devtool: isProduction ? 'nosources-source-map' : 'eval-source-map',
  };
};
```

### Отличия CRM-реализации от шаблона

В CRM-проекте (`DirRX-CRMComponents`) применяются следующие паттерны:

```javascript
// name — конкретная версия с underscore
name: 'DirRX_CRMComponents_2_0_0',

// exposes — каждый loader как отдельный модуль (5 лоадеров)
exposes: {
  './pipeline-kanban-loader': './src/loaders/pipeline-kanban-loader',
  './funnel-chart-loader': './src/loaders/funnel-chart-loader',
  './customer360-loader': './src/loaders/customer360-loader',
  './sales-dashboard-loader': './src/loaders/sales-dashboard-loader',
  './lead-board-loader': './src/loaders/lead-board-loader',
},
```

## host-api-stub.ts (заглушка для standalone отладки)

```typescript
import { IEntity, IRemoteComponentCardApi } from '@directum/sungero-remote-component-types';

class HostStubApi implements IRemoteComponentCardApi {
  public executeAction(actionName: string): Promise<void> {
    console.log(`Action ${actionName} executed.`);
    return Promise.resolve();
  }

  public canExecuteAction(actionName: string): boolean {
    return true;
  }

  public getEntity<T extends IEntity>(): T {
    return {
      Id: 1,
      DisplayValue: 'Test Entity',
      // Здесь — тестовые данные для вашей сущности
    } as unknown as T;
  }

  public onControlUpdate?: (() => void);
}

const api: IRemoteComponentCardApi = new HostStubApi();
export default api;
```

## host-context-stub.ts (заглушка контекста)

```typescript
import { ILogger, IRemoteComponentContext, Theme } from '@directum/sungero-remote-component-types';

const context: IRemoteComponentContext = {
  userId: 1,
  currentCulture: 'ru',
  tenant: null,
  theme: Theme.Default,
  clientId: '',
  logger: {
    error(msg: Error | string, ...args: any[]) { console.error(msg, ...args); },
    warning(msg: string, ...args: any[]) { console.warn(msg, ...args); },
    info(msg: string, ...args: any[]) { console.log(msg, ...args); },
    debug(msg: string, ...args: any[]) { console.log(msg, ...args); },
  } as unknown as ILogger,
  moduleLicenses: [
    { name: 'MyModule', version: '1.0' }
  ]
};

export default context;
```

## Сборка и запуск

| Команда | Описание |
|---------|----------|
| `npm run start:dev:standalone` | Локальная отладка без хоста (standalone) |
| `npm run start:dev:remote` | Отладка в контексте хоста (remote) |
| `npm run build:release` | Production сборка → `dist/remoteEntry.js` |
| `npm run serve` | Хостинг `dist/` на порту 3001 |

## Ключевые зависимости

> **Reference:** `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/package.json`

### HostApiVersion (версия SDK для платформы 26.1)

| Пакет | Версия 26.1 | Назначение |
|-------|-------------|------------|
| `@directum/sungero-remote-component-types` | **1.0.1** | TypeScript типы и интерфейсы SDK |
| `@directum/sungero-remote-component-metadata-plugin` | **1.0.1** | Webpack-плагин для метаданных |

### Shared dependencies (обязательно singleton)

| Пакет | Версия (CRM 26.1) | Назначение |
|-------|-------------------|------------|
| `react` | **18.2.0** (exact) | UI-фреймворк (shared с хостом) |
| `react-dom` | **18.2.0** (exact) | React DOM (shared с хостом) |
| `i18next` | **^23.7.0** | Интернационализация (shared с хостом) |
| `react-i18next` | **^13.5.0** | React-биндинги i18next (shared с хостом) |

### Build toolchain (devDependencies)

| Пакет | Версия (CRM 26.1) | Назначение |
|-------|-------------------|------------|
| `webpack` | ^5.90.0 | Сборщик с Module Federation |
| `webpack-cli` | ^5.1.4 | CLI для webpack |
| `webpack-dev-server` | ^4.15.0 | Dev-сервер с HMR |
| `babel-loader` | ^9.1.0 | Транспиляция TS/JSX |
| `@babel/core` | ^7.23.0 | Babel core |
| `@babel/preset-react` | ^7.23.0 | JSX → JS |
| `@babel/preset-typescript` | ^7.23.0 | TS → JS |
| `typescript` | ^5.3.0 | TypeScript compiler |
| `css-loader` | ^6.9.0 | CSS import |
| `style-loader` | ^3.3.0 | CSS injection (dev) |
| `mini-css-extract-plugin` | ^2.7.0 | CSS extraction (prod) |
| `css-minimizer-webpack-plugin` | ^5.0.0 | CSS minification |
| `terser-webpack-plugin` | ^5.3.0 | JS minification |
| `html-webpack-plugin` | ^5.6.0 | HTML template (standalone) |
| `@types/react` | ^18.2.0 | React type defs |
| `@types/react-dom` | ^18.2.0 | ReactDOM type defs |

### Дополнительные runtime-зависимости (НЕ shared)

Эти пакеты используются в CRM, но НЕ являются shared с хостом:

| Пакет | Версия | Назначение |
|-------|--------|------------|
| `@dnd-kit/core` | ^6.1.0 | Drag-and-drop для Kanban |
| `@dnd-kit/sortable` | ^8.0.0 | Sortable списки |
| `@dnd-kit/utilities` | ^3.2.2 | DnD утилиты |

## Связь с Directum RX

### Где регистрируются remote-компоненты

Remote-компоненты подключаются через конфигурацию веб-клиента Directum RX. Собранный `remoteEntry.js` размещается на доступном URL и регистрируется в настройках системы.

### Контролы на карточках

Card-контролы привязываются к свойствам сущности через `controlInfo.propertyName`. Они могут:
- Читать значения свойств через `api.getEntity()`
- Изменять значения через `entity.changeProperty(name, value)`
- Реагировать на блокировки (`LockInfo`)
- Выполнять Actions карточки (`api.executeAction`)

### Контролы на обложках

Cover-контролы работают в контексте обложки модуля. Они могут:
- Получать метаданные действий обложки (`api.getActionsMetadata()`)
- Выполнять действия обложки (`api.executeAction(id)`)

### Локализация

Remote-компоненты используют `i18next` с файлами локализации в `locales/{locale}/translation.json`. Текущая культура приходит из `context.currentCulture`.

## tsconfig.json

> **Reference:** `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/tsconfig.json`

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "module": "ESNext",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "jsx": "react-jsx",
    "moduleResolution": "node",
    "strict": true,
    "esModuleInterop": true,
    "allowSyntheticDefaultImports": true,
    "forceConsistentCasingInFileNames": true,
    "skipLibCheck": true,
    "resolveJsonModule": true,
    "declaration": false,
    "outDir": "./dist",
    "baseUrl": ".",
    "paths": {
      "@controls/*": ["src/controls/*"],
      "@shared/*": ["src/shared/*"],
      "@loaders/*": ["src/loaders/*"]
    }
  },
  "include": ["src/**/*", "*.ts", "*.js"],
  "exclude": ["node_modules", "dist"]
}
```

**Ключевые настройки:**
- `jsx: "react-jsx"` — новый JSX-трансформ React 17+ (не нужен `import React`)
- `moduleResolution: "node"` — обязательно для webpack
- `paths` — алиасы для удобного импорта (дублируйте в `resolve.alias` webpack при необходимости)

## Изменения в 26.1 по сравнению с 25.3

| Аспект | 25.3 | 26.1 |
|--------|------|------|
| **HostApiVersion** | 1.0.0 | **1.0.1** |
| **react-i18next** | не в shared | **обязательно в shared** |
| **Metadata plugin import** | `{ SungeroRemoteComponentMetadataPlugin }` | Fallback-паттерн (default/named) |
| **Metadata plugin args** | `new Plugin()` (без аргументов) | `new Plugin(manifest)` — передаётся manifest |
| **exposes** | один `./loaders` модуль | Предпочтительно: отдельные loader-модули |
| **devServer** | не документирован | CORS-заголовки обязательны (`Access-Control-Allow-Origin: *`) |
| **react version** | 18.x (any) | **18.2.0** (exact, pinned) |

## Источник

- Репозиторий: `https://github.com/DirectumCompany/sungero-remote-component-example-react`
- npm: `@directum/sungero-remote-component-types@1.0.1`
- npm: `@directum/sungero-remote-component-metadata-plugin@1.0.1`
