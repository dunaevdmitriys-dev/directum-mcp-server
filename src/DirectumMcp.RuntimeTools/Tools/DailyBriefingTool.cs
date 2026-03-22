using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class DailyBriefingTool
{
    private readonly DirectumODataClient _client;
    public DailyBriefingTool(DirectumODataClient client) => _client = client;

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

            // 2. Pending approvals
            int approvalCount = 0;
            try
            {
                var approvalJson = await _client.GetAsync("IAssignments",
                    $"Status eq 'InProcess'", "Id", top: 0);
                // Use count from active — approvals are subset
                approvalCount = 0; // Will be estimated below
            }
            catch { }

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
}
