using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AnalyzeSlaRulesTool
{
    private readonly DirectumODataClient _client;

    public AnalyzeSlaRulesTool(DirectumODataClient client)
    {
        _client = client;
    }

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
}
