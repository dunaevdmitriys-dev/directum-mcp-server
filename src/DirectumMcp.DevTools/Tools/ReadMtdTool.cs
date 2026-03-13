using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ReadMtdTool
{
    private static bool IsPathAllowed(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Environment.GetEnvironmentVariable("SOLUTION_PATH") ?? "",
            Path.GetTempPath()
        };
        return allowedPaths.Any(bp => !string.IsNullOrEmpty(bp) &&
            fullPath.StartsWith(Path.GetFullPath(bp), StringComparison.OrdinalIgnoreCase));
    }

    [McpServerTool(Name = "read_mtd")]
    [Description("Чтение и анализ MTD-метаданных сущности Directum RX. " +
                 "Выводит имя, тип, базовый тип, свойства, действия, формы, события, интеграционный сервис.")]
    public async Task<string> ReadMtd(string mtdPath)
    {
        if (!IsPathAllowed(mtdPath))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{mtdPath}` находится за пределами разрешённых директорий.";

        if (!File.Exists(mtdPath))
            return $"**ОШИБКА**: Файл не найден: `{mtdPath}`";

        var json = await File.ReadAllTextAsync(mtdPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sb = new StringBuilder();

        var metaType = GetString(root, "$type");
        var isModule = metaType.Contains("ModuleMetadata");

        if (isModule)
            FormatModule(root, sb, mtdPath);
        else
            FormatEntity(root, sb, mtdPath);

        return sb.ToString();
    }

    private static void FormatEntity(JsonElement root, StringBuilder sb, string path)
    {
        var name = GetString(root, "Name");
        var nameGuid = GetString(root, "NameGuid");
        var baseGuid = GetString(root, "BaseGuid");
        var metaType = GetString(root, "$type");
        var code = GetString(root, "Code");
        var integrationName = GetString(root, "IntegrationServiceName");

        var entityKind = metaType switch
        {
            var t when t.Contains("TaskMetadata") => "Task (задача)",
            var t when t.Contains("AssignmentMetadata") => "Assignment (задание)",
            var t when t.Contains("NoticeMetadata") => "Notice (уведомление)",
            var t when t.Contains("ReportMetadata") => "Report (отчёт)",
            _ => ResolveEntityKindFromBase(baseGuid)
        };

        sb.AppendLine($"# Сущность: {name}");
        sb.AppendLine();
        sb.AppendLine($"| Параметр | Значение |");
        sb.AppendLine($"|----------|----------|");
        sb.AppendLine($"| GUID | `{nameGuid}` |");
        sb.AppendLine($"| Тип метаданных | {entityKind} |");
        sb.AppendLine($"| BaseGuid | `{baseGuid}` |");
        if (!string.IsNullOrEmpty(code))
            sb.AppendLine($"| Code | `{code}` |");
        if (!string.IsNullOrEmpty(integrationName))
            sb.AppendLine($"| IntegrationServiceName | `{integrationName}` |");
        sb.AppendLine($"| Файл | `{Path.GetFileName(path)}` |");
        sb.AppendLine();

        // Properties
        if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
        {
            var propList = props.EnumerateArray().ToList();
            if (propList.Count > 0)
            {
                sb.AppendLine("## Свойства");
                sb.AppendLine();
                sb.AppendLine("| Имя | Тип | Code | Обязат. | Доп. инфо |");
                sb.AppendLine("|-----|-----|------|---------|-----------|");

                foreach (var prop in propList)
                {
                    var propType = GetString(prop, "$type");
                    var propName = GetString(prop, "Name");
                    var propCode = GetString(prop, "Code");
                    var isRequired = prop.TryGetProperty("IsRequired", out var req) && req.GetBoolean();
                    var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var anc) && anc.GetBoolean();

                    var shortType = ExtractPropertyType(propType);
                    var extra = new List<string>();

                    if (isAncestor) extra.Add("inherited");
                    if (prop.TryGetProperty("EntityGuid", out var eg))
                        extra.Add($"-> `{eg.GetString()}`");
                    if (prop.TryGetProperty("IsDisplayValue", out var dv) && dv.GetBoolean())
                        extra.Add("display");

                    // Enum direct values
                    if (prop.TryGetProperty("DirectValues", out var vals) && vals.ValueKind == JsonValueKind.Array)
                    {
                        var valNames = vals.EnumerateArray()
                            .Select(v => GetString(v, "Name"))
                            .Where(v => !string.IsNullOrEmpty(v));
                        extra.Add($"values: [{string.Join(", ", valNames)}]");
                    }

                    sb.AppendLine($"| {propName} | {shortType} | {propCode} | {(isRequired ? "да" : "")} | {string.Join("; ", extra)} |");
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
                sb.AppendLine("## Действия");
                sb.AppendLine();
                foreach (var action in actionList)
                {
                    var actionName = GetString(action, "Name");
                    var isAncestor = action.TryGetProperty("IsAncestorMetadata", out var anc) && anc.GetBoolean();
                    var genHandler = action.TryGetProperty("GenerateHandler", out var gh) && gh.GetBoolean();
                    var suffix = isAncestor ? " (inherited)" : "";
                    var handlerSuffix = genHandler ? " [handler]" : "";
                    sb.AppendLine($"- `{actionName}`{suffix}{handlerSuffix}");
                }
                sb.AppendLine();
            }
        }

        // AttachmentGroups (for Tasks)
        if (root.TryGetProperty("AttachmentGroups", out var groups) && groups.ValueKind == JsonValueKind.Array)
        {
            var groupList = groups.EnumerateArray().ToList();
            if (groupList.Count > 0)
            {
                sb.AppendLine("## Группы вложений");
                sb.AppendLine();
                foreach (var group in groupList)
                {
                    var groupName = GetString(group, "Name");
                    sb.Append($"- `{groupName}`");
                    if (group.TryGetProperty("Constraints", out var constraints) &&
                        constraints.ValueKind == JsonValueKind.Array)
                    {
                        var cList = constraints.EnumerateArray().ToList();
                        if (cList.Count > 0)
                        {
                            var cNames = cList.Select(c => GetString(c, "Name")).Where(n => !string.IsNullOrEmpty(n));
                            sb.Append($" — ограничения: [{string.Join(", ", cNames)}]");
                        }
                    }
                    sb.AppendLine();
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
                sb.AppendLine("## Формы");
                sb.AppendLine();
                foreach (var form in formList)
                {
                    var formName = GetString(form, "Name");
                    var formType = GetString(form, "$type");
                    var shortFormType = formType.Split(',')[0].Split('.').LastOrDefault() ?? formType;
                    sb.Append($"- `{formName}` ({shortFormType})");

                    if (form.TryGetProperty("Controls", out var controls) &&
                        controls.ValueKind == JsonValueKind.Array)
                    {
                        var count = controls.EnumerateArray().Count();
                        sb.Append($" — {count} контролов");
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
                sb.AppendLine("## Публичные функции");
                sb.AppendLine();
                foreach (var func in funcList)
                {
                    var funcName = GetString(func, "Name");
                    var returnType = GetString(func, "ReturnType");
                    var shortReturn = returnType.Replace("global::", "");

                    var paramStr = "";
                    if (func.TryGetProperty("Parameters", out var pars) && pars.ValueKind == JsonValueKind.Array)
                    {
                        var parList = pars.EnumerateArray()
                            .Select(p => $"{GetString(p, "ParameterType").Replace("global::", "")} {GetString(p, "Name")}");
                        paramStr = string.Join(", ", parList);
                    }
                    sb.AppendLine($"- `{shortReturn} {funcName}({paramStr})`");
                }
                sb.AppendLine();
            }
        }

        // Handled events
        if (root.TryGetProperty("HandledEvents", out var events) && events.ValueKind == JsonValueKind.Array)
        {
            var eventList = events.EnumerateArray().Select(e => e.GetString()).Where(e => e != null).ToList();
            if (eventList.Count > 0)
            {
                sb.AppendLine("## Обрабатываемые события");
                sb.AppendLine();
                foreach (var ev in eventList)
                    sb.AppendLine($"- `{ev}`");
                sb.AppendLine();
            }
        }

        // Blocks (workflow)
        if (root.TryGetProperty("Blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
        {
            var blockList = blocks.EnumerateArray().ToList();
            if (blockList.Count > 0)
            {
                sb.AppendLine("## Блоки (workflow)");
                sb.AppendLine();
                foreach (var block in blockList)
                {
                    var blockName = GetString(block, "Name");
                    var blockType = GetString(block, "$type");
                    var shortBlockType = blockType.Split(',')[0].Split('.').LastOrDefault() ?? blockType;
                    sb.AppendLine($"- `{blockName}` ({shortBlockType})");
                }
                sb.AppendLine();
            }
        }
    }

    private static void FormatModule(JsonElement root, StringBuilder sb, string path)
    {
        var name = GetString(root, "Name");
        var nameGuid = GetString(root, "NameGuid");

        sb.AppendLine($"# Модуль: {name}");
        sb.AppendLine();
        sb.AppendLine($"| Параметр | Значение |");
        sb.AppendLine($"|----------|----------|");
        sb.AppendLine($"| GUID | `{nameGuid}` |");
        sb.AppendLine($"| Файл | `{Path.GetFileName(path)}` |");
        sb.AppendLine();

        // Dependencies
        if (root.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
        {
            var depList = deps.EnumerateArray().ToList();
            if (depList.Count > 0)
            {
                sb.AppendLine("## Зависимости");
                sb.AppendLine();
                foreach (var dep in depList)
                {
                    var id = GetString(dep, "Id");
                    var minVer = GetString(dep, "MinVersion");
                    var isSolution = dep.TryGetProperty("IsSolutionModule", out var sm) && sm.GetBoolean();
                    var suffix = isSolution ? " (solution module)" : "";
                    sb.AppendLine($"- `{id}` >= {minVer}{suffix}");
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
                sb.AppendLine("## Асинхронные обработчики");
                sb.AppendLine();
                foreach (var h in handlerList)
                {
                    var hName = GetString(h, "Name");
                    sb.AppendLine($"- `{hName}`");
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
                sb.AppendLine("## Фоновые процессы");
                sb.AppendLine();
                foreach (var j in jobList)
                {
                    var jName = GetString(j, "Name");
                    sb.AppendLine($"- `{jName}`");
                }
                sb.AppendLine();
            }
        }

        // Cover (module cover actions)
        if (root.TryGetProperty("Cover", out var cover))
        {
            if (cover.TryGetProperty("Groups", out var cGroups) && cGroups.ValueKind == JsonValueKind.Array)
            {
                var groupList = cGroups.EnumerateArray().ToList();
                if (groupList.Count > 0)
                {
                    sb.AppendLine("## Обложка модуля");
                    sb.AppendLine();
                    foreach (var g in groupList)
                    {
                        var gName = GetString(g, "Name");
                        sb.AppendLine($"### Группа: {gName}");

                        if (g.TryGetProperty("Actions", out var cActions) && cActions.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var a in cActions.EnumerateArray())
                            {
                                var aName = GetString(a, "Name");
                                var aType = GetString(a, "$type");
                                var shortType = aType.Split(',')[0].Split('.').LastOrDefault() ?? aType;
                                sb.AppendLine($"  - `{aName}` ({shortType})");
                            }
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // PublicFunctions
        if (root.TryGetProperty("PublicFunctions", out var funcs) && funcs.ValueKind == JsonValueKind.Array)
        {
            var funcList = funcs.EnumerateArray().ToList();
            if (funcList.Count > 0)
            {
                sb.AppendLine("## Публичные функции модуля");
                sb.AppendLine();
                foreach (var func in funcList)
                {
                    var funcName = GetString(func, "Name");
                    var returnType = GetString(func, "ReturnType").Replace("global::", "");
                    sb.AppendLine($"- `{returnType} {funcName}(...)`");
                }
                sb.AppendLine();
            }
        }
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private static string ExtractPropertyType(string fullType)
    {
        // "Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata" -> "String"
        var className = fullType.Split(',')[0].Split('.').LastOrDefault() ?? fullType;
        return className
            .Replace("PropertyMetadata", "")
            .Replace("Metadata", "");
    }

    private static string ResolveEntityKindFromBase(string baseGuid)
    {
        return baseGuid.ToLowerInvariant() switch
        {
            "04581d26-0780-4cfd-b3cd-c2cafc5798b0" => "DatabookEntry (справочник)",
            "58cca102-1e97-4f07-b6ac-fd866a8b7cb1" => "Document (документ)",
            "d795d1f6-45c1-4e5e-9677-b53fb7280c7e" => "Task (задача)",
            "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24" => "Assignment (задание)",
            "4e09273f-8b3a-489e-814e-a4ebfbba3e6c" => "Notice (уведомление)",
            _ => "Entity"
        };
    }
}
