using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class TeamWorkloadTool
{
    private readonly DirectumODataClient _client;

    public TeamWorkloadTool(DirectumODataClient client) => _client = client;

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
}

internal record WorkloadItem(string Performer, int Total, int Overdue, int HighImportance);
