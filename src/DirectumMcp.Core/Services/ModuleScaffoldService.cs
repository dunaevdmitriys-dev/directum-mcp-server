using System.Text;
using System.Text.Json;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for scaffold_module. Generates complete module structure.
/// </summary>
public class ModuleScaffoldService : IPipelineStep
{
    public string ToolName => "scaffold_module";

    public async Task<ScaffoldModuleResult> ScaffoldAsync(
        string outputPath,
        string moduleName,
        string companyCode = "DirRX",
        string displayNameRu = "",
        string version = "1.0.0.0",
        string dependencies = "",
        bool hasCover = true,
        string coverGroups = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return Fail("Параметр `outputPath` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(moduleName))
            return Fail("Параметр `moduleName` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(companyCode))
            return Fail("Параметр `companyCode` не может быть пустым.");

        var fullName = $"{companyCode}.{moduleName}";
        var moduleGuid = Guid.NewGuid().ToString("D");
        var ruName = string.IsNullOrWhiteSpace(displayNameRu) ? moduleName : displayNameRu;

        // Parse dependencies
        var deps = ParseDependencies(dependencies);
        // Parse cover groups
        var groups = string.IsNullOrWhiteSpace(coverGroups)
            ? new List<string>()
            : coverGroups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        // Module root directory
        var moduleDir = Path.Combine(outputPath, fullName);
        var sharedDir = Path.Combine(moduleDir, $"{fullName}.Shared");
        var serverDir = Path.Combine(moduleDir, $"{fullName}.Server");
        var clientBaseDir = Path.Combine(moduleDir, $"{fullName}.ClientBase");
        var clientDir = Path.Combine(moduleDir, $"{fullName}.Client");
        var isolatedDir = Path.Combine(moduleDir, $"{fullName}.Isolated");

        Directory.CreateDirectory(sharedDir);
        Directory.CreateDirectory(serverDir);
        Directory.CreateDirectory(clientBaseDir);
        Directory.CreateDirectory(clientDir);
        Directory.CreateDirectory(isolatedDir);

        var createdFiles = new List<string>();

        // 1. Module.mtd
        var mtdContent = GenerateModuleMtd(moduleName, moduleGuid, companyCode, fullName, version, deps, hasCover, groups);
        await WriteFile(sharedDir, "Module.mtd", mtdContent, ct);
        createdFiles.Add($"{fullName}.Shared/Module.mtd");

        // 2. ModuleSystem.resx (neutral)
        var resxContent = GenerateModuleSystemResx(moduleName, groups, isRussian: false);
        await WriteFile(sharedDir, "ModuleSystem.resx", resxContent, ct);
        createdFiles.Add($"{fullName}.Shared/ModuleSystem.resx");

        // 3. ModuleSystem.ru.resx
        var resxRuContent = GenerateModuleSystemResx(ruName, groups, isRussian: true);
        await WriteFile(sharedDir, "ModuleSystem.ru.resx", resxRuContent, ct);
        createdFiles.Add($"{fullName}.Shared/ModuleSystem.ru.resx");

        // 4. Module.resx / Module.ru.resx (user resources — empty)
        var emptyResx = EntityScaffoldService.GenerateSystemResx("Module", [], isRussian: false, russianName: "");
        await WriteFile(sharedDir, "Module.resx", emptyResx, ct);
        createdFiles.Add($"{fullName}.Shared/Module.resx");
        var emptyResxRu = EntityScaffoldService.GenerateSystemResx("Module", [], isRussian: true, russianName: ruName);
        await WriteFile(sharedDir, "Module.ru.resx", emptyResxRu, ct);
        createdFiles.Add($"{fullName}.Shared/Module.ru.resx");

        // 5. ModuleSharedFunctions.cs
        await WriteFile(sharedDir, "ModuleSharedFunctions.cs", GenerateCs(fullName, "Shared", "ModuleFunctions"), ct);
        createdFiles.Add($"{fullName}.Shared/ModuleSharedFunctions.cs");

        // 6. ModuleConstants.cs
        await WriteFile(sharedDir, "ModuleConstants.cs", GenerateConstants(fullName, moduleGuid), ct);
        createdFiles.Add($"{fullName}.Shared/ModuleConstants.cs");

        // 7. ModuleStructures.cs
        await WriteFile(sharedDir, "ModuleStructures.cs", GenerateCs(fullName, "Shared", "ModuleStructures"), ct);
        createdFiles.Add($"{fullName}.Shared/ModuleStructures.cs");

        // 8. Server files
        await WriteFile(serverDir, "ModuleServerFunctions.cs", GenerateCs(fullName, "Server", "ModuleFunctions"), ct);
        createdFiles.Add($"{fullName}.Server/ModuleServerFunctions.cs");

        await WriteFile(serverDir, "ModuleHandlers.cs", GenerateCs(fullName, "Server", "ModuleHandlers"), ct);
        createdFiles.Add($"{fullName}.Server/ModuleHandlers.cs");

        await WriteFile(serverDir, "ModuleInitializer.cs", GenerateInitializer(fullName), ct);
        createdFiles.Add($"{fullName}.Server/ModuleInitializer.cs");

        await WriteFile(serverDir, "ModuleJobs.cs", GenerateCs(fullName, "Server", "ModuleJobs"), ct);
        createdFiles.Add($"{fullName}.Server/ModuleJobs.cs");

        await WriteFile(serverDir, "ModuleAsyncHandlers.cs", GenerateCs(fullName, "Server", "ModuleAsyncHandlers"), ct);
        createdFiles.Add($"{fullName}.Server/ModuleAsyncHandlers.cs");

        // 9. ClientBase files
        await WriteFile(clientBaseDir, "ModuleClientFunctions.cs", GenerateClientFunctions(fullName, groups), ct);
        createdFiles.Add($"{fullName}.ClientBase/ModuleClientFunctions.cs");

        await WriteFile(clientBaseDir, "ModuleHandlers.cs", GenerateCs(fullName, "Client", "ModuleHandlers"), ct);
        createdFiles.Add($"{fullName}.ClientBase/ModuleHandlers.cs");

        // 10. .sds/Libraries/Analyzers/ (empty — warning)
        var analyzersDir = Path.Combine(sharedDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);
        createdFiles.Add($"{fullName}.Shared/.sds/Libraries/Analyzers/");

        return new ScaffoldModuleResult
        {
            Success = true,
            ModulePath = moduleDir,
            ModuleGuid = moduleGuid,
            FullName = fullName,
            DisplayNameRu = ruName,
            CreatedFiles = createdFiles,
            Warnings = [".sds/Libraries/Analyzers/ создана пустой — скопируйте DLL из DDS installation"]
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        return await ScaffoldAsync(
            outputPath: GetStr(parameters, "outputPath"),
            moduleName: GetStr(parameters, "moduleName"),
            companyCode: GetStr(parameters, "companyCode", "DirRX"),
            displayNameRu: GetStr(parameters, "displayNameRu"),
            version: GetStr(parameters, "version", "1.0.0.0"),
            dependencies: GetStr(parameters, "dependencies"),
            hasCover: GetBool(parameters, "hasCover", true),
            coverGroups: GetStr(parameters, "coverGroups"),
            ct: ct);
    }

    #region Module.mtd Generation

    private static string GenerateModuleMtd(
        string moduleName, string moduleGuid, string companyCode, string fullName,
        string version, List<DependencyInfo> deps, bool hasCover, List<string> coverGroups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"$type\": \"Sungero.Metadata.ModuleMetadata, Sungero.Metadata\",");
        sb.AppendLine($"  \"NameGuid\": \"{moduleGuid}\",");
        sb.AppendLine($"  \"Name\": \"{moduleName}\",");
        sb.AppendLine($"  \"Code\": \"{moduleName}\",");
        sb.AppendLine($"  \"CompanyCode\": \"{companyCode}\",");
        sb.AppendLine("  \"Importance\": \"Medium\",");
        sb.AppendLine($"  \"Version\": \"{version}\",");
        sb.AppendLine("  \"IsVisible\": true,");
        sb.AppendLine();

        // Assembly names & namespaces
        sb.AppendLine($"  \"ClientAssemblyName\": \"{fullName}.Client\",");
        sb.AppendLine($"  \"ClientBaseAssemblyName\": \"{fullName}.ClientBase\",");
        sb.AppendLine($"  \"ClientBaseNamespace\": \"{fullName}.ClientBase\",");
        sb.AppendLine($"  \"ClientNamespace\": \"{fullName}.Client\",");
        sb.AppendLine($"  \"ServerAssemblyName\": \"{fullName}.Server\",");
        sb.AppendLine($"  \"ServerNamespace\": \"{fullName}.Server\",");
        sb.AppendLine($"  \"SharedAssemblyName\": \"{fullName}.Shared\",");
        sb.AppendLine($"  \"SharedNamespace\": \"{fullName}.Shared\",");
        sb.AppendLine($"  \"IsolatedAssemblyName\": \"{fullName}.Isolated\",");
        sb.AppendLine($"  \"IsolatedNamespace\": \"{fullName}.Isolated\",");
        sb.AppendLine("  \"InterfaceAssemblyName\": \"Sungero.Domain.Interfaces\",");
        sb.AppendLine($"  \"InterfaceNamespace\": \"{fullName}\",");
        sb.AppendLine("  \"ResourceInterfaceAssemblyName\": \"Sungero.Domain.Interfaces\",");
        sb.AppendLine($"  \"ResourceInterfaceNamespace\": \"{fullName}\",");
        sb.AppendLine();

        // Sections
        sb.AppendLine("  \"AsyncHandlers\": [],");
        sb.AppendLine("  \"Jobs\": [],");
        sb.AppendLine("  \"Blocks\": [],");

        // Cover
        if (hasCover)
            GenerateCoverSection(sb, coverGroups);
        else
            sb.AppendLine("  \"Cover\": null,");

        // Dependencies
        sb.AppendLine("  \"Dependencies\": [");
        for (int i = 0; i < deps.Count; i++)
        {
            var d = deps[i];
            var comma = i < deps.Count - 1 ? "," : "";
            sb.AppendLine("    {");
            sb.AppendLine($"      \"Id\": \"{d.Guid}\",");
            sb.AppendLine($"      \"IsSolutionModule\": {(d.IsSolution ? "true" : "false")},");
            sb.AppendLine("      \"MaxVersion\": \"\",");
            sb.AppendLine("      \"MinVersion\": \"\"");
            sb.AppendLine($"    }}{comma}");
        }
        sb.AppendLine("  ],");

        sb.AppendLine("  \"ExplorerTreeOrder\": [],");
        sb.AppendLine("  \"HandledEvents\": [\"InitializingServer\"],");
        sb.AppendLine("  \"Libraries\": [],");
        sb.AppendLine("  \"Overridden\": [],");
        sb.AppendLine("  \"PublicConstants\": [],");
        sb.AppendLine("  \"PublicFunctions\": [],");
        sb.AppendLine("  \"PublicStructures\": [],");

        // ResourcesKeys
        sb.AppendLine("  \"ResourcesKeys\": [");
        sb.Append("    \"DisplayName\"");
        foreach (var g in coverGroups)
        {
            sb.AppendLine(",");
            sb.Append($"    \"CoverGroup_{SanitizeName(g)}\"");
        }
        sb.AppendLine();
        sb.AppendLine("  ],");

        sb.AppendLine("  \"SpecialFolders\": [],");
        sb.AppendLine("  \"Widgets\": [],");

        // Versions
        sb.AppendLine("  \"Versions\": [");
        sb.AppendLine("    {");
        sb.AppendLine("      \"Type\": \"ModuleMetadata\",");
        sb.AppendLine("      \"Number\": 12");
        sb.AppendLine("    },");
        sb.AppendLine("    {");
        sb.AppendLine("      \"Type\": \"DomainApi\",");
        sb.AppendLine("      \"Number\": 3");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateCoverSection(StringBuilder sb, List<string> coverGroups)
    {
        var coverGuid = Guid.NewGuid().ToString("D");
        var headerGuid = Guid.NewGuid().ToString("D");
        var footerGuid = Guid.NewGuid().ToString("D");
        var tabGuid = Guid.NewGuid().ToString("D");

        sb.AppendLine("  \"Cover\": {");
        sb.AppendLine($"    \"NameGuid\": \"{coverGuid}\",");

        // Header & Footer
        sb.AppendLine("    \"Header\": {");
        sb.AppendLine($"      \"NameGuid\": \"{headerGuid}\",");
        sb.AppendLine("      \"BackgroundPosition\": \"Stretch\",");
        sb.AppendLine("      \"Versions\": []");
        sb.AppendLine("    },");
        sb.AppendLine("    \"Footer\": {");
        sb.AppendLine($"      \"NameGuid\": \"{footerGuid}\",");
        sb.AppendLine("      \"BackgroundPosition\": \"Stretch\",");
        sb.AppendLine("      \"Versions\": []");
        sb.AppendLine("    },");
        sb.AppendLine("    \"Background\": null,");

        // Tab
        sb.AppendLine("    \"Tabs\": [");
        sb.AppendLine("      {");
        sb.AppendLine($"        \"NameGuid\": \"{tabGuid}\",");
        sb.AppendLine("        \"Name\": \"Main\"");
        sb.AppendLine("      }");
        sb.AppendLine("    ],");

        // Groups
        sb.AppendLine("    \"Groups\": [");
        string? prevGroupGuid = null;
        var groupGuids = new List<string>();
        for (int i = 0; i < coverGroups.Count; i++)
        {
            var groupGuid = Guid.NewGuid().ToString("D");
            groupGuids.Add(groupGuid);
            var comma = i < coverGroups.Count - 1 ? "," : "";

            sb.AppendLine("      {");
            sb.AppendLine($"        \"NameGuid\": \"{groupGuid}\",");
            sb.AppendLine($"        \"Name\": \"{SanitizeName(coverGroups[i])}\",");
            sb.AppendLine($"        \"TabId\": \"{tabGuid}\",");
            sb.AppendLine("        \"BackgroundPosition\": \"Stretch\",");
            if (prevGroupGuid != null)
                sb.AppendLine($"        \"PreviousItemGuid\": \"{prevGroupGuid}\",");
            sb.AppendLine("        \"Versions\": []");
            sb.AppendLine($"      }}{comma}");

            prevGroupGuid = groupGuid;
        }
        sb.AppendLine("    ],");

        sb.AppendLine("    \"Actions\": [],");
        sb.AppendLine("    \"RemoteControls\": []");
        sb.AppendLine("  },");
    }

    #endregion

    #region Resx Generation

    private static string GenerateModuleSystemResx(string displayName, List<string> coverGroups, bool isRussian)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<root>");
        AppendResxSchema(sb);

        sb.AppendLine($"  <data name=\"DisplayName\" xml:space=\"preserve\">");
        sb.AppendLine($"    <value>{displayName}</value>");
        sb.AppendLine("  </data>");

        foreach (var g in coverGroups)
        {
            var name = SanitizeName(g);
            var value = isRussian ? g : name;
            sb.AppendLine($"  <data name=\"CoverGroup_{name}\" xml:space=\"preserve\">");
            sb.AppendLine($"    <value>{value}</value>");
            sb.AppendLine("  </data>");
        }

        sb.AppendLine("</root>");
        return sb.ToString();
    }

    private static void AppendResxSchema(StringBuilder sb)
    {
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
    }

    #endregion

    #region C# Generation

    private static string GenerateCs(string fullName, string layer, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {fullName}.{layer}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {className}");
        sb.AppendLine("    {");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateInitializer(string fullName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine("using Sungero.Domain.Initialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {fullName}.Server");
        sb.AppendLine("{");
        sb.AppendLine("    public partial class ModuleInitializer");
        sb.AppendLine("    {");
        sb.AppendLine("        public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(");
        sb.AppendLine("                this.FirstInitializing,");
        sb.AppendLine("                Constants.Module.Init.Name,");
        sb.AppendLine("                Version.Parse(Constants.Module.Init.FirstVersion));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public virtual void FirstInitializing()");
        sb.AppendLine("        {");
        sb.AppendLine($"            InitializationLogger.Debug(\"Init: {fullName} — первичная инициализация.\");");
        sb.AppendLine("            // TODO: CreateRoles(), GrantRights(), FillDatabooks()");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateConstants(string fullName, string moduleGuid)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {fullName}");
        sb.AppendLine("{");
        sb.AppendLine("    partial class Constants");
        sb.AppendLine("    {");
        sb.AppendLine("        public static class Module");
        sb.AppendLine("        {");
        sb.AppendLine($"            public static readonly Guid ModuleGuid = new(\"{moduleGuid}\");");
        sb.AppendLine();
        sb.AppendLine("            public static class Init");
        sb.AppendLine("            {");
        sb.AppendLine($"                public const string Name = \"{fullName}\";");
        sb.AppendLine("                public const string FirstVersion = \"0.0.1.0\";");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateClientFunctions(string fullName, List<string> coverGroups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {fullName}.Client");
        sb.AppendLine("{");
        sb.AppendLine("    partial class ModuleFunctions");
        sb.AppendLine("    {");

        // Placeholder functions for cover — so FunctionName references are valid
        if (coverGroups.Count > 0)
        {
            sb.AppendLine("        // Cover action functions (placeholder)");
            sb.AppendLine("        // Добавляйте функции для CoverFunctionActionMetadata здесь.");
            sb.AppendLine("        // Имя метода ДОЛЖНО точно совпадать с FunctionName в Module.mtd.");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static List<DependencyInfo> ParseDependencies(string dependencies)
    {
        var result = new List<DependencyInfo>();
        if (string.IsNullOrWhiteSpace(dependencies)) return result;

        foreach (var part in dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Support format: "guid" or "guid:solution"
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0)
            {
                result.Add(new DependencyInfo(
                    part[..colonIdx].Trim(),
                    part[(colonIdx + 1)..].Trim().Equals("solution", StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                result.Add(new DependencyInfo(part.Trim(), false));
            }
        }
        return result;
    }

    private static string SanitizeName(string name)
    {
        // Remove spaces and special chars for code identifiers
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }

    private static async Task WriteFile(string dir, string fileName, string content, CancellationToken ct)
    {
        var path = Path.Combine(dir, fileName);
        var d = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
        await File.WriteAllTextAsync(path, content, ct);
    }

    private static ScaffoldModuleResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetStr(Dictionary<string, JsonElement> p, string key, string def = "") =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : def;

    private static bool GetBool(Dictionary<string, JsonElement> p, string key, bool def = false) =>
        p.TryGetValue(key, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean() : def;

    private record DependencyInfo(string Guid, bool IsSolution);

    #endregion
}
