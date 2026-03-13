using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SyncResxKeysTool
{
    [McpServerTool(Name = "sync_resx_keys")]
    [Description("Сканирует .mtd файлы сущностей пакета, извлекает свойства и действия, добавляет недостающие ключи в *System.resx и *System.ru.resx файлы по конвенциям платформы Directum RX.")]
    public async Task<string> SyncResxKeys(
        [Description("Путь к директории пакета")] string packagePath,
        [Description("Если true — только показывает, что будет добавлено, без изменения файлов (по умолчанию true)")] bool dryRun = true)
    {
        if (!IsPathAllowed(packagePath))
            return $"**ОШИБКА**: Путь `{packagePath}` не разрешён. Разрешены только пути внутри SOLUTION_PATH или временной директории.";

        if (!Directory.Exists(packagePath))
            return $"**ОШИБКА**: Директория не найдена: `{packagePath}`";

        var mtdFiles = Directory.GetFiles(packagePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFileName(f), "Module.mtd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mtdFiles.Count == 0)
            return $"**ОШИБКА**: Файлы .mtd не найдены в `{packagePath}`";

        var report = new StringBuilder();
        report.AppendLine("## Синхронизация ключей System.resx");
        report.AppendLine();
        report.AppendLine($"**Пакет:** `{packagePath}`");
        report.AppendLine($"**Режим:** dryRun={dryRun.ToString().ToLower()}{(dryRun ? " (предварительный просмотр)" : " (запись изменений)")}");
        report.AppendLine();

        int totalEntities = 0;
        int totalKeysAdded = 0;
        int totalFilesChanged = 0;

        foreach (var mtdFile in mtdFiles)
        {
            string mtdContent;
            try
            {
                mtdContent = await File.ReadAllTextAsync(mtdFile);
            }
            catch (Exception ex)
            {
                report.AppendLine($"**ОШИБКА**: Не удалось прочитать `{mtdFile}`: {ex.Message}");
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(mtdContent);
            }
            catch (Exception ex)
            {
                report.AppendLine($"**ОШИБКА**: Не удалось разобрать JSON `{mtdFile}`: {ex.Message}");
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;

                if (!root.TryGetProperty("Name", out var nameProp))
                    continue;

                var entityName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(entityName))
                    continue;

                totalEntities++;

                // Collect required keys
                var requiredKeys = new List<(string Key, string Description)>();

                // DisplayName
                requiredKeys.Add(("DisplayName", "отображаемое имя"));

                // Properties
                if (root.TryGetProperty("Properties", out var propertiesElement) &&
                    propertiesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in propertiesElement.EnumerateArray())
                    {
                        if (prop.TryGetProperty("IsAncestorMetadata", out var isAncestor) &&
                            isAncestor.ValueKind == JsonValueKind.True)
                            continue;

                        if (!prop.TryGetProperty("Name", out var propNameEl))
                            continue;

                        var propName = propNameEl.GetString();
                        if (string.IsNullOrWhiteSpace(propName))
                            continue;

                        requiredKeys.Add(($"Property_{propName}", "свойство"));

                        // Enum values
                        if (prop.TryGetProperty("$type", out var typeProp) &&
                            typeProp.GetString()?.Contains("EnumPropertyMetadata") == true)
                        {
                            if (prop.TryGetProperty("DirectValues", out var directValues) &&
                                directValues.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var enumVal in directValues.EnumerateArray())
                                {
                                    if (!enumVal.TryGetProperty("Name", out var enumValNameEl))
                                        continue;

                                    var enumValName = enumValNameEl.GetString();
                                    if (string.IsNullOrWhiteSpace(enumValName))
                                        continue;

                                    requiredKeys.Add(($"Enum_{propName}_{enumValName}", "перечисление"));
                                }
                            }
                        }
                    }
                }

                // Actions
                if (root.TryGetProperty("Actions", out var actionsElement) &&
                    actionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var action in actionsElement.EnumerateArray())
                    {
                        if (action.TryGetProperty("IsAncestorMetadata", out var isAncestor) &&
                            isAncestor.ValueKind == JsonValueKind.True)
                            continue;

                        if (!action.TryGetProperty("Name", out var actionNameEl))
                            continue;

                        var actionName = actionNameEl.GetString();
                        if (string.IsNullOrWhiteSpace(actionName))
                            continue;

                        requiredKeys.Add(($"Action_{actionName}", "действие"));
                    }
                }

                // Find System.resx files
                var entityDir = Path.GetDirectoryName(mtdFile)!;
                var resxPattern = $"{entityName}System.resx";
                var resxRuPattern = $"{entityName}System.ru.resx";

                var resxFile = FindResxFile(entityDir, resxPattern);
                var resxRuFile = FindResxFile(entityDir, resxRuPattern);

                if (resxFile == null && resxRuFile == null)
                {
                    report.AppendLine($"### {entityName}");
                    report.AppendLine($"> Файлы `{resxPattern}` и `{resxRuPattern}` не найдены рядом с `{Path.GetFileName(mtdFile)}`");
                    report.AppendLine();
                    continue;
                }

                // Find missing keys in .resx
                var missingInResx = resxFile != null
                    ? GetMissingKeys(resxFile, requiredKeys.Select(k => k.Key).ToHashSet())
                    : new HashSet<string>();

                var missingInResxRu = resxRuFile != null
                    ? GetMissingKeys(resxRuFile, requiredKeys.Select(k => k.Key).ToHashSet())
                    : new HashSet<string>();

                // Union of all missing keys across both files
                var allMissingKeys = new HashSet<string>(missingInResx);
                allMissingKeys.UnionWith(missingInResxRu);

                var missingKeysList = requiredKeys
                    .Where(k => allMissingKeys.Contains(k.Key))
                    .ToList();

                if (missingKeysList.Count == 0)
                {
                    report.AppendLine($"### {entityName} — все ключи присутствуют");
                    report.AppendLine();
                    continue;
                }

                var resxDisplayName = resxFile != null ? Path.GetFileName(resxFile) : resxPattern;
                report.AppendLine($"### {entityName} ({resxDisplayName})");
                report.AppendLine($"Добавлено ключей: {missingKeysList.Count}");
                foreach (var (key, desc) in missingKeysList)
                    report.AppendLine($"- `{key}` ({desc})");
                report.AppendLine();

                if (!dryRun)
                {
                    int filesChanged = 0;

                    if (resxFile != null && missingInResx.Count > 0)
                    {
                        var keysToAdd = requiredKeys.Where(k => missingInResx.Contains(k.Key)).Select(k => k.Key).ToList();
                        try
                        {
                            AddKeysToResxFile(resxFile, keysToAdd);
                            filesChanged++;
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine($"  **ОШИБКА** при записи `{resxFile}`: {ex.Message}");
                        }
                    }

                    if (resxRuFile != null && missingInResxRu.Count > 0)
                    {
                        var keysToAdd = requiredKeys.Where(k => missingInResxRu.Contains(k.Key)).Select(k => k.Key).ToList();
                        try
                        {
                            AddKeysToResxFile(resxRuFile, keysToAdd);
                            filesChanged++;
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine($"  **ОШИБКА** при записи `{resxRuFile}`: {ex.Message}");
                        }
                    }

                    totalFilesChanged += filesChanged;
                }

                totalKeysAdded += missingKeysList.Count;
            }
        }

        report.AppendLine("### Итого");
        report.AppendLine($"- Проверено сущностей: {totalEntities}");
        report.AppendLine($"- Добавлено ключей: {totalKeysAdded}");
        report.AppendLine($"- Файлов изменено: {(dryRun ? 0 : totalFilesChanged)}");

        if (dryRun && totalKeysAdded > 0)
        {
            report.AppendLine();
            report.AppendLine("Для применения вызовите с `dryRun=false`.");
        }

        return report.ToString();
    }

    private static string? FindResxFile(string baseDir, string pattern)
    {
        // Search in the entity directory and one level up/down
        var candidates = Directory.GetFiles(baseDir, pattern, SearchOption.AllDirectories);
        if (candidates.Length > 0)
            return candidates[0];

        var parent = Directory.GetParent(baseDir)?.FullName;
        if (parent != null)
        {
            candidates = Directory.GetFiles(parent, pattern, SearchOption.AllDirectories);
            if (candidates.Length > 0)
                return candidates[0];
        }

        return null;
    }

    private static HashSet<string> GetMissingKeys(string resxFile, HashSet<string> requiredKeys)
    {
        try
        {
            var xdoc = XDocument.Load(resxFile);
            var existingKeys = xdoc.Root?
                .Elements("data")
                .Select(e => e.Attribute("name")?.Value)
                .Where(n => n != null)
                .Select(n => n!)
                .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();

            return requiredKeys.Where(k => !existingKeys.Contains(k)).ToHashSet();
        }
        catch
        {
            // If we can't parse the file, consider all keys missing
            return new HashSet<string>(requiredKeys);
        }
    }

    private static void AddKeysToResxFile(string resxFile, IEnumerable<string> keys)
    {
        var xdoc = XDocument.Load(resxFile);
        var rootEl = xdoc.Root ?? throw new InvalidOperationException("Нет корневого элемента в resx файле.");

        foreach (var key in keys)
        {
            var dataEl = new XElement("data",
                new XAttribute("name", key),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", key));
            rootEl.Add(dataEl);
        }

        xdoc.Save(resxFile);
    }

    private static bool IsPathAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        var tempPath = Path.GetTempPath();

        var normalizedPath = Path.GetFullPath(path);
        var normalizedTemp = Path.GetFullPath(tempPath);

        if (!string.IsNullOrWhiteSpace(solutionPath))
        {
            var normalizedSolution = Path.GetFullPath(solutionPath);
            if (normalizedPath.StartsWith(normalizedSolution, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (normalizedPath.StartsWith(normalizedTemp, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
