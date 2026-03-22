using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AnalyzePipelineValueTool
{
    private readonly DirectumODataClient _client;
    public AnalyzePipelineValueTool(DirectumODataClient client) => _client = client;

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
}
