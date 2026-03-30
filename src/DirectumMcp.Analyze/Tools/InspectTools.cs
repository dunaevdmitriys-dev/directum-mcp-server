using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

// [McpServerToolType] // Hidden: redundant with Claude Code native capabilities
public class InspectTools
{
    [McpServerTool(Name = "inspect")]
    [Description("Универсальный инструмент чтения метаданных Directum RX — MTD сущности, MTD модуля, resx, директория модуля")]
    public async Task<string> Inspect(
        [Description("Путь к файлу (.mtd, .resx) или директории модуля")] string path)
    {
        if (File.Exists(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mtd")
                return await HandleMtdFile(path);
            if (ext == ".resx")
                return await HandleResxFile(path);
            return $"**ОШИБКА**: Неподдерживаемый тип файла: `{ext}`. Поддерживаются: `.mtd`, `.resx`, или директория модуля.";
        }

        if (Directory.Exists(path))
            return await HandleDirectory(path);

        return $"**ОШИБКА**: Путь не найден: `{path}`";
    }

    private static async Task<string> HandleMtdFile(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var metaType = root.GetStringProp("$type");
        var sb = new StringBuilder();

        if (metaType.Contains("ModuleMetadata"))
            FormatModule(root, sb);
        else if (metaType.Contains("Metadata"))
            FormatEntity(root, sb);
        else
            sb.AppendLine($"**ОШИБКА**: Не удалось определить тип MTD. `$type`: `{metaType}`");

        return sb.ToString();
    }

    private static void FormatEntity(JsonElement root, StringBuilder sb)
    {
        var name = root.GetStringProp("Name");
        var nameGuid = root.GetStringProp("NameGuid");
        var baseGuid = root.GetStringProp("BaseGuid");
        var metaType = root.GetStringProp("$type");
        var isAbstract = root.TryGetProperty("IsAbstract", out var abs) && abs.ValueKind == JsonValueKind.True;
        var version = root.GetStringProp("Version");
        var integrationName = root.GetStringProp("IntegrationServiceName");

        var entityKind = metaType switch
        {
            var t when t.Contains("TaskMetadata") => "Task (задача)",
            var t when t.Contains("AssignmentMetadata") => "Assignment (задание)",
            var t when t.Contains("NoticeMetadata") => "Notice (уведомление)",
            var t when t.Contains("ReportMetadata") => "Report (отчёт)",
            _ => ResolveEntityKindFromBase(baseGuid)
        };

        sb.AppendLine($"## Сущность: {name}");
        sb.AppendLine();
        sb.AppendLine("| Поле | Значение |");
        sb.AppendLine("|------|----------|");
        sb.AppendLine($"| GUID | `{nameGuid}` |");
        sb.AppendLine($"| Тип | {entityKind} |");
        sb.AppendLine($"| Базовый тип | `{baseGuid}` |");
        sb.AppendLine($"| Абстрактный | {(isAbstract ? "да" : "нет")} |");
        if (!string.IsNullOrEmpty(version))
            sb.AppendLine($"| Версия | {version} |");
        sb.AppendLine($"| IntegrationService | {(string.IsNullOrEmpty(integrationName) ? "не настроено" : integrationName)} |");
        sb.AppendLine();

        // Properties
        if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            var propList = props.EnumerateArray().ToList();
            if (propList.Count > 0)
            {
                sb.AppendLine($"### Свойства ({propList.Count})");
                sb.AppendLine();
                sb.AppendLine("| Свойство | Тип | Code | Обязательное | Описание |");
                sb.AppendLine("|----------|-----|------|-------------|----------|");

                foreach (var prop in propList)
                {
                    var propType = prop.GetStringProp("$type");
                    var propName = prop.GetStringProp("Name");
                    var propCode = prop.GetStringProp("Code");
                    var isRequired = prop.TryGetProperty("IsRequired", out var req) && req.ValueKind == JsonValueKind.True;
                    var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var anc) && anc.ValueKind == JsonValueKind.True;

                    var shortType = ExtractPropertyType(propType);
                    var extras = new List<string>();

                    if (isAncestor) extras.Add("inherited");
                    if (prop.TryGetProperty("EntityGuid", out var eg) && eg.ValueKind == JsonValueKind.String)
                        extras.Add($"-> `{eg.GetString()}`");
                    if (prop.TryGetProperty("IsDisplayValue", out var dv) && dv.ValueKind == JsonValueKind.True)
                        extras.Add("display");
                    if (prop.TryGetProperty("DirectValues", out var vals) && vals.ValueKind == JsonValueKind.Array)
                    {
                        var valNames = vals.EnumerateArray()
                            .Select(v => v.GetStringProp("Name"))
                            .Where(v => !string.IsNullOrEmpty(v));
                        extras.Add($"values: [{string.Join(", ", valNames)}]");
                    }

                    sb.AppendLine($"| {propName} | {shortType} | {propCode} | {(isRequired ? "да" : "")} | {string.Join("; ", extras)} |");
                }
                sb.AppendLine();
            }
        }

        // Actions
        if (root.TryGetProperty("Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            var actionList = actions.EnumerateArray().ToList();
            if (actionList.Count > 0)
            {
                sb.AppendLine($"### Действия ({actionList.Count})");
                sb.AppendLine();
                sb.AppendLine("| Действие | IsAncestor |");
                sb.AppendLine("|----------|-----------|");
                foreach (var action in actionList)
                {
                    var actionName = action.GetStringProp("Name");
                    var isAncestor = action.TryGetProperty("IsAncestorMetadata", out var anc) && anc.ValueKind == JsonValueKind.True;
                    sb.AppendLine($"| {actionName} | {(isAncestor ? "да" : "нет")} |");
                }
                sb.AppendLine();
            }
        }

        // Forms
        if (root.TryGetProperty("Forms", out var forms) && forms.ValueKind == JsonValueKind.Array)
        {
            var formList = forms.EnumerateArray().ToList();
            if (formList.Count > 0)
            {
                sb.AppendLine("### Формы");
                sb.AppendLine();
                foreach (var form in formList)
                {
                    var formName = form.GetStringProp("Name");
                    var formType = form.GetStringProp("$type");
                    var shortFormType = formType.Split(',')[0].Split('.').LastOrDefault() ?? formType;
                    sb.Append($"- `{formName}` ({shortFormType})");
                    if (form.TryGetProperty("Controls", out var controls) && controls.ValueKind == JsonValueKind.Array)
                    {
                        var count = controls.EnumerateArray().Count();
                        sb.Append($" — {count} контролов");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
        }

        // HandledEvents
        if (root.TryGetProperty("HandledEvents", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            var eventList = events.EnumerateArray()
                .Select(e => e.GetString())
                .Where(e => e != null)
                .ToList();
            if (eventList.Count > 0)
            {
                sb.AppendLine("### Обработчики событий");
                sb.AppendLine();
                foreach (var ev in eventList)
                    sb.AppendLine($"- `{ev}`");
                sb.AppendLine();
            }
        }

        // AttachmentGroups
        if (root.TryGetProperty("AttachmentGroups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            var groupList = groups.EnumerateArray().ToList();
            if (groupList.Count > 0)
            {
                sb.AppendLine("### Группы вложений");
                sb.AppendLine();
                foreach (var group in groupList)
                {
                    var groupName = group.GetStringProp("Name");
                    sb.Append($"- `{groupName}`");
                    if (group.TryGetProperty("Constraints", out var constraints) && constraints.ValueKind == JsonValueKind.Array)
                    {
                        var cList = constraints.EnumerateArray().ToList();
                        if (cList.Count > 0)
                        {
                            var cNames = cList.Select(c => c.GetStringProp("Name")).Where(n => !string.IsNullOrEmpty(n));
                            sb.Append($" — ограничения: [{string.Join(", ", cNames)}]");
                        }
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
        }

        // PublicFunctions
        if (root.TryGetProperty("PublicFunctions", out var funcs) && funcs.ValueKind == JsonValueKind.Array)
        {
            var funcList = funcs.EnumerateArray().ToList();
            if (funcList.Count > 0)
            {
                sb.AppendLine("### Публичные функции");
                sb.AppendLine();
                foreach (var func in funcList)
                {
                    var funcName = func.GetStringProp("Name");
                    var returnType = func.GetStringProp("ReturnType").Replace("global::", "");
                    var paramStr = "";
                    if (func.TryGetProperty("Parameters", out var pars) && pars.ValueKind == JsonValueKind.Array)
                    {
                        var parList = pars.EnumerateArray()
                            .Select(p => $"{p.GetStringProp("ParameterType").Replace("global::", "")} {p.GetStringProp("Name")}");
                        paramStr = string.Join(", ", parList);
                    }
                    sb.AppendLine($"- `{returnType} {funcName}({paramStr})`");
                }
                sb.AppendLine();
            }
        }

        // Blocks (workflow)
        if (root.TryGetProperty("Blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
        {
            var blockList = blocks.EnumerateArray().ToList();
            if (blockList.Count > 0)
            {
                sb.AppendLine("### Блоки (workflow)");
                sb.AppendLine();
                foreach (var block in blockList)
                {
                    var blockName = block.GetStringProp("Name");
                    var blockType = block.GetStringProp("$type");
                    var shortBlockType = blockType.Split(',')[0].Split('.').LastOrDefault() ?? blockType;
                    sb.AppendLine($"- `{blockName}` ({shortBlockType})");
                }
                sb.AppendLine();
            }
        }
    }

    private static void FormatModule(JsonElement root, StringBuilder sb)
    {
        var name = root.GetStringProp("Name");
        var nameGuid = root.GetStringProp("NameGuid");
        var version = root.GetStringProp("Version");

        sb.AppendLine($"## Модуль: {name}");
        sb.AppendLine();
        sb.AppendLine("| Поле | Значение |");
        sb.AppendLine("|------|----------|");
        sb.AppendLine($"| GUID | `{nameGuid}` |");
        if (!string.IsNullOrEmpty(version))
            sb.AppendLine($"| Версия | {version} |");
        sb.AppendLine();

        // Dependencies
        if (root.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            var depList = deps.EnumerateArray().ToList();
            if (depList.Count > 0)
            {
                sb.AppendLine($"### Зависимости ({depList.Count})");
                sb.AppendLine();
                sb.AppendLine("| GUID |");
                sb.AppendLine("|------|");
                foreach (var dep in depList)
                {
                    var id = dep.GetStringProp("Id");
                    sb.AppendLine($"| `{id}` |");
                }
                sb.AppendLine();
            }
        }

        // Jobs
        if (root.TryGetProperty("Jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array)
        {
            var jobList = jobs.EnumerateArray().ToList();
            if (jobList.Count > 0)
            {
                sb.AppendLine($"### Jobs ({jobList.Count})");
                sb.AppendLine();
                sb.AppendLine("| Job | Описание |");
                sb.AppendLine("|-----|----------|");
                foreach (var j in jobList)
                {
                    var jName = j.GetStringProp("Name");
                    var jDesc = j.GetStringProp("Description");
                    sb.AppendLine($"| {jName} | {jDesc} |");
                }
                sb.AppendLine();
            }
        }

        // AsyncHandlers
        if (root.TryGetProperty("AsyncHandlers", out var handlers) && handlers.ValueKind == JsonValueKind.Array)
        {
            var handlerList = handlers.EnumerateArray().ToList();
            if (handlerList.Count > 0)
            {
                sb.AppendLine($"### AsyncHandlers ({handlerList.Count})");
                sb.AppendLine();
                sb.AppendLine("| Handler | Параметры |");
                sb.AppendLine("|---------|-----------|");
                foreach (var h in handlerList)
                {
                    var hName = h.GetStringProp("Name");
                    var paramNames = new List<string>();
                    if (h.TryGetProperty("Parameters", out var pars) && pars.ValueKind == JsonValueKind.Array)
                    {
                        paramNames = pars.EnumerateArray()
                            .Select(p => p.GetStringProp("Name"))
                            .Where(n => !string.IsNullOrEmpty(n))
                            .ToList();
                    }
                    sb.AppendLine($"| {hName} | {string.Join(", ", paramNames)} |");
                }
                sb.AppendLine();
            }
        }

        // Cover
        if (root.TryGetProperty("Cover", out var cover))
        {
            if (cover.TryGetProperty("Groups", out var cGroups) && cGroups.ValueKind == JsonValueKind.Array)
            {
                var groupList = cGroups.EnumerateArray().ToList();
                if (groupList.Count > 0)
                {
                    sb.AppendLine("### Cover (обложка)");
                    sb.AppendLine();
                    foreach (var g in groupList)
                    {
                        var gName = g.GetStringProp("Name");
                        sb.AppendLine($"#### Группа: {gName}");
                        if (g.TryGetProperty("Actions", out var cActions) && cActions.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var a in cActions.EnumerateArray())
                            {
                                var aName = a.GetStringProp("Name");
                                var aType = a.GetStringProp("$type");
                                var shortType = aType.Split(',')[0].Split('.').LastOrDefault() ?? aType;
                                var funcName = a.GetStringProp("FunctionName");
                                var funcSuffix = string.IsNullOrEmpty(funcName) ? "" : $" (FunctionName: {funcName})";
                                sb.AppendLine($"  - `{aName}` [{shortType}]{funcSuffix}");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // PublicStructures
        if (root.TryGetProperty("PublicStructures", out var structs) && structs.ValueKind == JsonValueKind.Array)
        {
            var structList = structs.EnumerateArray().ToList();
            if (structList.Count > 0)
            {
                sb.AppendLine("### PublicStructures");
                sb.AppendLine();
                foreach (var s in structList)
                {
                    var sName = s.GetStringProp("Name");
                    sb.AppendLine($"- `{sName}`");
                }
                sb.AppendLine();
            }
        }

        // PublicFunctions
        if (root.TryGetProperty("PublicFunctions", out var funcs) && funcs.ValueKind == JsonValueKind.Array)
        {
            var funcList = funcs.EnumerateArray().ToList();
            if (funcList.Count > 0)
            {
                sb.AppendLine("### Публичные функции модуля");
                sb.AppendLine();
                foreach (var func in funcList)
                {
                    var funcName = func.GetStringProp("Name");
                    var returnType = func.GetStringProp("ReturnType").Replace("global::", "");
                    sb.AppendLine($"- `{returnType} {funcName}(...)`");
                }
                sb.AppendLine();
            }
        }
    }

    private static async Task<string> HandleResxFile(string path)
    {
        var sb = new StringBuilder();
        var fileName = Path.GetFileName(path);
        var isSystem = fileName.Contains("System", StringComparison.OrdinalIgnoreCase);

        // Determine paired file
        string pairedPath;
        if (path.EndsWith(".ru.resx", StringComparison.OrdinalIgnoreCase))
            pairedPath = path[..^".ru.resx".Length] + ".resx";
        else
            pairedPath = path[..^".resx".Length] + ".ru.resx";
        var pairedExists = File.Exists(pairedPath);

        var xml = await File.ReadAllTextAsync(path);
        var xdoc = XDocument.Parse(xml);
        var dataElements = xdoc.Descendants("data").ToList();

        sb.AppendLine($"## Ресурсный файл: {fileName}");
        sb.AppendLine();
        sb.AppendLine("| Поле | Значение |");
        sb.AppendLine("|------|----------|");
        sb.AppendLine($"| Тип | {(isSystem ? "System.resx" : "обычный .resx")} |");
        sb.AppendLine($"| Ключей | {dataElements.Count} |");
        sb.AppendLine($"| Парный файл (.{(path.EndsWith(".ru.resx", StringComparison.OrdinalIgnoreCase) ? "" : "ru.")}resx) | {(pairedExists ? "существует" : "отсутствует")} |");
        sb.AppendLine();

        // Categorize keys
        var categories = new Dictionary<string, List<string>>
        {
            ["Property_"] = [],
            ["Action_"] = [],
            ["Enum_"] = [],
            ["ControlGroup_"] = [],
            ["Form_"] = [],
            ["Ribbon_"] = [],
            ["FilterPanel_"] = [],
        };
        var otherKeys = new List<string>();
        var problemKeys = new List<(string Key, string Value)>();

        foreach (var data in dataElements)
        {
            var key = data.Attribute("name")?.Value ?? "";
            var value = data.Element("value")?.Value ?? "";

            var categorized = false;
            foreach (var (prefix, list) in categories)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    list.Add(key);
                    categorized = true;
                    break;
                }
            }
            if (!categorized)
                otherKeys.Add(key);

            // Detect Resource_<GUID> pattern
            if (key.StartsWith("Resource_", StringComparison.Ordinal) && key.Length > 20)
                problemKeys.Add((key, value));
        }

        sb.AppendLine("### Ключи по категориям");
        sb.AppendLine();
        sb.AppendLine("| Категория | Количество | Примеры |");
        sb.AppendLine("|-----------|-----------|---------|");
        foreach (var (prefix, list) in categories)
        {
            if (list.Count > 0)
            {
                var examples = string.Join(", ", list.Take(3));
                sb.AppendLine($"| {prefix} | {list.Count} | {examples} |");
            }
        }
        if (otherKeys.Count > 0)
        {
            var examples = string.Join(", ", otherKeys.Take(3));
            sb.AppendLine($"| Другие | {otherKeys.Count} | {examples} |");
        }
        sb.AppendLine();

        // Problems
        if (problemKeys.Count > 0)
        {
            sb.AppendLine("### Проблемы");
            sb.AppendLine();
            foreach (var (key, value) in problemKeys)
                sb.AppendLine($"- `{key}` (значение: \"{value}\") — должен быть `Property_<Name>`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static async Task<string> HandleDirectory(string path)
    {
        var sb = new StringBuilder();
        var dirName = new DirectoryInfo(path).Name;

        sb.AppendLine($"## Модуль: {dirName}");
        sb.AppendLine();

        // Find Module.mtd
        var moduleMtdFiles = Directory.GetFiles(path, "Module.mtd", SearchOption.AllDirectories);
        string? moduleDepsSection = null;

        if (moduleMtdFiles.Length > 0)
        {
            var moduleJson = await File.ReadAllTextAsync(moduleMtdFiles[0]);
            using var moduleDoc = JsonDocument.Parse(moduleJson);
            var moduleRoot = moduleDoc.RootElement;

            if (moduleRoot.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                var depList = deps.EnumerateArray().ToList();
                if (depList.Count > 0)
                {
                    var depSb = new StringBuilder();
                    depSb.AppendLine($"### Зависимости модуля ({depList.Count})");
                    depSb.AppendLine();
                    foreach (var dep in depList)
                    {
                        var id = dep.GetStringProp("Id");
                        depSb.AppendLine($"- `{id}`");
                    }
                    depSb.AppendLine();
                    moduleDepsSection = depSb.ToString();
                }
            }
        }

        // Find all entity MTD files
        var allMtdFiles = Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories);
        var entityInfos = new List<(string Name, string Kind, int PropCount, int ActionCount)>();

        foreach (var mtdFile in allMtdFiles)
        {
            if (Path.GetFileName(mtdFile).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var metaType = root.GetStringProp("$type");
                if (metaType.Contains("ModuleMetadata"))
                    continue;

                var eName = root.GetStringProp("Name");
                var baseGuid = root.GetStringProp("BaseGuid");

                var kind = metaType switch
                {
                    var t when t.Contains("TaskMetadata") => "Task",
                    var t when t.Contains("AssignmentMetadata") => "Assignment",
                    var t when t.Contains("NoticeMetadata") => "Notice",
                    var t when t.Contains("ReportMetadata") => "Report",
                    _ => DirectumConstants.ResolveBaseType(baseGuid) is "Unknown" ? "Entity" : DirectumConstants.ResolveBaseType(baseGuid)
                };

                var propCount = 0;
                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                    propCount = props.EnumerateArray().Count();

                var actionCount = 0;
                if (root.TryGetProperty("Actions", out var acts) && acts.ValueKind == JsonValueKind.Array)
                    actionCount = acts.EnumerateArray().Count();

                entityInfos.Add((eName, kind, propCount, actionCount));
            }
            catch
            {
                // Skip unparseable files
            }
        }

        if (entityInfos.Count > 0)
        {
            sb.AppendLine($"### Сущности ({entityInfos.Count})");
            sb.AppendLine();
            sb.AppendLine("| Сущность | Тип | Свойств | Действий |");
            sb.AppendLine("|----------|-----|---------|----------|");
            foreach (var (eName, kind, propCount, actionCount) in entityInfos.OrderBy(e => e.Name))
                sb.AppendLine($"| {eName} | {kind} | {propCount} | {actionCount} |");
            sb.AppendLine();
        }

        // File structure
        var serverFiles = CountCsFiles(path, "Server");
        var clientBaseFiles = CountCsFiles(path, "ClientBase");
        var sharedFiles = CountCsFiles(path, "Shared");

        sb.AppendLine("### Структура файлов");
        sb.AppendLine();
        sb.AppendLine($"- Server/ ({serverFiles} .cs файлов)");
        sb.AppendLine($"- ClientBase/ ({clientBaseFiles} .cs файлов)");
        sb.AppendLine($"- Shared/ ({sharedFiles} .cs файлов)");
        sb.AppendLine();

        // Module dependencies
        if (moduleDepsSection != null)
            sb.Append(moduleDepsSection);

        return sb.ToString();
    }

    private static int CountCsFiles(string basePath, string subDir)
    {
        var dir = Path.Combine(basePath, subDir);
        if (!Directory.Exists(dir))
            return 0;
        return Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
    }

    private static string ExtractPropertyType(string fullType)
    {
        var className = fullType.Split(',')[0].Split('.').LastOrDefault() ?? fullType;
        return className
            .Replace("PropertyMetadata", "")
            .Replace("Metadata", "");
    }

    private static string ResolveEntityKindFromBase(string baseGuid)
    {
        var resolved = DirectumConstants.ResolveBaseType(baseGuid);
        return resolved == "Unknown" ? "Entity" : resolved;
    }
}
