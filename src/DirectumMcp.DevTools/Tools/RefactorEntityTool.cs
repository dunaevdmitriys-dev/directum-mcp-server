using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class RefactorEntityTool
{
    private static readonly Dictionary<string, string> BaseGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabookEntry"] = "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
        ["Document"] = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
        ["Task"] = "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
        ["Assignment"] = "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24",
        ["Notice"] = "4e09273f-8b3a-489e-814e-a4ebfbba3e6c"
    };

    private static readonly Dictionary<string, string> MetadataTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabookEntry"] = "Sungero.Metadata.EntityMetadata",
        ["Document"] = "Sungero.Metadata.EntityMetadata",
        ["Task"] = "Sungero.Metadata.TaskMetadata",
        ["Assignment"] = "Sungero.Metadata.AssignmentMetadata",
        ["Notice"] = "Sungero.Metadata.NoticeMetadata"
    };

    private static readonly Dictionary<string, string> PropertyTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "Sungero.Metadata.StringPropertyMetadata",
        ["int"] = "Sungero.Metadata.IntegerPropertyMetadata",
        ["double"] = "Sungero.Metadata.DoublePropertyMetadata",
        ["bool"] = "Sungero.Metadata.BooleanPropertyMetadata",
        ["date"] = "Sungero.Metadata.DateTimePropertyMetadata",
        ["text"] = "Sungero.Metadata.TextPropertyMetadata",
        ["navigation"] = "Sungero.Metadata.NavigationPropertyMetadata"
    };

    [McpServerTool(Name = "refactor_entity")]
    [Description("Каскадный рефакторинг: переименование, добавление, удаление свойств в MTD+resx+C#.")]
    public async Task<string> RefactorEntity(
        [Description("Путь к директории сущности (с .mtd файлом)")] string path,
        [Description("Действие: rename_property | add_property | remove_property | change_base_type | extract_to_databook")] string action,
        [Description("Имя свойства")] string? propertyName = null,
        [Description("Новое имя (для rename_property)")] string? newName = null,
        [Description("Тип свойства (для add_property): string, int, bool, date, enum(Val1|Val2)")] string? propertyType = null,
        [Description("Новый базовый тип (для change_base_type)")] string? newBaseType = null,
        [Description("Предпросмотр без записи (по умолчанию: true)")] bool dryRun = true)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "**ОШИБКА**: Параметр `path` не может быть пустым.";

        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория `{path}` не существует.";

        var mtdFiles = Directory.GetFiles(path, "*.mtd");
        if (mtdFiles.Length == 0)
            return $"**ОШИБКА**: В директории `{path}` не найден .mtd файл.";

        var mtdPath = mtdFiles[0];

        return action?.ToLowerInvariant() switch
        {
            "rename_property" => await DoRenameProperty(path, mtdPath, propertyName, newName, dryRun),
            "add_property" => await DoAddProperty(path, mtdPath, propertyName, propertyType, dryRun),
            "remove_property" => await DoRemoveProperty(path, mtdPath, propertyName, dryRun),
            "change_base_type" => await DoChangeBaseType(path, mtdPath, newBaseType, dryRun),
            "extract_to_databook" => await DoExtractToDatabook(path, mtdPath, propertyName, dryRun),
            _ => $"**ОШИБКА**: Неизвестное действие `{action}`. Допустимые: rename_property, add_property, remove_property, change_base_type, extract_to_databook."
        };
    }

    // ===== rename_property =====

    internal async Task<string> DoRenameProperty(string entityDir, string mtdPath, string? propertyName, string? newName, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "**ОШИБКА**: Параметр `propertyName` обязателен для rename_property.";
        if (string.IsNullOrWhiteSpace(newName))
            return "**ОШИБКА**: Параметр `newName` обязателен для rename_property.";

        if (!Regex.IsMatch(propertyName, @"^[A-Za-z][A-Za-z0-9_]*$"))
            return "**ОШИБКА**: `propertyName` должен содержать только латинские буквы, цифры и подчёркивания, начинаться с буквы.";
        if (!Regex.IsMatch(newName, @"^[A-Za-z][A-Za-z0-9_]*$"))
            return "**ОШИБКА**: `newName` должен содержать только латинские буквы, цифры и подчёркивания, начинаться с буквы.";

        var mtdText = await File.ReadAllTextAsync(mtdPath);
        var root = JsonNode.Parse(mtdText, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        if (root == null)
            return "**ОШИБКА**: Не удалось разобрать .mtd файл как JSON.";

        var props = root["Properties"]?.AsArray();
        if (props == null)
            return "**ОШИБКА**: В .mtd файле отсутствует массив Properties.";

        JsonObject? targetProp = null;
        int propIndex = -1;
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i]?.AsObject();
            if (p != null && string.Equals(p["Name"]?.GetValue<string>(), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                targetProp = p;
                propIndex = i;
                break;
            }
        }

        if (targetProp == null)
            return $"**ОШИБКА**: Свойство `{propertyName}` не найдено в MTD файле.";

        var oldCode = targetProp["Code"]?.GetValue<string>();
        var codeChanged = string.Equals(oldCode, propertyName, StringComparison.OrdinalIgnoreCase);

        var changes = new List<string>();
        changes.Add($"MTD: `Name` изменено с `{propertyName}` на `{newName}`");
        if (codeChanged)
            changes.Add($"MTD: `Code` изменено с `{oldCode}` на `{newName}`");

        // Find resx files
        var resxFiles = FindSystemResxFiles(entityDir);
        var resxChanges = new List<(string file, string oldKey, string newKey)>();
        foreach (var resxFile in resxFiles)
        {
            var oldKey = $"Property_{propertyName}";
            var newKey = $"Property_{newName}";
            var resxText = await File.ReadAllTextAsync(resxFile);
            if (resxText.Contains($"name=\"{oldKey}\"", StringComparison.Ordinal))
            {
                resxChanges.Add((Path.GetFileName(resxFile), oldKey, newKey));
                changes.Add($"resx `{Path.GetFileName(resxFile)}`: `{oldKey}` → `{newKey}`");
            }
        }

        // Scan C# files for references
        var csFiles = FindCsFiles(entityDir);
        var csRefs = new List<string>();
        foreach (var csFile in csFiles)
        {
            var csText = await File.ReadAllTextAsync(csFile);
            if (csText.Contains(propertyName, StringComparison.Ordinal))
                csRefs.Add(Path.GetRelativePath(entityDir, csFile));
        }

        if (!dryRun)
        {
            // Update MTD
            targetProp["Name"] = newName;
            if (codeChanged)
                targetProp["Code"] = newName;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var newMtdText = root.ToJsonString(options);
            await File.WriteAllTextAsync(mtdPath, newMtdText);

            // Update resx files
            foreach (var resxFile in resxFiles)
            {
                var oldKey = $"Property_{propertyName}";
                var newKey = $"Property_{newName}";
                var resxText = await File.ReadAllTextAsync(resxFile);
                if (resxText.Contains($"name=\"{oldKey}\"", StringComparison.Ordinal))
                {
                    var updated = resxText.Replace($"name=\"{oldKey}\"", $"name=\"{newKey}\"", StringComparison.Ordinal);
                    await File.WriteAllTextAsync(resxFile, updated);
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Переименование свойства: `{propertyName}` → `{newName}`");
        if (dryRun) sb.AppendLine("> **Режим предпросмотра** — изменения не записаны (dryRun=true)");
        sb.AppendLine();
        sb.AppendLine("### Изменения");
        sb.AppendLine();
        sb.AppendLine("| Файл | Описание |");
        sb.AppendLine("|------|----------|");
        foreach (var ch in changes)
        {
            var parts = ch.Split(':', 2);
            sb.AppendLine($"| {(parts.Length > 1 ? parts[0].Trim() : "—")} | {(parts.Length > 1 ? parts[1].Trim() : ch)} |");
        }
        sb.AppendLine();

        if (csRefs.Count > 0)
        {
            sb.AppendLine("### C# файлы с упоминанием свойства (требуют ручного обновления)");
            sb.AppendLine();
            foreach (var f in csRefs)
                sb.AppendLine($"- `{f}`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ===== add_property =====

    internal async Task<string> DoAddProperty(string entityDir, string mtdPath, string? propertyName, string? propertyType, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "**ОШИБКА**: Параметр `propertyName` обязателен для add_property.";
        if (string.IsNullOrWhiteSpace(propertyType))
            return "**ОШИБКА**: Параметр `propertyType` обязателен для add_property.";

        var mtdText = await File.ReadAllTextAsync(mtdPath);
        var root = JsonNode.Parse(mtdText, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        if (root == null)
            return "**ОШИБКА**: Не удалось разобрать .mtd файл как JSON.";

        var props = root["Properties"]?.AsArray();
        if (props == null)
            return "**ОШИБКА**: В .mtd файле отсутствует массив Properties.";

        // Parse property type
        string metadataType;
        List<string> enumValues = new();

        var enumMatch = Regex.Match(propertyType, @"^enum\((.+)\)$", RegexOptions.IgnoreCase);
        if (enumMatch.Success)
        {
            metadataType = "Sungero.Metadata.EnumPropertyMetadata";
            enumValues = enumMatch.Groups[1].Value
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
        else if (PropertyTypeMap.TryGetValue(propertyType, out var mapped))
        {
            metadataType = mapped;
        }
        else
        {
            metadataType = "Sungero.Metadata.StringPropertyMetadata";
        }

        var newGuid = Guid.NewGuid().ToString("D");
        var newPropNode = new JsonObject
        {
            ["$type"] = $"{metadataType}, Sungero.Metadata",
            ["NameGuid"] = newGuid,
            ["Name"] = propertyName,
            ["Code"] = propertyName,
            ["IsRequired"] = false
        };

        if (enumValues.Count > 0)
        {
            var directValues = new JsonArray();
            foreach (var val in enumValues)
            {
                var valNode = new JsonObject
                {
                    ["NameGuid"] = Guid.NewGuid().ToString("D"),
                    ["Name"] = val
                };
                directValues.Add(valNode);
            }
            newPropNode["DirectValues"] = directValues;
        }

        var changes = new List<string>
        {
            $"MTD: добавлено свойство `{propertyName}` типа `{metadataType}` (GUID: {newGuid})"
        };

        if (enumValues.Count > 0)
            changes.Add($"MTD: значения перечисления: {string.Join(", ", enumValues.Select(v => $"`{v}`"))}");

        var resxFiles = FindSystemResxFiles(entityDir);
        foreach (var resxFile in resxFiles)
            changes.Add($"resx `{Path.GetFileName(resxFile)}`: добавлен ключ `Property_{propertyName}`");

        if (!dryRun)
        {
            props.Add(newPropNode);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(mtdPath, root.ToJsonString(options));

            foreach (var resxFile in resxFiles)
            {
                var resxText = await File.ReadAllTextAsync(resxFile);
                var keyNode = $"  <data name=\"Property_{propertyName}\" xml:space=\"preserve\">\n    <value>{propertyName}</value>\n  </data>";
                resxText = InsertDataNodeBeforeRootClose(resxText, keyNode);
                await File.WriteAllTextAsync(resxFile, resxText);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Добавление свойства: `{propertyName}` ({propertyType})");
        if (dryRun) sb.AppendLine("> **Режим предпросмотра** — изменения не записаны (dryRun=true)");
        sb.AppendLine();
        sb.AppendLine("### Изменения");
        sb.AppendLine();
        foreach (var ch in changes)
            sb.AppendLine($"- {ch}");
        sb.AppendLine();

        return sb.ToString();
    }

    // ===== remove_property =====

    internal async Task<string> DoRemoveProperty(string entityDir, string mtdPath, string? propertyName, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "**ОШИБКА**: Параметр `propertyName` обязателен для remove_property.";

        var mtdText = await File.ReadAllTextAsync(mtdPath);
        var root = JsonNode.Parse(mtdText, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        if (root == null)
            return "**ОШИБКА**: Не удалось разобрать .mtd файл как JSON.";

        var props = root["Properties"]?.AsArray();
        if (props == null)
            return "**ОШИБКА**: В .mtd файле отсутствует массив Properties.";

        int propIndex = -1;
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i]?.AsObject();
            if (p != null && string.Equals(p["Name"]?.GetValue<string>(), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propIndex = i;
                break;
            }
        }

        if (propIndex < 0)
            return $"**ОШИБКА**: Свойство `{propertyName}` не найдено в MTD файле.";

        var changes = new List<string>
        {
            $"MTD: удалено свойство `{propertyName}` (индекс {propIndex})"
        };

        var resxFiles = FindSystemResxFiles(entityDir);
        foreach (var resxFile in resxFiles)
        {
            var resxText = await File.ReadAllTextAsync(resxFile);
            if (resxText.Contains($"name=\"Property_{propertyName}\"", StringComparison.Ordinal))
                changes.Add($"resx `{Path.GetFileName(resxFile)}`: удалён ключ `Property_{propertyName}`");
        }

        // Scan C# files for warnings
        var csFiles = FindCsFiles(entityDir);
        var csRefs = new List<string>();
        foreach (var csFile in csFiles)
        {
            var csText = await File.ReadAllTextAsync(csFile);
            if (csText.Contains(propertyName, StringComparison.Ordinal))
                csRefs.Add(Path.GetRelativePath(entityDir, csFile));
        }

        if (!dryRun)
        {
            props.RemoveAt(propIndex);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(mtdPath, root.ToJsonString(options));

            foreach (var resxFile in resxFiles)
            {
                var resxText = await File.ReadAllTextAsync(resxFile);
                resxText = RemoveResxDataNode(resxText, $"Property_{propertyName}");
                await File.WriteAllTextAsync(resxFile, resxText);
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Удаление свойства: `{propertyName}`");
        if (dryRun) sb.AppendLine("> **Режим предпросмотра** — изменения не записаны (dryRun=true)");
        sb.AppendLine();
        sb.AppendLine("### Изменения");
        sb.AppendLine();
        foreach (var ch in changes)
            sb.AppendLine($"- {ch}");
        sb.AppendLine();

        if (csRefs.Count > 0)
        {
            sb.AppendLine("### Предупреждения: C# файлы с упоминанием свойства (удалять вручную)");
            sb.AppendLine();
            foreach (var f in csRefs)
                sb.AppendLine($"- `{f}`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ===== change_base_type =====

    internal async Task<string> DoChangeBaseType(string entityDir, string mtdPath, string? newBaseType, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(newBaseType))
            return "**ОШИБКА**: Параметр `newBaseType` обязателен для change_base_type.";

        if (!BaseGuids.TryGetValue(newBaseType, out var newBaseGuid))
            return $"**ОШИБКА**: Неизвестный базовый тип `{newBaseType}`. Допустимые: {string.Join(", ", BaseGuids.Keys)}.";

        var newMetadataType = MetadataTypes[newBaseType];

        var mtdText = await File.ReadAllTextAsync(mtdPath);
        var root = JsonNode.Parse(mtdText, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        if (root == null)
            return "**ОШИБКА**: Не удалось разобрать .mtd файл как JSON.";

        var rootObj = root.AsObject();
        var oldBaseGuid = rootObj["BaseGuid"]?.GetValue<string>() ?? "(не задан)";
        var oldType = rootObj["$type"]?.GetValue<string>() ?? "(не задан)";

        var changes = new List<string>
        {
            $"MTD: `BaseGuid` изменён с `{oldBaseGuid}` на `{newBaseGuid}`",
            $"MTD: `$type` изменён с `{oldType}` на `{newMetadataType}, Sungero.Metadata`"
        };

        if (!dryRun)
        {
            rootObj["BaseGuid"] = newBaseGuid;
            rootObj["$type"] = $"{newMetadataType}, Sungero.Metadata";
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(mtdPath, root.ToJsonString(options));
        }

        // Determine manual review notes
        var reviewNotes = new List<string>();
        if (newBaseType is "DatabookEntry" or "Document")
            reviewNotes.Add("Проверьте ClientBase/\\*ClientBaseFunctions.cs — необходимы для DatabookEntry/Document");
        if (newBaseType is "Task" or "Assignment" or "Notice")
            reviewNotes.Add("Удалите ClientBase/\\*ClientBaseFunctions.cs — не нужны для Task/Assignment/Notice");
        reviewNotes.Add("Проверьте Module.mtd на корректность ссылок на сущность");

        var sb = new StringBuilder();
        sb.AppendLine($"## Изменение базового типа на: `{newBaseType}`");
        if (dryRun) sb.AppendLine("> **Режим предпросмотра** — изменения не записаны (dryRun=true)");
        sb.AppendLine();
        sb.AppendLine("### Изменения");
        sb.AppendLine();
        foreach (var ch in changes)
            sb.AppendLine($"- {ch}");
        sb.AppendLine();

        sb.AppendLine("### Требуют ручного просмотра");
        sb.AppendLine();
        foreach (var n in reviewNotes)
            sb.AppendLine($"- {n}");
        sb.AppendLine();

        return sb.ToString();
    }

    // ===== extract_to_databook =====

    internal async Task<string> DoExtractToDatabook(string entityDir, string mtdPath, string? propertyName, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "**ОШИБКА**: Параметр `propertyName` обязателен для extract_to_databook.";

        var mtdText = await File.ReadAllTextAsync(mtdPath);
        var root = JsonNode.Parse(mtdText, new JsonNodeOptions { PropertyNameCaseInsensitive = true });
        if (root == null)
            return "**ОШИБКА**: Не удалось разобрать .mtd файл как JSON.";

        var props = root["Properties"]?.AsArray();
        if (props == null)
            return "**ОШИБКА**: В .mtd файле отсутствует массив Properties.";

        int propIndex = -1;
        JsonObject? targetProp = null;
        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i]?.AsObject();
            if (p != null && string.Equals(p["Name"]?.GetValue<string>(), propertyName, StringComparison.OrdinalIgnoreCase))
            {
                targetProp = p;
                propIndex = i;
                break;
            }
        }

        if (targetProp == null)
            return $"**ОШИБКА**: Свойство `{propertyName}` не найдено в MTD файле.";

        var propType = targetProp["$type"]?.GetValue<string>() ?? "";
        var isString = propType.Contains("StringPropertyMetadata", StringComparison.OrdinalIgnoreCase);
        var isEnum = propType.Contains("EnumPropertyMetadata", StringComparison.OrdinalIgnoreCase);

        if (!isString && !isEnum)
            return $"**ОШИБКА**: Свойство `{propertyName}` имеет тип `{propType}`. extract_to_databook поддерживает только string и enum свойства.";

        var parentEntityName = root["Name"]?.GetValue<string>() ?? "Entity";
        var parentModuleName = root["ModuleName"]?.GetValue<string>() ?? "Module";
        var newEntityName = propertyName;
        var newEntityGuid = Guid.NewGuid().ToString("D");
        var newNavPropGuid = Guid.NewGuid().ToString("D");
        var databookBaseGuid = BaseGuids["DatabookEntry"];

        // Target directory for new entity
        var parentDir = Path.GetDirectoryName(entityDir) ?? entityDir;
        var newEntityDir = Path.Combine(parentDir, newEntityName);

        var createdFiles = new List<string>();
        var modifiedFiles = new List<string>();

        // New entity MTD content
        var newMtdContent = BuildDatabookMtd(newEntityName, newEntityGuid, databookBaseGuid, parentModuleName);
        var newMtdPath = Path.Combine(newEntityDir, $"{newEntityName}.mtd");

        // New entity resx content
        var newResxContent = BuildSimpleSystemResx(newEntityName);
        var newResxPath = Path.Combine(newEntityDir, $"{newEntityName}System.resx");
        var newResxRuPath = Path.Combine(newEntityDir, $"{newEntityName}System.ru.resx");

        createdFiles.Add(newMtdPath);
        createdFiles.Add(newResxPath);
        createdFiles.Add(newResxRuPath);

        // Build new navigation property node replacing original property
        var navPropNode = new JsonObject
        {
            ["$type"] = "Sungero.Metadata.NavigationPropertyMetadata, Sungero.Metadata",
            ["NameGuid"] = newNavPropGuid,
            ["Name"] = propertyName,
            ["Code"] = propertyName,
            ["EntityGuid"] = newEntityGuid,
            ["IsRequired"] = false
        };

        modifiedFiles.Add(mtdPath);

        if (!dryRun)
        {
            Directory.CreateDirectory(newEntityDir);
            await File.WriteAllTextAsync(newMtdPath, newMtdContent);
            await File.WriteAllTextAsync(newResxPath, newResxContent);
            await File.WriteAllTextAsync(newResxRuPath, newResxContent);

            // Replace old property with navigation property in parent MTD
            props[propIndex] = navPropNode;
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(mtdPath, root.ToJsonString(options));
        }

        var sb = new StringBuilder();
        sb.AppendLine($"## Извлечение свойства `{propertyName}` в отдельный справочник");
        if (dryRun) sb.AppendLine("> **Режим предпросмотра** — изменения не записаны (dryRun=true)");
        sb.AppendLine();
        sb.AppendLine($"**Новая сущность:** `{newEntityName}` (DatabookEntry)");
        sb.AppendLine($"**GUID новой сущности:** `{newEntityGuid}`");
        sb.AppendLine($"**Расположение:** `{newEntityDir}`");
        sb.AppendLine();
        sb.AppendLine("### Созданные файлы");
        sb.AppendLine();
        foreach (var f in createdFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();
        sb.AppendLine("### Изменённые файлы");
        sb.AppendLine();
        foreach (var f in modifiedFiles)
            sb.AppendLine($"- `{f}` — свойство `{propertyName}` заменено на NavigationPropertyMetadata → `{newEntityName}`");
        sb.AppendLine();
        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine($"1. Добавьте `{newEntityName}` в Module.mtd");
        sb.AppendLine("2. Запустите check_package для валидации");

        return sb.ToString();
    }

    // ===== Helpers =====

    internal static string[] FindSystemResxFiles(string entityDir)
    {
        return Directory.GetFiles(entityDir, "*System.resx", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(entityDir, "*System.ru.resx", SearchOption.TopDirectoryOnly))
            .Distinct()
            .OrderBy(f => f)
            .ToArray();
    }

    internal static string[] FindCsFiles(string entityDir)
    {
        return Directory.GetFiles(entityDir, "*.cs", SearchOption.AllDirectories);
    }

    private static string InsertDataNodeBeforeRootClose(string xml, string dataNode)
    {
        var closeRoot = xml.LastIndexOf("</root>", StringComparison.Ordinal);
        if (closeRoot >= 0)
            return xml[..closeRoot] + dataNode + "\n</root>";
        return xml + "\n" + dataNode;
    }

    private static string RemoveResxDataNode(string xml, string keyName)
    {
        // Remove the entire <data name="KEY" ...>...</data> block
        var pattern = $@"\s*<data\s+name=""{Regex.Escape(keyName)}""[^>]*>.*?</data>";
        return Regex.Replace(xml, pattern, "", RegexOptions.Singleline);
    }

    private static string BuildDatabookMtd(string entityName, string entityGuid, string baseGuid, string moduleName)
    {
        return $@"{{
  ""$type"": ""Sungero.Metadata.EntityMetadata, Sungero.Metadata"",
  ""NameGuid"": ""{entityGuid}"",
  ""Name"": ""{entityName}"",
  ""BaseGuid"": ""{baseGuid}"",
  ""ModuleName"": ""{moduleName}"",
  ""Actions"": [],
  ""Properties"": [
    {{
      ""$type"": ""Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata"",
      ""NameGuid"": ""{Guid.NewGuid():D}"",
      ""Name"": ""Name"",
      ""Code"": ""Name"",
      ""IsRequired"": false
    }}
  ]
}}
";
    }

    private static string BuildSimpleSystemResx(string entityName)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<root>
  <resheader name=""resmimetype"">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name=""version"">
    <value>2.0</value>
  </resheader>
  <data name=""DisplayName"" xml:space=""preserve"">
    <value>{entityName}</value>
  </data>
  <data name=""Property_Name"" xml:space=""preserve"">
    <value>Name</value>
  </data>
</root>
";
    }
}
