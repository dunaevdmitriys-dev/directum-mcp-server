using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class RelationshipTools
{
    [McpServerTool(Name = "analyze_relationship_graph")]
    [Description("Анализ графа связей между сущностями: NavigationProperty, collections, cross-module зависимости. Визуализация в текстовом формате.")]
    public async Task<string> AnalyzeRelationshipGraph(
        [Description("Путь к модулю или решению")] string path,
        [Description("Глубина анализа: 1 (только прямые), 2 (с зависимостями)")] int depth = 1)
    {
        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Граф связей сущностей");
        sb.AppendLine();

        var entities = new Dictionary<string, EntityInfo>();
        var relations = new List<(string From, string To, string Type, string PropertyName)>();

        // Find all .mtd files
        var mtdFiles = Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var entityGuid = root.TryGetProperty("NameGuid", out var g) ? g.GetString() ?? "" : "";
                var baseGuid = root.TryGetProperty("BaseGuid", out var bg) ? bg.GetString() ?? "" : "";
                var baseType = DirectumConstants.ResolveBaseType(baseGuid);

                if (string.IsNullOrEmpty(entityName)) continue;
                entities[entityGuid] = new EntityInfo(entityName, baseType, entityGuid);

                // Extract NavigationProperty relations
                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in props.EnumerateArray())
                    {
                        var propType = prop.TryGetProperty("$type", out var pt) ? pt.GetString() ?? "" : "";
                        var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";

                        if (propType.Contains("NavigationPropertyMetadata"))
                        {
                            var targetGuid = prop.TryGetProperty("EntityGuid", out var eg) ? eg.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(targetGuid))
                                relations.Add((entityGuid, targetGuid, "navigation", propName));
                        }
                        else if (propType.Contains("CollectionPropertyMetadata"))
                        {
                            var targetGuid = prop.TryGetProperty("EntityGuid", out var eg) ? eg.GetString() ?? "" : "";
                            if (!string.IsNullOrEmpty(targetGuid))
                                relations.Add((entityGuid, targetGuid, "collection", propName));
                        }
                    }
                }
            }
            catch { }
        }

        // Build graph
        sb.AppendLine($"**Сущностей:** {entities.Count}");
        sb.AppendLine($"**Связей:** {relations.Count}");
        sb.AppendLine();

        sb.AppendLine("## Сущности");
        sb.AppendLine("| Сущность | Тип | GUID |");
        sb.AppendLine("|----------|-----|------|");
        foreach (var (guid, info) in entities.OrderBy(x => x.Value.Name))
            sb.AppendLine($"| {info.Name} | {info.BaseType} | {guid[..8]}... |");
        sb.AppendLine();

        sb.AppendLine("## Связи");
        sb.AppendLine("| От | → | К | Тип | Свойство |");
        sb.AppendLine("|----|---|---|-----|----------|");
        foreach (var (from, to, type, propName) in relations)
        {
            var fromName = entities.TryGetValue(from, out var fi) ? fi.Name : from[..8] + "...";
            var toName = entities.TryGetValue(to, out var ti) ? ti.Name : DirectumConstants.ResolveBaseType(to) != "Unknown" ? DirectumConstants.ResolveBaseType(to) : to[..8] + "...(внешняя)";
            var arrow = type == "collection" ? "◇→" : "→";
            sb.AppendLine($"| {fromName} | {arrow} | {toName} | {type} | {propName} |");
        }
        sb.AppendLine();

        // Cross-module references
        var externalRefs = relations.Where(r => !entities.ContainsKey(r.To)).ToList();
        if (externalRefs.Count > 0)
        {
            sb.AppendLine($"## Внешние зависимости ({externalRefs.Count})");
            sb.AppendLine("Ссылки на сущности из других модулей:");
            foreach (var (from, to, type, propName) in externalRefs)
            {
                var fromName = entities.TryGetValue(from, out var fi) ? fi.Name : "?";
                sb.AppendLine($"- {fromName}.{propName} → {to[..13]}...");
            }
        }

        // Orphan detection
        var referencedGuids = relations.Select(r => r.To).ToHashSet();
        var referencingGuids = relations.Select(r => r.From).ToHashSet();
        var orphans = entities.Keys.Where(g => !referencedGuids.Contains(g) && !referencingGuids.Contains(g)).ToList();
        if (orphans.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"## Изолированные сущности ({orphans.Count})");
            sb.AppendLine("Не связаны ни с кем:");
            foreach (var g in orphans)
                sb.AppendLine($"- {entities[g].Name}");
        }

        return sb.ToString();
    }

    private record EntityInfo(string Name, string BaseType, string Guid);
}
