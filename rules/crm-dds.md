---
paths:
  - "CRM/crm-package/**"
  - "**/source/**/*.mtd"
  - "**/source/**/*.resx"
  - "**/*.sds"
---

# DDS-разработка (Directum RX 26.1)

## ГЛАВНОЕ ПРАВИЛО
Перед созданием ЛЮБОЙ сущности — найди пример:
- `search_metadata name=<keyword>` или `extract_entity_schema entity=<name>`
- Reference-код: `docs/platform/REFERENCE_CODE.md`
- Гайды: `knowledge-base/guides/solutions-reference.md`

## Приоритет reference (от надёжного к рискованному)
1. **Платформенные модули** (base/Sungero.*) → `search_metadata` — эталон
2. **knowledge-base/guides/23_mtd_reference.md** — проверенные шаблоны
3. **MCP scaffold_*** — генерация из параметров
4. **CRM/crm-package/** — ⚠️ рабочий проект, НЕ эталон. Виджеты, обложки, RC могут быть сломаны. Бери структуру .mtd, не копируй логику слепо

## Known Issues (19 критических)
Полный список с решениями: `docs/platform/DDS_KNOWN_ISSUES.md`

Краткий чеклист:
1. DatabookEntry НЕ может иметь CollectionProperty → используй Document
2. System.resx: `Property_<Name>`, НЕ `Resource_<GUID>`
3. Enum values: НЕ C# reserved words
4. Code свойств: уникальные в рамках иерархии наследования
5. AttachmentGroup Constraints: одинаковые в Task/Assignment/Notice
6. FormTabs НЕ поддерживаются в DDS 25.3/26.1
7. Внешние библиотеки: через UI DDS, не в .csproj

## CRM-специфичные модули (если работаешь с CRM/crm-package/)
| Модуль | Сущности |
|--------|----------|
| DirRX.CRM | Фасад: обложка, отчёты, AsyncHandlers, Jobs, PublicStructures |
| DirRX.CRMSales | Deal, Pipeline, Stage, Activity, Product |
| DirRX.CRMMarketing | Lead (BANT), Campaign, LeadSource |
| DirRX.CRMDocuments | CommercialProposal, Invoice + Task/Assignment |
| DirRX.CRMCommon | HasCRMAccess(), IsCRMAdmin() |
| DirRX.Solution | 5 Remote Components |

Зависимости: `Solution → CRMCommon → CRMSales ↔ CRMMarketing → CRM → CRMDocuments`

## После КАЖДОГО изменения → `/validate-all`
