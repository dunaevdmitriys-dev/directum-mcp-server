using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldReportTool
{
    [McpServerTool(Name = "scaffold_report")]
    [Description("Создать отчёт Directum RX: MTD + FastReport .frx + Queries.xml + обработчики + resx.")]
    public async Task<string> ScaffoldReport(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Имя отчёта PascalCase (например 'SalesFunnel')")] string reportName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Параметры отчёта через запятую: 'StartDate:DateTime,Department:navigation,ShowAll:bool'")] string parameters = "",
        [Description("Русское название отчёта")] string russianName = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var reportGuid = Guid.NewGuid().ToString("D");
        var ruName = string.IsNullOrWhiteSpace(russianName) ? reportName : russianName;
        var parsedParams = ParseParams(parameters);
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

    private static List<(string Name, string Type)> ParseParams(string parameters)
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
}
