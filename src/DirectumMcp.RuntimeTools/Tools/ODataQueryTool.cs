using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class ODataQueryTool
{
    private readonly DirectumODataClient _client;

    public ODataQueryTool(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "odata_query")]
    [Description("Выполнение произвольных OData GET-запросов к Directum RX через Integration Service. Поддерживает $filter, $select, $expand, $top, $skip, $orderby и шорткаты: recent, by_id, count.")]
    public async Task<string> Query(
        [Description("Имя сущности OData (например: IDocuments, IDatabookEntries, IOfficialDocuments)")] string entity,
        [Description("OData $filter выражение (например: Name eq 'Договор' or contains(Name, 'Акт'))")] string? filter = null,
        [Description("Список полей через запятую для $select (например: Id,Name,Created)")] string? select = null,
        [Description("Навигационные свойства для $expand (например: Author,DocumentKind)")] string? expand = null,
        [Description("Максимальное количество записей (1–200)")] int top = 20,
        [Description("Количество пропускаемых записей для постраничной навигации")] int skip = 0,
        [Description("Сортировка для $orderby (например: Created desc)")] string? orderby = null,
        [Description("Режим запроса: query (обычный), recent (последние N по Id), by_id (по конкретному Id), count (только количество)")] string mode = "query",
        [Description("ID сущности для режима by_id")] long id = 0,
        [Description("Формат вывода: table (markdown-таблица до 50 строк) или json (сырой JSON)")] string format = "table")
    {
        if (string.IsNullOrWhiteSpace(entity))
            return "Ошибка: не указано имя сущности (параметр entity).";

        top = Math.Clamp(top, 1, 200);

        try
        {
            return mode.ToLowerInvariant() switch
            {
                "by_id" => await QueryById(entity, id, select, format),
                "count" => await QueryCount(entity, filter),
                "recent" => await QueryRecent(entity, top, select, expand, format),
                _ => await QueryGeneral(entity, filter, select, expand, top, skip > 0 ? skip : null, orderby, format)
            };
        }
        catch (HttpRequestException ex)
        {
            return $"**ОШИБКА**: Не удалось подключиться к стенду. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить OData-запрос. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private async Task<string> QueryById(string entity, long id, string? select, string format)
    {
        if (id <= 0)
            return "Ошибка: для режима by_id необходимо указать id > 0.";

        var result = await _client.GetByIdAsync(entity, id, select);
        return format.ToLowerInvariant() == "json"
            ? FormatJson(result)
            : FormatSingleItem(result, entity, id);
    }

    private async Task<string> QueryCount(string entity, string? filter)
    {
        // $count=true returns OData response with @odata.count field
        var url = BuildCountUrl(entity, filter);
        var result = await _client.GetRawAsync(url);

        if (result.TryGetProperty("@odata.count", out var countProp))
            return $"Количество записей в {entity}: **{countProp.GetInt64()}**";

        // Fallback: count from value array
        var items = GetItems(result);
        return $"Количество записей в {entity}: **{items.Count}**";
    }

    private async Task<string> QueryRecent(string entity, int top, string? select, string? expand, string format)
    {
        var result = await _client.GetAsync(
            entity,
            filter: null,
            select: select,
            orderby: "Id desc",
            top: top,
            expand: expand);

        return FormatResult(result, entity, format, $"Последние {top} записей {entity}");
    }

    private async Task<string> QueryGeneral(string entity, string? filter, string? select, string? expand,
        int top, int? skip, string? orderby, string format)
    {
        var result = await _client.GetAsync(
            entity,
            filter: filter,
            select: select,
            orderby: orderby,
            top: top,
            skip: skip,
            expand: expand);

        return FormatResult(result, entity, format, $"Результаты запроса {entity}");
    }

    // Internal for testability
    internal static string BuildCountUrl(string entity, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return $"{entity}?$count=true&$top=0";
        return $"{entity}?$filter={Uri.EscapeDataString(filter)}&$count=true&$top=0";
    }

    // Internal for testability
    internal static string BuildQueryUrl(string baseUrl, string entity, string? filter, string? select,
        string? expand, int top, int? skip, string? orderby)
    {
        var url = $"{baseUrl.TrimEnd('/')}/{entity}";
        var parts = new List<string>();

        if (filter is not null) parts.Add($"$filter={filter}");
        if (select is not null) parts.Add($"$select={select}");
        if (orderby is not null) parts.Add($"$orderby={orderby}");
        parts.Add($"$top={top}");
        if (skip.HasValue && skip.Value > 0) parts.Add($"$skip={skip.Value}");
        if (expand is not null) parts.Add($"$expand={expand}");

        return url + "?" + string.Join("&", parts);
    }

    private static string FormatResult(JsonElement result, string entity, string format, string header)
    {
        if (format.ToLowerInvariant() == "json")
            return FormatJson(result);

        var items = GetItems(result);
        if (items.Count == 0)
            return $"Записи не найдены в {entity}.";

        return FormatTable(items, header);
    }

    private static string FormatSingleItem(JsonElement item, string entity, long id)
    {
        if (item.ValueKind == JsonValueKind.Null || item.ValueKind == JsonValueKind.Undefined)
            return $"Запись {entity}({id}) не найдена.";

        var sb = new StringBuilder();
        sb.AppendLine($"**{entity}({id})**");
        sb.AppendLine();

        foreach (var prop in item.EnumerateObject())
        {
            var val = prop.Value.ValueKind == JsonValueKind.Null ? "-" : prop.Value.ToString();
            sb.AppendLine($"- **{prop.Name}**: {val}");
        }

        return sb.ToString();
    }

    private static string FormatTable(List<JsonElement> items, string header)
    {
        var displayItems = items.Take(50).ToList();

        // Collect all property names from first item (to build columns)
        var columns = new List<string>();
        if (displayItems.Count > 0)
        {
            foreach (var prop in displayItems[0].EnumerateObject())
            {
                // Skip complex objects and arrays — only scalar columns
                if (prop.Value.ValueKind != JsonValueKind.Object && prop.Value.ValueKind != JsonValueKind.Array)
                    columns.Add(prop.Name);
            }
        }

        if (columns.Count == 0)
            return $"{header}\n\nНет скалярных полей для отображения. Используйте format=json.";

        var sb = new StringBuilder();
        sb.AppendLine($"{header}: {displayItems.Count} из {items.Count}");
        sb.AppendLine();

        // Header row
        sb.AppendLine("| " + string.Join(" | ", columns) + " |");
        sb.AppendLine("|" + string.Join("|", columns.Select(_ => "---|")) + "");

        foreach (var item in displayItems)
        {
            var cells = columns.Select(col =>
            {
                if (!item.TryGetProperty(col, out var val))
                    return "-";
                if (val.ValueKind == JsonValueKind.Null)
                    return "-";
                var str = val.ToString();
                // Truncate long values in table
                return str.Length > 60 ? str[..57] + "..." : str;
            });
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
        }

        if (items.Count > 50)
            sb.AppendLine($"\n> Показано 50 из {items.Count}. Используйте $skip для постраничной навигации.");

        return sb.ToString();
    }

    private static string FormatJson(JsonElement result)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(result, options);
    }
}
