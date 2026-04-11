#!/bin/bash
# PreToolUse hook: защита от записи в дистрибутив/ и от placeholder GUID в .mtd
# Exit 0 = разрешить, Exit 2 = заблокировать
# Dependencies: bash, grep (POSIX). No python3/jq required.

# Читаем JSON payload из stdin
INPUT=$(cat)

# Извлекаем tool_name — чистый bash (grep + sed)
TOOL_NAME=$(printf '%s' "$INPUT" | grep -o '"tool_name":"[^"]*' | head -1 | sed 's/"tool_name":"//')
# Извлекаем file_path из tool_input
FILE_PATH=$(printf '%s' "$INPUT" | grep -o '"file_path":"[^"]*' | head -1 | sed 's/"file_path":"//')

# === Проверка 1: Блокировка записи в дистрибутив/ ===
if [[ "$TOOL_NAME" == "Edit" || "$TOOL_NAME" == "Write" ]]; then
  if [[ "$FILE_PATH" == *"дистрибутив/"* ]]; then
    echo "БЛОКИРОВАНО: запись в дистрибутив/ запрещена. Это read-only reference платформы RX 26.1."
    exit 2
  fi
fi

# === Проверка 2: Блокировка placeholder GUID в .mtd ===
if [[ "$TOOL_NAME" == "Edit" || "$TOOL_NAME" == "Write" ]]; then
  if [[ "$FILE_PATH" == *.mtd ]]; then
    PLACEHOLDER="00000000-0000-0000-0000-000000000000"
    # Извлекаем content и new_string для проверки
    CONTENT=$(printf '%s' "$INPUT" | grep -o '"content":"[^"]*' | head -1 | sed 's/"content":"//')
    NEW_STRING=$(printf '%s' "$INPUT" | grep -o '"new_string":"[^"]*' | head -1 | sed 's/"new_string":"//')
    if [[ "$CONTENT" == *"$PLACEHOLDER"* ]] || [[ "$NEW_STRING" == *"$PLACEHOLDER"* ]]; then
      echo "БЛОКИРОВАНО: .mtd содержит placeholder GUID $PLACEHOLDER. Используй реальный GUID из extract_entity_schema или существующего .mtd."
      exit 2
    fi
  fi
fi

# Всё остальное — разрешить
exit 0
