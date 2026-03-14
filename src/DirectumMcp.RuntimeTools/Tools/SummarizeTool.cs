using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class SummarizeTool
{
    private static readonly Regex SafeQueryPattern = new(@"^[\p{L}\p{N}\s\-_.]+$");

    private readonly DirectumODataClient _client;

    public SummarizeTool(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "summarize")]
    [Description("Краткое содержание документа Directum RX — метаданные, статус, история согласования")]
    public async Task<string> Summarize(
        [Description("ID документа")] long? documentId = null,
        [Description("Поисковый запрос (если ID неизвестен)")] string? query = null)
    {
        try
        {
            if (documentId is null && string.IsNullOrWhiteSpace(query))
                return "Ошибка: укажите documentId или query для поиска документа.";

            if (query != null && !SafeQueryPattern.IsMatch(query))
                return "Ошибка: поисковый запрос содержит недопустимые символы. Используйте буквы, цифры и пробелы.";

            long id;
            JsonElement doc;

            if (documentId.HasValue)
            {
                id = documentId.Value;
                doc = await _client.GetByIdAsync("IOfficialDocuments", id,
                    select: "Id,Name,Created,Modified,LifeCycleState,Subject,Note");
            }
            else
            {
                var searchResult = await _client.GetAsync("IOfficialDocuments",
                    filter: $"contains(Name,'{EscapeOData(query!)}')",
                    select: "Id,Name",
                    top: 1,
                    orderby: "Modified desc");

                var found = GetItems(searchResult);
                if (found.Count == 0)
                    return $"Документ не найден по запросу '{query}'.";

                id = GetLong(found[0], "Id");
                doc = await _client.GetByIdAsync("IOfficialDocuments", id,
                    select: "Id,Name,Created,Modified,LifeCycleState,Subject,Note");
            }

            var name = GetString(doc, "Name");
            var created = FormatDate(GetString(doc, "Created"));
            var modified = FormatDate(GetString(doc, "Modified"));
            var state = GetString(doc, "LifeCycleState");
            var subject = GetString(doc, "Subject");
            var note = GetString(doc, "Note");

            // Try to get ActiveText
            string activeText = "";
            try
            {
                var textData = await _client.GetAsync("IOfficialDocuments",
                    filter: $"Id eq {id}", select: "ActiveText", top: 1);
                var items = GetItems(textData);
                if (items.Count > 0)
                    activeText = GetString(items[0], "ActiveText");
            }
            catch { /* ActiveText может быть недоступен */ }

            // Try to get tracking history
            var trackingEntries = new List<(string date, string action, string author)>();
            try
            {
                var tracking = await _client.GetRawAsync(
                    $"IOfficialDocuments({id})/Tracking?$select=Action,Author,Date&$orderby=Date desc&$top=10");
                var trackingItems = GetItems(tracking);
                foreach (var entry in trackingItems)
                {
                    var date = FormatDate(GetString(entry, "Date"), "dd.MM.yyyy HH:mm");
                    var action = GetString(entry, "Action");
                    var author = GetNestedString(entry, "Author", "Name");
                    trackingEntries.Add((date, action, author));
                }
            }
            catch { /* Tracking может быть недоступен */ }

            // Format markdown
            var sb = new StringBuilder();
            sb.AppendLine($"## Документ: {name}");
            sb.AppendLine();
            sb.AppendLine("| Поле | Значение |");
            sb.AppendLine("|------|----------|");
            sb.AppendLine($"| ID | {id} |");
            sb.AppendLine($"| Создан | {created} |");
            sb.AppendLine($"| Изменён | {modified} |");
            sb.AppendLine($"| Статус | {state} |");
            sb.AppendLine($"| Тема | {(subject == "-" ? "\u2014" : subject)} |");
            sb.AppendLine($"| Примечание | {(note == "-" ? "\u2014" : note)} |");
            sb.AppendLine();

            sb.AppendLine("### Содержание");
            if (!string.IsNullOrWhiteSpace(activeText) && activeText != "-")
            {
                var text = activeText.Length > 2000 ? activeText[..2000] + "..." : activeText;
                sb.AppendLine(text);
            }
            else
            {
                sb.AppendLine("Текст документа недоступен через API.");
            }
            sb.AppendLine();

            if (trackingEntries.Count > 0)
            {
                sb.AppendLine($"### История ({trackingEntries.Count} записей)");
                sb.AppendLine("| Дата | Действие | Автор |");
                sb.AppendLine("|------|----------|-------|");
                foreach (var (date, action, author) in trackingEntries)
                {
                    sb.AppendLine($"| {date} | {action} | {author} |");
                }
            }
            else
            {
                sb.AppendLine("### История");
                sb.AppendLine("История недоступна через API.");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить информацию о документе. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }
}
