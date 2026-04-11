#!/bin/bash
# PostToolUse hook: напоминание о валидации после изменения .mtd/.resx/.cs
# Exit 0 всегда (информационный хук)
# Dependencies: bash, grep, sed (POSIX). No python3/jq required.

# Читаем JSON payload из stdin
INPUT=$(cat)

# Извлекаем путь к файлу — чистый bash
FILE_PATH=$(printf '%s' "$INPUT" | grep -o '"file_path":"[^"]*' | head -1 | sed 's/"file_path":"//')

# Для .mtd, .resx, .cs файлов в source/
if [[ "$FILE_PATH" == *.mtd ]] || [[ "$FILE_PATH" == *.resx ]] || [[ "$FILE_PATH" == *source/*.cs ]]; then
  # Определяем путь модуля — чистый bash через parameter expansion
  if [[ "$FILE_PATH" == *"/source/"* ]]; then
    PREFIX="${FILE_PATH%%/source/*}"
    AFTER="${FILE_PATH#*/source/}"
    MODULE="${AFTER%%/*}"
    MODULE_PATH="${PREFIX}/source/${MODULE}"
    echo "VALIDATE NOW: mcp__directum-validate__validate_all path=\"$MODULE_PATH\""
  else
    echo "VALIDATE NOW: mcp__directum-validate__validate_all"
  fi
fi

exit 0
