---
description: "Единая валидация всех артефактов Directum RX через MCP — запускай после КАЖДОГО изменения .mtd, .resx, .cs"
---

# validate-all — Полная валидация за один вызов

Запускает ВСЕ MCP-проверки последовательно и выдаёт сводный отчёт.

## Когда использовать
- После создания/изменения любой сущности
- Перед сборкой .dat пакета
- Перед публикацией на стенд
- Когда что-то "не работает" и непонятно почему

## Алгоритм

### 1. Определить путь к пакету
```
path = аргумент пользователя ИЛИ автоопределение из {ProjectName}/source/
```

### 2. Запустить проверки (последовательно, каждая зависит от предыдущей)

```
Level 1: STRUCTURAL (блокирующий)
├── MCP: check_package {path}
├── MCP: validate_guid_consistency {path}
├── MCP: check_code_consistency {path}
└── MCP: check_resx {path}

Level 2: DEPENDENCIES
├── MCP: dependency_graph {path}
├── MCP: check_permissions {path}
└── MCP: find_dead_resources {path}

Level 3: DEPLOY-READINESS
└── MCP: validate_deploy {path}
```

### 3. Классифицировать результаты

| Уровень | Действие |
|---------|----------|
| CRITICAL | СТОП. Показать ошибку. Предложить fix. НЕ продолжать. |
| HIGH | Показать предупреждение. Предложить autofix через MCP fix_package. |
| MEDIUM | Показать информационно. Продолжить. |
| INFO | Записать в отчёт. |

### 4. Autofix (если есть HIGH)
```
MCP: fix_package {path}
MCP: sync_resx_keys {path}
→ Повторить Level 1 проверки после fix
```

### 5. Формат вывода

```
=== validate-all: {path} ===

Level 1 — Structural:
  [OK] check_package: 0 errors
  [OK] validate_guid_consistency: all GUIDs unique
  [FAIL] check_code_consistency: 2 mismatches (Entity.mtd ↔ EntityHandlers.cs)
  [OK] check_resx: format correct

Level 2 — Dependencies:
  [OK] dependency_graph: no cycles
  [OK] check_permissions: roles defined
  [WARN] find_dead_resources: 3 unused keys

Level 3 — Deploy:
  [OK] validate_deploy: ready

Итого: 6 OK, 1 WARN, 1 FAIL
Статус: НЕ ГОТОВ (исправить FAIL в Level 1)
```

## Definition of Done (встроенный чеклист)

Пакет считается ГОТОВЫМ только когда ВСЕ пункты выполнены:
- [ ] check_package: 0 errors
- [ ] check_code_consistency: 0 mismatches
- [ ] validate_guid_consistency: 0 duplicates
- [ ] check_resx: все ключи Property_<Name>, все DisplayName заполнены
- [ ] dependency_graph: нет циклов
- [ ] validate_deploy: ready
- [ ] Все .cs классы — partial (кроме Constants)
- [ ] Подписи полей на карточке НЕ пустые (проверить через Playwright если доступен)
- [ ] Обложка модуля работает (проверить через Playwright если доступен)
