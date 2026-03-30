using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Cache;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class MetadataTools
{
    [McpServerTool(Name = "search_metadata")]
    [Description(
        "Поиск по MTD-файлам: сущности по имени, GUID, свойству. " +
        "Результаты кэшируются — повторные запросы мгновенные. " +
        "Для поиска в C# коде используй grep. Макс 50 результатов. " +
        "Для извлечения полной схемы найденной сущности — extract_entity_schema.")]
    public async Task<string> SearchMetadata(
        IMetadataCache cache,
        SolutionPathConfig config,
        [Description("Строка поиска: имя, GUID, свойство")] string query,
        [Description("Макс результатов (1-50)")] int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "**ОШИБКА**: query не может быть пустым.";

        maxResults = Math.Clamp(maxResults, 1, 50);

        var results = await cache.SearchAsync(query, maxResults);

        if (results.Count == 0)
            return $"По запросу **\"{query}\"** ничего не найдено. Кэш содержит {cache.CachedEntityCount} сущностей, {cache.CachedModuleCount} модулей.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Результаты: \"{query}\"");
        sb.AppendLine();
        sb.AppendLine($"Найдено: **{results.Count}** (кэш: {cache.CachedEntityCount} сущностей, {cache.CachedModuleCount} модулей)");
        sb.AppendLine();
        sb.AppendLine("| Имя | Тип | NameGuid | Свойств | Путь |");
        sb.AppendLine("|-----|-----|----------|---------|------|");

        foreach (var r in results)
        {
            var guidShort = r.NameGuid?.Length > 8 ? r.NameGuid[..8] + "..." : r.NameGuid ?? "";
            var relPath = Path.GetRelativePath(config.Path, r.FilePath);
            sb.AppendLine($"| {r.Name} | {r.Type} | `{guidShort}` | {r.PropertyCount} | `{relPath}` |");
        }

        return sb.ToString();
    }

    [McpServerTool(Name = "extract_entity_schema")]
    [Description(
        "Извлечь полную схему сущности: NameGuid, BaseGuid, Properties (с типами и GUID), Actions, Forms. " +
        "Используй после search_metadata для получения деталей. " +
        "Результат содержит реальные GUID — используй их в scaffold_entity(mode=override).")]
    public async Task<string> ExtractEntitySchema(
        IMetadataCache cache,
        [Description("Имя сущности или NameGuid")] string entityNameOrGuid)
    {
        var entity = await cache.FindEntityAsync(entityNameOrGuid)
            ?? await cache.FindEntityByGuidAsync(entityNameOrGuid);

        if (entity == null)
            return $"**ОШИБКА**: Сущность `{entityNameOrGuid}` не найдена. Используй search_metadata для поиска.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Схема: {entity.Name}");
        sb.AppendLine();
        sb.AppendLine($"- **NameGuid:** `{entity.NameGuid}`");
        sb.AppendLine($"- **BaseGuid:** `{entity.BaseGuid}`");
        sb.AppendLine($"- **IsAbstract:** {entity.IsAbstract}");
        sb.AppendLine($"- **EntityGroup:** {entity.EntityGroup ?? "—"}");
        sb.AppendLine();

        if (entity.Properties.Count > 0)
        {
            sb.AppendLine($"### Properties ({entity.Properties.Count})");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Code | NameGuid | Required |");
            sb.AppendLine("|------|------|------|----------|----------|");
            foreach (var p in entity.Properties)
            {
                var shortType = p.PropertyType?.Split('.').LastOrDefault()?.Replace("Metadata", "") ?? "?";
                sb.AppendLine($"| {p.Name} | {shortType} | {p.Code} | `{p.NameGuid[..8]}...` | {p.IsRequired} |");
            }
            sb.AppendLine();
        }

        if (entity.Actions.Count > 0)
        {
            sb.AppendLine($"### Actions ({entity.Actions.Count})");
            sb.AppendLine();
            foreach (var a in entity.Actions)
                sb.AppendLine($"- {a.Name} (`{a.NameGuid[..8]}...`){(a.IsAncestorMetadata ? " [inherited]" : "")}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
