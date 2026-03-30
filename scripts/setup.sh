#!/usr/bin/env bash
# Directum MCP Server — установка для macOS и Linux
# Запуск: curl -fsSL https://raw.githubusercontent.com/dunaevdmitriys-dev/directum-mcp-server/master/scripts/setup.sh | bash
# Или:    bash setup.sh [путь_установки]
set -euo pipefail

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; BLUE='\033[0;34m'; NC='\033[0m'
log()  { echo -e "${GREEN}[OK]${NC} $1"; }
warn() { echo -e "${YELLOW}[!!]${NC} $1"; }
err()  { echo -e "${RED}[XX]${NC} $1"; }
info() { echo -e "${BLUE}[>>]${NC} $1"; }

INSTALL_DIR="${1:-$HOME/directum-workspace}"
REPO_URL="https://github.com/dunaevdmitriys-dev/directum-mcp-server.git"

echo ""
echo "  Directum MCP Server — установка"
echo "  Папка: $INSTALL_DIR"
echo ""

# --- Определяем ОС ---
if [[ "$OSTYPE" == "darwin"* ]]; then
    OS="macos"
    info "macOS $(sw_vers -productVersion)"
elif [[ "$OSTYPE" == "linux-gnu"* ]]; then
    OS="linux"
    info "Linux $(cat /etc/os-release 2>/dev/null | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"' || echo '')"
else
    err "Неподдерживаемая ОС. Для Windows используйте setup.ps1"
    exit 1
fi

# --- Git ---
if ! command -v git &>/dev/null; then
    err "Git не установлен."
    [[ "$OS" == "macos" ]] && info "Выполните: xcode-select --install"
    [[ "$OS" == "linux" ]] && info "Выполните: sudo apt-get install -y git"
    exit 1
fi
log "Git $(git --version | cut -d' ' -f3)"

# --- Node.js ---
if ! command -v node &>/dev/null; then
    info "Устанавливаю Node.js..."
    if [[ "$OS" == "macos" ]]; then
        command -v brew &>/dev/null || /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
        [[ -f /opt/homebrew/bin/brew ]] && eval "$(/opt/homebrew/bin/brew shellenv)"
        brew install node
    else
        curl -fsSL https://deb.nodesource.com/setup_22.x | sudo -E bash -
        sudo apt-get install -y nodejs
    fi
fi
log "Node.js $(node --version)"

# --- .NET SDK ---
if ! command -v dotnet &>/dev/null; then
    info "Устанавливаю .NET 10 SDK..."
    if [[ "$OS" == "macos" ]]; then
        command -v brew &>/dev/null || /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
        brew install dotnet-sdk
    else
        wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O /tmp/ms-prod.deb
        sudo dpkg -i /tmp/ms-prod.deb && rm /tmp/ms-prod.deb
        sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
    fi
fi
log ".NET SDK $(dotnet --version)"

# --- Claude Code ---
if ! command -v claude &>/dev/null; then
    info "Устанавливаю Claude Code..."
    npm install -g @anthropic-ai/claude-code
fi
log "Claude Code установлен"

# --- Клонирование ---
mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

if [[ -d ".mcp-server" ]]; then
    warn ".mcp-server уже существует, обновляю..."
    cd .mcp-server && git pull --ff-only 2>/dev/null || true && cd ..
else
    info "Клонирую репозиторий..."
    git clone "$REPO_URL" .mcp-server
fi

# --- Сборка ---
info "Собираю MCP-сервер..."
cd .mcp-server && dotnet build --verbosity quiet && cd ..
log "Сборка завершена"

# --- Настройка окружения ---
info "Настраиваю окружение..."
MCP_PATH="$(cd .mcp-server && pwd)"

# CLAUDE.md
cp -n .mcp-server/CLAUDE-TEMPLATE.md ./CLAUDE.md 2>/dev/null || true

# .mcp.json
cat > .mcp.json << MCPEOF
{
  "mcpServers": {
    "directum-scaffold": {
      "command": "dotnet",
      "args": ["run", "--project", "$MCP_PATH/src/DirectumMcp.Scaffold"],
      "env": { "SOLUTION_PATH": "$INSTALL_DIR" }
    },
    "directum-validate": {
      "command": "dotnet",
      "args": ["run", "--project", "$MCP_PATH/src/DirectumMcp.Validate"],
      "env": { "SOLUTION_PATH": "$INSTALL_DIR" }
    },
    "directum-analyze": {
      "command": "dotnet",
      "args": ["run", "--project", "$MCP_PATH/src/DirectumMcp.Analyze"],
      "env": { "SOLUTION_PATH": "$INSTALL_DIR" }
    },
    "directum-deploy": {
      "command": "dotnet",
      "args": ["run", "--project", "$MCP_PATH/src/DirectumMcp.Deploy"],
      "env": { "SOLUTION_PATH": "$INSTALL_DIR" }
    }
  }
}
MCPEOF

# Skills, agents, hooks, rules
mkdir -p .claude
for dir in skills agents hooks rules; do
    cp -r ".mcp-server/$dir" ".claude/$dir" 2>/dev/null || true
done

# Knowledge base
cp -r .mcp-server/knowledge-base ./knowledge-base 2>/dev/null || true

log "Окружение настроено"

# --- Итог ---
echo ""
echo "  Готово!"
echo ""
echo "  Что дальше:"
echo "    cd $INSTALL_DIR"
echo "    claude"
echo ""
echo "  Первая команда в Claude Code:"
echo "    Найди сущность Employee через search_metadata"
echo ""
echo "  Первая задача:"
echo "    /create-databook"
echo ""
