using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class QueryTools
{
    private readonly DirectumODataClient _client;

    public QueryTools(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "odata_query")]
    [Description(
        "OData GET-запрос к Directum RX Integration Service. " +
        "Поддерживает $filter, $select, $expand, $top, $skip, $orderby. " +
        "Режимы: query (обычный), recent (последние N), by_id, count. " +
        "Для поиска сущностей по имени используй search_metadata.")]
    public async Task<string> Query(
        [Description("Имя OData entity (IDocuments, IDatabookEntries)")] string entity,
        [Description("$filter (Name eq 'Договор')")] string? filter = null,
        [Description("$select поля (Id,Name,Created)")] string? select = null,
        [Description("$expand (Author,DocumentKind)")] string? expand = null,
        [Description("Макс записей (1-200)")] int top = 20,
        [Description("Пропустить записей")] int skip = 0,
        [Description("$orderby (Created desc)")] string? orderby = null,
        [Description("Режим: query, recent, by_id, count")] string mode = "query",
        [Description("ID для by_id")] long id = 0,
        [Description("Формат: table или json")] string format = "table")
    {
        if (string.IsNullOrWhiteSpace(entity))
            return "Ошибка: не указано имя сущности.";

        top = Math.Clamp(top, 1, 200);

        try
        {
            return mode.ToLowerInvariant() switch
            {
                "by_id" when id > 0 => await QueryById(entity, id, select, format),
                "count" => await QueryCount(entity, filter),
                "recent" => await QueryRecent(entity, top, select, expand, format),
                _ => await QueryGeneral(entity, filter, select, expand, top, skip > 0 ? skip : null, orderby, format)
            };
        }
        catch (HttpRequestException ex)
        {
            return $"**HTTP ERROR**: {ex.StatusCode} — {ex.Message}";
        }
    }

    private async Task<string> QueryById(string entity, long id, string? select, string format)
    {
        var result = await _client.GetByIdAsync(entity, id, select);
        return format == "json" ? result.ToString() : FormatSingleEntity(result, entity);
    }

    private async Task<string> QueryCount(string entity, string? filter)
    {
        var url = BuildODataUrl(entity, filter: filter, top: 0, count: true);
        var result = await _client.GetRawAsync(url);
        var count = result.TryGetProperty("@odata.count", out var c) ? c.GetInt64() : -1;
        return $"**{entity}**: {count} записей{(filter != null ? $" (фильтр: {filter})" : "")}";
    }

    private async Task<string> QueryRecent(string entity, int top, string? select, string? expand, string format)
    {
        var url = BuildODataUrl(entity, select: select, expand: expand, top: top, orderby: "Id desc");
        var result = await _client.GetRawAsync(url);
        return format == "json" ? result.ToString() : FormatResultTable(result, entity);
    }

    private async Task<string> QueryGeneral(string entity, string? filter, string? select, string? expand,
        int top, int? skip, string? orderby, string format)
    {
        var url = BuildODataUrl(entity, filter, select, expand, top, skip, orderby);
        var result = await _client.GetRawAsync(url);
        return format == "json" ? result.ToString() : FormatResultTable(result, entity);
    }

    private static string BuildODataUrl(string entity, string? filter = null, string? select = null,
        string? expand = null, int? top = null, int? skip = null, string? orderby = null, bool count = false)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(filter)) parts.Add($"$filter={Uri.EscapeDataString(filter)}");
        if (!string.IsNullOrEmpty(select)) parts.Add($"$select={select}");
        if (!string.IsNullOrEmpty(expand)) parts.Add($"$expand={expand}");
        if (top.HasValue) parts.Add($"$top={top}");
        if (skip.HasValue && skip > 0) parts.Add($"$skip={skip}");
        if (!string.IsNullOrEmpty(orderby)) parts.Add($"$orderby={Uri.EscapeDataString(orderby)}");
        if (count) parts.Add("$count=true");
        return parts.Count > 0 ? $"{entity}?{string.Join("&", parts)}" : entity;
    }

    private static string FormatSingleEntity(JsonElement element, string entity)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {entity}");
        sb.AppendLine();
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.Name.StartsWith("@odata")) continue;
            sb.AppendLine($"- **{prop.Name}**: {prop.Value}");
        }
        return sb.ToString();
    }

    private static string FormatResultTable(JsonElement result, string entity)
    {
        var sb = new StringBuilder();
        if (!result.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
            return "Нет результатов.";

        var rows = items.EnumerateArray().ToList();
        if (rows.Count == 0)
            return "Нет результатов.";

        var columns = rows[0].EnumerateObject()
            .Where(p => !p.Name.StartsWith("@odata"))
            .Select(p => p.Name)
            .Take(8)
            .ToList();

        sb.AppendLine($"## {entity} ({rows.Count} записей)");
        sb.AppendLine();
        sb.AppendLine("| " + string.Join(" | ", columns) + " |");
        sb.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");

        foreach (var row in rows.Take(50))
        {
            var values = columns.Select(c =>
            {
                if (row.TryGetProperty(c, out var v))
                {
                    var s = v.ToString();
                    return s.Length > 60 ? s[..57] + "..." : s;
                }
                return "—";
            });
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        if (rows.Count > 50)
            sb.AppendLine($"\n_Показано 50 из {rows.Count}. Используй $top и $skip._");

        return sb.ToString();
    }
}
