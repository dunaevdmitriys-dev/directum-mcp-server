---
description: "Закоммитить и запушить все изменения в репозиторий"
---

# Push All

Коммитит и пушит ВСЕ текущие изменения (staged + unstaged + untracked) одной командой.

## Когда использовать
- Быстрый push всех накопленных изменений
- Конец рабочей сессии — сохранить всё
- Когда `git add -A && git commit && git push` — это именно то, что нужно

## Workflow

### 1. Анализ изменений
- `git diff --stat` и `git status` — понять что изменилось
- Определить суть изменений для commit message

### 2. Stage и Commit
- `git add -A` — добавить всё
- `git diff --cached --stat` — показать что войдёт в коммит
- `git log --oneline -3` — стиль предыдущих коммитов
- Составить лаконичное сообщение коммита
- Коммит с `Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>`

### 3. Push и Отчёт
- Push в remote tracking branch
- Вывести: **branch**, **commit hash**, **кол-во файлов**

## Правила
- **Stage всё:** `git add -A` — без частичных коммитов
- **Стиль коммитов:** следовать конвенции проекта
- **Co-Author:** всегда включать
- **Без version bumps:** пропускать CHANGELOG/version если не просили явно

## GitHub Issues

После push:
1. Если работа ведётся по issue — **добавь комментарий** с commit hash и summary изменений

**Формат комментария:**
```
### Commit pushed: {hash}

**Изменения:**
- {summary}

**Файлов:** {N} changed
```

**API:**
```bash
gh api repos/dunaevdmitriys-dev/directum-mcp-server/issues/{N}/comments -f body="..."
```
