using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Resources;

[McpServerResourceType]
public class RuntimeKnowledgeBase
{
    [McpServerResource(UriTemplate = "directum-rx://knowledge/odata-schema", Name = "OData Schema Guide", MimeType = "text/plain")]
    [Description("Справочник OData API Directum RX: типы сущностей, свойства, Actions, фильтрация, навигация")]
    public static string GetODataSchema() => ODataSchema;

    [McpServerResource(UriTemplate = "directum-rx://knowledge/task-workflow", Name = "Task Workflow Guide", MimeType = "text/plain")]
    [Description("Работа с задачами и заданиями через OData: создание, выполнение, делегирование, мониторинг")]
    public static string GetTaskWorkflow() => TaskWorkflow;

    [McpServerResource(UriTemplate = "directum-rx://knowledge/document-operations", Name = "Document Operations", MimeType = "text/plain")]
    [Description("Операции с документами через OData: поиск, создание, версии, подписи, жизненный цикл")]
    public static string GetDocumentOperations() => DocumentOperations;

    [McpServerResource(UriTemplate = "directum-rx://knowledge/analytics-patterns", Name = "Analytics Patterns", MimeType = "text/plain")]
    [Description("Аналитика и отчётность: загрузка, просрочки, bottleneck, KPI подразделений")]
    public static string GetAnalyticsPatterns() => AnalyticsPatterns;

    private const string ODataSchema = """
        # OData API Directum RX — справочник

        ## Базовый URL
        http://server/Integration/odata/

        ## Основные типы сущностей
        | OData имя | Directum тип | Описание |
        |-----------|-------------|----------|
        | IEmployees | Employee | Сотрудники |
        | IDepartments | Department | Подразделения |
        | IBusinessUnits | BusinessUnit | НОР |
        | IOfficialDocuments | OfficialDocument | Документы |
        | ISimpleTasks | SimpleTask | Простые задачи |
        | IActionItemExecutionTasks | ActionItemExecutionTask | Поручения |
        | IAssignments | Assignment | Задания |
        | ICounterparties | Counterparty | Контрагенты |
        | ICompanies | Company (Org) | Организации |
        | IPersons | Person | Физ. лица |
        | IContacts | Contact | Контакты |

        ## Фильтрация (OData $filter)
        - Равенство: $filter=Name eq 'Иванов'
        - Содержит: $filter=contains(Name, 'Иван')
        - Дата: $filter=Created ge 2024-01-01T00:00:00Z
        - Навигация: $filter=Department/Name eq 'IT'
        - Enum: $filter=Status eq 'Active'
        - Логика: $filter=Status eq 'Active' and Department/Id eq 123

        ## Сортировка и пагинация
        - $orderby=Created desc
        - $top=50&$skip=100
        - $count=true

        ## Expand (связанные сущности)
        - $expand=Department
        - $expand=Author,OurSignatory
        - $expand=Performers($select=Id,Name)

        ## Actions (серверные операции)
        - POST /Entity(id)/Action — вызов action
        - Пример: POST /IAssignments(123)/Complete
        - Body: {"Result": "Completed"}

        ## Аутентификация
        - HTTP Basic Auth
        - Сервисный пользователь с правами Integration Service
        """;

    private const string TaskWorkflow = """
        # Работа с задачами через OData

        ## Создание задачи
        POST /ISimpleTasks
        Body: {
          "Subject": "Тема задачи",
          "Deadline": "2024-12-31T18:00:00Z",
          "Performers": [{"Id": 123}],
          "Importance": "High"
        }

        ## Получение моих заданий
        GET /IAssignments?$filter=Performer/Id eq {userId} and Status eq 'InProcess'
        &$orderby=Deadline asc
        &$expand=Task,Author
        &$top=50

        ## Выполнение задания
        POST /IAssignments({id})/Complete
        Body: {"Result": "Completed"}

        Результаты выполнения:
        - Completed — выполнено
        - ForReapproval — на переработку
        - Forward — переадресовано
        - Approved — согласовано
        - Rejected — отклонено

        ## Мониторинг задач
        Просроченные задания:
        GET /IAssignments?$filter=Status eq 'InProcess' and Deadline lt {now}

        Задачи под контролем:
        GET /IActionItemExecutionTasks?$filter=IsUnderControl eq true and Status eq 'InProcess'

        ## Делегирование
        POST /IAssignments({id})/Forward
        Body: {"ForwardTo": {"Id": 456}}

        ## Типы задач
        | Тип | OData | Назначение |
        |-----|-------|-----------|
        | SimpleTask | ISimpleTasks | Простая задача |
        | ActionItemExecutionTask | IActionItemExecutionTasks | Поручение |
        | ApprovalTask | IApprovalTasks | Согласование |
        | ReviewTask | IReviewTasks | Рассмотрение |
        """;

    private const string DocumentOperations = """
        # Документы через OData

        ## Поиск документов
        По имени: GET /IOfficialDocuments?$filter=contains(Name, 'договор')
        По типу: GET /IContracts?$filter=LifeCycleState eq 'Active'
        По автору: GET /IOfficialDocuments?$filter=Author/Id eq 123
        По дате: GET /IOfficialDocuments?$filter=Created ge 2024-01-01T00:00:00Z

        ## Версии документа
        GET /IOfficialDocuments({id})/Versions
        Последняя версия: $orderby=Number desc&$top=1

        ## Жизненный цикл
        | Состояние | Описание |
        |-----------|----------|
        | Draft | Черновик |
        | Active | Действующий |
        | Obsolete | Устаревший |

        ## Подписи
        GET /IOfficialDocuments({id})/Signatures
        Типы: Approval (согласование), Endorsing (утверждение), Sign (подпись)

        ## Создание документа
        POST /ISimpleDocuments
        Body: {
          "Name": "Название",
          "DocumentKind": {"Id": 1}
        }

        ## Типы документов
        | OData | Тип | Описание |
        |-------|-----|----------|
        | IContracts | Contract | Договор |
        | ISupAgreements | SupAgreement | Допсоглашение |
        | IIncomingLetters | IncomingLetter | Входящее письмо |
        | IOutgoingLetters | OutgoingLetter | Исходящее письмо |
        | IOrders | Order | Приказ |
        | IMemos | Memo | Служебная записка |
        """;

    private const string AnalyticsPatterns = """
        # Аналитика Directum RX

        ## Загрузка подразделения
        1. Получить сотрудников подразделения
        2. Для каждого: count заданий InProcess
        3. Для каждого: count просроченных (Deadline < now)
        4. Сортировать по загрузке

        Запрос:
        GET /IAssignments?$filter=Performer/Department/Id eq {deptId} and Status eq 'InProcess'
        &$apply=groupby((Performer/Name), aggregate($count as TaskCount))

        ## Просроченные задания
        GET /IAssignments?$filter=Status eq 'InProcess' and Deadline lt {now}
        &$expand=Task($select=Subject),Performer($select=Name),Author($select=Name)
        &$orderby=Deadline asc

        ## Статистика процессов
        GET /IActionItemExecutionTasks?$filter=Created ge {startDate}
        &$apply=groupby((Status), aggregate($count as Count))

        ## KPI показатели
        - Среднее время выполнения: avg(CompletedDate - Created)
        - % просрочки: count(overdue) / count(total)
        - Bottleneck: исполнитель с max InProcess assignments
        - Конверсия согласования: Approved / (Approved + Rejected)

        ## Отчёт по рискам дедлайнов
        Задания, дедлайн в ближайшие N дней:
        GET /IAssignments?$filter=Status eq 'InProcess'
          and Deadline ge {now}
          and Deadline le {now + N days}
        &$orderby=Deadline asc

        ## Воронка задач
        1. Создано за период: $filter=Created ge {start} and Created le {end}
        2. В работе: Status eq 'InProcess'
        3. Выполнено: Status eq 'Completed'
        4. Просрочено: CompletedDate gt Deadline
        """;
}
