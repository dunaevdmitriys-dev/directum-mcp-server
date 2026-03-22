using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Resources;

[McpServerResourceType]
public class PlatformKnowledgeBase
{
    // === EXISTING RESOURCES (5) ===

    [McpServerResource(UriTemplate = "directum://knowledge/platform-rules", Name = "Platform Rules", MimeType = "text/plain")]
    [Description("Правила и ограничения Directum RX DDS 25.3 — обязательны к соблюдению при генерации кода")]
    public static string GetPlatformRules() => PlatformRules;

    [McpServerResource(UriTemplate = "directum://knowledge/entity-types", Name = "Entity Types", MimeType = "text/plain")]
    [Description("Типы сущностей Directum RX — когда какой использовать, BaseGuid, ограничения")]
    public static string GetEntityTypes() => EntityTypes;

    [McpServerResource(UriTemplate = "directum://knowledge/resx-conventions", Name = "Resx Conventions", MimeType = "text/plain")]
    [Description("Конвенции именования ключей в .resx файлах Directum RX")]
    public static string GetResxConventions() => ResxConventions;

    [McpServerResource(UriTemplate = "directum://knowledge/module-guids", Name = "Module GUIDs", MimeType = "text/plain")]
    [Description("GUID платформенных модулей и базовых типов сущностей для Dependencies")]
    public static string GetModuleGuids() => ModuleGuids;

    [McpServerResource(UriTemplate = "directum://knowledge/csharp-patterns", Name = "CSharp Patterns", MimeType = "text/plain")]
    [Description("Правила C# кода для Directum RX — partial class, namespace, forbidden patterns")]
    public static string GetCsharpPatterns() => CsharpPatterns;

    // === NEW RESOURCES v2.0 (7) ===

    [McpServerResource(UriTemplate = "directum://knowledge/module-catalog", Name = "Module Catalog", MimeType = "text/plain")]
    [Description("Полный каталог 30 платформенных модулей Directum RX v25.3 — имена, GUID, сущности, зависимости")]
    public static string GetModuleCatalog() => ModuleCatalog;

    [McpServerResource(UriTemplate = "directum://knowledge/property-types", Name = "Property Types", MimeType = "text/plain")]
    [Description("Все типы свойств, DataBinder'ов, Controls и Form'ов в Directum RX — полный справочник")]
    public static string GetPropertyTypes() => PropertyTypes;

    [McpServerResource(UriTemplate = "directum://knowledge/csharp-functions", Name = "CSharp Function Patterns", MimeType = "text/plain")]
    [Description("Паттерны серверных/клиентских функций, атрибуты [Public]/[Remote], ModuleInitializer")]
    public static string GetCsharpFunctions() => CsharpFunctions;

    [McpServerResource(UriTemplate = "directum://knowledge/workflow-patterns", Name = "Workflow Patterns", MimeType = "text/plain")]
    [Description("Паттерны workflow: блоки, RouteScheme, Task/Assignment/Notice, обработчики событий")]
    public static string GetWorkflowPatterns() => WorkflowPatterns;

    [McpServerResource(UriTemplate = "directum://knowledge/solution-design", Name = "Solution Design Guide", MimeType = "text/plain")]
    [Description("Руководство по проектированию решений: CRM, ESM, HR — архитектурные решения, выбор типов, паттерны")]
    public static string GetSolutionDesign() => SolutionDesign;

    [McpServerResource(UriTemplate = "directum://knowledge/cover-widgets", Name = "Cover & Widgets", MimeType = "text/plain")]
    [Description("Обложки модулей (Cover), виджеты (Widgets), Remote Components — форматы и примеры")]
    public static string GetCoverWidgets() => CoverWidgets;

    [McpServerResource(UriTemplate = "directum://knowledge/initializer-guide", Name = "Initializer Guide", MimeType = "text/plain")]
    [Description("Полное руководство по ModuleInitializer: роли, права, справочники, SQL, версионная инициализация")]
    public static string GetInitializerGuide() => InitializerGuide;

    [McpServerResource(UriTemplate = "directum://knowledge/integration-patterns", Name = "Integration Patterns", MimeType = "text/plain")]
    [Description("Интеграция Directum RX: WebAPI, OData, AsyncHandlers, обмен документами, 1С, внешние системы")]
    public static string GetIntegrationPatterns() => IntegrationPatterns;

    [McpServerResource(UriTemplate = "directum://knowledge/report-patterns", Name = "Report Patterns", MimeType = "text/plain")]
    [Description("Отчёты Directum RX: ReportMetadata, FastReport .frx, Queries.xml, обработчики, параметры")]
    public static string GetReportPatterns() => ReportPatterns;

    // === v3.0 RESOURCES ===

    [McpServerResource(UriTemplate = "directum://knowledge/entity-catalog", Name = "Entity Catalog", MimeType = "text/plain")]
    [Description("ПОЛНЫЙ каталог сущностей платформы: GUID, свойства, связи, примеры использования. ЧИТАЙ ПЕРЕД СОЗДАНИЕМ ЛЮБОЙ СУЩНОСТИ.")]
    public static string GetEntityCatalog() => EntityCatalog;

    [McpServerResource(UriTemplate = "directum://knowledge/solutions-reference", Name = "Solutions Reference", MimeType = "text/plain")]
    [Description("30+ паттернов из 4 production-решений (Agile, Targets, ESM, CRM): WebAPI, RC, Email, SLA, AI, Round-robin, XLSX, Word")]
    public static string GetSolutionsReference() => SolutionsReference;

    [McpServerResource(UriTemplate = "directum://knowledge/dds-known-issues", Name = "DDS Known Issues", MimeType = "text/plain")]
    [Description("18 известных проблем DDS 25.3: ошибки импорта, билда, публикации. Причины + fix.")]
    public static string GetDdsKnownIssues() => DdsKnownIssues;

    [McpServerResource(UriTemplate = "directum://knowledge/dev-environments", Name = "Dev Environments", MimeType = "text/plain")]
    [Description("DDS vs CrossPlatform DS: сравнение сред разработки, автоматизация через HTTP API, выбор для Claude Code")]
    public static string GetDevEnvironments() => DevEnvironments;

    [McpServerResource(UriTemplate = "directum://knowledge/standalone-setup", Name = "Standalone Setup", MimeType = "text/plain")]
    [Description("Автономная установка стенда: 4 уровня, команды DeploymentTool, требования, цикл разработки")]
    public static string GetStandaloneSetup() => StandaloneSetup;

    [McpServerResource(UriTemplate = "directum://knowledge/architecture-patterns", Name = "Architecture Patterns", MimeType = "text/plain")]
    [Description("14 production-паттернов из ESM, Agile, Targets: WebAPI, AsyncHandler, DTO, Position, SoftDelete, IsolatedAreas")]
    public static string GetArchitecturePatterns() => ArchitecturePatterns;

    [McpServerResource(UriTemplate = "directum://knowledge/ui-catalog", Name = "UI & Reports Catalog", MimeType = "text/plain")]
    [Description("Каталог UI-контролов, Remote Components, библиотек, FastReport шаблонов, Aspose для импорта/экспорта")]
    public static string GetUiCatalog() => UiCatalog;

    // === v4.0 RESOURCES ===

    [McpServerResource(UriTemplate = "directum://knowledge/crm-patterns", Name = "CRM Patterns", MimeType = "text/plain")]
    [Description("Паттерны CRM-решения: Deal, Lead, Pipeline, BANT scoring, Round-robin, JSON serialization, GUID'ы сущностей")]
    public static string GetCrmPatterns() => CrmPatterns;

    [McpServerResource(UriTemplate = "directum://knowledge/esm-patterns", Name = "ESM Patterns", MimeType = "text/plain")]
    [Description("Паттерны ESM/Service Desk: Email-to-Ticket, SLA 4 режима, матричная приоритизация, AIAgentTool, ExpressionElement, AsyncHandlers, Jobs")]
    public static string GetEsmPatterns() => EsmPatterns;

    [McpServerResource(UriTemplate = "directum://knowledge/targets-patterns", Name = "Targets Patterns", MimeType = "text/plain")]
    [Description("Паттерны Targets/KPI: RemoteTableControl, Fan-out Async, Licensing, XLSX Pipeline, Word Processing, RC Components, WebAPI")]
    public static string GetTargetsPatterns() => TargetsPatterns;

    // =====================================================================
    // CONTENT
    // =====================================================================

    private const string PlatformRules = """
        # Directum RX DDS 25.3 — Критические правила

        ОБЯЗАТЕЛЬНО соблюдай ВСЕ правила при генерации .mtd, .resx, .cs файлов.

        ## Структурные правила
        1. CollectionPropertyMetadata в DatabookEntry — ЗАПРЕЩЕНО. Ошибка: "Missing area". Используй Document.
        2. NavigationProperty EntityGuid ОБЯЗАН быть из модуля в Dependencies. Циклы запрещены.
        3. AttachmentGroup Constraints у Task/Assignment/Notice ОБЯЗАНЫ совпадать. Безопасно: "Constraints": [].
        4. Enum values НЕ могут быть C# reserved words (new, event, class, default, string, int, bool, object, base, this, null, true, false, is, as, var и др.).
        5. Code свойств уникален в иерархии наследования. Префикс модуля: CrmDeal, не Deal.
        6. .sds/Libraries/Analyzers/ ОБЯЗАНА существовать с DLL.
        7. File locks после неудачного импорта → перезапусти DDS.

        ## Ресурсные правила
        8. System.resx ключи ТОЛЬКО: Property_<Name>, Action_<Name>, Enum_<EnumName>_<Value>. ЗАПРЕЩЕНО: Resource_<GUID>.
        9. КАЖДЫЙ элемент ОБЯЗАН иметь DisplayName в *System.ru.resx:
           - Модули: DisplayName в ModuleSystem.ru.resx
           - Сущности: DisplayName + CollectionDisplayName
           - Свойства: Property_<Name>
           - Действия: Action_<Name>
           - Cover: CoverGroup_<Name>, CoverAction_<Name>
           - Jobs: Job_<Name>
           - AsyncHandlers: AsyncHandler_<Name> (опционально)

        ## Код и функции
        10. CoverFunctionAction FunctionName ТОЧНО = имя метода в ModuleClientFunctions.cs.
        11. Библиотеки только через DDS UI "Сторонние библиотеки", НЕ .csproj. Имя без точек: NewtonsoftJson.
        12. Overridden: ["Controls"] + Controls: [] = ПУСТАЯ ФОРМА. Либо не перекрывай, либо заполни Controls.
        13. PublicStructures свойства ТОЛЬКО в Module.mtd → Properties[], НЕ в ModuleStructures.cs.
        14. Actions: DisplayName, Description, Область (Card/Collection) — обязательны.
        15. FormTabs НЕ поддерживаются в DDS 25.3.
        16. Versions обязательны: [{"Type":"EntityMetadata","Number":13},{"Type":"DomainApi","Number":2}]

        ## Версии метаданных DDS 25.3
        - ModuleMetadata: Number = 12
        - EntityMetadata: Number = 13
        - DomainApi: Number = 3
        """;

    private const string EntityTypes = """
        # Типы сущностей Directum RX

        ## Дерево решений: какой тип выбрать?

        ### DatabookEntry (Справочник)
        Когда: простые данные без workflow, без файлов, без дочерних коллекций
        Примеры: Сотрудник, Город, Валюта, Должность, Вид документа, Причина проигрыша
        BaseGuid: 04581d26-0780-4cfd-b3cd-c2cafc5798b0
        $type: Sungero.Metadata.EntityMetadata, Sungero.Metadata
        ЗАПРЕТ: CollectionPropertyMetadata (дочерние коллекции)
        Можно: любые свойства кроме коллекций

        ### Document (Документ, наследник OfficialDocument)
        Когда: нужны файлы, версии, подписи, дочерние коллекции, жизненный цикл
        Примеры: Договор, Приказ, Входящее письмо, Заявка, Акт
        BaseGuid: 58cca102-1e97-4f07-b6ac-fd866a8b7cb1
        $type: Sungero.Metadata.EntityMetadata, Sungero.Metadata
        Можно: CollectionPropertyMetadata, файлы, LifeCycleState

        ### Task (Задача)
        Когда: нужен workflow с маршрутизацией, заданиями, блоками
        Примеры: Задача на согласование, Задача по исполнению поручения, Задача на отсутствие
        BaseGuid: d795d1f6-45c1-4e5e-9677-b53fb7280c7e
        $type: Sungero.Metadata.TaskMetadata, Sungero.Workflow.Shared
        Всегда создавай вместе с Assignment и Notice

        ### Assignment (Задание)
        Когда: часть Task, конкретное задание исполнителю
        BaseGuid: 91cbfdc8-5d5d-465e-95a4-3a987e1a0c24
        $type: Sungero.Metadata.AssignmentMetadata, Sungero.Workflow.Shared

        ### Notice (Уведомление)
        Когда: информирование без требования действия
        BaseGuid: 4e09273f-8b3a-489e-814e-a4ebfbba3e6c
        $type: Sungero.Metadata.NoticeMetadata, Sungero.Workflow.Shared

        ## Фиксированные GUID (НЕ генерировать!)
        FilterPanel DatabookEntry: b0125fbd-3b91-4dbb-914a-689276216404
        FilterPanel Document: 80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f
        Card Form Document: fa03f748-4397-42ef-bdc2-22119af7bf7f
        CreationArea DatabookEntry: f7766750-eee2-4fcd-8003-5c06a90d1f44
        Status property: 1dcedc29-5140-4770-ac92-eabc212326a1

        ## Обязательные секции в КАЖДОЙ .mtd
        - Versions: [{"Type":"EntityMetadata","Number":13},{"Type":"DomainApi","Number":2}]
        - CreationAreaMetadata, FilterPanel
        - Forms (для DatabookEntry/Document)
        - Actions, Properties
        - RibbonCardMetadata, RibbonCollectionMetadata (IsAncestorMetadata: true)
        """;

    private const string ResxConventions = """
        # .resx файлы Directum RX — полное руководство

        ## Два типа файлов для каждой сущности:
        - Entity.resx / Entity.ru.resx — пользовательские ресурсы (ResourcesKeys в .mtd)
        - EntitySystem.resx / EntitySystem.ru.resx — системные ресурсы (подписи полей, действий)

        ## Ключи в System.resx (конвенция платформы):
        - Свойства: Property_<PropertyName>  (Property_Name, Property_TIN)
        - Действия: Action_<ActionName>
        - Перечисления: Enum_<EnumName>_<Value>
        - Группы контролов: ControlGroup_<GUID>
        - Формы: Form_<GUID>
        - Ленты: Ribbon_<Name>_<GUID>
        - Панели фильтров: FilterPanel_<Name>_<GUID>
        - DisplayName и CollectionDisplayName — без префикса
        - ЗАПРЕЩЕНО: Resource_<GUID> — runtime НЕ резолвит

        ## Ключи в Module System.resx:
        - Модуль: DisplayName
        - Cover Tab: CoverTab_<TabName>
        - Cover Group: CoverGroup_<GroupName>
        - Cover Action: CoverAction_<ActionName>
        - Job: Job_<JobName>, Job_<JobName>_Description
        - Role: RoleName_<RoleName>, RoleDescription_<RoleName>

        ## Обязательные resheader:
        resmimetype=text/microsoft-resx, version=2.0
        reader=System.Resources.ResXResourceReader, Version=4.0.0.0
        writer=System.Resources.ResXResourceWriter, Version=4.0.0.0

        ## XSD схема:
        Каждый .resx начинается с xsd:schema id="root" — копируй из платформенного примера.
        """;

    private const string ModuleGuids = """
        # GUID модулей и сущностей Directum RX v25.3 — ПОЛНЫЙ СПРАВОЧНИК

        ## ВСЕ 30 модулей платформы (для Dependencies в Module.mtd)
        | Модуль | GUID | Описание |
        |--------|------|----------|
        | Sungero.Shell | fcc573ab-5f4e-4b20-88e8-7b1e11a7a59a | Ядро UI, навигация |
        | Sungero.DirectumRX | e4fe1153-919e-4732-aadc-2c8e9b5c0b5a | Главный модуль, виджеты |
        | Sungero.Commons | 459fa497-ee5b-49a4-9980-de00cada9b7a | Справочники (город, страна, валюта) |
        | Sungero.Company | d534e107-a54d-48ec-85ff-bc44d731a82f | Организационная структура |
        | Sungero.Parties | 243b34ec-8425-4c7e-b66f-27f7b9c8f38d | Контрагенты |
        | Sungero.Docflow | df83a2ea-8d43-4ec4-a34a-2e61863014df | Документооборот (211 сущностей) |
        | Sungero.DocflowApproval | 62cf51a2-6371-4c12-9dec-68113862d5e1 | Расширенное согласование |
        | Sungero.Contracts | f9d15b1c-2784-4c84-8348-1e162d70b988 | Договоры |
        | Sungero.ContractsUI | 3c8b7d3a-187d-4445-8a8c-1d00ece44556 | UI договоров |
        | Sungero.RecordManagement | 4e25caec-c722-4740-bcfd-c4f803840ac6 | Делопроизводство |
        | Sungero.RecordManagementUI | 51247c94-981f-4bc8-819a-128704b5aa31 | UI делопроизводства |
        | Sungero.FinancialArchive | 59797aba-7718-45df-8ac1-5bb7a36c7a66 | Финансовый архив |
        | Sungero.FinancialArchiveUI | e99ae7e2-edb7-4904-a19a-4577f07609a4 | UI финансового архива |
        | Sungero.Exchange | cec41b99-da21-422f-9332-0fbc423e95c0 | Обмен документами |
        | Sungero.ExchangeCore | bc0d1897-640a-4b4d-a43a-a23c5984a009 | Ядро обмена |
        | Sungero.ExchangeCoreDiadoc | 30083842-5a15-4efb-9cab-0b61b1760157 | Интеграция с Диадок |
        | Sungero.ExchangeCoreSbis | d764569f-fa35-48be-aec9-d337b185d47a | Интеграция с СБИС |
        | Sungero.Integration1C | f7b1d5b7-5af1-4a9f-b4d7-4e18840d7195 | Интеграция с 1С |
        | Sungero.IntegrationDcs | 9ebe1af0-4286-4ab6-8975-68040cfa3e91 | Интеграция с DCS |
        | Sungero.IntegrationAIAgent | 2c67741c-79b4-41df-951f-decc45f63e08 | AI-агенты |
        | Sungero.Intelligence | e08dc659-2828-4d50-b90d-7d06408ab7cb | AI-обработка |
        | Sungero.Projects | 356e6500-45bc-482b-9791-189b5adedc28 | Проекты |
        | Sungero.Meetings | 593dcc11-15ee-49f2-b4ef-bf4cf7867055 | Совещания |
        | Sungero.MeetingsUI | 6ea9a047-b597-42eb-8f90-da8c559dd057 | UI совещаний |
        | Sungero.InternalPolicies | 48c9a380-db0e-47ca-ae0b-4015bbced723 | ЛНА, положения |
        | Sungero.InternalPoliciesUI | fc7d414c-a708-4d06-898e-e839ccd4d720 | UI ЛНА |
        | Sungero.SmartProcessing | bb685d97-a673-42ea-8605-66889967467f | Интеллектуальная обработка |
        | Sungero.MobileApps | 1a7ef5ec-c6f4-47df-98c1-b3eae77dabae | Мобильные приложения |
        | Sungero.PowerOfAttorneyCore | 1ecb3185-14ae-422d-99c6-babcf2ab059f | МЧД |
        | Sungero.PowerOfAttorneyKontur | a37bcb31-c5ce-4052-af97-ab7cbd19bf27 | МЧД Контур |

        ## Ключевые сущности (для NavigationProperty EntityGuid)
        | Сущность | GUID | Модуль |
        |----------|------|--------|
        | Employee | b7905516-2be5-4931-961c-cb38d5677565 | Company |
        | Department | 61b1c19f-26e2-49a5-b3d3-0d3618151e12 | Company |
        | BusinessUnit | eff95720-181f-4f7d-892d-dec034c7b2ab | Company |
        | JobTitle | 4a37aec4-764c-4c14-8887-e1ecafa5b4c5 | Company |
        | ManagersAssistant | c2200a86-5d5d-47d6-930d-c3ce8b11f04b | Company |
        | Counterparty | 294767f1-009f-4fbd-80fc-f98c49ddc560 | Parties |
        | Company(Org) | 593e143c-616c-4d95-9457-fd916c4aa7f8 | Parties |
        | Person | f5509cdc-ac0c-4507-a4d3-61d7a0a9b6cf | Parties |
        | Contact | c8daaef9-a679-4a29-ac01-b93c1637c72e | Parties |
        | Bank | 80c4e311-e95f-449b-984d-1fd540b8f0af | Parties |
        | City | 3a0c21f8-aa88-429c-891f-56c24d56fcef | Commons |
        | Country | 1f612925-95fc-4662-807d-c92c810a62b1 | Commons |
        | Region | 4efe2fa9-b1d1-4b47-b366-4128fe0a391c | Commons |
        | Currency | ffc2629f-dc30-4106-a3ce-c402ae7d32b9 | Commons |
        | VatRate | fe0ed345-5965-40d6-a559-24dcde189a95 | Commons |
        | OfficialDocument | 58cca102-1e97-4f07-b6ac-fd866a8b7cb1 | Docflow |
        | DocumentKind | 14a59623-89a2-4ea8-b6e9-2ad4365f358c | Docflow |
        | ApprovalTask | 100950d0-03d2-44f0-9e31-f9c8dfdf3829 | Docflow |
        | ContractualDocument | 454df3c6-b850-47cf-897f-a10d767baa77 | Contracts |
        | ActionItemExecutionTask | (в RecordManagement) | RecordManagement |
        | Absence | 40630200-4431-4021-ac0c-1199831bc7ad | Company |

        ## Типовые Dependencies для кастомного модуля
        Минимум: Sungero.Company (d534e107) — сотрудники
        + Документы: Sungero.Docflow (df83a2ea)
        + Контрагенты: Sungero.Parties (243b34ec)
        + Договоры: Sungero.Contracts (f9d15b1c)
        + Обмен: Sungero.ExchangeCore (bc0d1897)
        """;

    private const string CsharpPatterns = """
        # C# правила Directum RX — полное руководство

        ## Общие правила
        - Все классы partial (кроме Constants)
        - ModuleInitializer: public partial class без базового класса
        - Namespace: Server/ → .Server, ClientBase/ → .Client (НЕ .ClientBase!), Shared/ → .Shared
        - Handlers namespace: {CompanyCode}.{ModuleName} (без суффикса)

        ## Запрещённые паттерны
        - DateTime.Now → используй Calendar.Now (серверное время с учётом часового пояса)
        - System.Threading → используй AsyncHandlers
        - System.Reflection → запрещено в production коде
        - Session.Execute → используй Sungero.Docflow.PublicFunctions.Module.ExecuteSQLCommand()
        - new Tuple → используй Structures (PublicStructures в Module.mtd)
        - is/as → используй Entities.Is() / Entities.As()

        ## WebAPI
        - [Public(WebApiRequestType = RequestType.Post)] — POST эндпоинт
        - [Public(WebApiRequestType = RequestType.Get)] — GET эндпоинт
        - GET: только примитивные параметры (string, int, long)
        - POST: структуры через JSON body

        ## События сущности (HandledEvents)
        - Серверные: BeforeSaveServer, AfterSaveServer, CreatedServer, BeforeDeleteServer, AfterDeleteServer
        - Клиентские: ShowingClient, RefreshClient, ClosingClient
        - Свойства: {PropertyName}ChangedShared, {PropertyName}ValueInputClient

        ## Обязательные using (для серверного кода)
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Sungero.Core;
        using Sungero.CoreEntities;

        ## Расширенные using (для модулей с документами)
        using Sungero.Company;
        using Sungero.Docflow;
        using Sungero.Domain.Shared;
        using Sungero.Workflow;
        """;

    // =====================================================================
    // NEW v2.0 RESOURCES
    // =====================================================================

    private const string ModuleCatalog = """
        # Каталог модулей Directum RX v25.3 — 30 модулей

        ## Ядро платформы
        - Sungero.Shell — ядро UI, обложки, навигация
        - Sungero.DirectumRX — основной модуль, виджеты рабочего стола
        - Sungero.Commons — общие справочники (город, страна, валюта, AI Agent)
        - Sungero.Company — организационная структура (сотрудник, подразделение, должность, НОР)
        - Sungero.Parties — контрагенты (организация, персона, контакт, банк)

        ## Документооборот
        - Sungero.Docflow — ядро документооборота (211 сущностей!): документы, согласование, правила, реестры
        - Sungero.DocflowApproval — расширенное согласование
        - Sungero.RecordManagement — делопроизводство (71 сущность): поручения, журналы, дела
        - Sungero.Contracts — договоры (39 сущностей): контракт, допсоглашение, счёт
        - Sungero.FinancialArchive — финансовые документы (44 сущности): УПД, счёт-фактура

        ## Интеграция
        - Sungero.Exchange — обмен документами (EDI)
        - Sungero.ExchangeCore — ядро обмена
        - Sungero.ExchangeCoreDiadoc — интеграция с Диадок
        - Sungero.ExchangeCoreSbis — интеграция с СБИС
        - Sungero.Integration1C — интеграция с 1С (WebAPI endpoints)
        - Sungero.IntegrationDcs — интеграция с СКД

        ## Дополнительные модули
        - Sungero.Projects — управление проектами (18 сущностей)
        - Sungero.Meetings — совещания (8 сущностей)
        - Sungero.InternalPolicies — ЛНА, положения (11 сущностей)
        - Sungero.SmartProcessing — интеллектуальная обработка (13 сущностей)
        - Sungero.Intelligence — AI-обработка
        - Sungero.IntegrationAIAgent — AI-агенты
        - Sungero.MobileApps — мобильные приложения
        - Sungero.PowerOfAttorneyCore — МЧД (машиночитаемые доверенности)

        ## UI модули (только обложки, без сущностей)
        - Sungero.ContractsUI, Sungero.FinancialArchiveUI
        - Sungero.InternalPoliciesUI, Sungero.MeetingsUI
        - Sungero.RecordManagementUI

        ## Какой модуль взять как reference?
        - Простой справочник → Sungero.Company (Employee)
        - Документ → Sungero.Contracts (ContractualDocument)
        - Task + workflow → Sungero.RecordManagement (ActionItemExecutionTask)
        - Согласование → Sungero.Docflow (ApprovalTask)
        - WebAPI → Sungero.Integration1C
        - AI → Sungero.Intelligence
        - Обложка → любой модуль, все имеют Cover
        """;

    private const string PropertyTypes = """
        # Типы свойств, DataBinder'ов и Controls в Directum RX

        ## Типы свойств (Properties $type в .mtd)
        | Тип | Описание | DataBinder |
        |-----|----------|------------|
        | StringPropertyMetadata | Строка (одна) | StringEditorToStringBinder |
        | TextPropertyMetadata | Многострочный текст | TextEditorToTextBinder |
        | IntegerPropertyMetadata | Целое число 32-бит | NumericEditorToIntAndDoubleBinder |
        | LongIntegerPropertyMetadata | Целое число 64-бит | NumericEditorToIntAndDoubleBinder |
        | DoublePropertyMetadata | Дробное число | NumericEditorToIntAndDoubleBinder |
        | BooleanPropertyMetadata | Да/Нет | BooleanEditorToBooleanBinder |
        | DateTimePropertyMetadata | Дата/Время | DateTimeEditorToDateTimeBinder |
        | NavigationPropertyMetadata | Ссылка на сущность | DropDownEditorToNavigationBinder |
        | EnumPropertyMetadata | Перечисление | DropDownEditorToEnumerationBinder |
        | CollectionPropertyMetadata | Дочерняя коллекция | GridControlToChildCollectionBinder |
        | ImagePropertyMetadata | Изображение | ImageEditorToImageBinder |
        | BinaryDataPropertyMetadata | Бинарные данные | — |

        ## Типы форм
        | Тип | Когда |
        |-----|-------|
        | StandaloneFormMetadata | Для DatabookEntry, Document |
        | WorkflowEntityStandaloneFormMetadata | Для Task, Assignment, Notice |
        | InplaceFormMetadata | Для дочерних коллекций |
        | RemoteControlFormMetadata | Для Remote Components |

        ## Типы контролов на форме
        | Тип | Назначение |
        |-----|-----------|
        | ControlMetadata | Базовый контрол (поле) |
        | ControlGroupMetadata | Группа контролов |
        | HeaderControlGroupMetadata | Заголовок (закреплён сверху) — только для Task/Assignment |
        | FooterControlGroupMetadata | Подвал (закреплён снизу) — только для Task/Assignment |
        | ThreadControlGroupMetadata | Центральная часть (скроллируется) |
        | FunctionControlMetadata | Контрол состояния (вызывает функцию) |
        | LabelControlMetadata | Метка (текст без редактирования) |
        | HyperlinkControlMetadata | Гиперссылка |
        | PreviewControlMetadata | Предпросмотр документа |

        ## Полные имена DataBinder (для DataBinderTypeName)
        Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder
        Sungero.Presentation.CommonDataBinders.TextEditorToTextBinder
        Sungero.Presentation.CommonDataBinders.NumericEditorToIntAndDoubleBinder
        Sungero.Presentation.CommonDataBinders.BooleanEditorToBooleanBinder
        Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder
        Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder
        Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumerationBinder
        Sungero.Presentation.CommonDataBinders.GridControlToChildCollectionBinder
        Sungero.Presentation.CommonDataBinders.MultiSelectEditorToCollectionBinder
        Sungero.Presentation.CommonDataBinders.StateViewToFunctionBinder
        Sungero.Presentation.CommonDataBinders.ImageEditorToImageBinder
        Sungero.Presentation.CommonDataBinders.LabelToTextSourceBinder
        Sungero.Presentation.CommonDataBinders.HyperlinkToSourceBinder

        ## ColumnDefinitions (раскладка формы)
        Одна колонка: [{"Percentage": 100.0}]
        Две колонки: [{"Percentage": 50.0}, {"Percentage": 50.0}]
        Три колонки: [{"Percentage": 40.0}, {"Percentage": 35.0}, {"Percentage": 25.0}]
        """;

    private const string CsharpFunctions = """
        # Паттерны функций Directum RX

        ## Серверные функции модуля (ModuleServerFunctions.cs)
        ```
        namespace {CompanyCode}.{ModuleName}.Server
        {
          public partial class ModuleFunctions
          {
            [Remote]
            public static IEntity CreateEntity() { return Entities.Create(); }

            [Public]
            public static IQueryable<IEntity> GetActiveEntities()
            { return Entities.GetAll().Where(x => x.Status == Status.Active); }

            [Remote(IsPure = true), Public]
            public static bool CheckSomething(long entityId) { return true; }

            [Public(WebApiRequestType = RequestType.Get)]
            public virtual bool CheckConnection() { return true; }

            [Public(WebApiRequestType = RequestType.Post)]
            public virtual string ProcessData(string jsonInput) { return "ok"; }
          }
        }
        ```

        ## Клиентские функции модуля (ModuleClientFunctions.cs)
        ```
        namespace {CompanyCode}.{ModuleName}.Client
        {
          public partial class ModuleFunctions
          {
            [LocalizeFunction("CreateEntityFunctionName", "")]
            public virtual void CreateEntity()
            {
              Functions.Module.Remote.CreateEntity().Show();
            }

            [LocalizeFunction("ShowReportFunctionName", "ShowReportFunctionDescription")]
            public virtual void ShowReport()
            {
              var report = Reports.GetMyReport();
              report.Open();
            }
          }
        }
        ```

        ## Функции сущности (EntityServerFunctions.cs)
        ```
        namespace {CompanyCode}.{ModuleName}.Server
        {
          partial class {EntityName}Functions
          {
            [Remote(IsPure = true)]
            public Sungero.Core.StateView GetStateView() { ... }

            [Public]
            public static IQueryable<I{EntityName}> GetFiltered(long departmentId) { ... }
          }
        }
        ```

        ## Атрибуты функций
        | Атрибут | Назначение | Доступ |
        |---------|-----------|--------|
        | [Remote] | Вызов с клиента через Functions.Module.Remote.X() | Только этот модуль |
        | [Public] | Доступ из других модулей через PublicFunctions.Module.X() | Все модули |
        | [Remote, Public] | Оба вида доступа | Везде |
        | [Remote(IsPure = true)] | Чистая функция, кэшируемая | Кэш |
        | [Public(WebApiRequestType)] | REST API endpoint | HTTP |
        | [LocalizeFunction("Name", "Desc")] | Клиентская, локализованная | Обложка |
        | [Hyperlink] | Deep link из внешних систем | URL |

        ## PublicFunctions в Module.mtd
        ```json
        "PublicFunctions": [{
          "Name": "GetActiveEntities",
          "Placement": "Server",
          "IsRemote": true,
          "ReturnType": "global::System.Collections.Generic.IQueryable<global::MyModule.IEntity>",
          "ReturnTypeFullName": "System.Collections.Generic.IQueryable",
          "Parameters": [{
            "Name": "departmentId",
            "ParameterType": "global::System.Int64",
            "ParameterTypeFullName": "System.Int64"
          }]
        }]
        ```

        ## Маппинг типов для PublicFunctions
        string → global::System.String
        int → global::System.Int32
        long → global::System.Int64
        bool → global::System.Boolean
        DateTime → global::System.DateTime
        Guid → global::System.Guid
        List<T> → global::System.Collections.Generic.List<global::T>
        IQueryable<T> → global::System.Linq.IQueryable<global::T>
        void → (пусто, без ReturnType)
        IEntity → global::{CompanyCode}.{ModuleName}.I{EntityName}
        """;

    private const string WorkflowPatterns = """
        # Workflow паттерны Directum RX

        ## Структура Task + Assignment + Notice
        Task (задача) → содержит RouteScheme (схему маршрута)
          ├── Assignment (задание) — для каждого исполнителя
          └── Notice (уведомление) — информирование без действия

        ## Блоки workflow (Blocks в Module.mtd)
        | Тип | Назначение |
        |-----|-----------|
        | ScriptBlockMetadata | Выполнение C# кода |
        | AssignmentBlockMetadata | Создание задания исполнителю |
        | NoticeBlockMetadata | Отправка уведомления |
        | TaskBlockMetadata | Создание подзадачи |
        | MonitoringBlockMetadata | Мониторинг состояния |

        ## Пример ScriptBlock в Module.mtd
        ```json
        {
          "$type": "Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared",
          "NameGuid": "<NewGuid>",
          "Name": "ProcessBlock",
          "HandledEvents": ["ProcessBlockExecute"],
          "OutProperties": [{
            "$type": "Sungero.Metadata.EnumBlockPropertyMetadata, Sungero.Metadata",
            "NameGuid": "<NewGuid>",
            "Name": "ExecutionResult",
            "DirectValues": [
              {"NameGuid": "<NewGuid>", "Name": "Success", "Code": "Success"},
              {"NameGuid": "<NewGuid>", "Name": "Error", "Code": "Error"}
            ]
          }]
        }
        ```

        ## Обработчики событий Task
        - TaskCreatedServer — при создании задачи
        - TaskStartedServer — при старте задачи
        - TaskCompletedServer — при завершении
        - TaskAbortedServer — при прерывании

        ## Обработчики событий Assignment
        - AssignmentCreatedServer — при создании задания
        - AssignmentCompletedServer — при выполнении задания
        - AssignmentResultServer — обработка результата

        ## Обработчики событий Block
        - {BlockName}Execute — выполнение блока скрипта
        - {BlockName}Start — начало блока
        - {BlockName}End — завершение блока

        ## AttachmentGroups (вложения)
        Task, Assignment и Notice могут иметь AttachmentGroups.
        Constraints ОБЯЗАНЫ совпадать между Task и его Assignment/Notice.
        Безопасный вариант: "Constraints": [] везде.
        IsAssociatedEntityGroup: true — группа наследуется от Task.

        ## Form для Task/Assignment
        Используй WorkflowEntityStandaloneFormMetadata (не StandaloneFormMetadata!)
        Обязательные группы: HeaderControlGroup, ThreadControlGroup, FooterControlGroup
        """;

    private const string SolutionDesign = """
        # Руководство по проектированию решений Directum RX

        ## Типовые решения и их архитектура

        ### CRM (Управление продажами)
        Сущности:
        - Сделка (Document) — воронка, этапы, суммы, CollectionProperty для продуктов
        - Лид (DatabookEntry) — входящий запрос, конвертация в сделку
        - Продукт (DatabookEntry) — каталог товаров/услуг
        - Активность (DatabookEntry) — звонки, встречи, письма
        - Причина проигрыша (DatabookEntry) — справочник
        - Воронка/Pipeline (DatabookEntry) — этапы продаж
        Workflow: не нужен (продажи не согласовываются)
        Cover: Сделки, Лиды, Аналитика (виджеты с графиками)
        Remote Component: Kanban-доска для визуализации воронки

        ### ESM (Управление обращениями/Service Desk)
        Сущности:
        - Обращение/Ticket (Document) — с дочерней коллекцией комментариев
        - Категория (DatabookEntry) — категоризация обращений
        - SLA (DatabookEntry) — уровни обслуживания, сроки
        - Матрица приоритетов (DatabookEntry) — Impact × Urgency
        Workflow: Task для назначения исполнителя, эскалации
        AsyncHandler: автоматическая эскалация при просрочке SLA

        ### HR (Управление персоналом)
        Сущности:
        - Отпуск (DatabookEntry) — если без файлов, или Document с заявлением
        - Заявка на отпуск (Document) — с согласованием
        - График отпусков (Document с CollectionProperty)
        Workflow: Task согласования заявки
        Переиспользуй: Employee из Sungero.Company

        ### Канцелярия (Record Management расширение)
        Перекрытие: IncomingLetter, OutgoingLetter из Sungero.RecordManagement
        Добавление: новые свойства (доп. реквизиты)
        Не создавай новый модуль — перекрывай существующие сущности

        ## Алгоритм проектирования
        1. Определи домен → выбери reference-модуль из платформы
        2. Определи сущности → для каждой выбери тип (DatabookEntry/Document/Task)
        3. Нужен workflow? → добавь Task + Assignment + Notice
        4. Нужны дочерние коллекции? → используй Document, не DatabookEntry
        5. Нужна визуализация? → Remote Component (React SPA)
        6. Нужна интеграция? → WebAPI functions или AsyncHandler
        7. Нужны фоновые процессы? → Job (расписание) или AsyncHandler (событие)

        ## Переиспользование платформы
        - Сотрудники → Sungero.Company.Employee (НЕ создавай свой)
        - Контрагенты → Sungero.Parties.Counterparty
        - Подразделения → Sungero.Company.Department
        - Виды документов → Sungero.Docflow.DocumentKind
        - Согласование → Sungero.Docflow.ApprovalRule (настрой правила, не пиши код)

        ## Когда перекрывать vs создавать новое
        Перекрывай (override): добавить свойства к существующей сущности (Counterparty + TIN поле)
        Создавай новое: новый бизнес-объект, не имеющий аналога в платформе (Deal, Ticket, Lead)
        """;

    private const string CoverWidgets = """
        # Обложки и виджеты Directum RX

        ## Cover (обложка модуля) — структура в Module.mtd
        ```json
        "Cover": {
          "NameGuid": "<NewGuid>",
          "Header": {"NameGuid": "<NewGuid>", "BackgroundPosition": "Stretch", "Versions": []},
          "Footer": {"NameGuid": "<NewGuid>", "BackgroundPosition": "Stretch", "Versions": []},
          "Background": null,
          "Tabs": [
            {"NameGuid": "<NewGuid>", "Name": "MainTab", "PreviousItemGuid": "00000000-..."}
          ],
          "Groups": [
            {
              "NameGuid": "<GroupGuid>",
              "Name": "Sales",
              "TabId": "<TabGuid>",
              "PreviousItemGuid": "00000000-...",
              "BackgroundPosition": "Stretch",
              "Versions": []
            }
          ],
          "Actions": [
            {
              "$type": "Sungero.Metadata.CoverEntityListActionMetadata, Sungero.Metadata",
              "NameGuid": "<NewGuid>",
              "Name": "ShowDeals",
              "EntityTypeId": "<DealEntityGuid>",
              "GroupId": "<GroupGuid>",
              "PreviousItemGuid": "00000000-..."
            },
            {
              "$type": "Sungero.Metadata.CoverFunctionActionMetadata, Sungero.Metadata",
              "NameGuid": "<NewGuid>",
              "Name": "CreateDeal",
              "FunctionName": "CreateDeal",
              "GroupId": "<GroupGuid>"
            }
          ],
          "RemoteControls": []
        }
        ```

        ## Типы Cover Actions
        - CoverEntityListActionMetadata — открыть список сущностей (EntityTypeId)
        - CoverFunctionActionMetadata — вызвать клиентскую функцию (FunctionName)
        - CoverReportActionMetadata — открыть отчёт
        - CoverComputableFolderActionMetadata — открыть вычисляемую папку

        ## Resx ключи для Cover
        ModuleSystem.ru.resx:
        - DisplayName = "Управление продажами"
        - CoverTab_MainTab = "Основное"
        - CoverGroup_Sales = "Продажи"
        - CoverAction_ShowDeals = "Сделки"
        - CoverAction_CreateDeal = "Создать сделку"

        ## Виджеты (Widgets) — структура в Module.mtd
        ```json
        "Widgets": [{
          "NameGuid": "<NewGuid>",
          "Name": "ActiveDealsCounter",
          "Color": "WidgetColor1",
          "ColumnSpan": 20,
          "WidgetItems": [{
            "$type": "Sungero.Metadata.WidgetActionMetadata, Sungero.Metadata",
            "NameGuid": "<NewGuid>",
            "Name": "Counter",
            "EntityGuid": "<DealEntityGuid>",
            "IsMain": true,
            "HandledEvents": ["FilteringServer"]
          }]
        }]
        ```

        ## Remote Components (сторонние React-контролы)
        Для сложных UI (Kanban, графики, таблицы) используй RemoteControlFormMetadata.
        Технологии: React + TypeScript + Webpack → Module Federation.
        Регистрация: RemoteControls в Cover или RemoteControlFormMetadata в Forms.
        Пример: DirRX.AgileBoard (Kanban-доска).
        """;

    private const string InitializerGuide = """
        # ModuleInitializer — полное руководство

        ## Структура
        ```
        namespace {CompanyCode}.{ModuleName}.Server
        {
          public partial class ModuleInitializer
          {
            public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)
            {
              // Версионная инициализация
              Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
                this.FirstInitializing,
                Constants.Module.Init.ModuleName,
                Version.Parse(Constants.Module.Init.FirstVersion));

              Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
                this.Initializing_v2,
                Constants.Module.Init.ModuleName,
                Version.Parse(Constants.Module.Init.Version2));
            }

            public virtual void FirstInitializing()
            {
              CreateRoles();
              GrantRightsOnDatabooks();
              FillDatabookEntries();
            }
          }
        }
        ```

        ## Создание ролей
        ```
        Docflow.PublicInitializationFunctions.Module.CreateRole(
          Resources.RoleNameAdmin,
          Resources.RoleDescriptionAdmin,
          Constants.Module.AdminRoleGuid);
        ```

        ## Выдача прав на справочники
        ```
        MyEntities.AccessRights.Grant(Roles.AllUsers, DefaultAccessRightsTypes.Create);
        MyEntities.AccessRights.Save();
        ```

        ## Заполнение справочников (CreateOrUpdate паттерн)
        ```
        var entity = MyEntities.GetAll(x => x.Name == "Значение").FirstOrDefault();
        if (entity == null)
        {
          entity = MyEntities.Create();
          entity.Name = "Значение";
          entity.Save();
        }
        ```

        ## Создание SQL индексов
        ```
        Docflow.PublicFunctions.Module.ExecuteSQLCommand(
          Queries.Module.CreateMyIndex);
        ```

        ## Constants для версий
        ```
        public static class Module
        {
          public static readonly Guid AdminRoleGuid = new("12345678-...");

          public static class Init
          {
            public const string ModuleName = "DirRX.MyModule";
            public const string FirstVersion = "0.0.1.0";
            public const string Version2 = "1.0.0.0";
          }
        }
        ```

        ## Resx ключи для ролей
        Module.ru.resx:
        - RoleName_Admin = "Администратор модуля"
        - RoleDescription_Admin = "Роль для администрирования модуля"

        ## Порядок инициализации
        1. CreateRoles() — создать роли
        2. GrantRightsOnDatabooks() — раздать права на справочники
        3. GrantRightsOnDocuments() — права на типы документов
        4. CreateDocumentTypes() — зарегистрировать типы документов
        5. CreateDocumentKinds() — создать виды документов
        6. FillDatabookEntries() — заполнить справочники данными
        7. CreateIndices() — создать SQL индексы
        """;

    private const string IntegrationPatterns = """
        # Интеграция Directum RX с внешними системами

        ## 1. WebAPI через сервис интеграции
        Directum RX предоставляет OData v4 API через Integration Service.
        URL: http://server/Integration/odata/

        ### Создание WebAPI endpoint
        ```csharp
        [Public(WebApiRequestType = RequestType.Get)]
        public virtual bool CheckConnection()
        {
            return true;
        }

        [Public(WebApiRequestType = RequestType.Post)]
        public virtual string ProcessData(string jsonInput)
        {
            var data = JsonSerializer.Deserialize<MyStructure>(jsonInput);
            // обработка...
            return "ok";
        }
        ```

        ### Правила WebAPI
        - GET: только примитивные параметры (string, int, long, bool, DateTime)
        - POST: произвольные структуры через JSON body
        - Авторизация: HTTP Basic Auth
        - URL формат: /Integration/odata/{ModuleName}/{FunctionName}
        - Функция ОБЯЗАНА быть [Public] и зарегистрирована в PublicFunctions Module.mtd
        - virtual (НЕ static!) для WebAPI functions

        ## 2. Интеграция с 1С
        Reference: Sungero.Integration1C модуль
        Паттерн: WebAPI GET/POST endpoints + ExternalEntityLink для маппинга сущностей
        ```csharp
        [Public(WebApiRequestType = RequestType.Get)]
        public virtual List<long> GetContractIdsForSync(string systemId) { ... }

        [Hyperlink]
        public void FindContract(string uuid, string number, string sysid) { ... }
        ```

        ## 3. AsyncHandler для фоновой интеграции
        Когда: обработка событий, отложенная синхронизация, уведомления
        ```
        Module.mtd → AsyncHandlers: [{
          "Name": "SyncWithExternalSystem",
          "DelayPeriod": 15,
          "DelayStrategy": "ExponentialDelayStrategy",
          "Parameters": [{"Name": "EntityId", "ParameterType": "LongInteger"}]
        }]
        ```
        Вызов: `PublicFunctions.Module.CreateAsyncHandler(handlerId, entityId);`

        ## 4. Job для периодической синхронизации
        Когда: импорт/экспорт по расписанию
        ```
        Module.mtd → Jobs: [{
          "Name": "SyncJob",
          "MonthSchedule": "Monthly",
          "StartAt": "1753-01-01T02:00:00"
        }]
        ```

        ## 5. OData клиент (исходящие вызовы)
        Для вызова внешних OData API используй HttpClient:
        ```csharp
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", ...);
        var response = await client.GetAsync("https://external-api/data");
        ```
        ЗАПРЕЩЕНО: System.Net.WebClient (устаревший)

        ## 6. Обмен документами (EDI)
        Reference: Sungero.Exchange, Sungero.ExchangeCore
        Поддержка: Диадок, СБИС
        Паттерн: ExchangeDocumentInfo → привязка к OfficialDocument

        ## 7. Elasticsearch интеграция
        Reference: Sungero.Commons → IsElasticsearchConfigured()
        Паттерн: CreateIndexEntityAsyncHandler в AfterSave
        """;

    private const string ReportPatterns = """
        # Отчёты Directum RX

        ## Структура отчёта
        ```
        {ModuleName}.Shared/{ReportName}/
        ├── {ReportName}.mtd          — метаданные отчёта (ReportMetadata)
        ├── {ReportName}.frx          — шаблон FastReport
        ├── {ReportName}System.resx   — ресурсы (EN)
        ├── {ReportName}System.ru.resx — ресурсы (RU)
        └── Queries.xml              — SQL-запросы

        {ModuleName}.Server/
        └── {ReportName}Handlers.cs   — обработчики (BeforeExecute, AfterExecute)
        ```

        ## ReportMetadata (.mtd)
        ```json
        {
          "$type": "Sungero.Metadata.ReportMetadata, Sungero.Reporting.Shared",
          "NameGuid": "<NewGuid>",
          "Name": "SalesFunnel",
          "HandledEvents": ["BeforeExecuteServer", "AfterExecuteServer"],
          "Parameters": [{
            "NameGuid": "<NewGuid>",
            "Name": "StartDate",
            "InternalDataTypeName": "System.DateTime",
            "IsSimpleDataType": true
          }]
        }
        ```

        ## Обработчик BeforeExecute
        ```csharp
        partial class SalesFunnelHandlers
        {
          public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)
          {
            var startDate = SalesFunnel.StartDate;
            // Подготовка данных
            var dataTable = new System.Data.DataTable();
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("Value", typeof(int));
            dataTable.Rows.Add("Лиды", 100);
            dataTable.Rows.Add("Сделки", 50);
            // Привязка к DataSource в .frx
          }
        }
        ```

        ## FastReport .frx
        - Формат: XML (FastReport Designer)
        - DataSource привязан к Queries.xml или к DataTable из обработчика
        - Поддержка: таблицы, графики, группировка, итоги

        ## Queries.xml
        ```xml
        <Queries>
          <Query Name="MainQuery">
            <![CDATA[
              SELECT d.Name, COUNT(*) AS DealCount
              FROM DirRX_CRM_Deal d
              WHERE d.Created >= @StartDate
              GROUP BY d.Name
            ]]>
          </Query>
        </Queries>
        ```

        ## Как открыть отчёт из обложки
        1. Добавь CoverReportActionMetadata в Module.mtd Cover.Actions
        2. Или вызови из клиентской функции:
        ```csharp
        [LocalizeFunction("ShowFunnelFunctionName", "")]
        public virtual void ShowFunnel()
        {
          var report = Reports.GetSalesFunnel();
          report.StartDate = Calendar.Today.AddMonths(-3);
          report.Open();
        }
        ```

        ## Типы параметров отчёта
        | Тип | InternalDataTypeName |
        |-----|---------------------|
        | Строка | System.String |
        | Дата | System.DateTime |
        | Число | System.Int64 |
        | Boolean | System.Boolean |
        | Сущность | Sungero.Domain.Shared.IEntity |
        """;

    // === v3.0 CONTENT ===

    private const string EntityCatalog = """
        # Полный каталог сущностей платформы Directum RX v25.3

        ПЕРЕД СОЗДАНИЕМ ЛЮБОЙ СУЩНОСТИ — СНАЧАЛА ПРОВЕРЬ ЭТОТ КАТАЛОГ.
        Если потребность покрывается существующей сущностью — ИСПОЛЬЗУЙ ЕЁ или ПЕРЕКРОЙ.

        ---

        ## SUNGERO.COMPANY — Организационная структура

        ### Employee (Сотрудник) — GUID: b7905516-2be5-4931-961c-cb38d5677565
        Тип: DatabookEntry | Модуль: Sungero.Company (d534e107-a54d-48ec-85ff-bc44d731a82f)
        Свойства: Name(string,required), Person(nav→Person), Department(nav→Department,required),
        JobTitle(nav→JobTitle), BusinessUnit(nav→BusinessUnit), PersonnelNumber(string),
        Phone(string), Email(string), Photo(image), Status(enum), Login(nav→User)
        Действия: CopyEntity, AddPersonalPhoto, ShowResponsibilitiesReport, OpenCertificatesList
        Когда использовать: ВСЕГДА когда нужен сотрудник. НЕ создавай свой справочник людей.
        Перекрытие: добавить TabNo, SkillLevel, Team → override Employee, ancestorGuid=b7905516...
        NavigationProperty: Department GUID=61b1c19f, JobTitle GUID=4a37aec4, BusinessUnit GUID=eff95720

        ### Department (Подразделение) — GUID: 61b1c19f-26e2-49a5-b3d3-0d3618151e12
        Тип: DatabookEntry | Модуль: Sungero.Company
        Свойства: Name(string,required), ShortName(string), Code(string),
        Manager(nav→Employee), HeadOffice(nav→Department), BusinessUnit(nav→BusinessUnit),
        Phone(string), Note(text), Status(enum)
        Когда использовать: организационная структура, иерархия отделов
        Перекрытие: добавить CostCenter, Location → override Department

        ### BusinessUnit (НОР / Юридическое лицо) — GUID: eff95720-181f-4f7d-892d-dec034c7b2ab
        Тип: DatabookEntry | Модуль: Sungero.Company
        Свойства: Name(string,required), LegalName(text), TIN(string), TRRC(string),
        PSRN(string), NCEO(string), LegalAddress(string), PostalAddress(string),
        CEO(nav→Employee), CAO(nav→Employee), HeadCompany(nav→BusinessUnit),
        City(nav→City), Region(nav→Region), Bank(nav→Bank), Account(string),
        Phones(string), Email(string), Homepage(string), Nonresident(bool), Status(enum)
        Когда использовать: наши юридические лица, филиалы, головная компания

        ### JobTitle (Должность) — GUID: 4a37aec4-764c-4c14-8887-e1ecafa5b4c5
        Тип: DatabookEntry | Модуль: Sungero.Company
        Свойства: Name(string,required), Department(nav→Department), Status(enum)
        Когда использовать: справочник должностей. НЕ создавай свой.

        ### ManagersAssistant (Помощник руководителя) — GUID: c2200a86-5d5d-47d6-930d-c3ce8b11f04b
        Свойства: Manager(nav→Employee), Assistant(nav→Employee,required),
        IsAssistant(bool), PreparesResolution(bool), SendActionItems(bool)
        Когда использовать: делегирование полномочий руководителя

        ### Substitution (Замещение) — в Sungero.CoreEntities
        Свойства: User(nav→User), Substitute(nav→User), StartDate(date), EndDate(date)
        Когда использовать: временное замещение на период отпуска/командировки

        ### Absence (Отсутствие) — GUID: 40630200-4431-4021-ac0c-1199831bc7ad
        Тип: Document | Модуль: Sungero.Company
        Свойства: Employee, AbsenceType, StartDate, EndDate, Status
        Когда использовать: отпуска, командировки, больничные

        ---

        ## SUNGERO.PARTIES — Контрагенты

        ### Counterparty (Контрагент, базовый) — GUID: 294767f1-009f-4fbd-80fc-f98c49ddc560
        Тип: DatabookEntry | Модуль: Sungero.Parties (243b34ec-8425-4c7e-b66f-27f7b9c8f38d)
        Свойства: Name(string,required), TIN(string), PSRN(string), Phones(string),
        Email(string), Homepage(string), LegalAddress(string), PostalAddress(string),
        Region(nav→Region), City(nav→City), Bank(nav→Bank), Note(text), Status(enum),
        CanExchange(bool), ExchangeBoxes(collection)
        Наследники: Company, Person, Bank
        Когда использовать: НЕ напрямую — используй Company (юр.лицо) или Person (физ.лицо)
        Перекрытие: добавить Industry, Segment, Source → override Counterparty

        ### Company (Организация) — GUID: 593e143c-616c-4d95-9457-fd916c4aa7f8
        Наследник Counterparty. Дополнительно: NCEO, NCEA, все реквизиты юр.лица
        Когда использовать: клиенты, поставщики, партнёры — ВСЕ юр.лица
        НЕ СОЗДАВАЙ: «Клиент», «Заказчик», «Поставщик» — это Company с разными ролями

        ### Person (Физическое лицо) — GUID: f5509cdc-ac0c-4507-a4d3-61d7a0a9b6cf
        Наследник Counterparty. Дополнительно: LastName, FirstName, MiddleName
        Когда использовать: физические лица — контрагенты, авторы обращений

        ### Contact (Контактное лицо) — GUID: c8daaef9-a679-4a29-ac01-b93c1637c72e
        Тип: DatabookEntry | Модуль: Sungero.Parties
        Свойства: Name, Company(nav→Counterparty), JobTitle(string), Phone, Email, Status
        Когда использовать: контактные лица контрагентов. НЕ создавай свой справочник контактов.

        ### Bank (Банк) — GUID: 80c4e311-e95f-449b-984d-1fd540b8f0af
        Наследник Counterparty. Дополнительно: BIC, CorrAccount
        Когда использовать: банковские реквизиты

        ---

        ## SUNGERO.COMMONS — Общие справочники

        ### City — GUID: 3a0c21f8-aa88-429c-891f-56c24d56fcef
        Свойства: Name(string,required), Country(nav→Country), Region(nav→Region), Status
        ### Country — GUID: 1f612925-95fc-4662-807d-c92c810a62b1
        Свойства: Name(string,required), Status
        ### Region — GUID: 4efe2fa9-b1d1-4b47-b366-4128fe0a391c
        Свойства: Name(string,required), Country(nav→Country), Status
        ### Currency — GUID: ffc2629f-dc30-4106-a3ce-c402ae7d32b9
        Свойства: Name(string,required), AlphaCode(string,3 символа — USD,EUR), NumericCode, Status
        ### VatRate — GUID: fe0ed345-5965-40d6-a559-24dcde189a95
        Свойства: Name(string,required), Rate(int — процент), Status

        ---

        ## SUNGERO.DOCFLOW — Документооборот

        ### OfficialDocument — базовый для ВСЕХ документов
        GUID: 58cca102-1e97-4f07-b6ac-fd866a8b7cb1 | Модуль: Sungero.Docflow
        Свойства: Name(string), Subject(text), DocumentKind(nav→DocumentKind),
        BusinessUnit(nav→BusinessUnit), Department(nav→Department), Author(nav→Employee),
        OurSignatory(nav→Employee), LifeCycleState(enum: Draft|Active|Obsolete),
        RegistrationNumber(string), RegistrationDate(date), DeliveryMethod(nav),
        Created(date), Modified(date), Versions(collection), Note(text)
        Когда использовать: не напрямую — наследуй для создания своего типа документа

        ### DocumentKind (Вид документа) — GUID: 14a59623-89a2-4ea8-b6e9-2ad4365f358c
        Тип: DatabookEntry | Свойства: Name, ShortName, Code, DocumentType(nav), DocumentFlow(enum),
        IsDefault(bool), Status
        Когда использовать: классификация документов. НЕ создавай свою классификацию.
        Создание нового вида: через ModuleInitializer → CreateDocumentKind()

        ### DocumentRegister (Журнал регистрации)
        Когда использовать: нумерация и регистрация документов. Настраивается, не кодится.

        ### ApprovalTask (Задача на согласование) — GUID: 100950d0-03d2-44f0-9e31-f9c8dfdf3829
        Тип: Task | Модуль: Sungero.Docflow
        Когда использовать: согласование ЛЮБЫХ документов. НЕ создавай свою задачу согласования.
        Настройка: создай ApprovalRule с нужными этапами.
        Типы этапов: Согласование, Подпись, Регистрация, Рассмотрение, Контроль возврата и др.

        ### ApprovalRule (Правило согласования)
        Когда использовать: настройка маршрута согласования. Код не нужен.
        Привязка: по виду документа (DocumentKind) + подразделение + НОР

        ---

        ## SUNGERO.CONTRACTS — Договоры

        ### ContractualDocument (Договор) — наследник OfficialDocument
        GUID: 454df3c6-b850-47cf-897f-a10d767baa77 | Модуль: Sungero.Contracts
        Свойства: все от OfficialDocument + Counterparty(nav→Counterparty),
        TotalAmount(double), Currency(nav→Currency), ValidFrom(date), ValidTill(date),
        IsAutomaticRenewal(bool), DaysToFinishWorks(int)
        Когда использовать: договоры, контракты. НЕ создавай свой тип «Контракт».
        Перекрытие: добавить ContractCategory, Manager → override ContractualDocument

        ### SupAgreement (Допсоглашение) — наследник ContractualDocument
        Дополнительно: LeadingDocument(nav→ContractualDocument)
        Когда использовать: дополнительные соглашения к договорам

        ### IncomingInvoice (Входящий счёт), OutgoingInvoice (Исходящий счёт)
        Когда использовать: счета на оплату

        ---

        ## SUNGERO.RECORDMANAGEMENT — Делопроизводство

        ### ActionItemExecutionTask (Поручение) — Task
        Модуль: Sungero.RecordManagement (4e25caec-c722-4740-bcfd-c4f803840ac6)
        Когда использовать: задания с контролем исполнения. НЕ создавай свою задачу «поручение».

        ### IncomingLetter (Входящее письмо) — наследник OfficialDocument
        Дополнительно: InNumber, Dated, Correspondent(nav→Counterparty), Addressee(nav→Employee)
        Перекрытие: добавить DeliveryType, Priority → override IncomingLetter

        ### OutgoingLetter (Исходящее письмо) — наследник OfficialDocument
        Дополнительно: Addressees(collection), IsManyAddressees(bool)
        Перекрытие: аналогично IncomingLetter

        ---

        ## ПРАВИЛА ПРИНЯТИЯ РЕШЕНИЙ

        ### Алгоритм: создавать, перекрывать или настроить?

        1. ПОИСК: search_metadata name=<потребность> — есть в платформе?
           Да → используй как есть ИЛИ перекрой (override)
           Нет → создавай новую сущность (scaffold_entity mode=new)

        2. ПЕРЕКРЫТИЕ vs НОВОЕ:
           - Нужно ДОБАВИТЬ поля к существующей? → override (mode=override)
           - Принципиально НОВЫЙ бизнес-объект? → new (mode=new)

        3. КОД vs НАСТРОЙКА:
           - Новый вид документа → DocumentKind в справочнике (не код)
           - Маршрут согласования → ApprovalRule (не код)
           - Нумерация → DocumentRegister (не код)
           - Права → AccessRights.Grant() в Initializer

        ### Что ТОЧНО надо создавать (нет в платформе):
        Сделка(Deal), Лид(Lead), Обращение(Ticket), Продукт(Product),
        KPI/Цель(Target), Воронка(Pipeline), Этап(Stage), Активность(Activity),
        SLA(ServiceLevel), Категория услуг(ServiceCategory), Тикет(AgileTicket)

        ### Что ТОЧНО НЕ надо создавать:
        Сотрудник, Подразделение, Должность, Организация (наша), Контрагент,
        Контакт, Город, Страна, Валюта, Договор, Письмо, Приказ, Записка,
        Задача согласования, Поручение, Роль, Замещение

        ---

        ## SUNGERO.PROJECTS

        ### Project — GUID: 4383f2ff-56e6-46f4-b4ef-cc17e6aeef40
        Тип: Document | Модуль: Sungero.Projects (356e6500-45bc-482b-9791-189b5adedc28)
        Свойства: TeamMembers(collection), Classifier(collection)
        Когда использовать: управление проектами, портфели

        ---

        ## SUNGERO.MEETINGS

        ### Meeting — GUID: dbc0dd63-4d23-4f41-92ae-cab59bb70c8c
        Тип: Document | Модуль: Sungero.Meetings (593dcc11-15ee-49f2-b4ef-bf4cf7867055)
        Actions: CreateOrShowAgenda, CreateOrShowMinutes, OpenActionItems, AddMember
        Когда: совещания, планёрки. НЕ создавай свой тип "Встреча"

        ### Agenda — GUID: 5261da93-7879-4210-b3db-c92fa894ab4d
        Тип: Document | ConverterFunctions: GetMeetingDate, GetMeetingLocation, GetMeetingMembers

        ### Minutes — GUID: bb4780ff-b2c3-4044-a390-e9e110791bf6
        Тип: Document | Actions: CreateActionItems
        Когда: протоколы совещаний. НЕ создавай свой тип

        ---

        ## SUNGERO.INTERNALPOLICIES

        ### InternalPolicy — GUID: f421c353-d171-4422-8d81-ddb859d5a5f6
        Тип: Document | Модуль: Sungero.InternalPolicies (48c9a380-db0e-47ca-ae0b-4015bbced723)
        Когда: ЛНА, положения, регламенты. НЕ создавай свой тип

        ---

        ## SUNGERO.SMARTPROCESSING

        ### VerificationTask — GUID: 999a5ae0-17ec-4735-bc90-d85c7fe08dd3
        Тип: Task | Блоки: VerificationBlock(assignment), AnalyzeDocumentPackageSeparation(script)
        Когда: интеллектуальная обработка, OCR проверка

        ### BlobPackage — GUID: 1e9415ec-6ba8-46b5-b864-94b4385ffb52
        Свойства: Name, SourceType(enum), PackageFolderPath, Blobs(collection)
        Когда: массовый импорт документов

        ---

        ## SUNGERO.FINANCIALARCHIVE

        ### ContractStatement — GUID: f2f5774d-5ca3-4725-b31d-ac618f6b8850
        Тип: Document | Actions: ShowDuplicates, CreateCoverLetter
        Когда: финансовые акты, отчёты. НЕ создавай свой тип

        ### UniversalTransferDocument — наследник финансового документа
        Когда: УПД, электронные накладные
        """;


    private const string SolutionsReference = """
        # 30+ Production-паттернов из реальных решений Directum RX

        Каталог из 4 решений: AgileBoard, Targets/KPI, ESM (Service Desk), CRM.
        ИСПОЛЬЗУЙ как reference при проектировании. Не генерируй из головы — подглядывай.

        ## Быстрый поиск: "мне нужно..."
        | Задача | Решение | Путь к коду |
        |--------|---------|-------------|
        | REST API | AgileBoard (30+ endpoints) | archive/agile_extracted/DirRX.AgileBoards/ |
        | Remote Component (React) | Targets (6 RC) | archive/targets_extracted/DirRX.DirectumTargets/ |
        | Kanban-доска | AgileBoard | archive/agile_extracted/DirRX.AgileBoards/ |
        | Email-to-Ticket | ESM (DCS + regex) | archive/esm_extracted/rosa.ESM/ |
        | SLA расчёт | ESM (4 режима) | archive/esm_extracted/rosa.ESM/ |
        | AI-интеграция | ESM (AIAgentTool) | archive/esm_extracted/rosa.ESM/ |
        | XLSX импорт | Targets (Isolated) | archive/targets_extracted/DirRX.KPI/ |
        | Word генерация | Targets (Aspose) | archive/targets_extracted/DirRX.Targets/ |
        | Граф связей many-to-many | AgileBoard (SQL DDL) | archive/agile_extracted/DirRX.AgileBoards/ |
        | Real-time обновления | AgileBoard (ClientManager) | archive/agile_extracted/DirRX.AgileBoards/ |
        | CRM воронка | CRM (Pipeline→Stage→Deal) | crm-package/source/DirRX.CRMSales/ |
        | BANT lead scoring | CRM (4 boolean + auto-score) | crm-package/source/DirRX.CRMMarketing/ |
        | Round-robin распределение | CRM (LeadAssignmentJob) | crm-package/source/DirRX.CRM/ |
        | Виджеты (counter + chart) | CRM (4 виджета) | crm-package/source/DirRX.CRM/ |
        | ExpressionElement | ESM (5 функций) | archive/esm_extracted/rosa.ESM/ |
        | Матричная приоритизация | ESM (Impact×Urgency) | archive/esm_extracted/rosa.ESM/ |
        | Кастомная история | AgileBoard (SQL) | archive/agile_extracted/DirRX.History/ |

        ## Сводная матрица
        | Паттерн | Agile | Targets | ESM | CRM |
        |---------|:-----:|:-------:|:---:|:---:|
        | WebAPI endpoints | 30+ | 10+ | 5+ | 25+ |
        | Remote Components | — | 6 | — | 5 |
        | Real-time | + | — | — | — |
        | Isolated Areas | — | 2 | 1 | — |
        | Async Handlers | 9 | 12 | 14+ | 4 |
        | Jobs | — | 2 | 5 | 2 |
        | Виджеты | — | 1 | 3 | 4 |
        | Workflow (Task) | 1 | 1 | 1+7 | 1 |
        | AI-интеграция | — | — | + | — |
        | ExpressionElement | — | — | 5 | — |
        | Импорт данных | Trello | XLSX | Email | — |
        | Round-robin | — | — | — | + |
        | BANT scoring | — | — | — | + |

        ## Ключевые паттерны (детали)

        ### WebAPI через [Public(WebApiRequestType)]
        Решение: AgileBoard, CRM — весь фронтенд через REST endpoints
        ```csharp
        [Public(WebApiRequestType = RequestType.Post)]
        public virtual string GetBoardData(string boardId)
        { return JsonSerializer.Serialize(board); }
        ```
        Правила: GET только примитивы, POST структуры, virtual (не static)

        ### Real-time (ClientManager)
        Решение: AgileBoard — мгновенные обновления Kanban
        ```csharp
        ClientManager.Instance.GetClientsOfUser(userId)
          .ForEach(c => c.SendMessage("TicketUpdated", ticketId));
        ```

        ### Email-to-Ticket (DCS)
        Решение: ESM — входящий email → обращение
        Поток: DCS-пакет (JSON) → regex поиск номера в теме → создание/обновление тикета → файлы → уведомление

        ### SLA-калькулятор (4 режима)
        Решение: ESM — GetSolvationTime()
        Режимы: User (рабочий календарь), Group (календарь подразделения), BusinessUnit (НО), Overtime (24/7)

        ### AIAgentTool (Self-registration)
        Решение: ESM — для каждой услуги создаётся AI-инструмент
        ```json
        AsyncHandler: {"Name":"CreateRequestFromTool","Parameters":[{"Name":"ToolCallId","ParameterType":"String"},{"Name":"InputJson","ParameterType":"String"}]}
        ```

        ### XLSX импорт (Isolated)
        Решение: Targets — массовый импорт KPI
        Поток: Isolated Function парсит XLSX (ExcelDataReader) → List<Row> → Server создаёт сущности → async cleanup

        ### Round-robin
        Решение: CRM — LeadAssignmentJob
        ```csharp
        var managers = GetActiveManagers();
        var unassigned = Leads.GetAll(l => l.Manager == null);
        int i = 0;
        foreach (var lead in unassigned)
        { lead.Manager = managers[i % managers.Count]; lead.Save(); i++; }
        ```

        ### Граф связей (SQL DDL)
        Решение: AgileBoard — many-to-many для тикетов
        ```sql
        CREATE TABLE TicketRelation (Id SERIAL, TicketId1 INT, TicketId2 INT, RelationType VARCHAR(50));
        ```
        В ModuleInitializer: ExecuteSQLCommand(Queries.Module.CreateTicketRelationTable)

        ### Модульная архитектура "звезда"
        Решение: CRM — 5 модулей
        CRM (фасад) ↔ CRMSales (сделки) + CRMMarketing (лиды) + CRMDocuments + CRMCommon
        Модули не знают друг о друге, связь через PublicFunctions фасада.
        """;

    private const string DdsKnownIssues = """
        # 18 известных проблем DDS 25.3

        ## Ошибки импорта пакета

        ### 1. CollectionProperty в DatabookEntry
        Ошибка: "Missing area" / NullReferenceException в InterfacesGenerator
        Причина: DatabookEntry не поддерживает CollectionPropertyMetadata
        Fix: сменить BaseGuid на Document (58cca102) или удалить коллекцию

        ### 2. Cross-module NavigationProperty
        Ошибка: ссылка на сущность из незадекларированного модуля
        Причина: EntityGuid в NavigationProperty не в Dependencies
        Fix: добавить модуль в Dependencies Module.mtd

        ### 3. Reserved C# enum values
        Ошибка: CS0116, "not a valid identifier"
        Причина: enum value = new, event, class, default, string и др.
        Fix: fix_package (автоматически добавляет суффикс Value)

        ### 4. Duplicate DB column Code
        Ошибка: "column already exists"
        Причина: одинаковый Code у свойств в иерархии наследования
        Fix: fix_package (добавляет префикс модуля)

        ### 5. AttachmentGroup Constraints
        Ошибка: рассинхронизация Task ↔ Assignment/Notice
        Причина: разные Constraints в IsAssociatedEntityGroup группах
        Fix: fix_package (очищает до Constraints: [])

        ### 6. Resource_GUID в System.resx
        Ошибка: пустые подписи полей на карточке
        Причина: ключи Resource_<GUID> вместо Property_<Name>
        Fix: fix_package (заменяет на Property_<Name>)

        ### 7. Отсутствие .sds/Libraries/Analyzers/
        Ошибка: ошибка компиляции
        Fix: скопировать DLL из <DDS_INSTALL>/Analyzers/

        ## Ошибки сборки

        ### 8. File locks на .csproj
        Ошибка: "используется другим процессом"
        Причина: dotnet.exe не отпустил lock после неудачной сборки
        Fix: перезапустить DDS, taskkill /f /im dotnet.exe

        ### 9. FormTabs не поддерживаются
        Ошибка: при попытке использовать вкладки на карточке
        Fix: удалить FormTabs, использовать ControlGroupMetadata

        ### 10. DomainApi version отсутствует
        Ошибка: ошибка при публикации
        Fix: fix_package (добавляет DomainApi:2 в Versions)

        ## Ошибки публикации

        ### 11. CoverFunctionAction → "Can't resolve function"
        Причина: FunctionName ≠ имя метода в ModuleClientFunctions.cs
        Fix: переименовать метод или FunctionName

        ### 12. Пустые подписи на карточке
        Причина: нет satellite DLL с ресурсами
        Fix: пересобрать через DDS или вручную через al.exe

        ### 13. Overridden Controls = пустая форма
        Причина: "Overridden": ["Controls"] + "Controls": []
        Fix: либо убрать Controls из Overridden, либо заполнить Controls

        ## Ошибки разработки

        ### 14. DateTime.Now вместо Calendar.Now
        Причина: серверное время без учёта часового пояса
        Fix: заменить на Calendar.Now / Calendar.Today

        ### 15. public class вместо partial class
        Причина: DDS генерирует partial class, ваш код должен соответствовать
        Fix: заменить public class на partial class

        ### 16. Неправильный namespace
        Server/ → .Server, ClientBase/ → .Client (НЕ .ClientBase!), Shared/ → .Shared

        ### 17. Библиотеки через .csproj
        Причина: DDS затирает .csproj при переоткрытии
        Fix: добавлять через DDS UI "Сторонние библиотеки"

        ### 18. PublicStructures в .cs вместо Module.mtd
        Причина: свойства структур определяются ТОЛЬКО в Module.mtd
        Fix: перенести Properties из ModuleStructures.cs в Module.mtd → PublicStructures
        """;

    private const string DevEnvironments = """
        # DDS vs CrossPlatform DataServer

        ## Сравнение
        | Параметр | DDS (25.3) | CrossPlatform DS |
        |----------|-----------|------------------|
        | UI | WPF + DevExpress | Electron (Chromium) |
        | Runtime | .NET Framework 4.8 | .NET 6+ |
        | ОС | Windows only | Windows + Linux |
        | Редактор кода | Встроенный | VS Code + Extension |
        | Публикация | GUI | HTTP API (порт 7190) + CLI |
        | Git | LibGit2Sharp встроен | Системный git |
        | Автоматизация | Низкая (GUI) | Высокая (HTTP API) |

        ## Рекомендации для Claude Code / MCP
        - CrossPlatform DS лучше для автоматизации (HTTP API публикации)
        - DDS 25.3 — текущий стандарт, большинство решений на нём
        - MCP tools работают с обоими (файловая структура одинаковая)

        ## HTTP API CrossPlatform DS (порт 7190)
        POST /api/publish — публикация решения
        POST /api/build — сборка
        GET /api/status — статус

        ## Когда какой выбрать
        - Разработка на Windows → DDS 25.3 (стабильно, проверено)
        - CI/CD, автоматизация → CrossPlatform DS (HTTP API)
        - Linux сервер → CrossPlatform DS (единственный вариант)
        """;

    private const string StandaloneSetup = """
        # Автономная установка стенда Directum RX

        ## 4 уровня автономности

        ### Уровень 1: Генерация + сборка .dat (без RX)
        Требуется: .NET 8 SDK, 7-Zip
        Возможности: scaffold_*, check_package, fix_package, build_dat
        ```
        # Сборка .dat
        cd package-root
        find source settings -type f | sort > filelist.txt
        echo "PackageInfo.xml" >> filelist.txt
        7z a -tzip Module.dat @filelist.txt
        ```

        ### Уровень 2: Сборка в DDS (без публикации)
        Требуется: DDS 25.3 installed
        Возможности: импорт .dat, компиляция C#, проверка ошибок
        ```
        # Импорт через DDS GUI: Файл → Импорт пакета
        # Или через командную строку (DDS CLI)
        ```

        ### Уровень 3: Полный цикл (Launcher + DDS)
        Требуется: DirectumLauncher + PostgreSQL + IIS
        ```
        # Публикация через DeploymentTool
        do.bat dt deploy --package="C:\Module.dat" --force --dev
        do.bat dt deploy --ls  # листинг компонент
        ```

        ### Уровень 4: Export из git
        ```
        do.bat dt export-package --export_package="C:\output\Module.dat" \
          --root="C:\git_repo" --repositories="ModuleName"
        ```

        ## Требования оборудования
        | Компонент | Минимум | Рекомендуемое |
        |-----------|---------|---------------|
        | RAM | 8 ГБ | 16 ГБ |
        | CPU | 4 ядра | 8 ядер |
        | Диск | 50 ГБ SSD | 100 ГБ NVMe |
        | ОС | Windows 10/11 | Windows Server 2019+ |
        | PostgreSQL | 14+ | 16+ |

        ## Цикл автономной разработки
        1. scaffold_module → scaffold_entity → scaffold_function
        2. check_package → fix_package
        3. build_dat → .dat готов
        4. do.bat dt deploy → публикация на стенд
        5. Проверка в браузере → trace_errors при ошибках
        6. Исправление → повтор с п.2
        """;

    private const string ArchitecturePatterns = """
        # 14 Production-паттернов Directum RX

        ## Матрица паттернов по решениям
        | Паттерн | ESM | Agile | Targets | Когда использовать |
        |---------|-----|-------|---------|-------------------|
        | WebAPI endpoints | + | - | - | REST API для внешних систем |
        | AsyncHandler Lock-Safe | + | + | + | Параллельный доступ без deadlock |
        | ExponentialDelay | - | + | + | Конкурентный доступ, блокировки |
        | Versioned Init | + | + | + | Миграция данных по версиям |
        | Public DTO Structures | + | + | + | Сериализация через JSON |
        | Position Collections | - | + | - | Drag-and-drop, порядок элементов |
        | Soft Delete | - | + | - | Корзина, восстановление |
        | Hierarchy Traversal | - | - | + | Дерево отделов/целей/категорий |
        | Period Planning | - | - | + | KPI по периодам, план/факт |
        | Remote Components | + | - | + | React-контролы в карточке |
        | Role-Based Access | + | - | + | Выдача прав по ролям |
        | LocalizeFunction | + | - | - | Локализованные клиентские функции |
        | Multi-Session | - | + | - | Изоляция нескольких БД-операций |
        | IsolatedAreas | - | - | + | Aspose, ExcelDataReader |

        ## 1. AsyncHandler Lock-Safe
        ```csharp
        public virtual void ProcessEntity(AsyncHandlerInvokeArgs args)
        {
            var entityId = args.EntityId;
            var lockInfo = Locks.GetLockInfo(entity);
            if (lockInfo.IsLocked) { args.Retry = true; return; }
            // обработка...
        }
        ```

        ## 2. Versioned Init
        ```csharp
        public override void Initializing(ModuleInitializingEventArgs e)
        {
            Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
                this.FirstInit, Constants.Module.Init.Name, Version.Parse("0.0.1.0"));
            Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
                this.Init_v2, Constants.Module.Init.Name, Version.Parse("1.0.0.0"));
            Commons.PublicInitializationFunctions.Module.ModuleVersionInit(
                this.Init_v3, Constants.Module.Init.Name, Version.Parse("2.0.0.0"));
        }
        ```

        ## 3. Position Collections (drag-and-drop)
        Добавь свойство Position:int в CollectionProperty.
        При перемещении: пересчитай Position у всех элементов.

        ## 4. Soft Delete
        Добавь IsDeleted:bool + DeletedDate:date.
        Фильтруй: .Where(x => x.IsDeleted != true)
        Восстановление: entity.IsDeleted = false; entity.Save();

        ## 5. IsolatedAreas (внешние библиотеки)
        Для Aspose, ExcelDataReader — IsolatedFunctions в .Isolated проекте.
        Вызов из Server: IsolatedFunctions.ParseXlsx(bytes);
        Библиотека добавляется только в Isolated.csproj.

        ## 6. ExpressionElement (5 типов функций для workflow)
        GetFilteredEntities, GetPredefinedValues, GetDisplayName, Validate, Calculate
        Используются в RouteScheme для динамического workflow.

        ## 7. Email-to-Ticket (DCS интеграция)
        1. Настроить DCS (Document Capture Service) на отслеживание ящика
        2. AsyncHandler обрабатывает входящий email:
           - Парсит тему (regex) → определяет существующий тикет или создаёт новый
           - Парсит тело → текст комментария
           - Вложения → файлы к документу
        3. Ответ через email: обратная интеграция через SMTP

        ## 8. AI Agent Tool
        AsyncHandler с типом AIAgentTool в Module.mtd.
        Обрабатывает запрос от AI-агента, возвращает результат.
        ```json
        "AsyncHandlers": [{
            "Name": "AIAgentToolHandler",
            "DelayPeriod": 1,
            "DelayStrategy": "RegularDelayStrategy",
            "Parameters": [
                {"Name": "ToolCallId", "ParameterType": "String"},
                {"Name": "InputJson", "ParameterType": "String"}
            ]
        }]
        ```
        """;

    private const string UiCatalog = """
        # UI-каталог Directum RX

        ## Матрица: задача → решение
        | Задача | Решение | Пример |
        |--------|---------|--------|
        | Таблица с inline-редактированием | XtraGrid (DevExpress) | Targets: KeyResults |
        | Дерево/граф | Remote Component (React) | ESM: CIRelationsTree |
        | Графики | Remote Component | Targets: ChartsControl |
        | Kanban с drag-and-drop | Remote Component / SPA | Agile: KanbanBoard |
        | Каскадные поля | InputDialog + OnValueChanged | ESM: Category → Service |
        | Информационный блок | StateView | ESM: CMDB links |
        | KPI-число | Widget | ESM: ActiveRequests counter |
        | Печатный отчёт | FastReport (.frx) | Docflow: ApprovalSheet |
        | Генерация Word | Aspose.Words (Isolated) | Targets: TargetsMapReport |
        | Импорт Excel | Aspose.Cells (Isolated) | Targets: MetricMassImport |
        | Markdown в карточке | Remote Component | Targets: RichMarkdownEditor |
        | Timeline | Remote Component | CRM: Customer360 timeline |

        ## Каталог Remote Components (12 из production)
        | Компонент | Решение | Технологии | Назначение |
        |-----------|---------|------------|-----------|
        | ServiceCatalogControl | ESM | React + Tree | Каталог услуг |
        | CIRelationsTree | ESM | React + D3 | Граф связей CMDB |
        | WorkEvaluationControl | ESM | React + Stars | Оценка обращения |
        | TableControl | Targets | React + AG Grid | Таблица KPI |
        | ChartsControl | Targets | React + Chart.js | Графики план/факт |
        | GoalsMap | Targets | React + D3 | Карта целей (дерево) |
        | PeriodControl | Targets | React + DatePicker | Выбор периода |
        | RichMarkdownEditor | Targets | React + TipTap | Редактор текста |
        | KanbanBoard | Agile | React + DnD | Канбан-доска |
        | CRMDashboard | CRM | React + Ant Design | Дашборд продаж |
        | FunnelChart | CRM | React + Recharts | Воронка продаж |
        | Customer360 | CRM | React + Timeline | Карточка клиента |

        ## Библиотеки (C# + npm)
        C#: Newtonsoft.Json, Aspose.Words, Aspose.Cells, Svg, NaturalSort
        npm: react 18, react-dom, i18next, moment, antd, recharts, ag-grid

        ## FastReport .frx — структура
        ```xml
        <Report>
          <Dictionary>
            <SungeroSqlDataConnection Name="Connection1">
              <TableDataSource Name="Data" SelectCommand="SELECT ..." />
            </SungeroSqlDataConnection>
          </Dictionary>
          <ReportPage>
            <ReportTitleBand /> <!-- Заголовок -->
            <PageHeaderBand />  <!-- Верх каждой страницы -->
            <GroupHeaderBand />  <!-- Группировка -->
            <DataBand />         <!-- Данные -->
            <PageFooterBand />   <!-- Низ каждой страницы -->
          </ReportPage>
        </Report>
        ```

        ## InputDialog (NoCode-диалоги)
        ```csharp
        var dialog = Dialogs.CreateInputDialog("Заголовок");
        var name = dialog.AddString("Название", true);  // required
        var date = dialog.AddDate("Дата", true);
        var dept = dialog.AddSelect("Подразделение", true, Departments.GetAll());
        dialog.SetOnRefresh((e) => {
            if (dept.Value != null)
                name.Value = dept.Value.Name;
        });
        if (dialog.Show() == DialogButtons.Ok)
        {
            // используй name.Value, date.Value, dept.Value
        }
        ```

        ## StateView (информационный блок)
        ```csharp
        public Sungero.Core.StateView GetStateView()
        {
            var stateView = StateView.Create();
            var block = stateView.AddBlock();
            block.AddLabel("Статус: Активен");
            block.AddLabel("Просрочено: 3 задания");
            return stateView;
        }
        ```
        """;

    // === v4.0 CONTENT ===

    private const string CrmPatterns = """
        # CRM-паттерны Directum RX (из production DirRX.CRM v8)

        ## Ключевые GUID сущностей
        | Сущность | GUID | Модуль | Свойства |
        |----------|------|--------|----------|
        | Deal | a7f05f7d-19a3-4733-9432-1eb0ff68b56d | DirRX.CRMSales | 15 свойств, 6 actions |
        | Lead | cbd3a9f3-0652-43f5-bd32-36f9ee498c85 | DirRX.CRMMarketing | 20+ свойств, BANT |
        | Pipeline | dd530164-xxxx (префикс) | DirRX.CRMSales | Name, IsDefault, Stages(collection) |
        | Stage | da93667b-xxxx (префикс) | DirRX.CRMSales | Name, Position, Color, WipLimit, IsFinal |

        ## Pipeline Value формула
        Стоимость воронки = сумма всех активных сделок (не на финальном этапе).
        ```csharp
        public static double GetPipelineValue(long pipelineId)
        {
            return Deals.GetAll()
                .Where(d => d.Pipeline != null && d.Pipeline.Id == pipelineId)
                .Where(d => d.Stage != null && d.Stage.IsFinal != true)
                .Sum(d => d.Amount ?? 0);
        }
        ```

        ## Round-robin Job (распределение лидов)
        Фоновый процесс LeadAssignmentJob: берёт нераспределённых лидов и назначает менеджеров по кругу.
        ```csharp
        public virtual void ExecuteLeadAssignment()
        {
            var managers = Employees.GetAll()
                .Where(e => e.Status == Status.Active && e.Department?.Name == "Продажи")
                .ToList();
            if (!managers.Any()) return;

            var unassigned = Leads.GetAll(l => l.Manager == null).ToList();
            for (int i = 0; i < unassigned.Count; i++)
            {
                unassigned[i].Manager = managers[i % managers.Count];
                unassigned[i].Save();
            }
        }
        ```

        ## BANT Auto-Scoring
        4 boolean-свойства: HasBudget, HasAuthority, HasNeed, HasTimeline.
        Каждый true = +25 баллов, максимум 100.
        Реализация через ChangedShared events на каждом свойстве.
        ```csharp
        // В LeadHandlers.cs (Shared)
        public virtual void HasBudgetChangedShared(bool? newValue, bool? oldValue)
        {
            RecalculateBantScore(_obj);
        }

        public static void RecalculateBantScore(ILead lead)
        {
            int score = 0;
            if (lead.HasBudget == true) score += 25;
            if (lead.HasAuthority == true) score += 25;
            if (lead.HasNeed == true) score += 25;
            if (lead.HasTimeline == true) score += 25;
            lead.BantScore = score;
        }
        ```

        ## WIP Limit на Stage (ограничение количества сделок)
        При перемещении сделки проверяем лимит целевого этапа.
        ```csharp
        // В DealHandlers.cs (Server) — BeforeSaveServer
        public override void BeforeSaveServer(BeforeSaveEventArgs e)
        {
            if (_obj.State.Properties.Stage.IsChanged && _obj.Stage != null)
            {
                var wipLimit = _obj.Stage.WipLimit;
                if (wipLimit.HasValue && wipLimit.Value > 0)
                {
                    var currentCount = Deals.GetAll()
                        .Count(d => d.Stage != null && d.Stage.Id == _obj.Stage.Id && d.Id != _obj.Id);
                    if (currentCount >= wipLimit.Value)
                        e.AddError("Превышен лимит WIP для этапа: " + _obj.Stage.Name);
                }
            }
        }
        ```

        ## JSON Serialization без Newtonsoft
        Ручная сериализация через StringBuilder. Имена свойств ОБЯЗАНЫ совпадать со структурами.
        ```csharp
        public static string DealToJson(IDeal deal)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"Id\":{0}", deal.Id);
            sb.AppendFormat(",\"Name\":\"{0}\"", EscapeJson(deal.Name));
            sb.AppendFormat(",\"Amount\":{0}", deal.Amount?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "null");
            sb.AppendFormat(",\"Stage\":\"{0}\"", EscapeJson(deal.Stage?.Name));
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }
        ```

        ## Модульная архитектура CRM (5 модулей)
        ```
        DirRX.CRM (фасад: обложка, виджеты, Jobs)
          ├── DirRX.CRMSales (Deal, Pipeline, Stage, Activity)
          ├── DirRX.CRMMarketing (Lead, BANT, Campaign)
          ├── DirRX.CRMDocuments (перекрытие ContractualDocument)
          └── DirRX.CRMCommon (LossReason, Product, общие справочники)
        ```
        Модули НЕ знают друг о друге. Связь через PublicFunctions фасада DirRX.CRM.
        """;

    private const string EsmPatterns = """
        # ESM-паттерны Directum RX (из production rosa.ESM)

        ## Ключевой GUID
        | Сущность | GUID | Описание |
        |----------|------|----------|
        | RequestDatabook | 7d1c17bc-xxxx (префикс) | Обращение (основная сущность ESM) |

        ## Email-to-Ticket (DCS интеграция)
        Полный поток обработки входящего email:
        1. DCS (Document Capture Service) получает JSON-пакет с email
        2. AsyncHandler парсит тему: regex `\[Request#(\d{6})\]`
        3. Если номер найден → обновление существующего тикета (добавление комментария)
        4. Если номер НЕ найден → создание нового тикета
        5. Вложения → прикрепляются как файлы к документу
        6. Уведомление ответственному через Notice

        ```csharp
        // AsyncHandler: ProcessIncomingEmail
        public virtual void ProcessIncomingEmail(AsyncHandlerInvokeArgs args)
        {
            var packageId = args.PackageId;
            var package = BlobPackages.Get(packageId);
            var subject = package.Subject;

            // Поиск номера тикета в теме
            var match = System.Text.RegularExpressions.Regex.Match(
                subject, @"\[Request#(\d{6})\]");

            if (match.Success)
            {
                var ticketNumber = match.Groups[1].Value;
                var ticket = Requests.GetAll(r => r.RegistrationNumber == ticketNumber).FirstOrDefault();
                if (ticket != null)
                {
                    // Добавить комментарий
                    AddCommentFromEmail(ticket, package.Body);
                    AttachFiles(ticket, package.Blobs);
                    return;
                }
            }

            // Создать новый тикет
            var newTicket = Requests.Create();
            newTicket.Subject = subject;
            newTicket.Description = package.Body;
            newTicket.Save();
            AttachFiles(newTicket, package.Blobs);
        }
        ```

        ## SLA — 4 режима расчёта времени
        | Режим | Описание | Источник календаря |
        |-------|----------|-------------------|
        | User | Личный рабочий календарь сотрудника | Employee.Calendar |
        | Group | Календарь подразделения | Department.Calendar |
        | BusinessUnit | Календарь НОР | BusinessUnit.Calendar |
        | Overtime | 24/7 без выходных | null (простая разница дат) |

        Формула:
        ```csharp
        public static double GetSolvationTime(IRequest request)
        {
            var calendar = GetCalendarByMode(request.SlaMode);
            if (calendar == null)
            {
                // Режим Overtime (24/7)
                return (request.ClosingDate.Value - request.RegistrationDate.Value).TotalHours;
            }
            return WorkingTime.GetDurationInWorkingHours(
                request.RegistrationDate.Value,
                request.ClosingDate.Value,
                calendar);
        }
        ```

        ## Матричная приоритизация (Urgency x Influence)
        Двумерная матрица: Urgency (строки) x Influence (столбцы) → Priority.
        ```
        |            | Low    | Medium | High     | Critical |
        |------------|--------|--------|----------|----------|
        | Low        | Low    | Low    | Medium   | Medium   |
        | Medium     | Low    | Medium | High     | High     |
        | High       | Medium | High   | Critical | Critical |
        | Critical   | Medium | High   | Critical | Critical |
        ```
        Реализация: DatabookEntry "PriorityMatrix" с 3 свойствами: Urgency(enum), Influence(enum), Priority(enum).
        Поиск: `PriorityMatrices.GetAll(m => m.Urgency == urgency && m.Influence == influence).FirstOrDefault()?.Priority`

        ## AIAgentTool — саморегистрация инструментов
        Для каждой услуги (Service) создаётся AI-инструмент.
        AsyncHandler: CreateRequestFromTool
        ```csharp
        // Параметры: ToolCallId(string), InputJson(string)
        public virtual void CreateRequestFromTool(AsyncHandlerInvokeArgs args)
        {
            var toolCallId = args.ToolCallId;
            var inputJson = args.InputJson;

            // Десериализация входных данных
            var input = ParseToolInput(inputJson);

            // Создание обращения
            var request = Requests.Create();
            request.Subject = input.Subject;
            request.Service = Services.Get(input.ServiceId);
            request.Priority = CalculatePriority(request.Service);
            request.Save();

            // Возврат результата AI-агенту
            SetToolResult(toolCallId, request.Id.ToString());
        }
        ```

        ## ExpressionElement — 5 типов функций для workflow
        | Тип | Назначение | Пример |
        |-----|-----------|--------|
        | GetFilteredEntities | Фильтрация сущностей для выбора | Список ответственных по услуге |
        | GetPredefinedValues | Предзаполненные значения | SLA-параметры по категории |
        | GetDisplayName | Отображаемое имя | Формат "Request#000001: Тема" |
        | Validate | Валидация перед переходом | Проверка заполненности полей |
        | Calculate | Вычисление значений | Автоматическая приоритизация |

        ## AsyncHandlers ESM (14 обработчиков)
        | Name | Delay | Strategy | MaxRetry | Назначение |
        |------|-------|----------|----------|-----------|
        | ProcessIncomingEmail | 5 | Regular | 3 | Обработка входящего email |
        | EscalateOverdue | 15 | Exponential | 5 | Эскалация просроченных |
        | NotifyResponsible | 1 | Regular | 3 | Уведомление ответственного |
        | UpdateSlaMetrics | 10 | Regular | 3 | Пересчёт SLA-метрик |
        | SyncCmdbRelations | 30 | Exponential | 5 | Синхронизация CMDB |
        | CreateRequestFromTool | 1 | Regular | 3 | AI Agent Tool |
        | ProcessAutoReply | 5 | Regular | 3 | Автоответ по шаблону |
        | CloseInactiveRequests | 60 | Regular | 1 | Закрытие неактивных |
        | RecalculatePriority | 1 | Regular | 3 | Пересчёт приоритета |
        | SendSatisfactionSurvey | 5 | Regular | 3 | Опрос удовлетворённости |
        | ImportFromExternalSystem | 30 | Exponential | 5 | Импорт из внешней системы |
        | GenerateReport | 15 | Regular | 3 | Генерация отчёта |
        | ArchiveResolved | 60 | Regular | 1 | Архивация решённых |
        | SyncKnowledgeBase | 30 | Exponential | 5 | Синхронизация базы знаний |

        ## Jobs ESM (5 фоновых процессов)
        | Name | График | Цель |
        |------|--------|------|
        | EscalationJob | Каждые 15 мин | Проверка SLA и эскалация |
        | AutoCloseJob | Ежедневно 02:00 | Закрытие неактивных тикетов |
        | SlaReportJob | Еженедельно Пн 08:00 | Генерация SLA-отчёта |
        | CmdbSyncJob | Каждые 30 мин | Синхронизация CMDB |
        | KnowledgeBaseSync | Ежедневно 03:00 | Обновление базы знаний |
        """;

    private const string TargetsPatterns = """
        # Targets/KPI-паттерны Directum RX (из production DirRX.Targets)

        ## RemoteTableControl (AG Grid + CRUD + ChangeTracking)
        Компонент для inline-редактирования таблицы KPI в карточке сущности.
        Протокол ChangeTracking: клиент отправляет массив изменений с метаданными.
        ```json
        // Формат изменённой строки
        {
            "Id": 42,
            "Name": "KPI-001",
            "Value": 85.5,
            "_ChangedColumns": ["Value"],
            "_ChangedFrom": {"Value": 80.0},
            "_ChangeType": "update"
        }
        ```
        _ChangeType: "insert" | "update" | "delete"
        _ChangedColumns: массив имён изменённых колонок
        _ChangedFrom: предыдущие значения (для undo/audit)

        Серверный WebAPI для TableControl:
        ```csharp
        // GET: Метаданные таблицы (колонки, типы, права)
        [Public(WebApiRequestType = RequestType.Get)]
        public virtual string GetTableMetadata(long entityId)
        {
            var entity = Entities.Get(entityId);
            // Возвращает JSON с описанием колонок, типов, editability
            return BuildTableMetadataJson(entity);
        }

        // POST: Пакетное обновление строк
        [Public(WebApiRequestType = RequestType.Post)]
        public virtual string BatchUpdate(string jsonInput)
        {
            var changes = ParseChanges(jsonInput);
            foreach (var change in changes)
            {
                switch (change.ChangeType)
                {
                    case "insert": CreateRow(change); break;
                    case "update": UpdateRow(change); break;
                    case "delete": DeleteRow(change); break;
                }
            }
            return "{\"status\":\"ok\"}";
        }
        ```

        ## Fan-out Async (Master → N Executors)
        Паттерн: Master AsyncHandler создаёт N дочерних Executor AsyncHandler'ов.
        Используется для параллельной обработки (например, рассылка по N подразделениям).
        ```csharp
        // Master handler
        public virtual void DistributeTargets(AsyncHandlerInvokeArgs args)
        {
            var periodId = args.PeriodId;
            var departments = Departments.GetAll().Where(d => d.Status == Status.Active).ToList();

            foreach (var dept in departments)
            {
                // Создание дочернего handler для каждого подразделения
                var asyncHandler = AsyncHandlerInvokeArgs.Create(
                    Constants.Module.ExecuteTargetCalculation);
                asyncHandler.PeriodId = periodId;
                asyncHandler.DepartmentId = dept.Id;
                asyncHandler.ExecuteAsync();
            }
        }

        // Executor handler (для одного подразделения)
        public virtual void ExecuteTargetCalculation(AsyncHandlerInvokeArgs args)
        {
            var periodId = args.PeriodId;
            var departmentId = args.DepartmentId;
            // Расчёт KPI для конкретного подразделения...
        }
        ```

        ## Licensing через пустой модуль
        Паттерн проверки лицензии: создаётся пустой модуль с IsLicensed=true.
        ```csharp
        public static bool IsModuleLicensed()
        {
            try
            {
                // Попытка обратиться к лицензируемому модулю
                var dummy = DirRX.TargetsPremium.PublicFunctions.Module.Remote.CheckLicense();
                return true;
            }
            catch
            {
                return false;
            }
        }
        ```
        В Module.mtd лицензируемого модуля: `"IsLicensed": true`

        ## XLSX Pipeline (6 шагов массового импорта)
        ```
        1. Template    → Скачивание шаблона XLSX с заголовками
        2. Parse       → IsolatedFunction парсит XLSX через ExcelDataReader
        3. Validate    → Проверка типов, обязательных полей, ссылок
        4. Report      → Генерация отчёта об ошибках (если есть)
        5. Import      → Создание/обновление сущностей через Server Functions
        6. Cleanup     → Удаление временных файлов, AsyncHandler cleanup
        ```
        ```csharp
        // Isolated Function (парсинг XLSX)
        [Public]
        public virtual List<Structures.Module.ImportRow> ParseXlsxFile(byte[] fileBytes)
        {
            var result = new List<Structures.Module.ImportRow>();
            using (var stream = new MemoryStream(fileBytes))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet();
                var sheet = dataSet.Tables[0];
                for (int row = 1; row < sheet.Rows.Count; row++)
                {
                    result.Add(new Structures.Module.ImportRow
                    {
                        Name = sheet.Rows[row][0]?.ToString(),
                        Value = Convert.ToDouble(sheet.Rows[row][1]),
                        DepartmentName = sheet.Rows[row][2]?.ToString()
                    });
                }
            }
            return result;
        }
        ```

        ## Word Processing (Aspose.Words в Isolated)
        Генерация документов Word по шаблону.
        ```csharp
        // Isolated Function
        public virtual byte[] GenerateTargetsMap(byte[] templateBytes, string jsonData)
        {
            var doc = new Aspose.Words.Document(new MemoryStream(templateBytes));

            // Удаление якорных тегов {{TAG}}
            RemoveAnchorTags(doc, "{{START}}", "{{END}}");

            // Заполнение таблиц
            ProcessTables(doc, jsonData);

            // Настройка полей
            SetMargins(doc, 1.0, 1.0, 1.5, 1.5); // top, bottom, left, right (cm)

            using (var output = new MemoryStream())
            {
                doc.Save(output, Aspose.Words.SaveFormat.Docx);
                return output.ToArray();
            }
        }
        ```

        ## 6 Remote Components (RC) в Targets
        | Компонент | Технологии | Назначение |
        |-----------|-----------|-----------|
        | GoalsMap | React + D3.js | Карта целей (дерево с drag-and-drop) |
        | TableControl | React + AG Grid | Таблица KPI с inline-редактированием |
        | ChartsControl | React + Chart.js | Графики план/факт (bar, line, pie) |
        | PeriodControl | React + DatePicker | Выбор периода (квартал, год) |
        | RichMarkdownEditor | React + TipTap | Редактор описания цели |
        | AnalyticsControl | React + Recharts | Аналитические дашборды |

        ## Parametrized Widgets (виджеты с параметрами)
        Виджеты могут принимать параметры для фильтрации.
        ```json
        // В Module.mtd → Widgets
        {
            "Name": "TargetsByPeriod",
            "Parameters": [
                {
                    "Name": "Period",
                    "ParameterType": "NavigationParameter",
                    "EntityGuid": "<PeriodEntityGuid>"
                },
                {
                    "Name": "ShowCompleted",
                    "ParameterType": "Boolean"
                },
                {
                    "Name": "ViewMode",
                    "ParameterType": "Enum",
                    "Values": ["Summary", "Detailed", "Chart"]
                }
            ]
        }
        ```

        ## WebAPI для Remote Components
        ```csharp
        // GET: Метаданные для TableControl
        [Public(WebApiRequestType = RequestType.Get)]
        public virtual string GetTableMetadata(long targetId)
        {
            var target = Targets.Get(targetId);
            return SerializeTableMetadata(target);
        }

        // POST: Пакетное обновление из TableControl
        [Public(WebApiRequestType = RequestType.Post)]
        public virtual string BatchUpdateKeyResults(string jsonInput)
        {
            var changes = ParseBatchChanges(jsonInput);
            var results = new List<string>();
            foreach (var change in changes)
            {
                ApplyChange(change);
                results.Add(change.Id.ToString());
            }
            return SerializeResults(results);
        }
        ```
        """;
}
