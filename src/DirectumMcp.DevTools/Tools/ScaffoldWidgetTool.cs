using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldWidgetTool
{
    [McpServerTool(Name = "scaffold_widget")]
    [Description("Создать виджет модуля: счётчик, диаграмма. Обновляет Module.mtd Widgets + resx + обработчики.")]
    public async Task<string> ScaffoldWidget(
        [Description("Путь к директории модуля (где Module.mtd)")] string modulePath,
        [Description("Имя виджета PascalCase (например 'ActiveDealsCounter')")] string widgetName,
        [Description("Полное имя модуля (например 'DirRX.CRM')")] string moduleName,
        [Description("Тип: counter (счётчик сущностей) или chart (диаграмма)")] string widgetType = "counter",
        [Description("GUID сущности для counter-виджета (EntityGuid)")] string entityGuid = "",
        [Description("Тип диаграммы: HorizontalBar, Column, Pie, Line (для chart)")] string chartType = "HorizontalBar",
        [Description("Цвет: WidgetColor1, WidgetColor2, WidgetColor3, WidgetColor4")] string color = "WidgetColor1",
        [Description("Русское название виджета")] string russianName = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var widgetGuid = Guid.NewGuid().ToString("D");
        var itemGuid = Guid.NewGuid().ToString("D");
        var ruName = string.IsNullOrWhiteSpace(russianName) ? widgetName : russianName;

        // 1. Build widget JSON
        var widgetJson = widgetType == "chart"
            ? BuildChartWidget(widgetName, widgetGuid, itemGuid, color, chartType)
            : BuildCounterWidget(widgetName, widgetGuid, itemGuid, color, entityGuid);

        // 2. Update Module.mtd
        var mtdPath = FindMtd(modulePath, moduleName);
        if (mtdPath == null)
            return $"**ОШИБКА**: Module.mtd не найден в `{modulePath}`";

        var mtdJson = await File.ReadAllTextAsync(mtdPath);
        var node = JsonNode.Parse(mtdJson);
        if (node is not JsonObject root)
            return "**ОШИБКА**: Невалидный Module.mtd";

        var widgets = root["Widgets"]?.AsArray();
        if (widgets == null) { widgets = new JsonArray(); root["Widgets"] = widgets; }

        widgets.Add(JsonNode.Parse(widgetJson));

        // Add ResourcesKeys
        var resKeys = root["ResourcesKeys"]?.AsArray();
        if (resKeys != null)
        {
            resKeys.Add($"Widget{widgetName}");
        }

        await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // 3. Generate widget handlers (server)
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(serverDir);
        var handlerPath = Path.Combine(serverDir, "ModuleWidgetHandlers.cs");

        var handlerCode = widgetType == "chart"
            ? GenerateChartHandlerCs(moduleName, widgetName, itemGuid)
            : GenerateCounterHandlerCs(moduleName, widgetName, itemGuid);

        if (File.Exists(handlerPath))
        {
            var existing = await File.ReadAllTextAsync(handlerPath);
            var insertIdx = existing.LastIndexOf('}', existing.LastIndexOf('}') - 1);
            if (insertIdx > 0)
                await File.WriteAllTextAsync(handlerPath, existing[..insertIdx] + "\n" + handlerCode + "\n" + existing[insertIdx..]);
        }
        else
        {
            var fullHandler = $"using System;\nusing System.Linq;\nusing Sungero.Core;\nusing Sungero.CoreEntities;\n\nnamespace {moduleName}.Server\n{{\n    partial class ModuleWidgetHandlers\n    {{\n{handlerCode}\n    }}\n}}";
            await File.WriteAllTextAsync(handlerPath, fullHandler);
        }

        // 4. Update resx
        var resxPath = Path.Combine(modulePath, $"{moduleName}.Shared", "ModuleSystem.ru.resx");
        if (File.Exists(resxPath))
        {
            var xml = await File.ReadAllTextAsync(resxPath);
            if (!xml.Contains($"Widget{widgetName}"))
            {
                xml = Core.Services.JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
                    $"  <data name=\"Widget{widgetName}\" xml:space=\"preserve\">\n    <value>{ruName}</value>\n  </data>");
                await File.WriteAllTextAsync(resxPath, xml);
            }
        }

        return $"""
            ## Виджет создан

            **Имя:** {widgetName}
            **Тип:** {widgetType}
            **GUID:** {widgetGuid}
            **Цвет:** {color}

            ### Обновлённые файлы
            - `Module.mtd` — добавлен в Widgets
            - `ModuleWidgetHandlers.cs` — обработчик FilteringServer{(widgetType == "chart" ? " + GetValueServer" : "")}
            - `ModuleSystem.ru.resx` — Widget{widgetName} = {ruName}

            ### Следующие шаги
            1. Реализуйте логику фильтрации в ModuleWidgetHandlers.cs
            {(widgetType == "chart" ? "2. Реализуйте GetValueServer для данных диаграммы" : "")}
            """;
    }

    private static string BuildCounterWidget(string name, string widgetGuid, string itemGuid, string color, string entityGuid)
    {
        return $$"""
            {
              "NameGuid": "{{widgetGuid}}",
              "Name": "{{name}}",
              "Color": "{{color}}",
              "ColumnSpan": 20,
              "Versions": [],
              "WidgetItems": [
                {
                  "$type": "Sungero.Metadata.WidgetActionMetadata, Sungero.Metadata",
                  "NameGuid": "{{itemGuid}}",
                  "Name": "{{name}}Item",
                  "EntityGuid": "{{(string.IsNullOrEmpty(entityGuid) ? Guid.NewGuid().ToString("D") : entityGuid)}}",
                  "IsMain": true,
                  "HandledEvents": ["FilteringServer"],
                  "Versions": []
                }
              ]
            }
            """;
    }

    private static string BuildChartWidget(string name, string widgetGuid, string itemGuid, string color, string chartType)
    {
        return $$"""
            {
              "NameGuid": "{{widgetGuid}}",
              "Name": "{{name}}",
              "Color": "{{color}}",
              "ColumnSpan": 20,
              "Versions": [],
              "WidgetItems": [
                {
                  "$type": "Sungero.Metadata.WidgetChartMetadata, Sungero.Metadata",
                  "NameGuid": "{{itemGuid}}",
                  "Name": "{{name}}Chart",
                  "ChartType": "{{chartType}}",
                  "ChartHeight": 11,
                  "SpecificationType": "Custom",
                  "HandledEvents": ["GetValueServer", "ExecuteClient"],
                  "Versions": []
                }
              ]
            }
            """;
    }

    private static string GenerateCounterHandlerCs(string moduleName, string widgetName, string itemGuid)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"        // Widget: {widgetName}");
        sb.AppendLine($"        public virtual IQueryable<Sungero.Domain.Shared.IEntity> {widgetName}ItemFiltering(");
        sb.AppendLine("            IQueryable<Sungero.Domain.Shared.IEntity> query, Sungero.Domain.UiFilteringEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine($"            // TODO: Добавьте фильтрацию для виджета {widgetName}");
        sb.AppendLine("            return query;");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string GenerateChartHandlerCs(string moduleName, string widgetName, string itemGuid)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"        // Widget chart: {widgetName}");
        sb.AppendLine($"        public virtual List<Sungero.Core.WidgetChartValue> Get{widgetName}ChartValue(");
        sb.AppendLine("            Sungero.Domain.GetWidgetBarChartValueEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            var values = new List<Sungero.Core.WidgetChartValue>();");
        sb.AppendLine("            // TODO: Заполните данные диаграммы");
        sb.AppendLine("            return values;");
        sb.AppendLine("        }");
        return sb.ToString();
    }

    private static string? FindMtd(string modulePath, string moduleName)
    {
        var path = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        return File.Exists(path) ? path : null;
    }
}
