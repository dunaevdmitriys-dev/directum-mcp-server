using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class ContractExpiryTool
{
    private readonly DirectumODataClient _client;
    public ContractExpiryTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "contract_expiry")]
    [Description("Договоры, истекающие в указанный период: контрагент, сумма, дата окончания, дней осталось.")]
    public async Task<string> ContractExpiry(
        [Description("Период: 'апрель', 'май', или конкретные даты")] string? period = null,
        [Description("Дней вперёд (по умолчанию 30)")] int daysAhead = 30,
        [Description("Минимальная сумма для фильтрации")] double minAmount = 0,
        [Description("Макс. записей")] int top = 50)
    {
        var now = DateTime.UtcNow;
        DateTime dateFrom, dateTo;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var (from, to) = ParsePeriod(period, now);
            dateFrom = from;
            dateTo = to;
        }
        else
        {
            dateFrom = now;
            dateTo = now.AddDays(daysAhead);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Договоры, истекающие {dateFrom:dd.MM.yyyy} — {dateTo:dd.MM.yyyy}");
        sb.AppendLine();

        try
        {
            var filter = $"ValidTill ge {dateFrom:yyyy-MM-dd}T00:00:00Z and ValidTill le {dateTo:yyyy-MM-dd}T23:59:59Z and LifeCycleState eq 'Active'";
            if (minAmount > 0)
                filter += $" and TotalAmount ge {minAmount}";

            var json = await _client.GetAsync("IContracts",
                filter, "Id,Name,TotalAmount,ValidTill",
                "ValidTill asc",
                expand: "Counterparty($select=Id,Name),Currency($select=AlphaCode)",
                top: top);

            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            {
                sb.AppendLine("Договоров, истекающих в указанный период, не найдено.");
                return sb.ToString();
            }

            int count = 0;
            double totalAmount = 0;

            foreach (var item in values.EnumerateArray())
            {
                count++;
                var id = item.TryGetProperty("Id", out var idEl) ? idEl.GetInt64() : 0;
                var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var amount = item.TryGetProperty("TotalAmount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0;
                totalAmount += amount;

                var validTill = "";
                int daysLeft = 0;
                if (item.TryGetProperty("ValidTill", out var vt) && vt.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(vt.GetString(), out var vtDate))
                {
                    validTill = vtDate.ToString("dd.MM.yyyy");
                    daysLeft = (int)(vtDate - now).TotalDays;
                }

                var counterparty = "—";
                if (item.TryGetProperty("Counterparty", out var cp) && cp.ValueKind == JsonValueKind.Object)
                    counterparty = cp.TryGetProperty("Name", out var cpn) ? cpn.GetString() ?? "—" : "—";

                var currency = "";
                if (item.TryGetProperty("Currency", out var cur) && cur.ValueKind == JsonValueKind.Object)
                    currency = cur.TryGetProperty("AlphaCode", out var ca) ? ca.GetString() ?? "" : "";

                var urgency = daysLeft <= 7 ? " [СРОЧНО]" : daysLeft <= 14 ? " [скоро]" : "";

                sb.AppendLine($"{count}. #{id} {Truncate(name, 40)}{urgency}");
                sb.AppendLine($"   Контрагент: {counterparty}");
                sb.AppendLine($"   Сумма: {(amount > 0 ? $"{amount:N0} {currency}" : "не указана")}");
                sb.AppendLine($"   Истекает: {validTill} ({daysLeft} дн.)");
                sb.AppendLine();
            }

            sb.AppendLine($"Итого: {count} договоров на сумму {totalAmount:N0}");

            if (count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Рекомендации:");
                var urgent = values.EnumerateArray().Count(v =>
                    v.TryGetProperty("ValidTill", out var vt2) && DateTime.TryParse(vt2.GetString(), out var d) && (d - now).TotalDays <= 7);
                if (urgent > 0)
                    sb.AppendLine($"- {urgent} договоров истекают в ближайшие 7 дней — нужна пролонгация");
                sb.AppendLine("- Используйте `contract_review` для анализа рисков конкретного договора");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
        }

        return sb.ToString();
    }

    private static (DateTime From, DateTime To) ParsePeriod(string period, DateTime now)
    {
        var lower = period.ToLowerInvariant().Trim();
        var months = new Dictionary<string, int>
        {
            ["январ"] = 1, ["феврал"] = 2, ["март"] = 3, ["апрел"] = 4,
            ["ма"] = 5, ["июн"] = 6, ["июл"] = 7, ["август"] = 8,
            ["сентябр"] = 9, ["октябр"] = 10, ["ноябр"] = 11, ["декабр"] = 12
        };

        foreach (var (name, monthNum) in months)
        {
            if (lower.Contains(name))
            {
                var year = monthNum >= now.Month ? now.Year : now.Year + 1;
                var from = new DateTime(year, monthNum, 1);
                var to = from.AddMonths(1).AddDays(-1);
                return (from, to);
            }
        }

        // Try parse as date range "2026-04-01..2026-04-30"
        if (lower.Contains(".."))
        {
            var parts = lower.Split("..");
            if (parts.Length == 2 && DateTime.TryParse(parts[0], out var f) && DateTime.TryParse(parts[1], out var t))
                return (f, t);
        }

        // Default: next 30 days
        return (now, now.AddDays(30));
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "..." : s;
}
