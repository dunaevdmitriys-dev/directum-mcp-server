using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for scaffold_function. Generates server/client functions and updates Module.mtd.
/// </summary>
public class FunctionScaffoldService : IPipelineStep
{
    public string ToolName => "scaffold_function";

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "global::System.String",
        ["int"] = "global::System.Int32",
        ["long"] = "global::System.Int64",
        ["bool"] = "global::System.Boolean",
        ["double"] = "global::System.Double",
        ["decimal"] = "global::System.Decimal",
        ["DateTime"] = "global::System.DateTime",
        ["Guid"] = "global::System.Guid",
        ["void"] = "",
    };

    private static readonly Dictionary<string, string> TypeFullNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["string"] = "System.String",
        ["int"] = "System.Int32",
        ["long"] = "System.Int64",
        ["bool"] = "System.Boolean",
        ["double"] = "System.Double",
        ["decimal"] = "System.Decimal",
        ["DateTime"] = "System.DateTime",
        ["Guid"] = "System.Guid",
    };

    public async Task<ScaffoldFunctionResult> ScaffoldAsync(
        string modulePath,
        string functionName,
        string moduleName,
        string? entityName = null,
        string returnType = "void",
        string parameters = "",
        string side = "server",
        bool isPublic = false,
        bool isRemote = false,
        string? body = null,
        string? description = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
            return Fail("Параметр `modulePath` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(functionName))
            return Fail("Параметр `functionName` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(moduleName))
            return Fail("Параметр `moduleName` не может быть пустым.");
        if (!Regex.IsMatch(functionName, @"^[A-Za-z][A-Za-z0-9_]*$"))
            return Fail("`functionName` должен содержать только латинские буквы, цифры и подчёркивания.");

        side = side.ToLowerInvariant();
        if (side is not ("server" or "client" or "shared"))
            return Fail("Параметр `side` должен быть 'server', 'client' или 'shared'.");

        var parsedParams = ParseParameters(parameters);
        var createdFiles = new List<string>();
        var modifiedFiles = new List<string>();
        bool mtdUpdated = false;

        // Determine target file and class name
        string targetDir;
        string targetFile;
        string className;
        string nsLayer;

        if (string.IsNullOrWhiteSpace(entityName))
        {
            // Module-level function
            switch (side)
            {
                case "server":
                    targetDir = Path.Combine(modulePath, $"{moduleName}.Server");
                    targetFile = "ModuleServerFunctions.cs";
                    className = "ModuleFunctions";
                    nsLayer = "Server";
                    break;
                case "client":
                    targetDir = Path.Combine(modulePath, $"{moduleName}.ClientBase");
                    targetFile = "ModuleClientFunctions.cs";
                    className = "ModuleFunctions";
                    nsLayer = "Client";
                    break;
                default: // shared
                    targetDir = Path.Combine(modulePath, $"{moduleName}.Shared");
                    targetFile = "ModuleSharedFunctions.cs";
                    className = "ModuleFunctions";
                    nsLayer = "Shared";
                    break;
            }
        }
        else
        {
            // Entity-level function
            switch (side)
            {
                case "server":
                    targetDir = Path.Combine(modulePath, $"{moduleName}.Server");
                    targetFile = $"{entityName}ServerFunctions.cs";
                    className = $"{entityName}Functions";
                    nsLayer = "Server";
                    break;
                case "client":
                    targetDir = Path.Combine(modulePath, $"{moduleName}.ClientBase");
                    targetFile = $"{entityName}ClientFunctions.cs";
                    className = $"{entityName}Functions";
                    nsLayer = "Client";
                    break;
                default:
                    targetDir = Path.Combine(modulePath, $"{moduleName}.Shared");
                    targetFile = $"{entityName}SharedFunctions.cs";
                    className = $"{entityName}Functions";
                    nsLayer = "Shared";
                    break;
            }
        }

        Directory.CreateDirectory(targetDir);
        var fullPath = Path.Combine(targetDir, targetFile);

        // Generate function code
        var functionCode = GenerateFunctionCode(
            functionName, returnType, parsedParams, side, isPublic, isRemote, body, description);

        // Insert into existing file or create new
        if (File.Exists(fullPath))
        {
            var existing = await File.ReadAllTextAsync(fullPath, ct);
            var updated = InsertFunctionIntoClass(existing, className, functionCode);
            await File.WriteAllTextAsync(fullPath, updated, ct);
            modifiedFiles.Add(targetFile);
        }
        else
        {
            var newFile = GenerateNewFunctionFile(moduleName, nsLayer, className, functionCode);
            await File.WriteAllTextAsync(fullPath, newFile, ct);
            createdFiles.Add(targetFile);
        }

        // Update Module.mtd PublicFunctions if isPublic
        if (isPublic && side == "server")
        {
            var mtdPath = FindModuleMtd(modulePath, moduleName);
            if (mtdPath != null)
            {
                mtdUpdated = await UpdateModuleMtdPublicFunctions(
                    mtdPath, functionName, returnType, parsedParams, isRemote, ct);
                if (mtdUpdated)
                    modifiedFiles.Add("Module.mtd");
            }
        }

        return new ScaffoldFunctionResult
        {
            Success = true,
            FunctionName = functionName,
            Side = side,
            IsPublic = isPublic,
            IsRemote = isRemote,
            CreatedFiles = createdFiles,
            ModifiedFiles = modifiedFiles,
            MtdUpdated = mtdUpdated
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        return await ScaffoldAsync(
            modulePath: GetStr(parameters, "modulePath"),
            functionName: GetStr(parameters, "functionName"),
            moduleName: GetStr(parameters, "moduleName"),
            entityName: GetStrOrNull(parameters, "entityName"),
            returnType: GetStr(parameters, "returnType", "void"),
            parameters: GetStr(parameters, "parameters"),
            side: GetStr(parameters, "side", "server"),
            isPublic: GetBool(parameters, "isPublic"),
            isRemote: GetBool(parameters, "isRemote"),
            body: GetStrOrNull(parameters, "body"),
            description: GetStrOrNull(parameters, "description"),
            ct: ct);
    }

    #region Code Generation

    private static string GenerateFunctionCode(
        string functionName, string returnType, List<ParamDef> parameters,
        string side, bool isPublic, bool isRemote, string? body, string? description)
    {
        var sb = new StringBuilder();

        // XML doc
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// {description}");
            sb.AppendLine("        /// </summary>");
        }

        // Attributes
        var attrs = new List<string>();
        if (side == "server")
        {
            if (isRemote) attrs.Add("Remote");
            if (isPublic) attrs.Add("Public");
        }
        else if (side == "client")
        {
            attrs.Add($"LocalizeFunction(\"{functionName}FunctionName\", \"\")");
        }

        if (attrs.Count > 0)
            sb.AppendLine($"        [{string.Join(", ", attrs)}]");

        // Signature
        var csReturnType = MapToCsType(returnType);
        var csParams = string.Join(", ", parameters.Select(p => $"{MapToCsType(p.Type)} {p.Name}"));
        var isStatic = side == "server" && string.IsNullOrWhiteSpace(body) ? "static " : "";
        var isVirtual = side == "client" ? "virtual " : "";

        sb.Append($"        public {isStatic}{isVirtual}{csReturnType} {functionName}({csParams})");

        // Body
        if (!string.IsNullOrWhiteSpace(body))
        {
            sb.AppendLine();
            sb.AppendLine("        {");
            foreach (var line in body.Split('\n'))
                sb.AppendLine($"            {line.TrimEnd()}");
            sb.AppendLine("        }");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("        {");
            if (csReturnType == "void")
            {
                sb.AppendLine("            // TODO: implement");
            }
            else
            {
                sb.AppendLine("            // TODO: implement");
                sb.AppendLine($"            throw new NotImplementedException();");
            }
            sb.AppendLine("        }");
        }

        return sb.ToString();
    }

    private static string InsertFunctionIntoClass(string existingCode, string className, string functionCode)
    {
        // Find the last closing brace of the class (second-to-last '}')
        var lastClassBrace = existingCode.LastIndexOf('}');
        if (lastClassBrace < 0) return existingCode;

        // Find the class closing brace (one before namespace closing)
        var classClose = existingCode.LastIndexOf('}', lastClassBrace - 1);
        if (classClose < 0) classClose = lastClassBrace;

        return existingCode[..classClose] + "\n" + functionCode + "\n" + existingCode[classClose..];
    }

    private static string GenerateNewFunctionFile(string moduleName, string nsLayer, string className, string functionCode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.{nsLayer}");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {className}");
        sb.AppendLine("    {");
        sb.Append(functionCode);
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    #endregion

    #region Module.mtd Update

    private static string? FindModuleMtd(string modulePath, string moduleName)
    {
        var sharedPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (File.Exists(sharedPath)) return sharedPath;

        // Try searching recursively
        var candidates = Directory.GetFiles(modulePath, "Module.mtd", SearchOption.AllDirectories);
        return candidates.FirstOrDefault();
    }

    private static async Task<bool> UpdateModuleMtdPublicFunctions(
        string mtdPath, string functionName, string returnType,
        List<ParamDef> parameters, bool isRemote, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(mtdPath, ct);
            var node = JsonNode.Parse(json);
            if (node is not JsonObject root) return false;

            var publicFunctions = root["PublicFunctions"]?.AsArray();
            if (publicFunctions == null)
            {
                publicFunctions = new JsonArray();
                root["PublicFunctions"] = publicFunctions;
            }

            // Check if function already exists
            foreach (var fn in publicFunctions)
            {
                if (fn?["Name"]?.GetValue<string>() == functionName)
                    return false; // already exists
            }

            var funcObj = new JsonObject
            {
                ["Name"] = functionName,
                ["Placement"] = "Server",
                ["IsRemote"] = isRemote,
            };

            var resolvedReturn = ResolveGlobalType(returnType);
            if (!string.IsNullOrEmpty(resolvedReturn))
            {
                funcObj["ReturnType"] = resolvedReturn;
                funcObj["ReturnTypeFullName"] = ResolveTypeFullName(returnType);
            }

            if (parameters.Count > 0)
            {
                var paramsArray = new JsonArray();
                foreach (var p in parameters)
                {
                    paramsArray.Add(new JsonObject
                    {
                        ["Name"] = p.Name,
                        ["ParameterType"] = ResolveGlobalType(p.Type),
                        ["ParameterTypeFullName"] = ResolveTypeFullName(p.Type)
                    });
                }
                funcObj["Parameters"] = paramsArray;
            }

            publicFunctions.Add(funcObj);

            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(mtdPath, node.ToJsonString(options), ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Type Resolution

    private static string MapToCsType(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Equals("void", StringComparison.OrdinalIgnoreCase))
            return "void";

        return type switch
        {
            "string" => "string",
            "int" => "int",
            "long" => "long",
            "bool" => "bool",
            "double" => "double",
            "decimal" => "decimal",
            "DateTime" => "DateTime",
            "Guid" => "Guid",
            _ => type // IQueryable<IDeal>, List<string>, etc. — pass through
        };
    }

    private static string ResolveGlobalType(string type)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Equals("void", StringComparison.OrdinalIgnoreCase))
            return "";

        if (TypeMap.TryGetValue(type, out var mapped))
            return mapped;

        // If already has global:: prefix
        if (type.StartsWith("global::"))
            return type;

        return $"global::{type}";
    }

    private static string ResolveTypeFullName(string type)
    {
        if (TypeFullNameMap.TryGetValue(type, out var mapped))
            return mapped;
        return type;
    }

    #endregion

    #region Parameter Parsing

    private static List<ParamDef> ParseParameters(string parameters)
    {
        var result = new List<ParamDef>();
        if (string.IsNullOrWhiteSpace(parameters)) return result;

        foreach (var part in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0)
            {
                result.Add(new ParamDef(
                    part[..colonIdx].Trim(),
                    part[(colonIdx + 1)..].Trim()));
            }
            else
            {
                // Try "Type Name" format (e.g., "long entityId")
                var spaceIdx = part.LastIndexOf(' ');
                if (spaceIdx > 0)
                {
                    result.Add(new ParamDef(
                        part[(spaceIdx + 1)..].Trim(),
                        part[..spaceIdx].Trim()));
                }
            }
        }
        return result;
    }

    private record ParamDef(string Name, string Type);

    #endregion

    #region Helpers

    private static ScaffoldFunctionResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetStr(Dictionary<string, JsonElement> p, string key, string def = "") =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : def;

    private static string? GetStrOrNull(Dictionary<string, JsonElement> p, string key) =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;

    private static bool GetBool(Dictionary<string, JsonElement> p, string key, bool def = false) =>
        p.TryGetValue(key, out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? el.GetBoolean() : def;

    #endregion
}
