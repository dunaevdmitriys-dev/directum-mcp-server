using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class FindDeadResourcesTool
{
    private static readonly HashSet<string> StandaloneValidKeys = new(StringComparer.Ordinal)
    {
        "DisplayName",
        "CollectionDisplayName",
        "AccusativeDisplayName",
        "AdditionalInfoTemplate",
        "Description",
    };

    private static bool IsPathAllowed(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Environment.GetEnvironmentVariable("SOLUTION_PATH") ?? "",
            Path.GetTempPath()
        };
        return allowedPaths.Any(bp => !string.IsNullOrEmpty(bp) &&
            fullPath.StartsWith(Path.GetFullPath(bp), StringComparison.OrdinalIgnoreCase));
    }

    [McpServerTool(Name = "find_dead_resources")]
    [Description("Поиск «мёртвых» ресурсов в модуле Directum RX: ключи System.resx без соответствующего свойства/действия в MTD, " +
                 "свойства и действия MTD без перевода в System.resx, а также ResourcesKeys в MTD без ключей в Entity.resx.")]
    public async Task<string> FindDeadResources(
        [Description("Путь к директории модуля")] string modulePath)
    {
        if (!IsPathAllowed(modulePath))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{modulePath}` находится за пределами разрешённых директорий.";

        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

        var allMtdFiles = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (allMtdFiles.Length == 0)
            return $"**Результат**: В директории `{modulePath}` не найдено файлов сущностей (.mtd).";

        var entityResults = new List<EntityAnalysis>();

        foreach (var mtdFile in allMtdFiles)
        {
            var analysis = await AnalyzeEntity(mtdFile);
            if (analysis != null)
                entityResults.Add(analysis);
        }

        return FormatReport(modulePath, entityResults);
    }

    private static async Task<EntityAnalysis?> AnalyzeEntity(string mtdFile)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(mtdFile);
        }
        catch
        {
            return null;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;

            var metaType = GetString(root, "$type");
            if (metaType.Contains("ModuleMetadata"))
                return null;

            var entityName = GetString(root, "Name");
            if (string.IsNullOrEmpty(entityName))
                return null;

            var mtdDir = Path.GetDirectoryName(mtdFile) ?? "";

            // Collect property names (non-inherited)
            var mtdProperties = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props.EnumerateArray())
                {
                    var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var anc) &&
                                     anc.ValueKind == JsonValueKind.True;
                    if (isAncestor)
                        continue;
                    var name = GetString(prop, "Name");
                    if (!string.IsNullOrEmpty(name))
                        mtdProperties.Add(name);
                }
            }

            // Collect action names (non-inherited)
            var mtdActions = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
            {
                foreach (var action in actions.EnumerateArray())
                {
                    var isAncestor = action.TryGetProperty("IsAncestorMetadata", out var anc) &&
                                     anc.ValueKind == JsonValueKind.True;
                    if (isAncestor)
                        continue;
                    var name = GetString(action, "Name");
                    if (!string.IsNullOrEmpty(name))
                        mtdActions.Add(name);
                }
            }

            // Collect ResourcesKeys
            var resourcesKeys = new HashSet<string>(StringComparer.Ordinal);
            if (root.TryGetProperty("ResourcesKeys", out var resKeys) && resKeys.ValueKind == JsonValueKind.Array)
            {
                foreach (var key in resKeys.EnumerateArray())
                {
                    var k = key.ValueKind == JsonValueKind.String ? key.GetString() : GetString(key, "Key");
                    if (!string.IsNullOrEmpty(k))
                        resourcesKeys.Add(k);
                }
            }

            var issues = new List<ResourceIssue>();

            // Cross-reference with System.resx files
            await AnalyzeSystemResx(mtdDir, entityName, mtdProperties, mtdActions, issues);

            // Cross-reference with Entity.resx files
            await AnalyzeEntityResx(mtdDir, entityName, resourcesKeys, issues);

            return new EntityAnalysis(entityName, mtdFile, issues);
        }
    }

    private static async Task AnalyzeSystemResx(
        string mtdDir,
        string entityName,
        HashSet<string> mtdProperties,
        HashSet<string> mtdActions,
        List<ResourceIssue> issues)
    {
        var systemResxFiles = FindResxFiles(mtdDir, entityName, isSystem: true);

        if (systemResxFiles.Count == 0)
        {
            if (mtdProperties.Count > 0 || mtdActions.Count > 0)
            {
                foreach (var prop in mtdProperties)
                    issues.Add(new ResourceIssue(IssueKind.MissingTranslation, $"Property_{prop}", $"*System.resx не найден"));
                foreach (var action in mtdActions)
                    issues.Add(new ResourceIssue(IssueKind.MissingTranslation, $"Action_{action}", $"*System.resx не найден"));
            }
            return;
        }

        // Use the first found System.resx for analysis (primary locale, not .ru.resx)
        var primaryFile = systemResxFiles.FirstOrDefault(f =>
            !Path.GetFileName(f).EndsWith(".ru.resx", StringComparison.OrdinalIgnoreCase))
            ?? systemResxFiles[0];

        var resxKeys = await ReadResxKeys(primaryFile);

        // Dead keys in System.resx: Property_X with no MTD property, Action_X with no MTD action
        foreach (var key in resxKeys)
        {
            if (key.StartsWith("Property_", StringComparison.Ordinal))
            {
                var propName = key["Property_".Length..];
                if (!mtdProperties.Contains(propName))
                    issues.Add(new ResourceIssue(IssueKind.DeadKey, key,
                        "ключ Property_ без соответствующего свойства в MTD"));
            }
            else if (key.StartsWith("Action_", StringComparison.Ordinal))
            {
                var actionName = key["Action_".Length..];
                if (!mtdActions.Contains(actionName))
                    issues.Add(new ResourceIssue(IssueKind.DeadKey, key,
                        "ключ Action_ без соответствующего действия в MTD"));
            }
        }

        // Missing translations: MTD property/action with no key in System.resx
        foreach (var prop in mtdProperties)
        {
            if (!resxKeys.Contains($"Property_{prop}"))
                issues.Add(new ResourceIssue(IssueKind.MissingTranslation, $"Property_{prop}",
                    "свойство MTD без ключа в System.resx"));
        }

        foreach (var action in mtdActions)
        {
            if (!resxKeys.Contains($"Action_{action}"))
                issues.Add(new ResourceIssue(IssueKind.MissingTranslation, $"Action_{action}",
                    "действие MTD без ключа в System.resx"));
        }
    }

    private static async Task AnalyzeEntityResx(
        string mtdDir,
        string entityName,
        HashSet<string> resourcesKeys,
        List<ResourceIssue> issues)
    {
        if (resourcesKeys.Count == 0)
            return;

        var entityResxFiles = FindResxFiles(mtdDir, entityName, isSystem: false);

        if (entityResxFiles.Count == 0)
        {
            foreach (var key in resourcesKeys)
                issues.Add(new ResourceIssue(IssueKind.MissingResource, key, "Entity.resx не найден"));
            return;
        }

        var primaryFile = entityResxFiles.FirstOrDefault(f =>
            !Path.GetFileName(f).EndsWith(".ru.resx", StringComparison.OrdinalIgnoreCase))
            ?? entityResxFiles[0];

        var resxKeys = await ReadResxKeys(primaryFile);

        foreach (var key in resourcesKeys)
        {
            if (!resxKeys.Contains(key))
                issues.Add(new ResourceIssue(IssueKind.MissingResource, key,
                    "ResourcesKeys в MTD без соответствующего ключа в Entity.resx"));
        }
    }

    private static List<string> FindResxFiles(string directory, string entityName, bool isSystem)
    {
        if (!Directory.Exists(directory))
            return [];

        var allResx = Directory.GetFiles(directory, "*.resx", SearchOption.AllDirectories);
        var results = new List<string>();

        foreach (var file in allResx)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            // Normalize: strip .ru suffix for matching
            if (fileName.EndsWith(".ru", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^3];

            if (isSystem)
            {
                // *System.resx: e.g. ContractSystem.resx, ContractSystem.ru.resx
                if (fileName.Equals(entityName + "System", StringComparison.OrdinalIgnoreCase))
                    results.Add(file);
            }
            else
            {
                // Entity.resx: e.g. Contract.resx, Contract.ru.resx — but NOT ContractSystem
                if (fileName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                    results.Add(file);
            }
        }

        return results;
    }

    private static async Task<HashSet<string>> ReadResxKeys(string resxFile)
    {
        try
        {
            var xml = await File.ReadAllTextAsync(resxFile);
            var xdoc = XDocument.Parse(xml);
            return new HashSet<string>(
                xdoc.Descendants("data")
                    .Select(d => d.Attribute("name")?.Value ?? "")
                    .Where(k => !string.IsNullOrEmpty(k)),
                StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private static string FormatReport(string modulePath, List<EntityAnalysis> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ мёртвых ресурсов");
        sb.AppendLine();
        sb.AppendLine($"**Модуль**: `{modulePath}`");
        sb.AppendLine($"**Сущностей проанализировано**: {entities.Count}");
        sb.AppendLine();

        var allIssues = entities.SelectMany(e => e.Issues).ToList();
        var deadCount = allIssues.Count(i => i.Kind == IssueKind.DeadKey);
        var missingTranslationCount = allIssues.Count(i => i.Kind == IssueKind.MissingTranslation);
        var missingResourceCount = allIssues.Count(i => i.Kind == IssueKind.MissingResource);

        if (allIssues.Count == 0)
        {
            sb.AppendLine("**Результат**: Мёртвых ресурсов и отсутствующих переводов не обнаружено.");
            return sb.ToString();
        }

        sb.AppendLine($"**Мёртвых ключей (dead)**: {deadCount}");
        sb.AppendLine($"**Отсутствующих переводов (missing translation)**: {missingTranslationCount}");
        sb.AppendLine($"**Отсутствующих ресурсов (missing resource)**: {missingResourceCount}");
        sb.AppendLine();

        foreach (var entity in entities.Where(e => e.Issues.Count > 0).OrderBy(e => e.Name))
        {
            sb.AppendLine($"## {entity.Name}");
            sb.AppendLine($"MTD: `{entity.MtdPath}`");
            sb.AppendLine();

            sb.AppendLine("| Тип проблемы | Ключ | Описание | Рекомендация |");
            sb.AppendLine("|-------------|------|----------|-------------|");

            foreach (var issue in entity.Issues.OrderBy(i => i.Kind).ThenBy(i => i.Key))
            {
                var (typeLabel, recommendation) = issue.Kind switch
                {
                    IssueKind.DeadKey => ("dead", "Удалите ключ из System.resx или добавьте соответствующее свойство/действие в MTD"),
                    IssueKind.MissingTranslation => ("missing translation", "Добавьте ключ в *System.resx (и *System.ru.resx)"),
                    IssueKind.MissingResource => ("missing resource", "Добавьте ключ в Entity.resx (и Entity.ru.resx)"),
                    _ => ("unknown", "")
                };

                var escapedKey = issue.Key.Replace("|", "\\|");
                var escapedDesc = issue.Description.Replace("|", "\\|");
                sb.AppendLine($"| {typeLabel} | `{escapedKey}` | {escapedDesc} | {recommendation} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private enum IssueKind { DeadKey, MissingTranslation, MissingResource }

    private record ResourceIssue(IssueKind Kind, string Key, string Description);

    private record EntityAnalysis(string Name, string MtdPath, List<ResourceIssue> Issues);
}
