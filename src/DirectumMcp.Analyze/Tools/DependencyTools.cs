using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class DependencyTools
{

    [McpServerTool(Name = "dependency_graph")]
    [Description("Граф зависимостей модулей: визуализация, циклы, impact-анализ.")]
    public async Task<string> DependencyGraph(
        [Description("Путь к корню решения. Если не указан — используется переменная окружения SOLUTION_PATH")] string? solutionPath = null,
        [Description("Действие: graph | cycles | impact (по умолчанию: graph)")] string action = "graph",
        [Description("GUID модуля для анализа влияния (требуется для action=impact)")] string? moduleGuid = null)
    {
        var resolvedPath = solutionPath ?? Environment.GetEnvironmentVariable("SOLUTION_PATH");

        if (string.IsNullOrEmpty(resolvedPath))
            return "**ОШИБКА**: Путь к решению не указан и переменная окружения SOLUTION_PATH не задана.";
        if (!Directory.Exists(resolvedPath))
            return $"**ОШИБКА**: Директория не найдена: `{resolvedPath}`";

        var graph = await BuildGraph(resolvedPath);

        if (graph.Count == 0)
            return $"**ОШИБКА**: Module.mtd файлы не найдены в `{resolvedPath}`";

        return action.ToLowerInvariant() switch
        {
            "cycles" => RenderCycles(graph),
            "impact" => RenderImpact(graph, moduleGuid),
            _ => RenderGraph(graph, resolvedPath)
        };
    }

    private record ModuleInfo(string Name, string Guid, List<string> DependencyGuids, bool IsPlatform);

    private static async Task<Dictionary<string, ModuleInfo>> BuildGraph(string solutionPath)
    {
        var mtdFiles = Directory.GetFiles(solutionPath, "Module.mtd", SearchOption.AllDirectories);
        var graph = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);

        var basePath = Path.GetFullPath(Path.Combine(solutionPath, "base"));
        var workPath = Path.GetFullPath(Path.Combine(solutionPath, "work"));

        foreach (var file in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var metaType = GetString(root, "$type");
                if (!metaType.Contains("ModuleMetadata"))
                    continue;

                var name = GetString(root, "Name");
                var guid = GetString(root, "NameGuid");

                if (string.IsNullOrEmpty(guid))
                    continue;

                var deps = new List<string>();
                if (root.TryGetProperty("Dependencies", out var depsEl) && depsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var dep in depsEl.EnumerateArray())
                    {
                        var id = GetString(dep, "Id");
                        if (!string.IsNullOrEmpty(id))
                            deps.Add(id.ToLowerInvariant());
                    }
                }

                var fullFilePath = Path.GetFullPath(file);
                var isPlatform = fullFilePath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) ||
                                 (!fullFilePath.StartsWith(workPath, StringComparison.OrdinalIgnoreCase) &&
                                  !fullFilePath.StartsWith(Path.GetFullPath(solutionPath + "/work"), StringComparison.OrdinalIgnoreCase));

                graph[guid.ToLowerInvariant()] = new ModuleInfo(name, guid.ToLowerInvariant(), deps, isPlatform);
            }
            catch
            {
                // Skip unparseable files
            }
        }

        return graph;
    }

    private static string RenderGraph(Dictionary<string, ModuleInfo> graph, string solutionPath)
    {
        var sb = new StringBuilder();
        var platformModules = graph.Values.Where(m => m.IsPlatform).OrderBy(m => m.Name).ToList();
        var customModules = graph.Values.Where(m => !m.IsPlatform).OrderBy(m => m.Name).ToList();

        sb.AppendLine("# Граф зависимостей модулей Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Решение:** `{solutionPath}`");
        sb.AppendLine($"**Всего модулей:** {graph.Count} (платформенных: {platformModules.Count}, кастомных: {customModules.Count})");
        sb.AppendLine();

        void RenderSection(string title, List<ModuleInfo> modules)
        {
            if (modules.Count == 0)
                return;

            sb.AppendLine($"## {title} ({modules.Count})");
            sb.AppendLine();
            sb.AppendLine("| Модуль | GUID | Зависимости (→) | Зависят от этого (←) |");
            sb.AppendLine("|--------|------|-----------------|----------------------|");

            foreach (var m in modules)
            {
                var depsResolved = m.DependencyGuids
                    .Select(d => graph.TryGetValue(d, out var dm) ? dm.Name : $"`{d}`")
                    .ToList();

                var dependents = graph.Values
                    .Where(other => other.DependencyGuids.Contains(m.Guid))
                    .Select(other => other.Name)
                    .OrderBy(n => n)
                    .ToList();

                var depsStr = depsResolved.Count > 0 ? string.Join(", ", depsResolved) : "—";
                var depStr = dependents.Count > 0 ? string.Join(", ", dependents) : "—";

                sb.AppendLine($"| **{m.Name}** | `{m.Guid}` | {depsStr} | {depStr} |");
            }

            sb.AppendLine();
        }

        RenderSection("Кастомные модули (work/)", customModules);
        RenderSection("Платформенные модули (base/)", platformModules);

        return sb.ToString();
    }

    private static string RenderCycles(Dictionary<string, ModuleInfo> graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Анализ циклических зависимостей");
        sb.AppendLine();

        var cycles = FindCycles(graph);

        if (cycles.Count == 0)
        {
            sb.AppendLine("Циклических зависимостей не обнаружено.");
        }
        else
        {
            sb.AppendLine($"**Обнаружено циклов: {cycles.Count}**");
            sb.AppendLine();
            for (var i = 0; i < cycles.Count; i++)
            {
                var cycle = cycles[i];
                var names = cycle.Select(guid =>
                    graph.TryGetValue(guid, out var m) ? m.Name : guid);
                sb.AppendLine($"{i + 1}. {string.Join(" → ", names)}");
            }
        }

        return sb.ToString();
    }

    private static List<List<string>> FindCycles(Dictionary<string, ModuleInfo> graph)
    {
        var cycles = new List<List<string>>();
        var color = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0=white, 1=gray, 2=black
        var path = new List<string>();

        foreach (var guid in graph.Keys)
            color[guid] = 0;

        void Dfs(string current)
        {
            color[current] = 1;
            path.Add(current);

            if (graph.TryGetValue(current, out var info))
            {
                foreach (var dep in info.DependencyGuids)
                {
                    if (!color.ContainsKey(dep))
                        continue;

                    if (color[dep] == 1)
                    {
                        var cycleStart = path.IndexOf(dep);
                        if (cycleStart >= 0)
                        {
                            var cycle = path[cycleStart..];
                            cycle.Add(dep);
                            cycles.Add(new List<string>(cycle));
                        }
                    }
                    else if (color[dep] == 0)
                    {
                        Dfs(dep);
                    }
                }
            }

            path.RemoveAt(path.Count - 1);
            color[current] = 2;
        }

        foreach (var guid in graph.Keys)
        {
            if (color[guid] == 0)
                Dfs(guid);
        }

        return cycles;
    }

    private static string RenderImpact(Dictionary<string, ModuleInfo> graph, string? moduleGuid)
    {
        if (string.IsNullOrEmpty(moduleGuid))
            return "**ОШИБКА**: Для действия `impact` необходимо указать параметр `moduleGuid`.";

        var normalizedGuid = moduleGuid.ToLowerInvariant();

        if (!graph.TryGetValue(normalizedGuid, out var targetModule))
            return $"**ОШИБКА**: Модуль с GUID `{moduleGuid}` не найден в решении. Убедитесь, что GUID указан верно.";

        var sb = new StringBuilder();
        sb.AppendLine($"# Анализ влияния модуля: {targetModule.Name}");
        sb.AppendLine();
        sb.AppendLine($"**GUID:** `{targetModule.Guid}`");
        sb.AppendLine($"**Тип:** {(targetModule.IsPlatform ? "платформенный" : "кастомный")}");
        sb.AppendLine();

        var directDependents = graph.Values
            .Where(m => m.DependencyGuids.Contains(normalizedGuid))
            .OrderBy(m => m.Name)
            .ToList();

        sb.AppendLine($"## Прямые зависимости ({directDependents.Count})");
        sb.AppendLine();
        if (directDependents.Count == 0)
        {
            sb.AppendLine("Ни один модуль не зависит от данного модуля напрямую.");
        }
        else
        {
            sb.AppendLine("| Модуль | GUID | Тип |");
            sb.AppendLine("|--------|------|-----|");
            foreach (var m in directDependents)
                sb.AppendLine($"| **{m.Name}** | `{m.Guid}` | {(m.IsPlatform ? "платформенный" : "кастомный")} |");
        }
        sb.AppendLine();

        var transitive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>(directDependents.Select(m => m.Guid));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (transitive.Contains(current))
                continue;
            transitive.Add(current);

            foreach (var m in graph.Values.Where(m => m.DependencyGuids.Contains(current)))
            {
                if (!transitive.Contains(m.Guid) && m.Guid != normalizedGuid)
                    queue.Enqueue(m.Guid);
            }
        }

        var transitiveOnly = transitive
            .Where(g => directDependents.All(d => !string.Equals(d.Guid, g, StringComparison.OrdinalIgnoreCase)))
            .Select(g => graph.TryGetValue(g, out var m) ? m : null)
            .Where(m => m != null)
            .OrderBy(m => m!.Name)
            .ToList();

        sb.AppendLine($"## Транзитивные зависимости ({transitiveOnly.Count})");
        sb.AppendLine();
        if (transitiveOnly.Count == 0)
        {
            sb.AppendLine("Транзитивных зависимостей нет.");
        }
        else
        {
            sb.AppendLine("| Модуль | GUID | Тип |");
            sb.AppendLine("|--------|------|-----|");
            foreach (var m in transitiveOnly)
                sb.AppendLine($"| **{m!.Name}** | `{m.Guid}` | {(m.IsPlatform ? "платформенный" : "кастомный")} |");
        }
        sb.AppendLine();

        sb.AppendLine($"## Итого");
        sb.AppendLine();
        sb.AppendLine($"- Прямых зависимостей: **{directDependents.Count}**");
        sb.AppendLine($"- Транзитивных зависимостей: **{transitiveOnly.Count}**");
        sb.AppendLine($"- Суммарный охват: **{transitive.Count}** модул{(transitive.Count is 1 ? "ь" : transitive.Count is >= 2 and <= 4 ? "я" : "ей")}");

        return sb.ToString();
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }


    [McpServerTool(Name = "visualize_dependencies")]
    [Description("Визуализация графа зависимостей модулей: Mermaid-диаграмма + интерактивный HTML. Показывает связи, циклы, orphans.")]
    public async Task<string> VisualizeDependencies(
        [Description("Путь к решению (содержит base/ и/или work/)")] string solutionPath,
        [Description("Формат: mermaid (текст) или html (интерактивный граф)")] string format = "mermaid")
    {
        if (!Directory.Exists(solutionPath))
            return $"**ОШИБКА**: Директория не найдена: `{solutionPath}`";

        // Collect modules and dependencies
        var modules = new Dictionary<string, VisModuleInfo>();
        var dependencies = new List<(string From, string To)>();

        foreach (var subDir in new[] { "base", "work" })
        {
            var dir = Path.Combine(solutionPath, subDir);
            if (!Directory.Exists(dir)) continue;

            foreach (var moduleDir in Directory.GetDirectories(dir))
            {
                var mtdFiles = Directory.GetFiles(moduleDir, "Module.mtd", SearchOption.AllDirectories);
                foreach (var mtdFile in mtdFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(mtdFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var guid = root.TryGetProperty("NameGuid", out var g) ? g.GetString() ?? "" : "";
                        var name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                        var companyCode = root.TryGetProperty("CompanyCode", out var cc) ? cc.GetString() ?? "" : "";
                        var fullName = string.IsNullOrEmpty(companyCode) ? name : $"{companyCode}.{name}";

                        if (string.IsNullOrEmpty(guid)) continue;

                        var entityCount = Directory.GetFiles(Path.GetDirectoryName(mtdFile)!, "*.mtd", SearchOption.AllDirectories)
                            .Count(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));

                        modules[guid] = new VisModuleInfo(fullName, guid, subDir, entityCount);

                        if (root.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var dep in deps.EnumerateArray())
                            {
                                var depId = dep.TryGetProperty("Id", out var did) ? did.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(depId))
                                    dependencies.Add((guid, depId));
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        if (modules.Count == 0)
            return "Модули не найдены. Проверьте путь к решению (должен содержать base/ или work/).";

        if (format == "html")
            return GenerateHtml(modules, dependencies);
        else
            return GenerateMermaid(modules, dependencies);
    }

    private static string GenerateMermaid(Dictionary<string, VisModuleInfo> modules, List<(string From, string To)> deps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Граф зависимостей модулей");
        sb.AppendLine();
        sb.AppendLine($"**Модулей:** {modules.Count} | **Зависимостей:** {deps.Count}");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("graph LR");

        // Nodes
        foreach (var (guid, info) in modules)
        {
            var shortGuid = guid[..8];
            var shape = info.Source == "work" ? $"[[\"{info.Name}\\n({info.EntityCount} сущностей)\"]]" : $"(\"{info.Name}\\n({info.EntityCount})\")";
            sb.AppendLine($"  {shortGuid}{shape}");
        }

        // Edges
        foreach (var (from, to) in deps)
        {
            if (modules.ContainsKey(from))
            {
                var fromShort = from[..8];
                var toShort = modules.ContainsKey(to) ? to[..8] : "ext_" + to[..6];

                if (!modules.ContainsKey(to))
                    sb.AppendLine($"  {toShort}[\"External\\n{to[..13]}...\"]");

                sb.AppendLine($"  {fromShort} --> {toShort}");
            }
        }

        // Styles
        sb.AppendLine();
        foreach (var (guid, info) in modules)
        {
            var shortGuid = guid[..8];
            if (info.Source == "work")
                sb.AppendLine($"  style {shortGuid} fill:#e1f5fe,stroke:#01579b");
            else
                sb.AppendLine($"  style {shortGuid} fill:#f3e5f5,stroke:#4a148c");
        }

        sb.AppendLine("```");
        sb.AppendLine();

        // Legend
        sb.AppendLine("**Легенда:** Синие = work/ (кастомные) | Фиолетовые = base/ (платформа)");
        sb.AppendLine();

        // Orphan detection
        var referenced = deps.Select(d => d.To).ToHashSet();
        var referencing = deps.Select(d => d.From).ToHashSet();
        var orphans = modules.Keys.Where(g => !referenced.Contains(g) && !referencing.Contains(g)).ToList();
        if (orphans.Count > 0)
        {
            sb.AppendLine("### Изолированные модули (нет связей)");
            foreach (var o in orphans)
                sb.AppendLine($"- {modules[o].Name}");
        }

        // Cycle detection (simple)
        foreach (var (from, to) in deps)
        {
            if (deps.Any(d => d.From == to && d.To == from))
            {
                var fromName = modules.TryGetValue(from, out var fi) ? fi.Name : from[..8];
                var toName = modules.TryGetValue(to, out var ti) ? ti.Name : to[..8];
                sb.AppendLine();
                sb.AppendLine($"**ЦИКЛ:** {fromName} ↔ {toName}");
            }
        }

        return sb.ToString();
    }

    private static string GenerateHtml(Dictionary<string, VisModuleInfo> modules, List<(string From, string To)> deps)
    {
        var nodesJson = new StringBuilder("[");
        var edgesJson = new StringBuilder("[");

        int i = 0;
        var guidToIdx = new Dictionary<string, int>();
        foreach (var (guid, info) in modules)
        {
            if (i > 0) nodesJson.Append(",");
            var color = info.Source == "work" ? "#01579b" : "#7b1fa2";
            nodesJson.Append($"{{\"id\":{i},\"label\":\"{info.Name}\\n({info.EntityCount})\",\"color\":\"{color}\"}}");
            guidToIdx[guid] = i;
            i++;
        }
        nodesJson.Append("]");

        int e = 0;
        foreach (var (from, to) in deps)
        {
            if (guidToIdx.ContainsKey(from) && guidToIdx.ContainsKey(to))
            {
                if (e > 0) edgesJson.Append(",");
                edgesJson.Append($"{{\"from\":{guidToIdx[from]},\"to\":{guidToIdx[to]}}}");
                e++;
            }
        }
        edgesJson.Append("]");

        var html = new StringBuilder();
        html.AppendLine($"# Интерактивный граф зависимостей");
        html.AppendLine();
        html.AppendLine($"**Модулей:** {modules.Count} | **Зависимостей:** {deps.Count}");
        html.AppendLine();
        html.AppendLine("Скопируйте HTML ниже в файл и откройте в браузере:");
        html.AppendLine();
        html.AppendLine("```html");
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head><title>Directum RX — Граф зависимостей</title>");
        html.AppendLine("<script src=\"https://unpkg.com/vis-network/standalone/umd/vis-network.min.js\"></script>");
        html.AppendLine("<style>body{margin:0;font-family:Arial}#graph{width:100vw;height:100vh}.info{position:fixed;top:10px;left:10px;background:#fff;padding:10px;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,.15);z-index:10}</style>");
        html.AppendLine("</head><body>");
        html.AppendLine($"<div class=\"info\"><b>Directum RX</b> — {modules.Count} модулей, {deps.Count} зависимостей<br>");
        html.AppendLine("<span style=\"color:#01579b\">■</span> work/ <span style=\"color:#7b1fa2\">■</span> base/</div>");
        html.AppendLine("<div id=\"graph\"></div>");
        html.AppendLine("<script>");
        html.AppendLine($"var nodes=new vis.DataSet({nodesJson});");
        html.AppendLine($"var edges=new vis.DataSet({edgesJson});");
        html.AppendLine("var c=document.getElementById('graph');");
        html.AppendLine("new vis.Network(c,{nodes:nodes,edges:edges},{nodes:{shape:'box',font:{size:12},borderWidth:2},edges:{arrows:'to',color:'#999'},physics:{solver:'forceAtlas2Based'}});");
        html.AppendLine("</script></body></html>");
        html.AppendLine("```");
        return html.ToString();
    }

    private record VisModuleInfo(string Name, string Guid, string Source, int EntityCount);

}
