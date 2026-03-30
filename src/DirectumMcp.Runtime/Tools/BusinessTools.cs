using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text;
using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class BusinessTools
{
    private readonly DirectumODataClient _client;

    public BusinessTools(DirectumODataClient client)
    {
        _client = client;
    }

    #region DailyBriefingTool


    [McpServerTool(Name = "daily_briefing")]
    [Description("Что у меня на сегодня: задания, согласования, просроченные, дедлайны. Один вызов — полная картина дня.")]
    public async Task<string> DailyBriefing(
        [Description("Дата (yyyy-MM-dd, по умолчанию сегодня)")] string? date = null)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow : DateTime.Parse(date);
        var dateStr = targetDate.ToString("yyyy-MM-dd");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var sb = new StringBuilder();
        sb.AppendLine($"Дайджест на {targetDate:dd.MM.yyyy}");
        sb.AppendLine();

        try
        {
            // 1. Active assignments
            var activeJson = await _client.GetAsync("IAssignments",
                $"Status eq 'InProcess'", "Id,Subject,Deadline,Importance",
                "Deadline asc", top: 50);

            int activeCount = 0, overdueCount = 0, todayDeadlines = 0;
            var urgentItems = new List<string>();

            if (activeJson.TryGetProperty("value", out var activeValues))
            {
                foreach (var item in activeValues.EnumerateArray())
                {
                    activeCount++;
                    var subj = item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : "";
                    var id = item.TryGetProperty("Id", out var idEl) ? idEl.GetInt64() : 0;
                    var imp = item.TryGetProperty("Importance", out var impEl) ? impEl.GetString() ?? "" : "";

                    if (item.TryGetProperty("Deadline", out var dl) && dl.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(dl.GetString(), out var deadline))
                    {
                        if (deadline < DateTime.UtcNow)
                        {
                            overdueCount++;
                            urgentItems.Add($"[просрочено] #{id} {Truncate(subj, 40)} (срок был {deadline:dd.MM})");
                        }
                        else if (deadline.Date == targetDate.Date)
                        {
                            todayDeadlines++;
                            urgentItems.Add($"[сегодня {deadline:HH:mm}] #{id} {Truncate(subj, 40)}");
                        }
                        else if (imp == "High")
                        {
                            urgentItems.Add($"[важное] #{id} {Truncate(subj, 40)} (срок {deadline:dd.MM})");
                        }
                    }
                }
            }

            // Summary card
            sb.AppendLine($"В работе: {activeCount}");
            sb.AppendLine($"Просрочено: {overdueCount}");
            sb.AppendLine($"Дедлайны сегодня: {todayDeadlines}");
            sb.AppendLine();

            if (urgentItems.Count > 0)
            {
                sb.AppendLine("Приоритетные:");
                foreach (var item in urgentItems.Take(10))
                    sb.AppendLine($"  {item}");

                if (urgentItems.Count > 10)
                    sb.AppendLine($"  ...и ещё {urgentItems.Count - 10}");
            }
            else
            {
                sb.AppendLine("Нет срочных задач. Хороший день!");
            }

            sb.AppendLine();

            // Recommendation
            if (overdueCount > 0)
                sb.AppendLine($"Рекомендация: {overdueCount} просроченных — начните с них. Используйте `complete` для выполнения.");
            if (todayDeadlines > 0)
                sb.AppendLine($"Рекомендация: {todayDeadlines} дедлайнов сегодня — не откладывайте.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "..." : s;

    #endregion
    #region ContractExpiryTool


    [McpServerTool(Name = "contract_expiry")]
    [Description("Договоры, истекающие в указанный период: контрагент, сумма, дата окончания, дней осталось.")]
    public async Task<string> ContractExpiry(
        [Description("Период: 'апрель', 'май', или конкретные даты")] string? period = null,
        [Description("Дней вперёд (по умолчанию 30)")] int daysAhead = 30,
        [Description("Минимальная сумма для фильтрации")] double minAmount = 0,
        [Description("Макс. записей")] int top = 50)
    {
        var now = DateTime.UtcNow;
        DateTime dateFrom, dateTo;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var (from, to) = ParsePeriod(period, now);
            dateFrom = from;
            dateTo = to;
        }
        else
        {
            dateFrom = now;
            dateTo = now.AddDays(daysAhead);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Договоры, истекающие {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}");
        sb.AppendLine();

        try
        {
            var filter = $"ValidTill ge {dateFrom:yyyy-MM-dd}T00:00:00Z and ValidTill le {dateTo:yyyy-MM-dd}T23:59:59Z and LifeCycleState eq 'Active'";
            if (minAmount > 0)
                filter += $" and TotalAmount ge {minAmount}";

            var json = await _client.GetAsync("IContracts",
                filter, "Id,Name,TotalAmount,ValidTill",
                "ValidTill asc",
                expand: "Counterparty($select=Id,Name),Currency($select=AlphaCode)",
                top: top);

            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            {
                sb.AppendLine("Договоров, истекающих в указанный период, не найдено.");
                return sb.ToString();
            }

            int count = 0;
            double totalAmount = 0;

            foreach (var item in values.EnumerateArray())
            {
                count++;
                var id = item.TryGetProperty("Id", out var idEl) ? idEl.GetInt64() : 0;
                var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var amount = item.TryGetProperty("TotalAmount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0;
                totalAmount += amount;

                var validTill = "";
                int daysLeft = 0;
                if (item.TryGetProperty("ValidTill", out var vt) && vt.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(vt.GetString(), out var vtDate))
                {
                    validTill = vtDate.ToString("dd.MM.yyyy");
                    daysLeft = (int)(vtDate - now).TotalDays;
                }

                var counterparty = "—";
                if (item.TryGetProperty("Counterparty", out var cp) && cp.ValueKind == JsonValueKind.Object)
                    counterparty = cp.TryGetProperty("Name", out var cpn) ? cpn.GetString() ?? "—" : "—";

                var currency = "";
                if (item.TryGetProperty("Currency", out var cur) && cur.ValueKind == JsonValueKind.Object)
                    currency = cur.TryGetProperty("AlphaCode", out var ca) ? ca.GetString() ?? "" : "";

                var urgency = daysLeft <= 7 ? " [СРОЧНО]" : daysLeft <= 14 ? " [скоро]" : "";

                sb.AppendLine($"{count}. #{id} {Truncate(name, 40)}{urgency}");
                sb.AppendLine($"   Контрагент: {counterparty}");
                sb.AppendLine($"   Сумма: {(amount > 0 ? $"{amount:N0} {currency}" : "не указана")}");
                sb.AppendLine($"   Истекает: {validTill} ({daysLeft} дн.)");
                sb.AppendLine();
            }

            sb.AppendLine($"Итого: {count} договоров на сумму {totalAmount:N0}");

            if (count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Рекомендации:");
                var urgent = values.EnumerateArray().Count(v =>
                    v.TryGetProperty("ValidTill", out var vt2) && DateTime.TryParse(vt2.GetString(), out var d) && (d - now).TotalDays <= 7);
                if (urgent > 0)
                    sb.AppendLine($"- {urgent} договоров истекают в ближайшие 7 дней — нужна пролонгация");
                sb.AppendLine("- Используйте `contract_review` для анализа рисков конкретного договора");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
        }

        return sb.ToString();
    }

    private static (DateTime From, DateTime To) ParsePeriod(string period, DateTime now)
    {
        var lower = period.ToLowerInvariant().Trim();
        var months = new Dictionary<string, int>
        {
            ["январ"] = 1, ["феврал"] = 2, ["март"] = 3, ["апрел"] = 4,
            ["ма"] = 5, ["июн"] = 6, ["июл"] = 7, ["август"] = 8,
            ["сентябр"] = 9, ["октябр"] = 10, ["ноябр"] = 11, ["декабр"] = 12
        };

        foreach (var (name, monthNum) in months)
        {
            if (lower.Contains(name))
            {
                var year = monthNum >= now.Month ? now.Year : now.Year + 1;
                var from = new DateTime(year, monthNum, 1);
                var to = from.AddMonths(1).AddDays(-1);
                return (from, to);
            }
        }

        // Try parse as date range "2026-04-01..2026-04-30"
        if (lower.Contains(".."))
        {
            var parts = lower.Split("..");
            if (parts.Length == 2 && DateTime.TryParse(parts[0], out var f) && DateTime.TryParse(parts[1], out var t))
                return (f, t);
        }

        // Default: next 30 days
        return (now, now.AddDays(30));
    }


    #endregion
    #region ContractReviewTool


    [McpServerTool(Name = "contract_review")]
    [Description("Анализ рисков договора: проверка обязательных полей, сроков, сумм, контрагента. Рекомендации перед согласованием.")]
    public async Task<string> ContractReview(
        [Description("ID договора")] long contractId,
        [Description("OData тип (по умолчанию IContractualDocuments)")] string entityType = "IContractualDocuments")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ рисков договора");
        sb.AppendLine();

        try
        {
            var json = await _client.GetAsync(entityType,
                $"$filter=Id eq {contractId}" +
                $"&$select=Id,Name,Subject,TotalAmount,ValidFrom,ValidTill,LifeCycleState,Note,IsAutomaticRenewal" +
                $"&$expand=Counterparty($select=Id,Name,TIN,Status),OurSignatory($select=Id,Name),DocumentKind($select=Id,Name),Currency($select=Id,Name,AlphaCode)");

            if (json.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Договор {contractId} не найден.";

            var values = json.GetProperty("value");
            if (values.GetArrayLength() == 0)
                return $"**ОШИБКА**: Договор {contractId} не найден.";

            var doc = values[0];
            var name = doc.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var amount = doc.TryGetProperty("TotalAmount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0;
            var validFrom = doc.TryGetProperty("ValidFrom", out var vf) && vf.ValueKind == JsonValueKind.String ? vf.GetString() : null;
            var validTill = doc.TryGetProperty("ValidTill", out var vt) && vt.ValueKind == JsonValueKind.String ? vt.GetString() : null;
            var state = doc.TryGetProperty("LifeCycleState", out var ls) ? ls.GetString() ?? "?" : "?";
            var autoRenewal = doc.TryGetProperty("IsAutomaticRenewal", out var ar) && ar.ValueKind == JsonValueKind.True;

            var counterpartyName = "не указан";
            var counterpartyTin = "";
            var counterpartyStatus = "";
            if (doc.TryGetProperty("Counterparty", out var cp) && cp.ValueKind == JsonValueKind.Object)
            {
                counterpartyName = cp.TryGetProperty("Name", out var cpn) ? cpn.GetString() ?? "?" : "?";
                counterpartyTin = cp.TryGetProperty("TIN", out var cpt) ? cpt.GetString() ?? "" : "";
                counterpartyStatus = cp.TryGetProperty("Status", out var cps) ? cps.GetString() ?? "" : "";
            }

            var signatory = "не указан";
            if (doc.TryGetProperty("OurSignatory", out var os) && os.ValueKind == JsonValueKind.Object)
                signatory = os.TryGetProperty("Name", out var osn) ? osn.GetString() ?? "?" : "?";

            sb.AppendLine($"**Договор:** #{contractId} — {name}");
            sb.AppendLine($"**Контрагент:** {counterpartyName} (ИНН: {(string.IsNullOrEmpty(counterpartyTin) ? "не указан" : counterpartyTin)})");
            sb.AppendLine($"**Сумма:** {(amount > 0 ? amount.ToString("N2") : "не указана")}");
            sb.AppendLine($"**Срок:** {validFrom ?? "?"} — {validTill ?? "бессрочный"}");
            sb.AppendLine($"**Подписант:** {signatory}");
            sb.AppendLine($"**Статус:** {state}");
            sb.AppendLine($"**Автопролонгация:** {(autoRenewal ? "да" : "нет")}");
            sb.AppendLine();

            // Risk analysis
            var risks = new List<(string Level, string Risk, string Recommendation)>();

            // 1. Amount
            if (amount <= 0)
                risks.Add(("HIGH", "Сумма не указана", "Заполните TotalAmount перед согласованием"));
            else if (amount > 10_000_000)
                risks.Add(("MEDIUM", $"Крупная сумма: {amount:N0}", "Требуется дополнительное согласование с финансовым директором"));

            // 2. Counterparty
            if (counterpartyName == "не указан")
                risks.Add(("HIGH", "Контрагент не указан", "Укажите контрагента"));
            if (counterpartyStatus == "Closed")
                risks.Add(("HIGH", $"Контрагент '{counterpartyName}' закрыт", "Нельзя заключать договор с закрытым контрагентом"));
            if (string.IsNullOrEmpty(counterpartyTin))
                risks.Add(("MEDIUM", "У контрагента не указан ИНН", "Проверьте реквизиты контрагента"));

            // 3. Dates
            if (validFrom == null)
                risks.Add(("MEDIUM", "Дата начала не указана", "Укажите ValidFrom"));
            if (validTill != null && DateTime.TryParse(validTill, out var tillDate) && tillDate < DateTime.UtcNow)
                risks.Add(("HIGH", $"Срок действия истёк: {validTill}", "Договор просрочен, нужна пролонгация или новый договор"));
            if (validTill != null && DateTime.TryParse(validTill, out var tillDate2) &&
                tillDate2 < DateTime.UtcNow.AddDays(30))
                risks.Add(("MEDIUM", $"Срок истекает менее чем через 30 дней", "Подготовьте пролонгацию или новый договор"));

            // 4. Signatory
            if (signatory == "не указан")
                risks.Add(("MEDIUM", "Подписант не указан", "Укажите OurSignatory"));

            // 5. Auto-renewal
            if (autoRenewal && validTill != null)
                risks.Add(("INFO", "Включена автопролонгация", "Убедитесь что условия автопролонгации устраивают"));

            // Report
            sb.AppendLine("## Риски");
            sb.AppendLine();

            if (risks.Count == 0)
            {
                sb.AppendLine("Критических рисков не обнаружено. Договор готов к согласованию.");
            }
            else
            {
                sb.AppendLine("| Уровень | Риск | Рекомендация |");
                sb.AppendLine("|---------|------|-------------|");
                foreach (var (level, risk, rec) in risks.OrderByDescending(r => r.Level))
                    sb.AppendLine($"| **{level}** | {risk} | {rec} |");

                var highCount = risks.Count(r => r.Level == "HIGH");
                sb.AppendLine();
                if (highCount > 0)
                    sb.AppendLine($"**ВЕРДИКТ:** {highCount} критических рисков — НЕ РЕКОМЕНДУЕТСЯ отправлять на согласование.");
                else
                    sb.AppendLine("**ВЕРДИКТ:** Критических рисков нет, можно отправлять на согласование.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
    #region WorkflowEscalationTool


    [McpServerTool(Name = "workflow_escalation")]
    [Description("Эскалация просроченных заданий: найти просроченные, определить руководителя, предложить переадресацию.")]
    public async Task<string> EscalateOverdue(
        [Description("Минимальное количество дней просрочки для эскалации")] int overdueDays = 2,
        [Description("ID подразделения (опционально)")] long departmentId = 0,
        [Description("Режим: report (только отчёт) или execute (переадресовать)")] string mode = "report")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Эскалация просроченных заданий");
        sb.AppendLine();

        try
        {
            var now = DateTime.UtcNow;
            var threshold = now.AddDays(-overdueDays).ToString("yyyy-MM-ddTHH:mm:ssZ");

            var filter = $"Status eq 'InProcess' and Deadline lt {threshold}";
            if (departmentId > 0)
                filter += $" and Performer/Department/Id eq {departmentId}";

            var json = await _client.GetAsync("IAssignments",
                $"$filter={filter}&$expand=Performer($select=Id,Name;$expand=Department($select=Id,Name,Manager($select=Id,Name))),Task($select=Id,Subject),Author($select=Id,Name)&$top=100&$orderby=Deadline asc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine("Не удалось получить данные. Проверьте OData.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            var escalations = new List<EscalationItem>();

            foreach (var item in values.EnumerateArray())
            {
                var assignId = item.TryGetProperty("Id", out var id) ? id.GetInt64() : 0;
                var subject = "?";
                if (item.TryGetProperty("Task", out var task) && task.ValueKind == JsonValueKind.Object)
                    subject = task.TryGetProperty("Subject", out var s) ? s.GetString() ?? "?" : "?";

                var performerName = "?";
                var managerId = 0L;
                var managerName = "?";
                var deptName = "?";

                if (item.TryGetProperty("Performer", out var perf) && perf.ValueKind == JsonValueKind.Object)
                {
                    performerName = perf.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                    if (perf.TryGetProperty("Department", out var dept) && dept.ValueKind == JsonValueKind.Object)
                    {
                        deptName = dept.TryGetProperty("Name", out var dn) ? dn.GetString() ?? "?" : "?";
                        if (dept.TryGetProperty("Manager", out var mgr) && mgr.ValueKind == JsonValueKind.Object)
                        {
                            managerId = mgr.TryGetProperty("Id", out var mid) ? mid.GetInt64() : 0;
                            managerName = mgr.TryGetProperty("Name", out var mn) ? mn.GetString() ?? "?" : "?";
                        }
                    }
                }

                var deadline = item.TryGetProperty("Deadline", out var dl) && dl.ValueKind == JsonValueKind.String
                    ? dl.GetString() ?? "" : "";

                var daysOverdue = 0;
                if (DateTime.TryParse(deadline, out var deadlineDate))
                    daysOverdue = (int)(now - deadlineDate).TotalDays;

                escalations.Add(new EscalationItem(
                    assignId, subject, performerName, deptName,
                    managerId, managerName, daysOverdue, deadline));
            }

            sb.AppendLine($"**Просрочка >{overdueDays} дней:** {escalations.Count} заданий");
            sb.AppendLine($"**Режим:** {mode}");
            sb.AppendLine();

            if (escalations.Count == 0)
            {
                sb.AppendLine("Просроченных заданий не найдено. Всё в порядке.");
                return sb.ToString();
            }

            sb.AppendLine("| # | Задание | Тема | Исполнитель | Подразделение | Просрочка | Руководитель |");
            sb.AppendLine("|---|---------|------|-------------|---------------|-----------|-------------|");

            foreach (var (i, e) in escalations.Select((e, i) => (i + 1, e)))
            {
                sb.AppendLine($"| {i} | #{e.AssignmentId} | {Truncate(e.Subject, 30)} | {e.PerformerName} | {e.DeptName} | {e.DaysOverdue}д | {e.ManagerName} |");
            }
            sb.AppendLine();

            if (mode == "execute")
            {
                sb.AppendLine("## Результат эскалации");
                var forwarded = 0;
                foreach (var e in escalations.Where(e => e.ManagerId > 0))
                {
                    try
                    {
                        await _client.PostActionAsync("IAssignments", e.AssignmentId, "Forward",
                            JsonSerializer.Serialize(new { ForwardTo = new { Id = e.ManagerId } }));
                        sb.AppendLine($"- #{e.AssignmentId} → переадресовано {e.ManagerName}");
                        forwarded++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"- #{e.AssignmentId} → **ОШИБКА**: {ex.Message}");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"**Переадресовано:** {forwarded}/{escalations.Count}");
            }
            else
            {
                sb.AppendLine("## Рекомендации");
                sb.AppendLine("Для автоматической переадресации запустите с `mode=execute`.");
                sb.AppendLine("Задания будут переадресованы руководителю подразделения исполнителя.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }


    private record EscalationItem(long AssignmentId, string Subject, string PerformerName,
        string DeptName, long ManagerId, string ManagerName, int DaysOverdue, string Deadline);

    #endregion
    #region AbsencesTool


    [McpServerTool(Name = "absences")]
    [Description("Кто отсутствует: отпуска, больничные, командировки. По дате, подразделению, типу.")]
    public async Task<string> Absences(
        [Description("Дата проверки (yyyy-MM-dd, по умолчанию сегодня)")] string? date = null,
        [Description("Фильтр по подразделению (часть названия)")] string? department = null,
        [Description("Тип: All, Vacation, SickLeave, BusinessTrip")] string type = "All",
        [Description("Макс. записей")] int top = 50)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow : DateTime.Parse(date);
        var dateFilter = targetDate.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine($"Отсутствующие на {targetDate:dd.MM.yyyy}");
        sb.AppendLine();

        try
        {
            // Try Absences entity (Sungero.Company.Absence)
            var filter = $"AbsenceSince le {dateFilter}T23:59:59Z and AbsenceTill ge {dateFilter}T00:00:00Z";

            var json = await _client.GetAsync("IAbsences",
                filter, "Id,AbsenceSince,AbsenceTill,AbsenceType",
                expand: "Employee($select=Id,Name;$expand=Department($select=Name))",
                top: top);

            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            {
                sb.AppendLine("Отсутствующих не найдено.");
                sb.AppendLine();
                sb.AppendLine("Возможные причины:");
                sb.AppendLine("- На эту дату нет записей об отсутствии");
                sb.AppendLine("- OData entity set `IAbsences` может иметь другое имя в вашей версии RX");
                return sb.ToString();
            }

            var absences = new List<(string Name, string Dept, string Type, string From, string To)>();

            foreach (var item in values.EnumerateArray())
            {
                var empName = "?";
                var deptName = "?";

                if (item.TryGetProperty("Employee", out var emp) && emp.ValueKind == JsonValueKind.Object)
                {
                    empName = emp.TryGetProperty("Name", out var en) ? en.GetString() ?? "?" : "?";
                    if (emp.TryGetProperty("Department", out var dept) && dept.ValueKind == JsonValueKind.Object)
                        deptName = dept.TryGetProperty("Name", out var dn) ? dn.GetString() ?? "?" : "?";
                }

                // Department filter
                if (!string.IsNullOrWhiteSpace(department) &&
                    !deptName.Contains(department, StringComparison.OrdinalIgnoreCase))
                    continue;

                var absType = item.TryGetProperty("AbsenceType", out var at) ? at.GetString() ?? "?" : "?";
                var from = item.TryGetProperty("AbsenceSince", out var af) ? af.GetString() ?? "" : "";
                var to = item.TryGetProperty("AbsenceTill", out var att) ? att.GetString() ?? "" : "";

                // Type filter
                if (type != "All" && !absType.Contains(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                var typeRu = absType switch
                {
                    var t when t.Contains("Vacation", StringComparison.OrdinalIgnoreCase) => "Отпуск",
                    var t when t.Contains("Sick", StringComparison.OrdinalIgnoreCase) => "Больничный",
                    var t when t.Contains("Business", StringComparison.OrdinalIgnoreCase) => "Командировка",
                    var t when t.Contains("Trip", StringComparison.OrdinalIgnoreCase) => "Командировка",
                    _ => absType
                };

                var fromDate = DateTime.TryParse(from, out var fd) ? fd.ToString("dd.MM") : "?";
                var toDate = DateTime.TryParse(to, out var td) ? td.ToString("dd.MM") : "?";

                absences.Add((empName, deptName, typeRu, fromDate, toDate));
            }

            if (absences.Count == 0)
            {
                sb.AppendLine("По заданным фильтрам отсутствующих не найдено.");
                return sb.ToString();
            }

            // Group by department
            var grouped = absences.GroupBy(a => a.Dept).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"{group.Key}:");
                foreach (var (name, _, absType, from, to) in group)
                    sb.AppendLine($"  {name} — {absType} ({from}–{to})");
                sb.AppendLine();
            }

            sb.AppendLine($"Всего: {absences.Count} чел.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Попробуйте `odata_query` с ручным запросом к IAbsences или используйте `search` с запросом \"отсутствующие\".");
        }

        return sb.ToString();
    }

    #endregion
    #region AutoClassifyTool


    [McpServerTool(Name = "auto_classify")]
    [Description("Классификация входящих документов: анализ названия/содержания, предложение вида документа и контрагента.")]
    public async Task<string> AutoClassify(
        [Description("ID документа для классификации")] long documentId,
        [Description("OData тип документа (по умолчанию IOfficialDocuments)")] string entityType = "IOfficialDocuments")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Классификация документа");
        sb.AppendLine();

        try
        {
            var docJson = await _client.GetAsync(entityType,
                $"$filter=Id eq {documentId}&$select=Id,Name,Subject,Note&$expand=DocumentKind($select=Id,Name),Author($select=Id,Name),Counterparty($select=Id,Name)");

            if (docJson.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Документ {documentId} не найден.";

            var values = docJson.GetProperty("value");
            if (values.GetArrayLength() == 0)
                return $"**ОШИБКА**: Документ {documentId} не найден.";

            var item = values[0];
            var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var subject = item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : "";
            var note = item.TryGetProperty("Note", out var nt) ? nt.GetString() ?? "" : "";
            var currentKind = "не определён";
            if (item.TryGetProperty("DocumentKind", out var dk) && dk.ValueKind == JsonValueKind.Object)
                currentKind = dk.TryGetProperty("Name", out var dkn) ? dkn.GetString() ?? "?" : "?";

            var text = $"{name} {subject} {note}".ToLowerInvariant();

            sb.AppendLine($"**Документ:** #{documentId}");
            sb.AppendLine($"**Название:** {name}");
            sb.AppendLine($"**Тема:** {subject}");
            sb.AppendLine($"**Текущий вид:** {currentKind}");
            sb.AppendLine();

            // Rule-based classification
            sb.AppendLine("## Предложения");
            sb.AppendLine();

            var suggestions = new List<(string Kind, int Score, string Reason)>();

            if (text.Contains("договор") || text.Contains("контракт") || text.Contains("соглашени"))
                suggestions.Add(("Договор", 90, "Содержит «договор»/«контракт»/«соглашение»"));

            if (text.Contains("счёт") || text.Contains("счет") || text.Contains("invoice"))
                suggestions.Add(("Счёт на оплату", 85, "Содержит «счёт»/«invoice»"));

            if (text.Contains("акт") && (text.Contains("выполнен") || text.Contains("приём") || text.Contains("сверк")))
                suggestions.Add(("Акт", 80, "Содержит «акт выполненных работ»/«акт сверки»"));

            if (text.Contains("письм") || text.Contains("обращени") || text.Contains("запрос"))
                suggestions.Add(("Входящее письмо", 75, "Содержит «письмо»/«обращение»/«запрос»"));

            if (text.Contains("приказ") || text.Contains("распоряжени"))
                suggestions.Add(("Приказ", 85, "Содержит «приказ»/«распоряжение»"));

            if (text.Contains("служебн") || text.Contains("записк") || text.Contains("memo"))
                suggestions.Add(("Служебная записка", 80, "Содержит «служебная записка»"));

            if (text.Contains("доверенност") || text.Contains("мчд"))
                suggestions.Add(("Доверенность", 85, "Содержит «доверенность»/«МЧД»"));

            if (text.Contains("накладн") || text.Contains("упд") || text.Contains("торг-12"))
                suggestions.Add(("Товарная накладная", 80, "Содержит «накладная»/«УПД»/«ТОРГ-12»"));

            if (text.Contains("счёт-фактур") || text.Contains("счет-фактур"))
                suggestions.Add(("Счёт-фактура", 90, "Содержит «счёт-фактура»"));

            if (suggestions.Count == 0)
                suggestions.Add(("Простой документ", 50, "Не удалось определить тип по содержимому"));

            sb.AppendLine("| Вид документа | Уверенность | Причина |");
            sb.AppendLine("|--------------|-------------|---------|");
            foreach (var (kind, score, reason) in suggestions.OrderByDescending(s => s.Score))
                sb.AppendLine($"| **{kind}** | {score}% | {reason} |");

            sb.AppendLine();
            sb.AppendLine("## Действия");
            sb.AppendLine("Для применения вида документа используйте OData PATCH:");
            sb.AppendLine("```");
            sb.AppendLine($"PATCH /{entityType}({documentId})");
            sb.AppendLine("{\"DocumentKind\": {\"Id\": <DocumentKindId>}}");
            sb.AppendLine("```");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
    #region SummarizeTool

    private static readonly Regex SafeQueryPattern = new(@"^[\p{L}\p{N}\s\-_.]+$");



    [McpServerTool(Name = "summarize")]
    [Description("Краткое содержание документа Directum RX — метаданные, статус, история согласования")]
    public async Task<string> Summarize(
        [Description("ID документа")] long? documentId = null,
        [Description("Поисковый запрос (если ID неизвестен)")] string? query = null)
    {
        try
        {
            if (documentId is null && string.IsNullOrWhiteSpace(query))
                return "Ошибка: укажите documentId или query для поиска документа.";

            if (query != null && !SafeQueryPattern.IsMatch(query))
                return "Ошибка: поисковый запрос содержит недопустимые символы. Используйте буквы, цифры и пробелы.";

            long id;
            JsonElement doc;

            if (documentId.HasValue)
            {
                id = documentId.Value;
                doc = await _client.GetByIdAsync("IOfficialDocuments", id,
                    select: "Id,Name,Created,Modified,LifeCycleState,Subject,Note");
            }
            else
            {
                var searchResult = await _client.GetAsync("IOfficialDocuments",
                    filter: $"contains(Name,'{EscapeOData(query!)}')",
                    select: "Id,Name",
                    top: 1,
                    orderby: "Modified desc");

                var found = GetItems(searchResult);
                if (found.Count == 0)
                    return $"Документ не найден по запросу '{query}'.";

                id = GetLong(found[0], "Id");
                doc = await _client.GetByIdAsync("IOfficialDocuments", id,
                    select: "Id,Name,Created,Modified,LifeCycleState,Subject,Note");
            }

            var name = GetString(doc, "Name");
            var created = FormatDate(GetString(doc, "Created"));
            var modified = FormatDate(GetString(doc, "Modified"));
            var state = GetString(doc, "LifeCycleState");
            var subject = GetString(doc, "Subject");
            var note = GetString(doc, "Note");

            // Try to get ActiveText
            string activeText = "";
            try
            {
                var textData = await _client.GetAsync("IOfficialDocuments",
                    filter: $"Id eq {id}", select: "ActiveText", top: 1);
                var items = GetItems(textData);
                if (items.Count > 0)
                    activeText = GetString(items[0], "ActiveText");
            }
            catch { /* ActiveText может быть недоступен */ }

            // Try to get tracking history
            var trackingEntries = new List<(string date, string action, string author)>();
            try
            {
                var tracking = await _client.GetRawAsync(
                    $"IOfficialDocuments({id})/Tracking?$select=Action,Author,Date&$orderby=Date desc&$top=10");
                var trackingItems = GetItems(tracking);
                foreach (var entry in trackingItems)
                {
                    var date = FormatDate(GetString(entry, "Date"), "dd.MM.yyyy HH:mm");
                    var action = GetString(entry, "Action");
                    var author = GetNestedString(entry, "Author", "Name");
                    trackingEntries.Add((date, action, author));
                }
            }
            catch { /* Tracking может быть недоступен */ }

            // Format markdown
            var sb = new StringBuilder();
            sb.AppendLine($"## Документ: {name}");
            sb.AppendLine();
            sb.AppendLine("| Поле | Значение |");
            sb.AppendLine("|------|----------|");
            sb.AppendLine($"| ID | {id} |");
            sb.AppendLine($"| Создан | {created} |");
            sb.AppendLine($"| Изменён | {modified} |");
            sb.AppendLine($"| Статус | {state} |");
            sb.AppendLine($"| Тема | {(subject == "-" ? "\u2014" : subject)} |");
            sb.AppendLine($"| Примечание | {(note == "-" ? "\u2014" : note)} |");
            sb.AppendLine();

            sb.AppendLine("### Содержание");
            if (!string.IsNullOrWhiteSpace(activeText) && activeText != "-")
            {
                var text = activeText.Length > 2000 ? activeText[..2000] + "..." : activeText;
                sb.AppendLine(text);
            }
            else
            {
                sb.AppendLine("Текст документа недоступен через API.");
            }
            sb.AppendLine();

            if (trackingEntries.Count > 0)
            {
                sb.AppendLine($"### История ({trackingEntries.Count} записей)");
                sb.AppendLine("| Дата | Действие | Автор |");
                sb.AppendLine("|------|----------|-------|");
                foreach (var (date, action, author) in trackingEntries)
                {
                    sb.AppendLine($"| {date} | {action} | {author} |");
                }
            }
            else
            {
                sb.AppendLine("### История");
                sb.AppendLine("История недоступна через API.");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить информацию о документе. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    #endregion
    #region FindContractsTool



    [McpServerTool(Name = "find_contracts")]
    [Description("Поиск договоров в Directum RX по контрагенту, сумме, сроку действия, статусу согласования")]
    public async Task<string> Search(
        [Description("Текст для поиска в названии договора")] string? query = null,
        [Description("Имя контрагента (поиск по contains)")] string? counterparty = null,
        [Description("Минимальная сумма договора")] decimal? amountFrom = null,
        [Description("Максимальная сумма договора")] decimal? amountTo = null,
        [Description("Действует от (yyyy-MM-dd)")] string? validFrom = null,
        [Description("Действует до (yyyy-MM-dd)")] string? validTill = null,
        [Description("Статус жизненного цикла: Draft, Active, Obsolete")] string? status = null,
        [Description("Статус согласования: OnApproval, PendingSign, Signed")] string? approvalState = null,
        [Description("Только просроченные (с истекшим сроком действия)")] bool expired = false,
        [Description("Только рамочные договоры")] bool frameworkOnly = false,
        [Description("Максимальное количество результатов")] int top = 20)
    {
        top = Math.Clamp(top, 1, 100);

        try
        {
            var filters = BuildFilters(query, counterparty, amountFrom, amountTo,
                validFrom, validTill, status, approvalState, expired, frameworkOnly);

            var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;

            var result = await _client.GetAsync("IContracts",
                filter: filter,
                select: "Id,Name,TotalAmount,ValidFrom,ValidTill,RegistrationNumber,RegistrationDate,Subject,Note,LifeCycleState,InternalApprovalState,ExternalApprovalState,IsFrameworkContract",
                expand: "Counterparty,ResponsibleEmployee,OurSignatory",
                orderby: "Created desc",
                top: top);

            return FormatResults(result, filter);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}";
        }
    }

    private static List<string> BuildFilters(string? query, string? counterparty,
        decimal? amountFrom, decimal? amountTo, string? validFrom, string? validTill,
        string? status, string? approvalState, bool expired, bool frameworkOnly)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(query))
            filters.Add($"contains(Name, '{EscapeOData(query)}')");

        if (!string.IsNullOrWhiteSpace(counterparty))
            filters.Add($"Counterparty/Name ne null and contains(Counterparty/Name, '{EscapeOData(counterparty)}')");

        if (amountFrom.HasValue)
            filters.Add($"TotalAmount ge {amountFrom.Value.ToString(CultureInfo.InvariantCulture)}");

        if (amountTo.HasValue)
            filters.Add($"TotalAmount le {amountTo.Value.ToString(CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(validFrom))
            filters.Add($"ValidFrom ge {validFrom}T00:00:00Z");

        if (!string.IsNullOrWhiteSpace(validTill))
            filters.Add($"ValidTill le {validTill}T23:59:59Z");

        if (!string.IsNullOrWhiteSpace(status))
            filters.Add($"LifeCycleState eq '{EscapeOData(status)}'");

        if (!string.IsNullOrWhiteSpace(approvalState))
            filters.Add($"InternalApprovalState eq '{EscapeOData(approvalState)}'");

        if (expired)
            filters.Add($"ValidTill lt {DateTime.UtcNow:yyyy-MM-dd}T00:00:00Z");

        if (frameworkOnly)
            filters.Add("IsFrameworkContract eq true");

        return filters;
    }

    private static string FormatResults(JsonElement result, string? filter)
    {
        var items = GetItems(result);
        if (items.Count == 0)
        {
            var sb2 = new StringBuilder();
            sb2.AppendLine("Договоры не найдены.");
            if (filter is not null) sb2.AppendLine($"_Фильтр: {filter}_");
            return sb2.ToString();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**Договоры** ({items.Count} найдено)");
        if (filter is not null) sb.AppendLine($"_Фильтр: {filter}_");
        sb.AppendLine();

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = GetString(item, "Name");
            var regNum = GetString(item, "RegistrationNumber");
            var regDate = FormatDate(GetString(item, "RegistrationDate"));
            var cp = GetNestedString(item, "Counterparty", "Name");
            var responsible = GetNestedString(item, "ResponsibleEmployee", "Name");
            var signatory = GetNestedString(item, "OurSignatory", "Name");
            var subject = GetString(item, "Subject");
            var amount = FormatAmount(item);
            var validFrom = FormatDate(GetString(item, "ValidFrom"), "dd.MM.yyyy");
            var validTill = FormatDate(GetString(item, "ValidTill"), "dd.MM.yyyy");
            var state = GetString(item, "LifeCycleState");
            var approval = GetString(item, "InternalApprovalState");
            var extApproval = GetString(item, "ExternalApprovalState");
            var isFramework = GetString(item, "IsFrameworkContract") == "True" ? "Да" : "Нет";

            sb.AppendLine($"### Договор #{id}");
            sb.AppendLine($"**{name}**");
            sb.AppendLine();
            sb.AppendLine($"| Параметр | Значение |");
            sb.AppendLine($"|---|---|");
            if (regNum != "-") sb.AppendLine($"| Рег. номер | {regNum} от {regDate} |");
            sb.AppendLine($"| Контрагент | {cp} |");
            sb.AppendLine($"| Предмет | {subject} |");
            sb.AppendLine($"| Сумма | {amount} |");
            sb.AppendLine($"| Срок действия | {validFrom} — {validTill} |");
            sb.AppendLine($"| Статус | {state} |");
            sb.AppendLine($"| Согласование | внутр: {approval}, внеш: {extApproval} |");
            sb.AppendLine($"| Ответственный | {responsible} |");
            sb.AppendLine($"| Подписант | {signatory} |");
            sb.AppendLine($"| Рамочный | {isFramework} |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatAmount(JsonElement item)
    {
        if (item.TryGetProperty("TotalAmount", out var amt) && amt.ValueKind == JsonValueKind.Number)
            return amt.GetDecimal().ToString("N2", new CultureInfo("ru-RU")) + " руб.";
        return "-";
    }

    #endregion
    #region RouteBulkActionTool


    [McpServerTool(Name = "route_bulk_action")]
    [Description("Массовая маршрутизация заданий: переадресация/выполнение по фильтру. Preview перед выполнением.")]
    public async Task<string> RouteBulkAction(
        [Description("OData $filter для выбора заданий")] string filter,
        [Description("Действие: forward (переадресовать) или complete (выполнить)")] string action = "forward",
        [Description("ID сотрудника для переадресации (для action=forward)")] long forwardToId = 0,
        [Description("Режим: preview (показать что будет) или execute (выполнить)")] string mode = "preview",
        [Description("Максимум заданий для обработки")] int limit = 50)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Массовая маршрутизация заданий");
        sb.AppendLine();

        try
        {
            var json = await _client.GetAsync("IAssignments",
                $"$filter={filter} and Status eq 'InProcess'&$select=Id,Subject,Deadline&$expand=Performer($select=Id,Name),Author($select=Id,Name)&$top={limit}&$orderby=Created desc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine("Не удалось получить данные.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            var count = values.GetArrayLength();

            sb.AppendLine($"**Фильтр:** `{filter}`");
            sb.AppendLine($"**Действие:** {action}");
            sb.AppendLine($"**Найдено заданий:** {count}");
            sb.AppendLine($"**Режим:** {mode}");
            sb.AppendLine();

            if (count == 0)
            {
                sb.AppendLine("Заданий, соответствующих фильтру, не найдено.");
                return sb.ToString();
            }

            // Preview
            sb.AppendLine("| # | ID | Тема | Исполнитель | Автор |");
            sb.AppendLine("|---|-----|------|-------------|-------|");

            var assignments = new List<(long Id, string Subject)>();
            int idx = 0;
            foreach (var item in values.EnumerateArray())
            {
                idx++;
                var id = item.TryGetProperty("Id", out var idEl) ? idEl.GetInt64() : 0;
                var subj = item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "?" : "?";
                var perf = "?";
                if (item.TryGetProperty("Performer", out var p) && p.ValueKind == JsonValueKind.Object)
                    perf = p.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";
                var auth = "?";
                if (item.TryGetProperty("Author", out var a) && a.ValueKind == JsonValueKind.Object)
                    auth = a.TryGetProperty("Name", out var an) ? an.GetString() ?? "?" : "?";

                assignments.Add((id, subj));
                sb.AppendLine($"| {idx} | #{id} | {Truncate(subj, 35)} | {perf} | {auth} |");
            }
            sb.AppendLine();

            if (mode == "preview")
            {
                sb.AppendLine($"Для выполнения запустите с `mode=execute`.");
                if (action == "forward" && forwardToId == 0)
                    sb.AppendLine("**ВНИМАНИЕ:** Укажите `forwardToId` для переадресации.");
                return sb.ToString();
            }

            // Execute
            sb.AppendLine("## Результат");
            int success = 0, errors = 0;

            foreach (var (id, subj) in assignments)
            {
                try
                {
                    if (action == "forward")
                    {
                        if (forwardToId == 0)
                        {
                            sb.AppendLine($"- #{id} → **ПРОПУЩЕНО**: не указан forwardToId");
                            errors++;
                            continue;
                        }
                        await _client.PostActionAsync("IAssignments", id, "Forward",
                            JsonSerializer.Serialize(new { ForwardTo = new { Id = forwardToId } }));
                        sb.AppendLine($"- #{id} → переадресовано");
                    }
                    else // complete
                    {
                        await _client.PostActionAsync("IAssignments", id, "Complete",
                            JsonSerializer.Serialize(new { Result = "Completed" }));
                        sb.AppendLine($"- #{id} → выполнено");
                    }
                    success++;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"- #{id} → **ОШИБКА**: {ex.Message}");
                    errors++;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"**Успешно:** {success} | **Ошибок:** {errors}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }


    #endregion
}