using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class GenerateStructuresTool
{
    // Value types that use .GetHashCode() directly (not null-checked)
    private static readonly HashSet<string> ValueTypes = new(StringComparer.Ordinal)
    {
        "global::System.Int32",
        "global::System.Int64",
        "global::System.Double",
        "global::System.Boolean",
        "global::System.Byte",
        "global::System.Char",
        "global::System.Decimal",
        "global::System.Single",
        "global::System.Int16",
        "global::System.UInt16",
        "global::System.UInt32",
        "global::System.UInt64",
        "global::System.Guid",
    };

    [McpServerTool(Name = "generate_structures_cs")]
    [Description("Генерация ModuleStructures.g.cs из PublicStructures в Module.mtd. Восстановление после обнуления DDS.")]
    public async Task<string> Execute(
        [Description("Путь к Module.mtd")] string moduleMtdPath,
        [Description("Сохранить .g.cs файл рядом с Module.mtd (по умолчанию false — только вывод)")] bool save = false)
    {
        if (!PathGuard.IsAllowed(moduleMtdPath))
            return PathGuard.DenyMessage(moduleMtdPath);

        if (!File.Exists(moduleMtdPath))
            return $"**ОШИБКА**: Файл не найден: `{moduleMtdPath}`";

        JsonDocument doc;
        try
        {
            doc = await MtdParser.ParseRawAsync(moduleMtdPath);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось прочитать MTD: {ex.Message}";
        }

        using (doc)
        {
            var root = doc.RootElement;
            var moduleName = root.GetStringProp("Name");

            if (!root.TryGetProperty("PublicStructures", out var structures) ||
                structures.ValueKind != JsonValueKind.Array)
                return $"**ОШИБКА**: `PublicStructures` не найден или пуст в `{moduleMtdPath}`";

            var structList = new List<StructInfo>();
            int parseErrors = 0;

            foreach (var structure in structures.EnumerateArray())
            {
                try
                {
                    var info = ParseStructure(structure, moduleName);
                    if (info != null)
                        structList.Add(info);
                }
                catch
                {
                    parseErrors++;
                }
            }

            if (structList.Count == 0)
                return $"**ОШИБКА**: Нет валидных структур в PublicStructures. Ошибок парсинга: {parseErrors}";

            var generatedCode = GenerateCode(structList);

            var sb = new StringBuilder();
            sb.AppendLine("# Генерация ModuleStructures.g.cs");
            sb.AppendLine();
            sb.AppendLine($"**Модуль**: `{moduleName}`");
            sb.AppendLine($"**Структур**: {structList.Count}");
            if (parseErrors > 0)
                sb.AppendLine($"**Ошибок парсинга**: {parseErrors}");
            sb.AppendLine();

            foreach (var s in structList)
            {
                sb.AppendLine($"- `{s.Name}`: {s.Properties.Count} свойств, интерфейс `{s.InterfaceType}`");
            }
            sb.AppendLine();

            if (save)
            {
                var outputPath = Path.Combine(Path.GetDirectoryName(moduleMtdPath)!, "ModuleStructures.g.cs");
                await File.WriteAllTextAsync(outputPath, generatedCode);
                sb.AppendLine($"**Сохранено**: `{outputPath}`");
                sb.AppendLine();
                sb.AppendLine("> Рекомендуется: `attrib +R ModuleStructures.g.cs` для защиты от перезаписи DDS");
            }

            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine(generatedCode);
            sb.AppendLine("```");

            return sb.ToString();
        }
    }

    private static StructInfo? ParseStructure(JsonElement structure, string moduleName)
    {
        var name = structure.GetStringProp("Name");
        if (string.IsNullOrEmpty(name))
            return null;

        var ns = structure.GetStringProp("StructureNamespace");
        if (string.IsNullOrEmpty(ns))
            ns = $"{moduleName}.Structures.Module";

        var properties = new List<PropInfo>();
        var hasEntityRef = false;

        if (structure.TryGetProperty("Properties", out var propsEl) &&
            propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsEl.EnumerateArray())
            {
                var propName = prop.GetStringProp("Name");
                var typeFullName = prop.GetStringProp("TypeFullName");
                if (string.IsNullOrEmpty(propName) || string.IsNullOrEmpty(typeFullName))
                    continue;

                var isNullable = prop.TryGetProperty("IsNullable", out var nullEl) &&
                                 nullEl.ValueKind == JsonValueKind.True;
                var isList = prop.TryGetProperty("IsList", out var listEl) &&
                             listEl.ValueKind == JsonValueKind.True;
                var isEntity = prop.TryGetProperty("IsEntity", out var entEl) &&
                               entEl.ValueKind == JsonValueKind.True;

                if (isEntity)
                    hasEntityRef = true;

                properties.Add(new PropInfo(propName, typeFullName, isNullable, isList, isEntity));
            }
        }

        // Determine interface: IEntityAppliedStructure if has entity refs, else ISimpleAppliedStructure
        var interfaceType = hasEntityRef
            ? "global::Sungero.Domain.Shared.IEntityAppliedStructure"
            : "global::Sungero.Domain.Shared.ISimpleAppliedStructure";

        return new StructInfo(name, ns, properties, interfaceType, !hasEntityRef);
    }

    private static string GenerateCode(List<StructInfo> structures)
    {
        var sb = new StringBuilder();

        // Group by namespace
        var byNamespace = structures.GroupBy(s => s.Namespace);

        foreach (var nsGroup in byNamespace)
        {
            sb.AppendLine($"namespace {nsGroup.Key}");
            sb.AppendLine("{");

            foreach (var (idx, structure) in nsGroup.Select((s, i) => (i, s)))
            {
                if (idx > 0)
                    sb.AppendLine();

                GenerateStructure(sb, structure);
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void GenerateStructure(StringBuilder sb, StructInfo info)
    {
        var indent = "  ";
        var name = info.Name;

        // Serializable attribute for simple structures
        if (info.IsSerializable)
            sb.AppendLine($"{indent}[global::System.Serializable]");

        sb.AppendLine($"{indent}public partial class {name} : {info.InterfaceType}");
        sb.AppendLine($"{indent}{{");

        // Empty Create()
        sb.AppendLine($"{indent}  public static {name} Create()");
        sb.AppendLine($"{indent}  {{");
        sb.AppendLine($"{indent}    return new {name}();");
        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();

        // Create() with all params
        if (info.Properties.Count > 0)
        {
            var paramList = string.Join(", ",
                info.Properties.Select(p => $"{p.TypeFullName} {ToCamelCase(p.Name)}"));

            sb.AppendLine($"{indent}  public static {name} Create({paramList})");
            sb.AppendLine($"{indent}  {{");
            sb.AppendLine($"{indent}    return new {name}");
            sb.AppendLine($"{indent}    {{");

            for (int i = 0; i < info.Properties.Count; i++)
            {
                var p = info.Properties[i];
                var comma = i < info.Properties.Count - 1 ? "," : "";
                sb.AppendLine($"{indent}      {p.Name} = {ToCamelCase(p.Name)}{comma}");
            }

            sb.AppendLine($"{indent}    }};");
            sb.AppendLine($"{indent}  }}");
            sb.AppendLine();
        }

        // GetHashCode
        sb.AppendLine($"{indent}  public override int GetHashCode()");
        sb.AppendLine($"{indent}  {{");
        sb.AppendLine($"{indent}    unchecked");
        sb.AppendLine($"{indent}    {{");

        if (info.Properties.Count == 0)
        {
            sb.AppendLine($"{indent}      return 0;");
        }
        else
        {
            var hashParts = info.Properties.Select(p => GetHashCodeExpression(p));
            sb.AppendLine($"{indent}      return {string.Join(" ^ ", hashParts)};");
        }

        sb.AppendLine($"{indent}    }}");
        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();

        // Equals(object)
        sb.AppendLine($"{indent}  public override bool Equals(object obj)");
        sb.AppendLine($"{indent}  {{");
        sb.AppendLine($"{indent}    if (ReferenceEquals(null, obj)) return false;");
        sb.AppendLine($"{indent}    if (ReferenceEquals(this, obj)) return true;");
        sb.AppendLine($"{indent}    if (obj.GetType() != this.GetType()) return false;");
        sb.AppendLine($"{indent}    return Equals(({name})obj);");
        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();

        // operator ==
        sb.AppendLine($"{indent}  public static bool operator ==({name} left, {name} right)");
        sb.AppendLine($"{indent}  {{");
        sb.AppendLine($"{indent}    if (ReferenceEquals(left, right))");
        sb.AppendLine($"{indent}      return true;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    if (((object)left) == null || ((object)right) == null)");
        sb.AppendLine($"{indent}      return false;");
        sb.AppendLine();
        sb.AppendLine($"{indent}    return left.Equals(right);");
        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();

        // operator !=
        sb.AppendLine($"{indent}  public static bool operator !=({name} left, {name} right)");
        sb.AppendLine($"{indent}  {{");
        sb.AppendLine($"{indent}    return !(left == right);");
        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();

        // Equals(T)
        sb.AppendLine($"{indent}  protected bool Equals({name} other)");
        sb.AppendLine($"{indent}  {{");

        if (info.Properties.Count == 0)
        {
            sb.AppendLine($"{indent}    return true;");
        }
        else
        {
            var parts = new List<string>();
            foreach (var p in info.Properties)
            {
                parts.Add(GetEqualsExpression(p));
            }

            sb.Append($"{indent}    return {parts[0]}");
            for (int i = 1; i < parts.Count; i++)
            {
                sb.AppendLine();
                sb.Append($"{indent}           && {parts[i]}");
            }
            sb.AppendLine(" ;");
        }

        sb.AppendLine($"{indent}  }}");
        sb.AppendLine();
        sb.AppendLine($"{indent}}}");
    }

    private static string GetHashCodeExpression(PropInfo prop)
    {
        if (IsValueType(prop.TypeFullName) && !prop.IsNullable)
            return $"(this.{prop.Name}.GetHashCode() * 199)";

        return $"((this.{prop.Name} != null ? this.{prop.Name}.GetHashCode() : 0) * 199)";
    }

    private static string GetEqualsExpression(PropInfo prop)
    {
        if (prop.IsList)
            return $"global::System.Linq.Enumerable.SequenceEqual(this.{prop.Name}, other.{prop.Name})";

        if (IsValueType(prop.TypeFullName) && !prop.IsNullable)
            return $"this.{prop.Name} == other.{prop.Name}";

        return $"object.Equals(this.{prop.Name}, other.{prop.Name})";
    }

    private static bool IsValueType(string typeFullName)
    {
        return ValueTypes.Contains(typeFullName);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private record StructInfo(
        string Name,
        string Namespace,
        List<PropInfo> Properties,
        string InterfaceType,
        bool IsSerializable);

    private record PropInfo(
        string Name,
        string TypeFullName,
        bool IsNullable,
        bool IsList,
        bool IsEntity);
}
