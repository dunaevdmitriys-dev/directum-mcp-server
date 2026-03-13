using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldEntityTool
{
    private static bool IsPathAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Path.GetFullPath(solutionPath),
            Path.GetFullPath(Path.GetTempPath())
        };
        return allowedPaths.Any(bp =>
            bp.Length >= 4 &&
            fullPath.StartsWith(bp, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Dictionary<string, string> BaseGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabookEntry"] = "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
        ["Document"] = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
        ["Task"] = "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
        ["Assignment"] = "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24",
        ["Notice"] = "4e09273f-8b3a-489e-814e-a4ebfbba3e6c"
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

    [McpServerTool(Name = "scaffold_entity")]
    [Description("Генерация скелета новой сущности Directum RX или override существующей: MTD-метаданные, resx-ресурсы, серверные/клиентские функции.")]
    public async Task<string> ScaffoldEntity(
        [Description("Путь к директории, где будут созданы файлы сущности")] string outputPath,
        [Description("Имя сущности в PascalCase (например 'ContractDocument')")] string entityName,
        [Description("Пространство имён модуля (например 'DirRX.Contracts')")] string moduleName,
        [Description("Базовый тип: DatabookEntry, Document, Task, Assignment, Notice")] string baseType = "DatabookEntry",
        [Description("Режим: 'new' — создание с нуля, 'override' — переопределение существующей сущности")] string mode = "new",
        [Description("Свойства через запятую: 'Name:string,Amount:int,Status:enum(Active|Closed),Counterparty:navigation'")] string properties = "",
        [Description("GUID переопределяемой сущности (только для mode=override)")] string ancestorGuid = "")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return "**ОШИБКА**: Параметр `outputPath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(entityName))
            return "**ОШИБКА**: Параметр `entityName` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(moduleName))
            return "**ОШИБКА**: Параметр `moduleName` не может быть пустым.";

        if (!IsPathAllowed(outputPath))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{outputPath}` находится за пределами разрешённых директорий.";

        if (!BaseGuids.ContainsKey(baseType))
            return $"**ОШИБКА**: Неизвестный базовый тип `{baseType}`. Допустимые: {string.Join(", ", BaseGuids.Keys)}.";

        if (mode != "new" && mode != "override")
            return "**ОШИБКА**: Параметр `mode` должен быть 'new' или 'override'.";

        if (mode == "override" && string.IsNullOrWhiteSpace(ancestorGuid))
            return "**ОШИБКА**: Для режима 'override' необходимо указать `ancestorGuid`.";

        var parsedProperties = ParseProperties(properties);
        var entityGuid = Guid.NewGuid().ToString("D");
        var baseGuid = BaseGuids[baseType];

        Directory.CreateDirectory(outputPath);

        var createdFiles = new List<string>();

        // 1. MTD
        var mtdContent = GenerateMtd(entityName, entityGuid, baseGuid, baseType, moduleName, mode, ancestorGuid, parsedProperties);
        await WriteFileAsync(outputPath, $"{entityName}.mtd", mtdContent);
        createdFiles.Add($"{entityName}.mtd");

        // 2. System.resx (neutral)
        var resxContent = GenerateSystemResx(entityName, parsedProperties, isRussian: false);
        await WriteFileAsync(outputPath, $"{entityName}System.resx", resxContent);
        createdFiles.Add($"{entityName}System.resx");

        // 3. System.ru.resx
        var resxRuContent = GenerateSystemResx(entityName, parsedProperties, isRussian: true);
        await WriteFileAsync(outputPath, $"{entityName}System.ru.resx", resxRuContent);
        createdFiles.Add($"{entityName}System.ru.resx");

        // 4. Server functions
        var serverContent = GenerateCsFile(moduleName, "Server", entityName);
        await WriteFileAsync(outputPath, $"Server/{entityName}ServerFunctions.cs", serverContent);
        createdFiles.Add($"Server/{entityName}ServerFunctions.cs");

        // 5. Shared functions
        var sharedContent = GenerateCsFile(moduleName, "Shared", entityName);
        await WriteFileAsync(outputPath, $"Shared/{entityName}SharedFunctions.cs", sharedContent);
        createdFiles.Add($"Shared/{entityName}SharedFunctions.cs");

        // 6. ClientBase functions (only for Document and DatabookEntry)
        if (baseType is "Document" or "DatabookEntry")
        {
            var clientContent = GenerateCsFile(moduleName, "Client", entityName);
            await WriteFileAsync(outputPath, $"ClientBase/{entityName}ClientBaseFunctions.cs", clientContent);
            createdFiles.Add($"ClientBase/{entityName}ClientBaseFunctions.cs");
        }

        // Build report
        var sb = new StringBuilder();
        sb.AppendLine("## Сущность создана");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {entityName}");
        sb.AppendLine($"**Тип:** {baseType}");
        sb.AppendLine($"**Модуль:** {moduleName}");
        sb.AppendLine($"**Режим:** {mode}");
        sb.AppendLine($"**GUID:** {entityGuid}");
        sb.AppendLine();

        if (parsedProperties.Count > 0)
        {
            sb.AppendLine($"### Свойства ({parsedProperties.Count})");
            sb.AppendLine();
            sb.AppendLine("| Имя | Тип | GUID |");
            sb.AppendLine("|-----|-----|------|");
            foreach (var prop in parsedProperties)
                sb.AppendLine($"| {prop.Name} | {prop.RawType} | {prop.Guid} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Свойства (0)");
            sb.AppendLine();
            sb.AppendLine("| Имя | Тип | GUID |");
            sb.AppendLine("|-----|-----|------|");
            sb.AppendLine();
        }

        sb.AppendLine($"### Созданные файлы ({createdFiles.Count})");
        sb.AppendLine();
        foreach (var f in createdFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("1. Добавьте сущность в Module.mtd");
        sb.AppendLine("2. Запустите check_package для валидации");

        return sb.ToString();
    }

    #region MTD Generation

    private static string GenerateMtd(string entityName, string entityGuid, string baseGuid,
        string baseType, string moduleName, string mode, string ancestorGuid,
        List<PropertyDef> properties)
    {
        var metadataType = baseType switch
        {
            "Task" => "Sungero.Metadata.TaskMetadata",
            "Assignment" => "Sungero.Metadata.AssignmentMetadata",
            "Notice" => "Sungero.Metadata.NoticeMetadata",
            _ => "Sungero.Metadata.EntityMetadata"
        };

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"$type\": \"{metadataType}, Sungero.Metadata\",");
        sb.AppendLine($"  \"NameGuid\": \"{entityGuid}\",");
        sb.AppendLine($"  \"Name\": \"{entityName}\",");
        sb.AppendLine($"  \"BaseGuid\": \"{baseGuid}\",");

        if (mode == "override" && !string.IsNullOrWhiteSpace(ancestorGuid))
        {
            sb.AppendLine($"  \"AncestorGuid\": \"{ancestorGuid}\",");
        }

        sb.AppendLine($"  \"ModuleName\": \"{moduleName}\",");
        sb.AppendLine("  \"Actions\": [],");
        sb.AppendLine("  \"Properties\": [");

        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var isLast = i == properties.Count - 1;
            var propJson = GeneratePropertyJson(prop, mode);
            sb.Append(propJson);
            if (!isLast)
                sb.AppendLine(",");
            else
                sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GeneratePropertyJson(PropertyDef prop, string mode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    {");
        sb.AppendLine($"      \"$type\": \"{prop.MetadataType}, Sungero.Metadata\",");
        sb.AppendLine($"      \"NameGuid\": \"{prop.Guid}\",");
        sb.AppendLine($"      \"Name\": \"{prop.Name}\",");

        if (mode == "override")
        {
            sb.AppendLine("      \"IsAncestorMetadata\": false,");
        }

        if (prop.MetadataType == "Sungero.Metadata.EnumPropertyMetadata" && prop.EnumValues.Count > 0)
        {
            sb.AppendLine("      \"DirectValues\": [");
            for (int i = 0; i < prop.EnumValues.Count; i++)
            {
                var val = prop.EnumValues[i];
                var valGuid = Guid.NewGuid().ToString("D");
                var comma = i < prop.EnumValues.Count - 1 ? "," : "";
                sb.AppendLine("        {");
                sb.AppendLine($"          \"NameGuid\": \"{valGuid}\",");
                sb.AppendLine($"          \"Name\": \"{val}\"");
                sb.AppendLine($"        }}{comma}");
            }
            sb.AppendLine("      ],");
        }

        sb.AppendLine("      \"IsRequired\": false");
        sb.Append("    }");

        return sb.ToString();
    }

    #endregion

    #region Resx Generation

    private static string GenerateSystemResx(string entityName, List<PropertyDef> properties, bool isRussian)
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

        var displayValue = isRussian ? entityName : entityName;
        sb.AppendLine($"  <data name=\"DisplayName\" xml:space=\"preserve\">");
        sb.AppendLine($"    <value>{displayValue}</value>");
        sb.AppendLine("  </data>");

        foreach (var prop in properties)
        {
            var propValue = isRussian ? prop.Name : prop.Name;
            sb.AppendLine($"  <data name=\"Property_{prop.Name}\" xml:space=\"preserve\">");
            sb.AppendLine($"    <value>{propValue}</value>");
            sb.AppendLine("  </data>");
        }

        sb.AppendLine("</root>");
        return sb.ToString();
    }

    #endregion

    #region C# File Generation

    private static string GenerateCsFile(string moduleName, string layer, string entityName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.{layer}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {entityName}");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Property Parsing

    private static List<PropertyDef> ParseProperties(string properties)
    {
        var result = new List<PropertyDef>();
        if (string.IsNullOrWhiteSpace(properties))
            return result;

        foreach (var part in properties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex <= 0)
                continue;

            var name = part[..colonIndex].Trim();
            var rawType = part[(colonIndex + 1)..].Trim();
            var guid = Guid.NewGuid().ToString("D");
            var enumValues = new List<string>();

            string metadataType;

            // Check for enum(Value1|Value2|...)
            var enumMatch = Regex.Match(rawType, @"^enum\((.+)\)$", RegexOptions.IgnoreCase);
            if (enumMatch.Success)
            {
                metadataType = "Sungero.Metadata.EnumPropertyMetadata";
                enumValues = enumMatch.Groups[1].Value
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
            }
            else if (PropertyTypeMap.TryGetValue(rawType, out var mapped))
            {
                metadataType = mapped;
            }
            else
            {
                // Default to string for unknown types
                metadataType = "Sungero.Metadata.StringPropertyMetadata";
            }

            result.Add(new PropertyDef(name, rawType, metadataType, guid, enumValues));
        }

        return result;
    }

    #endregion

    #region Helpers

    private static async Task WriteFileAsync(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private record PropertyDef(string Name, string RawType, string MetadataType, string Guid, List<string> EnumValues);

    #endregion
}
