# Установка за 5 минут

> Нужна подписка Claude Pro ($20/мес) или Max ($100/мес) на [claude.ai](https://claude.ai)

---

## Шаг 1. Установить зависимости

### macOS

```bash
# Node.js
brew install node

# .NET 10 SDK
brew install dotnet-sdk

# Git (обычно уже есть)
brew install git

# Claude Code
npm install -g @anthropic-ai/claude-code
```

### Windows

```powershell
# Node.js — скачать установщик
# https://nodejs.org (LTS версия)

# .NET 10 SDK — скачать установщик
# https://dotnet.microsoft.com/download/dotnet/10.0

# Git — скачать установщик
# https://git-scm.com/download/win

# Claude Code (после установки Node.js, в PowerShell или cmd)
npm install -g @anthropic-ai/claude-code
```

### Linux (Ubuntu/Debian)

```bash
# Node.js
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt-get install -y nodejs

# .NET 10 SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# Git
sudo apt-get install -y git

# Claude Code
npm install -g @anthropic-ai/claude-code
```

---

## Шаг 2. Скачать и собрать

### macOS / Linux

```bash
mkdir ~/directum-workspace && cd ~/directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server && dotnet build && cd ..
```

### Windows (PowerShell)

```powershell
mkdir $HOME\directum-workspace
cd $HOME\directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server; dotnet build; cd ..
```

---

## Шаг 3. Запустить Claude Code и вставить промпт

### macOS / Linux

```bash
cd ~/directum-workspace
claude
```

### Windows (PowerShell)

```powershell
cd $HOME\directum-workspace
claude
```

### Промпт-установщик (одинаковый для всех ОС)

Вставьте в Claude Code этот текст:

```
Настрой рабочее пространство для разработки Directum RX. Выполни по шагам:

1. Скопируй файл .mcp-server/CLAUDE-TEMPLATE.md в ./CLAUDE.md

2. Создай файл .mcp.json в текущей папке с 4 MCP-серверами:
   directum-scaffold, directum-validate, directum-analyze, directum-deploy.
   Каждый:
   - command: "dotnet"
   - args: ["run", "--project", ".mcp-server/src/DirectumMcp.<Name>"]
   - env.SOLUTION_PATH = абсолютный путь текущей папки
   Используй правильный формат путей для текущей ОС.

3. Скопируй папки из .mcp-server/ в .claude/:
   - .mcp-server/skills -> .claude/skills
   - .mcp-server/agents -> .claude/agents
   - .mcp-server/hooks -> .claude/hooks
   - .mcp-server/rules -> .claude/rules

4. Скопируй .mcp-server/knowledge-base -> ./knowledge-base

5. Проверь что MCP работает: найди сущность Employee через search_metadata
```

Если Employee нашёлся — всё готово. Можно работать.

---

## Шаг 4. Первая задача

```
Создай справочник «Источник лидов» (LeadSource).
Поля: Name (строка, обязательное), Description (текст).
```

Или вызовите skill:
```
/create-databook
```

---

## Что дальше

- 41 skill доступен через `/имя-skill` (полный список: `.claude/skills/`)
- Knowledge base: `knowledge-base/` (38 гайдов по DDS)
- Ошибки сборки: `/dds-build-errors`
- Валидация: `/validate-all` (после каждого изменения)

---

## Решение проблем

### MCP не подключается

При первом запуске серверы компилируются — это занимает 10-15 секунд. Если `search_metadata` не сработал, подождите и попробуйте снова.

### Windows: пути с кириллицей

Если путь к папке содержит русские буквы (`C:\Пользователи\...`), создайте рабочую папку без кириллицы: `C:\dev\directum-workspace`.

### Windows: PowerShell не запускает npm

Выполните от имени администратора:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### dotnet build падает

Убедитесь что установлен именно .NET **10** SDK (не Runtime):
```bash
dotnet --list-sdks
```
Должна быть строка `10.x.xxx`.

### Claude Code просит авторизацию

При первом запуске `claude` откроет браузер для входа в аккаунт Anthropic. Войдите и вернитесь в терминал.
