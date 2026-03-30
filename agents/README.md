# Pipeline Agents Reference

## Quick Reference

| Agent | Phase | File | When to Use |
|-------|-------|------|-------------|
| Product Owner | 0 - PRD | `product-owner.md` | Превращает размытый запрос в PRD (Product Requirements Document) |
| Researcher | 1 - Research | `researcher.md` | Сбор фактов: BaseGuid, паттерны через MCP: search_metadata, ограничения платформы |
| Architect | 2 - Design | `architect.md` | C4-модель, domain model, API contracts, ADR, test strategy |
| Planner | 3 - Plan | `planner.md` | Детальный план реализации по этапам + тест-план |
| Developer | 4 - Implement | `developer.md` | Генерация кода Directum RX: .mtd, .cs, .resx |
| SPA Developer | 4 - Implement | `spa-developer.md` | React SPA (Vite + Ant Design + Zustand) |
| API Developer | 4 - Implement | `api-developer.md` | Standalone .NET Minimal API (Npgsql, прямой SQL) |
| Remote Component Dev | 4 - Implement | `remote-component-developer.md` | React-компоненты через Webpack Module Federation |
| Code Reviewer | 4 - Review | `code-reviewer.md` | Качество кода: запрещенные паттерны, метрики, API contracts |
| Security Reviewer | 4 - Review | `security-reviewer.md` | SQL-инъекции, секреты, Server/Client разделение |
| Architecture Reviewer | 4 - Review | `architecture-reviewer.md` | Соответствие кода архитектуре из Phase 2 |
| Test Executor | 4 - Test | `test-executor.md` | Структурные тесты, тест-кейсы, Playwright E2E |
| Debugger | any | `debugger.md` | Диагностика ошибок: компиляция, DDS-импорт, runtime |
| System Engineer | any | `system-engineer.md` | Сборка .dat, публикация DeploymentTool, управление стендом |
| Documenter | post | `documenter.md` | Генерация технической документации по решению |
| Final Reviewer | 5 - Final | `final-reviewer.md` | Финальная проверка: все scores, трассируемость, вердикт |

## Pipeline Flow

```
Phase 0          Phase 1          Phase 2          Phase 3
+-----------+    +-----------+    +-----------+    +-----------+
| Product   | -> | Researcher| -> | Architect | -> | Planner   |
| Owner     |    |           |    |           |    |           |
| (PRD)     |    | (Facts)   |    | (Design)  |    | (Plan)    |
+-----------+    +-----------+    +-----------+    +-----------+
                                                        |
                                                        v
Phase 4: Implementation
+---------------------------------------------------------------+
|                                                               |
|  +------------+  +---------------+  +---------------+         |
|  | Developer  |  | SPA Developer |  | API Developer |         |
|  | (RX code)  |  | (React SPA)  |  | (.NET API)    |         |
|  +-----+------+  +------+-------+  +------+--------+         |
|        |                 |                 |                   |
|        +--------+--------+---------+-------+                  |
|                 |                   |                          |
|                 v                   v                          |
|  +------------------+  +-------------------+                  |
|  | Remote Component |  | System Engineer   |                  |
|  | Developer        |  | (build & deploy)  |                  |
|  +------------------+  +-------------------+                  |
|                                                               |
|  Review (parallel):                                           |
|  +---------------+  +------------------+  +----------------+  |
|  | Code Reviewer |  | Security Reviewer|  | Arch Reviewer  |  |
|  +-------+-------+  +--------+---------+  +-------+--------+  |
|          |                    |                    |           |
|          +--------------------+--------------------+           |
|                               |                               |
|                               v                               |
|                    +-------------------+                      |
|                    | Test Executor     |                      |
|                    | (tests & E2E)     |                      |
|                    +-------------------+                      |
+---------------------------------------------------------------+
                                |
                                v
Phase 5
+-----------+
| Final     |
| Reviewer  |
| (Verdict) |
+-----------+

Support agents (any phase):
  - Debugger: diagnostics & error resolution
  - System Engineer: build, deploy, infrastructure
  - Documenter: technical documentation
```

## Artifacts by Phase

| Phase | Directory | Key Files |
|-------|-----------|-----------|
| 0 | `.pipeline/00-prd/` | `prd.md` |
| 1 | `.pipeline/01-research/` | `research.md` |
| 2 | `.pipeline/02-design/` | `c4-*.md`, `domain-model.md`, `api-contracts.md`, `test-strategy.md`, `adr/` |
| 3 | `.pipeline/03-plan/` | `plan.md`, `test-plan.md` |
| 4 | `.pipeline/04-implementation/` | `code-review.md`, `security-review.md`, `architecture-review.md`, `test-results.md`, `changelog.md`, `e2e/` |
| 5 | `.pipeline/05-final-review/` | `final-report.md` |

## Notes

- Pipeline skill (`/pipeline`) handles model selection -- agents do NOT specify models
- GitHub API calls use `{GITHUB_OWNER}/{GITHUB_REPO}` placeholders
- Knowledge base references: `knowledge-base/guides/`
- Platform version: Directum RX 26.1
