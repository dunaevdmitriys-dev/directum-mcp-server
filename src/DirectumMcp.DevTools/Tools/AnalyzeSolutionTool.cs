using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class AnalyzeSolutionTool
{
    [McpServerTool(Name = "analyze_solution")]
    [Description("Аудит решения Directum RX: health, конфликты, сироты, дубликаты GUID, версии, WebAPI карта, обложки.")]
    public async Task<string> AnalyzeSolution(
        [Description("Путь к корню решения")] string? solutionPath = null,
        [Description("Действие: health | conflicts | orphans | duplicates | versions | api | cover | rc | trace")] string action = "health",
        [Description("Имя сущности (для action=trace)")] string? entity = null)
    {
        var resolvedPath = solutionPath ?? Environment.GetEnvironmentVariable("SOLUTION_PATH");

        if (string.IsNullOrEmpty(resolvedPath))
            return "**ОШИБКА**: Путь к решению не указан и переменная окружения SOLUTION_PATH не задана.";

        if (!PathGuard.IsAllowed(resolvedPath))
            return PathGuard.DenyMessage(resolvedPath);

        if (!Directory.Exists(resolvedPath))
            return $"**ОШИБКА**: Директория не найдена: `{resolvedPath}`";

        var (modules, entities, parseErrors) = await BuildSolutionModel(resolvedPath);

        var result = action.ToLowerInvariant() switch
        {
            "conflicts" => RenderConflicts(modules, entities),
            "orphans" => RenderOrphans(modules, entities),
            "duplicates" => RenderDuplicates(modules, entities),
            "versions" => await RenderVersions(modules, resolvedPath),
            "api" => await RenderApi(modules, resolvedPath),
            "cover" => await RenderCover(modules, resolvedPath),
            "rc" => await RenderRemoteComponents(modules, resolvedPath),
            "trace" => await RenderTrace(modules, entities, resolvedPath, entity),
            _ => RenderHealth(resolvedPath, modules, entities)
        };

        if (parseErrors > 0)
            result += $"\n\n> **Ошибок парсинга**: {parseErrors} файлов пропущено\n";

        return result;
    }

    internal record ModuleInfo(string Name, string Guid, string Path, List<string> DependencyGuids, List<string> EntityGuids, bool IsPlatform);
    internal record EntityInfo(string Name, string Guid, string BaseGuid, string? AncestorGuid, string ModuleGuid, string FilePath, List<PropertyInfo> Properties);
    internal record PropertyInfo(string Name, string Code, string Type);

    internal static async Task<(List<ModuleInfo> Modules, List<EntityInfo> Entities, int ParseErrors)> BuildSolutionModel(string solutionPath)
    {
        var modules = new List<ModuleInfo>();
        var entities = new List<EntityInfo>();
        int parseErrors = 0;

        var basePath = Path.GetFullPath(Path.Combine(solutionPath, "base"));
        var workPath = Path.GetFullPath(Path.Combine(solutionPath, "work"));

        var moduleMtdFiles = Directory.GetFiles(solutionPath, "Module.mtd", SearchOption.AllDirectories);

        foreach (var moduleMtdFile in moduleMtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(moduleMtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var metaType = root.GetStringProp("$type");
                if (!metaType.Contains("ModuleMetadata"))
                    continue;

                var moduleName = root.GetStringProp("Name");
                var moduleGuid = root.GetStringProp("NameGuid");

                if (string.IsNullOrEmpty(moduleGuid))
                    continue;

                var deps = new List<string>();
                if (root.TryGetProperty("Dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dep in depsEl.EnumerateArray())
                    {
                        var id = dep.GetStringProp("Id");
                        if (!string.IsNullOrEmpty(id))
                            deps.Add(id.ToLowerInvariant());
                    }
                }

                var entityGuids = new List<string>();
                if (root.TryGetProperty("Entities", out var entitiesEl) && entitiesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ent in entitiesEl.EnumerateArray())
                    {
                        var id = ent.GetStringProp("Id");
                        if (!string.IsNullOrEmpty(id))
                            entityGuids.Add(id.ToLowerInvariant());
                    }
                }

                var fullModulePath = Path.GetFullPath(moduleMtdFile);
                var moduleDir = Path.GetDirectoryName(fullModulePath)!;
                var isPlatform = fullModulePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) ||
                                 !fullModulePath.StartsWith(workPath, StringComparison.OrdinalIgnoreCase);

                var moduleInfo = new ModuleInfo(moduleName, moduleGuid.ToLowerInvariant(), moduleDir, deps, entityGuids, isPlatform);
                modules.Add(moduleInfo);

                // Load all entity .mtd files in this module's directory tree
                var allMtdInModule = Directory.GetFiles(moduleDir, "*.mtd", SearchOption.AllDirectories);
                foreach (var entityMtdFile in allMtdInModule)
                {
                    if (Path.GetFileName(entityMtdFile).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        var entityJson = await File.ReadAllTextAsync(entityMtdFile);
                        using var entityDoc = JsonDocument.Parse(entityJson);
                        var entityRoot = entityDoc.RootElement;

                        var entityType = entityRoot.GetStringProp("$type");
                        if (!entityType.Contains("EntityMetadata") && !entityType.Contains("DocumentMetadata") &&
                            !entityType.Contains("TaskMetadata") && !entityType.Contains("AssignmentMetadata"))
                            continue;

                        var entityName = entityRoot.GetStringProp("Name");
                        var entityGuid = entityRoot.GetStringProp("NameGuid");
                        var baseGuid = entityRoot.GetStringProp("BaseGuid");
                        var ancestorGuid = entityRoot.GetStringProp("AncestorGuid");

                        if (string.IsNullOrEmpty(entityGuid))
                            continue;

                        var props = new List<PropertyInfo>();
                        if (entityRoot.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var prop in propsEl.EnumerateArray())
                            {
                                var propName = prop.GetStringProp("Name");
                                var propCode = prop.GetStringProp("Code");
                                var propType = prop.GetStringProp("$type");
                                if (!string.IsNullOrEmpty(propName))
                                    props.Add(new PropertyInfo(propName, string.IsNullOrEmpty(propCode) ? propName : propCode, propType));
                            }
                        }

                        entities.Add(new EntityInfo(
                            entityName,
                            entityGuid.ToLowerInvariant(),
                            baseGuid.ToLowerInvariant(),
                            string.IsNullOrEmpty(ancestorGuid) ? null : ancestorGuid.ToLowerInvariant(),
                            moduleGuid.ToLowerInvariant(),
                            entityMtdFile,
                            props));
                    }
                    catch
                    {
                        parseErrors++;
                    }
                }
            }
            catch
            {
                parseErrors++;
            }
        }

        return (modules, entities, parseErrors);
    }

    internal static string RenderHealth(string solutionPath, List<ModuleInfo> modules, List<EntityInfo> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Отчёт о состоянии решения Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Решение:** `{solutionPath}`");
        sb.AppendLine();

        var platformModules = modules.Where(m => m.IsPlatform).ToList();
        var customModules = modules.Where(m => !m.IsPlatform).ToList();

        sb.AppendLine("## Общая статистика");
        sb.AppendLine();
        sb.AppendLine($"| Показатель | Значение |");
        sb.AppendLine($"|------------|----------|");
        sb.AppendLine($"| Всего модулей | **{modules.Count}** |");
        sb.AppendLine($"| Платформенных модулей | {platformModules.Count} |");
        sb.AppendLine($"| Кастомных модулей | {customModules.Count} |");
        sb.AppendLine($"| Всего сущностей | **{entities.Count}** |");
        sb.AppendLine();

        // Duplicate GUID detection
        var guidGroups = entities
            .GroupBy(e => e.Guid)
            .Where(g => g.Count() > 1)
            .ToList();

        sb.AppendLine("## Дубликаты GUID");
        sb.AppendLine();
        if (guidGroups.Count == 0)
        {
            sb.AppendLine("Дубликатов GUID не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено дубликатов: {guidGroups.Count}**");
            sb.AppendLine();
            sb.AppendLine("| GUID | Сущности |");
            sb.AppendLine("|------|----------|");
            foreach (var grp in guidGroups)
                sb.AppendLine($"| `{grp.Key}` | {string.Join(", ", grp.Select(e => e.Name))} |");
        }
        sb.AppendLine();

        // Override conflicts
        var conflicts = FindOverrideConflicts(modules, entities);
        sb.AppendLine("## Конфликты перекрытий");
        sb.AppendLine();
        if (conflicts.Count == 0)
        {
            sb.AppendLine("Конфликтов перекрытий не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено конфликтов: {conflicts.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Предок | Модуль A | Сущность A | Модуль B | Сущность B |");
            sb.AppendLine("|--------|----------|-----------|----------|-----------|");
            foreach (var (ancestorGuid, e1, e2, mod1, mod2) in conflicts)
                sb.AppendLine($"| `{ancestorGuid}` | {mod1} | {e1} | {mod2} | {e2} |");
        }
        sb.AppendLine();

        // Orphan custom modules
        var orphans = FindOrphanCustomModules(modules);
        sb.AppendLine("## Сиротские кастомные модули");
        sb.AppendLine();
        if (orphans.Count == 0)
        {
            sb.AppendLine("Сиротских модулей не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено сиротских модулей: {orphans.Count}**");
            sb.AppendLine();
            foreach (var m in orphans)
                sb.AppendLine($"- **{m.Name}** (`{m.Guid}`)");
        }
        sb.AppendLine();

        // Missing dependencies
        var allGuids = new HashSet<string>(modules.Select(m => m.Guid), StringComparer.OrdinalIgnoreCase);
        var missing = new List<(string ModuleName, string MissingGuid)>();
        foreach (var module in modules)
        {
            foreach (var dep in module.DependencyGuids)
            {
                if (!allGuids.Contains(dep))
                    missing.Add((module.Name, dep));
            }
        }

        sb.AppendLine("## Отсутствующие зависимости");
        sb.AppendLine();
        if (missing.Count == 0)
        {
            sb.AppendLine("Отсутствующих зависимостей не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено отсутствующих зависимостей: {missing.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Модуль | Отсутствующий GUID |");
            sb.AppendLine("|--------|--------------------|");
            foreach (var (modName, guid) in missing)
                sb.AppendLine($"| {modName} | `{guid}` |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    internal static string RenderConflicts(List<ModuleInfo> modules, List<EntityInfo> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ конфликтов перекрытий");
        sb.AppendLine();

        var conflicts = FindOverrideConflicts(modules, entities);

        if (conflicts.Count == 0)
        {
            sb.AppendLine("Конфликтов перекрытий не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено конфликтов: {conflicts.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Предок | Модуль A | Сущность A | Модуль B | Сущность B |");
            sb.AppendLine("|--------|----------|-----------|----------|-----------|");
            foreach (var (ancestorGuid, e1, e2, mod1, mod2) in conflicts)
                sb.AppendLine($"| `{ancestorGuid}` | {mod1} | {e1} | {mod2} | {e2} |");
        }
        sb.AppendLine();

        // Also check property Code collisions in inheritance hierarchies
        var codeCollisions = FindPropertyCodeCollisions(entities);

        sb.AppendLine("## Коллизии Code свойств в иерархиях наследования");
        sb.AppendLine();
        if (codeCollisions.Count == 0)
        {
            sb.AppendLine("Коллизий Code свойств не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено коллизий: {codeCollisions.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Сущность A | Сущность B | Code |");
            sb.AppendLine("|-----------|-----------|------|");
            foreach (var (e1, e2, code) in codeCollisions)
                sb.AppendLine($"| {e1} | {e2} | `{code}` |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    internal static string RenderOrphans(List<ModuleInfo> modules, List<EntityInfo> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ сиротских элементов");
        sb.AppendLine();

        var orphanModules = FindOrphanCustomModules(modules);

        sb.AppendLine("## Сиротские кастомные модули");
        sb.AppendLine();
        if (orphanModules.Count == 0)
        {
            sb.AppendLine("Сиротских модулей не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено: {orphanModules.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Модуль | GUID | Путь |");
            sb.AppendLine("|--------|------|------|");
            foreach (var m in orphanModules)
                sb.AppendLine($"| **{m.Name}** | `{m.Guid}` | `{m.Path}` |");
        }
        sb.AppendLine();

        // Entity .mtd files not referenced in any Module.mtd's Entities array
        var allReferencedEntityGuids = new HashSet<string>(
            modules.SelectMany(m => m.EntityGuids),
            StringComparer.OrdinalIgnoreCase);

        var orphanEntities = entities
            .Where(e => !allReferencedEntityGuids.Contains(e.Guid))
            .OrderBy(e => e.Name)
            .ToList();

        sb.AppendLine("## Сущности не упомянутые в Module.mtd");
        sb.AppendLine();
        if (orphanEntities.Count == 0)
        {
            sb.AppendLine("Все сущности упомянуты в Module.mtd.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено: {orphanEntities.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Сущность | GUID | Файл |");
            sb.AppendLine("|----------|------|------|");
            foreach (var e in orphanEntities)
                sb.AppendLine($"| **{e.Name}** | `{e.Guid}` | `{e.FilePath}` |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    internal static string RenderDuplicates(List<ModuleInfo> modules, List<EntityInfo> entities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ дубликатов");
        sb.AppendLine();

        // Duplicate NameGuid
        var guidGroups = entities
            .GroupBy(e => e.Guid)
            .Where(g => g.Count() > 1)
            .ToList();

        sb.AppendLine("## Дубликаты NameGuid сущностей");
        sb.AppendLine();
        if (guidGroups.Count == 0)
        {
            sb.AppendLine("Дубликатов GUID не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено: {guidGroups.Count}**");
            sb.AppendLine();
            sb.AppendLine("| GUID | Сущности |");
            sb.AppendLine("|------|----------|");
            foreach (var grp in guidGroups)
                sb.AppendLine($"| `{grp.Key}` | {string.Join(", ", grp.Select(e => e.Name))} |");
        }
        sb.AppendLine();

        // Property Code collisions
        var codeCollisions = FindPropertyCodeCollisions(entities);

        sb.AppendLine("## Коллизии Code свойств");
        sb.AppendLine();
        if (codeCollisions.Count == 0)
        {
            sb.AppendLine("Коллизий Code свойств не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено: {codeCollisions.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Сущность A | Сущность B | Code |");
            sb.AppendLine("|-----------|-----------|------|");
            foreach (var (e1, e2, code) in codeCollisions)
                sb.AppendLine($"| {e1} | {e2} | `{code}` |");
        }
        sb.AppendLine();

        // Duplicate enum value names
        var enumDuplicates = FindDuplicateEnumValues(entities);

        sb.AppendLine("## Дубликаты значений перечислений");
        sb.AppendLine();
        if (enumDuplicates.Count == 0)
        {
            sb.AppendLine("Дубликатов значений перечислений не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено: {enumDuplicates.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Сущность | Перечисление | Дублированные значения |");
            sb.AppendLine("|----------|-------------|------------------------|");
            foreach (var (entityName, enumName, values) in enumDuplicates)
                sb.AppendLine($"| {entityName} | {enumName} | {string.Join(", ", values)} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    internal static async Task<string> RenderVersions(List<ModuleInfo> modules, string solutionPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ версий модулей");
        sb.AppendLine();

        var versionInfos = new List<(string ModuleName, string ModulePath, string Version)>();

        foreach (var module in modules)
        {
            var packageInfoPath = Path.Combine(module.Path, "PackageInfo.xml");
            if (!File.Exists(packageInfoPath))
                continue;

            try
            {
                var xml = await File.ReadAllTextAsync(packageInfoPath);
                var xdoc = XDocument.Parse(xml);
                var version = xdoc.Root?.Element("Version")?.Value ?? "";
                if (!string.IsNullOrEmpty(version))
                    versionInfos.Add((module.Name, module.Path, version));
            }
            catch
            {
                // Skip unreadable XML
            }
        }

        if (versionInfos.Count == 0)
        {
            sb.AppendLine("PackageInfo.xml не найдены ни в одном модуле.");
            return sb.ToString();
        }

        sb.AppendLine("## Версии пакетов");
        sb.AppendLine();
        sb.AppendLine("| Модуль | Версия |");
        sb.AppendLine("|--------|--------|");
        foreach (var (name, _, version) in versionInfos.OrderBy(v => v.ModuleName))
            sb.AppendLine($"| {name} | `{version}` |");
        sb.AppendLine();

        // Find mismatches between interdependent modules
        var moduleByName = modules.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var versionByName = versionInfos.ToDictionary(v => v.ModuleName, v => v.Version, StringComparer.OrdinalIgnoreCase);
        var moduleByGuid = modules.ToDictionary(m => m.Guid, StringComparer.OrdinalIgnoreCase);

        var mismatches = new List<(string ModuleA, string VersionA, string ModuleB, string VersionB)>();
        var checked_ = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules)
        {
            if (!versionByName.TryGetValue(module.Name, out var versionA))
                continue;

            foreach (var depGuid in module.DependencyGuids)
            {
                if (!moduleByGuid.TryGetValue(depGuid, out var depModule))
                    continue;

                if (!versionByName.TryGetValue(depModule.Name, out var versionB))
                    continue;

                var key = string.Compare(module.Name, depModule.Name, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{module.Name}|{depModule.Name}"
                    : $"{depModule.Name}|{module.Name}";

                if (checked_.Contains(key))
                    continue;

                checked_.Add(key);

                if (!string.Equals(versionA, versionB, StringComparison.OrdinalIgnoreCase))
                    mismatches.Add((module.Name, versionA, depModule.Name, versionB));
            }
        }

        sb.AppendLine("## Несоответствия версий между зависимыми модулями");
        sb.AppendLine();
        if (mismatches.Count == 0)
        {
            sb.AppendLine("Несоответствий версий не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено несоответствий: {mismatches.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Модуль A | Версия A | Модуль B | Версия B |");
            sb.AppendLine("|----------|----------|----------|----------|");
            foreach (var (modA, verA, modB, verB) in mismatches)
                sb.AppendLine($"| {modA} | `{verA}` | {modB} | `{verB}` |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    // ========== api action ==========

    internal static async Task<string> RenderApi(List<ModuleInfo> modules, string solutionPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Карта WebAPI endpoints");
        sb.AppendLine();

        var webApiPattern = new Regex(
            @"\[Public\(WebApiRequestType\.(?<method>\w+)\)\]\s*(?:(?:\[.*?\]\s*)*)(?:public\s+)?(?:virtual\s+)?(?:static\s+)?(?<return>\S+)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)",
            RegexOptions.Singleline);

        var workPath = Path.GetFullPath(Path.Combine(solutionPath, "work"));
        var workModules = modules.Where(m => !m.IsPlatform && m.Path.StartsWith(workPath, StringComparison.OrdinalIgnoreCase)).ToList();

        var rows = new List<(string Module, string Function, string HttpMethod, string Parameters)>();

        foreach (var module in workModules)
        {
            var serverFunctionsFiles = Directory.GetFiles(module.Path, "ModuleServerFunctions.cs", SearchOption.AllDirectories);
            foreach (var file in serverFunctionsFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var matches = webApiPattern.Matches(content);
                    foreach (Match match in matches)
                    {
                        var httpMethod = match.Groups["method"].Value;
                        var funcName = match.Groups["name"].Value;
                        var rawParams = match.Groups["params"].Value.Trim();
                        var paramList = string.IsNullOrWhiteSpace(rawParams) ? "—" : rawParams.Replace("|", "\\|");
                        rows.Add((module.Name, funcName, httpMethod, paramList));
                    }
                }
                catch
                {
                    // Skip unreadable files
                }
            }
        }

        if (rows.Count == 0)
        {
            sb.AppendLine("WebAPI endpoints с атрибутом `[Public(WebApiRequestType.*)]` не найдены в work/ модулях.");
        }
        else
        {
            sb.AppendLine($"**Найдено endpoints: {rows.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Module | Function | HTTP Method | Parameters |");
            sb.AppendLine("|--------|----------|-------------|------------|");
            foreach (var (mod, func, method, pars) in rows)
                sb.AppendLine($"| {mod} | `{func}` | {method} | {pars} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    // ========== cover action ==========

    internal static async Task<string> RenderCover(List<ModuleInfo> modules, string solutionPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Карта обложек модулей");
        sb.AppendLine();

        var workPath = Path.GetFullPath(Path.Combine(solutionPath, "work"));
        var workModules = modules.Where(m => !m.IsPlatform && m.Path.StartsWith(workPath, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var module in workModules)
        {
            var moduleMtdPath = Path.Combine(module.Path, "Module.mtd");
            if (!File.Exists(moduleMtdPath))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(moduleMtdPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Cover", out var coverEl) || coverEl.ValueKind != JsonValueKind.Object)
                    continue;

                sb.AppendLine($"## {module.Name}");
                sb.AppendLine();

                // Collect client function names for validation
                var clientFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var clientFuncFiles = Directory.GetFiles(module.Path, "ModuleClientFunctions.cs", SearchOption.AllDirectories);
                foreach (var cf in clientFuncFiles)
                {
                    try
                    {
                        var cfContent = await File.ReadAllTextAsync(cf);
                        var methodPattern = new Regex(@"(?:public|private|internal|protected)?\s*(?:virtual\s+)?(?:static\s+)?(?:partial\s+)?(?:void|Task|string|bool|int|IQueryable\S*|\S+)\s+(\w+)\s*\(");
                        foreach (Match m in methodPattern.Matches(cfContent))
                            clientFunctions.Add(m.Groups[1].Value);
                    }
                    catch { }
                }

                // Parse Tabs
                if (coverEl.TryGetProperty("Tabs", out var tabsEl) && tabsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tab in tabsEl.EnumerateArray())
                    {
                        var tabName = tab.GetStringProp("Name");
                        sb.AppendLine($"### Tab: {(string.IsNullOrEmpty(tabName) ? "(без имени)" : tabName)}");
                        sb.AppendLine();

                        if (tab.TryGetProperty("Groups", out var groupsEl) && groupsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var group in groupsEl.EnumerateArray())
                            {
                                var groupName = group.GetStringProp("Name");
                                sb.AppendLine($"  **Group: {(string.IsNullOrEmpty(groupName) ? "(без имени)" : groupName)}**");
                                sb.AppendLine();

                                if (group.TryGetProperty("Actions", out var actionsEl) && actionsEl.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var action in actionsEl.EnumerateArray())
                                    {
                                        var actionType = action.GetStringProp("$type");
                                        var actionName = action.GetStringProp("Name");
                                        var shortType = actionType.Contains("CoverEntityListAction") ? "EntityList"
                                            : actionType.Contains("CoverFunctionAction") ? "Function"
                                            : actionType.Contains("CoverReportAction") ? "Report"
                                            : actionType.Split('.').LastOrDefault()?.Replace("Metadata", "") ?? actionType;

                                        var line = $"  - [{shortType}] **{actionName}**";

                                        if (actionType.Contains("CoverFunctionAction"))
                                        {
                                            var funcName = action.GetStringProp("FunctionName");
                                            if (!string.IsNullOrEmpty(funcName))
                                            {
                                                var exists = clientFunctions.Contains(funcName);
                                                line += $" → `{funcName}()` {(exists ? "OK" : "**NOT FOUND in ModuleClientFunctions.cs**")}";
                                            }
                                        }

                                        sb.AppendLine(line);
                                    }
                                }
                                sb.AppendLine();
                            }
                        }
                    }
                }

                // Remote controls on cover
                if (coverEl.TryGetProperty("RemoteControls", out var rcEl) && rcEl.ValueKind == JsonValueKind.Array)
                {
                    sb.AppendLine("**Remote Controls on Cover:**");
                    sb.AppendLine();
                    foreach (var rc in rcEl.EnumerateArray())
                    {
                        var rcName = rc.GetStringProp("Name");
                        var scope = rc.GetStringProp("Scope");
                        var moduleName = rc.GetStringProp("Module");
                        sb.AppendLine($"  - {rcName} (Scope: {scope}, Module: {moduleName})");
                    }
                    sb.AppendLine();
                }
            }
            catch
            {
                sb.AppendLine($"## {module.Name}");
                sb.AppendLine();
                sb.AppendLine("*Ошибка чтения Module.mtd*");
                sb.AppendLine();
            }
        }

        if (workModules.Count == 0)
            sb.AppendLine("Кастомных модулей в work/ не найдено.");

        return sb.ToString();
    }

    // ========== rc action ==========

    internal static async Task<string> RenderRemoteComponents(List<ModuleInfo> modules, string solutionPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Проверка Remote Components");
        sb.AppendLine();

        var workPath = Path.GetFullPath(Path.Combine(solutionPath, "work"));
        var workModules = modules.Where(m => !m.IsPlatform && m.Path.StartsWith(workPath, StringComparison.OrdinalIgnoreCase)).ToList();

        var rows = new List<(string Module, string Component, string Loader, string Scope, string WebpackExpose, string Status)>();

        foreach (var module in workModules)
        {
            var moduleMtdPath = Path.Combine(module.Path, "Module.mtd");
            if (!File.Exists(moduleMtdPath))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(moduleMtdPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract RemoteComponents → Controls and Loaders
                if (!root.TryGetProperty("RemoteComponents", out var rcRoot) || rcRoot.ValueKind != JsonValueKind.Object)
                    continue;

                var controls = new List<(string Name, string Loader, string Scope)>();

                if (rcRoot.TryGetProperty("Controls", out var controlsEl) && controlsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ctrl in controlsEl.EnumerateArray())
                    {
                        var name = ctrl.GetStringProp("Name");
                        var loader = ctrl.GetStringProp("Loader");
                        var scope = ctrl.GetStringProp("Scope");
                        if (!string.IsNullOrEmpty(name))
                            controls.Add((name, loader, scope));
                    }
                }

                if (rcRoot.TryGetProperty("Loaders", out var loadersEl) && loadersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ldr in loadersEl.EnumerateArray())
                    {
                        var name = ldr.GetStringProp("Name");
                        var loader = ldr.GetStringProp("Loader");
                        var scope = ldr.GetStringProp("Scope");
                        if (!string.IsNullOrEmpty(name))
                            controls.Add((name, loader, scope));
                    }
                }

                if (controls.Count == 0)
                    continue;

                // Find webpack.config.js exposes
                var webpackExposes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var webpackFiles = Directory.GetFiles(module.Path, "webpack.config.js", SearchOption.AllDirectories);
                foreach (var wpFile in webpackFiles)
                {
                    try
                    {
                        var wpContent = await File.ReadAllTextAsync(wpFile);
                        // Match patterns like './ComponentName': './src/...'  or  "./ComponentName": "./src/..."
                        var exposePattern = new Regex(@"['""]\.\/(?<key>[^'""]+)['""]\s*:\s*['""](?<value>[^'""]+)['""]");
                        foreach (Match m in exposePattern.Matches(wpContent))
                            webpackExposes.TryAdd(m.Groups["key"].Value, m.Groups["value"].Value);
                    }
                    catch { }
                }

                // Find deployed remoteEntry.js
                var remoteEntryFiles = Directory.GetFiles(module.Path, "remoteEntry.js", SearchOption.AllDirectories);
                var deployedExposes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var reFile in remoteEntryFiles)
                {
                    try
                    {
                        var reContent = await File.ReadAllTextAsync(reFile);
                        // In compiled remoteEntry.js, exposed keys appear in var patterns
                        var exposeKeyPattern = new Regex(@"['""]\.\/(?<key>[^'""]+)['""]");
                        foreach (Match m in exposeKeyPattern.Matches(reContent))
                            deployedExposes.Add(m.Groups["key"].Value);
                    }
                    catch { }
                }

                foreach (var (name, loader, scope) in controls)
                {
                    var wpExpose = webpackExposes.TryGetValue(name, out var val) ? val : "—";
                    var status = "?";
                    if (webpackExposes.ContainsKey(name))
                        status = deployedExposes.Contains(name) ? "OK" : "webpack OK, deploy NOT FOUND";
                    else
                        status = "NOT in webpack.config.js";

                    rows.Add((module.Name, name, loader, scope, wpExpose, status));
                }
            }
            catch { }
        }

        if (rows.Count == 0)
        {
            sb.AppendLine("Remote Components не найдены в work/ модулях.");
        }
        else
        {
            sb.AppendLine($"**Найдено компонентов: {rows.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Module | Component | Loader | Scope | webpack expose | Status |");
            sb.AppendLine("|--------|-----------|--------|-------|----------------|--------|");
            foreach (var (mod, comp, loader, scope, wpExpose, status) in rows)
                sb.AppendLine($"| {mod} | `{comp}` | {loader} | {scope} | {wpExpose} | {status} |");
        }
        sb.AppendLine();

        return sb.ToString();
    }

    // ========== trace action ==========

    private static readonly Dictionary<string, string> DbTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringPropertyMetadata"] = "citext",
        ["IntegerPropertyMetadata"] = "int8",
        ["DoublePropertyMetadata"] = "float8",
        ["BooleanPropertyMetadata"] = "bool",
        ["DateTimePropertyMetadata"] = "timestamp",
        ["NavigationPropertyMetadata"] = "int8 (FK)",
        ["EnumPropertyMetadata"] = "citext",
        ["TextPropertyMetadata"] = "citext",
    };

    internal static async Task<string> RenderTrace(List<ModuleInfo> modules, List<EntityInfo> entities, string solutionPath, string? entityName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Cross-layer trace");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(entityName))
        {
            sb.AppendLine("**ОШИБКА**: Укажите параметр `entity` с именем сущности (например: `entity=Deal`).");
            return sb.ToString();
        }

        var targetEntities = entities
            .Where(e => e.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetEntities.Count == 0)
        {
            sb.AppendLine($"Сущность `{entityName}` не найдена в решении.");
            return sb.ToString();
        }

        var moduleByGuid = modules.ToDictionary(m => m.Guid, StringComparer.OrdinalIgnoreCase);

        foreach (var entity in targetEntities)
        {
            var moduleName = moduleByGuid.TryGetValue(entity.ModuleGuid, out var mod) ? mod.Name : entity.ModuleGuid;
            var moduleDir = mod?.Path ?? "";

            sb.AppendLine($"## {entity.Name} (модуль: {moduleName})");
            sb.AppendLine();

            // MTD info
            sb.AppendLine("### MTD");
            sb.AppendLine();
            sb.AppendLine($"- **Name:** {entity.Name}");
            sb.AppendLine($"- **NameGuid:** `{entity.Guid}`");
            sb.AppendLine($"- **BaseGuid:** `{entity.BaseGuid}`");
            if (!string.IsNullOrEmpty(entity.AncestorGuid))
                sb.AppendLine($"- **AncestorGuid:** `{entity.AncestorGuid}`");

            // HandledEvents from MTD
            var handledEvents = new List<string>();
            try
            {
                var mtdJson = await File.ReadAllTextAsync(entity.FilePath);
                using var mtdDoc = JsonDocument.Parse(mtdJson);
                var mtdRoot = mtdDoc.RootElement;

                if (mtdRoot.TryGetProperty("HandledEvents", out var eventsEl) && eventsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ev in eventsEl.EnumerateArray())
                    {
                        var evName = ev.ValueKind == JsonValueKind.String ? ev.GetString() : ev.GetStringProp("Name");
                        if (!string.IsNullOrEmpty(evName))
                            handledEvents.Add(evName);
                    }
                }
            }
            catch { }

            if (handledEvents.Count > 0)
            {
                sb.AppendLine($"- **HandledEvents:** {string.Join(", ", handledEvents)}");
            }
            sb.AppendLine();

            // Properties → DB mapping
            sb.AppendLine("### Properties → DB Columns");
            sb.AppendLine();
            sb.AppendLine("| Property | Code | MTD Type | DB Column | DB Type |");
            sb.AppendLine("|----------|------|----------|-----------|---------|");

            // Derive table name: Sungero_<ModuleShortName>_<EntityName> (convention)
            var tablePrefix = moduleName.Contains('.') ? moduleName.Split('.').Last() : moduleName;

            foreach (var prop in entity.Properties)
            {
                var shortType = prop.Type.Split('.').LastOrDefault() ?? prop.Type;
                var dbColumn = prop.Code;
                var dbType = DbTypeMap.TryGetValue(shortType, out var dt) ? dt : "?";
                sb.AppendLine($"| {prop.Name} | {prop.Code} | {shortType} | `{dbColumn}` | {dbType} |");
            }
            sb.AppendLine();
            sb.AppendLine($"*Предполагаемая таблица:* `Sungero_{tablePrefix}_{entity.Name}`");
            sb.AppendLine();

            // Resx: DisplayName, property labels
            sb.AppendLine("### Resx Labels");
            sb.AppendLine();

            var entityDir = Path.GetDirectoryName(entity.FilePath)!;
            var resxFiles = Directory.Exists(entityDir)
                ? Directory.GetFiles(entityDir, "*System.ru.resx", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.GetFiles(entityDir, "*System.resx", SearchOption.TopDirectoryOnly))
                    .ToArray()
                : Array.Empty<string>();

            var resxLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var resxFile in resxFiles)
            {
                try
                {
                    var resxXml = await File.ReadAllTextAsync(resxFile);
                    var xdoc = XDocument.Parse(resxXml);
                    foreach (var dataEl in xdoc.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
                    {
                        var name = dataEl.Attribute("name")?.Value;
                        var value = dataEl.Element("value")?.Value;
                        if (!string.IsNullOrEmpty(name) && value != null)
                            resxLabels.TryAdd(name, value);
                    }
                }
                catch { }
            }

            if (resxLabels.Count > 0)
            {
                if (resxLabels.TryGetValue("DisplayName", out var displayName))
                    sb.AppendLine($"- **DisplayName:** {displayName}");

                sb.AppendLine();
                sb.AppendLine("| Property | Resx Key | Label |");
                sb.AppendLine("|----------|----------|-------|");
                foreach (var prop in entity.Properties)
                {
                    var key = $"Property_{prop.Name}";
                    var label = resxLabels.TryGetValue(key, out var lbl) ? lbl : "—";
                    sb.AppendLine($"| {prop.Name} | `{key}` | {label} |");
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("*System.resx файлы не найдены или пусты.*");
                sb.AppendLine();
            }

            // API: grep entity name in ServerFunctions.cs
            sb.AppendLine("### API References");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(moduleDir))
            {
                var serverFuncFiles = Directory.GetFiles(moduleDir, "*ServerFunctions.cs", SearchOption.AllDirectories);
                var apiRefs = new List<(string File, int Line, string Text)>();

                var entityPattern = new Regex($@"\b{Regex.Escape(entity.Name)}\b");

                foreach (var sfFile in serverFuncFiles)
                {
                    try
                    {
                        var lines = await File.ReadAllLinesAsync(sfFile);
                        for (var i = 0; i < lines.Length; i++)
                        {
                            if (entityPattern.IsMatch(lines[i]))
                            {
                                var relPath = Path.GetRelativePath(solutionPath, sfFile);
                                apiRefs.Add((relPath, i + 1, lines[i].Trim()));
                            }
                        }
                    }
                    catch { }
                }

                if (apiRefs.Count > 0)
                {
                    sb.AppendLine($"Найдено {apiRefs.Count} упоминаний в ServerFunctions:");
                    sb.AppendLine();
                    foreach (var (file, line, text) in apiRefs.Take(20))
                        sb.AppendLine($"- `{file}:{line}` — `{text}`");

                    if (apiRefs.Count > 20)
                        sb.AppendLine($"- ... и ещё {apiRefs.Count - 20}");
                }
                else
                {
                    sb.AppendLine($"Упоминания `{entity.Name}` в ServerFunctions.cs не найдены.");
                }
            }
            else
            {
                sb.AppendLine("*Директория модуля не определена.*");
            }
            sb.AppendLine();

            // Full trace summary
            sb.AppendLine("### Полная трасса (Property → DB → Resx → Events)");
            sb.AppendLine();
            sb.AppendLine("| Property | DB Column | DB Type | Resx Label | Handled Events |");
            sb.AppendLine("|----------|-----------|---------|------------|----------------|");

            var eventsStr = handledEvents.Count > 0 ? string.Join(", ", handledEvents) : "—";
            foreach (var prop in entity.Properties)
            {
                var shortType = prop.Type.Split('.').LastOrDefault() ?? prop.Type;
                var dbType = DbTypeMap.TryGetValue(shortType, out var dt2) ? dt2 : "?";
                var resxKey = $"Property_{prop.Name}";
                var label = resxLabels.TryGetValue(resxKey, out var lbl2) ? lbl2 : "—";
                sb.AppendLine($"| {prop.Name} | `{prop.Code}` | {dbType} | {label} | {eventsStr} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static List<(string AncestorGuid, string EntityA, string EntityB, string ModuleA, string ModuleB)> FindOverrideConflicts(
        List<ModuleInfo> modules, List<EntityInfo> entities)
    {
        var moduleByGuid = modules.ToDictionary(m => m.Guid, StringComparer.OrdinalIgnoreCase);

        var overrideGroups = entities
            .Where(e => !string.IsNullOrEmpty(e.AncestorGuid))
            .GroupBy(e => e.AncestorGuid!)
            .Where(g => g.Count() > 1)
            .ToList();

        var conflicts = new List<(string, string, string, string, string)>();

        foreach (var grp in overrideGroups)
        {
            var overrides = grp.ToList();
            for (var i = 0; i < overrides.Count; i++)
            {
                for (var j = i + 1; j < overrides.Count; j++)
                {
                    var e1 = overrides[i];
                    var e2 = overrides[j];

                    if (string.Equals(e1.ModuleGuid, e2.ModuleGuid, StringComparison.OrdinalIgnoreCase))
                        continue; // Same module — not a conflict

                    var mod1Name = moduleByGuid.TryGetValue(e1.ModuleGuid, out var mod1) ? mod1.Name : e1.ModuleGuid;
                    var mod2Name = moduleByGuid.TryGetValue(e2.ModuleGuid, out var mod2) ? mod2.Name : e2.ModuleGuid;

                    conflicts.Add((grp.Key, e1.Name, e2.Name, mod1Name, mod2Name));
                }
            }
        }

        return conflicts;
    }

    private static List<(string EntityA, string EntityB, string Code)> FindPropertyCodeCollisions(List<EntityInfo> entities)
    {
        var collisions = new List<(string, string, string)>();
        // Use a safe lookup that tolerates duplicate GUIDs (they'll be caught by the duplicates check)
        var entityByGuid = new Dictionary<string, EntityInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entities)
            entityByGuid.TryAdd(e.Guid, e);

        // Build inheritance chains: for each entity collect all ancestor entity guids
        var checkedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            // Walk up the inheritance chain via BaseGuid
            var ancestorChain = new List<EntityInfo>();
            var current = entity;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrEmpty(current.BaseGuid) && entityByGuid.TryGetValue(current.BaseGuid, out var parent))
            {
                if (visited.Contains(parent.Guid))
                    break;
                visited.Add(parent.Guid);
                ancestorChain.Add(parent);
                current = parent;
            }

            foreach (var ancestor in ancestorChain)
            {
                var pairKey = string.Compare(entity.Guid, ancestor.Guid, StringComparison.OrdinalIgnoreCase) < 0
                    ? $"{entity.Guid}|{ancestor.Guid}"
                    : $"{ancestor.Guid}|{entity.Guid}";

                if (checkedPairs.Contains(pairKey))
                    continue;
                checkedPairs.Add(pairKey);

                var codes1 = entity.Properties.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var codes2 = ancestor.Properties.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var code in codes1.Intersect(codes2, StringComparer.OrdinalIgnoreCase))
                    collisions.Add((entity.Name, ancestor.Name, code));
            }
        }

        return collisions;
    }

    private static List<(string EntityName, string EnumName, List<string> DuplicateValues)> FindDuplicateEnumValues(List<EntityInfo> entities)
    {
        // This is a simplified check — duplicate property Names within the same entity's properties
        var result = new List<(string, string, List<string>)>();

        foreach (var entity in entities)
        {
            var nameGroups = entity.Properties
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var grp in nameGroups)
                result.Add((entity.Name, "Properties", grp.Select(p => p.Name).ToList()));
        }

        return result;
    }

    private static List<ModuleInfo> FindOrphanCustomModules(List<ModuleInfo> modules)
    {
        var allReferencedGuids = new HashSet<string>(
            modules.SelectMany(m => m.DependencyGuids),
            StringComparer.OrdinalIgnoreCase);

        return modules
            .Where(m => !m.IsPlatform && !allReferencedGuids.Contains(m.Guid))
            .OrderBy(m => m.Name)
            .ToList();
    }

}
