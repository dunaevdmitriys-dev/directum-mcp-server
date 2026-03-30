---
paths:
  - "CRM/crm-package/**"
  - "targets/source/**"
  - "omniapplied/source/**"
  - "**/source/**/*.mtd"
  - "**/source/**/*.resx"
  - "**/source/**/*.cs"
---

# Карта примеров DDS — где что лежать, чтобы не выдумывать

> Прежде чем создать что-то — открой пример из этой таблицы и адаптируй.

## Источники (по надёжности)

| # | Источник | Что внутри | Когда использовать |
|---|----------|-----------|-------------------|
| 1 | **Платформа** (base/Sungero.*) | Эталон | `search_metadata` — всегда первый |
| 2 | **targets/source/** | 5 модулей, 40 .mtd, 6 RC, 500+ .cs | Сложные сущности, KPI, виджеты, initializer |
| 3 | **omniapplied/source/** | 2 модуля, override Company | Паттерн перекрытия модуля |
| 4 | **CRM/crm-package/source/** | Рабочий проект | ⚠️ НЕ эталон, могут быть баги |

## Сущности (.mtd)

| Тип | Лучший пример | Путь |
|-----|---------------|------|
| **DatabookEntry** (простой) | LeadSource | `CRM/crm-package/source/DirRX.CRMMarketing/.../LeadSource/LeadSource.mtd` |
| **DatabookEntry** (сложный, эталон) | Period (18KB) | `targets/source/DirRX.DTCommons/.../Period/Period.mtd` |
| **DatabookEntry + NavigationProperty** | Deal | `CRM/crm-package/source/DirRX.CRMSales/.../Deal/Deal.mtd` |
| **DatabookEntry** (KPI-метрика, 35KB, эталон) | Metric | `targets/source/DirRX.KPI/.../Metric/Metric.mtd` |
| **Сущность** (46KB, эталон сложной) | Target | `targets/source/DirRX.Targets/.../Target/Target.mtd` |
| **Карта/иерархия** (39KB) | TargetsMap | `targets/source/DirRX.Targets/.../TargetsMap/TargetsMap.mtd` |
| **Document** | CommercialProposal | `CRM/crm-package/source/DirRX.CRMDocuments/.../CommercialProposal/CommercialProposal.mtd` |
| **Document (alt)** | Invoice | `CRM/crm-package/source/DirRX.CRMDocuments/.../Invoice/Invoice.mtd` |
| **Task** (с workflow, эталон) | MapApprovalTask (13KB) | `targets/source/DirRX.Targets/.../MapApprovalTask/MapApprovalTask.mtd` |
| **Task** | ProposalApprovalTask | `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalApprovalTask/ProposalApprovalTask.mtd` |
| **Assignment** | ProposalApprovalAssignment | `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalApprovalAssignment/ProposalApprovalAssignment.mtd` |
| **Notice** | ProposalNotice | `CRM/crm-package/source/DirRX.CRMDocuments/.../ProposalNotice/ProposalNotice.mtd` |
| **Коллекция** (KeyResults) | TargetKeyResults | `targets/source/DirRX.Targets/.../Target@KeyResults/TargetKeyResults.mtd` |
| **Коллекция** (Indicators) | PersonalKPIMapIndicators | `targets/source/DirRX.Targets/.../PersonalKPIMap@Indicators/PersonalKPIMapIndicators.mtd` |

## Модуль (Module.mtd)

| Что | Пример | Путь |
|-----|--------|------|
| **Module.mtd (эталон, 106KB)** | DirRX.Targets | `targets/source/DirRX.Targets/DirRX.Targets.Shared/Module.mtd` — AsyncHandlers, Cover, WebAPI, Jobs, PublicStructures |
| **Module.mtd (KPI, 66KB)** | DirRX.KPI | `targets/source/DirRX.KPI/DirRX.KPI.Shared/Module.mtd` — виджеты, структуры, async |
| **Module.mtd (Commons, 33KB)** | DirRX.DTCommons | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Shared/Module.mtd` |
| **Module.mtd (полный CRM)** | DirRX.CRM | `CRM/crm-package/source/DirRX.CRM/DirRX.CRM.Shared/Module.mtd` |
| **Module.mtd (чистый CRM)** | DirRX.CRMSales | `CRM/crm-package/source/DirRX.CRMSales/DirRX.CRMSales.Shared/Module.mtd` |
| **Override модуля** | Sungero.Omni → Company | `omniapplied/source/Sungero.Omni/.../Sungero.Company/Module.mtd` |
| **AsyncHandler** | DealStageChanged | `CRM/crm-package/source/DirRX.CRM/.../Module.mtd` строки 5-77 |
| **Cover (обложка)** | CRMSales cover | `CRM/crm-package/source/DirRX.CRMSales/.../Module.mtd` |
| **PublicStructures** | 22 DTO | `CRM/crm-package/source/DirRX.CRM/.../Module.mtd` |

## Ресурсы (.resx)

| Что | Пример | Путь |
|-----|--------|------|
| **System.resx (эталон)** | LeadSource | `CRM/crm-package/source/DirRX.CRMMarketing/.../LeadSource/LeadSourceSystem.ru.resx` |
| **Формат ключей** | `Property_Name`, `Property_Description`, `DisplayName`, `CollectionDisplayName` | |

## C# код

| Что | Пример | Путь |
|-----|--------|------|
| **ModuleInitializer (эталон, 29KB)** | DTCommons | `targets/source/DirRX.DTCommons/DirRX.DTCommons.Server/ModuleInitializer.cs` |
| **ModuleInitializer** | CRMDocuments | `CRM/crm-package/source/DirRX.CRMDocuments/.../ModuleInitializer.cs` |
| **ServerFunctions (эталон, 126KB)** | KPI | `targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleServerFunctions.cs` |
| **ServerFunctions (WebAPI, 30+ endpoints)** | CRM | `CRM/crm-package/source/DirRX.CRM/.../ModuleServerFunctions.cs` |
| **ModuleWidgetHandlers (эталон, 25KB)** | KPI | `targets/source/DirRX.KPI/DirRX.KPI.Server/ModuleWidgetHandlers.cs` |
| **ModuleAsyncHandlers (19KB)** | Targets | `targets/source/DirRX.Targets/DirRX.Targets.Server/ModuleAsyncHandlers.cs` |
| **PeriodServerFunctions (25KB)** | DTCommons | `targets/source/DirRX.DTCommons/.../Period/PeriodServerFunctions.cs` |
| **SharedFunctions + Structures** | DTCommons | `targets/source/DirRX.DTCommons/.../ModuleSharedFunctions.cs`, `ModuleStructures.cs` |
| **Override модуля** | Omni → Company | `omniapplied/source/Sungero.Omni/.../Sungero.Company/ModuleServerFunctions.cs` |
| **ServerFunctions (модуль)** | CRMDocuments | `CRM/crm-package/source/DirRX.CRMDocuments/.../ModuleServerFunctions.cs` |
| **ClientFunctions (модуль)** | CRMDocuments | `CRM/crm-package/source/DirRX.CRMDocuments/.../ModuleClientFunctions.cs` |
| **ServerFunctions (сущность)** | CommercialProposal | `CRM/crm-package/source/DirRX.CRMDocuments/.../CommercialProposal/CommercialProposalServerFunctions.cs` |
| **BeforeSave handler** | Deal | `CRM/crm-package/source/DirRX.CRMSales/.../Deal/DealHandlers.cs` |
| **AsyncHandler (модуль)** | DealStageChanged | `CRM/crm-package/source/DirRX.CRM/.../ModuleAsyncHandlers.cs` |
| **PublicFunctions [Remote]** | CRMSales (7 функций) | `CRM/crm-package/source/DirRX.CRMSales/.../ModuleServerFunctions.cs` |
| **Metric handlers (10KB)** | Metric | `targets/source/DirRX.KPI/.../Metric/MetricHandlers.cs` |
| **Constants (11KB)** | DTCommons | `targets/source/DirRX.DTCommons/.../ModuleConstants.cs` |

## Remote Components

| Что | Путь |
|-----|------|
| **Targets RC (6 компонентов, эталон)** | `targets/source/DirRX.DirectumTargets/DirRX.DirectumTargets.Components/` |
| — ChartsControl | `.../Components/ChartsControl/` — графики, диаграммы |
| — GoalsMap | `.../Components/GoalsMap/` — дерево целей (Cover+Card) |
| — TableControl | `.../Components/TableControl/` — таблица KPI |
| — PeriodControl | `.../Components/PeriodControl/` — селектор периода |
| — RichMarkdownEditor | `.../Components/RichMarkdownEditor/` — WYSIWYG (RXMD) |
| — AnalyticsControl | `.../Components/AnalyticsControl/` — аналитика |
| **Omni RC (Matrix)** | `omniapplied/source/Sungero.Omni/Sungero.Omni.Components/Matrix/` — чат с WASM |
| **CRM RC проект** | `CRM/crm-package/source/DirRX.CRM/DirRX-CRMComponents/` |
| **component.manifest.js** | `CRM/.../DirRX-CRMComponents/component.manifest.js` |
| **Loader (Cover)** | `CRM/.../DirRX-CRMComponents/src/loaders/pipeline-kanban-loader.tsx` |
| **Loader (Card)** | `CRM/.../DirRX-CRMComponents/src/loaders/customer360-loader.tsx` |
| **Компонент (React)** | `CRM/.../DirRX-CRMComponents/src/controls/pipeline-kanban/PipelineKanban.tsx` |

## Отчёты

| Что | Путь |
|-----|------|
| **Report .mtd** | `CRM/crm-package/source/DirRX.CRM/.../Reports/SalesFunnelReport/SalesFunnelReport.mtd` |
| **Report .frx (FastReport)** | `CRM/.../DirRX.CRM.Server/Reports/DealsByManagerReport/DealsByManagerReport.frx` |

## Knowledge Base гайды (когда нет примера в коде)

| Задача | Гайд | Что внутри |
|--------|------|-----------|
| 18 copy-paste рецептов | `19_cookbook.md` | Create, Remote, Task, SQL, Dialog, Lock, Filter, Email, Log, Index |
| C# handlers (все типы) | `25_code_patterns.md` | BeforeSave, AfterSave, Refresh, Showing, AsyncHandler, Job, Initializer |
| .mtd JSON шаблоны | `23_mtd_reference.md` | Databook, Document, Task templates + ancestor GUIDs |
| Event lifecycle | `11_events_lifecycle.md` | Полная таблица + порядок выполнения |
| Production-паттерны | `solutions-reference.md` | AgileBoard (WebAPI), Targets (RC, XLSX), ESM (SLA, AI), CRM (pipeline) |

## RC Plugin Components

| Что | Пример | Путь |
|-----|--------|------|
| Microservice Plugin (BaseComponent) | ChatBot | chatbot/chatbot_plugin/chatbot_component.py |
| Applied Solution Plugin (BaseRnDComponent) | OmniApplied | omniapplied/omni_plugin/omni_installer.py |
| Applied Solution Plugin (with deps) | Targets | targets/targets_plugin/targets_installer.py |
| DotNetServiceBase | ChatBot service | chatbot/chatbot_plugin/chatbot_service.py |
| Config generation (wrap_variable) | ChatBot defaults | chatbot/chatbot_plugin/default_settings.py |
| JSON Schema UI | ChatBot schema | chatbot/chatbot_plugin/schema/ |
| Config mutation | Omni update | omniapplied/omni_plugin/omni_update_config_settings.py |
| Publish/packaging | ChatBot publish | chatbot/chatbot_plugin/publish.py |
| Plugin i18n (.po) | ChatBot translations | chatbot/chatbot_plugin/translations/ |
