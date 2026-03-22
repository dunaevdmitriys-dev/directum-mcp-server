using System.ComponentModel;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SuggestPatternTool
{
    [McpServerTool(Name = "suggest_pattern")]
    [Description("Найти подходящий паттерн реализации для задачи. Встроенная база знаний из 50 паттернов: AgileBoard, Targets, ESM, CRM, платформа.")]
    public string Suggest(
        [Description("Описание задачи: что нужно реализовать (например: 'REST API для SPA', 'email → обращение', 'кастомная таблица на карточке')")]
        string task)
    {
        var taskLower = task.ToLowerInvariant();
        var results = new List<PatternMatch>();

        foreach (var pattern in Patterns)
        {
            var score = 0;
            foreach (var keyword in pattern.Keywords)
            {
                if (taskLower.Contains(keyword))
                    score += 10;
            }
            if (score > 0)
                results.Add(new PatternMatch(pattern, score));
        }

        if (results.Count == 0)
        {
            // Fuzzy: check each word of the task against keywords
            var words = taskLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pattern in Patterns)
            {
                var score = 0;
                foreach (var word in words)
                {
                    if (word.Length < 3) continue;
                    foreach (var kw in pattern.Keywords)
                    {
                        if (kw.Contains(word) || word.Contains(kw))
                            score += 5;
                    }
                }
                if (score > 0)
                    results.Add(new PatternMatch(pattern, score));
            }
        }

        results = results.OrderByDescending(r => r.Score).Take(5).ToList();

        if (results.Count == 0)
            return "Подходящий паттерн не найден. Попробуйте:\n" +
                   "- `search_metadata` для поиска по платформенным модулям\n" +
                   "- Посмотрите `base/Sungero.Docflow/` — самый богатый модуль\n" +
                   "- Прочитайте `knowledge-base/patterns-catalog.md`";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Найдено паттернов: {results.Count}");
        sb.AppendLine();

        foreach (var match in results)
        {
            var p = match.Pattern;
            sb.AppendLine($"## {p.Name}");
            sb.AppendLine($"**Источник:** {p.Source}");
            sb.AppendLine($"**Когда использовать:** {p.WhenToUse}");
            sb.AppendLine($"**Суть:** {p.Summary}");
            if (!string.IsNullOrEmpty(p.BaseReference))
                sb.AppendLine($"**Reference в base/:** {p.BaseReference}");
            if (!string.IsNullOrEmpty(p.CodeExample))
            {
                sb.AppendLine("**Пример:**");
                sb.AppendLine($"```csharp\n{p.CodeExample}\n```");
            }
            if (!string.IsNullOrEmpty(p.Pitfalls))
                sb.AppendLine($"**Подводные камни:** {p.Pitfalls}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private record PatternMatch(Pattern Pattern, int Score);

    private record Pattern(
        string Name, string Source, string WhenToUse, string Summary,
        string? BaseReference, string? CodeExample, string? Pitfalls,
        string[] Keywords);

    private static readonly Pattern[] Patterns =
    [
        // === WEBAPI ===
        new("REST API через WebApiRequestType",
            "AgileBoard (30+ endpoints), Sungero.Shell (30+ endpoints)",
            "Когда SPA или внешний клиент должен вызывать серверные функции по HTTP",
            "[Public(WebApiRequestType = RequestType.Post/Get)] на серверной функции → автоматический HTTP-эндпоинт через Integration Service",
            "base/Sungero.Shell/.../Module.mtd — 30+ PublicFunctions с WebApiRequestType",
            "[Public(WebApiRequestType = RequestType.Post)]\npublic virtual string MyEndpoint(long entityId) { ... }",
            "GET — только примитивные параметры. POST — поддерживает структуры. Каждый эндпоинт сам проверяет права.",
            ["webapi", "rest", "api", "spa", "http", "endpoint", "integration"]),

        // === REAL-TIME ===
        new("Real-time через ClientManager",
            "AgileBoard",
            "Когда несколько пользователей работают с одними данными и должны видеть изменения мгновенно",
            "ClientManager.Instance.GetClientsOfUser(userId) → получить GUID сессий → AgileMessageSender.Send*(clientIds, appId, data)",
            null,
            "var clientIds = GetBoardUsers(boardId)\n  .SelectMany(u => ClientManager.Instance.GetClientsOfUser(u.Id))\n  .Distinct().ToArray();\nAgileMessageSender.SendTicketUpdated(clientIds, appId, boardId, data);",
            "ClientManager — синглтон, только на сервере. Передавайте appId инициатора чтобы он не получил дубль.",
            ["real-time", "realtime", "push", "обновлени", "мгновенн", "collaborative"]),

        // === HISTORY ===
        new("Кастомная история через SQL",
            "AgileBoard/DirRX.History",
            "Когда стандартная RX-история недостаточна: нужны значения свойств, JSON, выборки по дате",
            "SQL-таблица с JSONB-колонкой Properties. INSERT в Saving() через Reflection (IsChanged). Батч по 1000.",
            null,
            "INSERT INTO History(UserId, Date, EntityGuid, EntityId, Properties)\nVALUES (@UserId, @Date, @EntityGuid, @EntityId, @Properties::jsonb);",
            "SQL.GetCurrentConnection() для INSERT внутри Saving, SQL.CreateConnection() для SELECT. Батч по 1000 для производительности.",
            ["истори", "history", "аудит", "журнал", "лог", "tracking", "изменени"]),

        // === MANY-TO-MANY ===
        new("Граф связей (many-to-many) через SQL DDL",
            "AgileBoard",
            "Когда нужна связь many-to-many которую невозможно через CollectionPropertyMetadata",
            "CREATE TABLE с from_id, to_id, link_type. CRUD через прямой SQL. Создание в ModuleInitializer.",
            null,
            "CREATE TABLE Relations (\n  id SERIAL PRIMARY KEY,\n  from_id bigint NOT NULL,\n  to_id bigint NOT NULL,\n  link_type int NOT NULL\n);",
            "Индексы на обе стороны обязательны. Каскадное удаление вручную. link_type как int — документировать маппинг.",
            ["many-to-many", "связ", "граф", "relation", "ссылк", "m2m"]),

        // === EMAIL ===
        new("Email-to-Ticket через DCS",
            "ESM (rosa.ESM)",
            "Когда входящая почта должна автоматически создавать обращения/заявки",
            "ProccessDCSPackage(base64Json) → десериализация → regex поиск номера в теме → обновление или создание. Вложения → AttachmentToRequest.",
            null,
            "[Public(WebApiRequestType = RequestType.Post)]\npublic void ProccessDCSPackage(string packageJson)\n{\n  var package = Package.Deserialize(Convert.FromBase64String(packageJson));\n  var mailInfo = package.MailCaptureInstanceInfo;\n  var request = GetRequestBySubject(mailInfo.Subject);\n  if (request != null) UpdateFromEmail(request, mailInfo);\n  else CreateFromEmail(mailInfo);\n}",
            "packageJson в base64. Поиск сотрудника по email — Count()==1. IsRequired обнулять для полей при создании из почты.",
            ["email", "почт", "dcs", "capture", "письм", "обращени", "тикет", "ticket"]),

        // === SLA ===
        new("SLA-калькулятор (4 режима)",
            "ESM (rosa.ESM)",
            "Когда нужно считать время решения с учётом разных рабочих календарей",
            "User → AddWorkingHours(employee), Group → AddWorkingHours(department/BU), Overtime → AddHours (24/7), Default → AddWorkingHours()",
            null,
            "if (workingTime == User)\n  result = startDate.AddWorkingHours(employee, hours);\nelse if (workingTime == Group && hasDeptCalendar)\n  result = startDate.AddWorkingHours(department, hours);\nelse if (workingTime == Overtime)\n  result = startDate.AddHours(hours);\nelse\n  result = startDate.AddWorkingHours(hours);",
            "ReactionTime в минутах, SolvingHours в часах. Calendar.SqlMaxValue если SLA не задано.",
            ["sla", "дедлайн", "срок", "время", "реакци", "календар", "рабочее время"]),

        // === AI INTEGRATION ===
        new("AI-интеграция (AIAgentTool)",
            "ESM (rosa.ESM)",
            "Когда модуль должен зарегистрироваться как AI-инструмент в помощнике Directum RX",
            "При сохранении Service создаётся AIAgentTool с HandlerName. Права синхронизируются по категориям/услугам.",
            "base/Sungero.Commons/.../AIAgentTool — сущность инструмента",
            "var tool = Commons.PublicFunctions.Module\n  .AIAgentGetOrCreateExecFunctionTool(functionInfo, toolInfo);\nAddOrUpdateToolConstant(tool, \"ServiceId\", service.Id.ToString());",
            "Дубли при параллельных сохранениях. Права: отозвать у роли → заново поинструментно.",
            ["ai", "agent", "tool", "помощник", "ии", "бот", "искусственн"]),

        // === EXPRESSION ELEMENT ===
        new("ExpressionElement для конструктора маршрутов",
            "ESM (rosa.ESM)",
            "Когда бизнес-аналитик должен использовать сложную логику в визуальном конструкторе маршрутов без кода",
            "Атрибут [ExpressionElement(\"Name_Key\", \"Desc_Key\")] на серверной функции → доступна как блок в конструкторе.",
            null,
            "[ExpressionElement(\"ExpFunc_GetResponsibles_Name\",\n  \"ExpFunc_GetResponsibles_Description\")]\npublic List<IRecipient> GetResponsibles(IRequestDatabook request) { ... }",
            "Ключи ресурсов в Module.resx, не System.resx. Nullable возвраты нужно обрабатывать в конструкторе.",
            ["expression", "конструктор", "маршрут", "визуальн", "блок", "условие"]),

        // === REMOTE COMPONENT ===
        new("Remote Component (React в RX)",
            "Targets (6 RC), CRM (5 RC)",
            "Когда стандартных контролов RX недостаточно и нужен кастомный UI (таблица, график, kanban)",
            "RC регистрируется в Solution-модуле (SolutionMetadata). metadata.json + webpack bundle. Scope: Card или Cover.",
            null,
            "// Module.mtd Solution-модуля:\n\"RemoteComponents\": [{\n  \"Name\": \"TableControl\",\n  \"PublicName\": \"Directum_TableControl_1_1\",\n  \"Controls\": [{\"Loaders\": [{\"Name\": \"card-api-table-loader\"}]}]\n}]",
            "RC только в Solution-модуле. PublicName в .mtd === publicName в metadata.json. Scope определяет доступность (Card/Cover).",
            ["remote", "component", "react", "компонент", "виджет", "spa", "kanban", "график", "таблиц"]),

        // === CRUD TABLE ===
        new("RemoteTableControl — CRUD-таблица на карточке",
            "Targets/KPI",
            "Когда нужна редактируемая таблица с серверным CRUD вместо стандартной коллекции DDS",
            "TableMetadata (GET) → колонки + типы. GetRowsData (GET) → строки. BatchUpdate (POST) → изменения. ChangeTracking.",
            null,
            "[Public(WebApiRequestType = RequestType.Get)]\npublic IRemoteTableMetadata_Response TableMetadata(string entityType, long id, string propertyName)\n{\n  var col = RemoteTableMetadata_Column.Create();\n  col.Type = \"number\";\n  col.Title = \"Значение\";\n  // ...\n}",
            "Имена колонок = имена свойств в структуре. Methods.GetRowsData — строка с именем WebAPI-функции.",
            ["таблиц", "table", "crud", "строк", "колонк", "редактир", "grid"]),

        // === XLSX IMPORT ===
        new("Массовый импорт XLSX через Isolated Area",
            "Targets/KPI",
            "Когда нужно парсить/генерировать Excel через Aspose.Cells (запрещён в Server-слое)",
            "IsolatedArea + IsolatedFunction. Данные через ByteArray (byte[]). GenerateTemplate → парсинг → async import.",
            null,
            "// В Isolated:\nvar workbook = new Aspose.Cells.Workbook(new MemoryStream(byteArray.Content));\nvar worksheet = workbook.Worksheets[0];\nworksheet.Cells.DeleteBlankRows();",
            "IsolatedArea.cs может быть пустым. ByteArray для маршалинга byte[]. DeleteBlankRows() до парсинга.",
            ["excel", "xlsx", "импорт", "import", "aspose", "таблиц", "массов"]),

        // === ROUND-ROBIN ===
        new("Round-robin распределение",
            "CRM",
            "Когда новые заявки/лиды нужно автоматически назначать сотрудникам по кругу",
            "Job берёт неназначенные записи + активных сотрудников роли. managers[i % managers.Count]. Lock/Unlock каждой записи.",
            null,
            "for (int i = 0; i < unassigned.Count; i++)\n{\n  var manager = managers[i % managers.Count];\n  if (!Locks.TryLock(item)) continue;\n  try { item.Responsible = manager; item.Save(); }\n  finally { Locks.Unlock(item); }\n}",
            "Не учитывает загрузку. Пропущенный (заблокированный) элемент тратит индекс. Unlock в finally с IsLockedByMe.",
            ["round-robin", "распределен", "назначен", "распредел", "очеред", "автоматическ"]),

        // === BANT ===
        new("BANT Lead Scoring",
            "CRM",
            "Когда нужна квалификация лидов по методологии BANT (Budget, Authority, Need, Timeline)",
            "4 boolean свойства с ChangedShared handlers. RecalcScore() суммирует по 25 за каждый true.",
            null,
            "public void RecalcScore()\n{\n  var score = 0;\n  if (_obj.HasBudget == true) score += 25;\n  if (_obj.HasAuthority == true) score += 25;\n  if (_obj.HasNeed == true) score += 25;\n  if (_obj.HasTimeline == true) score += 25;\n  _obj.Score = score;\n}",
            "Boolean в DDS nullable (bool?), проверять == true. SharedFunctions, не ServerFunctions. При 5+ критериях — менять веса.",
            ["bant", "скоринг", "scoring", "лид", "lead", "квалификац", "budget", "authority"]),

        // === WIP LIMIT ===
        new("WIP-лимит на стадии (Kanban)",
            "CRM",
            "Когда нужно ограничить количество элементов на стадии (принцип Kanban WIP Limit)",
            "Stage.WipLimit (int?). При перемещении: Count() текущих на стадии >= WipLimit → ошибка.",
            null,
            "if (stage.WipLimit > 0)\n{\n  var count = Deals.GetAll(d => Equals(d.Stage, stage)).Count();\n  if (count >= stage.WipLimit)\n    return JsonError(\"WIP limit reached\");\n}",
            "WipLimit=0 или null = без ограничения. Race condition между Count и Save. Не проверяется при прямом создании.",
            ["wip", "лимит", "kanban", "ограничен", "стади", "колонк"]),

        // === FAN-OUT ===
        new("Fan-out async (координатор + исполнители)",
            "Targets",
            "Когда нужно обработать много записей асинхронно, каждую в своей транзакции с retry",
            "Координатор получает список, для каждой записи создаёт отдельный async исполнителя. Ошибка одного не откатит другие.",
            null,
            "// Координатор:\nforeach (var item in items)\n{\n  var executor = AsyncHandlers.ProcessItem.Create();\n  executor.ItemId = item.Id;\n  executor.ExecuteAsync();\n}",
            "Координатор НЕ модифицирует данные. ExponentialDelayStrategy для retry. Именование: Executor + имя координатора.",
            ["fan-out", "fanout", "параллел", "массов", "батч", "batch", "много записей"]),

        // === ASYNC HANDLERS (базовые) ===
        new("AsyncHandler с retry и блокировкой",
            "base/Sungero.Company (11 handlers), AgileBoard, CRM",
            "Когда операция должна выполняться асинхронно с retry при конкурентных блокировках",
            "Locks.GetLockInfo → IsLocked? args.Retry=true : TryLock → process → Unlock в finally. ExponentialDelay/RegularDelay.",
            "base/Sungero.Company/.../Module.mtd — 11 AsyncHandlers с разными стратегиями",
            "if (Locks.GetLockInfo(entity).IsLocked)\n  { args.Retry = true; return; }\nif (!Locks.TryLock(entity))\n  { args.Retry = true; return; }\ntry { /* process */ entity.Save(); }\nfinally { if (Locks.GetLockInfo(entity).IsLockedByMe) Locks.Unlock(entity); }",
            "Двойная проверка: GetLockInfo (быстрая read) + TryLock (захват). args.Retry=false при 'not found'. Unlock в finally с IsLockedByMe.",
            ["async", "handler", "фоновый", "асинхрон", "retry", "блокировк", "lock"]),

        // === JOBS ===
        new("Job (фоновый процесс по расписанию)",
            "base/Sungero.Contracts (2 Jobs), ESM (5 Jobs), CRM (2 Jobs)",
            "Когда операция должна выполняться по расписанию (ежедневно, еженедельно)",
            "Объявление в Module.mtd секция Jobs. Обработчик в ModuleJobs.cs. Daily/Monthly + StartAt. Per-entity try-catch.",
            "base/Sungero.Contracts/.../Module.mtd — SendNotificationForExpiringContracts, SendTaskForContractMilestones",
            "// Module.mtd:\n{ \"Name\": \"StaleDealJob\", \"Daily\": true, \"GenerateHandler\": true, \"MonthSchedule\": \"Monthly\" }\n\n// ModuleJobs.cs:\npublic virtual void StaleDealJob()\n{\n  foreach (var deal in activeDeals)\n  {\n    try { CheckAndNotify(deal); }\n    catch (Exception ex) { Logger.Error(ex.Message); }\n  }\n}",
            "Per-entity try-catch обязателен — ошибка одной записи не должна останавливать весь Job.",
            ["job", "фоновый процесс", "расписани", "ежедневн", "schedule", "cron", "таймер"]),

        // === COVER ===
        new("Обложка модуля (Cover) с действиями",
            "base/Sungero.Company, base/Sungero.Parties, CRM",
            "Когда нужна главная страница модуля с группами действий и навигацией",
            "Cover секция в Module.mtd: Groups (вкладки), Actions (CoverEntityListAction, CoverFunctionAction, CoverComputableFolderAction).",
            "base/Sungero.Company/.../Module.mtd — Cover с 6 группами, 20 действиями",
            "// CoverFunctionAction (вызов клиентской функции):\n{ \"$type\": \"CoverFunctionActionMetadata\",\n  \"FunctionName\": \"OpenCRMApp\" }\n\n// ModuleClientFunctions.cs:\npublic virtual void OpenCRMApp() {\n  Hyperlinks.Open(\"http://...\");\n}",
            "FunctionName ТОЧНО = имя метода в ClientFunctions. CoverComputableFolderAction для динамических папок.",
            ["обложк", "cover", "навигаци", "главная", "вкладк", "действи", "action"]),

        // === ВИДЖЕТЫ ===
        new("Виджет на рабочем столе",
            "base/Sungero.DirectumRX, Targets/KPI, CRM (4 виджета)",
            "Когда нужен информационный блок на рабочем столе: счётчик, диаграмма, график",
            "Widgets секция в Module.mtd. WidgetItems: Counter (число), Plot (график), Pie (круг), HorizontalBar. GetValueServer handler.",
            "base/Sungero.DirectumRX/.../Module.mtd — виджеты платформы",
            "// Фильтрация по текущему пользователю:\npublic virtual IQueryable<IDeal> MyDealsFiltering(IQueryable<IDeal> query)\n{\n  var employee = Employees.As(Users.Current);\n  if (employee == null) return query.Where(l => false);\n  return query.Where(d => Equals(d.Responsible, employee));\n}",
            "Users.Current может быть не Employee — проверять As()==null. Equals() вместо .Id== для null-safety.",
            ["виджет", "widget", "счётчик", "counter", "диаграмм", "график", "дашборд", "dashboard"]),

        // === INITIALIZER ===
        new("ModuleInitializer (начальные данные)",
            "base/Sungero.Company, base/Sungero.Contracts, CRM",
            "Когда при первой публикации нужно создать роли, права, справочные данные",
            "ModuleInitializer.cs с Initializing(). ModuleVersionInit для идемпотентности. Проверка Any() перед созданием.",
            "base/Sungero.Company/.../ModuleInitializer.cs",
            "public override void Initializing(ModuleInitializingEventArgs e)\n{\n  Commons.PublicInitializationFunctions.Module.ModuleVersionInit(\n    this.FirstInitializing,\n    Constants.Module.Init.ModuleName.Name,\n    Version.Parse(Constants.Module.Init.ModuleName.FirstInitVersion));\n}",
            "ModuleInitializer — public partial class БЕЗ базового класса. ModuleVersionInit для идемпотентности.",
            ["initializ", "инициализ", "начальны", "роли", "права", "справочн", "seed"]),
    ];
}
