using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SyncCheckTool
{
    [McpServerTool(Name = "sync_check")]
    [Description("Сравнивает локальную папку с исходниками разработчика и опубликованный модуль на стенде (AppliedModules). Находит расхождения в .mtd и .resx файлах. Чисто файловый инструмент, не использует OData.")]
    public async Task<string> SyncCheck(
        [Description("Путь к папке с исходниками разработчика")] string source_path,
        [Description("Путь к папке с опубликованным модулем на стенде (обычно в AppliedModules)")] string published_path)
    {
        if (string.IsNullOrWhiteSpace(source_path))
            return "**ОШИБКА**: Параметр `source_path` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(published_path))
            return "**ОШИБКА**: Параметр `published_path` не может быть пустым.";

        if (!PathGuard.IsAllowed(source_path))
            return PathGuard.DenyMessage(source_path);
        if (!PathGuard.IsAllowed(published_path))
            return PathGuard.DenyMessage(published_path);

        if (!Directory.Exists(source_path))
            return $"**ОШИБКА**: Директория исходников не найдена: `{source_path}`";
        if (!Directory.Exists(published_path))
            return $"**ОШИБКА**: Директория стенда не найдена: `{published_path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Сравнение исходников и стенда");
        sb.AppendLine();
        sb.AppendLine($"**Исходники:** `{source_path}`");
        sb.AppendLine($"**Стенд:** `{published_path}`");
        sb.AppendLine();

        // --- PackageInfo.xml ---
        await AppendPackageInfoDiff(sb, source_path, published_path);

        // --- Collect .mtd files ---
        var sourceMtd = CollectFiles(source_path, ".mtd");
        var publishedMtd = CollectFiles(published_path, ".mtd");

        var allMtdKeys = new HashSet<string>(sourceMtd.Keys, StringComparer.OrdinalIgnoreCase);
        allMtdKeys.UnionWith(publishedMtd.Keys);

        var onlyInSource = new List<string>();
        var onlyInPublished = new List<string>();
        var matchedSame = new List<string>();
        var matchedDiff = new List<(string RelPath, MtdDiff Diff)>();

        foreach (var key in allMtdKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var inSource = sourceMtd.TryGetValue(key, out var srcFile);
            var inPub = publishedMtd.TryGetValue(key, out var pubFile);

            if (inSource && !inPub)
            {
                onlyInSource.Add(key);
            }
            else if (!inSource && inPub)
            {
                onlyInPublished.Add(key);
            }
            else if (inSource && inPub)
            {
                var diff = await CompareMtdFilesAsync(srcFile!, pubFile!);
                if (diff.HasDifferences)
                    matchedDiff.Add((key, diff));
                else
                    matchedSame.Add(key);
            }
        }

        // --- Files table ---
        sb.AppendLine("## Файлы");
        sb.AppendLine("| Файл | Статус |");
        sb.AppendLine("|------|--------|");

        foreach (var key in allMtdKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            string status;
            if (onlyInSource.Contains(key))
                status = "Только в исходниках";
            else if (onlyInPublished.Contains(key))
                status = "Только на стенде";
            else if (matchedDiff.Any(d => string.Equals(d.RelPath, key, StringComparison.OrdinalIgnoreCase)))
                status = "Различается";
            else
                status = "Совпадает";

            sb.AppendLine($"| {key} | {status} |");
        }
        sb.AppendLine();

        // --- MTD differences ---
        foreach (var (relPath, diff) in matchedDiff)
        {
            sb.AppendLine($"## Различия в {relPath}");
            sb.AppendLine();

            if (diff.AddedProperties.Count > 0)
            {
                sb.AppendLine("### Добавленные свойства (в исходниках)");
                foreach (var (name, type) in diff.AddedProperties)
                    sb.AppendLine($"- {name} ({type})");
                sb.AppendLine();
            }

            if (diff.RemovedProperties.Count > 0)
            {
                sb.AppendLine("### Удалённые свойства (на стенде)");
                foreach (var (name, type) in diff.RemovedProperties)
                    sb.AppendLine($"- {name} ({type})");
                sb.AppendLine();
            }

            if (diff.EnumChanges.Count > 0)
            {
                sb.AppendLine("### Изменённые Enum-значения");
                foreach (var (propName, srcValues, pubValues) in diff.EnumChanges)
                {
                    var srcStr = string.Join(", ", srcValues);
                    var pubStr = string.Join(", ", pubValues);
                    sb.AppendLine($"- {propName}: исходники [{srcStr}], стенд [{pubStr}]");
                }
                sb.AppendLine();
            }
        }

        // --- Resx comparison ---
        var sourceResx = CollectFiles(source_path, ".resx");
        var publishedResx = CollectFiles(published_path, ".resx");

        var allResxKeys = new HashSet<string>(sourceResx.Keys, StringComparer.OrdinalIgnoreCase);
        allResxKeys.UnionWith(publishedResx.Keys);

        var resxRows = new List<(string File, int NewKeys, int RemovedKeys)>();

        foreach (var key in allResxKeys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var inSrc = sourceResx.TryGetValue(key, out var srcResx);
            var inPub = publishedResx.TryGetValue(key, out var pubResx);

            if (inSrc && inPub)
            {
                var (added, removed) = await CompareResxKeysAsync(srcResx!, pubResx!);
                if (added > 0 || removed > 0)
                    resxRows.Add((key, added, removed));
            }
            else if (inSrc && !inPub)
            {
                var srcKeys = await CountResxKeysAsync(srcResx!);
                resxRows.Add((key, srcKeys, 0));
            }
            else if (!inSrc && inPub)
            {
                var pubKeys = await CountResxKeysAsync(pubResx!);
                resxRows.Add((key, 0, pubKeys));
            }
        }

        if (resxRows.Count > 0)
        {
            sb.AppendLine("## Resx");
            sb.AppendLine("| Файл | Новых ключей | Удалённых ключей |");
            sb.AppendLine("|------|-------------|-----------------|");
            foreach (var (file, newKeys, deletedKeys) in resxRows)
                sb.AppendLine($"| {file} | {newKeys} | {deletedKeys} |");
            sb.AppendLine();
        }

        // --- Summary ---
        sb.AppendLine("## Итого");
        sb.AppendLine($"- Совпадают: {matchedSame.Count} файлов");
        sb.AppendLine($"- Различаются: {matchedDiff.Count} файлов");
        sb.AppendLine($"- Только в исходниках: {onlyInSource.Count}");
        sb.AppendLine($"- Только на стенде: {onlyInPublished.Count}");

        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // PackageInfo.xml
    // -------------------------------------------------------------------------

    private static async Task AppendPackageInfoDiff(StringBuilder sb, string sourcePath, string publishedPath)
    {
        var srcPkg = Path.Combine(sourcePath, "PackageInfo.xml");
        var pubPkg = Path.Combine(publishedPath, "PackageInfo.xml");

        var hasSrc = File.Exists(srcPkg);
        var hasPub = File.Exists(pubPkg);

        if (!hasSrc && !hasPub)
            return;

        sb.AppendLine("## PackageInfo.xml");
        sb.AppendLine();

        var srcVersion = hasSrc ? ReadPackageVersion(await File.ReadAllTextAsync(srcPkg)) : "(нет)";
        var pubVersion = hasPub ? ReadPackageVersion(await File.ReadAllTextAsync(pubPkg)) : "(нет)";

        sb.AppendLine($"- Исходники: `{srcVersion}`");
        sb.AppendLine($"- Стенд:     `{pubVersion}`");

        if (!string.Equals(srcVersion, pubVersion, StringComparison.Ordinal))
            sb.AppendLine("- **Версии отличаются**");
        else
            sb.AppendLine("- Версии совпадают");

        sb.AppendLine();
    }

    private static string ReadPackageVersion(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Root?.Attribute("Version")?.Value
                ?? doc.Root?.Element("Version")?.Value
                ?? "(версия не найдена)";
        }
        catch
        {
            return "(ошибка разбора)";
        }
    }

    // -------------------------------------------------------------------------
    // File collection
    // -------------------------------------------------------------------------

    private static Dictionary<string, string> CollectFiles(string rootDir, string extension)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(rootDir, $"*{extension}", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(rootDir, file).Replace('\\', '/');
            result[rel] = file;
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // MTD comparison
    // -------------------------------------------------------------------------

    private static async Task<MtdDiff> CompareMtdFilesAsync(string srcFile, string pubFile)
    {
        var diff = new MtdDiff();

        string srcContent, pubContent;
        try
        {
            srcContent = await File.ReadAllTextAsync(srcFile);
            pubContent = await File.ReadAllTextAsync(pubFile);
        }
        catch
        {
            return diff;
        }

        if (string.Equals(srcContent, pubContent, StringComparison.Ordinal))
            return diff;

        try
        {
            using var srcDoc = JsonDocument.Parse(srcContent);
            using var pubDoc = JsonDocument.Parse(pubContent);

            var srcRoot = srcDoc.RootElement;
            var pubRoot = pubDoc.RootElement;

            // Properties
            var srcProps = ExtractProperties(srcRoot);
            var pubProps = ExtractProperties(pubRoot);

            foreach (var (name, type) in srcProps)
                if (!pubProps.ContainsKey(name))
                    diff.AddedProperties.Add((name, type));

            foreach (var (name, type) in pubProps)
                if (!srcProps.ContainsKey(name))
                    diff.RemovedProperties.Add((name, type));

            // Enum values
            var srcEnums = ExtractEnumValues(srcRoot);
            var pubEnums = ExtractEnumValues(pubRoot);

            var allEnumProps = new HashSet<string>(srcEnums.Keys, StringComparer.Ordinal);
            allEnumProps.UnionWith(pubEnums.Keys);

            foreach (var propName in allEnumProps.OrderBy(x => x))
            {
                srcEnums.TryGetValue(propName, out var srcVals);
                pubEnums.TryGetValue(propName, out var pubVals);

                srcVals ??= new List<string>();
                pubVals ??= new List<string>();

                if (!srcVals.SequenceEqual(pubVals, StringComparer.Ordinal))
                    diff.EnumChanges.Add((propName, srcVals, pubVals));
            }
        }
        catch
        {
            // Treat as a generic diff — caller will see HasDifferences via non-empty content difference
            // But we can't tell properties apart, so mark it as structurally different without details
            diff.AddedProperties.Add(("(ошибка парсинга JSON)", ""));
        }

        return diff;
    }

    private static Dictionary<string, string> ExtractProperties(JsonElement root)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("Properties", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var prop in arr.EnumerateArray())
        {
            if (!prop.TryGetProperty("Name", out var nameEl))
                continue;
            var name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var type = "(неизвестный тип)";
            if (prop.TryGetProperty("$type", out var typeEl))
            {
                var fullType = typeEl.GetString() ?? "";
                // Extract short type name: "Sungero.Metadata.StringPropertyMetadata" -> "StringPropertyMetadata"
                var dot = fullType.LastIndexOf('.');
                type = dot >= 0 ? fullType[(dot + 1)..] : fullType;
            }

            result[name] = type;
        }

        return result;
    }

    private static Dictionary<string, List<string>> ExtractEnumValues(JsonElement root)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (!root.TryGetProperty("Properties", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var prop in arr.EnumerateArray())
        {
            if (!prop.TryGetProperty("$type", out var typeEl))
                continue;
            if (typeEl.GetString()?.Contains("EnumPropertyMetadata") != true)
                continue;

            if (!prop.TryGetProperty("Name", out var nameEl))
                continue;
            var propName = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(propName))
                continue;

            var values = new List<string>();
            if (prop.TryGetProperty("DirectValues", out var dvArr) && dvArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var dv in dvArr.EnumerateArray())
                {
                    if (dv.TryGetProperty("Name", out var dvName))
                    {
                        var v = dvName.GetString();
                        if (!string.IsNullOrWhiteSpace(v))
                            values.Add(v!);
                    }
                }
            }

            result[propName!] = values;
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Resx comparison
    // -------------------------------------------------------------------------

    private static async Task<(int Added, int Removed)> CompareResxKeysAsync(string srcFile, string pubFile)
    {
        try
        {
            var srcKeys = ParseResxKeys(await File.ReadAllTextAsync(srcFile));
            var pubKeys = ParseResxKeys(await File.ReadAllTextAsync(pubFile));

            var added = srcKeys.Keys.Except(pubKeys.Keys, StringComparer.Ordinal).Count();
            var removed = pubKeys.Keys.Except(srcKeys.Keys, StringComparer.Ordinal).Count();
            return (added, removed);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static async Task<int> CountResxKeysAsync(string file)
    {
        try
        {
            var keys = ParseResxKeys(await File.ReadAllTextAsync(file));
            return keys.Count;
        }
        catch
        {
            return 0;
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

    // -------------------------------------------------------------------------
    // Inner types
    // -------------------------------------------------------------------------

    private sealed class MtdDiff
    {
        public List<(string Name, string Type)> AddedProperties { get; } = new();
        public List<(string Name, string Type)> RemovedProperties { get; } = new();
        public List<(string PropName, List<string> SrcValues, List<string> PubValues)> EnumChanges { get; } = new();

        public bool HasDifferences =>
            AddedProperties.Count > 0 ||
            RemovedProperties.Count > 0 ||
            EnumChanges.Count > 0;
    }
}
