using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class SyncTools
{
    [McpServerTool(Name = "sync_resx_keys")]
    [Description("Добавить недостающие ключи в System.resx из MTD (Property_, Action_, Enum_, ControlGroup_, Cover, Job, AsyncHandler).")]
    public async Task<string> SyncResxKeys(
        [Description("Путь к директории пакета")] string packagePath,
        [Description("Если true — только показывает, что будет добавлено, без изменения файлов (по умолчанию true)")] bool dryRun = true)
    {
        if (!Directory.Exists(packagePath))
            return $"**ОШИБКА**: Директория не найдена: `{packagePath}`";

        var mtdFiles = Directory.GetFiles(packagePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !string.Equals(Path.GetFileName(f), "Module.mtd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("## Синхронизация ключей System.resx");
        report.AppendLine();
        report.AppendLine($"**Пакет:** `{packagePath}`");
        report.AppendLine($"**Режим:** dryRun={dryRun.ToString().ToLower()}{(dryRun ? " (предварительный просмотр)" : " (запись изменений)")}");
        report.AppendLine();

        int totalEntities = 0;
        int totalKeysAdded = 0;
        int totalFilesChanged = 0;

        // Process entity MTD files
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

                // Collect required keys with default values
                var requiredKeys = new List<(string Key, string Description, string DefaultValue)>();

                // DisplayName
                requiredKeys.Add(("DisplayName", "отображаемое имя", entityName));

                // CollectionDisplayName
                requiredKeys.Add(("CollectionDisplayName", "множественное отображаемое имя", entityName));

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

                        requiredKeys.Add(($"Property_{propName}", "свойство", propName));

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

                                    requiredKeys.Add(($"Enum_{propName}_{enumValName}", "перечисление", enumValName));
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

                        requiredKeys.Add(($"Action_{actionName}", "действие", actionName));
                    }
                }

                // ControlGroups (from Forms)
                if (root.TryGetProperty("Forms", out var formsElement) &&
                    formsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var form in formsElement.EnumerateArray())
                    {
                        if (form.TryGetProperty("Controls", out var controlsEl) &&
                            controlsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var ctrl in controlsEl.EnumerateArray())
                            {
                                var ctrlType = ctrl.TryGetProperty("$type", out var ctT) ? ctT.GetString() ?? "" : "";
                                if (!ctrlType.Contains("ControlGroupMetadata")) continue;

                                if (ctrl.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                                    isAnc.ValueKind == JsonValueKind.True)
                                    continue;

                                var ctrlGuid = ctrl.TryGetProperty("NameGuid", out var cgEl) ? cgEl.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(ctrlGuid))
                                    requiredKeys.Add(($"ControlGroup_{ctrlGuid}", "группа контролов", ""));
                            }
                        }
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
                var keySet = requiredKeys.Select(k => k.Key).ToHashSet();

                var missingInResx = resxFile != null
                    ? GetMissingKeys(resxFile, keySet)
                    : new HashSet<string>();

                var missingInResxRu = resxRuFile != null
                    ? GetMissingKeys(resxRuFile, keySet)
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
                foreach (var (key, desc, _) in missingKeysList)
                    report.AppendLine($"- `{key}` ({desc})");
                report.AppendLine();

                if (!dryRun)
                {
                    int filesChanged = 0;

                    if (resxFile != null && missingInResx.Count > 0)
                    {
                        var keysToAdd = requiredKeys
                            .Where(k => missingInResx.Contains(k.Key))
                            .Select(k => (k.Key, k.DefaultValue))
                            .ToList();
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
                        var keysToAdd = requiredKeys
                            .Where(k => missingInResxRu.Contains(k.Key))
                            .Select(k => (k.Key, k.DefaultValue))
                            .ToList();
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

        // Process Module.mtd files for Cover/Job/AsyncHandler keys
        var moduleMtdFiles = Directory.GetFiles(packagePath, "Module.mtd", SearchOption.AllDirectories);
        foreach (var moduleMtdFile in moduleMtdFiles)
        {
            string mtdContent;
            try
            {
                mtdContent = await File.ReadAllTextAsync(moduleMtdFile);
            }
            catch (Exception ex)
            {
                report.AppendLine($"**ОШИБКА**: Не удалось прочитать `{moduleMtdFile}`: {ex.Message}");
                continue;
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(mtdContent);
            }
            catch (Exception ex)
            {
                report.AppendLine($"**ОШИБКА**: Не удалось разобрать JSON `{moduleMtdFile}`: {ex.Message}");
                continue;
            }

            using (doc)
            {
                var root = doc.RootElement;

                var moduleName = root.TryGetProperty("Name", out var mnProp) ? mnProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(moduleName))
                    continue;

                totalEntities++;

                var requiredKeys = new List<(string Key, string Description, string DefaultValue)>();

                // Cover groups and actions
                if (root.TryGetProperty("Cover", out var coverEl))
                {
                    // Groups
                    if (coverEl.TryGetProperty("Groups", out var groupsEl) &&
                        groupsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var group in groupsEl.EnumerateArray())
                        {
                            if (group.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                                isAnc.ValueKind == JsonValueKind.True)
                                continue;

                            var groupName = group.TryGetProperty("Name", out var gnEl) ? gnEl.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(groupName))
                                requiredKeys.Add(($"CoverGroup_{groupName}", "группа обложки", groupName));

                            // Actions inside groups
                            if (group.TryGetProperty("Actions", out var grpActionsEl) &&
                                grpActionsEl.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var action in grpActionsEl.EnumerateArray())
                                {
                                    if (action.TryGetProperty("IsAncestorMetadata", out var isAncAct) &&
                                        isAncAct.ValueKind == JsonValueKind.True)
                                        continue;

                                    var actionName = action.TryGetProperty("Name", out var anEl) ? anEl.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(actionName))
                                        requiredKeys.Add(($"CoverAction_{actionName}", "действие обложки", actionName));
                                }
                            }
                        }
                    }

                    // Tabs
                    if (coverEl.TryGetProperty("Tabs", out var tabsEl) &&
                        tabsEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var tab in tabsEl.EnumerateArray())
                        {
                            if (tab.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                                isAnc.ValueKind == JsonValueKind.True)
                                continue;

                            var tabName = tab.TryGetProperty("Name", out var tnEl) ? tnEl.GetString() : null;
                            if (!string.IsNullOrWhiteSpace(tabName))
                                requiredKeys.Add(($"CoverTab_{tabName}", "вкладка обложки", tabName));
                        }
                    }
                }

                // Jobs
                if (root.TryGetProperty("Jobs", out var jobsEl) &&
                    jobsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var job in jobsEl.EnumerateArray())
                    {
                        if (job.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                            isAnc.ValueKind == JsonValueKind.True)
                            continue;

                        var jobName = job.TryGetProperty("Name", out var jnEl) ? jnEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(jobName))
                            requiredKeys.Add(($"Job_{jobName}", "фоновый процесс", jobName));
                    }
                }

                // AsyncHandlers
                if (root.TryGetProperty("AsyncHandlers", out var asyncEl) &&
                    asyncEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in asyncEl.EnumerateArray())
                    {
                        if (handler.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                            isAnc.ValueKind == JsonValueKind.True)
                            continue;

                        var handlerName = handler.TryGetProperty("Name", out var hnEl) ? hnEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(handlerName))
                            requiredKeys.Add(($"AsyncHandler_{handlerName}", "асинхронный обработчик", handlerName));
                    }
                }

                // Widgets
                if (root.TryGetProperty("Widgets", out var widgetsEl) &&
                    widgetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var widget in widgetsEl.EnumerateArray())
                    {
                        if (widget.TryGetProperty("IsAncestorMetadata", out var isAnc) &&
                            isAnc.ValueKind == JsonValueKind.True)
                            continue;

                        var widgetName = widget.TryGetProperty("Name", out var wnEl) ? wnEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(widgetName))
                            requiredKeys.Add(($"Widget_{widgetName}", "виджет", widgetName));
                    }
                }

                if (requiredKeys.Count == 0)
                    continue;

                // Find ModuleSystem.resx files
                var moduleDir = Path.GetDirectoryName(moduleMtdFile)!;
                var resxFile = FindResxFile(moduleDir, "ModuleSystem.resx");
                var resxRuFile = FindResxFile(moduleDir, "ModuleSystem.ru.resx");

                if (resxFile == null && resxRuFile == null)
                {
                    report.AppendLine($"### Module: {moduleName}");
                    report.AppendLine("> Файлы `ModuleSystem.resx` и `ModuleSystem.ru.resx` не найдены рядом с `Module.mtd`");
                    report.AppendLine();
                    continue;
                }

                var keySet = requiredKeys.Select(k => k.Key).ToHashSet();

                var missingInResx = resxFile != null
                    ? GetMissingKeys(resxFile, keySet)
                    : new HashSet<string>();

                var missingInResxRu = resxRuFile != null
                    ? GetMissingKeys(resxRuFile, keySet)
                    : new HashSet<string>();

                var allMissingKeys = new HashSet<string>(missingInResx);
                allMissingKeys.UnionWith(missingInResxRu);

                var missingKeysList = requiredKeys
                    .Where(k => allMissingKeys.Contains(k.Key))
                    .ToList();

                if (missingKeysList.Count == 0)
                {
                    report.AppendLine($"### Module: {moduleName} — все ключи присутствуют");
                    report.AppendLine();
                    continue;
                }

                report.AppendLine($"### Module: {moduleName} (ModuleSystem.resx)");
                report.AppendLine($"Добавлено ключей: {missingKeysList.Count}");
                foreach (var (key, desc, _) in missingKeysList)
                    report.AppendLine($"- `{key}` ({desc})");
                report.AppendLine();

                if (!dryRun)
                {
                    int filesChanged = 0;

                    if (resxFile != null && missingInResx.Count > 0)
                    {
                        var keysToAdd = requiredKeys
                            .Where(k => missingInResx.Contains(k.Key))
                            .Select(k => (k.Key, k.DefaultValue))
                            .ToList();
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
                        var keysToAdd = requiredKeys
                            .Where(k => missingInResxRu.Contains(k.Key))
                            .Select(k => (k.Key, k.DefaultValue))
                            .ToList();
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
        report.AppendLine($"- Проверено сущностей/модулей: {totalEntities}");
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

    private static void AddKeysToResxFile(string resxFile, IEnumerable<(string Key, string DefaultValue)> keys)
    {
        var xdoc = XDocument.Load(resxFile);
        var rootEl = xdoc.Root ?? throw new InvalidOperationException("Нет корневого элемента в resx файле.");

        foreach (var (key, defaultValue) in keys)
        {
            var dataEl = new XElement("data",
                new XAttribute("name", key),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", defaultValue));
            rootEl.Add(dataEl);
        }

        xdoc.Save(resxFile);
    }

}
