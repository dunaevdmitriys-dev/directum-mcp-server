using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text;
using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class AnalyticsTools
{
    private readonly DirectumODataClient _client;

    public AnalyticsTools(DirectumODataClient client)
    {
        _client = client;
    }

    #region ProcessStatsTool



    [McpServerTool(Name = "process_stats")]
    [Description("Статистика маршрутов и процессов: среднее время выполнения по типам задач, процент просрочки, самые частые задачи, нагрузка по дням недели.")]
    public async Task<string> GetProcessStats(
        [Description("Период анализа в днях (по умолчанию: 30)")] int days = 30,
        [Description("Группировка: type (по типу задачи), performer (по исполнителю), weekday (по дням недели)")] string groupBy = "type",
        [Description("Максимум групп в результате")] int top = 15)
    {
        top = Math.Clamp(top, 1, 50);
        try
        {
            var daysAgo = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Get completed tasks
            var taskFilter = $"Status eq 'Completed' and Created ge {daysAgo}";
            var taskResult = await _client.GetAsync(
                "ITasks",
                filter: taskFilter,
                select: "Id,Subject,Created,Started,MaxDeadline,Status",
                expand: "Author",
                top: 500);

            var tasks = GetItems(taskResult);

            // Get completed assignments
            var assignFilter = $"Status eq 'Completed' and Completed ne null and Created ge {daysAgo}";
            var assignResult = await _client.GetAsync(
                "IAssignments",
                filter: assignFilter,
                select: "Id,Subject,Created,Completed,Deadline",
                expand: "Performer",
                top: 1000);

            var assignments = GetItems(assignResult);

            // Also get active tasks for in-progress stats
            var activeResult = await _client.GetAsync(
                "ITasks",
                filter: "Status eq 'InProcess'",
                select: "Id,Subject,Created,MaxDeadline",
                top: 200);

            var activeTasks = GetItems(activeResult);

            return FormatReport(tasks, assignments, activeTasks, days, groupBy, top);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}\nПроверьте RX_ODATA_URL, RX_USERNAME, RX_PASSWORD.";
        }
    }

    internal static string FormatReport(
        List<JsonElement> tasks, List<JsonElement> assignments,
        List<JsonElement> activeTasks, int days, string groupBy, int top)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Статистика процессов");
        sb.AppendLine();
        sb.AppendLine($"**Период:** {days} дней");
        sb.AppendLine($"**Завершённых задач:** {tasks.Count} | **Завершённых заданий:** {assignments.Count} | **Активных задач:** {activeTasks.Count}");

        if (assignments.Count == 0 && tasks.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Нет данных за указанный период.");
            return sb.ToString();
        }

        // Parse assignments
        var parsedAssignments = ParseAssignments(assignments);

        // Global metrics
        AppendGlobalMetrics(sb, parsedAssignments);

        // Grouped analysis
        switch (groupBy.ToLowerInvariant())
        {
            case "performer":
                AppendByPerformer(sb, parsedAssignments, top);
                break;
            case "weekday":
                AppendByWeekday(sb, parsedAssignments);
                break;
            default: // "type"
                AppendByTaskType(sb, tasks, parsedAssignments, top);
                break;
        }

        // Active tasks overview
        if (activeTasks.Count > 0)
            AppendActiveOverview(sb, activeTasks);

        return sb.ToString();
    }

    private static List<AssignmentStat> ParseAssignments(List<JsonElement> items)
    {
        var result = new List<AssignmentStat>();
        foreach (var item in items)
        {
            var createdStr = GetString(item, "Created");
            var completedStr = GetString(item, "Completed");
            var deadlineStr = GetString(item, "Deadline");
            var performer = GetNestedString(item, "Performer", "Name");
            var subject = GetString(item, "Subject");

            if (!DateTime.TryParse(createdStr, out var created) ||
                !DateTime.TryParse(completedStr, out var completed))
                continue;

            var hours = (completed - created).TotalHours;
            var isOverdue = deadlineStr != "-" &&
                            DateTime.TryParse(deadlineStr, out var dl) &&
                            completed > dl;

            result.Add(new AssignmentStat(subject, performer, created, completed, hours, isOverdue));
        }
        return result;
    }

    private static void AppendGlobalMetrics(StringBuilder sb, List<AssignmentStat> data)
    {
        if (data.Count == 0) return;

        var avgHours = data.Average(a => a.DurationHours);
        var medianHours = GetMedian(data.Select(a => a.DurationHours).ToList());
        var overdueRate = data.Count(a => a.IsOverdue) * 100.0 / data.Count;
        var avgPerDay = data.Count / Math.Max(1, (data.Max(a => a.Completed) - data.Min(a => a.Created)).TotalDays);

        sb.AppendLine();
        sb.AppendLine("## Общие метрики");
        sb.AppendLine();
        sb.AppendLine("| Метрика | Значение |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Среднее время выполнения | {FormatDuration(avgHours)} |");
        sb.AppendLine($"| Медиана | {FormatDuration(medianHours)} |");
        sb.AppendLine($"| Процент просрочки | {overdueRate:F1}% |");
        sb.AppendLine($"| Заданий в день (ср.) | {avgPerDay:F1} |");
    }

    private static void AppendByPerformer(StringBuilder sb, List<AssignmentStat> data, int top)
    {
        var groups = data
            .GroupBy(a => a.Performer)
            .Where(g => g.Key != "-")
            .Select(g => new
            {
                Name = g.Key,
                Count = g.Count(),
                AvgHours = g.Average(a => a.DurationHours),
                OverdueRate = g.Count(a => a.IsOverdue) * 100.0 / g.Count()
            })
            .OrderByDescending(g => g.Count)
            .Take(top)
            .ToList();

        if (groups.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## По исполнителям");
        sb.AppendLine();
        sb.AppendLine("| Исполнитель | Заданий | Ср.время | Просрочка |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var g in groups)
        {
            var name = g.Name.Length > 25 ? g.Name[..22] + "..." : g.Name;
            sb.AppendLine($"| {name} | {g.Count} | {FormatDuration(g.AvgHours)} | {g.OverdueRate:F0}% |");
        }
    }

    private static void AppendByWeekday(StringBuilder sb, List<AssignmentStat> data)
    {
        var days = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var groups = data
            .GroupBy(a => ((int)a.Completed.DayOfWeek + 6) % 7) // Monday=0
            .OrderBy(g => g.Key)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("## По дням недели (завершение)");
        sb.AppendLine();
        sb.AppendLine("| День | Завершено | Ср.время | Просрочка |");
        sb.AppendLine("|---|---|---|---|");

        for (var i = 0; i < 7; i++)
        {
            var g = groups.FirstOrDefault(x => x.Key == i);
            if (g == null)
            {
                sb.AppendLine($"| {days[i]} | 0 | - | - |");
                continue;
            }
            var avg = g.Average(a => a.DurationHours);
            var overdueRate = g.Count(a => a.IsOverdue) * 100.0 / g.Count();
            var bar = new string('█', Math.Min(20, g.Count()));
            sb.AppendLine($"| {days[i]} | {g.Count()} {bar} | {FormatDuration(avg)} | {overdueRate:F0}% |");
        }
    }

    private static void AppendByTaskType(StringBuilder sb, List<JsonElement> tasks, List<AssignmentStat> assignments, int top)
    {
        // Extract task type from subject prefix (before "- " delimiter)
        var groups = assignments
            .GroupBy(a =>
            {
                var idx = a.Subject.IndexOf(" - ", StringComparison.Ordinal);
                return idx > 0 ? a.Subject[..idx].Trim() : "(без типа)";
            })
            .Select(g => new
            {
                Type = g.Key,
                Count = g.Count(),
                AvgHours = g.Average(a => a.DurationHours),
                OverdueRate = g.Count(a => a.IsOverdue) * 100.0 / g.Count()
            })
            .OrderByDescending(g => g.Count)
            .Take(top)
            .ToList();

        if (groups.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## По типам задач");
        sb.AppendLine();
        sb.AppendLine("| Тип | Кол-во | Ср.время | Просрочка |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var g in groups)
        {
            var type = g.Type.Length > 35 ? g.Type[..32] + "..." : g.Type;
            sb.AppendLine($"| {type} | {g.Count} | {FormatDuration(g.AvgHours)} | {g.OverdueRate:F0}% |");
        }
    }

    private static void AppendActiveOverview(StringBuilder sb, List<JsonElement> active)
    {
        var now = DateTime.UtcNow;
        var overdue = 0;
        var noDeadline = 0;

        foreach (var item in active)
        {
            var dl = GetString(item, "MaxDeadline");
            if (dl == "-") { noDeadline++; continue; }
            if (DateTime.TryParse(dl, out var deadline) && now > deadline)
                overdue++;
        }

        sb.AppendLine();
        sb.AppendLine("## Активные задачи");
        sb.AppendLine($"- Всего: **{active.Count}**");
        sb.AppendLine($"- Просрочены: **{overdue}**");
        sb.AppendLine($"- Без дедлайна: **{noDeadline}**");
    }

    private static double GetMedian(List<double> sorted)
    {
        sorted.Sort();
        if (sorted.Count == 0) return 0;
        return sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0
            : sorted[sorted.Count / 2];
    }

    private static string FormatDuration(double hours)
    {
        if (hours < 1) return $"{hours * 60:F0}м";
        if (hours < 24) return $"{hours:F1}ч";
        return $"{hours / 24:F1}д";
    }

    #endregion
    #region OverdueReportTool



    [McpServerTool(Name = "overdue_report")]
    [Description("Отчёт по просроченным заданиям — группировка по исполнителям, сортировка по длительности просрочки.")]
    public async Task<string> OverdueReport(
        [Description("Максимум результатов")] int top = 50,
        [Description("Группировать по: performer | importance | author")] string groupBy = "performer")
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var filter = $"Status eq 'InProcess' and Deadline lt {now}";
            var select = "Id,Subject,Deadline,Created,Importance";

            var result = await _client.GetAsync(
                "IAssignments",
                filter: filter,
                select: select,
                orderby: "Deadline asc",
                top: top,
                expand: "Performer,Author");

            var items = GetItems(result);
            var overdueItems = ParseOverdueItems(items);

            return FormatReport(overdueItems, groupBy);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить просроченные задания. Проверьте переменные RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static List<OverdueItem> ParseOverdueItems(List<JsonElement> items)
    {
        var now = DateTime.UtcNow;
        var result = new List<OverdueItem>();
        foreach (var item in items)
        {
            var id = GetLong(item, "Id");
            var subject = GetString(item, "Subject");
            var performer = GetNestedString(item, "Performer", "Name");
            var author = GetNestedString(item, "Author", "Name");
            var importance = GetString(item, "Importance");
            var deadlineStr = GetString(item, "Deadline");

            if (!DateTime.TryParse(deadlineStr, out var deadline))
                continue;

            var overdueDays = (now - deadline).TotalDays;
            result.Add(new OverdueItem(id, subject, performer, author, importance, deadline, overdueDays));
        }
        return result;
    }

    internal static string FormatReport(List<OverdueItem> items, string groupBy)
    {
        var today = DateTime.UtcNow.Date;
        var sb = new StringBuilder();
        sb.AppendLine("# Отчёт по просроченным заданиям");
        sb.AppendLine();
        sb.AppendLine($"**Дата:** {today:dd.MM.yyyy}");
        sb.AppendLine($"**Всего просрочено:** {items.Count}");

        if (items.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Просроченных заданий не найдено.");
            return sb.ToString();
        }

        var groupTitle = groupBy.ToLowerInvariant() switch
        {
            "importance" => "важности",
            "author" => "авторам",
            _ => "исполнителям"
        };

        sb.AppendLine();
        sb.AppendLine($"## По {groupTitle}");

        Func<OverdueItem, string> groupSelector = groupBy.ToLowerInvariant() switch
        {
            "importance" => item => item.Importance,
            "author" => item => item.Author,
            _ => item => item.Performer
        };

        var groups = items
            .GroupBy(groupSelector)
            .OrderByDescending(g => g.Count());

        foreach (var group in groups)
        {
            sb.AppendLine();
            sb.AppendLine($"### {group.Key} ({group.Count()} заданий)");
            sb.AppendLine();
            sb.AppendLine("| ID | Тема | Срок | Просрочка (дн) | Автор | Важность |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var item in group.OrderBy(i => i.Deadline))
            {
                var deadlineFormatted = item.Deadline.ToString("dd.MM.yyyy HH:mm");
                sb.AppendLine($"| {item.Id} | {item.Subject} | {deadlineFormatted} | {item.OverdueDays:F1} | {item.Author} | {item.Importance} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("*Для выполнения задания: `complete assignmentId=<ID>`*");

        return sb.ToString();
    }

    #endregion
    #region TeamWorkloadTool



    [McpServerTool(Name = "team_workload")]
    [Description("Нагрузка команды — количество активных заданий по исполнителям, баланс распределения.")]
    public async Task<string> TeamWorkload(
        [Description("Максимум исполнителей")] int top = 20)
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            var result = await _client.GetAsync(
                "IAssignments",
                filter: "Status eq 'InProcess'",
                select: "Id,Deadline,Importance",
                expand: "Performer");

            var items = GetItems(result);
            var workloadItems = BuildWorkload(items, top);

            return FormatReport(workloadItems);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить нагрузку команды. Проверьте переменные RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static List<WorkloadItem> BuildWorkload(List<JsonElement> items, int top)
    {
        var now = DateTime.UtcNow;
        return items
            .GroupBy(item => GetNestedString(item, "Performer", "Name"))
            .Select(g =>
            {
                var total = g.Count();
                var overdue = g.Count(item =>
                {
                    var deadlineStr = GetString(item, "Deadline");
                    return deadlineStr != "-" &&
                           DateTime.TryParse(deadlineStr, out var dl) &&
                           dl < now;
                });
                var highImportance = g.Count(item => GetString(item, "Importance") == "High");
                return new WorkloadItem(g.Key, total, overdue, highImportance);
            })
            .OrderByDescending(w => w.Total)
            .Take(top)
            .ToList();
    }

    internal static string FormatReport(List<WorkloadItem> items)
    {
        var today = DateTime.UtcNow.Date;
        var sb = new StringBuilder();
        sb.AppendLine("# Нагрузка команды");
        sb.AppendLine();
        sb.AppendLine($"**Дата:** {today:dd.MM.yyyy}");

        if (items.Count == 0)
        {
            sb.AppendLine("**Активных заданий:** 0");
            sb.AppendLine("**Исполнителей:** 0");
            sb.AppendLine();
            sb.AppendLine("Активных заданий не найдено.");
            return sb.ToString();
        }

        var sorted = items.OrderByDescending(w => w.Total).ToList();
        var totalAssignments = sorted.Sum(w => w.Total);
        var avg = sorted.Count > 0 ? (double)totalAssignments / sorted.Count : 0;
        var maxTotal = sorted.Max(w => w.Total);

        sb.AppendLine($"**Активных заданий:** {totalAssignments}");
        sb.AppendLine($"**Исполнителей:** {sorted.Count}");
        sb.AppendLine();
        sb.AppendLine("| Исполнитель | Заданий | Просрочено | Важных | Загрузка |");
        sb.AppendLine("|---|---|---|---|---|");

        foreach (var item in sorted)
        {
            var bar = BuildBar(item.Total, maxTotal);
            sb.AppendLine($"| {item.Performer} | {item.Total} | {item.Overdue} | {item.HighImportance} | {bar} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Средняя нагрузка:** {avg:F1} заданий/исполнитель");

        return sb.ToString();
    }

    internal static string BuildBar(int value, int max)
    {
        if (max <= 0) return "░░░░░░░░░░";
        var filled = (int)Math.Round(value * 10.0 / max);
        filled = Math.Clamp(filled, 0, 10);
        return new string('█', filled) + new string('░', 10 - filled);
    }

    #endregion
    #region DeadlineRiskTool



    [McpServerTool(Name = "deadline_risk")]
    [Description("Предсказание просрочки — анализ активных заданий с дедлайнами: риск (High/Medium/Low), дней до срока, загруженность исполнителя, историческое среднее время выполнения.")]
    public async Task<string> PredictDeadlineRisk(
        [Description("Период анализа исторических данных в днях (по умолчанию: 60)")] int historyDays = 60,
        [Description("Максимум результатов")] int top = 20,
        [Description("Фильтр по имени исполнителя (частичное совпадение)")] string? performer = null)
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            // 1. Get active assignments with deadlines
            var activeFilter = "Status eq 'InProcess' and Deadline ne null";
            if (!string.IsNullOrWhiteSpace(performer))
                activeFilter += $" and contains(Performer/Name, '{EscapeOData(performer)}')";

            var activeResult = await _client.GetAsync(
                "IAssignments",
                filter: activeFilter,
                select: "Id,Subject,Created,Deadline,Importance",
                expand: "Performer",
                top: 500);

            var activeItems = GetItems(activeResult);

            // 2. Get historical completed assignments for avg duration per performer
            var historyDate = DateTime.UtcNow.AddDays(-historyDays).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var historyFilter = $"Status eq 'Completed' and Completed ne null and Created ge {historyDate}";
            var historyResult = await _client.GetAsync(
                "IAssignments",
                filter: historyFilter,
                select: "Id,Created,Completed,Performer",
                expand: "Performer",
                top: 1000);

            var historyItems = GetItems(historyResult);

            // 3. Calculate performer averages
            var performerAvg = CalculatePerformerAverages(historyItems);

            // 4. Calculate workload (active count per performer)
            var workload = CalculateWorkload(activeItems);

            // 5. Score each active assignment
            var risks = ScoreAssignments(activeItems, performerAvg, workload);

            return FormatReport(risks, top, historyDays, performerAvg.Count);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}\nПроверьте RX_ODATA_URL, RX_USERNAME, RX_PASSWORD.";
        }
    }

    internal static Dictionary<string, double> CalculatePerformerAverages(List<JsonElement> items)
    {
        var groups = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var name = GetNestedString(item, "Performer", "Name");
            if (name == "-") continue;

            var createdStr = GetString(item, "Created");
            var completedStr = GetString(item, "Completed");
            if (!DateTime.TryParse(createdStr, out var created) ||
                !DateTime.TryParse(completedStr, out var completed))
                continue;

            var hours = (completed - created).TotalHours;
            if (hours < 0) continue;

            if (!groups.ContainsKey(name))
                groups[name] = [];
            groups[name].Add(hours);
        }

        return groups.ToDictionary(
            g => g.Key,
            g => g.Value.Average(),
            StringComparer.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, int> CalculateWorkload(List<JsonElement> items)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var name = GetNestedString(item, "Performer", "Name");
            if (name == "-") continue;
            result[name] = result.GetValueOrDefault(name) + 1;
        }
        return result;
    }

    internal static List<RiskItem> ScoreAssignments(
        List<JsonElement> active,
        Dictionary<string, double> performerAvg,
        Dictionary<string, int> workload)
    {
        var now = DateTime.UtcNow;
        var globalAvgHours = performerAvg.Values.Count > 0 ? performerAvg.Values.Average() : 48.0;
        var result = new List<RiskItem>();

        foreach (var item in active)
        {
            var id = GetLong(item, "Id");
            var subject = GetString(item, "Subject");
            var performerName = GetNestedString(item, "Performer", "Name");
            var deadlineStr = GetString(item, "Deadline");
            var createdStr = GetString(item, "Created");
            var importance = GetString(item, "Importance");

            if (!DateTime.TryParse(deadlineStr, out var deadline)) continue;
            if (!DateTime.TryParse(createdStr, out var created)) continue;

            var hoursRemaining = (deadline - now).TotalHours;
            var hoursElapsed = (now - created).TotalHours;
            var avgHours = performerAvg.GetValueOrDefault(performerName, globalAvgHours);
            var activeCount = workload.GetValueOrDefault(performerName, 1);

            // Risk scoring:
            // - How much time left vs performer's average?
            // - Workload multiplier (more tasks = slower)
            // - Already overdue = instant High
            var estimatedHours = avgHours * Math.Max(1.0, activeCount * 0.3);
            var remainingRatio = hoursRemaining / Math.Max(estimatedHours, 1.0);

            string risk;
            if (hoursRemaining <= 0)
                risk = "🔴 OVERDUE";
            else if (remainingRatio < 0.3 || hoursRemaining < 8)
                risk = "🔴 High";
            else if (remainingRatio < 0.7 || hoursRemaining < 24)
                risk = "🟡 Medium";
            else
                risk = "🟢 Low";

            result.Add(new RiskItem(
                id, subject, performerName, deadline, hoursRemaining,
                avgHours, activeCount, risk, importance));
        }

        return result.OrderBy(r => r.HoursRemaining).ToList();
    }

    internal static string FormatReport(List<RiskItem> risks, int top, int historyDays, int performerCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Прогноз просрочки заданий");
        sb.AppendLine();
        sb.AppendLine($"**Активных заданий с дедлайном:** {risks.Count}");
        sb.AppendLine($"**Исторических данных:** {historyDays} дней, {performerCount} исполнителей");

        if (risks.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Нет активных заданий с дедлайнами.");
            return sb.ToString();
        }

        // Summary
        var overdue = risks.Count(r => r.Risk.Contains("OVERDUE"));
        var high = risks.Count(r => r.Risk.Contains("High"));
        var medium = risks.Count(r => r.Risk.Contains("Medium"));
        var low = risks.Count(r => r.Risk.Contains("Low"));

        sb.AppendLine();
        sb.AppendLine("## Сводка рисков");
        sb.AppendLine($"- 🔴 Просрочено: **{overdue}**");
        sb.AppendLine($"- 🔴 Высокий риск: **{high}**");
        sb.AppendLine($"- 🟡 Средний: **{medium}**");
        sb.AppendLine($"- 🟢 Низкий: **{low}**");

        // Table (top N, prioritized by risk)
        var display = risks.Take(top).ToList();
        sb.AppendLine();
        sb.AppendLine("## Детали");
        sb.AppendLine();
        sb.AppendLine("| Риск | Задание | Исполнитель | Дедлайн | Осталось | Ср.время (ч) | Нагрузка |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var r in display)
        {
            var remaining = r.HoursRemaining <= 0
                ? $"**-{Math.Abs(r.HoursRemaining):F0}ч**"
                : r.HoursRemaining < 24
                    ? $"{r.HoursRemaining:F0}ч"
                    : $"{r.HoursRemaining / 24:F1}д";

            var subj = r.Subject.Length > 40 ? r.Subject[..37] + "..." : r.Subject;
            var perf = r.Performer.Length > 20 ? r.Performer[..17] + "..." : r.Performer;

            sb.AppendLine($"| {r.Risk} | {subj} | {perf} | {r.Deadline:dd.MM HH:mm} | {remaining} | {r.AvgHours:F0} | {r.ActiveCount} |");
        }

        if (risks.Count > top)
            sb.AppendLine($"\n> Показано {top} из {risks.Count}. Увеличьте параметр `top` для полного списка.");

        // Recommendations
        var criticalPerformers = risks
            .Where(r => r.Risk.Contains("OVERDUE") || r.Risk.Contains("High"))
            .GroupBy(r => r.Performer)
            .Where(g => g.Count() >= 2)
            .Select(g => $"**{g.Key}** ({g.Count()} заданий в зоне риска)")
            .ToList();

        if (criticalPerformers.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Рекомендации");
            sb.AppendLine("Перераспределить нагрузку для:");
            foreach (var p in criticalPerformers)
                sb.AppendLine($"- {p}");
        }

        return sb.ToString();
    }

    #endregion
    #region BottleneckDetectTool



    [McpServerTool(Name = "bottleneck_detect")]
    [Description("Анализ узких мест в процессах Directum RX — какие этапы маршрутов тормозят, кто задерживает выполнение заданий.")]
    public async Task<string> DetectBottlenecks(
        [Description("Период анализа в днях (по умолчанию: 30)")] int days = 30,
        [Description("Минимальное количество заданий для анализа")] int minCount = 5,
        [Description("Максимум результатов")] int top = 10)
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            var daysAgo = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var filter = $"Status eq 'Completed' and Completed ne null and Created ge {daysAgo}";
            var select = "Id,Subject,Created,Completed,Deadline,Performer";

            var result = await _client.GetAsync(
                "IAssignments",
                filter: filter,
                select: select,
                expand: "Performer");

            var items = GetItems(result);
            var assignments = ParseBottleneckAssignments(items);

            return FormatReport(assignments, days, minCount, top);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить анализ узких мест. Проверьте переменные RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static List<AssignmentData> ParseBottleneckAssignments(List<JsonElement> items)
    {
        var result = new List<AssignmentData>();
        foreach (var item in items)
        {
            var performer = GetNestedString(item, "Performer", "Name");
            var createdStr = GetString(item, "Created");
            var completedStr = GetString(item, "Completed");
            var deadlineStr = GetString(item, "Deadline");

            if (!DateTime.TryParse(createdStr, out var created) ||
                !DateTime.TryParse(completedStr, out var completed))
                continue;

            var durationHours = (completed - created).TotalHours;
            var isOverdue = deadlineStr != "-" &&
                            DateTime.TryParse(deadlineStr, out var deadline) &&
                            completed > deadline;

            result.Add(new AssignmentData(performer, durationHours, isOverdue));
        }
        return result;
    }

    internal static string FormatReport(List<AssignmentData> data, int days, int minCount = 5, int top = 10)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ узких мест");
        sb.AppendLine();
        sb.AppendLine($"**Период:** последние {days} дней");
        sb.AppendLine($"**Проанализировано заданий:** {data.Count}");

        if (data.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Нет завершённых заданий за указанный период.");
            return sb.ToString();
        }

        // Group by performer
        var byPerformer = data
            .GroupBy(a => a.Performer)
            .Where(g => g.Count() >= minCount)
            .Select(g => new
            {
                Performer = g.Key,
                Count = g.Count(),
                AvgHours = g.Average(a => a.DurationHours),
                OverdueCount = g.Count(a => a.IsOverdue)
            })
            .OrderByDescending(p => p.AvgHours)
            .Take(top)
            .ToList();

        sb.AppendLine();
        sb.AppendLine("## Самые медленные исполнители");

        if (byPerformer.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine($"Нет исполнителей с {minCount}+ заданиями за период.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("| Исполнитель | Заданий | Среднее время (ч) | Просрочено | % просрочки |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var p in byPerformer)
            {
                var overduePercent = p.Count > 0 ? p.OverdueCount * 100.0 / p.Count : 0;
                sb.AppendLine($"| {p.Performer} | {p.Count} | {p.AvgHours:F1} | {p.OverdueCount} | {overduePercent:F0}% |");
            }
        }

        // Global stats
        var allDurations = data.Select(a => a.DurationHours).OrderBy(h => h).ToList();
        var avgHours = allDurations.Average();
        var median = allDurations.Count % 2 == 0
            ? (allDurations[allDurations.Count / 2 - 1] + allDurations[allDurations.Count / 2]) / 2.0
            : allDurations[allDurations.Count / 2];
        var overdueTotal = data.Count(a => a.IsOverdue);
        var globalOverduePercent = data.Count > 0 ? overdueTotal * 100.0 / data.Count : 0;

        sb.AppendLine();
        sb.AppendLine("## Статистика по срокам");
        sb.AppendLine();
        sb.AppendLine("| Метрика | Значение |");
        sb.AppendLine("|---|---|");
        sb.AppendLine($"| Среднее время выполнения | {avgHours:F1} ч |");
        sb.AppendLine($"| Медиана | {median:F1} ч |");
        sb.AppendLine($"| Заданий с просрочкой | {overdueTotal} ({globalOverduePercent:F0}%) |");

        return sb.ToString();
    }

    #endregion
    #region AnalyzePipelineValueTool


    [McpServerTool(Name = "analyze_pipeline_value")]
    [Description("Взвешенная стоимость воронки продаж: сумма × вероятность по этапам, прогноз выручки.")]
    public async Task<string> AnalyzePipelineValue(
        [Description("OData тип сущности сделок")] string entityType = "IDeals",
        [Description("Поле суммы")] string amountField = "TotalAmount",
        [Description("Поле вероятности (0-100) или этапа")] string probabilityField = "Probability",
        [Description("OData $filter для активных сделок")] string filter = "Status eq 'Active'")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ воронки продаж");
        sb.AppendLine();

        try
        {
            var json = await _client.GetAsync(entityType,
                $"$filter={filter}&$select=Id,Name,{amountField},{probabilityField}&$top=1000");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine($"Не удалось получить `{entityType}`.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            int total = 0;
            double totalRaw = 0, totalWeighted = 0;

            foreach (var item in values.EnumerateArray())
            {
                total++;
                double amount = 0, prob = 50;

                if (item.TryGetProperty(amountField, out var aEl) && aEl.ValueKind == JsonValueKind.Number)
                    amount = aEl.GetDouble();

                if (item.TryGetProperty(probabilityField, out var pEl) && pEl.ValueKind == JsonValueKind.Number)
                    prob = pEl.GetDouble();

                totalRaw += amount;
                totalWeighted += amount * (prob / 100.0);
            }

            sb.AppendLine($"**Активных сделок:** {total}");
            sb.AppendLine($"**Общая стоимость (raw):** {totalRaw:N0}");
            sb.AppendLine($"**Взвешенная стоимость:** {totalWeighted:N0}");
            sb.AppendLine($"**Средняя вероятность:** {(total > 0 && totalRaw > 0 ? 100.0 * totalWeighted / totalRaw : 0):F0}%");
            sb.AppendLine($"**Средний чек:** {(total > 0 ? totalRaw / total : 0):N0}");
            sb.AppendLine();
            sb.AppendLine($"**Прогноз выручки (взвешенный):** {totalWeighted:N0}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
    #region AnalyzeBantTool


    [McpServerTool(Name = "analyze_bant")]
    [Description("Анализ BANT-распределения лидов/сделок: Budget, Authority, Need, Timeline. Скоринг и рекомендации.")]
    public async Task<string> AnalyzeBant(
        [Description("OData тип сущности (например 'ILeads' или 'IDeals')")] string entityType = "ILeads",
        [Description("Поле бюджета (например 'Budget' или 'TotalAmount')")] string budgetField = "Budget",
        [Description("Дополнительный OData $filter")] string filter = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# BANT-анализ");
        sb.AppendLine();

        try
        {
            var odataFilter = string.IsNullOrWhiteSpace(filter) ? "" : $"$filter={filter}&";
            var json = await _client.GetAsync(entityType,
                $"{odataFilter}$select=Id,Name,Status,{budgetField}&$top=1000&$orderby=Created desc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine($"Не удалось получить `{entityType}`. Проверьте OData и тип сущности.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            int total = 0, withBudget = 0, noBudget = 0;
            double totalAmount = 0;
            var statusCounts = new Dictionary<string, int>();

            foreach (var item in values.EnumerateArray())
            {
                total++;
                if (item.TryGetProperty(budgetField, out var budgetEl) &&
                    budgetEl.ValueKind == JsonValueKind.Number)
                {
                    var amount = budgetEl.GetDouble();
                    if (amount > 0) { withBudget++; totalAmount += amount; }
                    else noBudget++;
                }
                else noBudget++;

                var status = item.TryGetProperty("Status", out var s) ? s.GetString() ?? "Unknown" : "Unknown";
                statusCounts[status] = statusCounts.GetValueOrDefault(status) + 1;
            }

            sb.AppendLine($"**Всего:** {total}");
            sb.AppendLine($"**С бюджетом (B):** {withBudget} ({(total > 0 ? 100.0 * withBudget / total : 0):F0}%)");
            sb.AppendLine($"**Без бюджета:** {noBudget}");
            sb.AppendLine($"**Общая сумма:** {totalAmount:N0}");
            sb.AppendLine($"**Средний чек:** {(withBudget > 0 ? totalAmount / withBudget : 0):N0}");
            sb.AppendLine();

            sb.AppendLine("## По статусам");
            foreach (var (status, count) in statusCounts.OrderByDescending(x => x.Value))
                sb.AppendLine($"- {status}: {count} ({100.0 * count / total:F0}%)");
            sb.AppendLine();

            sb.AppendLine("## Рекомендации BANT");
            if (withBudget < total * 0.3)
                sb.AppendLine("- **Budget:** <30% с бюджетом. Фокус на квалификации бюджета на ранних этапах.");
            if (total > 0 && withBudget > total * 0.7)
                sb.AppendLine("- **Budget:** >70% с бюджетом. Хорошая квалификация, фокус на Authority и Timeline.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
    #region AnalyzeSlaRulesTool



    [McpServerTool(Name = "analyze_sla_rules")]
    [Description("Анализ SLA-правил: сроки обработки, просрочки, режимы (User/Group/Overtime), статистика соблюдения.")]
    public async Task<string> AnalyzeSlaRules(
        [Description("OData тип сущности SLA (например 'ISLASettings' или имя кастомной сущности)")] string slaEntityType = "",
        [Description("OData тип задания для анализа просрочек (например 'IAssignments')")] string assignmentType = "IAssignments",
        [Description("Количество дней для анализа (по умолчанию 30)")] int days = 30)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ SLA");
        sb.AppendLine();

        try
        {
            // 1. Get assignments with deadlines
            var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var filter = $"Created ge {since}";
            var result = await _client.GetAsync(assignmentType,
                $"$filter={filter}&$select=Id,Subject,Created,Deadline,Status,Modified&$top=500&$orderby=Created desc");

            if (result.ValueKind == System.Text.Json.JsonValueKind.Undefined)
            {
                sb.AppendLine($"Не удалось получить данные из `{assignmentType}`. Проверьте OData URL и права.");
                return sb.ToString();
            }

            var values = result.GetProperty("value");
            int total = 0, overdue = 0, inProcess = 0, completed = 0;
            var now = DateTime.UtcNow;

            foreach (var item in values.EnumerateArray())
            {
                total++;
                var status = item.TryGetProperty("Status", out var s) ? s.GetString() ?? "" : "";

                if (status == "InProcess") inProcess++;
                else if (status == "Completed") completed++;

                if (item.TryGetProperty("Deadline", out var dl) && dl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    if (DateTime.TryParse(dl.GetString(), out var deadline))
                    {
                        if (status == "InProcess" && deadline < now)
                            overdue++;
                        else if (status == "Completed" && item.TryGetProperty("Modified", out var mod) &&
                                 DateTime.TryParse(mod.GetString(), out var completedDate) && completedDate > deadline)
                            overdue++;
                    }
                }
            }

            sb.AppendLine($"**Период:** последние {days} дней");
            sb.AppendLine($"**Всего заданий:** {total}");
            sb.AppendLine($"**В работе:** {inProcess}");
            sb.AppendLine($"**Выполнено:** {completed}");
            sb.AppendLine($"**Просрочено:** {overdue}");
            sb.AppendLine($"**% соблюдения SLA:** {(total > 0 ? (100.0 * (total - overdue) / total).ToString("F1") : "—")}%");
            sb.AppendLine();

            if (overdue > 0)
            {
                sb.AppendLine("## Рекомендации");
                var overduePercent = 100.0 * overdue / total;
                if (overduePercent > 30)
                    sb.AppendLine("- **КРИТИЧНО**: >30% просрочек. Пересмотрите SLA сроки или увеличьте штат.");
                else if (overduePercent > 15)
                    sb.AppendLine("- **ВНИМАНИЕ**: 15-30% просрочек. Анализируйте bottleneck через `bottleneck_detect`.");
                else
                    sb.AppendLine("- SLA в целом соблюдается. Точечные просрочки — проверьте `overdue_report`.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Убедитесь что:");
            sb.AppendLine("- RX_ODATA_URL указывает на рабочий сервис интеграции");
            sb.AppendLine("- Сервисный пользователь имеет права на чтение заданий");
        }

        return sb.ToString();
    }

    #endregion
    #region AuditAssignmentStrategyTool


    [McpServerTool(Name = "audit_assignment_strategy")]
    [Description("Аудит распределения заданий: баланс нагрузки, round-robin, перекосы по исполнителям.")]
    public async Task<string> AuditAssignmentStrategy(
        [Description("OData тип заданий")] string entityType = "IAssignments",
        [Description("Количество дней для анализа")] int days = 30,
        [Description("OData $filter дополнительный")] string filter = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Аудит распределения заданий");
        sb.AppendLine();

        try
        {
            var since = DateTime.UtcNow.AddDays(-days).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var baseFilter = $"Created ge {since}";
            if (!string.IsNullOrWhiteSpace(filter)) baseFilter += $" and {filter}";

            var json = await _client.GetAsync(entityType,
                $"$filter={baseFilter}&$select=Id,Created,Status,Deadline,Modified&$expand=Performer($select=Id,Name),Author($select=Id,Name)&$top=2000&$orderby=Created desc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine("Не удалось получить данные.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            var performerStats = new Dictionary<string, (string Name, int Total, int Overdue, int Completed)>();
            var authorStats = new Dictionary<string, int>();
            int total = 0;

            foreach (var item in values.EnumerateArray())
            {
                total++;
                var performerName = "Unknown";
                var performerId = "0";

                if (item.TryGetProperty("Performer", out var perf) && perf.ValueKind == JsonValueKind.Object)
                {
                    performerName = perf.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";
                    performerId = perf.TryGetProperty("Id", out var pid) ? pid.GetRawText() : "0";
                }

                if (!performerStats.ContainsKey(performerId))
                    performerStats[performerId] = (performerName, 0, 0, 0);

                var (name, t, o, c) = performerStats[performerId];
                t++;

                var status = item.TryGetProperty("Status", out var s) ? s.GetString() ?? "" : "";
                if (status == "Completed") c++;

                if (item.TryGetProperty("Deadline", out var dl) && dl.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(dl.GetString(), out var deadline))
                {
                    if (status == "InProcess" && deadline < DateTime.UtcNow) o++;
                    else if (status == "Completed" && item.TryGetProperty("Modified", out var mod) &&
                             DateTime.TryParse(mod.GetString(), out var completed) && completed > deadline) o++;
                }

                performerStats[performerId] = (name, t, o, c);

                if (item.TryGetProperty("Author", out var auth) && auth.ValueKind == JsonValueKind.Object)
                {
                    var authorName = auth.TryGetProperty("Name", out var an) ? an.GetString() ?? "?" : "?";
                    authorStats[authorName] = authorStats.GetValueOrDefault(authorName) + 1;
                }
            }

            sb.AppendLine($"**Период:** последние {days} дней");
            sb.AppendLine($"**Всего заданий:** {total}");
            sb.AppendLine();

            // Distribution table
            sb.AppendLine("## Распределение по исполнителям");
            sb.AppendLine("| Исполнитель | Всего | Выполнено | Просрочено | % загрузки |");
            sb.AppendLine("|-------------|-------|-----------|------------|------------|");

            var avgLoad = total > 0 && performerStats.Count > 0 ? (double)total / performerStats.Count : 0;

            foreach (var (_, stats) in performerStats.OrderByDescending(x => x.Value.Total))
            {
                var loadPercent = total > 0 ? 100.0 * stats.Total / total : 0;
                var icon = stats.Total > avgLoad * 1.5 ? " **ПЕРЕГРУЗ**" : stats.Total < avgLoad * 0.5 ? " _недогруз_" : "";
                sb.AppendLine($"| {stats.Name}{icon} | {stats.Total} | {stats.Completed} | {stats.Overdue} | {loadPercent:F0}% |");
            }
            sb.AppendLine();

            // Balance assessment
            if (performerStats.Count > 1)
            {
                var loads = performerStats.Values.Select(x => x.Total).ToList();
                var max = loads.Max();
                var min = loads.Min();
                var ratio = min > 0 ? (double)max / min : 999;

                sb.AppendLine("## Оценка баланса");
                if (ratio > 3)
                    sb.AppendLine($"- **КРИТИЧНО:** перекос {ratio:F1}x (макс {max} / мин {min}). Нужна ребалансировка.");
                else if (ratio > 2)
                    sb.AppendLine($"- **ВНИМАНИЕ:** перекос {ratio:F1}x. Рекомендуется проверить распределение.");
                else
                    sb.AppendLine($"- **OK:** баланс {ratio:F1}x — в пределах нормы.");

                sb.AppendLine($"- Средняя нагрузка: {avgLoad:F0} заданий/чел.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
}

internal record AssignmentStat(
    string Subject, string Performer, DateTime Created, DateTime Completed,
    double DurationHours, bool IsOverdue);
internal record OverdueItem(long Id, string Subject, string Performer, string Author, string Importance, DateTime Deadline, double OverdueDays);
internal record WorkloadItem(string Performer, int Total, int Overdue, int HighImportance);
internal record RiskItem(
    long Id, string Subject, string Performer, DateTime Deadline,
    double HoursRemaining, double AvgHours, int ActiveCount,
    string Risk, string Importance);
internal record AssignmentData(string Performer, double DurationHours, bool IsOverdue);