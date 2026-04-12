using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for scaffold_entity. Used by MCP tool and pipeline.
/// </summary>
public class EntityScaffoldService : IPipelineStep
{
    public string ToolName => "scaffold_entity";

    private static Dictionary<string, string> BaseGuids => DirectumConstants.BaseTypeToGuid;

    private static readonly Dictionary<string, string> PropertyTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "Sungero.Metadata.StringPropertyMetadata",
        ["int"] = "Sungero.Metadata.IntegerPropertyMetadata",
        ["double"] = "Sungero.Metadata.DoublePropertyMetadata",
        ["bool"] = "Sungero.Metadata.BooleanPropertyMetadata",
        ["date"] = "Sungero.Metadata.DateTimePropertyMetadata",
        ["datetime"] = "Sungero.Metadata.DateTimePropertyMetadata",
        ["text"] = "Sungero.Metadata.TextPropertyMetadata",
        ["navigation"] = "Sungero.Metadata.NavigationPropertyMetadata"
    };

    private static readonly Dictionary<string, string> DataBinderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
        ["text"] = "Sungero.Presentation.CommonDataBinders.StringEditorToStringBinder",
        ["int"] = "Sungero.Presentation.CommonDataBinders.NumericEditorToIntBinder",
        ["double"] = "Sungero.Presentation.CommonDataBinders.NumericEditorToDoubleBinder",
        ["bool"] = "Sungero.Presentation.CommonDataBinders.BooleanEditorToBooleanBinder",
        ["date"] = "Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder",
        ["datetime"] = "Sungero.Presentation.CommonDataBinders.DateTimeEditorToDateTimeBinder",
        ["navigation"] = "Sungero.Presentation.CommonDataBinders.DropDownEditorToNavigationBinder",
        ["enum"] = "Sungero.Presentation.CommonDataBinders.DropDownEditorToEnumerationBinder"
    };

    public async Task<ScaffoldEntityResult> ScaffoldAsync(
        string outputPath,
        string entityName,
        string moduleName,
        string baseType = "DatabookEntry",
        string mode = "new",
        string properties = "",
        string ancestorGuid = "",
        string russianName = "",
        CancellationToken ct = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(outputPath))
            return Fail("Параметр `outputPath` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(entityName))
            return Fail("Параметр `entityName` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(moduleName))
            return Fail("Параметр `moduleName` не может быть пустым.");
        if (!Regex.IsMatch(entityName, @"^[A-Za-z][A-Za-z0-9_]*$"))
            return Fail("`entityName` должен содержать только латинские буквы, цифры и подчёркивания.");
        if (!Regex.IsMatch(moduleName, @"^[A-Za-z][A-Za-z0-9_.]*$"))
            return Fail("`moduleName` должен содержать только латинские буквы, цифры, точки и подчёркивания.");
        if (!BaseGuids.ContainsKey(baseType))
            return Fail($"Неизвестный базовый тип `{baseType}`. Допустимые: {string.Join(", ", BaseGuids.Keys)}.");
        if (mode == "override" && string.IsNullOrWhiteSpace(ancestorGuid))
            return Fail("Для режима 'override' необходимо указать `ancestorGuid`.");

        var parsedProperties = ParseProperties(properties);
        var entityGuid = Guid.NewGuid().ToString("D");
        var baseGuid = BaseGuids[baseType];

        Directory.CreateDirectory(outputPath);
        var createdFiles = new List<string>();

        // 1. MTD
        var mtdContent = GenerateMtd(entityName, entityGuid, baseGuid, baseType, moduleName, mode, ancestorGuid, parsedProperties);
        await WriteFileAsync(outputPath, $"{entityName}.mtd", mtdContent, ct);
        createdFiles.Add($"{entityName}.mtd");

        // 2. System.resx (neutral)
        var resxContent = GenerateSystemResx(entityName, parsedProperties, isRussian: false, russianName: "");
        await WriteFileAsync(outputPath, $"{entityName}System.resx", resxContent, ct);
        createdFiles.Add($"{entityName}System.resx");

        // 3. System.ru.resx
        var resxRuContent = GenerateSystemResx(entityName, parsedProperties, isRussian: true, russianName: russianName);
        await WriteFileAsync(outputPath, $"{entityName}System.ru.resx", resxRuContent, ct);
        createdFiles.Add($"{entityName}System.ru.resx");

        // 4. Server functions
        var serverContent = GenerateCsFile(moduleName, "Server", entityName);
        await WriteFileAsync(outputPath, $"Server/{entityName}ServerFunctions.cs", serverContent, ct);
        createdFiles.Add($"Server/{entityName}ServerFunctions.cs");

        // 5. Shared functions
        var sharedContent = GenerateCsFile(moduleName, "Shared", entityName);
        await WriteFileAsync(outputPath, $"Shared/{entityName}SharedFunctions.cs", sharedContent, ct);
        createdFiles.Add($"Shared/{entityName}SharedFunctions.cs");

        // 6. ClientBase functions (only for Document and DatabookEntry)
        if (baseType is "Document" or "DatabookEntry")
        {
            var clientContent = GenerateCsFile(moduleName, "Client", entityName);
            await WriteFileAsync(outputPath, $"ClientBase/{entityName}ClientBaseFunctions.cs", clientContent, ct);
            createdFiles.Add($"ClientBase/{entityName}ClientBaseFunctions.cs");
        }

        return new ScaffoldEntityResult
        {
            Success = true,
            EntityName = entityName,
            EntityGuid = entityGuid,
            BaseType = baseType,
            ModuleName = moduleName,
            OutputPath = outputPath,
            CreatedFiles = createdFiles,
            Properties = parsedProperties
                .Select(p => new ScaffoldEntityResult.PropertyInfo(p.Name, p.RawType, p.Guid))
                .ToList()
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        return await ScaffoldAsync(
            outputPath: GetString(parameters, "outputPath"),
            entityName: GetString(parameters, "entityName"),
            moduleName: GetString(parameters, "moduleName"),
            baseType: GetString(parameters, "baseType", "DatabookEntry"),
            mode: GetString(parameters, "mode", "new"),
            properties: GetString(parameters, "properties"),
            ancestorGuid: GetString(parameters, "ancestorGuid"),
            russianName: GetString(parameters, "russianName"),
            ct: ct);
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
        var code = entityName.Length > 20 ? entityName[..20] : entityName;
        sb.AppendLine($"  \"Name\": \"{entityName}\",");
        sb.AppendLine($"  \"Code\": \"{code}\",");
        sb.AppendLine($"  \"BaseGuid\": \"{baseGuid}\",");
        sb.AppendLine("  \"CanBeNavigationPropertyType\": true,");

        if (mode == "override" && !string.IsNullOrWhiteSpace(ancestorGuid))
            sb.AppendLine($"  \"AncestorGuid\": \"{ancestorGuid}\",");

        sb.AppendLine($"  \"ModuleName\": \"{moduleName}\",");
        sb.AppendLine("  \"Actions\": [],");

        var creationAreaGuid = Guid.NewGuid().ToString("D");
        sb.AppendLine("  \"CreationAreaMetadata\": {");
        sb.AppendLine($"    \"NameGuid\": \"{creationAreaGuid}\",");
        sb.AppendLine("    \"Name\": \"CreationArea\",");
        sb.AppendLine("    \"Buttons\": [],");
        sb.AppendLine("    \"Versions\": []");
        sb.AppendLine("  },");

        var filterPanelGuid = Guid.NewGuid().ToString("D");
        sb.AppendLine("  \"FilterPanel\": {");
        sb.AppendLine($"    \"NameGuid\": \"{filterPanelGuid}\",");
        sb.AppendLine("    \"Name\": \"FilterPanel\",");
        sb.AppendLine("    \"Controls\": [],");
        sb.AppendLine("    \"Versions\": []");
        sb.AppendLine("  },");

        if (baseType is "DatabookEntry" or "Document")
            GenerateFormsSection(sb, properties);

        sb.AppendLine("  \"Properties\": [");
        for (int i = 0; i < properties.Count; i++)
        {
            var isLast = i == properties.Count - 1;
            sb.Append(GeneratePropertyJson(properties[i], mode));
            sb.AppendLine(isLast ? "" : ",");
        }
        sb.AppendLine("  ],");

        // Versions — required by DDS, otherwise "Invalid .mtd"
        sb.AppendLine("  \"Versions\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"Type\": \"{metadataType.Split('.').Last()}\",");
        sb.AppendLine("      \"Number\": 13");
        sb.AppendLine("    },");
        sb.AppendLine("    {");
        sb.AppendLine("      \"Type\": \"DomainApi\",");
        sb.AppendLine("      \"Number\": 2");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateFormsSection(StringBuilder sb, List<PropertyDef> properties)
    {
        var formGuid = Guid.NewGuid().ToString("D");
        var controlGroupGuid = Guid.NewGuid().ToString("D");

        sb.AppendLine("  \"Forms\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"$type\": \"Sungero.Metadata.StandaloneFormMetadata, Sungero.Metadata\",");
        sb.AppendLine($"      \"NameGuid\": \"{formGuid}\",");
        sb.AppendLine("      \"Name\": \"Card\",");
        sb.AppendLine("      \"Controls\": [");
        sb.AppendLine("        {");
        sb.AppendLine("          \"$type\": \"Sungero.Metadata.ControlGroupMetadata, Sungero.Metadata\",");
        sb.AppendLine($"          \"NameGuid\": \"{controlGroupGuid}\",");
        sb.AppendLine("          \"Name\": \"Main\",");
        sb.AppendLine("          \"ColumnDefinitions\": [");
        sb.AppendLine("            {");
        sb.AppendLine("              \"Percentage\": 100.0");
        sb.AppendLine("            }");
        sb.AppendLine("          ],");
        sb.AppendLine("          \"Controls\": [");

        for (int i = 0; i < properties.Count; i++)
        {
            var prop = properties[i];
            var controlGuid = Guid.NewGuid().ToString("D");
            var dataBinder = GetDataBinder(prop.RawType);
            var comma = i == properties.Count - 1 ? "" : ",";

            sb.AppendLine("            {");
            sb.AppendLine("              \"$type\": \"Sungero.Metadata.ControlMetadata, Sungero.Metadata\",");
            sb.AppendLine($"              \"NameGuid\": \"{controlGuid}\",");
            sb.AppendLine($"              \"Name\": \"{prop.Name}\",");
            sb.AppendLine("              \"ColumnNumber\": 0,");
            sb.AppendLine("              \"ColumnSpan\": 1,");
            sb.AppendLine($"              \"DataBinderTypeName\": \"{dataBinder}\",");
            sb.AppendLine($"              \"ParentGuid\": \"{controlGroupGuid}\",");
            sb.AppendLine($"              \"PropertyGuid\": \"{prop.Guid}\",");
            sb.AppendLine($"              \"RowNumber\": {i},");
            sb.AppendLine("              \"RowSpan\": 1");
            sb.AppendLine($"            }}{comma}");
        }

        sb.AppendLine("          ],");
        sb.AppendLine("          \"Versions\": []");
        sb.AppendLine("        }");
        sb.AppendLine("      ],");
        sb.AppendLine("      \"Versions\": []");
        sb.AppendLine("    }");
        sb.AppendLine("  ],");
    }

    private static string GetDataBinder(string rawType)
    {
        if (rawType.StartsWith("enum", StringComparison.OrdinalIgnoreCase))
            return DataBinderMap["enum"];
        return DataBinderMap.GetValueOrDefault(rawType, DataBinderMap["string"]);
    }

    private static string GeneratePropertyJson(PropertyDef prop, string mode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("    {");
        sb.AppendLine($"      \"$type\": \"{prop.MetadataType}, Sungero.Metadata\",");
        sb.AppendLine($"      \"NameGuid\": \"{prop.Guid}\",");
        sb.AppendLine($"      \"Name\": \"{prop.Name}\",");
        sb.AppendLine($"      \"Code\": \"{prop.Name}\",");

        // ListDataBinderTypeName — required for property visibility in list views
        var listBinder = GetDataBinder(prop.RawType);
        sb.AppendLine($"      \"ListDataBinderTypeName\": \"{listBinder}\",");

        if (mode == "override")
            sb.AppendLine("      \"IsAncestorMetadata\": false,");

        if (prop.MetadataType == "Sungero.Metadata.EnumPropertyMetadata" && prop.EnumValues.Count > 0)
        {
            sb.AppendLine("      \"DirectValues\": [");
            for (int i = 0; i < prop.EnumValues.Count; i++)
            {
                var valGuid = Guid.NewGuid().ToString("D");
                var comma = i < prop.EnumValues.Count - 1 ? "," : "";
                sb.AppendLine("        {");
                sb.AppendLine($"          \"NameGuid\": \"{valGuid}\",");
                sb.AppendLine($"          \"Name\": \"{prop.EnumValues[i]}\"");
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

    internal static string GenerateSystemResx(string entityName, List<PropertyDef> properties, bool isRussian, string russianName)
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

        var displayValue = isRussian
            ? (!string.IsNullOrWhiteSpace(russianName) ? russianName : $"[RU] {entityName}")
            : entityName;
        sb.AppendLine($"  <data name=\"DisplayName\" xml:space=\"preserve\">");
        sb.AppendLine($"    <value>{displayValue}</value>");
        sb.AppendLine("  </data>");

        var collectionDisplayValue = isRussian
            ? (!string.IsNullOrWhiteSpace(russianName) ? russianName : $"[RU] {entityName}")
            : entityName;
        sb.AppendLine($"  <data name=\"CollectionDisplayName\" xml:space=\"preserve\">");
        sb.AppendLine($"    <value>{collectionDisplayValue}</value>");
        sb.AppendLine("  </data>");

        foreach (var prop in properties)
        {
            var propValue = isRussian ? $"[TODO] {prop.Name}" : prop.Name;
            sb.AppendLine($"  <data name=\"Property_{prop.Name}\" xml:space=\"preserve\">");
            sb.AppendLine($"    <value>{propValue}</value>");
            sb.AppendLine("  </data>");
        }

        sb.AppendLine("</root>");
        return sb.ToString();
    }

    #endregion

    #region C# Generation

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

    internal static List<PropertyDef> ParseProperties(string properties)
    {
        var result = new List<PropertyDef>();
        if (string.IsNullOrWhiteSpace(properties))
            return result;

        foreach (var part in properties.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex <= 0) continue;

            var name = part[..colonIndex].Trim();
            var rawType = part[(colonIndex + 1)..].Trim();
            var guid = Guid.NewGuid().ToString("D");
            var enumValues = new List<string>();
            string metadataType;

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
                metadataType = "Sungero.Metadata.StringPropertyMetadata";
            }

            result.Add(new PropertyDef(name, rawType, metadataType, guid, enumValues));
        }

        return result;
    }

    #endregion

    #region Helpers

    internal static async Task WriteFileAsync(string root, string relativePath, string content, CancellationToken ct = default)
    {
        var fullPath = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(fullPath, content, ct);
    }

    private static ScaffoldEntityResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetString(Dictionary<string, JsonElement> p, string key, string def = "")
    {
        if (p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? def;
        return def;
    }

    internal record PropertyDef(string Name, string RawType, string MetadataType, string Guid, List<string> EnumValues);

    #endregion
}
