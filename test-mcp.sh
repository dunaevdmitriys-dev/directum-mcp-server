#!/bin/bash
# Test MCP server with JSON-RPC messages via stdio
# Usage: ./test-mcp.sh
# Prerequisites: set SOLUTION_PATH to your Directum RX git repository

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

# === CONFIGURE THESE PATHS FOR YOUR ENVIRONMENT ===
SOLUTION_PATH="${SOLUTION_PATH:-/path/to/your/rx/git_repository}"
# Example MTD file path (any entity .mtd in your solution)
MTD_PATH="${MTD_PATH:-$SOLUTION_PATH/base/Sungero.Commons/Sungero.Commons.Shared/AIAgentRole/AIAgentRole.mtd}"
# Example directory path (any entity directory)
ENTITY_DIR="${ENTITY_DIR:-$(dirname "$MTD_PATH")}"

if [ ! -d "$SOLUTION_PATH" ] || [ "$SOLUTION_PATH" = "/path/to/your/rx/git_repository" ]; then
    echo "ERROR: Set SOLUTION_PATH environment variable to your Directum RX git repository"
    echo "Example: SOLUTION_PATH=/d/25.3/RXlog/git_repository ./test-mcp.sh"
    exit 1
fi

# Build JSON-RPC messages
INIT='{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
INIT_DONE='{"jsonrpc":"2.0","method":"notifications/initialized"}'
LIST_TOOLS='{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'
CALL_INSPECT='{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"inspect","arguments":{"path":"'"$MTD_PATH"'"}}}'
CALL_CHECK_RESX='{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"check_resx","arguments":{"directoryPath":"'"$ENTITY_DIR"'"}}}'

echo "=== Testing Directum MCP DevTools Server ==="
echo "SOLUTION_PATH: $SOLUTION_PATH"
echo "MTD_PATH: $MTD_PATH"
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
