using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class SearchDocumentsTool
{
    private readonly DirectumODataClient _client;

    public SearchDocumentsTool(DirectumODataClient client)
    {
        _client = client;
    }

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Draft", "Active", "Obsolete"
    };

    [McpServerTool(Name = "find_docs")]
    [Description("Поиск документов в Directum RX по названию, типу, дате, статусу")]
    public async Task<string> Search(
        [Description("Текст для поиска в названии документа")] string? query = null,
        [Description("Тип документа (DocumentKind)")] string? documentType = null,
        [Description("Дата создания от (yyyy-MM-dd)")] string? dateFrom = null,
        [Description("Дата создания до (yyyy-MM-dd)")] string? dateTo = null,
        [Description("Статус жизненного цикла: Draft, Active, Obsolete")] string? status = null,
        [Description("Максимальное количество результатов")] int top = 20)
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            // Validate dateFrom format
            if (!string.IsNullOrWhiteSpace(dateFrom) &&
                !DateTime.TryParseExact(dateFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "Ошибка: параметр dateFrom должен быть в формате yyyy-MM-dd.";

            // Validate dateTo format
            if (!string.IsNullOrWhiteSpace(dateTo) &&
                !DateTime.TryParseExact(dateTo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "Ошибка: параметр dateTo должен быть в формате yyyy-MM-dd.";

            // Validate status against allowlist
            if (!string.IsNullOrWhiteSpace(status) && !AllowedStatuses.Contains(status))
                return $"Ошибка: недопустимый статус '{status}'. Допустимые значения: {string.Join(", ", AllowedStatuses)}.";

            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(query))
                filters.Add($"contains(Name, '{EscapeOData(query)}')");

            if (!string.IsNullOrWhiteSpace(documentType))
                filters.Add($"DocumentKind/Name eq '{EscapeOData(documentType)}'");

            if (!string.IsNullOrWhiteSpace(dateFrom))
                filters.Add($"Created ge {dateFrom}T00:00:00Z");

            if (!string.IsNullOrWhiteSpace(dateTo))
                filters.Add($"Created le {dateTo}T23:59:59Z");

            if (!string.IsNullOrWhiteSpace(status))
                filters.Add($"LifeCycleState eq '{EscapeOData(status)}'");

            var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;
            var select = "Id,Name,DocumentKind,Created,Modified,Author,LifeCycleState";

            var result = await _client.GetAsync(
                "IOfficialDocuments",
                filter: filter,
                select: select,
                orderby: "Modified desc",
                top: top);

            return FormatResults(result);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить поиск документов. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static string FormatResults(JsonElement result)
    {
        var items = GetItems(result);
        if (items.Count == 0)
            return "Документы не найдены.";

        var sb = new StringBuilder();
        sb.AppendLine($"Найдено документов: {items.Count}");
        sb.AppendLine();
        sb.AppendLine("| ID | Название | Вид | Создан | Изменён | Автор | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = GetString(item, "Name");
            var kind = GetNestedString(item, "DocumentKind", "Name");
            var created = FormatDate(GetString(item, "Created"));
            var modified = FormatDate(GetString(item, "Modified"));
            var author = GetNestedString(item, "Author", "Name");
            var state = GetString(item, "LifeCycleState");

            sb.AppendLine($"| {id} | {name} | {kind} | {created} | {modified} | {author} | {state} |");
        }

        return sb.ToString();
    }
}
