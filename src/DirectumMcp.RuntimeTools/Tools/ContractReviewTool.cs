using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class ContractReviewTool
{
    private readonly DirectumODataClient _client;
    public ContractReviewTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "contract_review")]
    [Description("Анализ рисков договора: проверка обязательных полей, сроков, сумм, контрагента. Рекомендации перед согласованием.")]
    public async Task<string> ContractReview(
        [Description("ID договора")] long contractId,
        [Description("OData тип (по умолчанию IContractualDocuments)")] string entityType = "IContractualDocuments")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ рисков договора");
        sb.AppendLine();

        try
        {
            var json = await _client.GetAsync(entityType,
                $"$filter=Id eq {contractId}" +
                $"&$select=Id,Name,Subject,TotalAmount,ValidFrom,ValidTill,LifeCycleState,Note,IsAutomaticRenewal" +
                $"&$expand=Counterparty($select=Id,Name,TIN,Status),OurSignatory($select=Id,Name),DocumentKind($select=Id,Name),Currency($select=Id,Name,AlphaCode)");

            if (json.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Договор {contractId} не найден.";

            var values = json.GetProperty("value");
            if (values.GetArrayLength() == 0)
                return $"**ОШИБКА**: Договор {contractId} не найден.";

            var doc = values[0];
            var name = doc.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var amount = doc.TryGetProperty("TotalAmount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() : 0;
            var validFrom = doc.TryGetProperty("ValidFrom", out var vf) && vf.ValueKind == JsonValueKind.String ? vf.GetString() : null;
            var validTill = doc.TryGetProperty("ValidTill", out var vt) && vt.ValueKind == JsonValueKind.String ? vt.GetString() : null;
            var state = doc.TryGetProperty("LifeCycleState", out var ls) ? ls.GetString() ?? "?" : "?";
            var autoRenewal = doc.TryGetProperty("IsAutomaticRenewal", out var ar) && ar.ValueKind == JsonValueKind.True;

            var counterpartyName = "не указан";
            var counterpartyTin = "";
            var counterpartyStatus = "";
            if (doc.TryGetProperty("Counterparty", out var cp) && cp.ValueKind == JsonValueKind.Object)
            {
                counterpartyName = cp.TryGetProperty("Name", out var cpn) ? cpn.GetString() ?? "?" : "?";
                counterpartyTin = cp.TryGetProperty("TIN", out var cpt) ? cpt.GetString() ?? "" : "";
                counterpartyStatus = cp.TryGetProperty("Status", out var cps) ? cps.GetString() ?? "" : "";
            }

            var signatory = "не указан";
            if (doc.TryGetProperty("OurSignatory", out var os) && os.ValueKind == JsonValueKind.Object)
                signatory = os.TryGetProperty("Name", out var osn) ? osn.GetString() ?? "?" : "?";

            sb.AppendLine($"**Договор:** #{contractId} — {name}");
            sb.AppendLine($"**Контрагент:** {counterpartyName} (ИНН: {(string.IsNullOrEmpty(counterpartyTin) ? "не указан" : counterpartyTin)})");
            sb.AppendLine($"**Сумма:** {(amount > 0 ? amount.ToString("N2") : "не указана")}");
            sb.AppendLine($"**Срок:** {validFrom ?? "?"} — {validTill ?? "бессрочный"}");
            sb.AppendLine($"**Подписант:** {signatory}");
            sb.AppendLine($"**Статус:** {state}");
            sb.AppendLine($"**Автопролонгация:** {(autoRenewal ? "да" : "нет")}");
            sb.AppendLine();

            // Risk analysis
            var risks = new List<(string Level, string Risk, string Recommendation)>();

            // 1. Amount
            if (amount <= 0)
                risks.Add(("HIGH", "Сумма не указана", "Заполните TotalAmount перед согласованием"));
            else if (amount > 10_000_000)
                risks.Add(("MEDIUM", $"Крупная сумма: {amount:N0}", "Требуется дополнительное согласование с финансовым директором"));

            // 2. Counterparty
            if (counterpartyName == "не указан")
                risks.Add(("HIGH", "Контрагент не указан", "Укажите контрагента"));
            if (counterpartyStatus == "Closed")
                risks.Add(("HIGH", $"Контрагент '{counterpartyName}' закрыт", "Нельзя заключать договор с закрытым контрагентом"));
            if (string.IsNullOrEmpty(counterpartyTin))
                risks.Add(("MEDIUM", "У контрагента не указан ИНН", "Проверьте реквизиты контрагента"));

            // 3. Dates
            if (validFrom == null)
                risks.Add(("MEDIUM", "Дата начала не указана", "Укажите ValidFrom"));
            if (validTill != null && DateTime.TryParse(validTill, out var tillDate) && tillDate < DateTime.UtcNow)
                risks.Add(("HIGH", $"Срок действия истёк: {validTill}", "Договор просрочен, нужна пролонгация или новый договор"));
            if (validTill != null && DateTime.TryParse(validTill, out var tillDate2) &&
                tillDate2 < DateTime.UtcNow.AddDays(30))
                risks.Add(("MEDIUM", $"Срок истекает менее чем через 30 дней", "Подготовьте пролонгацию или новый договор"));

            // 4. Signatory
            if (signatory == "не указан")
                risks.Add(("MEDIUM", "Подписант не указан", "Укажите OurSignatory"));

            // 5. Auto-renewal
            if (autoRenewal && validTill != null)
                risks.Add(("INFO", "Включена автопролонгация", "Убедитесь что условия автопролонгации устраивают"));

            // Report
            sb.AppendLine("## Риски");
            sb.AppendLine();

            if (risks.Count == 0)
            {
                sb.AppendLine("Критических рисков не обнаружено. Договор готов к согласованию.");
            }
            else
            {
                sb.AppendLine("| Уровень | Риск | Рекомендация |");
                sb.AppendLine("|---------|------|-------------|");
                foreach (var (level, risk, rec) in risks.OrderByDescending(r => r.Level))
                    sb.AppendLine($"| **{level}** | {risk} | {rec} |");

                var highCount = risks.Count(r => r.Level == "HIGH");
                sb.AppendLine();
                if (highCount > 0)
                    sb.AppendLine($"**ВЕРДИКТ:** {highCount} критических рисков — НЕ РЕКОМЕНДУЕТСЯ отправлять на согласование.");
                else
                    sb.AppendLine("**ВЕРДИКТ:** Критических рисков нет, можно отправлять на согласование.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }
}
