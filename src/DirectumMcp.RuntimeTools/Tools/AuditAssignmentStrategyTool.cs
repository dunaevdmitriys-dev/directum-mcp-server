using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AuditAssignmentStrategyTool
{
    private readonly DirectumODataClient _client;
    public AuditAssignmentStrategyTool(DirectumODataClient client) => _client = client;

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
}
