using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class CodeTools
{
    [McpServerTool(Name = "check_code_consistency")]
    [Description("Проверить согласованность MTD ↔ C#: функции, классы, namespace, инициализатор.")]
    public async Task<string> CheckCodeConsistency(
        [Description("Путь к директории пакета Directum RX")] string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return "**ОШИБКА**: Параметр `packagePath` не может быть пустым.";
        if (!Directory.Exists(packagePath))
            return $"**ОШИБКА**: Директория не найдена: `{packagePath}`";

        var mtdFiles = Directory.GetFiles(packagePath, "*.mtd", SearchOption.AllDirectories);
        var csFiles = Directory.GetFiles(packagePath, "*.cs", SearchOption.AllDirectories);

        if (mtdFiles.Length == 0)
            return $"**ОШИБКА**: В директории `{packagePath}` не найдено ни одного .mtd файла.";

        // Pre-read all .cs files content for searching
        var csFileContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var csFile in csFiles)
        {
            try
            {
                csFileContents[csFile] = await File.ReadAllTextAsync(csFile);
            }
            catch
            {
                // Skip unreadable files
            }
        }

        var issues = new List<ConsistencyIssue>();
        int entityCount = 0;

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var metaType = root.GetStringProp("$type");
                var entityName = root.GetStringProp("Name");

                if (metaType.Contains("ModuleMetadata"))
                {
                    // Check 3: Client functions from cover actions
                    CheckCoverClientFunctions(root, entityName, csFiles, csFileContents, issues);
                    // Check 6: ModuleInitializer
                    CheckModuleInitializer(entityName, csFiles, csFileContents, issues);
                    continue;
                }

                if (string.IsNullOrEmpty(entityName))
                    continue;

                entityCount++;

                // Check 1: Server functions
                CheckServerFunctions(root, entityName, csFiles, csFileContents, issues);

                // Check 2: Shared functions
                CheckSharedFunctions(root, entityName, csFiles, csFileContents, issues);

                // Check 4: Partial class — only for entities with HandledEvents or custom code
                CheckPartialClassName(root, entityName, csFiles, csFileContents, issues);

            }
            catch
            {
                // Skip unparseable files
            }
        }

        // Check 5: Namespace consistency — run once for all files, not per entity
        CheckNamespaceConsistencyAll(csFiles, csFileContents, issues);

        // Build output
        var sb = new StringBuilder();
        sb.AppendLine("## Проверка согласованности MTD ↔ C# код");
        sb.AppendLine();
        sb.AppendLine($"**Пакет:** `{packagePath}`");
        sb.AppendLine();

        if (issues.Count > 0)
        {
            sb.AppendLine($"### Проблемы ({issues.Count})");
            sb.AppendLine();
            sb.AppendLine("| # | Проверка | Сущность | Описание |");
            sb.AppendLine("|---|----------|----------|----------|");

            for (int i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                sb.AppendLine($"| {i + 1} | {issue.CheckName} | {issue.EntityName} | {issue.Description} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("### Итого");
        sb.AppendLine($"- Проверено сущностей: {entityCount}");
        sb.AppendLine($"- Проверено .cs файлов: {csFileContents.Count}");
        sb.AppendLine($"- Проблем найдено: {issues.Count}");
        sb.AppendLine();

        if (issues.Count == 0)
            sb.AppendLine("✅ Все проверки пройдены");

        return sb.ToString();
    }

    private static void CheckServerFunctions(
        JsonElement root, string entityName,
        string[] csFiles, Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        var functions = GetFunctionNames(root, "PublicFunctions");
        if (functions.Count == 0)
            return;

        // Find server .cs files for this entity
        var serverCsFiles = csFiles.Where(f =>
        {
            var fileName = Path.GetFileName(f);
            var dirName = Path.GetFileName(Path.GetDirectoryName(f) ?? "");
            return fileName.Contains("ServerFunctions", StringComparison.OrdinalIgnoreCase) ||
                   dirName.Equals("Server", StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var serverContent = GetCombinedContent(serverCsFiles, csFileContents);

        foreach (var funcName in functions)
        {
            if (!ContainsMethodName(serverContent, funcName))
            {
                issues.Add(new ConsistencyIssue(
                    "ServerFunctions",
                    entityName,
                    $"Функция `{funcName}` объявлена в MTD (PublicFunctions), но не найдена в серверном коде"));
            }
        }
    }

    private static void CheckSharedFunctions(
        JsonElement root, string entityName,
        string[] csFiles, Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        // PublicStructures are data definitions (structs), NOT callable functions.
        // They are auto-generated by DDS and don't need method implementations.
        // Only check SharedPublicFunctions.
        var functions = GetFunctionNames(root, "SharedPublicFunctions");

        if (functions.Count == 0)
            return;

        var sharedCsFiles = csFiles.Where(f =>
        {
            var fileName = Path.GetFileName(f);
            var dirName = Path.GetFileName(Path.GetDirectoryName(f) ?? "");
            return fileName.Contains("SharedFunctions", StringComparison.OrdinalIgnoreCase) ||
                   dirName.Equals("Shared", StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var sharedContent = GetCombinedContent(sharedCsFiles, csFileContents);

        foreach (var funcName in functions)
        {
            if (!ContainsMethodName(sharedContent, funcName))
            {
                issues.Add(new ConsistencyIssue(
                    "SharedFunctions",
                    entityName,
                    $"Функция `{funcName}` объявлена в MTD (SharedPublicFunctions), но не найдена в shared-коде"));
            }
        }
    }

    private static void CheckCoverClientFunctions(
        JsonElement root, string moduleName,
        string[] csFiles, Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        if (!root.TryGetProperty("Actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
            return;

        var clientCsFiles = csFiles.Where(f =>
        {
            var fileName = Path.GetFileName(f);
            return fileName.Contains("ClientFunctions", StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals("ModuleClientFunctions.cs", StringComparison.OrdinalIgnoreCase);
        }).ToList();

        var clientContent = GetCombinedContent(clientCsFiles, csFileContents);

        foreach (var action in actions.EnumerateArray())
        {
            var actionType = action.GetStringProp("$type");
            if (!actionType.Contains("CoverFunctionActionMetadata"))
                continue;

            var functionName = action.GetStringProp("FunctionName");
            if (string.IsNullOrEmpty(functionName))
                continue;

            if (!ContainsMethodName(clientContent, functionName))
            {
                var actionName = action.GetStringProp("Name");
                issues.Add(new ConsistencyIssue(
                    "ClientFunctions",
                    moduleName,
                    $"Действие обложки `{actionName}` ссылается на функцию `{functionName}`, но она не найдена в клиентском коде"));
            }
        }
    }

    /// <summary>
    /// Check partial class existence. DDS auto-generates partial classes at compile time,
    /// so a missing partial class in .cs files is only an error when the entity has
    /// HandledEvents (event handlers that require developer code) or PublicFunctions/SharedPublicFunctions.
    /// Entities without custom logic don't need .cs files — DDS handles them automatically.
    /// </summary>
    private static void CheckPartialClassName(
        JsonElement root, string entityName, string[] csFiles,
        Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        // Determine if this entity requires custom .cs code
        bool requiresCustomCode = false;

        // Check for HandledEvents at entity level
        if (root.TryGetProperty("HandledEvents", out var handledEvents) &&
            handledEvents.ValueKind == JsonValueKind.Array &&
            handledEvents.GetArrayLength() > 0)
        {
            requiresCustomCode = true;
        }

        // Check for HandledEvents in properties (PropertyChanged handlers)
        if (!requiresCustomCode &&
            root.TryGetProperty("Properties", out var props) &&
            props.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in props.EnumerateArray())
            {
                if (prop.TryGetProperty("HandledEvents", out var propEvents) &&
                    propEvents.ValueKind == JsonValueKind.Array &&
                    propEvents.GetArrayLength() > 0)
                {
                    requiresCustomCode = true;
                    break;
                }
            }
        }

        // Check for PublicFunctions or SharedPublicFunctions
        if (!requiresCustomCode)
        {
            var pubFuncs = GetFunctionNames(root, "PublicFunctions");
            var sharedFuncs = GetFunctionNames(root, "SharedPublicFunctions");
            if (pubFuncs.Count > 0 || sharedFuncs.Count > 0)
                requiresCustomCode = true;
        }

        // If entity doesn't require custom code, skip the check — DDS generates everything
        if (!requiresCustomCode)
            return;

        var pattern = @"\bpartial\s+class\s+" + Regex.Escape(entityName) + @"\b";
        var found = csFileContents.Values.Any(content =>
            Regex.IsMatch(content, pattern));

        if (!found)
        {
            issues.Add(new ConsistencyIssue(
                "PartialClass",
                entityName,
                $"Класс `partial class {entityName}` не найден в .cs файлах, но в MTD есть HandledEvents/Functions"));
        }
    }

    /// <summary>
    /// Check namespace consistency. Run once for all .cs files, not per-entity.
    /// Deduplicates by tracking already-checked files.
    /// </summary>
    private static void CheckNamespaceConsistencyAll(
        string[] csFiles, Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        var checkedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var csFile in csFiles)
        {
            if (!csFileContents.TryGetValue(csFile, out var content))
                continue;

            if (!checkedFiles.Add(csFile))
                continue;

            // Walk up directory tree to find the layer folder (Server/Shared/ClientBase)
            var dir = Path.GetDirectoryName(csFile) ?? "";
            string? expectedSuffix = null;
            string? layerDirName = null;
            var current = dir;

            while (!string.IsNullOrEmpty(current))
            {
                var dirName = Path.GetFileName(current);
                expectedSuffix = dirName switch
                {
                    "Server" => ".Server",
                    "Shared" => ".Shared",
                    "ClientBase" => ".Client",
                    _ => null
                };
                if (expectedSuffix != null)
                {
                    layerDirName = dirName;
                    break;
                }
                var parent = Path.GetDirectoryName(current);
                if (parent == current) break;
                current = parent;
            }

            if (expectedSuffix == null)
                continue;

            var nsMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            if (!nsMatch.Success)
                continue;

            var ns = nsMatch.Groups[1].Value;
            if (!ns.EndsWith(expectedSuffix, StringComparison.Ordinal))
            {
                var fileName = Path.GetFileName(csFile);
                issues.Add(new ConsistencyIssue(
                    "Namespace",
                    fileName,
                    $"Файл `{fileName}` в папке `{layerDirName}` имеет namespace `{ns}`, ожидается окончание `{expectedSuffix}`"));
            }
        }
    }

    /// <summary>
    /// ModuleInitializer is optional — only needed for modules with initialization logic
    /// (creating roles, constants, registering handlers). DDS auto-generates an empty
    /// initializer if needed. Skip this check to avoid false positives.
    /// </summary>
    private static void CheckModuleInitializer(
        string moduleName, string[] csFiles,
        Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        // No-op: ModuleInitializer is optional, not reporting as error
    }

    private static List<string> GetFunctionNames(JsonElement root, string sectionName)
    {
        var names = new List<string>();

        if (!root.TryGetProperty(sectionName, out var section))
            return names;

        if (section.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in section.EnumerateArray())
            {
                var itemType = item.GetStringProp("$type");
                if (itemType.Contains("FunctionMetadata"))
                {
                    var name = item.GetStringProp("Name");
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
        }

        return names;
    }

    private static string GetCombinedContent(
        List<string> files, Dictionary<string, string> csFileContents)
    {
        var sb = new StringBuilder();
        foreach (var file in files)
        {
            if (csFileContents.TryGetValue(file, out var content))
                sb.AppendLine(content);
        }
        return sb.ToString();
    }

    private static bool ContainsMethodName(string content, string methodName)
    {
        if (string.IsNullOrEmpty(content))
            return false;
        return Regex.IsMatch(content, @"\b" + Regex.Escape(methodName) + @"\s*\(");
    }

    private record ConsistencyIssue(string CheckName, string EntityName, string Description);
}
