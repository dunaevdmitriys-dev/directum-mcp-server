using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class DiffPackagesTool
{
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

    [McpServerTool(Name = "diff_packages")]
    [Description("Сравнение двух пакетов Directum RX (директорий или .dat-файлов): различия в метаданных, ресурсах и коде.")]
    public async Task<string> DiffPackages(
        [Description("Путь к первому пакету (директория или .dat-файл)")] string pathA,
        [Description("Путь к второму пакету (директория или .dat-файл)")] string pathB,
        [Description("Область сравнения: 'metadata' (только .mtd), 'resources' (только .resx), 'code' (только .cs), 'all' (по умолчанию)")] string scope = "all")
    {
        if (string.IsNullOrWhiteSpace(pathA))
            return "**ОШИБКА**: Параметр `pathA` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(pathB))
            return "**ОШИБКА**: Параметр `pathB` не может быть пустым.";

        if (!IsPathAllowed(pathA))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{pathA}` находится за пределами разрешённых директорий.";
        if (!IsPathAllowed(pathB))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{pathB}` находится за пределами разрешённых директорий.";

        var normalizedScope = scope.ToLowerInvariant();
        if (normalizedScope is not ("all" or "metadata" or "resources" or "code"))
            return $"**ОШИБКА**: Неизвестный scope `{scope}`. Допустимые: all, metadata, resources, code.";

        string? tempDirA = null;
        string? tempDirB = null;

        try
        {
            var dirA = ResolvePath(pathA, ref tempDirA);
            if (dirA == null)
                return $"**ОШИБКА**: Путь не найден или не является директорией/.dat-файлом: `{pathA}`";

            var dirB = ResolvePath(pathB, ref tempDirB);
            if (dirB == null)
                return $"**ОШИБКА**: Путь не найден или не является директорией/.dat-файлом: `{pathB}`";

            var extensions = GetExtensions(normalizedScope);

            var filesA = CollectFiles(dirA, extensions);
            var filesB = CollectFiles(dirB, extensions);

            var allKeys = new HashSet<string>(filesA.Keys, StringComparer.OrdinalIgnoreCase);
            allKeys.UnionWith(filesB.Keys);

            var added = new List<string>();
            var removed = new List<string>();
            var changed = new List<(string RelPath, string Details)>();
            int unchanged = 0;

            foreach (var key in allKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                var inA = filesA.TryGetValue(key, out var fileA);
                var inB = filesB.TryGetValue(key, out var fileB);

                if (inA && !inB)
                {
                    removed.Add(key);
                }
                else if (!inA && inB)
                {
                    added.Add(key);
                }
                else if (inA && inB)
                {
                    var details = await CompareFilesAsync(fileA!, fileB!, key);
                    if (details != null)
                        changed.Add((key, details));
                    else
                        unchanged++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("## Сравнение пакетов");
            sb.AppendLine();
            sb.AppendLine($"**A:** `{pathA}`");
            sb.AppendLine($"**B:** `{pathB}`");
            sb.AppendLine();

            // Добавлено
            sb.AppendLine($"### Добавлено ({added.Count})");
            if (added.Count > 0)
            {
                sb.AppendLine("| Файл | Тип |");
                sb.AppendLine("|------|-----|");
                foreach (var f in added)
                    sb.AppendLine($"| {f} | {ClassifyFile(f)} |");
            }
            sb.AppendLine();

            // Удалено
            sb.AppendLine($"### Удалено ({removed.Count})");
            if (removed.Count > 0)
            {
                sb.AppendLine("| Файл | Тип |");
                sb.AppendLine("|------|-----|");
                foreach (var f in removed)
                    sb.AppendLine($"| {f} | {ClassifyFile(f)} |");
            }
            sb.AppendLine();

            // Изменено
            sb.AppendLine($"### Изменено ({changed.Count})");
            if (changed.Count > 0)
            {
                sb.AppendLine("| Файл | Тип | Детали |");
                sb.AppendLine("|------|-----|--------|");
                foreach (var (relPath, details) in changed)
                    sb.AppendLine($"| {relPath} | {ClassifyFile(relPath)} | {details} |");
            }
            sb.AppendLine();

            // Итого
            sb.AppendLine("### Итого");
            sb.AppendLine($"- Добавлено: {added.Count} файлов");
            sb.AppendLine($"- Удалено: {removed.Count} файлов");
            sb.AppendLine($"- Изменено: {changed.Count} файлов");
            sb.AppendLine($"- Без изменений: {unchanged} файлов");
            sb.AppendLine();

            return sb.ToString();
        }
        finally
        {
            if (tempDirA != null && Directory.Exists(tempDirA))
                try { Directory.Delete(tempDirA, true); } catch { }
            if (tempDirB != null && Directory.Exists(tempDirB))
                try { Directory.Delete(tempDirB, true); } catch { }
        }
    }

    private static string? ResolvePath(string path, ref string? tempDir)
    {
        if (Directory.Exists(path))
            return path;

        if (File.Exists(path) && path.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            tempDir = Path.Combine(Path.GetTempPath(), "diffpkg_" + Guid.NewGuid().ToString("N")[..8]);
            ZipFile.ExtractToDirectory(path, tempDir);
            return tempDir;
        }

        return null;
    }

    private static HashSet<string> GetExtensions(string scope)
    {
        return scope switch
        {
            "metadata" => new(StringComparer.OrdinalIgnoreCase) { ".mtd" },
            "resources" => new(StringComparer.OrdinalIgnoreCase) { ".resx" },
            "code" => new(StringComparer.OrdinalIgnoreCase) { ".cs" },
            _ => new(StringComparer.OrdinalIgnoreCase) { ".mtd", ".resx", ".cs", ".xml" }
        };
    }

    private static Dictionary<string, string> CollectFiles(string rootDir, HashSet<string> extensions)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (extensions.Contains(ext))
            {
                var relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
                result[relativePath] = file;
            }
        }
        return result;
    }

    private static async Task<string?> CompareFilesAsync(string fileA, string fileB, string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();

        return ext switch
        {
            ".mtd" => await CompareMtdAsync(fileA, fileB),
            ".resx" => await CompareResxAsync(fileA, fileB),
            ".cs" => await CompareCsAsync(fileA, fileB),
            _ => await CompareGenericAsync(fileA, fileB)
        };
    }

    private static async Task<string?> CompareMtdAsync(string fileA, string fileB)
    {
        var contentA = await File.ReadAllTextAsync(fileA);
        var contentB = await File.ReadAllTextAsync(fileB);

        if (contentA == contentB)
            return null;

        try
        {
            using var docA = JsonDocument.Parse(contentA);
            using var docB = JsonDocument.Parse(contentB);
            var rootA = docA.RootElement;
            var rootB = docB.RootElement;

            var details = new List<string>();

            // Compare properties
            var propsA = GetNames(rootA, "Properties");
            var propsB = GetNames(rootB, "Properties");
            var addedProps = propsB.Except(propsA).ToList();
            var removedProps = propsA.Except(propsB).ToList();
            if (addedProps.Count > 0)
                details.Add($"+{addedProps.Count} свойств");
            if (removedProps.Count > 0)
                details.Add($"-{removedProps.Count} свойств");

            // Compare actions
            var actionsA = GetNames(rootA, "Actions");
            var actionsB = GetNames(rootB, "Actions");
            var addedActions = actionsB.Except(actionsA).ToList();
            var removedActions = actionsA.Except(actionsB).ToList();
            if (addedActions.Count > 0)
                details.Add($"+{addedActions.Count} действий");
            if (removedActions.Count > 0)
                details.Add($"-{removedActions.Count} действий");

            if (details.Count == 0)
                details.Add("структурные изменения");

            return string.Join(", ", details);
        }
        catch
        {
            return "изменено (ошибка парсинга JSON)";
        }
    }

    private static HashSet<string> GetNames(JsonElement root, string arrayProperty)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(arrayProperty, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("Name", out var nameVal) && nameVal.ValueKind == JsonValueKind.String)
                {
                    var name = nameVal.GetString();
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
        }
        return names;
    }

    private static async Task<string?> CompareResxAsync(string fileA, string fileB)
    {
        var contentA = await File.ReadAllTextAsync(fileA);
        var contentB = await File.ReadAllTextAsync(fileB);

        if (contentA == contentB)
            return null;

        try
        {
            var keysA = ParseResxKeys(contentA);
            var keysB = ParseResxKeys(contentB);

            var allKeys = new HashSet<string>(keysA.Keys);
            allKeys.UnionWith(keysB.Keys);

            int addedKeys = 0, removedKeys = 0, changedKeys = 0;

            foreach (var key in allKeys)
            {
                var inA = keysA.TryGetValue(key, out var valA);
                var inB = keysB.TryGetValue(key, out var valB);

                if (!inA && inB) addedKeys++;
                else if (inA && !inB) removedKeys++;
                else if (inA && inB && valA != valB) changedKeys++;
            }

            var details = new List<string>();
            if (addedKeys > 0) details.Add($"+{addedKeys} ключей");
            if (removedKeys > 0) details.Add($"-{removedKeys} ключей");
            if (changedKeys > 0) details.Add($"~{changedKeys} изменено");

            return details.Count > 0 ? string.Join(", ", details) : "изменено";
        }
        catch
        {
            return "изменено (ошибка парсинга RESX)";
        }
    }

    private static Dictionary<string, string> ParseResxKeys(string resxContent)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            var doc = XDocument.Parse(resxContent);
            foreach (var data in doc.Descendants("data"))
            {
                var name = data.Attribute("name")?.Value;
                var value = data.Element("value")?.Value ?? "";
                if (!string.IsNullOrEmpty(name))
                    result[name] = value;
            }
        }
        catch
        {
            // Return empty on parse error
        }
        return result;
    }

    private static async Task<string?> CompareCsAsync(string fileA, string fileB)
    {
        var contentA = await File.ReadAllTextAsync(fileA);
        var contentB = await File.ReadAllTextAsync(fileB);

        if (contentA == contentB)
            return null;

        var linesA = contentA.Split('\n').Length;
        var linesB = contentB.Split('\n').Length;

        return $"{linesA} → {linesB} строк";
    }

    private static async Task<string?> CompareGenericAsync(string fileA, string fileB)
    {
        var contentA = await File.ReadAllBytesAsync(fileA);
        var contentB = await File.ReadAllBytesAsync(fileB);

        return contentA.AsSpan().SequenceEqual(contentB) ? null : "изменено";
    }

    private static string ClassifyFile(string relativePath)
    {
        var ext = Path.GetExtension(relativePath).ToLowerInvariant();
        return ext switch
        {
            ".mtd" => "Метаданные",
            ".resx" => "Ресурсы",
            ".cs" => "Код",
            ".xml" => "Конфигурация",
            _ => "Прочее"
        };
    }
}
