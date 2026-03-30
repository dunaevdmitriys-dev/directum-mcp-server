using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AnalyzeBantTool
{
    private readonly DirectumODataClient _client;
    public AnalyzeBantTool(DirectumODataClient client) => _client = client;

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
}
