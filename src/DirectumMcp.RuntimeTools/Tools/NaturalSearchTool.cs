using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public partial class NaturalSearchTool
{
    private readonly DirectumODataClient _client;

    public NaturalSearchTool(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "search")]
    [Description("Поиск по естественному запросу на русском языке. Примеры: 'договоры с Газпромом', 'просроченные задания', 'документы за март', 'сотрудники отдела продаж'. Автоматически определяет тип сущности и строит OData-запрос.")]
    public async Task<string> Search(
        [Description("Запрос на естественном языке (например: 'договоры на сумму больше 100000')")] string query,
        [Description("Максимум результатов")] int top = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Укажите поисковый запрос.";

        top = Math.Clamp(top, 1, 100);
        var q = query.ToLowerInvariant().Trim();

        try
        {
            var intent = DetectIntent(q);
            var result = await ExecuteIntent(intent, q, top);
            return result;
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}\n\n💡 Попробуйте уточнить запрос или используйте `odata_query` для ручного запроса. Используйте `discover` для просмотра доступных сущностей.";
        }
    }

    private SearchIntent DetectIntent(string q)
    {
        // Priority order matters — check specific before generic
        if (MatchesAny(q, "договор", "контракт", "соглашени"))
            return SearchIntent.Contracts;
        if (MatchesAny(q, "задани", "поручени", "исполнени", "просрочен"))
            return SearchIntent.Assignments;
        if (MatchesAny(q, "задач", "задачу", "задачи"))
            return SearchIntent.Tasks;
        if (MatchesAny(q, "сотрудник", "работник", "персонал", "специалист"))
            return SearchIntent.Employees;
        if (MatchesAny(q, "контрагент", "организаци", "компани", "поставщик", "подрядчик", "клиент"))
            return SearchIntent.Companies;
        if (MatchesAny(q, "подразделени", "отдел", "департамент", "управлени"))
            return SearchIntent.Departments;
        if (MatchesAny(q, "документ", "файл", "письм", "приказ", "распоряжени", "акт ", "накладн", "счёт", "счет"))
            return SearchIntent.Documents;

        // Default: search documents as the most common entity
        return SearchIntent.Documents;
    }

    private async Task<string> ExecuteIntent(SearchIntent intent, string q, int top)
    {
        return intent switch
        {
            SearchIntent.Contracts => await SearchContracts(q, top),
            SearchIntent.Assignments => await SearchAssignments(q, top),
            SearchIntent.Tasks => await SearchTasks(q, top),
            SearchIntent.Employees => await SearchEmployees(q, top),
            SearchIntent.Companies => await SearchCompanies(q, top),
            SearchIntent.Departments => await SearchDepartments(q, top),
            SearchIntent.Documents => await SearchDocuments(q, top),
            _ => await SearchDocuments(q, top)
        };
    }

    #region Contract Search

    private async Task<string> SearchContracts(string q, int top)
    {
        var filters = new List<string>();

        // Extract counterparty name
        var counterparty = ExtractAfterKeywords(q, "с ", "от ", "с компанией ", "контрагент ");
        if (counterparty is not null)
            filters.Add($"contains(Name, '{EscapeOData(counterparty)}')");

        // Extract amount filters
        var (amountFilter, amountDesc) = ExtractAmountFilter(q);
        if (amountFilter is not null)
            filters.Add(amountFilter);

        // Extract date filters
        var (dateFilters, dateDesc) = ExtractDateFilters(q, "Created");
        filters.AddRange(dateFilters);

        // Extract status
        var statusFilter = ExtractContractStatus(q);
        if (statusFilter is not null)
            filters.Add(statusFilter);

        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var result = await _client.GetAsync("IContracts",
            filter: filter,
            select: "Id,Name,TotalAmount,ValidFrom,ValidTill,LifeCycleState,InternalApprovalState",
            expand: "Counterparty",
            orderby: "Created desc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("договоры", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Договоры** ({items.Count} найдено)");
        if (filter is not null) sb.AppendLine($"_Фильтр: {filter}_");
        sb.AppendLine();
        sb.AppendLine("| ID | Название | Контрагент | Сумма | Действует | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = Truncate(GetString(item, "Name"), 40);
            var cp = GetNestedString(item, "Counterparty", "Name");
            var amount = FormatAmount(item);
            var period = FormatPeriod(item);
            var state = GetString(item, "LifeCycleState");
            sb.AppendLine($"| {id} | {name} | {cp} | {amount} | {period} | {state} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Assignment Search

    private async Task<string> SearchAssignments(string q, int top)
    {
        var filters = new List<string>();

        // Overdue
        if (MatchesAny(q, "просрочен", "просрочк"))
        {
            filters.Add("Status eq 'InProcess'");
            filters.Add($"Deadline lt {DateTime.Now:yyyy-MM-dd}T00:00:00Z");
        }
        else
        {
            // Default: active
            var status = "InProcess";
            if (MatchesAny(q, "выполнен", "завершен", "закрыт"))
                status = "Completed";
            else if (MatchesAny(q, "прерван", "отменен"))
                status = "Aborted";
            filters.Add($"Status eq '{status}'");
        }

        // Extract text search
        var textSearch = ExtractTextSearch(q, "задани", "поручени", "просрочен", "выполнен",
            "завершен", "активн", "мои", "все");
        if (textSearch is not null)
            filters.Add($"contains(Subject, '{EscapeOData(textSearch)}')");

        var (dateFilters, _) = ExtractDateFilters(q, "Created");
        filters.AddRange(dateFilters);

        var filter = string.Join(" and ", filters);
        var result = await _client.GetAsync("IAssignments",
            filter: filter,
            select: "Id,Subject,Created,Deadline,Status,Importance,Result",
            expand: "Performer,Author",
            orderby: "Deadline asc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("задания", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Задания** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | Тема | Исполнитель | Срок | Важность | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var subject = Truncate(GetString(item, "Subject"), 40);
            var performer = GetNestedString(item, "Performer", "Name");
            var deadline = FormatDate(GetString(item, "Deadline"));
            var importance = GetString(item, "Importance");
            var status = GetString(item, "Result");
            if (status == "-") status = GetString(item, "Status");
            sb.AppendLine($"| {id} | {subject} | {performer} | {deadline} | {importance} | {status} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Task Search

    private async Task<string> SearchTasks(string q, int top)
    {
        var filters = new List<string>();

        var status = "InProcess";
        if (MatchesAny(q, "выполнен", "завершен"))
            status = "Completed";
        else if (MatchesAny(q, "черновик", "новые"))
            status = "Draft";
        filters.Add($"Status eq '{status}'");

        var textSearch = ExtractTextSearch(q, "задач", "мои", "все", "активн", "выполнен", "завершен", "новые");
        if (textSearch is not null)
            filters.Add($"contains(Subject, '{EscapeOData(textSearch)}')");

        var filter = string.Join(" and ", filters);
        var result = await _client.GetAsync("ISimpleTasks",
            filter: filter,
            select: "Id,Subject,Created,Deadline,Status,Importance",
            expand: "Author",
            orderby: "Created desc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("задачи", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Задачи** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | Тема | Автор | Создана | Срок | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var subject = Truncate(GetString(item, "Subject"), 40);
            var author = GetNestedString(item, "Author", "Name");
            var created = FormatDate(GetString(item, "Created"));
            var deadline = FormatDate(GetString(item, "Deadline"));
            var status2 = GetString(item, "Status");
            sb.AppendLine($"| {id} | {subject} | {author} | {created} | {deadline} | {status2} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Employee Search

    private async Task<string> SearchEmployees(string q, int top)
    {
        var filters = new List<string>();

        var nameSearch = ExtractTextSearch(q, "сотрудник", "работник", "персонал", "специалист",
            "найди", "покажи", "все");
        if (nameSearch is not null)
            filters.Add($"contains(Name, '{EscapeOData(nameSearch)}')");

        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var result = await _client.GetAsync("IEmployees",
            filter: filter,
            select: "Id,Name,Phone,Email",
            expand: "Department,JobTitle",
            orderby: "Name asc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("сотрудники", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Сотрудники** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | ФИО | Подразделение | Должность | Телефон | Email |");
        sb.AppendLine("|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = GetString(item, "Name");
            var dept = GetNestedString(item, "Department", "Name");
            var job = GetNestedString(item, "JobTitle", "Name");
            var phone = GetString(item, "Phone");
            var email = GetString(item, "Email");
            sb.AppendLine($"| {id} | {name} | {dept} | {job} | {phone} | {email} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Company Search

    private async Task<string> SearchCompanies(string q, int top)
    {
        var filters = new List<string>();

        var nameSearch = ExtractTextSearch(q, "контрагент", "организаци", "компани", "поставщик",
            "подрядчик", "клиент", "найди", "покажи", "все");
        if (nameSearch is not null)
            filters.Add($"contains(Name, '{EscapeOData(nameSearch)}')");

        // Extract TIN
        var tinMatch = TinRegex().Match(q);
        if (tinMatch.Success)
            filters.Add($"TIN eq '{tinMatch.Value}'");

        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var result = await _client.GetAsync("ICompanies",
            filter: filter,
            select: "Id,Name,TIN,TRRC,LegalName,Phones,Email,Status",
            orderby: "Name asc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("контрагенты", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Контрагенты** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | Название | ИНН | КПП | Телефон | Email | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = Truncate(GetString(item, "Name"), 35);
            var tin = GetString(item, "TIN");
            var trrc = GetString(item, "TRRC");
            var phone = GetString(item, "Phones");
            var email = GetString(item, "Email");
            var status = GetString(item, "Status");
            sb.AppendLine($"| {id} | {name} | {tin} | {trrc} | {phone} | {email} | {status} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Department Search

    private async Task<string> SearchDepartments(string q, int top)
    {
        var filters = new List<string>();

        var nameSearch = ExtractTextSearch(q, "подразделени", "отдел", "департамент", "управлени",
            "найди", "покажи", "все");
        if (nameSearch is not null)
            filters.Add($"contains(Name, '{EscapeOData(nameSearch)}')");

        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var result = await _client.GetAsync("IDepartments",
            filter: filter,
            select: "Id,Name,Code,ShortName,Status",
            expand: "HeadOffice,Manager",
            orderby: "Name asc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("подразделения", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Подразделения** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | Название | Код | Руководитель | Головная орг. |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = GetString(item, "Name");
            var code = GetString(item, "Code");
            var manager = GetNestedString(item, "Manager", "Name");
            var head = GetNestedString(item, "HeadOffice", "Name");
            sb.AppendLine($"| {id} | {name} | {code} | {manager} | {head} |");
        }

        return sb.ToString();
    }

    #endregion

    #region Document Search

    private async Task<string> SearchDocuments(string q, int top)
    {
        var filters = new List<string>();

        var textSearch = ExtractTextSearch(q, "документ", "файл", "письм", "приказ",
            "распоряжени", "акт", "накладн", "счёт", "счет", "найди", "покажи", "все", "последни");
        if (textSearch is not null)
            filters.Add($"contains(Name, '{EscapeOData(textSearch)}')");

        var (dateFilters, _) = ExtractDateFilters(q, "Created");
        filters.AddRange(dateFilters);

        // Status
        if (MatchesAny(q, "черновик", "draft"))
            filters.Add("LifeCycleState eq 'Draft'");
        else if (MatchesAny(q, "действующ", "активн", "active"))
            filters.Add("LifeCycleState eq 'Active'");
        else if (MatchesAny(q, "устаревш", "obsolete"))
            filters.Add("LifeCycleState eq 'Obsolete'");

        var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
        var result = await _client.GetAsync("IOfficialDocuments",
            filter: filter,
            select: "Id,Name,Created,Modified,RegistrationNumber,LifeCycleState",
            expand: "Author,DocumentKind",
            orderby: "Modified desc",
            top: top);

        var items = GetItems(result);
        if (items.Count == 0)
            return FormatNoResults("документы", q, filter);

        var sb = new StringBuilder();
        sb.AppendLine($"**Документы** ({items.Count} найдено)");
        sb.AppendLine();
        sb.AppendLine("| ID | Название | Вид | Рег.номер | Создан | Автор | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = Truncate(GetString(item, "Name"), 35);
            var kind = GetNestedString(item, "DocumentKind", "Name");
            var regNum = GetString(item, "RegistrationNumber");
            var created = FormatDate(GetString(item, "Created"));
            var author = GetNestedString(item, "Author", "Name");
            var state = GetString(item, "LifeCycleState");
            sb.AppendLine($"| {id} | {name} | {kind} | {regNum} | {created} | {author} | {state} |");
        }

        return sb.ToString();
    }

    #endregion

    #region NL Parsing Helpers

    private static bool MatchesAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Extracts meaningful text after removing known intent keywords.
    /// Returns null if nothing meaningful remains.
    /// </summary>
    private static string? ExtractTextSearch(string q, params string[] stopWords)
    {
        var cleaned = q;
        foreach (var sw in stopWords)
            cleaned = cleaned.Replace(sw, " ", StringComparison.OrdinalIgnoreCase);

        // Remove common filler words
        foreach (var filler in new[] { "найди", "покажи", "найти", "показать", "все", "мои", "последние", "новые" })
            cleaned = cleaned.Replace(filler, " ", StringComparison.OrdinalIgnoreCase);

        // Remove date-related words (handled separately)
        cleaned = DateCleanupRegex().Replace(cleaned, " ");

        cleaned = cleaned.Trim();
        // Remove excessive spaces
        cleaned = MultiSpaceRegex().Replace(cleaned, " ").Trim();

        return cleaned.Length >= 2 ? cleaned : null;
    }

    /// <summary>
    /// Extract text after specific Russian prepositions for counterparty/name extraction.
    /// </summary>
    private static string? ExtractAfterKeywords(string q, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            var idx = q.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var after = q[(idx + prefix.Length)..].Trim();
            // Take words until we hit a known stop word
            var words = new List<string>();
            foreach (var word in after.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsStopWord(word)) break;
                words.Add(word);
                if (words.Count >= 4) break; // Max 4 words for a name
            }

            if (words.Count > 0)
                return string.Join(" ", words);
        }

        return null;
    }

    private static bool IsStopWord(string word)
        => new[] { "за", "на", "от", "до", "в", "с", "по", "для", "при", "более", "менее", "больше", "меньше",
                   "сумм", "статус", "период", "дат" }
            .Any(sw => word.StartsWith(sw, StringComparison.OrdinalIgnoreCase));

    private static (string? Filter, string? Description) ExtractAmountFilter(string q)
    {
        // "больше 100000", "более 100 000", "от 50000", "сумма > 100000"
        var match = AmountRegex().Match(q);
        if (!match.Success) return (null, null);

        var op = match.Groups[1].Value.Trim().ToLowerInvariant();
        var amountStr = match.Groups[2].Value.Replace(" ", "").Replace("\u00a0", "");

        if (!decimal.TryParse(amountStr, CultureInfo.InvariantCulture, out var amount))
            return (null, null);

        var odataOp = op switch
        {
            "больше" or "более" or "от" or ">" or "свыше" => "gt",
            "меньше" or "менее" or "до" or "<" => "lt",
            "=" or "равно" or "ровно" => "eq",
            _ => "gt"
        };

        return ($"TotalAmount {odataOp} {amount.ToString(CultureInfo.InvariantCulture)}",
                $"сумма {odataOp} {amount:N0}");
    }

    private static (List<string> Filters, string? Description) ExtractDateFilters(string q, string dateField)
    {
        var filters = new List<string>();
        var now = DateTime.Now;

        if (MatchesAny(q, "сегодня"))
        {
            filters.Add($"{dateField} ge {now:yyyy-MM-dd}T00:00:00Z");
            return (filters, "сегодня");
        }

        if (MatchesAny(q, "вчера"))
        {
            var yesterday = now.AddDays(-1);
            filters.Add($"{dateField} ge {yesterday:yyyy-MM-dd}T00:00:00Z");
            filters.Add($"{dateField} lt {now:yyyy-MM-dd}T00:00:00Z");
            return (filters, "вчера");
        }

        if (MatchesAny(q, "на этой неделе", "эта неделя", "текущая неделя", "за неделю"))
        {
            var weekStart = now.AddDays(-(int)now.DayOfWeek + 1);
            filters.Add($"{dateField} ge {weekStart:yyyy-MM-dd}T00:00:00Z");
            return (filters, "на этой неделе");
        }

        if (MatchesAny(q, "за последний месяц", "за месяц", "в этом месяце", "текущий месяц"))
        {
            var monthStart = new DateTime(now.Year, now.Month, 1);
            filters.Add($"{dateField} ge {monthStart:yyyy-MM-dd}T00:00:00Z");
            return (filters, "за текущий месяц");
        }

        if (MatchesAny(q, "за последний год", "за год", "в этом году"))
        {
            var yearStart = new DateTime(now.Year, 1, 1);
            filters.Add($"{dateField} ge {yearStart:yyyy-MM-dd}T00:00:00Z");
            return (filters, "за текущий год");
        }

        // "за март", "в марте", "в январе" etc.
        var monthMatch = MonthRegex().Match(q);
        if (monthMatch.Success)
        {
            var monthName = monthMatch.Groups[1].Value.ToLowerInvariant();
            var month = ParseRussianMonth(monthName);
            if (month > 0)
            {
                var year = month > now.Month ? now.Year - 1 : now.Year;
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1);
                filters.Add($"{dateField} ge {start:yyyy-MM-dd}T00:00:00Z");
                filters.Add($"{dateField} lt {end:yyyy-MM-dd}T00:00:00Z");
                return (filters, $"за {monthName}");
            }
        }

        return (filters, null);
    }

    private static string? ExtractContractStatus(string q)
    {
        if (MatchesAny(q, "на согласовани", "согласуем"))
            return "InternalApprovalState eq 'OnApproval'";
        if (MatchesAny(q, "подписан", "согласован"))
            return "InternalApprovalState eq 'Signed'";
        if (MatchesAny(q, "черновик"))
            return "LifeCycleState eq 'Draft'";
        if (MatchesAny(q, "действующ", "активн"))
            return "LifeCycleState eq 'Active'";
        if (MatchesAny(q, "просрочен", "истекш"))
            return $"ValidTill lt {DateTime.Now:yyyy-MM-dd}T00:00:00Z";
        return null;
    }

    private static int ParseRussianMonth(string month)
    {
        // Handle various forms: "январь", "января", "январе"
        if (month.StartsWith("январ")) return 1;
        if (month.StartsWith("феврал")) return 2;
        if (month.StartsWith("март") || month.StartsWith("марте")) return 3;
        if (month.StartsWith("апрел")) return 4;
        if (month.StartsWith("ма") && (month.Contains("й") || month.Contains("я") || month.Contains("е"))) return 5;
        if (month.StartsWith("июн")) return 6;
        if (month.StartsWith("июл")) return 7;
        if (month.StartsWith("август")) return 8;
        if (month.StartsWith("сентябр")) return 9;
        if (month.StartsWith("октябр")) return 10;
        if (month.StartsWith("ноябр")) return 11;
        if (month.StartsWith("декабр")) return 12;
        return 0;
    }

    private static string FormatAmount(JsonElement item)
    {
        if (item.TryGetProperty("TotalAmount", out var amt) && amt.ValueKind == JsonValueKind.Number)
            return amt.GetDecimal().ToString("N0", new CultureInfo("ru-RU"));
        return "-";
    }

    private static string FormatPeriod(JsonElement item)
    {
        var from = FormatDate(GetString(item, "ValidFrom"), "dd.MM.yy");
        var till = FormatDate(GetString(item, "ValidTill"), "dd.MM.yy");
        if (from == "-" && till == "-") return "-";
        return $"{from} — {till}";
    }

    private static string Truncate(string s, int maxLen)
        => s.Length > maxLen ? s[..(maxLen - 3)] + "..." : s;

    private static string FormatNoResults(string entityRu, string query, string? filter)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"По запросу «{query}» {entityRu} не найдены.");
        if (filter is not null)
            sb.AppendLine($"_Применённый фильтр: {filter}_");
        sb.AppendLine();
        sb.AppendLine("Попробуйте:");
        sb.AppendLine("- Упростить запрос");
        sb.AppendLine("- Использовать `discover` для просмотра доступных сущностей");
        sb.AppendLine("- Использовать `odata_query` для ручного запроса");
        return sb.ToString();
    }

    #endregion

    #region Regex

    [GeneratedRegex(@"(больше|более|меньше|менее|от|до|свыше|>|<|=|равно|ровно)\s*([\d\s\u00a0]+)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();

    [GeneratedRegex(@"\b(?:за|в|на)\s+(январ\w*|феврал\w*|март\w*|апрел\w*|ма[йяе]\w*|июн\w*|июл\w*|август\w*|сентябр\w*|октябр\w*|ноябр\w*|декабр\w*)", RegexOptions.IgnoreCase)]
    private static partial Regex MonthRegex();

    [GeneratedRegex(@"\b\d{10,12}\b")]
    private static partial Regex TinRegex();

    [GeneratedRegex(@"\b(за|в|на|последн\w*|текущ\w*|этой|этом|прошл\w*|сегодня|вчера|неделю?|месяц\w*|год\w*|январ\w*|феврал\w*|март\w*|апрел\w*|ма[йяе]\w*|июн\w*|июл\w*|август\w*|сентябр\w*|октябр\w*|ноябр\w*|декабр\w*)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateCleanupRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();

    #endregion

    private enum SearchIntent
    {
        Documents,
        Contracts,
        Assignments,
        Tasks,
        Employees,
        Companies,
        Departments
    }
}
