---
description: "Оркестратор мультиагентной системы: полный цикл разработки Directum RX от PRD до деплоя и E2E-тестирования"
---

# Pipeline — Оркестратор мультиагентной системы

Полный цикл разработки: PRD (опц.) → Research → Architecture → Planning → Implementation → Final Review → System Engineering → Documentation (опц.).
Контекст сохраняется между фазами в `{project_path}/.pipeline/`.

## Входные данные
- **Задача** — описание фичи/решения от пользователя
- **Путь** — `{ProjectName}/` в корне workspace (НЕ в `projects/`). Создаётся автоматически.

## Структура артефактов

```
{ProjectName}/
  .pipeline/
    status.md                     <- RESUME-ФАЙЛ: точка входа для возобновления
    00-prd/
      prd.md                      <- Product Requirements Document (опц.)
    01-research/
      research.md                 <- факты, зависимости, ограничения
    02-design/
      c4-context.md               <- C4 Context diagram
      c4-container.md             <- C4 Container diagram
      c4-component.md             <- C4 Component diagram
      data-flow.md                <- Data Flow Diagram
      sequence-diagrams.md        <- Sequence Diagrams
      domain-model.md             <- Domain model (главный)
      api-contracts.md            <- API contracts
      integration-contracts.md    <- Integration contracts
      test-strategy.md            <- Test strategy
      adr/
        001-{decision}.md         <- Architecture Decision Records
    03-plan/
      plan.md                     <- План реализации с этапами и валидацией
      test-plan.md                <- План тестирования привязанный к этапам
    04-implementation/
      changelog.md                <- Лог изменений разработчика
      code-review.md              <- Code Quality Review
      security-review.md          <- Security Review
      architecture-review.md      <- Architecture Review
      test-results.md             <- Результат тестов
    05-final-review/
      final-report.md             <- Итоговый вердикт
    06-system-engineer/
      build-report.md             <- Отчёт сборки + валидации
    07-documentation/
      docs-report.md              <- Отчёт документации (опц.)
  source/                         <- Код пакета
  settings/
  PackageInfo.xml
  {SolutionName}.dat              <- Готовый пакет для публикации
  {SolutionName}.xml              <- Внешний манифест
```

## Алгоритм оркестрации

### 1. Инициализация
```
mkdir -p {ProjectName}/.pipeline/{00-prd,01-research,02-design/adr,03-plan,04-implementation,05-final-review,06-system-engineer,07-documentation}
```

**Создать `status.md`** — resume-файл для возобновления между сессиями:
```
Создать файл {project_path}/.pipeline/status.md по шаблону (см. секцию "status.md — формат")
```

**При возобновлении:** Если `status.md` уже существует — прочитать его ПЕРВЫМ. Поле `Next` указывает на текущий этап. Не пересоздавать завершённые фазы.

### 2. Фаза 0: PRD (опциональная)
**Запускается при:** `/pipeline` (полный цикл 0-7) или `/pipeline prd`
**Пропускается при:** `/pipeline dev`, `/pipeline review`, `/pipeline build`

Прочитай `.claude/agents/product-owner.md` и запусти агента:
```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое product-owner.md}</agent_prompt>\n\nЗадача: {описание}\nПуть проекта: {project_path}")
```

**Выход:** `00-prd/prd.md` — PRD с user stories, сущностями, ограничениями
**Обновить status.md:** Done += PRD, Next = Research
**Показать пользователю:** summary PRD, спросить "Продолжить?"

### 3. Фаза 1: Research
Прочитай `.claude/agents/researcher.md` и запусти агента:
```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое researcher.md}</agent_prompt>\n\nЗадача: {описание}\nПуть проекта: {project_path}")
```

Агент-исследователь сам запускает суб-агентов через Task для параллельного исследования:
- Архитектурный ресёрч
- Анализ паттернов (через MCP: search_metadata / extract_entity_schema)
- Анализ интеграций
- Анализ существующей кодовой базы
- Анализ ограничений

**MCP после фазы:** `analyze_solution action=health` — проверить состояние решения

**Выход:** `01-research/research.md` — только факты
**Обновить status.md:** Done += Research, Next = Architecture
**Показать пользователю:** краткую сводку, спросить "Продолжить?"

### 4. Фаза 2: Architecture & Design
Прочитай `.claude/agents/architect.md` и `01-research/research.md`:
```
Task(subagent_type="general-purpose",
     model="opus",
     prompt="<agent_prompt>{содержимое architect.md}</agent_prompt>\n\nПуть: {project_path}\n\n<research>{содержимое research.md}</research>")
```

**MCP после фазы:** `analyze_solution action=api` — проверить API-контракты

**Выход:** 8+ файлов в `02-design/`
**Проверить:** все файлы созданы, сущности консистентны
**Обновить status.md:** Done += Architecture, Next = Planning
**Показать пользователю:** domain-model + ключевые ADR, спросить "Продолжить?"

### 5. Фаза 3: Planning
Прочитай `.claude/agents/planner.md`:
```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое planner.md}</agent_prompt>\n\nПуть: {project_path}")
```

**Выход:** `03-plan/plan.md` + `03-plan/test-plan.md`
**Обновить status.md:** Done += Planning, Next = Implementation
**Показать пользователю:** план с этапами, спросить "Продолжить?"

### 6. Фаза 4: Implementation

**6a. Разработка:**

Определить тип проекта и выбрать агента:
- **Directum RX модуль** → `developer.md` (стандартный)
- **Standalone SPA** → `spa-developer.md`
- **Standalone .NET API** → `api-developer.md`
- **Комбинированный** → запустить несколько агентов последовательно

```
Task(subagent_type="general-purpose",
     model="opus",
     prompt="<agent_prompt>{содержимое developer.md}</agent_prompt>\n\nПуть: {project_path}")
```
**Выход:** код в `source/`, `settings/`, `PackageInfo.xml`

**MCP после разработки:** `check_package {path}`, `check_code_consistency {path}`

**6b. Ревью (3 агента параллельно):**
```
Task(code-reviewer.md, model="sonnet", run_in_background=true)
Task(security-reviewer.md, model="sonnet", run_in_background=true)
Task(architecture-reviewer.md, model="sonnet", run_in_background=true)
// Дождаться всех трёх
```

**6c. Тесты:**
```
Task(test-executor.md, model="sonnet")
```

**6d. Цикл исправлений (fix-loop):**

**Порог:** только `has_critical OR has_high`. MEDIUM-замечания = рекомендации, НЕ блокируют pipeline.

```
while has_critical or has_high:
    Task(developer.md, model="opus", prompt="Исправь замечания CRITICAL и HIGH: {замечания}")
    Task(code-reviewer.md, model="sonnet")  // повторная проверка
```

### 7. Фаза 5: Final Review
```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое final-reviewer.md}</agent_prompt>\n\nПуть: {project_path}")
```

**Выход:** `05-final-review/final-report.md`
**Решение:** APPROVED | APPROVED WITH NOTES | CHANGES REQUESTED | REJECTED

### 8. Фаза 6: System Engineering
Прочитай `.claude/agents/system-engineer.md`:
```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое system-engineer.md}</agent_prompt>\n\nПуть: {project_path}\n\nЗадача: Предсборочная валидация + сборка .dat пакета")
```

**Что делает:**
- Валидирует структуру проекта (PackageInfo.xml, source/, settings/)
- Проверяет GUID-согласованность между PackageInfo.xml, settings/Module.json и Module.mtd
- Валидирует JSON всех .mtd файлов
- Проверяет .resx формат и парность
- Проверяет .cs: partial class, namespace, ModuleInitializer
- Проверяет DDS-совместимость (BlockIds, CardForm GUID, FilterPanel, Assignment Ribbon, etc.)
- Собирает .dat пакет (`zip -r`)
- Создаёт внешний .xml манифест
- Генерирует команды для публикации через DeploymentTool
- Для SPA: npm build, копирование в IIS content/
- Для CrmApiV3: dotnet build, настройка как сервис

**MCP после фазы:** `validate_deploy {path}` — проверить деплой-готовность

**Выход:** `06-system-engineer/build-report.md`
**Артефакты:** `{SolutionName}.dat` + `{SolutionName}.xml`

**При ошибке:** цикл исправления (макс. 5 итераций):
```
while build_failed:
    Исправить причину ошибки
    Повторить сборку
```

### 8a. Playwright E2E проверка (после деплоя)

После успешного деплоя, если Playwright MCP подключён:
```
# Запустить E2E тесты
Task(test-executor.md, model="sonnet",
     prompt="Запусти E2E Playwright тесты после деплоя. URL стенда: {stand_url}")
```

Если Playwright MCP не подключён — пропустить, зафиксировать в status.md как "E2E: skipped (no Playwright MCP)".

### 9. Фаза 7: Documentation (опциональная)
**Запускается при:** `/pipeline` (полный цикл) или `/pipeline docs`
**Пропускается при:** `/pipeline dev`, `/pipeline build`

```
Task(subagent_type="general-purpose",
     model="sonnet",
     prompt="<agent_prompt>{содержимое documenter.md}</agent_prompt>\n\nПуть: {project_path}")
```

**Выход:** `07-documentation/docs-report.md`
**Обновить status.md:** Done += Documentation

### 10. Итоговый отчёт пользователю

```
=== Pipeline Complete: {название} ===

Фазы:
[v] PRD — {N} user stories (если запускалась)
[v] Research — {N} фактов собрано
[v] Architecture — {N} артефактов создано
[v] Planning — {N} этапов, {N} задач
[v] Implementation — {N} файлов создано
   Code Review: {PASS|CONCERNS} (CRITICAL: {N}, HIGH: {N}, MEDIUM: {N})
   Security: {PASS|CONCERNS}
   Architecture: {N}%
   Tests: {N}/{N}
[v] Final Review — {APPROVED|...}
[v] System Engineer — .dat собран, {N} файлов, {N} KB
[v] E2E — {PASS|SKIP}
[v] Documentation — {PASS|SKIP}

Артефакты: {ProjectName}/.pipeline/
Код: {ProjectName}/source/
Пакет: {ProjectName}/{Name}.dat

Следующие шаги:
- Публикация: do dt deploy --package="{Name}.dat" --force
- /push-all для коммита
```

## status.md — формат resume-файла

`status.md` — единая точка входа для возобновления pipeline после сброса контекста. Обновляется после КАЖДОЙ фазы.

```markdown
# Pipeline Status

## Snapshot
- Решение: {SolutionName}
- Задача: {одна строка}
- Тип проекта: {rx-module | spa | api | combined}
- Текущая фаза: {N. Name}
- Статус: {green / yellow / red}
- Последнее обновление: {YYYY-MM-DD HH:MM}
- GitHub Issue: #{N}

## Done
- [x] Фаза 0: PRD — {N} user stories
- [x] Фаза 1: Research — {N} фактов
- [x] Фаза 2: Architecture — {N} артефактов

## In Progress
- [ ] Фаза 3: Planning

## Next
- Фаза 3: Planning -> запустить planner.md
- После: Фаза 4: Implementation

## Used GUIDs
| GUID | Тип | Назначение |
|------|-----|------------|
| {guid} | Module | {ModuleName} NameGuid |
| {guid} | Entity | {EntityName} NameGuid |
| {guid} | Property | {EntityName}.{PropertyName} |
| {guid} | Action | {EntityName}.{ActionName} |
| {guid} | Block | {TaskName} Block {N} |

## Decisions Made
- {решение} — {причина}

## Assumptions
- {допущение, принятое без подтверждения пользователя}

## Blockers
- None

## Audit Log
| Дата | Фаза | Артефакты | Валидация | Результат | Следующее |
|------|------|-----------|-----------|-----------|-----------|
| 2026-03-14 12:00 | Research | research.md | analyze_solution | 15 фактов | Architecture |
| 2026-03-14 12:30 | Architecture | 8 файлов | analyze_solution api | OK | Planning |

## Validation Results
| Этап | Команда | Результат | Дата |
|------|---------|-----------|------|
| Этап 2 | `check_package DirRX.CRM` | 0 ошибок | 2026-03-14 |
| Этап 2 | `check_code_consistency DirRX.CRM` | OK | 2026-03-14 |
| Deploy | `validate_deploy DirRX.CRM` | OK | 2026-03-14 |

## MCP Integration Log
| Фаза | MCP-команда | Результат | Дата |
|------|-------------|-----------|------|
| Research | `analyze_solution action=health` | OK | 2026-03-14 |
| Architecture | `analyze_solution action=api` | OK | 2026-03-14 |
| Implementation | `check_package` | 0 errors | 2026-03-14 |
| Implementation | `check_code_consistency` | OK | 2026-03-14 |
| Deploy | `validate_deploy` | OK | 2026-03-14 |
```

### Правила обновления status.md
1. **После каждой фазы** — перенести из In Progress в Done, обновить Next, добавить строку в Audit Log
2. **При ошибке** — записать в Blockers, статус -> red
3. **При допущении** — записать в Assumptions с обоснованием
4. **При решении** — записать в Decisions Made
5. **При валидации** — записать в Validation Results
6. **При MCP вызове** — записать в MCP Integration Log
7. **При генерации GUID** — записать в Used GUIDs (модули, сущности, свойства, блоки)
8. **Никогда не удалять** историю из Audit Log

## test-plan.md — формат плана тестирования

Создаётся планировщиком вместе с plan.md. Привязывает тесты к этапам плана.

```markdown
# Test Plan: {Решение}

## Scope
- In scope: {сущности, модули, бизнес-логика}
- Out of scope: {что не тестируем в этой итерации}

## Environment
- DDS: {версия}
- Стенд: {URL или путь}
- Тестовые данные: {фикстуры, генерация}

## Validation по этапам

### Этап 1: Scaffolding
- [ ] `python3 -m json.tool Module.mtd` — JSON валиден
- [ ] Все директории созданы (Server, ClientBase, Shared)

### Этап 2: Справочники
- [ ] `check_package {path}` — 0 ошибок
- [ ] `check_code_consistency {path}` — MTD <-> C# согласованы
- [ ] Property_* ключи в System.resx для всех свойств

### Этап N: ...
- [ ] {конкретная команда валидации}

## E2E Tests (Playwright)
- [ ] Карточка {Entity} открывается
- [ ] Обязательные поля валидируются
- [ ] Workflow запускается и завершается
- [ ] Обложка модуля отображается корректно

## Acceptance Gates
- [ ] `check_package` — 0 ошибок по всем модулям
- [ ] `check_code_consistency` — 0 расхождений
- [ ] `build_dat` — пакет собирается
- [ ] `validate_deploy` — деплой-ready
- [ ] Code Review: 0 CRITICAL, 0 HIGH
- [ ] Security: 0 CRITICAL, 0 HIGH

## Smoke Checks (после публикации)
- [ ] Карточки сущностей открываются
- [ ] Подписи полей отображаются (не пустые)
- [ ] Обложка модуля работает
- [ ] Действия обложки не вызывают ошибок
```

## Режимы запуска

| Команда | Фазы |
|---------|------|
| `/pipeline` | 0-7 (полный цикл с PRD и документацией) |
| `/pipeline dev` | 1-4 (research -> implementation, без PRD) |
| `/pipeline fast` | 3-4 (сразу planning + implementation, БЕЗ research/architecture) |
| `/pipeline research` | 1 |
| `/pipeline design` | 1-2 |
| `/pipeline plan` | 1-3 |
| `/pipeline review-and-build {path}` | 4-6 (ревью + сборка) |
| `/pipeline review {path}` | 4-6 (на существующем коде) |
| `/pipeline fix {path}` | Исправить замечания + повторное ревью |
| `/pipeline build {path}` | 6 (только сборка .dat) |
| `/pipeline resume {path}` | Продолжить с места остановки (читает status.md) |
| `/pipeline prd` | 0 (только PRD) |
| `/pipeline docs {path}` | 7 (только документация) |
| `/pipeline spa` | 0-7 SPA pipeline: PRD -> scaffold_spa -> generate_crud_api -> build -> deploy -> E2E |
| `/pipeline spa dev` | 1-4 SPA pipeline без PRD |

### SPA Pipeline (режим `/pipeline spa`)

Специализированный pipeline для standalone SPA проектов (React + Vite):

1. **Фаза 0: PRD** — product-owner.md (стандартный)
2. **Фаза 1: Research** — researcher.md (анализ API, DB schema)
3. **Фаза 2: Architecture** — architect.md (SPA-архитектура, API endpoints)
4. **Фаза 3: Planning** — planner.md
5. **Фаза 4: Implementation**
   - **4a:** API Developer (`api-developer.md`) — .NET Minimal API + Npgsql
   - **4b:** SPA Developer (`spa-developer.md`) — React + Vite + Ant Design
   - **4c:** Ревью (code-reviewer + security-reviewer)
   - **4d:** Fix-loop (только CRITICAL + HIGH)
6. **Фаза 5: Final Review** — final-reviewer.md
7. **Фаза 6: System Engineering** — system-engineer.md (npm build + dotnet build + IIS + service)
8. **Фаза 6a: E2E** — Playwright тесты
9. **Фаза 7: Documentation** — documenter.md

## Быстрый режим: `/pipeline fast`

**Для задач < 8 часов, багфиксов, рефакторинга.** Пропускает Research и Architecture.

```
Фаза 3: Planning (сокращённый)
  → Прочитать dds-guardrails SKILL.md ПЕРЕД планированием
  → Создать plan.md (без design-артефактов)
  → Перечислить конкретные файлы и изменения

Фаза 4: Implementation
  → Стандартный цикл: dev → review → fix
  → ОБЯЗАТЕЛЬНО: /validate-all после каждого этапа

Фаза 4+: Definition of Done
  → Проверить ВСЕ пункты DoD (см. ниже)
  → Если хоть один FAIL → fix → повторная проверка
```

## Definition of Done (обязателен для ВСЕХ режимов)

Перед завершением pipeline КАЖДАЯ задача должна пройти:

```
=== Definition of Done ===
- [ ] check_package: 0 errors
- [ ] check_code_consistency: 0 mismatches
- [ ] validate_guid_consistency: 0 duplicates
- [ ] check_resx: все Property_<Name>, все DisplayName
- [ ] dependency_graph: нет циклов
- [ ] validate_deploy: ready
- [ ] Code Review: 0 CRITICAL, 0 HIGH
- [ ] Security: 0 CRITICAL, 0 HIGH
- [ ] Все .cs — partial (кроме Constants)
- [ ] Подписи полей НЕ пустые
- [ ] Обложка модуля работает
```

Если Playwright MCP доступен — проверить последние 2 пункта автоматически. Иначе — отметить как "manual check required".

## Правила
- **GUARDRAILS FIRST** — ПЕРЕД любой фазой, работающей с DDS, прочитать `/dds-guardrails`. Это ОБЯЗАТЕЛЬНО. Не пропускать.
- **VALIDATE AFTER** — ПОСЛЕ каждой фазы, создающей/изменяющей артефакты, запустить `/validate-all`. Не переходить к следующей фазе при CRITICAL/HIGH.
- **DOD BEFORE DONE** — Pipeline НЕ считается завершённым пока ВСЕ пункты Definition of Done не пройдены.
- **BREAK THE LOOP** — Если fix не помог с первого раза, НЕ повторять. Искать другую причину. Запустить `/validate-all` для полной картины.
- **Артефакты сохраняются** — не пересоздавать существующие документы
- **Контекст передаётся** — каждый агент читает результаты предыдущих фаз
- **Трассируемость** — от задачи до кода
- **Автономность** — агенты работают самостоятельно внутри Task
- **Между фазами** — показать результат пользователю, спросить "Продолжить?"
- **status.md — source of truth** — обновлять после каждой фазы, читать первым при возобновлении
- **Assumptions явно** — все допущения записывать в status.md и plan.md, не прятать в тексте
- **Validation per milestone** — каждый этап плана имеет конкретные MCP-команды валидации
- **Stop-and-fix** — если валидация этапа провалилась, исправить ДО перехода к следующему
- **Fix-loop порог** — CRITICAL + HIGH блокируют. MEDIUM = рекомендации, не блокируют pipeline
- **GUID реестр** — все сгенерированные GUID записываются в status.md секцию `Used GUIDs`
- **MCP интеграция** — после каждой фазы вызывать соответствующую MCP-команду валидации

## Агенты (14 файлов в `.claude/agents/`)
| Фаза | Агент | Файл | Модель |
|------|-------|------|--------|
| 0. PRD | Product Owner | `product-owner.md` | **sonnet** |
| 1. Research | Исследователь | `researcher.md` | **sonnet** |
| 2. Design | Архитектор | `architect.md` | **opus** |
| 3. Plan | Планировщик | `planner.md` | **sonnet** |
| 4. Implement | Разработчик | `developer.md` | **opus** |
| 4. Implement | SPA Разработчик | `spa-developer.md` | **opus** |
| 4. Implement | API Разработчик | `api-developer.md` | **opus** |
| 4. Review | Ревью кода | `code-reviewer.md` | **sonnet** |
| 4. Review | Ревью безопасности | `security-reviewer.md` | **sonnet** |
| 4. Review | Ревью архитектуры | `architecture-reviewer.md` | **sonnet** |
| 4. Test | Тест-дизайнер & Исполнитель | `test-executor.md` | **sonnet** |
| 5. Final | Финальный ревьюер | `final-reviewer.md` | **sonnet** |
| 6. Build | Системный инженер | `system-engineer.md` | **sonnet** |
| 7. Docs | Документатор | `documenter.md` | **sonnet** |

### Внешние агенты (вне pipeline)
| Агент | Файл | Модель |
|-------|------|--------|
| Remote Component Dev | `remote-component-developer.md` | **opus** |

## GitHub Issues — интеграция

Pipeline автоматически отслеживает прогресс через `status.md`. Дополнительно:

1. **При старте pipeline** — создать issue (если не существует) или прикрепить к существующему
2. **После каждой фазы** — агент фазы добавляет комментарий к issue (см. секции GitHub Issues в каждом агенте)
3. **При APPROVED** — final-reviewer закрывает issue
4. **При ошибке** — debugger создаёт bug-issue если ошибка в MCP

**Issue номер** передаётся через `status.md` поле `GitHub Issue: #{N}` в секции Snapshot.

**API:**
```bash
# Создать issue
gh api repos/dunaevdmitriys-dev/directum-mcp-server/issues -f title="[pipeline] {название}" -f body="..."

# Комментарий
gh api repos/dunaevdmitriys-dev/directum-mcp-server/issues/{N}/comments -f body="..."

# Закрыть
gh api repos/dunaevdmitriys-dev/directum-mcp-server/issues/{N} -X PATCH -f state=closed
```
