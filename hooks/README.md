# Hooks -- автоматический контроль качества

Hooks запускаются автоматически при работе Claude Code. Проверяют запрещенные паттерны в C#-коде и типичные ошибки в .mtd/.resx файлах Directum RX сразу после каждого Write/Edit.

## Установка

```bash
cp -r hooks/ /ваш-проект/.claude/hooks/
chmod +x /ваш-проект/.claude/hooks/*.sh
```

Также добавьте в `.claude/settings.json`:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "bash \"$CLAUDE_PROJECT_DIR/.claude/hooks/validate-cs.sh\" \"$CLAUDE_FILE_PATH\""
          },
          {
            "type": "command",
            "command": "bash \"$CLAUDE_PROJECT_DIR/.claude/hooks/validate-mtd-resx.sh\" \"$CLAUDE_FILE_PATH\""
          }
        ]
      }
    ]
  }
}
```

## Hooks

| Hook | Тип | Что делает |
|------|-----|------------|
| `validate-cs.sh` | PostToolUse | Проверяет .cs файлы на запрещенные паттерны Directum RX |
| `validate-mtd-resx.sh` | PostToolUse | Проверяет .mtd и .resx файлы на типичные ошибки DDS |

## validate-cs.sh -- проверки C#

Срабатывает на `.cs` файлы. Блокирует (exit 1) при обнаружении:

| Паттерн | Причина | Правильная альтернатива |
|---------|---------|------------------------|
| `is/as IType` | NHibernate прокси не совместимы с прямым cast | `Entities.Is()` / `Entities.As()` |
| `DateTime.Now/Today` | Не учитывает часовой пояс пользователя | `Calendar.Now` / `Calendar.Today` |
| `System.Threading` | Запрещено в Directum RX | `AsyncHandlers` |
| `System.Reflection` | Запрещено в Directum RX | -- |
| `Session.Execute` | Устаревший API | `SQL.CreateConnection()` |
| `new Tuple<>` | Не сериализуется в DDS | Структуры через `Create()` |
| `new { ... = }` | Анонимные типы (только предупреждение) | Структуры |

Строки-комментарии (`//`) исключаются из проверки.

## validate-mtd-resx.sh -- проверки .mtd и .resx

### Проверки .mtd (блокирующие):

| Проверка | Описание |
|----------|----------|
| Нулевой NameGuid | `00000000-0000-...` -- нужен реальный GUID из `extract_entity_schema` |
| Пустой BaseGuid | Не указан GUID базового типа |
| CollectionProperty в DatabookEntry | DatabookEntry не поддерживает коллекции -- используй Document |
| FormTabs | Не поддерживаются в DDS 25.3/26.1 |

### Проверки .resx (блокирующие):

| Проверка | Описание |
|----------|----------|
| `Resource_<GUID>` | Запрещенный формат ключа -- используй `Property_<Name>` |
| Пустые `<value>` | Предупреждение для System.resx -- заполни DisplayName и Property_ ключи |

## Коды возврата

- `exit 0` -- проверка пройдена (или файл не подходит по расширению)
- `exit 1` -- найдены ошибки, Claude Code покажет диагностику

## Зависимости

bash, grep (POSIX). Не требует python3, jq или других внешних утилит.
