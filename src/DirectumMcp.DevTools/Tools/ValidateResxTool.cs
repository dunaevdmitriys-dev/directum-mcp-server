using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateResxTool
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
        // Module-level cover keys
        "CoverGroup_",
        "CoverAction_",
        "CoverTab_",
        "CoverFunction_",
        // Module-level keys: widgets
        "Widget_",
        "WidgetTitle_",
        "WidgetDescription_",
        "WidgetAction_",
        // Module-level keys: jobs and async handlers
        "Job_",
        "JobDisplayName_",
        "JobDescription_",
        "AsyncHandler_",
        "AsyncHandlerDisplayName_",
        "AsyncHandlerDescription_",
        // Attachment groups, reports, parameters
        "AttachmentGroup_",
        "Report_",
        "Parameter_",
        // Block / route / assignment / task workflow keys
        "Block_",
        "Route_",
        "Assignment_",
        "Notice_",
        "Task_",
        "State_",
        // Collection / child entity keys
        "Collection_",
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

    // Pattern to detect GUID-embedded keys (e.g., SomePrefix_a1b2c3d4-e5f6-...)
    private static readonly Regex EmbeddedGuidPattern = new(
        @"_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-",
        RegexOptions.Compiled);

    // Custom string resource keys: PascalCase identifiers used for validation messages,
    // dialog text, error messages (e.g., "NameRequired", "MaxAmountMustBePositive").
    // Valid Sungero convention — developers define custom string resources this way.
    private static readonly Regex CustomStringResourcePattern = new(
        @"^[A-Z][a-zA-Z0-9]+$",
        RegexOptions.Compiled);

    [McpServerTool(Name = "check_resx")]
    [Description("Найти неверные ключи System.resx (Resource_GUID вместо Property_Name).")]
    public async Task<string> ValidateResx(string directoryPath)
    {
        if (!PathGuard.IsAllowed(directoryPath))
            return PathGuard.DenyMessage(directoryPath);

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
                        suggestion,
                        ResxSeverity.Error));
                    continue;
                }

                // Check 2: Verify known prefixes
                // Custom PascalCase keys and platform-inherited keys are valid — skip silently
                if (!IsValidKey(keyName))
                {
                    // Only report truly suspicious keys (not PascalCase custom resources)
                    if (!CustomStringResourcePattern.IsMatch(keyName))
                    {
                        fileIssues.Add(new ResxIssue(
                            keyName, value,
                            "Нестандартный ключ (INFO — возможно, кастомный или платформенный)",
                            null,
                            ResxSeverity.Info));
                    }
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

        // Keys containing embedded GUIDs are platform-generated
        if (EmbeddedGuidPattern.IsMatch(key))
            return true;

        return false;
    }

    private static async Task<Dictionary<string, List<string>>> BuildMtdPropertyMap(string[] mtdFiles)
    {
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
        var fileName = Path.GetFileNameWithoutExtension(resxFile);
        if (fileName.EndsWith(".ru", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^3];
        if (fileName.EndsWith("System", StringComparison.OrdinalIgnoreCase))
            fileName = fileName[..^6];

        if (string.IsNullOrEmpty(fileName))
            return null;

        if (mtdPropertyMap.TryGetValue(fileName, out var propNames))
        {
            var matchByValue = propNames.FirstOrDefault(p =>
                string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
            if (matchByValue != null)
                return $"Property_{matchByValue}";
        }

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

        var criticalCount = issues.Sum(f => f.Issues.Count(i => i.Severity == ResxSeverity.Error));
        var infoCount = issues.Sum(f => f.Issues.Count(i => i.Severity == ResxSeverity.Info));

        sb.AppendLine($"**Критических проблем (Resource_GUID)**: {criticalCount}");
        if (infoCount > 0)
            sb.AppendLine($"**Информационных (нестандартные ключи)**: {infoCount}");
        sb.AppendLine();

        var criticalFiles = issues.Where(f => f.Issues.Any(i => i.Severity == ResxSeverity.Error)).ToList();
        var infoOnlyFiles = issues.Where(f => f.Issues.All(i => i.Severity == ResxSeverity.Info)).ToList();

        foreach (var fileIssue in criticalFiles)
        {
            var relPath = fileIssue.FilePath;
            sb.AppendLine($"## `{Path.GetFileName(relPath)}`");
            sb.AppendLine($"Путь: `{relPath}`");
            sb.AppendLine();

            var criticals = fileIssue.Issues.Where(i => i.Severity == ResxSeverity.Error).ToList();

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

        if (infoOnlyFiles.Count > 0)
        {
            sb.AppendLine($"### Информационные ({infoCount} нестандартных ключей в {infoOnlyFiles.Count} файлах)");
            sb.AppendLine();
            sb.AppendLine("Эти ключи не соответствуют стандартным префиксам, но могут быть кастомными или платформенными. Проверьте вручную при необходимости.");
            sb.AppendLine();
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
            sb.AppendLine("- Виджеты: `Widget_*`, `WidgetTitle_*`, `WidgetDescription_*`, `WidgetAction_*`");
            sb.AppendLine("- Задания/Обработчики: `Job_*`, `JobDisplayName_*`, `AsyncHandler_*`, `AsyncHandlerDisplayName_*`");
            sb.AppendLine("- Вложения: `AttachmentGroup_*`");
            sb.AppendLine("- Отчёты: `Report_*`, `Parameter_*`");
            sb.AppendLine("- `DisplayName`, `CollectionDisplayName` — без префикса");
        }

        return sb.ToString();
    }

    private enum ResxSeverity { Error, Info }
    private record ResxIssue(string Key, string Value, string Problem, string? Suggestion, ResxSeverity Severity);
    private record FileIssues(string FilePath, List<ResxIssue> Issues);
}
