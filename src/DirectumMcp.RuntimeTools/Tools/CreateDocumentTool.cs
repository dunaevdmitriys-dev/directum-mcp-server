using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class CreateDocumentTool
{
    private readonly DirectumODataClient _client;
    public CreateDocumentTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "create_document")]
    [Description("Создать документ в Directum RX: входящее письмо, служебную записку, приказ, простой документ. Указать тему, автора, контрагента.")]
    public async Task<string> CreateDocument(
        [Description("Тип: SimpleDocument, IncomingLetter, OutgoingLetter, Memo, Order")] string documentType = "SimpleDocument",
        [Description("Название документа")] string name = "",
        [Description("Тема")] string subject = "",
        [Description("Имя автора (для поиска)")] string authorName = "",
        [Description("Имя контрагента (для IncomingLetter)")] string counterpartyName = "",
        [Description("ID вида документа (DocumentKind)")] long documentKindId = 0,
        [Description("Дополнительные свойства JSON")] string extraJson = "")
    {
        var sb = new StringBuilder();

        try
        {
            var entitySet = documentType switch
            {
                "IncomingLetter" => "IIncomingLetters",
                "OutgoingLetter" => "IOutgoingLetters",
                "Memo" => "IMemos",
                "Order" => "IOrders",
                _ => "ISimpleDocuments"
            };

            var body = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(name))
                body["Name"] = name;
            if (!string.IsNullOrWhiteSpace(subject))
                body["Subject"] = subject;

            // Lookup author
            if (!string.IsNullOrWhiteSpace(authorName))
            {
                var empJson = await _client.GetAsync("IEmployees",
                    $"contains(Name, '{authorName}') and Status eq 'Active'", "Id,Name", top: 1);

                if (empJson.TryGetProperty("value", out var empVals) && empVals.GetArrayLength() > 0)
                {
                    var empId = empVals[0].TryGetProperty("Id", out var eid) ? eid.GetInt64() : 0;
                    if (empId > 0) body["Author"] = new { Id = empId };
                }
            }

            // Lookup counterparty (for IncomingLetter)
            if (!string.IsNullOrWhiteSpace(counterpartyName) && documentType == "IncomingLetter")
            {
                var cpJson = await _client.GetAsync("ICounterparties",
                    $"contains(Name, '{counterpartyName}')", "Id,Name", top: 1);

                if (cpJson.TryGetProperty("value", out var cpVals) && cpVals.GetArrayLength() > 0)
                {
                    var cpId = cpVals[0].TryGetProperty("Id", out var cid) ? cid.GetInt64() : 0;
                    if (cpId > 0) body["Correspondent"] = new { Id = cpId };
                }
            }

            // DocumentKind
            if (documentKindId > 0)
                body["DocumentKind"] = new { Id = documentKindId };

            // Extra properties
            if (!string.IsNullOrWhiteSpace(extraJson))
            {
                try
                {
                    var extra = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(extraJson);
                    if (extra != null)
                        foreach (var (k, v) in extra) body[k] = v;
                }
                catch { }
            }

            var result = await _client.PostAsync(entitySet, body);
            var docId = result.TryGetProperty("Id", out var id) ? id.GetInt64() : 0;
            var docName = result.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : name;

            sb.AppendLine("Документ создан");
            sb.AppendLine();
            sb.AppendLine($"ID: #{docId}");
            sb.AppendLine($"Тип: {documentType}");
            sb.AppendLine($"Название: {docName}");
            if (!string.IsNullOrWhiteSpace(subject))
                sb.AppendLine($"Тема: {subject}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Возможные причины:");
            sb.AppendLine("- Не указан обязательный DocumentKind");
            sb.AppendLine("- Нет прав на создание документов этого типа");
            sb.AppendLine($"- OData entity set для `{documentType}` может иметь другое имя");
        }

        return sb.ToString();
    }
}
