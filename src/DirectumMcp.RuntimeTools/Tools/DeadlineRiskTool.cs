using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class DeadlineRiskTool
{
    private readonly DirectumODataClient _client;

    public DeadlineRiskTool(DirectumODataClient client) => _client = client;

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
}

internal record RiskItem(
    long Id, string Subject, string Performer, DateTime Deadline,
    double HoursRemaining, double AvgHours, int ActiveCount,
    string Risk, string Importance);
