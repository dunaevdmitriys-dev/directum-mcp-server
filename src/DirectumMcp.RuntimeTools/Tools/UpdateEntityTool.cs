using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class UpdateEntityTool
{
    private readonly DirectumODataClient _client;
    public UpdateEntityTool(DirectumODataClient client) => _client = client;

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
            // Parse properties
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

            // 1. Get current state
            var currentJson = await _client.GetAsync(entityType, $"Id eq {entityId}",
                string.Join(",", properties.Keys.Append("Id").Append("Name").Distinct()));

            if (!currentJson.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
                return $"Сущность {entityType}({entityId}) не найдена.";

            var current = values[0];
            var name = current.TryGetProperty("Name", out var n) ? n.GetString() ?? "" :
                       current.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : $"#{entityId}";

            // 2. Show diff
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

            // 3. Execute PATCH
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
}
