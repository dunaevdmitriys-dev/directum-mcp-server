---
paths:
  - "CRM/CrmApiV3/**"
---

# CRM API (.NET 8 Minimal APIs, v16.0)

## Архитектура
- **Minimal APIs** (НЕ Controllers) — 37 endpoint-файлов в `Endpoints/`, 190+ routes
- **Сервисы** в `Services/` (16 файлов): DbService, ODataService, AuthService, PipelineService, EmailSyncService, TelegramBotService, FollowUpService, RabbitMqService, QueueConsumerService, ...
- **DTO** в `Models.cs` (~500 записей), валидаторы в `Validators.cs` (25+ FluentValidation)
- **Middleware** в `Middleware.cs`: ApiVersionMiddleware, CorrelationIdMiddleware

## Правила разработки
- Auth: token в localStorage (Bearer header), НЕ JWT в Zustand memory
- SSO endpoint: `GET /api/auth/sso?user=LoginName` — аутентификация по имени пользователя RX
- OData: все ответы CallWebApiGetAsync/PostAsync автоматически разворачиваются через `UnwrapODataValue()` (убирает обёртку `{ value: [...] }`)
- client.ts: Axios с Bearer token из localStorage, auto-attach на каждый запрос
- OData к RX: ВСЕГДА через Polly (retry 3x exponential + circuit breaker 50%/15s)
- SQL: ТОЛЬКО NpgsqlParameter, никаких конкатенаций
- Rate limiting: 30/min auth, 600/min API
- Output cache: Short 10s, Medium 30s, Long 5min
- API versioning: header `Api-Version: 3`
- Логи: Serilog + CorrelationId (X-Correlation-ID)
- Новый endpoint → добавить FluentValidation + `.WithValidation<T>()`

## Кастомные PostgreSQL-таблицы
crm_refresh_tokens, crm_deal_history, crm_proposals, crm_messages, crm_call_logs, crm_automations, crm_email_sync, crm_settings

## Сервисы-синглтоны
DbService (Npgsql pool 5-100), AuthService, RabbitMqService, TelegramBotService, EmailSyncService, FollowUpService

## Background Services (IHostedService)
QueueConsumerService (RabbitMQ), EmailSyncService (MailKit IMAP polling), FollowUpService (reminders)

## Тесты
`dotnet test CrmApi.Tests/CrmApi.Tests.csproj` — xUnit + Moq + FluentAssertions (267 тестов)
- Validators/ (11 suites), Services/ (8 suites), Middleware/, Security/

## Known Bugs (аудит 2026-03-27, CRM MCP Server)
> 5 critical bugs в crm-mcp-server tools. Если используешь эти tools — учитывай.

1. **stale_deals / overdue_deals** — нет pipelineId в запросе → каскад на 6 tools (funnel, conversion, forecast и др.)
2. **dashboard** — GET vs POST mismatch (tool шлёт GET, API ожидает POST)
3. **search_deals** — `formatDeal()` вызывается на `AutocompleteItemDto` (разная структура)
4. **close_deal** — `r.id` vs `dealId` (результат не содержит .id, нужен переданный dealId)
5. **reject_lead** — поле `note` отсутствует в DTO (сервер игнорирует)

**Покрытие CRM MCP:** 61% работают, 22% partial, 9% broken, 8% coverage gaps (20+ API endpoints без tools)

## Интеграции (опциональные, graceful degradation)
- RabbitMQ: 4 очереди (automation, notifications, email, activities) — inline fallback
- MailKit: IMAP/SMTP, SyncIntervalMinutes=5
- Telegram.Bot v21.3: webhook `/api/crm/telegram/webhook` (без JWT, secret token)
- Prometheus: `/metrics` (требует auth)
