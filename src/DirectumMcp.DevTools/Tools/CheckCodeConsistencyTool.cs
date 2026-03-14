using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class CheckCodeConsistencyTool
{
    [McpServerTool(Name = "check_code_consistency")]
    [Description("Проверка согласованности между MTD-метаданными и C#-кодом в пакете Directum RX: функции, классы, пространства имён, инициализатор модуля.")]
    public async Task<string> CheckCodeConsistency(
        [Description("Путь к директории пакета Directum RX")] string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return "**ОШИБКА**: Параметр `packagePath` не может быть пустым.";

        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

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

                var metaType = GetString(root, "$type");
                var entityName = GetString(root, "Name");

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

                // Check 4: Partial class name
                CheckPartialClassName(entityName, csFiles, csFileContents, issues);

                // Check 5: Namespace consistency
                CheckNamespaceConsistency(csFiles, csFileContents, issues, entityName);
            }
            catch
            {
                // Skip unparseable files
            }
        }

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
        var functions = GetFunctionNames(root, "PublicStructures");
        // Also check for shared functions in a separate section if exists
        var sharedFunctions = GetFunctionNames(root, "SharedPublicFunctions");
        functions.AddRange(sharedFunctions);

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
            var actionType = GetString(action, "$type");
            if (!actionType.Contains("CoverFunctionActionMetadata"))
                continue;

            var functionName = GetString(action, "FunctionName");
            if (string.IsNullOrEmpty(functionName))
                continue;

            if (!ContainsMethodName(clientContent, functionName))
            {
                var actionName = GetString(action, "Name");
                issues.Add(new ConsistencyIssue(
                    "ClientFunctions",
                    moduleName,
                    $"Действие обложки `{actionName}` ссылается на функцию `{functionName}`, но она не найдена в клиентском коде"));
            }
        }
    }

    private static void CheckPartialClassName(
        string entityName, string[] csFiles,
        Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        var pattern = @"\bpartial\s+class\s+" + Regex.Escape(entityName) + @"\b";
        var found = csFileContents.Values.Any(content =>
            Regex.IsMatch(content, pattern));

        if (!found)
        {
            issues.Add(new ConsistencyIssue(
                "PartialClass",
                entityName,
                $"Класс `partial class {entityName}` не найден в .cs файлах"));
        }
    }

    private static void CheckNamespaceConsistency(
        string[] csFiles, Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues, string entityName)
    {
        foreach (var csFile in csFiles)
        {
            if (!csFileContents.TryGetValue(csFile, out var content))
                continue;

            var dirName = Path.GetFileName(Path.GetDirectoryName(csFile) ?? "");

            string? expectedSuffix = dirName switch
            {
                "Server" => ".Server",
                "Shared" => ".Shared",
                "ClientBase" => ".Client",
                _ => null
            };

            if (expectedSuffix == null)
                continue;

            var nsMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            if (!nsMatch.Success)
                continue;

            var ns = nsMatch.Groups[1].Value;
            if (!ns.EndsWith(expectedSuffix, StringComparison.Ordinal))
            {
                issues.Add(new ConsistencyIssue(
                    "Namespace",
                    entityName,
                    $"Файл `{Path.GetFileName(csFile)}` в папке `{dirName}` имеет namespace `{ns}`, ожидается окончание `{expectedSuffix}`"));
            }
        }
    }

    private static void CheckModuleInitializer(
        string moduleName, string[] csFiles,
        Dictionary<string, string> csFileContents,
        List<ConsistencyIssue> issues)
    {
        var hasInitializer = csFiles.Any(f =>
            Path.GetFileName(f).Equals("ModuleInitializer.cs", StringComparison.OrdinalIgnoreCase));

        if (!hasInitializer)
        {
            // Also check if any .cs file contains a class inheriting from ModuleInitializer
            var found = csFileContents.Values.Any(content =>
                Regex.IsMatch(content, @":\s*ModuleInitializer\b"));

            if (!found)
            {
                issues.Add(new ConsistencyIssue(
                    "ModuleInitializer",
                    moduleName,
                    "Файл `ModuleInitializer.cs` не найден и нет класса, наследующего `ModuleInitializer`"));
            }
        }
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
                var itemType = GetString(item, "$type");
                if (itemType.Contains("FunctionMetadata"))
                {
                    var name = GetString(item, "Name");
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

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private record ConsistencyIssue(string CheckName, string EntityName, string Description);
}
