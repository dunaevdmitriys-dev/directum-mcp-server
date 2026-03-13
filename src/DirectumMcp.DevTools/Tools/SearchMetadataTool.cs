using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SearchMetadataTool
{
    private static readonly Dictionary<string, string> FilterTypeGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabookEntry"] = "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
        ["Document"]      = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
        ["Task"]          = "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
        ["Assignment"]    = "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24",
        ["Notice"]        = "4e09273f-8b3a-489e-814e-a4ebfbba3e6c",
    };

    private static readonly Dictionary<string, string> KnownBaseGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["04581d26-0780-4cfd-b3cd-c2cafc5798b0"] = "DatabookEntry",
        ["58cca102-1e97-4f07-b6ac-fd866a8b7cb1"] = "Document",
        ["d795d1f6-45c1-4e5e-9677-b53fb7280c7e"] = "Task",
        ["91cbfdc8-5d5d-465e-95a4-3a987e1a0c24"] = "Assignment",
        ["4e09273f-8b3a-489e-814e-a4ebfbba3e6c"] = "Notice",
    };

    private const int MaxResults = 50;

    private static bool IsPathAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Path.GetFullPath(solutionPath),
            Path.GetFullPath(Path.GetTempPath())
        };
        return allowedPaths.Any(bp =>
            bp.Length >= 4 &&
            fullPath.StartsWith(bp, StringComparison.OrdinalIgnoreCase));
    }

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

        if (!IsPathAllowed(solutionPath))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{solutionPath}` находится за пределами разрешённых директорий.";

        if (!Directory.Exists(solutionPath))
            return $"**ОШИБКА**: Директория не найдена: `{solutionPath}`";

        if (string.IsNullOrWhiteSpace(query))
            return "**ОШИБКА**: Параметр `query` не может быть пустым.";

        string? filterBaseGuid = null;
        if (!string.IsNullOrEmpty(filterType))
        {
            if (!FilterTypeGuids.TryGetValue(filterType, out filterBaseGuid) &&
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

                var metaType = GetString(root, "$type");
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

        var name = GetString(root, "Name");
        var nameGuid = GetString(root, "NameGuid");
        var baseGuid = GetString(root, "BaseGuid");
        var metaType = GetString(root, "$type");

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
                _ => KnownBaseGuids.TryGetValue(baseGuid, out var k) ? k : "Entity"
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
                var propName = GetString(prop, "Name");
                var entityGuid = GetString(prop, "EntityGuid");
                var propCode = GetString(prop, "Code");

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
                var actionName = GetString(action, "Name");
                if (actionName.Contains(q, StringComparison.OrdinalIgnoreCase))
                    AddResult($"Action.Name: {actionName}");
            }
        }

        return results;
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private record SearchResult(string Name, string Kind, string MatchedField, string RelativePath);
}
