---
paths:
  - "CRM/crm-mcp-server/**"
---

# CRM MCP Bridge (Node.js, 64 tools)

## Что это
MCP-мост Claude ↔ CRM API v3. HTTP :3002 или STDIO.

## Архитектура
```
server.js (JSON-RPC, MCP 2.0) → tools.js (64 handlers) → crm-client.js (JWT auto-refresh) → CRM API :5099
```

## Конвенции
- Handlers в `tools.js`: name + description + inputSchema + handler
- Ответы: `{ content: [{ type: 'text', text }] }`, ошибки: `isError: true`
- Форматтеры: `formatLead()`, `formatDeal()`, `formatActivity()`, `formatContact()`
- Деньги: RU locale (₽), даты: ISO 8601

## 64 tools по доменам
- Dashboard (3): daily_briefing, dashboard, week_plan
- Leads (8): create_lead, list_leads, get_lead, update_lead, reject_lead, convert_lead, transfer_lead, quick_lead
- Deals (11): create_deal, get_deal, update_deal, close_deal, search_deals, reassign_deal, stale_deals, overdue_deals, deal_history, deal_priorities, batch_reassign_deals
- Contacts (5): create_contact, find_contacts, company_details, create_counterparty, list_counterparties
- Activities (5): create_activity, list_activities, complete_activity, add_note, list_notes
- Analytics (13): funnel, plan_fact, sales_forecast, manager_dashboard, conversion_report, by_source_report, forecast, period_comparison, loss_reasons, weekly_report, manager_activity, competitor_analysis, whats_new
- Config (5): list_pipelines, list_lead_sources, list_loss_reasons, search_employees, available_managers
- Comms (5): save_communication, timeline, list_messages, log_call, send_message
- Advanced (9): meeting_briefing, conference_summary, schedule_reactivation, reactivation_list, escalate, lead_recommendation, batch_create_leads, batch_create_activities, global_search

## Конфигурация (.env)
CRM_API_URL (default :5099), CRM_USERNAME, CRM_PASSWORD, CRM_JWT_TOKEN, MCP_TRANSPORT (http|stdio), MCP_PORT (3002)
