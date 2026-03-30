using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Prompts;

[McpServerPromptType]
public class DirectumWorkflowPrompts
{
    [McpServerPrompt, Description("Создать решение Directum RX с нуля по описанию. Интеллектуальный ассистент: уточняет требования, проектирует архитектуру, генерирует код, валидирует, собирает .dat.")]
    public static IEnumerable<PromptMessage> CreateSolution(
        [Description("Описание решения на естественном языке (например: 'CRM для отдела продаж' или 'система управления обращениями')")] string description,
        [Description("Код компании (например: DirRX)")] string companyCode = "DirRX")
    {
        // System message с полным контекстом платформы
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Я хочу создать решение Directum RX: {description}
                    Код компании: {companyCode}

                    Ты — эксперт-архитектор Directum RX. У тебя есть полная knowledge base платформы через MCP Resources.

                    ## ШАГ 1: ИССЛЕДОВАНИЕ (ОБЯЗАТЕЛЬНО)
                    Прочитай ресурсы В ЭТОМ ПОРЯДКЕ:

                    ПЕРВЫМ ДЕЛОМ:
                    - directum://knowledge/entity-catalog — ПОЛНЫЙ каталог сущностей платформы.
                      Здесь ВСЕ что есть: Employee, Department, Counterparty, Contract и ещё 25+ сущностей.
                      НЕ создавай то, что уже есть. Перекрывай (override) если нужно добавить поля.

                    ЗАТЕМ:
                    - directum://knowledge/platform-rules — критические правила
                    - directum://knowledge/entity-types — выбор типов (DatabookEntry vs Document vs Task)
                    - directum://knowledge/solution-design — архитектурные паттерны (CRM, ESM, HR)
                    - directum://knowledge/module-guids — GUID для Dependencies и NavigationProperty
                    - directum://knowledge/property-types — все типы свойств

                    ## ШАГ 2: УТОЧНЕНИЕ ТРЕБОВАНИЙ
                    Задай мне 3-5 уточняющих вопросов:
                    - Какие основные бизнес-объекты? (сделки, обращения, задачи, проекты...)
                    - Нужен ли workflow/согласование?
                    - Нужна ли интеграция с другими системами?
                    - Какие свойства у ключевых сущностей?
                    - Нужна ли визуализация (Kanban, графики)?
                    НЕ проектируй пока не получишь ответы!

                    ## ШАГ 3: ПРОЕКТИРОВАНИЕ
                    После получения ответов:
                    1. Определи модули и их Dependencies
                    2. Для КАЖДОЙ потребности — СНАЧАЛА проверь entity-catalog:
                       - Есть в платформе? → ИСПОЛЬЗУЙ как есть
                       - Есть похожее? → ПЕРЕКРОЙ (override), добавь свои свойства
                       - Нет аналога? → СОЗДАЙ новое (scaffold_entity mode=new)
                    3. Для каждой НОВОЙ сущности определи тип:
                       - Простые данные без файлов → DatabookEntry
                       - Нужны файлы/коллекции → Document
                       - Нужен workflow → Task + Assignment + Notice
                    4. NavigationProperty: используй GUID из entity-catalog (Employee=b7905516, Department=61b1c19f и т.д.)
                    5. Согласование: НЕ пиши свою Task — настрой ApprovalRule!
                    6. Виды документов: НЕ создавай новый тип — создай DocumentKind!
                    7. Для подсказки: search_metadata name=<похожая_сущность>

                    ПОКАЖИ МНЕ ПРОЕКТ и подожди подтверждения!

                    ## ШАГ 4: ГЕНЕРАЦИЯ
                    После подтверждения:
                    1. scaffold_module — создать модуль с Cover
                    2. scaffold_entity для КАЖДОЙ сущности
                    3. check_package после КАЖДОЙ сущности
                    4. fix_package если есть ошибки
                    5. scaffold_function — серверные/клиентские функции

                    ## ШАГ 5: ФИНАЛИЗАЦИЯ
                    1. Финальный check_package → 14/14
                    2. build_dat → готовый .dat пакет
                    3. Покажи итоговый отчёт: модули, сущности, файлы, .dat путь

                    ## ВАЖНО
                    - НЕ генерируй из головы — СНАЧАЛА ищи пример в платформе через search_metadata
                    - Если что-то не получается — спроси у меня, не угадывай
                    - Каждое решение должно быть PRODUCTION-ready
                    """
            }
        };
    }

    [McpServerPrompt, Description("Создать сущность с правильным типом, свойствами и валидацией.")]
    public static IEnumerable<PromptMessage> CreateEntity(
        [Description("Имя сущности (PascalCase)")] string entityName,
        [Description("Тип: databook | document | task")] string baseType = "databook",
        [Description("Свойства: 'Name:string, Amount:double, Status:enum(Active,Closed)'")] string properties = "")
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Создай сущность {entityName} типа {baseType} со свойствами: {properties}

                    ПЕРЕД СОЗДАНИЕМ:
                    1. Прочитай directum://knowledge/platform-rules и directum://knowledge/property-types
                    2. Найди эталон в платформе:
                       - databook → search_metadata name=Employee
                       - document → search_metadata name=ContractualDocument
                       - task → search_metadata name=ActionItemExecutionTask
                    3. Проверь ограничения:
                       - databook + коллекции → СТОП, используй document
                       - Enum со значениями New/Default/Class → переименуй (NewValue, DefaultValue)
                       - NavigationProperty → проверь EntityGuid в directum://knowledge/module-guids

                    СОЗДАНИЕ:
                    scaffold_entity с правильными параметрами

                    ПРОВЕРКА:
                    check_package → 14/14. Если FAIL → fix_package → повтори check_package.
                    """
            }
        };
    }

    [McpServerPrompt, Description("Проверить пакет и исправить ошибки автоматически.")]
    public static IEnumerable<PromptMessage> ValidateAndFix(
        [Description("Путь к пакету")] string packagePath)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Проверь и исправь пакет: {packagePath}

                    1. check_package {packagePath} → отчёт 14 проверок
                    2. Есть FAIL с AutoFix? → fix_package {packagePath} dryRun=false
                    3. Есть FAIL без AutoFix? → объясни причину, предложи решение
                       Используй directum://knowledge/platform-rules для диагностики
                    4. Повтори check_package → всё зелёное? → build_dat
                    """
            }
        };
    }

    [McpServerPrompt, Description("Разобраться с ошибкой импорта .dat в DDS.")]
    public static IEnumerable<PromptMessage> DebugImportError(
        [Description("Текст ошибки")] string errorMessage)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Ошибка импорта DDS: {errorMessage}

                    Прочитай directum://knowledge/platform-rules для контекста.

                    Типовые ошибки:
                    - "Missing area" / NullReferenceException InterfacesGenerator → CollectionProperty в DatabookEntry → сменить на Document
                    - "Reserved word" → enum value new/class/default → переименовать (NewValue, DefaultValue)
                    - "Duplicate column" → Code не уникален → добавить префикс модуля
                    - "File lock" .csproj → перезапустить DDS (kill dotnet.exe)
                    - "Недопустимый ввод ResX" → нет обязательного resheader в .resx
                    - "Can't resolve function" → FunctionName в Cover ≠ имя метода в ModuleClientFunctions.cs
                    - Пустые подписи полей → Resource_GUID вместо Property_Name в System.resx

                    Если не из списка:
                    1. check_package — может поймать причину
                    2. trace_errors — полные логи DDS
                    3. search_metadata — найти рабочий аналог в base/ для сравнения
                    4. Предложи 2-3 варианта решения. НЕ повторяй один fix.
                    """
            }
        };
    }

    [McpServerPrompt, Description("Code review пакета разработки Directum RX — проверка архитектуры, качества, платформенных паттернов.")]
    public static IEnumerable<PromptMessage> ReviewPackage(
        [Description("Путь к пакету или модулю")] string packagePath)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Проведи code review пакета: {packagePath}

                    Прочитай ресурсы:
                    - directum://knowledge/platform-rules
                    - directum://knowledge/csharp-patterns
                    - directum://knowledge/csharp-functions

                    ## Проверки:
                    1. СТРУКТУРА: check_package → 14/14
                    2. АРХИТЕКТУРА:
                       - Правильный выбор типов сущностей?
                       - Нет ли дублирования с платформой? (search_metadata)
                       - Dependencies минимальны и корректны?
                    3. КОД:
                       - check_code_consistency → MTD ↔ C# синхронизация
                       - Нет DateTime.Now? (Calendar.Now)
                       - Нет Session.Execute? (ExecuteSQLCommand)
                       - Правильные namespace (.Server, .Client, .Shared)?
                    4. RESX:
                       - check_resx → все ключи на месте
                       - Нет Resource_GUID?
                       - Все DisplayName заполнены?
                    5. БЕЗОПАСНОСТЬ:
                       - check_permissions → права доступа
                       - Нет SQL injection в Queries?
                       - Нет захардкоженных credentials?

                    Выдай отчёт с оценкой 1-5 по каждой категории и рекомендациями.
                    """
            }
        };
    }

    [McpServerPrompt, Description("Создать задачу с workflow: Task + Assignment + Notice + блоки + RouteScheme.")]
    public static IEnumerable<PromptMessage> CreateTaskWorkflow(
        [Description("Описание задачи (например: 'Задача на согласование заявки на отпуск')")] string description,
        [Description("Модуль")] string moduleName = "")
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Создай задачу с workflow: {description}
                    {(string.IsNullOrWhiteSpace(moduleName) ? "" : $"Модуль: {moduleName}")}

                    Прочитай ресурсы:
                    - directum://knowledge/entity-catalog — проверь нет ли готовой задачи
                    - directum://knowledge/workflow-patterns — блоки, RouteScheme, обработчики
                    - directum://knowledge/entity-types — Task/Assignment/Notice форматы

                    ВАЖНО: Сначала проверь — может быть достаточно ApprovalTask + ApprovalRule (настройка, не код)?

                    Если нужна кастомная задача:
                    1. scaffold_task — создаёт Task + Assignment + Notice
                    2. Определи блоки workflow: Script, Assignment, Notice, Monitoring
                    3. Определи переходы между блоками (RouteScheme)
                    4. modify_workflow — добавь блоки в Module.mtd
                    5. Реализуй BlockHandlers
                    6. validate_workflow — проверь маршрут
                    """
            }
        };
    }

    [McpServerPrompt, Description("Диагностика ошибки DDS: pattern-matching + рекомендации + auto-fix.")]
    public static IEnumerable<PromptMessage> DiagnoseError(
        [Description("Текст ошибки из DDS/логов")] string errorText)
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Ошибка: {errorText}

                    1. diagnose_build_error — pattern-matching по 10 известным ошибкам
                    2. Если не нашёл — check_package для структурной диагностики
                    3. Если не помогло — trace_errors для полных логов
                    4. Прочитай directum://knowledge/platform-rules
                    5. Для сравнения: search_metadata для рабочего аналога
                    """
            }
        };
    }

    [McpServerPrompt, Description("Перекрыть существующую сущность Directum RX — добавить свойства, действия, логику.")]
    public static IEnumerable<PromptMessage> OverrideEntity(
        [Description("Имя сущности для перекрытия (например: Counterparty)")] string entityName,
        [Description("Что добавить (например: 'поле ИНН, поле ОГРН, действие Проверить')")] string additions = "")
    {
        yield return new PromptMessage
        {
            Role = Role.User,
            Content = new TextContentBlock
            {
                Text = $"""
                    Перекрой сущность {entityName}: {additions}

                    ## ШАГ 1: ИССЛЕДОВАНИЕ
                    1. extract_entity_schema entity={entityName} — получи текущую схему
                    2. search_metadata name={entityName} — найди .mtd файл
                    3. Прочитай directum://knowledge/module-guids — найди GUID сущности и модуля

                    ## ШАГ 2: ПЕРЕКРЫТИЕ
                    scaffold_entity mode=override
                    - ancestorGuid = GUID оригинальной сущности
                    - moduleName = твой модуль (не оригинальный!)
                    - Добавляй ТОЛЬКО новые свойства
                    - НЕ дублируй существующие свойства (IsAncestorMetadata: true)

                    ## ШАГ 3: ПРОВЕРКА
                    check_package → убедись что Dependencies содержат модуль оригинала
                    """
            }
        };
    }
}
