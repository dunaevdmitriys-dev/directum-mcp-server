using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class ResourceTools
{
    // Known valid key prefixes in System.resx files
    private static readonly string[] ValidPrefixes =
    [
        "Property_",
        "Action_",
        "Enum_",
        "ControlGroup_",
        "Form_",
        "Ribbon_",
        "FilterPanel_",
        // Module-level cover keys (CoverGroup, CoverAction, CoverTab, CoverTitle)
        "CoverGroup_",
        "CoverAction_",
        "CoverTab_",
        "CoverFunction_",
        // Module-level keys
        "Widget_",
        "Job_",
        "AsyncHandler_",
    ];

    // Keys that are valid without any prefix
    private static readonly HashSet<string> ValidStandaloneKeys = new(StringComparer.Ordinal)
    {
        "DisplayName",
        "CollectionDisplayName",
        "AccusativeDisplayName",
        "AdditionalInfoTemplate",
        "Description",
        "CoverTitle",
    };

    private static readonly Regex GuidPattern = new(
        @"^Resource_[0-9a-fA-F]{8}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    private static readonly Regex ControlGroupGuidPattern = new(
        @"^ControlGroup_[0-9a-fA-F]{32}$",
        RegexOptions.Compiled);

    [McpServerTool(Name = "check_resx")]
    [Description("Найти неверные ключи System.resx (Resource_GUID вместо Property_Name).")]
    public async Task<string> ValidateResx(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return $"**ОШИБКА**: Директория не найдена: `{directoryPath}`";

        var resxFiles = Directory.GetFiles(directoryPath, "*System.resx", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directoryPath, "*System.ru.resx", SearchOption.AllDirectories))
            .Distinct()
            .OrderBy(f => f)
            .ToArray();

        if (resxFiles.Length == 0)
            return $"**Результат**: В директории `{directoryPath}` не найдено файлов *System.resx / *System.ru.resx.";

        // Find MTD files for cross-referencing
        var mtdFiles = Directory.GetFiles(directoryPath, "*.mtd", SearchOption.AllDirectories);
        var mtdPropertyMap = await BuildMtdPropertyMap(mtdFiles);

        var allIssues = new List<FileIssues>();
        int totalKeys = 0;
        int totalBadKeys = 0;

        foreach (var resxFile in resxFiles)
        {
            var xml = await File.ReadAllTextAsync(resxFile);
            var xdoc = XDocument.Parse(xml);
            var dataElements = xdoc.Descendants("data").ToList();
            var fileIssues = new List<ResxIssue>();

            foreach (var data in dataElements)
            {
                var keyName = data.Attribute("name")?.Value ?? "";
                var value = data.Element("value")?.Value ?? "";
                totalKeys++;

                // Check 1: Resource_<GUID> format — definite error
                if (GuidPattern.IsMatch(keyName))
                {
                    totalBadKeys++;
                    var suggestion = TrySuggestCorrectKey(resxFile, keyName, value, mtdPropertyMap);
                    fileIssues.Add(new ResxIssue(
                        keyName, value,
                        "Resource_<GUID> — неверный формат, runtime не сможет разрешить подпись",
                        suggestion));
                    continue;
                }

                // Check 2: Verify known prefixes
                if (!IsValidKey(keyName))
                {
                    // Could be a custom key — just a warning
                    fileIssues.Add(new ResxIssue(
                        keyName, value,
                        "Неизвестный формат ключа — возможно, нестандартный",
                        null));
                }
            }

            if (fileIssues.Count > 0)
            {
                allIssues.Add(new FileIssues(resxFile, fileIssues));
            }
        }

        return FormatReport(directoryPath, resxFiles.Length, totalKeys, totalBadKeys, allIssues);
    }

    private static bool IsValidKey(string key)
    {
        if (ValidStandaloneKeys.Contains(key))
            return true;

        foreach (var prefix in ValidPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        // ControlGroup with GUID is valid platform convention
        if (ControlGroupGuidPattern.IsMatch(key))
            return true;

        // Form_<GUID>, Ribbon_*, FilterPanel_* are valid
        if (key.StartsWith("Form_") || key.StartsWith("Ribbon_") || key.StartsWith("FilterPanel_"))
            return true;

        return false;
    }

    private static async Task<Dictionary<string, List<string>>> BuildMtdPropertyMap(string[] mtdFiles)
    {
        // Map: entity file stem (without extension) -> list of property names
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(entityName))
                {
                    doc.Dispose();
                    continue;
                }

                var propNames = new List<string>();

                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in props.EnumerateArray())
                    {
                        if (prop.TryGetProperty("Name", out var pn))
                        {
                            var propName = pn.GetString();
                            if (!string.IsNullOrEmpty(propName))
                                propNames.Add(propName);
                        }
                    }
                }

                map[entityName] = propNames;
                doc.Dispose();
            }
            catch
            {
                // Skip unparseable MTD files
            }
        }

        return map;
    }

    private static string? TrySuggestCorrectKey(
        string resxFile,
        string badKey,
        string value,
        Dictionary<string, List<string>> mtdPropertyMap)
    {
        // Try to find the entity name from the resx file name
        // E.g., "ContractSystem.resx" -> entity "Contract"
        var fileName = Path.GetFileNameWithoutExtension(resxFile); // "ContractSystem" or "ContractSystem.ru"
        if (fileName.EndsWith(".ru", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^3]; // Remove ".ru"
        if (fileName.EndsWith("System", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^6]; // Remove "System"

        if (string.IsNullOrEmpty(fileName))
            return null;

        // Look up properties from MTD
        if (mtdPropertyMap.TryGetValue(fileName, out var propNames))
        {
            // Try to match by value — if the resx value matches a property name, suggest it
            var matchByValue = propNames.FirstOrDefault(p =>
                string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
            if (matchByValue != null)
                return $"Property_{matchByValue}";

            // If only one property is unmatched, suggest it
            // (This is a heuristic — works well for small entities)
        }

        // If the value looks like a property name (PascalCase word), suggest Property_<Value>
        if (!string.IsNullOrEmpty(value) && Regex.IsMatch(value, @"^[A-ZА-ЯЁ]"))
            return $"Property_{value} (предположение по значению)";

        return null;
    }

    private static string FormatReport(
        string directory,
        int totalFiles,
        int totalKeys,
        int totalBadKeys,
        List<FileIssues> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Проверка System.resx файлов");
        sb.AppendLine();
        sb.AppendLine($"**Директория**: `{directory}`");
        sb.AppendLine($"**Файлов проверено**: {totalFiles}");
        sb.AppendLine($"**Всего ключей**: {totalKeys}");
        sb.AppendLine();

        if (issues.Count == 0)
        {
            sb.AppendLine("**Результат**: Все ключи в правильном формате. Проблем не обнаружено.");
            return sb.ToString();
        }

        var criticalCount = issues.Sum(f => f.Issues.Count(i => i.Key.StartsWith("Resource_")));
        var warningCount = issues.Sum(f => f.Issues.Count(i => !i.Key.StartsWith("Resource_")));

        sb.AppendLine($"**Критических проблем (Resource_GUID)**: {criticalCount}");
        if (warningCount > 0)
            sb.AppendLine($"**Предупреждений**: {warningCount}");
        sb.AppendLine();

        foreach (var fileIssue in issues)
        {
            var relPath = fileIssue.FilePath;
            sb.AppendLine($"## `{Path.GetFileName(relPath)}`");
            sb.AppendLine($"Путь: `{relPath}`");
            sb.AppendLine();

            var criticals = fileIssue.Issues.Where(i => i.Key.StartsWith("Resource_")).ToList();
            var warnings = fileIssue.Issues.Where(i => !i.Key.StartsWith("Resource_")).ToList();

            if (criticals.Count > 0)
            {
                sb.AppendLine("### Ошибки (Resource_GUID)");
                sb.AppendLine();
                sb.AppendLine("| Текущий ключ | Значение | Рекомендуемый ключ |");
                sb.AppendLine("|-------------|----------|-------------------|");
                foreach (var issue in criticals)
                {
                    var suggestion = issue.Suggestion ?? "_(определите вручную по MTD)_";
                    sb.AppendLine($"| `{issue.Key}` | {issue.Value} | `{suggestion}` |");
                }
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine("### Предупреждения");
                sb.AppendLine();
                foreach (var issue in warnings)
                {
                    sb.AppendLine($"- `{issue.Key}` = \"{issue.Value}\" — {issue.Problem}");
                }
                sb.AppendLine();
            }
        }

        if (criticalCount > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Как исправить");
            sb.AppendLine();
            sb.AppendLine("1. Замените ключи `Resource_<GUID>` на `Property_<PropertyName>` в файлах *System.resx и *System.ru.resx");
            sb.AppendLine("2. Имена свойств берите из соответствующего .mtd файла (поле `Name` в `Properties`)");
            sb.AppendLine("3. После исправления пересоберите satellite DLL или переимпортируйте пакет");
            sb.AppendLine();
            sb.AppendLine("**Формат ключей платформы:**");
            sb.AppendLine("- Свойства: `Property_<PropertyName>`");
            sb.AppendLine("- Действия: `Action_<ActionName>`");
            sb.AppendLine("- Перечисления: `Enum_<EnumName>_<Value>`");
            sb.AppendLine("- Группы контролов: `ControlGroup_<GUID>`");
            sb.AppendLine("- `DisplayName`, `CollectionDisplayName` — без префикса");
        }

        return sb.ToString();
    }

    private record ResxIssue(string Key, string Value, string Problem, string? Suggestion);
    private record FileIssues(string FilePath, List<ResxIssue> Issues);
}
