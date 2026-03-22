using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldCoverActionTool
{
    [McpServerTool(Name = "scaffold_cover_action")]
    [Description("Добавить действие на обложку модуля: открытие списка сущностей, вызов функции, открытие отчёта.")]
    public async Task<string> ScaffoldCoverAction(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Имя действия PascalCase (например 'ShowDeals')")] string actionName,
        [Description("Тип: entity_list (список сущностей), function (клиентская функция), report (отчёт)")] string actionType = "entity_list",
        [Description("GUID сущности (для entity_list) или имя функции (для function)")] string target = "",
        [Description("Имя группы обложки, куда добавить действие")] string groupName = "",
        [Description("Русское название действия")] string russianName = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var mtdPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (!File.Exists(mtdPath))
            return $"**ОШИБКА**: Module.mtd не найден: `{mtdPath}`";

        var json = await File.ReadAllTextAsync(mtdPath);
        var node = JsonNode.Parse(json);
        if (node is not JsonObject root || root["Cover"] is not JsonObject cover)
            return "**ОШИБКА**: Cover не найден в Module.mtd. Сначала создайте модуль с hasCover=true.";

        var actions = cover["Actions"]?.AsArray();
        if (actions == null) { actions = new JsonArray(); cover["Actions"] = actions; }

        // Find group GUID
        string? groupGuid = null;
        if (!string.IsNullOrWhiteSpace(groupName))
        {
            var groups = cover["Groups"]?.AsArray();
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    if (g?["Name"]?.GetValue<string>()?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        groupGuid = g["NameGuid"]?.GetValue<string>();
                        break;
                    }
                }
            }
            if (groupGuid == null)
                return $"**ОШИБКА**: Группа `{groupName}` не найдена в Cover.Groups. Доступные: {GetGroupNames(cover)}";
        }

        var actionGuid = Guid.NewGuid().ToString("D");
        var ruName = string.IsNullOrWhiteSpace(russianName) ? actionName : russianName;

        // Build action JSON
        JsonObject actionObj;
        switch (actionType.ToLowerInvariant())
        {
            case "entity_list":
                if (string.IsNullOrWhiteSpace(target))
                    return "**ОШИБКА**: Для entity_list укажите GUID сущности в параметре target.";
                actionObj = new JsonObject
                {
                    ["$type"] = "Sungero.Metadata.CoverEntityListActionMetadata, Sungero.Metadata",
                    ["NameGuid"] = actionGuid,
                    ["Name"] = actionName,
                    ["EntityTypeId"] = target,
                    ["Versions"] = new JsonArray()
                };
                break;

            case "function":
                if (string.IsNullOrWhiteSpace(target))
                    return "**ОШИБКА**: Для function укажите имя клиентской функции в параметре target.";
                actionObj = new JsonObject
                {
                    ["$type"] = "Sungero.Metadata.CoverFunctionActionMetadata, Sungero.Metadata",
                    ["NameGuid"] = actionGuid,
                    ["Name"] = actionName,
                    ["FunctionName"] = target,
                    ["Versions"] = new JsonArray()
                };
                break;

            case "report":
                actionObj = new JsonObject
                {
                    ["$type"] = "Sungero.Metadata.CoverReportActionMetadata, Sungero.Metadata",
                    ["NameGuid"] = actionGuid,
                    ["Name"] = actionName,
                    ["Versions"] = new JsonArray()
                };
                break;

            default:
                return $"**ОШИБКА**: Неизвестный тип `{actionType}`. Допустимые: entity_list, function, report.";
        }

        if (groupGuid != null)
            actionObj["GroupId"] = groupGuid;

        actions.Add(actionObj);

        // Add ResourcesKey
        var resKeys = root["ResourcesKeys"]?.AsArray();
        var coverActionKey = $"CoverAction_{actionName}";
        if (resKeys != null && !resKeys.Any(k => k?.GetValue<string>() == coverActionKey))
            resKeys.Add(coverActionKey);

        await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Update resx
        var resxPath = Path.Combine(modulePath, $"{moduleName}.Shared", "ModuleSystem.ru.resx");
        if (File.Exists(resxPath))
        {
            var xml = await File.ReadAllTextAsync(resxPath);
            if (!xml.Contains($"name=\"{coverActionKey}\""))
            {
                xml = Core.Services.JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
                    $"  <data name=\"{coverActionKey}\" xml:space=\"preserve\">\n    <value>{ruName}</value>\n  </data>");
                await File.WriteAllTextAsync(resxPath, xml);
            }
        }

        return $"""
            ## Действие обложки добавлено

            **Имя:** {actionName}
            **Тип:** {actionType}
            **GUID:** {actionGuid}
            {(groupGuid != null ? $"**Группа:** {groupName}" : "**Группа:** не указана")}
            {(actionType == "function" ? $"**Функция:** {target} (должна быть в ModuleClientFunctions.cs!)" : "")}

            ### Обновлённые файлы
            - `Module.mtd` — Cover.Actions
            - `ModuleSystem.ru.resx` — CoverAction_{actionName} = {ruName}
            """;
    }

    private static string GetGroupNames(JsonObject cover)
    {
        var groups = cover["Groups"]?.AsArray();
        if (groups == null) return "(нет групп)";
        return string.Join(", ", groups.Select(g => g?["Name"]?.GetValue<string>() ?? "?"));
    }
}
