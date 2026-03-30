# Directum MCP Server — установка для Windows (PowerShell)
# Запуск: powershell -ExecutionPolicy Bypass -File setup.ps1
$ErrorActionPreference = "Stop"

function Log($msg)  { Write-Host "[OK] $msg" -ForegroundColor Green }
function Warn($msg) { Write-Host "[!!] $msg" -ForegroundColor Yellow }
function Err($msg)  { Write-Host "[XX] $msg" -ForegroundColor Red }
function Info($msg) { Write-Host "[>>] $msg" -ForegroundColor Cyan }

$RepoUrl = "https://github.com/dunaevdmitriys-dev/directum-mcp-server.git"
$InstallDir = if ($args[0]) { $args[0] } else { "$env:USERPROFILE\directum-workspace" }

Write-Host ""
Write-Host "  Directum MCP Server - установка (Windows)" -ForegroundColor Blue
Write-Host "  Папка: $InstallDir" -ForegroundColor Blue
Write-Host ""

# --- winget ---
$hasWinget = $false
try { $null = Get-Command winget -ErrorAction Stop; $hasWinget = $true } catch {}

# --- Git ---
try { $null = Get-Command git -ErrorAction Stop }
catch {
    if ($hasWinget) {
        Info "Устанавливаю Git..."
        winget install --id Git.Git -e --accept-source-agreements --accept-package-agreements
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    } else {
        Err "Git не установлен. Скачайте: https://git-scm.com/download/win"
        exit 1
    }
}
Log "Git: $(git --version)"

# --- Node.js ---
$needNode = $false
try {
    $null = Get-Command node -ErrorAction Stop
    $nodeMajor = [int]((node --version).TrimStart('v').Split('.')[0])
    if ($nodeMajor -lt 20) { $needNode = $true }
} catch { $needNode = $true }

if ($needNode) {
    if ($hasWinget) {
        Info "Устанавливаю Node.js..."
        winget install --id OpenJS.NodeJS.LTS -e --accept-source-agreements --accept-package-agreements
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    } else {
        Err "Node.js 20+ не установлен. Скачайте: https://nodejs.org"
        exit 1
    }
}
Log "Node.js: $(node --version)"

# --- .NET SDK ---
$needDotnet = $false
try {
    $null = Get-Command dotnet -ErrorAction Stop
    $dotnetMajor = [int]((dotnet --version).Split('.')[0])
    if ($dotnetMajor -lt 10) { $needDotnet = $true }
} catch { $needDotnet = $true }

if ($needDotnet) {
    if ($hasWinget) {
        Info "Устанавливаю .NET 10 SDK..."
        winget install --id Microsoft.DotNet.SDK.10 -e --accept-source-agreements --accept-package-agreements
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
    } else {
        Err ".NET 10 SDK не установлен. Скачайте: https://dotnet.microsoft.com/download/dotnet/10.0"
        exit 1
    }
}
Log ".NET SDK: $(dotnet --version)"

# --- Claude Code ---
try { $null = Get-Command claude -ErrorAction Stop }
catch {
    Info "Устанавливаю Claude Code..."
    npm install -g @anthropic-ai/claude-code
}
Log "Claude Code установлен"

# --- Клонирование ---
if (-not (Test-Path $InstallDir)) { New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null }
Push-Location $InstallDir

if (Test-Path ".mcp-server") {
    Warn ".mcp-server уже существует, обновляю..."
    Push-Location .mcp-server
    try { git pull --ff-only } catch { Warn "Не удалось обновить" }
    Pop-Location
} else {
    Info "Клонирую репозиторий..."
    git clone $RepoUrl .mcp-server
}

# --- Сборка ---
Info "Собираю MCP-сервер..."
Push-Location .mcp-server
dotnet build --verbosity quiet
Pop-Location
Log "Сборка завершена"

# --- Настройка ---
Info "Настраиваю окружение..."
$McpPath = (Resolve-Path ".mcp-server").Path -replace '\\', '/'
$WorkPath = (Resolve-Path ".").Path -replace '\\', '/'

# CLAUDE.md
if (-not (Test-Path "CLAUDE.md")) {
    Copy-Item ".mcp-server/CLAUDE-TEMPLATE.md" "CLAUDE.md"
}

# .mcp.json
$mcpJson = @"
{
  "mcpServers": {
    "directum-scaffold": {
      "command": "dotnet",
      "args": ["run", "--project", "$McpPath/src/DirectumMcp.Scaffold"],
      "env": { "SOLUTION_PATH": "$WorkPath" }
    },
    "directum-validate": {
      "command": "dotnet",
      "args": ["run", "--project", "$McpPath/src/DirectumMcp.Validate"],
      "env": { "SOLUTION_PATH": "$WorkPath" }
    },
    "directum-analyze": {
      "command": "dotnet",
      "args": ["run", "--project", "$McpPath/src/DirectumMcp.Analyze"],
      "env": { "SOLUTION_PATH": "$WorkPath" }
    },
    "directum-deploy": {
      "command": "dotnet",
      "args": ["run", "--project", "$McpPath/src/DirectumMcp.Deploy"],
      "env": { "SOLUTION_PATH": "$WorkPath" }
    }
  }
}
"@
$mcpJson | Out-File -FilePath ".mcp.json" -Encoding utf8

# Skills, agents, hooks, rules
if (-not (Test-Path ".claude")) { New-Item -ItemType Directory -Path ".claude" | Out-Null }
foreach ($dir in @("skills", "agents", "hooks", "rules")) {
    if (Test-Path ".mcp-server/$dir") {
        Copy-Item -Recurse -Force ".mcp-server/$dir" ".claude/$dir"
    }
}

# Knowledge base
if (Test-Path ".mcp-server/knowledge-base") {
    Copy-Item -Recurse -Force ".mcp-server/knowledge-base" "knowledge-base"
}

Pop-Location
Log "Окружение настроено"

# --- Итог ---
Write-Host ""
Write-Host "  Готово!" -ForegroundColor Green
Write-Host ""
Write-Host "  Что дальше:" -ForegroundColor White
Write-Host "    cd $InstallDir" -ForegroundColor Gray
Write-Host "    claude" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Первая команда в Claude Code:" -ForegroundColor White
Write-Host "    Найди сущность Employee через search_metadata" -ForegroundColor Gray
Write-Host ""
