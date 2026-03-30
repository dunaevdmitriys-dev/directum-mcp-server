using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class DependencyGraphVisualizerTool
{
    [McpServerTool(Name = "visualize_dependencies")]
    [Description("Визуализация графа зависимостей модулей: Mermaid-диаграмма + интерактивный HTML. Показывает связи, циклы, orphans.")]
    public async Task<string> VisualizeDependencies(
        [Description("Путь к решению (содержит base/ и/или work/)")] string solutionPath,
        [Description("Формат: mermaid (текст) или html (интерактивный граф)")] string format = "mermaid")
    {
        if (!PathGuard.IsAllowed(solutionPath))
            return PathGuard.DenyMessage(solutionPath);

        if (!Directory.Exists(solutionPath))
            return $"**ОШИБКА**: Директория не найдена: `{solutionPath}`";

        // Collect modules and dependencies
        var modules = new Dictionary<string, ModuleInfo>();
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

                        modules[guid] = new ModuleInfo(fullName, guid, subDir, entityCount);

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

    private static string GenerateMermaid(Dictionary<string, ModuleInfo> modules, List<(string From, string To)> deps)
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

    private static string GenerateHtml(Dictionary<string, ModuleInfo> modules, List<(string From, string To)> deps)
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

    private record ModuleInfo(string Name, string Guid, string Source, int EntityCount);
}
