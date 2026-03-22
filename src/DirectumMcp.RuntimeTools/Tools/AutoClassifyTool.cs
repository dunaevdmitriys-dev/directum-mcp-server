using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AutoClassifyTool
{
    private readonly DirectumODataClient _client;
    public AutoClassifyTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "auto_classify")]
    [Description("Классификация входящих документов: анализ названия/содержания, предложение вида документа и контрагента.")]
    public async Task<string> AutoClassify(
        [Description("ID документа для классификации")] long documentId,
        [Description("OData тип документа (по умолчанию IOfficialDocuments)")] string entityType = "IOfficialDocuments")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Классификация документа");
        sb.AppendLine();

        try
        {
            var docJson = await _client.GetAsync(entityType,
                $"$filter=Id eq {documentId}&$select=Id,Name,Subject,Note&$expand=DocumentKind($select=Id,Name),Author($select=Id,Name),Counterparty($select=Id,Name)");

            if (docJson.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Документ {documentId} не найден.";

            var values = docJson.GetProperty("value");
            if (values.GetArrayLength() == 0)
                return $"**ОШИБКА**: Документ {documentId} не найден.";

            var item = values[0];
            var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
            var subject = item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : "";
            var note = item.TryGetProperty("Note", out var nt) ? nt.GetString() ?? "" : "";
            var currentKind = "не определён";
            if (item.TryGetProperty("DocumentKind", out var dk) && dk.ValueKind == JsonValueKind.Object)
                currentKind = dk.TryGetProperty("Name", out var dkn) ? dkn.GetString() ?? "?" : "?";

            var text = $"{name} {subject} {note}".ToLowerInvariant();

            sb.AppendLine($"**Документ:** #{documentId}");
            sb.AppendLine($"**Название:** {name}");
            sb.AppendLine($"**Тема:** {subject}");
            sb.AppendLine($"**Текущий вид:** {currentKind}");
            sb.AppendLine();

            // Rule-based classification
            sb.AppendLine("## Предложения");
            sb.AppendLine();

            var suggestions = new List<(string Kind, int Score, string Reason)>();

            if (text.Contains("договор") || text.Contains("контракт") || text.Contains("соглашени"))
                suggestions.Add(("Договор", 90, "Содержит «договор»/«контракт»/«соглашение»"));

            if (text.Contains("счёт") || text.Contains("счет") || text.Contains("invoice"))
                suggestions.Add(("Счёт на оплату", 85, "Содержит «счёт»/«invoice»"));

            if (text.Contains("акт") && (text.Contains("выполнен") || text.Contains("приём") || text.Contains("сверк")))
                suggestions.Add(("Акт", 80, "Содержит «акт выполненных работ»/«акт сверки»"));

            if (text.Contains("письм") || text.Contains("обращени") || text.Contains("запрос"))
                suggestions.Add(("Входящее письмо", 75, "Содержит «письмо»/«обращение»/«запрос»"));

            if (text.Contains("приказ") || text.Contains("распоряжени"))
                suggestions.Add(("Приказ", 85, "Содержит «приказ»/«распоряжение»"));

            if (text.Contains("служебн") || text.Contains("записк") || text.Contains("memo"))
                suggestions.Add(("Служебная записка", 80, "Содержит «служебная записка»"));

            if (text.Contains("доверенност") || text.Contains("мчд"))
                suggestions.Add(("Доверенность", 85, "Содержит «доверенность»/«МЧД»"));

            if (text.Contains("накладн") || text.Contains("упд") || text.Contains("торг-12"))
                suggestions.Add(("Товарная накладная", 80, "Содержит «накладная»/«УПД»/«ТОРГ-12»"));

            if (text.Contains("счёт-фактур") || text.Contains("счет-фактур"))
                suggestions.Add(("Счёт-фактура", 90, "Содержит «счёт-фактура»"));

            if (suggestions.Count == 0)
                suggestions.Add(("Простой документ", 50, "Не удалось определить тип по содержимому"));

            sb.AppendLine("| Вид документа | Уверенность | Причина |");
            sb.AppendLine("|--------------|-------------|---------|");
            foreach (var (kind, score, reason) in suggestions.OrderByDescending(s => s.Score))
                sb.AppendLine($"| **{kind}** | {score}% | {reason} |");

            sb.AppendLine();
            sb.AppendLine("## Действия");
            sb.AppendLine("Для применения вида документа используйте OData PATCH:");
            sb.AppendLine("```");
            sb.AppendLine($"PATCH /{entityType}({documentId})");
            sb.AppendLine("{\"DocumentKind\": {\"Id\": <DocumentKindId>}}");
            sb.AppendLine("```");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }
}
