using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SearchMetadataTool
{
    private const int MaxResults = 50;

    [McpServerTool(Name = "search_metadata")]
    [Description("Поиск по всем MTD-файлам репозитория Directum RX: поиск сущностей по имени, GUID, типу свойства, ссылке EntityGuid и т.д.")]
    public async Task<string> SearchMetadata(
        [Description("Строка поиска: имя сущности, GUID, имя свойства или частичное совпадение")] string query,
        [Description("Область поиска: 'entities' — только сущности, 'modules' — только модули, 'all' — всё (по умолчанию)")] string scope = "all",
        [Description("Фильтр по базовому типу: DatabookEntry, Document, Task, Assignment, Notice, Report")] string? filterType = null)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return "**ОШИБКА**: Переменная среды `SOLUTION_PATH` не задана.";

        if (!PathGuard.IsAllowed(solutionPath))
            return PathGuard.DenyMessage(solutionPath);

        if (!Directory.Exists(solutionPath))
            return $"**ОШИБКА**: Директория не найдена: `{solutionPath}`";

        if (string.IsNullOrWhiteSpace(query))
            return "**ОШИБКА**: Параметр `query` не может быть пустым.";

        string? filterBaseGuid = null;
        if (!string.IsNullOrEmpty(filterType))
        {
            if (!DirectumConstants.BaseTypeToGuid.TryGetValue(filterType, out filterBaseGuid) &&
                !string.Equals(filterType, "Report", StringComparison.OrdinalIgnoreCase))
                return $"**ОШИБКА**: Неизвестный filterType `{filterType}`. Допустимые: DatabookEntry, Document, Task, Assignment, Notice, Report.";
        }

        var normalizedScope = scope.ToLowerInvariant();
        if (normalizedScope is not ("all" or "entities" or "modules"))
            return $"**ОШИБКА**: Неизвестный scope `{scope}`. Допустимые: all, entities, modules.";

        var mtdFiles = Directory.GetFiles(solutionPath, "*.mtd", SearchOption.AllDirectories);

        var results = new List<SearchResult>();
        int totalMatches = 0;

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var metaType = root.GetStringProp("$type");
                bool isModule = metaType.Contains("ModuleMetadata");

                if (normalizedScope == "entities" && isModule) continue;
                if (normalizedScope == "modules" && !isModule) continue;

                var fileMatches = SearchInDocument(root, query, mtdFile, solutionPath, isModule, filterBaseGuid, filterType);
                totalMatches += fileMatches.Count;

                if (results.Count < MaxResults)
                {
                    var remaining = MaxResults - results.Count;
                    results.AddRange(fileMatches.Take(remaining));
                }
            }
            catch
            {
                // Skip unparseable files
            }
        }

        if (results.Count == 0)
            return $"По запросу **\"{query}\"** ничего не найдено.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Результаты поиска: \"{query}\"");
        sb.AppendLine();
        sb.AppendLine($"Найдено совпадений: **{totalMatches}**{(totalMatches > MaxResults ? $" (показано первые {MaxResults})" : "")}");
        sb.AppendLine();
        sb.AppendLine("| Имя | Тип | Совпадение | Путь |");
        sb.AppendLine("|-----|-----|------------|------|");

        foreach (var r in results)
            sb.AppendLine($"| {r.Name} | {r.Kind} | {r.MatchedField} | `{r.RelativePath}` |");

        sb.AppendLine();
        return sb.ToString();
    }

    private static List<SearchResult> SearchInDocument(
        JsonElement root,
        string query,
        string filePath,
        string solutionPath,
        bool isModule,
        string? filterBaseGuid,
        string? filterType)
    {
        var results = new List<SearchResult>();
        var q = query.ToLowerInvariant();
        var relativePath = Path.GetRelativePath(solutionPath, filePath);

        var name = root.GetStringProp("Name");
        var nameGuid = root.GetStringProp("NameGuid");
        var baseGuid = root.GetStringProp("BaseGuid");
        var metaType = root.GetStringProp("$type");

        string kind;
        if (isModule)
        {
            kind = "Module";
        }
        else
        {
            kind = metaType switch
            {
                var t when t.Contains("TaskMetadata")       => "Task",
                var t when t.Contains("AssignmentMetadata") => "Assignment",
                var t when t.Contains("NoticeMetadata")     => "Notice",
                var t when t.Contains("ReportMetadata")     => "Report",
                _ => DirectumConstants.KnownBaseGuids.TryGetValue(baseGuid, out var k) ? k : "Entity"
            };
        }

        // Apply filterType constraint
        if (!string.IsNullOrEmpty(filterType))
        {
            if (string.Equals(filterType, "Report", StringComparison.OrdinalIgnoreCase))
            {
                if (!metaType.Contains("ReportMetadata")) return results;
            }
            else if (!string.IsNullOrEmpty(filterBaseGuid))
            {
                if (!string.Equals(baseGuid, filterBaseGuid, StringComparison.OrdinalIgnoreCase)) return results;
            }
        }

        void AddResult(string matchedField) =>
            results.Add(new SearchResult(name, kind, matchedField, relativePath));

        if (name.Contains(q, StringComparison.OrdinalIgnoreCase))
            AddResult("Name");

        if (!string.IsNullOrEmpty(nameGuid) && nameGuid.Contains(q, StringComparison.OrdinalIgnoreCase))
            AddResult("NameGuid");

        if (!string.IsNullOrEmpty(baseGuid) && baseGuid.Contains(q, StringComparison.OrdinalIgnoreCase))
            AddResult("BaseGuid");

        // Search in Properties
        if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in props.EnumerateArray())
            {
                var propName = prop.GetStringProp("Name");
                var entityGuid = prop.GetStringProp("EntityGuid");
                var propCode = prop.GetStringProp("Code");

                if (propName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    AddResult($"Property.Name: {propName}");

                if (!string.IsNullOrEmpty(entityGuid) && entityGuid.Contains(q, StringComparison.OrdinalIgnoreCase))
                    AddResult($"Property.EntityGuid ({propName}): {entityGuid}");

                if (!string.IsNullOrEmpty(propCode) && propCode.Contains(q, StringComparison.OrdinalIgnoreCase))
                    AddResult($"Property.Code ({propName}): {propCode}");
            }
        }

        // Search in Actions
        if (root.TryGetProperty("Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actions.EnumerateArray())
            {
                var actionName = action.GetStringProp("Name");
                if (actionName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    AddResult($"Action.Name: {actionName}");
            }
        }

        return results;
    }

    private record SearchResult(string Name, string Kind, string MatchedField, string RelativePath);
}
