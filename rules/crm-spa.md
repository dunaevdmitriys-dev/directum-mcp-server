---
paths:
  - "CRM/crm-spa/**"
---

# CRM SPA (React 18, Vite 5.1, v13.0)

## Правила
- State: **Zustand** (8 stores) — НЕ Redux, НЕ Context для серверных данных
- Routing: **HashRouter** — НЕ BrowserRouter (деплой в /Client/content/crm/)
- UI: **Ant Design 5** (tokens, ConfigProvider, algorithm) — НЕ Tailwind, НЕ CSS modules
- API: Axios в `src/api/client.ts` — Bearer token из localStorage + retry 3x (GET only) на 502/503/504
- i18n: **i18next** (ru/en, ~400 ключей) — все UI-строки через `t()`
- Темы: ThemeProvider (dark/light) через `antTheme.darkAlgorithm`
- Charts: **recharts**

## Структура src/
- `pages/` — 32 lazy-loaded страницы (Dashboard, Deals, Leads, Analytics, Marketing, Settings, ...)
- `stores/` — 8 Zustand: pipeline, lead, activity, analytics, settings, communication, marketing, apiStatus
- `api/` — 22 endpoint-модуля + `client.ts` (Axios) + `types.ts` (83 интерфейса)
- `components/` — shared: ThemeProvider, ErrorBoundary, GlobalSearch, NotificationBell, Customer360Panel, ...
- `controls/` — 5 Remote Components (Webpack Module Federation → remoteEntry.js)
- `i18n/` — ru.json, en.json

## Remote Components (для RX)
PipelineKanban (Cover), LeadBoard (Cover), SalesDashboard (Cover), FunnelChart (Cover), Customer360 (Card)
Loader: `(args: ILoaderArgs) => { createRoot(args.container).render(<C />); return cleanup; }`

## Auth-модель
- SSO: параметр `?user=LoginName` в URL → `GET /api/auth/sso?user=LoginName` → token сохраняется в localStorage
- Token persistence: `localStorage.getItem('token')` — НЕ JWT в Zustand memory
- F5 работает: `detectUser()` читает token из localStorage → `GET /auth/me` → восстанавливает сессию
- LoginPage: fallback для прямого доступа без token, редирект на RX если токена нет
- Deeplinks: «Открыть в RX» — клиентская генерация URL `/Client/#/card/{typeGuid}/{id}`

## Конфигурация
- `config.ts`: API_BASE_URL=/api/crm, ODATA_URL=/Integration/odata, LOCALE=ru, VERSION=15.0.0
- `window.__CRM_CONFIG__` — runtime override при встраивании в RX
- `vite.config.ts`: proxy /api/→:5099, /Integration/→:5099, base=/Client/content/crm/ (prod)
- Code splitting: vendor (react), antd, charts

## Сборка
- Dev: `npm run dev` → :3000
- Build: `npm run build` → dist/ → /Client/content/crm/
