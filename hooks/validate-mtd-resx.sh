#!/bin/bash
# Валидация .mtd и .resx файлов Directum RX
# PostToolUse hook: проверяет типичные ошибки после Write/Edit

FILE="$1"

# Только .mtd и .resx файлы
if [[ "$FILE" != *.mtd ]] && [[ "$FILE" != *.resx ]]; then
  exit 0
fi

if [[ ! -f "$FILE" ]]; then
  exit 0
fi

ERRORS=0

# === .mtd проверки ===
if [[ "$FILE" == *.mtd ]]; then

  # 1. Проверка что NameGuid не пустой (00000000-0000-...)
  if grep -q '"NameGuid": "00000000-0000-0000-0000-000000000000"' "$FILE"; then
    echo "FAIL: $FILE — NameGuid содержит нулевой GUID. Возьми реальный из extract_entity_schema или существующего .mtd"
    ERRORS=$((ERRORS + 1))
  fi

  # 2. Проверка что BaseGuid указан (не пустая строка)
  if grep -q '"BaseGuid": ""' "$FILE"; then
    echo "FAIL: $FILE — BaseGuid пустой. Укажи GUID базового типа из 23_mtd_reference.md"
    ERRORS=$((ERRORS + 1))
  fi

  # 3. CollectionPropertyMetadata в DatabookEntry — запрещено
  if grep -q '"DatabookEntry\|04581d26-0780-4cfd-b3cd-c2cafc5798b0' "$FILE"; then
    if grep -q 'CollectionPropertyMetadata' "$FILE"; then
      echo "FAIL: $FILE — DatabookEntry НЕ может иметь CollectionProperty. Используй Document"
      ERRORS=$((ERRORS + 1))
    fi
  fi

  # 4. FormTabs — не поддерживаются в DDS 25.3/26.1
  if grep -q '"FormTabs"' "$FILE"; then
    echo "FAIL: $FILE — FormTabs НЕ поддерживаются в DDS 25.3/26.1"
    ERRORS=$((ERRORS + 1))
  fi

fi

# === .resx проверки ===
if [[ "$FILE" == *.resx ]]; then

  # 1. Resource_<GUID> вместо Property_<Name> — запрещено
  if grep -q 'name="Resource_' "$FILE"; then
    echo "FAIL: $FILE — найден Resource_<GUID>. Используй Property_<Name> формат"
    grep -n 'name="Resource_' "$FILE"
    ERRORS=$((ERRORS + 1))
  fi

  # 2. Пустые value в System.resx
  if [[ "$FILE" == *System*.resx ]]; then
    if grep -q '<value></value>' "$FILE" || grep -q '<value />' "$FILE"; then
      echo "WARN: $FILE — найдены пустые значения. Заполни DisplayName и Property_ ключи"
    fi
  fi

fi

if [[ $ERRORS -gt 0 ]]; then
  echo "---"
  echo "Найдено $ERRORS ошибок. Исправь перед продолжением."
  exit 1
fi

exit 0
