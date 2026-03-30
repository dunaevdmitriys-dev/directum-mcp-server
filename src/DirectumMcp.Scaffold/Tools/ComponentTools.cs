using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class ComponentTools
{
    // ───────────────────────────────────────────────────────────────
    // scaffold_dialog
    // ───────────────────────────────────────────────────────────────

    [McpServerTool(Name = "scaffold_dialog")]
    [Description("Создать InputDialog: C# код с полями, валидацией, каскадными зависимостями. Для действий и обложки.")]
    public Task<string> ScaffoldDialog(
        [Description("Имя диалога PascalCase (например 'CreateDealDialog')")] string dialogName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Поля: 'Name:string:required,Date:date,Department:navigation:Employee,ShowAll:bool'")] string fields = "",
        [Description("Заголовок диалога (русский)")] string title = "",
        [Description("Каскадные зависимости: 'Department→Employee' — при смене Department фильтруется Employee")] string cascades = "")
    {
        var parsedFields = ParseDialogFields(fields);
        var parsedCascades = ParseCascades(cascades);
        var dialogTitle = string.IsNullOrWhiteSpace(title) ? dialogName : title;

        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Client");
        sb.AppendLine("{");
        sb.AppendLine("    partial class ModuleFunctions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Диалог: {dialogTitle}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        [LocalizeFunction(\"{dialogName}FunctionName\", \"\")]");
        sb.AppendLine($"        public virtual void {dialogName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            var dialog = Dialogs.CreateInputDialog(\"{dialogTitle}\");");
        sb.AppendLine();

        // Add fields
        foreach (var field in parsedFields)
        {
            var varName = char.ToLowerInvariant(field.Name[0]) + field.Name[1..];
            var required = field.IsRequired ? "true" : "false";

            switch (field.Type.ToLowerInvariant())
            {
                case "string":
                    sb.AppendLine($"            var {varName} = dialog.AddString(\"{field.DisplayName}\", {required});");
                    break;
                case "text":
                    sb.AppendLine($"            var {varName} = dialog.AddMultilineString(\"{field.DisplayName}\", {required});");
                    break;
                case "int":
                case "long":
                    sb.AppendLine($"            var {varName} = dialog.AddInteger(\"{field.DisplayName}\", {required});");
                    break;
                case "double":
                    sb.AppendLine($"            var {varName} = dialog.AddDouble(\"{field.DisplayName}\", {required});");
                    break;
                case "bool":
                    sb.AppendLine($"            var {varName} = dialog.AddBoolean(\"{field.DisplayName}\", false);");
                    break;
                case "date":
                    sb.AppendLine($"            var {varName} = dialog.AddDate(\"{field.DisplayName}\", {required});");
                    break;
                default:
                    // Navigation — AddSelect
                    sb.AppendLine($"            var {varName} = dialog.AddSelect(\"{field.DisplayName}\", {required}, {field.Type}s.GetAll());");
                    break;
            }
        }

        // Add cascades
        if (parsedCascades.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("            // Каскадные зависимости");
            sb.AppendLine("            dialog.SetOnRefresh((e) =>");
            sb.AppendLine("            {");
            foreach (var (parent, child) in parsedCascades)
            {
                var parentVar = char.ToLowerInvariant(parent[0]) + parent[1..];
                var childVar = char.ToLowerInvariant(child[0]) + child[1..];
                sb.AppendLine($"                // Фильтрация {child} по {parent}");
                sb.AppendLine($"                if ({parentVar}.Value != null)");
                sb.AppendLine($"                    {childVar}.From({child}s.GetAll().Where(x => Equals(x.{parent}, {parentVar}.Value)));");
            }
            sb.AppendLine("            });");
        }

        // Show dialog
        sb.AppendLine();
        sb.AppendLine("            if (dialog.Show() == DialogButtons.Ok)");
        sb.AppendLine("            {");
        foreach (var field in parsedFields)
        {
            var varName = char.ToLowerInvariant(field.Name[0]) + field.Name[1..];
            sb.AppendLine($"                var result{field.Name} = {varName}.Value;");
        }
        sb.AppendLine();
        sb.AppendLine("                // TODO: Обработка результата диалога");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var code = sb.ToString();

        var report = new StringBuilder();
        report.AppendLine("## Диалог создан");
        report.AppendLine();
        report.AppendLine($"**Имя:** {dialogName}");
        report.AppendLine($"**Заголовок:** {dialogTitle}");
        report.AppendLine($"**Полей:** {parsedFields.Count}");
        if (parsedCascades.Count > 0)
            report.AppendLine($"**Каскадов:** {string.Join(", ", parsedCascades.Select(c => $"{c.Parent}→{c.Child}"))}");
        report.AppendLine();
        report.AppendLine("### Поля");
        foreach (var f in parsedFields)
            report.AppendLine($"- {f.Name} ({f.Type}{(f.IsRequired ? ", required" : "")})");
        report.AppendLine();
        report.AppendLine("### Сгенерированный код");
        report.AppendLine("```csharp");
        report.Append(code);
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("### Использование");
        report.AppendLine($"Вставьте код в `ModuleClientFunctions.cs` модуля `{moduleName}`.");
        report.AppendLine($"Добавьте CoverFunctionAction с FunctionName=\"{dialogName}\" на обложку.");

        return Task.FromResult(report.ToString());
    }

    // ───────────────────────────────────────────────────────────────
    // scaffold_report
    // ───────────────────────────────────────────────────────────────

    [McpServerTool(Name = "scaffold_report")]
    [Description("Создать отчёт Directum RX: MTD + FastReport .frx + Queries.xml + обработчики + resx.")]
    public async Task<string> ScaffoldReport(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Имя отчёта PascalCase (например 'SalesFunnel')")] string reportName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Параметры отчёта через запятую: 'StartDate:DateTime,Department:navigation,ShowAll:bool'")] string parameters = "",
        [Description("Русское название отчёта")] string russianName = "")
    {
        var reportGuid = Guid.NewGuid().ToString("D");
        var ruName = string.IsNullOrWhiteSpace(russianName) ? reportName : russianName;
        var parsedParams = ParseReportParams(parameters);
        var sharedDir = Path.Combine(modulePath, $"{moduleName}.Shared");
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        var reportDir = Path.Combine(sharedDir, reportName);

        Directory.CreateDirectory(reportDir);
        Directory.CreateDirectory(serverDir);

        var createdFiles = new List<string>();

        // 1. Report MTD
        var mtdContent = GenerateReportMtd(reportName, reportGuid, moduleName, parsedParams);
        await File.WriteAllTextAsync(Path.Combine(reportDir, $"{reportName}.mtd"), mtdContent);
        createdFiles.Add($"{reportName}/{reportName}.mtd");

        // 2. FastReport template (.frx)
        var frxContent = GenerateFrxTemplate(reportName, parsedParams);
        await File.WriteAllTextAsync(Path.Combine(reportDir, $"{reportName}.frx"), frxContent);
        createdFiles.Add($"{reportName}/{reportName}.frx");

        // 3. Queries.xml
        var queriesContent = GenerateQueriesXml(reportName);
        await File.WriteAllTextAsync(Path.Combine(reportDir, "Queries.xml"), queriesContent);
        createdFiles.Add($"{reportName}/Queries.xml");

        // 4. System.resx
        var resxContent = GenerateReportResx(reportName, parsedParams, ruName, isRussian: false);
        await File.WriteAllTextAsync(Path.Combine(reportDir, $"{reportName}System.resx"), resxContent);
        createdFiles.Add($"{reportName}/{reportName}System.resx");

        var resxRuContent = GenerateReportResx(reportName, parsedParams, ruName, isRussian: true);
        await File.WriteAllTextAsync(Path.Combine(reportDir, $"{reportName}System.ru.resx"), resxRuContent);
        createdFiles.Add($"{reportName}/{reportName}System.ru.resx");

        // 5. Server handler
        var handlerContent = GenerateReportHandlerCs(moduleName, reportName, parsedParams);
        var handlerPath = Path.Combine(serverDir, $"{reportName}Handlers.cs");
        await File.WriteAllTextAsync(handlerPath, handlerContent);
        createdFiles.Add($"Server/{reportName}Handlers.cs");

        return $"""
            ## Отчёт создан

            **Имя:** {reportName}
            **GUID:** {reportGuid}
            **Модуль:** {moduleName}
            **Название:** {ruName}
            **Параметры:** {(parsedParams.Count > 0 ? string.Join(", ", parsedParams.Select(p => $"{p.Name}:{p.Type}")) : "нет")}

            ### Созданные файлы ({createdFiles.Count})
            {string.Join("\n", createdFiles.Select(f => $"- `{f}`"))}

            ### Следующие шаги
            1. Настройте шаблон отчёта в `{reportName}.frx` (FastReport Designer)
            2. Добавьте SQL-запрос в `Queries.xml`
            3. Реализуйте логику в `{reportName}Handlers.cs` — метод BeforeExecute
            4. Добавьте действие на обложку для открытия отчёта
            """;
    }

    // ───────────────────────────────────────────────────────────────
    // scaffold_widget
    // ───────────────────────────────────────────────────────────────

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
                xml = JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
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

    // ───────────────────────────────────────────────────────────────
    // scaffold_cover_action
    // ───────────────────────────────────────────────────────────────

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
                xml = JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
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

    // ───────────────────────────────────────────────────────────────
    // Private helpers — Dialog
    // ───────────────────────────────────────────────────────────────

    private static List<DialogFieldDef> ParseDialogFields(string fields)
    {
        var result = new List<DialogFieldDef>();
        if (string.IsNullOrWhiteSpace(fields)) return result;

        foreach (var part in fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length < 2) continue;

            var name = segments[0];
            var type = segments[1];
            var isRequired = segments.Length > 2 && segments[2].Equals("required", StringComparison.OrdinalIgnoreCase);
            var displayName = name; // Can be enhanced with camelCase→readable

            result.Add(new DialogFieldDef(name, type, displayName, isRequired));
        }
        return result;
    }

    private static List<(string Parent, string Child)> ParseCascades(string cascades)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(cascades)) return result;

        foreach (var part in cascades.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var arrow = part.IndexOf('→');
            if (arrow < 0) arrow = part.IndexOf("->");
            if (arrow <= 0) continue;

            var parent = part[..arrow].Trim();
            var child = part[(arrow + (part[arrow] == '→' ? 1 : 2))..].Trim();
            result.Add((parent, child));
        }
        return result;
    }

    private record DialogFieldDef(string Name, string Type, string DisplayName, bool IsRequired);

    // ───────────────────────────────────────────────────────────────
    // Private helpers — Report
    // ───────────────────────────────────────────────────────────────

    private static string GenerateReportMtd(string reportName, string reportGuid,
        string moduleName, List<(string Name, string Type)> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"$type\": \"Sungero.Metadata.ReportMetadata, Sungero.Reporting.Shared\",");
        sb.AppendLine($"  \"NameGuid\": \"{reportGuid}\",");
        sb.AppendLine($"  \"Name\": \"{reportName}\",");
        sb.AppendLine($"  \"ModuleName\": \"{moduleName}\",");
        sb.AppendLine("  \"HandledEvents\": [\"BeforeExecuteServer\", \"AfterExecuteServer\"],");
        sb.AppendLine("  \"Parameters\": [");

        for (int i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            var paramGuid = Guid.NewGuid().ToString("D");
            var internalType = MapToReportParamType(p.Type);
            var comma = i < parameters.Count - 1 ? "," : "";

            sb.AppendLine("    {");
            sb.AppendLine($"      \"NameGuid\": \"{paramGuid}\",");
            sb.AppendLine($"      \"Name\": \"{p.Name}\",");
            sb.AppendLine($"      \"InternalDataTypeName\": \"{internalType}\",");
            sb.AppendLine($"      \"IsSimpleDataType\": {(p.Type != "navigation" ? "true" : "false")}");
            sb.AppendLine($"    }}{comma}");
        }

        sb.AppendLine("  ],");
        sb.AppendLine("  \"Versions\": [");
        sb.AppendLine("    { \"Type\": \"ReportMetadata\", \"Number\": 2 },");
        sb.AppendLine("    { \"Type\": \"DomainApi\", \"Number\": 2 }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateFrxTemplate(string reportName, List<(string Name, string Type)> parameters)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report ScriptLanguage="CSharp" ReportInfo.Name="{reportName}">
              <Dictionary>
                <TableDataSource Name="Data" ReferenceName="Data" Enabled="true">
                </TableDataSource>
              </Dictionary>
              <ReportPage Name="Page1">
                <ReportTitleBand Name="ReportTitle1" Width="718.2" Height="37.8">
                  <TextObject Name="Text1" Width="718.2" Height="37.8" Text="{reportName}" HorzAlign="Center" Font="Arial, 14pt, style=Bold"/>
                </ReportTitleBand>
                <DataBand Name="Data1" Width="718.2" Height="18.9" DataSource="Data">
                  <TextObject Name="Text2" Width="718.2" Height="18.9" Text="[Data.Column1]"/>
                </DataBand>
              </ReportPage>
            </Report>
            """;
    }

    private static string GenerateQueriesXml(string reportName)
    {
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Queries>
              <Query Name="{reportName}Query">
                <![CDATA[
                  -- TODO: Добавьте SQL-запрос для отчёта
                  SELECT 1 AS Column1
                ]]>
              </Query>
            </Queries>
            """;
    }

    private static string GenerateReportResx(string reportName, List<(string Name, string Type)> parameters,
        string ruName, bool isRussian)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<root>");
        // Minimal resx schema
        sb.AppendLine("  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>");
        sb.AppendLine("  <resheader name=\"version\"><value>2.0</value></resheader>");

        sb.AppendLine($"  <data name=\"DisplayName\" xml:space=\"preserve\"><value>{(isRussian ? ruName : reportName)}</value></data>");
        sb.AppendLine($"  <data name=\"Description\" xml:space=\"preserve\"><value>{(isRussian ? $"Отчёт {ruName}" : $"Report {reportName}")}</value></data>");

        foreach (var p in parameters)
        {
            var label = isRussian ? $"[TODO] {p.Name}" : p.Name;
            sb.AppendLine($"  <data name=\"Parameter_{p.Name}\" xml:space=\"preserve\"><value>{label}</value></data>");
        }

        sb.AppendLine("</root>");
        return sb.ToString();
    }

    private static string GenerateReportHandlerCs(string moduleName, string reportName,
        List<(string Name, string Type)> parameters)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {reportName}Handlers");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Подготовка данных отчёта {reportName}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)");
        sb.AppendLine("        {");

        if (parameters.Count > 0)
        {
            sb.AppendLine("            // Чтение параметров отчёта");
            foreach (var p in parameters)
                sb.AppendLine($"            // var {ToCamelCase(p.Name)} = {reportName}.{p.Name};");
        }

        sb.AppendLine();
        sb.AppendLine("            // TODO: Заполните DataSource данными");
        sb.AppendLine("            // var table = new System.Data.DataTable();");
        sb.AppendLine("            // table.Columns.Add(\"Column1\");");
        sb.AppendLine("            // table.Rows.Add(\"Value1\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string MapToReportParamType(string type) => type.ToLowerInvariant() switch
    {
        "string" => "System.String",
        "int" or "long" => "System.Int64",
        "bool" => "System.Boolean",
        "datetime" or "date" => "System.DateTime",
        "double" => "System.Double",
        "navigation" => "Sungero.Domain.Shared.IEntity",
        _ => "System.String"
    };

    private static List<(string Name, string Type)> ParseReportParams(string parameters)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(parameters)) return result;

        foreach (var part in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0)
                result.Add((part[..colonIdx].Trim(), part[(colonIdx + 1)..].Trim()));
        }
        return result;
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];

    // ───────────────────────────────────────────────────────────────
    // Private helpers — Widget
    // ───────────────────────────────────────────────────────────────

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

    // ───────────────────────────────────────────────────────────────
    // Private helpers — Cover Action
    // ───────────────────────────────────────────────────────────────

    private static string GetGroupNames(JsonObject cover)
    {
        var groups = cover["Groups"]?.AsArray();
        if (groups == null) return "(нет групп)";
        return string.Join(", ", groups.Select(g => g?["Name"]?.GetValue<string>() ?? "?"));
    }
}
