using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class ProcessStatsTool
{
    private readonly DirectumODataClient _client;

    public ProcessStatsTool(DirectumODataClient client) => _client = client;

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
}

internal record AssignmentStat(
    string Subject, string Performer, DateTime Created, DateTime Completed,
    double DurationHours, bool IsOverdue);
