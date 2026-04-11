# Rules -- правила для Claude Code

Rules автоматически загружаются Claude Code при работе в проекте и задают контекст: конвенции, запреты, ссылки на reference-код. Файлы размещаются в `.claude/rules/` и подхватываются без дополнительной настройки.

## Установка

```bash
cp -r rules/ /ваш-проект/.claude/rules/
```

Claude Code подхватит все `.md` файлы из этого каталога автоматически.

## Файлы

| Файл | Описание |
|------|----------|
| `mcp-devtools.md` | Архитектура 5 MCP-серверов (86 tools), конвенции, env vars, known issues v2 |
| `crm-dds.md` | DDS-разработка Directum RX 26.1: приоритет reference, known issues, CRM-модули и зависимости |
| `crm-api.md` | CRM API (.NET 8, 190+ routes): JWT, Polly, SQL-безопасность, PostgreSQL-таблицы, тесты |
| `crm-spa.md` | CRM SPA (React 18, Vite 5.1): Zustand, HashRouter, Ant Design 5, Remote Components |
| `crm-mcp-bridge.md` | CRM MCP Bridge (Node.js, 64 tools): JSON-RPC мост Claude <-> CRM API v3 |
| `telegram-bot.md` | Telegram CRM Bot (Node.js): AI-ассистент отдела продаж, Claude Haiku + 2 MCP-сервера |
| `dds-examples-map.md` | Карта примеров DDS: где искать эталонные .mtd, .resx, .cs, RC, отчеты, плагины |
| `knowledge-base.md` | Индекс 38 гайдов по Directum RX: от архитектуры до plugin development |
| `deploy.md` | Docker-инфраструктура: два compose-файла, credentials, volumes, конфигурация RX |

## Фильтрация по путям (frontmatter)

Каждый rule-файл содержит YAML frontmatter с `paths:`, определяющий, когда правило активно:

```yaml
---
paths:
  - "CRM/crm-package/**"
  - "**/source/**/*.mtd"
---
```

Claude Code загружает правило только при работе с файлами, совпадающими с указанными glob-паттернами.

## Как добавить своё правило

Создайте `.md` файл в `.claude/rules/` -- Claude Code подхватит автоматически. Рекомендуется добавить frontmatter с `paths:` для ограничения области действия.

Пример:

```markdown
---
paths:
  - "MyModule/**"
---

# Правила для MyModule

- Все сущности наследуют от DatabookEntry
- Enum values не должны совпадать с C# reserved words
```
