#!/bin/bash
# Автовалидация .cs файлов Directum RX
# Проверяет запрещённые паттерны после каждого Write/Edit
# Вызывается через Claude Code Hooks (PostToolUse)

FILE="$1"

# Только .cs файлы
if [[ "$FILE" != *.cs ]]; then
  exit 0
fi

# Если файл не существует
if [[ ! -f "$FILE" ]]; then
  exit 0
fi

ERRORS=0

# Исключаем строки-комментарии (начинающиеся с //)
filter_comments() {
  grep -v '^\s*//'
}

# 1. is/as cast (NHibernate прокси)
if grep -n -E '\b(is|as)\s+I[A-Z][a-zA-Z]+' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено 'is/as IType'. Используй Entities.Is()/As()"
  grep -n -E '\b(is|as)\s+I[A-Z][a-zA-Z]+' "$FILE" | filter_comments
  ERRORS=$((ERRORS + 1))
fi

# 2. DateTime.Now / DateTime.Today
if grep -n -E 'DateTime\.(Now|Today)' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено DateTime.Now/Today. Используй Calendar.Now/Today"
  grep -n -E 'DateTime\.(Now|Today)' "$FILE" | filter_comments
  ERRORS=$((ERRORS + 1))
fi

# 3. System.Threading
if grep -n 'System\.Threading' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено System.Threading. Используй AsyncHandlers"
  ERRORS=$((ERRORS + 1))
fi

# 4. System.Reflection
if grep -n 'System\.Reflection' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено System.Reflection"
  ERRORS=$((ERRORS + 1))
fi

# 5. Session.Execute (устаревший API)
if grep -n 'Session\.Execute' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено Session.Execute. Используй SQL.CreateConnection()"
  ERRORS=$((ERRORS + 1))
fi

# 6. new Tuple<
if grep -n -E 'new\s+Tuple<' "$FILE" | filter_comments | grep -q .; then
  echo "FAIL: $FILE — запрещено new Tuple<>. Используй структуры через Create()"
  ERRORS=$((ERRORS + 1))
fi

# 7. Анонимные типы (new { ... = })
if grep -n -E 'new\s*\{[^}]*[a-zA-Z]+\s*=' "$FILE" | filter_comments | grep -q .; then
  echo "WARN: $FILE — возможен анонимный тип (new { }). Используй структуры"
  # Только предупреждение, не ошибка
fi

if [[ $ERRORS -gt 0 ]]; then
  echo "---"
  echo "Найдено $ERRORS запрещённых паттернов. Исправь их."
  exit 1
fi

exit 0
