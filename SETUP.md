# Установка

> Нужна подписка Claude Pro ($20/мес) или Max ($100/мес) на [claude.ai](https://claude.ai)

## Вариант 1: Одна команда (рекомендуется)

Скрипт сам проверит/установит зависимости, скачает репозиторий, соберёт, настроит окружение.

### macOS / Linux

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/dunaevdmitriys-dev/directum-mcp-server/master/scripts/setup.sh)
```

### Windows (PowerShell от администратора)

```powershell
irm https://raw.githubusercontent.com/dunaevdmitriys-dev/directum-mcp-server/master/scripts/setup.ps1 | iex
```

После завершения:
```bash
cd ~/directum-workspace
claude
```

Первая команда в Claude Code:
```
Найди сущность Employee через search_metadata
```

Если Employee нашёлся — всё работает.

---

## Вариант 2: Вручную

<details>
<summary>macOS</summary>

```bash
brew install node dotnet-sdk git
npm install -g @anthropic-ai/claude-code
mkdir ~/directum-workspace && cd ~/directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server && dotnet build && cd ..
claude
```

</details>

<details>
<summary>Windows</summary>

1. Установить [Node.js](https://nodejs.org), [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), [Git](https://git-scm.com/download/win)
2. Перезапустить терминал
3. В PowerShell:

```powershell
npm install -g @anthropic-ai/claude-code
mkdir $HOME\directum-workspace
cd $HOME\directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server; dotnet build; cd ..
claude
```

</details>

<details>
<summary>Linux (Ubuntu/Debian)</summary>

```bash
curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
sudo apt-get install -y nodejs git
# .NET 10: https://learn.microsoft.com/dotnet/core/install/linux-ubuntu
npm install -g @anthropic-ai/claude-code
mkdir ~/directum-workspace && cd ~/directum-workspace
git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server .mcp-server
cd .mcp-server && dotnet build && cd ..
claude
```

</details>

### После запуска Claude Code — вставьте промпт:

```
Настрой рабочее пространство для разработки Directum RX:
1. Скопируй .mcp-server/CLAUDE-TEMPLATE.md в ./CLAUDE.md
2. Создай .mcp.json с 4 серверами (scaffold, validate, analyze, deploy)
   из .mcp-server/src/DirectumMcp.*, SOLUTION_PATH = текущая папка
3. Скопируй skills, agents, hooks, rules из .mcp-server/ в .claude/
4. Скопируй .mcp-server/knowledge-base в ./knowledge-base
5. Проверь: найди Employee через search_metadata
```

---

## Первая задача

```
/create-databook
```

Или:
```
Создай справочник «Источник лидов» (LeadSource).
Поля: Name (строка, обязательное), Description (текст).
```

---

## Решение проблем

| Проблема | Решение |
|----------|---------|
| MCP не подключается | Подождите 10-15 сек (серверы компилируются при первом запуске) |
| Windows: кириллица в пути | Создайте папку без русских букв: `C:\dev\directum-workspace` |
| Windows: PowerShell блокирует скрипт | `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser` |
| dotnet build падает | Проверьте `dotnet --list-sdks` — нужен SDK 10.x |
| Claude Code просит авторизацию | При первом запуске откроется браузер — войдите в аккаунт Anthropic |
