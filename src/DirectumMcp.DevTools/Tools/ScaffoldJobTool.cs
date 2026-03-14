using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldJobTool
{
    [McpServerTool(Name = "scaffold_job")]
    [Description("Генерация фонового задания (Background Job) Directum RX: Module.mtd с JobMetadata, ModuleJobs.cs с обработчиком, ресурсные файлы.")]
    public async Task<string> ScaffoldJob(
        [Description("Путь к директории модуля")] string outputPath,
        [Description("Имя задания в PascalCase")] string jobName,
        [Description("Пространство имён модуля")] string moduleName,
        [Description("Cron-расписание (по умолчанию: ежедневно в полночь)")] string cronSchedule = "0 0 * * *")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return "**ОШИБКА**: Параметр `outputPath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(jobName))
            return "**ОШИБКА**: Параметр `jobName` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(moduleName))
            return "**ОШИБКА**: Параметр `moduleName` не может быть пустым.";

        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        return await ExecuteAsync(outputPath, jobName, moduleName, cronSchedule);
    }

    internal static async Task<string> ExecuteAsync(string outputPath, string jobName, string moduleName, string cronSchedule)
    {
        var cron = string.IsNullOrWhiteSpace(cronSchedule) ? "0 0 * * *" : cronSchedule.Trim();
        var jobGuid = Guid.NewGuid().ToString("D");

        Directory.CreateDirectory(outputPath);

        var createdFiles = new List<string>();
        var updatedFiles = new List<string>();

        // 1. Update Module.mtd — add JobMetadata entry to Jobs array
        var mtdPath = Path.Combine(outputPath, "Module.mtd");
        UpdateModuleMtdWithJob(mtdPath, jobName, jobGuid, cron);
        updatedFiles.Add("Module.mtd");

        // 2. ModuleJobs.cs
        var jobsCs = GenerateModuleJobsCs(moduleName, jobName);
        await WriteFileAsync(outputPath, "ModuleJobs.cs", jobsCs);
        createdFiles.Add("ModuleJobs.cs");

        // 3. ModuleSystem.resx (neutral)
        var resxPath = Path.Combine(outputPath, "ModuleSystem.resx");
        AddJobKeysToResx(resxPath, jobName, isRussian: false);
        if (createdFiles.Contains("ModuleSystem.resx") || !File.Exists(resxPath + ".existed_before"))
            createdFiles.Add("ModuleSystem.resx");
        else
            updatedFiles.Add("ModuleSystem.resx");

        // 4. ModuleSystem.ru.resx
        var resxRuPath = Path.Combine(outputPath, "ModuleSystem.ru.resx");
        AddJobKeysToResx(resxRuPath, jobName, isRussian: true);
        if (!updatedFiles.Contains("ModuleSystem.ru.resx"))
            createdFiles.Add("ModuleSystem.ru.resx");

        var sb = new StringBuilder();
        sb.AppendLine("## Фоновое задание создано");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {jobName}");
        sb.AppendLine($"**Модуль:** {moduleName}");
        sb.AppendLine($"**GUID:** {jobGuid}");
        sb.AppendLine($"**Расписание (cron):** `{cron}`");
        sb.AppendLine();

        if (createdFiles.Count > 0)
        {
            sb.AppendLine($"### Созданные файлы ({createdFiles.Count})");
            sb.AppendLine();
            foreach (var f in createdFiles)
                sb.AppendLine($"- `{f}`");
            sb.AppendLine();
        }

        sb.AppendLine($"### Обновлённые файлы ({updatedFiles.Count})");
        sb.AppendLine();
        foreach (var f in updatedFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("1. Реализуйте логику задания в `ModuleJobs.cs`");
        sb.AppendLine("2. Проверьте cron-расписание в Module.mtd");
        sb.AppendLine("3. Запустите check_package для валидации");

        return sb.ToString();
    }

    internal static void UpdateModuleMtdWithJob(string mtdPath, string jobName, string jobGuid, string cron)
    {
        string json;
        if (File.Exists(mtdPath))
        {
            json = File.ReadAllText(mtdPath);
        }
        else
        {
            // Create stub Module.mtd
            json = "{\n  \"Jobs\": []\n}";
        }

        var jobEntry = $"    {{\n" +
                       $"      \"$type\": \"Sungero.Metadata.JobMetadata, Sungero.Metadata\",\n" +
                       $"      \"NameGuid\": \"{jobGuid}\",\n" +
                       $"      \"Name\": \"{jobName}\",\n" +
                       $"      \"GenerateHandler\": true,\n" +
                       $"      \"CronSchedule\": \"{cron}\"\n" +
                       $"    }}";

        // Check if Jobs array exists
        if (json.Contains("\"Jobs\""))
        {
            // Insert before the closing bracket of the Jobs array
            var jobsEndIndex = FindJobsArrayEnd(json);
            if (jobsEndIndex >= 0)
            {
                var beforeClose = json[..jobsEndIndex].TrimEnd();
                var afterClose = json[jobsEndIndex..];

                string insertion;
                if (beforeClose.EndsWith('['))
                {
                    // Empty array
                    insertion = $"\n{jobEntry}\n  ";
                }
                else
                {
                    insertion = $",\n{jobEntry}";
                }

                json = beforeClose + insertion + afterClose;
            }
        }
        else
        {
            // Add Jobs array before the last closing brace
            var lastBrace = json.LastIndexOf('}');
            if (lastBrace >= 0)
            {
                var before = json[..lastBrace].TrimEnd();
                var needsComma = before.TrimEnd().Length > 1 && !before.TrimEnd().EndsWith('{');
                var comma = needsComma ? "," : "";
                json = before + $"{comma}\n  \"Jobs\": [\n{jobEntry}\n  ]\n}}";
            }
            else
            {
                json = $"{{\n  \"Jobs\": [\n{jobEntry}\n  ]\n}}";
            }
        }

        File.WriteAllText(mtdPath, json);
    }

    internal static int FindJobsArrayEnd(string json)
    {
        // Find the closing ] of the Jobs array
        var jobsIdx = json.IndexOf("\"Jobs\"", StringComparison.Ordinal);
        if (jobsIdx < 0) return -1;

        var openBracket = json.IndexOf('[', jobsIdx);
        if (openBracket < 0) return -1;

        var depth = 0;
        for (var i = openBracket; i < json.Length; i++)
        {
            if (json[i] == '[') depth++;
            else if (json[i] == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
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
        string xml;
        if (File.Exists(resxPath))
        {
            xml = File.ReadAllText(resxPath);
        }
        else
        {
            xml = BuildEmptyResx();
        }

        var nameValue = isRussian ? jobName : jobName;
        var descValue = isRussian ? $"Описание задания {jobName}" : $"Description of job {jobName}";

        var nameKey = $"Job_{jobName}";
        var descKey = $"Job_{jobName}_Description";

        // Only add keys if they don't already exist
        if (!xml.Contains($"name=\"{nameKey}\""))
        {
            var dataName = $"  <data name=\"{nameKey}\" xml:space=\"preserve\">\n    <value>{nameValue}</value>\n  </data>";
            xml = InsertDataNodeBeforeRootClose(xml, dataName);
        }

        if (!xml.Contains($"name=\"{descKey}\""))
        {
            var dataDesc = $"  <data name=\"{descKey}\" xml:space=\"preserve\">\n    <value>{descValue}</value>\n  </data>";
            xml = InsertDataNodeBeforeRootClose(xml, dataDesc);
        }

        File.WriteAllText(resxPath, xml);
    }

    internal static string InsertDataNodeBeforeRootClose(string xml, string dataNode)
    {
        var closeRoot = xml.LastIndexOf("</root>", StringComparison.Ordinal);
        if (closeRoot >= 0)
            return xml[..closeRoot] + dataNode + "\n</root>";
        return xml + "\n" + dataNode;
    }

    internal static string BuildEmptyResx()
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

    private static async Task WriteFileAsync(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(fullPath, content);
    }
}
