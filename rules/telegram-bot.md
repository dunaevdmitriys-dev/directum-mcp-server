---
paths:
  - "CRM/telegram-bot/**"
---

# Telegram CRM Bot (Node.js v3.1)

## Что это
AI-ассистент для отдела продаж. Claude Haiku + 2 MCP-сервера (RX :3001 + CRM :3002).

## Архитектура
- `bot.js` (721 строк) — диспетчер, LLM chat loop (max 5 tool iterations), inline keyboards
- `bot-scheduler.js` (280 строк) — push каждые 15 мин: upcoming activities, overdue, new leads
- `context-store.js` (205 строк) — entity context per chat (30-мин expiry, анафоры: "он", "эта сделка")
- `users.json` — persistent auth state

## Роли и фильтрация tools
- **SDR** (18 tools): lead-focused
- **Manager** (28 tools): leads + deals + activities + notes + reports
- **Head** (null = all ~53 tools)

## Возможности
- Текст → LLM → tool_calls → CRM
- Голос: OGG → Whisper STT → chat (OPENAI_API_KEY)
- Фото: base64 → Claude Vision → extract contact → create_lead
- `/conf start EventName` → каждое сообщение = quick_lead с source
- Эскалация: "срочно" → forward всем Head-ам
- Inline keyboards: после create_lead/deal/contact/activity — контекстные кнопки

## Конфигурация (.env)
TELEGRAM_TOKEN, LLM_API_KEY, LLM_BASE_URL (<your-llm-endpoint>), LLM_MODEL (claude-haiku-4-5), OPENAI_API_KEY, MCP_RX_URL (:3001), MCP_CRM_URL (:3002), CRM_API_URL (:5099), BOT_PASSWORD, ALLOWED_CHATS
