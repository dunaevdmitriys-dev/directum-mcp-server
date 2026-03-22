using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldDialogTool
{
    [McpServerTool(Name = "scaffold_dialog")]
    [Description("Создать InputDialog: C# код с полями, валидацией, каскадными зависимостями. Для действий и обложки.")]
    public Task<string> ScaffoldDialog(
        [Description("Имя диалога PascalCase (например 'CreateDealDialog')")] string dialogName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Поля: 'Name:string:required,Date:date,Department:navigation:Employee,ShowAll:bool'")] string fields = "",
        [Description("Заголовок диалога (русский)")] string title = "",
        [Description("Каскадные зависимости: 'Department→Employee' — при смене Department фильтруется Employee")] string cascades = "")
    {
        var parsedFields = ParseFields(fields);
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

    private static List<FieldDef> ParseFields(string fields)
    {
        var result = new List<FieldDef>();
        if (string.IsNullOrWhiteSpace(fields)) return result;

        foreach (var part in fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length < 2) continue;

            var name = segments[0];
            var type = segments[1];
            var isRequired = segments.Length > 2 && segments[2].Equals("required", StringComparison.OrdinalIgnoreCase);
            var displayName = name; // Can be enhanced with camelCase→readable

            result.Add(new FieldDef(name, type, displayName, isRequired));
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

    private record FieldDef(string Name, string Type, string DisplayName, bool IsRequired);
}
