#!/bin/bash
# Test MCP server with JSON-RPC messages via stdio

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
SOLUTION_PATH="d:\\25.3.20260215.0041\\RXlog\\git_repository"
MTD_PATH="d:/25.3.20260215.0041/RXlog/git_repository/base/Sungero.Commons/Sungero.Commons.Shared/AIAgentRole/AIAgentRole.mtd"
RESX_PATH="d:/25.3.20260215.0041/RXlog/git_repository/base/Sungero.Commons/Sungero.Commons.Shared/AIAgentRole/AIAgentRoleSystem.resx"

# Build JSON-RPC messages
INIT='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
INIT_DONE='{"jsonrpc":"2.0","method":"notifications/initialized"}'
LIST_TOOLS='{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
CALL_INSPECT='{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"inspect","arguments":{"path":"'"$MTD_PATH"'"}}}'
CALL_CHECK_RESX='{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"check_resx","arguments":{"directoryPath":"d:/25.3.20260215.0041/RXlog/git_repository/base/Sungero.Commons/Sungero.Commons.Shared/AIAgentRole"}}}'

echo "=== Testing Directum MCP DevTools Server ==="
echo ""

# Send all messages
(
  echo "$INIT"
  sleep 1
  echo "$INIT_DONE"
  sleep 0.5
  echo "$LIST_TOOLS"
  sleep 0.5
  echo "$CALL_INSPECT"
  sleep 2
  echo "$CALL_CHECK_RESX"
  sleep 2
) | SOLUTION_PATH="$SOLUTION_PATH" dotnet run --project "$PROJECT_DIR/src/DirectumMcp.DevTools" --no-build 2>/dev/null
