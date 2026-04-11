# Scripts -- автоустановка

Скрипты для автоматической настройки рабочего пространства Directum MCP Server с нуля.

## setup.sh (macOS / Linux)

```bash
# Вариант 1: one-liner
curl -fsSL https://raw.githubusercontent.com/dunaevdmitriys-dev/directum-mcp-server/master/scripts/setup.sh | bash

# Вариант 2: с указанием папки
bash setup.sh /путь/к/рабочей/папке
```

По умолчанию устанавливает в `~/directum-workspace`.

### Поддерживаемые ОС

- **macOS** -- зависимости через Homebrew (устанавливает автоматически)
- **Linux** (Ubuntu/Debian) -- зависимости через apt + NodeSource

## setup.ps1 (Windows)

```powershell
# Запуск
powershell -ExecutionPolicy Bypass -File setup.ps1

# С указанием папки
powershell -ExecutionPolicy Bypass -File setup.ps1 C:\directum-workspace
```

По умолчанию устанавливает в `%USERPROFILE%\directum-workspace`.

Зависимости устанавливаются через **winget** (если доступен), иначе скрипт выводит ссылки для ручной установки.

## Что делает скрипт

1. **Проверяет зависимости** и устанавливает недостающее:
   - Git
   - Node.js (20+ для Windows, любая версия для macOS/Linux)
   - .NET 10 SDK
   - Claude Code (`npm install -g @anthropic-ai/claude-code`)
2. **Клонирует репозиторий** `directum-mcp-server` в `.mcp-server/`
3. **Собирает MCP-серверы** (`dotnet build --verbosity quiet`)
4. **Настраивает рабочее пространство:**
   - Генерирует `.mcp.json` с 4 серверами (scaffold, validate, analyze, deploy)
   - Копирует `CLAUDE-TEMPLATE.md` в `CLAUDE.md`
   - Копирует `skills/`, `agents/`, `hooks/`, `rules/` в `.claude/`
   - Копирует `knowledge-base/` в корень проекта

## Структура после установки

```
~/directum-workspace/
├── .mcp-server/              -- клонированный репозиторий MCP-сервера
├── .claude/
│   ├── skills/               -- навыки Claude Code
│   ├── agents/               -- агенты
│   ├── hooks/                -- хуки автовалидации
│   └── rules/                -- правила контекста
├── knowledge-base/           -- 38 гайдов по Directum RX
├── .mcp.json                 -- конфигурация 4 MCP-серверов
└── CLAUDE.md                 -- инструкции проекта
```

## Что дальше

После установки:

```bash
cd ~/directum-workspace
claude
```

Первая команда в Claude Code для проверки:

```
Найди сущность Employee через search_metadata
```
