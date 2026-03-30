using System.Text;
using System.Text.Json;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for scaffold_job. Used by MCP tool and pipeline.
/// </summary>
public class JobScaffoldService : IPipelineStep
{
    public string ToolName => "scaffold_job";

    public async Task<ScaffoldJobResult> ScaffoldAsync(
        string outputPath,
        string jobName,
        string moduleName,
        string cronSchedule = "0 0 * * *",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return Fail("Параметр `outputPath` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(jobName))
            return Fail("Параметр `jobName` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(moduleName))
            return Fail("Параметр `moduleName` не может быть пустым.");

        var cron = string.IsNullOrWhiteSpace(cronSchedule) ? "0 0 * * *" : cronSchedule.Trim();
        var jobGuid = Guid.NewGuid().ToString("D");

        Directory.CreateDirectory(outputPath);

        var createdFiles = new List<string>();
        var updatedFiles = new List<string>();

        // 1. Update Module.mtd
        var mtdPath = Path.Combine(outputPath, "Module.mtd");
        UpdateModuleMtdWithJob(mtdPath, jobName, jobGuid, cron);
        updatedFiles.Add("Module.mtd");

        // 2. ModuleJobs.cs
        var jobsCs = GenerateModuleJobsCs(moduleName, jobName);
        await EntityScaffoldService.WriteFileAsync(outputPath, "ModuleJobs.cs", jobsCs, ct);
        createdFiles.Add("ModuleJobs.cs");

        // 3. ModuleSystem.resx
        var resxPath = Path.Combine(outputPath, "ModuleSystem.resx");
        AddJobKeysToResx(resxPath, jobName, isRussian: false);
        createdFiles.Add("ModuleSystem.resx");

        // 4. ModuleSystem.ru.resx
        var resxRuPath = Path.Combine(outputPath, "ModuleSystem.ru.resx");
        AddJobKeysToResx(resxRuPath, jobName, isRussian: true);
        createdFiles.Add("ModuleSystem.ru.resx");

        return new ScaffoldJobResult
        {
            Success = true,
            JobName = jobName,
            JobGuid = jobGuid,
            ModuleName = moduleName,
            CronSchedule = cron,
            CreatedFiles = createdFiles,
            UpdatedFiles = updatedFiles
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        return await ScaffoldAsync(
            outputPath: GetString(parameters, "outputPath"),
            jobName: GetString(parameters, "jobName"),
            moduleName: GetString(parameters, "moduleName"),
            cronSchedule: GetString(parameters, "cronSchedule", "0 0 * * *"),
            ct: ct);
    }

    internal static void UpdateModuleMtdWithJob(string mtdPath, string jobName, string jobGuid, string cron)
    {
        string json = File.Exists(mtdPath)
            ? File.ReadAllText(mtdPath)
            : "{\n  \"Jobs\": []\n}";

        var jobEntry = $"    {{\n" +
                       $"      \"$type\": \"Sungero.Metadata.JobMetadata, Sungero.Metadata\",\n" +
                       $"      \"NameGuid\": \"{jobGuid}\",\n" +
                       $"      \"Name\": \"{jobName}\",\n" +
                       $"      \"GenerateHandler\": true,\n" +
                       $"      \"CronSchedule\": \"{cron}\"\n" +
                       $"    }}";

        if (json.Contains("\"Jobs\""))
        {
            var jobsEndIndex = FindJobsArrayEnd(json);
            if (jobsEndIndex >= 0)
            {
                var beforeClose = json[..jobsEndIndex].TrimEnd();
                var afterClose = json[jobsEndIndex..];
                string insertion = beforeClose.EndsWith('[')
                    ? $"\n{jobEntry}\n  "
                    : $",\n{jobEntry}";
                json = beforeClose + insertion + afterClose;
            }
        }
        else
        {
            var lastBrace = json.LastIndexOf('}');
            if (lastBrace >= 0)
            {
                var before = json[..lastBrace].TrimEnd();
                var needsComma = before.TrimEnd().Length > 1 && !before.TrimEnd().EndsWith('{');
                var comma = needsComma ? "," : "";
                json = before + $"{comma}\n  \"Jobs\": [\n{jobEntry}\n  ]\n}}";
            }
        }

        File.WriteAllText(mtdPath, json);
    }

    internal static int FindJobsArrayEnd(string json)
    {
        var jobsIdx = json.IndexOf("\"Jobs\"", StringComparison.Ordinal);
        if (jobsIdx < 0) return -1;
        var openBracket = json.IndexOf('[', jobsIdx);
        if (openBracket < 0) return -1;

        var depth = 0;
        for (var i = openBracket; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']') { depth--; if (depth == 0) return i; }
        }
        return -1;
    }

    internal static string GenerateModuleJobsCs(string moduleName, string jobName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine("    partial class ModuleJobs");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Фоновое задание {jobName}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public virtual void {jobName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            Logger.Debug(\"{moduleName}: Запуск задания {jobName}\");");
        sb.AppendLine("            // TODO: Реализовать логику задания");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    internal static void AddJobKeysToResx(string resxPath, string jobName, bool isRussian)
    {
        string xml = File.Exists(resxPath) ? File.ReadAllText(resxPath) : BuildEmptyResx();

        var descValue = isRussian ? $"Описание задания {jobName}" : $"Description of job {jobName}";
        var nameKey = $"Job_{jobName}";
        var descKey = $"Job_{jobName}_Description";

        if (!xml.Contains($"name=\"{nameKey}\""))
            xml = InsertDataNodeBeforeRootClose(xml,
                $"  <data name=\"{nameKey}\" xml:space=\"preserve\">\n    <value>{jobName}</value>\n  </data>");

        if (!xml.Contains($"name=\"{descKey}\""))
            xml = InsertDataNodeBeforeRootClose(xml,
                $"  <data name=\"{descKey}\" xml:space=\"preserve\">\n    <value>{descValue}</value>\n  </data>");

        File.WriteAllText(resxPath, xml);
    }

    public static string InsertDataNodeBeforeRootClose(string xml, string dataNode)
    {
        var closeRoot = xml.LastIndexOf("</root>", StringComparison.Ordinal);
        return closeRoot >= 0
            ? xml[..closeRoot] + dataNode + "\n</root>"
            : xml + "\n" + dataNode;
    }

    public static string BuildEmptyResx()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<root>");
        sb.AppendLine("  <xsd:schema id=\"root\" xmlns=\"\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:msdata=\"urn:schemas-microsoft-com:xml-msdata\">");
        sb.AppendLine("    <xsd:element name=\"root\" msdata:IsDataSet=\"true\">");
        sb.AppendLine("      <xsd:complexType>");
        sb.AppendLine("        <xsd:choice maxOccurs=\"unbounded\">");
        sb.AppendLine("          <xsd:element name=\"data\">");
        sb.AppendLine("            <xsd:complexType>");
        sb.AppendLine("              <xsd:sequence>");
        sb.AppendLine("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />");
        sb.AppendLine("                <xsd:element name=\"comment\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"2\" />");
        sb.AppendLine("              </xsd:sequence>");
        sb.AppendLine("              <xsd:attribute name=\"name\" type=\"xsd:string\" msdata:Ordinal=\"0\" />");
        sb.AppendLine("              <xsd:attribute name=\"type\" type=\"xsd:string\" />");
        sb.AppendLine("              <xsd:attribute name=\"mimetype\" type=\"xsd:string\" />");
        sb.AppendLine("            </xsd:complexType>");
        sb.AppendLine("          </xsd:element>");
        sb.AppendLine("          <xsd:element name=\"resheader\">");
        sb.AppendLine("            <xsd:complexType>");
        sb.AppendLine("              <xsd:sequence>");
        sb.AppendLine("                <xsd:element name=\"value\" type=\"xsd:string\" minOccurs=\"0\" msdata:Ordinal=\"1\" />");
        sb.AppendLine("              </xsd:sequence>");
        sb.AppendLine("              <xsd:attribute name=\"name\" type=\"xsd:string\" use=\"required\" />");
        sb.AppendLine("            </xsd:complexType>");
        sb.AppendLine("          </xsd:element>");
        sb.AppendLine("        </xsd:choice>");
        sb.AppendLine("      </xsd:complexType>");
        sb.AppendLine("    </xsd:element>");
        sb.AppendLine("  </xsd:schema>");
        sb.AppendLine("  <resheader name=\"resmimetype\">");
        sb.AppendLine("    <value>text/microsoft-resx</value>");
        sb.AppendLine("  </resheader>");
        sb.AppendLine("  <resheader name=\"version\">");
        sb.AppendLine("    <value>2.0</value>");
        sb.AppendLine("  </resheader>");
        sb.AppendLine("  <resheader name=\"reader\">");
        sb.AppendLine("    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>");
        sb.AppendLine("  </resheader>");
        sb.AppendLine("  <resheader name=\"writer\">");
        sb.AppendLine("    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>");
        sb.AppendLine("  </resheader>");
        sb.Append("</root>");
        return sb.ToString();
    }

    private static ScaffoldJobResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetString(Dictionary<string, JsonElement> p, string key, string def = "") =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : def;
}
