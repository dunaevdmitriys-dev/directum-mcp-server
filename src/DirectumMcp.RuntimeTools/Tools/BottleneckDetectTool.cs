using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class BottleneckDetectTool
{
    private readonly DirectumODataClient _client;

    public BottleneckDetectTool(DirectumODataClient client) => _client = client;

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
            var assignments = ParseAssignments(items);

            return FormatReport(assignments, days, minCount, top);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить анализ узких мест. Проверьте переменные RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static List<AssignmentData> ParseAssignments(List<JsonElement> items)
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
}

internal record AssignmentData(string Performer, double DurationHours, bool IsOverdue);
