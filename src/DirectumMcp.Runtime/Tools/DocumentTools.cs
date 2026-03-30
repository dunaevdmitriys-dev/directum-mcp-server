using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class DocumentTools
{
    private readonly DirectumODataClient _client;
    public DocumentTools(DirectumODataClient client) => _client = client;

    #region CreateDocumentTool

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
            if (!string.IsNullOrWhiteSpace(name)) body["Name"] = name;
            if (!string.IsNullOrWhiteSpace(subject)) body["Subject"] = subject;

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

            if (documentKindId > 0) body["DocumentKind"] = new { Id = documentKindId };

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
            if (!string.IsNullOrWhiteSpace(subject)) sb.AppendLine($"Тема: {subject}");
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

    #endregion

    #region UpdateEntityTool

    [McpServerTool(Name = "update_entity")]
    [Description("Обновить свойства сущности (PATCH). Изменить название, статус, срок, сумму, ответственного и т.д.")]
    public async Task<string> UpdateEntity(
        [Description("OData тип сущности (IContracts, IOfficialDocuments, IEmployees, IAssignments...)")] string entityType,
        [Description("ID сущности")] long entityId,
        [Description("Свойства для обновления в формате JSON: {\"Subject\":\"Новая тема\",\"Importance\":\"High\"}")] string propertiesJson,
        [Description("Режим: preview (показать что изменится) или execute (применить)")] string mode = "preview")
    {
        var sb = new StringBuilder();
        try
        {
            Dictionary<string, JsonElement> properties;
            try
            {
                properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(propertiesJson)
                    ?? new Dictionary<string, JsonElement>();
            }
            catch (JsonException ex)
            {
                return $"Невалидный JSON: {ex.Message}\nФормат: {{\"Subject\":\"Новая тема\",\"Importance\":\"High\"}}";
            }

            if (properties.Count == 0)
                return "Нет свойств для обновления. Укажите JSON: {\"Field\":\"Value\"}";

            var currentJson = await _client.GetAsync(entityType, $"Id eq {entityId}",
                string.Join(",", properties.Keys.Append("Id").Append("Name").Distinct()));

            if (!currentJson.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
                return $"Сущность {entityType}({entityId}) не найдена.";

            var current = values[0];
            var name = current.TryGetProperty("Name", out var n) ? n.GetString() ?? "" :
                       current.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : $"#{entityId}";

            sb.AppendLine($"Обновление: {entityType}({entityId}) — {name}");
            sb.AppendLine();
            sb.AppendLine("Изменения:");

            foreach (var (key, newValue) in properties)
            {
                var oldValue = current.TryGetProperty(key, out var old) ? FormatValue(old) : "—";
                var newVal = FormatValue(newValue);
                sb.AppendLine($"  {key}: {oldValue} → {newVal}");
            }
            sb.AppendLine();

            if (mode == "preview")
            {
                sb.AppendLine("Режим предпросмотра. Запустите с mode=execute для применения.");
                return sb.ToString();
            }

            await _client.PatchAsync(entityType, entityId, properties);
            sb.AppendLine("Изменения применены.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
        }
        return sb.ToString();
    }

    private static string FormatValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => $"\"{el.GetString()}\"",
        JsonValueKind.Number => el.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => el.GetRawText()
    };

    #endregion

    #region DeleteEntityTool

    [McpServerTool(Name = "delete_entity")]
    [Description("Удалить сущность по ID. Только черновики и отменённые. Preview перед удалением.")]
    public async Task<string> DeleteEntity(
        [Description("OData тип сущности")] string entityType,
        [Description("ID сущности")] long entityId,
        [Description("Режим: preview (показать что удалится) или execute (удалить)")] string mode = "preview")
    {
        var sb = new StringBuilder();
        try
        {
            var json = await _client.GetAsync(entityType, $"Id eq {entityId}", "Id,Name,Subject,Status,LifeCycleState");
            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
                return $"Сущность {entityType}({entityId}) не найдена.";

            var item = values[0];
            var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" :
                       item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : $"#{entityId}";
            var status = item.TryGetProperty("Status", out var st) ? st.GetString() ?? "" : "";
            var lifecycle = item.TryGetProperty("LifeCycleState", out var lc) ? lc.GetString() ?? "" : "";

            var isDraft = lifecycle is "Draft" or "" || status is "Draft" or "Aborted" or "Closed";
            if (!isDraft && mode == "execute")
            {
                sb.AppendLine($"ОТКАЗАНО: {entityType}({entityId}) — {name}");
                sb.AppendLine($"Статус: {status}, ЖЦ: {lifecycle}");
                sb.AppendLine();
                sb.AppendLine("Удаление разрешено только для черновиков (Draft), отменённых (Aborted) и закрытых (Closed).");
                sb.AppendLine("Для активных сущностей используйте update_entity для смены статуса.");
                return sb.ToString();
            }

            sb.AppendLine($"Удаление: {entityType}({entityId})");
            sb.AppendLine($"Название: {name}");
            sb.AppendLine($"Статус: {status}");
            if (!string.IsNullOrEmpty(lifecycle)) sb.AppendLine($"Жизненный цикл: {lifecycle}");
            sb.AppendLine();

            if (mode == "preview")
            {
                sb.AppendLine(isDraft
                    ? "Можно удалить. Запустите с mode=execute."
                    : "ВНИМАНИЕ: сущность не в черновике. Удаление может быть заблокировано сервером.");
                return sb.ToString();
            }

            await _client.DeleteAsync(entityType, entityId);
            sb.AppendLine("Удалено.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Возможные причины:");
            sb.AppendLine("- Нет прав на удаление");
            sb.AppendLine("- Сущность используется в других записях");
            sb.AppendLine("- OData не поддерживает DELETE для этого типа");
        }
        return sb.ToString();
    }

    #endregion
}
