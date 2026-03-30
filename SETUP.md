# Установка за 5 минут

## Шаг 1. Установить зависимости

```bash
# Node.js
brew install node          # Mac
# или скачать с https://nodejs.org

# .NET 10 SDK
brew install dotnet-sdk    # Mac
# или скачать с https://dotnet.microsoft.com

# Claude Code
npm install -g @anthropic-ai/claude-code
```

> Нужна подписка Claude Pro ($20/мес) или Max ($100/мес) на [claude.ai](https://claude.ai)

## Шаг 2. Скачать и собрать

```bash
mkdir ~/directum-workspace && cd ~/directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server && dotnet build && cd ..
```

## Шаг 3. Запустить Claude Code и вставить промпт

```bash
cd ~/directum-workspace
claude
```

Вставьте этот промпт:

---

```
Настрой рабочее пространство для разработки Directum RX. Выполни по шагам:

1. Скопируй файл .mcp-server/CLAUDE-TEMPLATE.md в ./CLAUDE.md

2. Создай файл .mcp.json в текущей папке:
{
  "mcpServers": {
    "directum-scaffold": {
      "command": "dotnet",
      "args": ["run", "--project", ".mcp-server/src/DirectumMcp.Scaffold"],
      "env": { "SOLUTION_PATH": "<ТЕКУЩАЯ_ПАПКА>" }
    },
    "directum-validate": {
      "command": "dotnet",
      "args": ["run", "--project", ".mcp-server/src/DirectumMcp.Validate"],
      "env": { "SOLUTION_PATH": "<ТЕКУЩАЯ_ПАПКА>" }
    },
    "directum-analyze": {
      "command": "dotnet",
      "args": ["run", "--project", ".mcp-server/src/DirectumMcp.Analyze"],
      "env": { "SOLUTION_PATH": "<ТЕКУЩАЯ_ПАПКА>" }
    },
    "directum-deploy": {
      "command": "dotnet",
      "args": ["run", "--project", ".mcp-server/src/DirectumMcp.Deploy"],
      "env": { "SOLUTION_PATH": "<ТЕКУЩАЯ_ПАПКА>" }
    }
  }
}
Замени <ТЕКУЩАЯ_ПАПКА> на абсолютный путь текущей директории.

3. Скопируй папки из .mcp-server/ в .claude/:
   - .mcp-server/skills -> .claude/skills
   - .mcp-server/agents -> .claude/agents
   - .mcp-server/hooks -> .claude/hooks
   - .mcp-server/rules -> .claude/rules

4. Скопируй .mcp-server/knowledge-base -> ./knowledge-base

5. Проверь что MCP работает: найди сущность Employee через search_metadata
```

---

Если Employee нашёлся — всё готово. Можно работать.

## Шаг 4. Первая задача

```
Создай справочник «Источник лидов» (LeadSource).
Поля: Name (строка, обязательное), Description (текст).
```

Или вызовите skill:
```
/create-databook
```

## Что дальше

- 41 skill доступен через `/имя-skill` (полный список: `.claude/skills/`)
- Knowledge base: `knowledge-base/` (38 гайдов по DDS)
- Ошибки сборки: `/dds-build-errors`
- Валидация: `/validate-all` (после каждого изменения)
