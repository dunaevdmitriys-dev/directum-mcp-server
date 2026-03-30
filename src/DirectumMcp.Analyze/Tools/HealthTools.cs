using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class HealthTools
{
    [McpServerTool(Name = "solution_health")]
    [Description("Дашборд здоровья решения: модули, сущности, ошибки, покрытие resx, качество кода, граф зависимостей — всё в одном.")]
    public async Task<string> SolutionHealth(
        [Description("Путь к решению или модулю")] string path)
    {
        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Solution Health Dashboard");
        sb.AppendLine();

        // 1. Module count
        int baseModules = 0, workModules = 0;
        var baseDir = Path.Combine(path, "base");
        var workDir = Path.Combine(path, "work");

        if (Directory.Exists(baseDir)) baseModules = Directory.GetDirectories(baseDir).Length;
        if (Directory.Exists(workDir)) workModules = Directory.GetDirectories(workDir).Length;

        // If not a solution root, treat as single module
        bool isSolution = baseModules > 0 || workModules > 0;
        var scanPath = isSolution ? path : path;

        // 2. Count entities, resx, cs files
        var mtdFiles = Directory.GetFiles(scanPath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase)).ToArray();
        var moduleMtds = Directory.GetFiles(scanPath, "Module.mtd", SearchOption.AllDirectories);
        var resxFiles = Directory.GetFiles(scanPath, "*.resx", SearchOption.AllDirectories);
        var resxRuFiles = Directory.GetFiles(scanPath, "*.ru.resx", SearchOption.AllDirectories);
        var csFiles = Directory.GetFiles(scanPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains(".g.cs")).ToArray();

        int totalLines = 0;
        foreach (var cs in csFiles)
        {
            try { totalLines += File.ReadAllLines(cs).Length; } catch { }
        }

        // 3. Count properties, actions, handlers
        int totalProperties = 0, totalActions = 0, totalAsyncHandlers = 0, totalJobs = 0;
        int totalWidgets = 0, totalPublicStructures = 0, totalPublicFunctions = 0;
        var entityTypes = new Dictionary<string, int>();

        foreach (var mtdFile in mtdFiles.Concat(moduleMtds))
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                    totalProperties += props.GetArrayLength();
                if (root.TryGetProperty("Actions", out var acts) && acts.ValueKind == JsonValueKind.Array)
                    totalActions += acts.GetArrayLength();
                if (root.TryGetProperty("AsyncHandlers", out var ah) && ah.ValueKind == JsonValueKind.Array)
                    totalAsyncHandlers += ah.GetArrayLength();
                if (root.TryGetProperty("Jobs", out var jbs) && jbs.ValueKind == JsonValueKind.Array)
                    totalJobs += jbs.GetArrayLength();
                if (root.TryGetProperty("Widgets", out var wdg) && wdg.ValueKind == JsonValueKind.Array)
                    totalWidgets += wdg.GetArrayLength();
                if (root.TryGetProperty("PublicStructures", out var ps) && ps.ValueKind == JsonValueKind.Array)
                    totalPublicStructures += ps.GetArrayLength();
                if (root.TryGetProperty("PublicFunctions", out var pf) && pf.ValueKind == JsonValueKind.Array)
                    totalPublicFunctions += pf.GetArrayLength();

                // Entity type distribution
                var baseGuid = root.TryGetProperty("BaseGuid", out var bg) ? bg.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(baseGuid) && !Path.GetFileName(mtdFile).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                {
                    var baseType = DirectumConstants.ResolveBaseType(baseGuid);
                    entityTypes[baseType] = entityTypes.GetValueOrDefault(baseType) + 1;
                }
            }
            catch { }
        }

        // 4. Anti-pattern scan
        int antiPatterns = 0;
        foreach (var cs in csFiles.Take(200)) // Limit for performance
        {
            try
            {
                var content = await File.ReadAllTextAsync(cs);
                if (content.Contains("DateTime.Now") && !content.Contains("// allow")) antiPatterns++;
                if (content.Contains("System.Reflection") && !content.Contains("// allow")) antiPatterns++;
                if (content.Contains("Session.Execute")) antiPatterns++;
            }
            catch { }
        }

        // 5. Resx coverage
        int resxWithDisplayName = 0;
        foreach (var resx in resxRuFiles.Take(100))
        {
            try
            {
                var content = await File.ReadAllTextAsync(resx);
                if (content.Contains("DisplayName")) resxWithDisplayName++;
            }
            catch { }
        }

        // Dashboard output
        sb.AppendLine("```");
        sb.AppendLine("╔══════════════════════════════════════════════════╗");
        sb.AppendLine("║           SOLUTION HEALTH DASHBOARD              ║");
        sb.AppendLine("╠══════════════════════════════════════════════════╣");
        sb.AppendLine("║                                                  ║");

        if (isSolution)
        {
            sb.AppendLine($"║  Модулей (base):     {baseModules,5}                      ║");
            sb.AppendLine($"║  Модулей (work):     {workModules,5}                      ║");
        }
        sb.AppendLine($"║  Module.mtd:         {moduleMtds.Length,5}                      ║");
        sb.AppendLine($"║  Сущностей (.mtd):   {mtdFiles.Length,5}                      ║");
        sb.AppendLine($"║  C# файлов:          {csFiles.Length,5}                      ║");
        sb.AppendLine($"║  Строк кода:         {totalLines,5}                      ║");
        sb.AppendLine($"║  .resx файлов:       {resxFiles.Length,5}                      ║");
        sb.AppendLine("║                                                  ║");
        sb.AppendLine("╠══════════════ МЕТАДАННЫЕ ════════════════════════╣");
        sb.AppendLine("║                                                  ║");
        sb.AppendLine($"║  Properties:         {totalProperties,5}                      ║");
        sb.AppendLine($"║  Actions:            {totalActions,5}                      ║");
        sb.AppendLine($"║  AsyncHandlers:      {totalAsyncHandlers,5}                      ║");
        sb.AppendLine($"║  Jobs:               {totalJobs,5}                      ║");
        sb.AppendLine($"║  Widgets:            {totalWidgets,5}                      ║");
        sb.AppendLine($"║  PublicStructures:   {totalPublicStructures,5}                      ║");
        sb.AppendLine($"║  PublicFunctions:    {totalPublicFunctions,5}                      ║");
        sb.AppendLine("║                                                  ║");
        sb.AppendLine("╠══════════════ ТИПЫ СУЩНОСТЕЙ ═══════════════════╣");
        sb.AppendLine("║                                                  ║");

        foreach (var (type, count) in entityTypes.OrderByDescending(x => x.Value))
            sb.AppendLine($"║  {type,-20} {count,5}                      ║");

        sb.AppendLine("║                                                  ║");
        sb.AppendLine("╠══════════════ КАЧЕСТВО ═════════════════════════╣");
        sb.AppendLine("║                                                  ║");

        var resxCoverage = resxRuFiles.Length > 0 ? 100.0 * resxWithDisplayName / resxRuFiles.Length : 100;
        sb.AppendLine($"║  Anti-patterns:      {antiPatterns,5}  {(antiPatterns == 0 ? "✅" : "⚠️"),2}                ║");
        sb.AppendLine($"║  Resx coverage:      {resxCoverage,4:F0}%  {(resxCoverage >= 90 ? "✅" : "⚠️"),2}                ║");

        // Health score
        double score = 10.0;
        score -= antiPatterns * 0.5;
        score -= (100 - resxCoverage) * 0.05;
        score = Math.Max(1, Math.Min(10, score));
        var scoreBar = new string('█', (int)score) + new string('░', 10 - (int)score);

        sb.AppendLine("║                                                  ║");
        sb.AppendLine($"║  Health Score: [{scoreBar}] {score:F1}/10    ║");
        sb.AppendLine("║                                                  ║");
        sb.AppendLine("╚══════════════════════════════════════════════════╝");
        sb.AppendLine("```");

        // Recommendations
        sb.AppendLine();
        if (antiPatterns > 0)
            sb.AppendLine($"- ⚠️ **{antiPatterns} anti-patterns** → запустите `validate_all level=full`");
        if (resxCoverage < 90)
            sb.AppendLine($"- ⚠️ **Resx coverage {resxCoverage:F0}%** → запустите `sync_resx_keys`");
        if (totalAsyncHandlers > 0)
            sb.AppendLine($"- 💡 {totalAsyncHandlers} AsyncHandlers → запустите `lint_async_handlers` для проверки retry-стратегий");
        if (totalPublicStructures > 0)
            sb.AppendLine($"- 💡 {totalPublicStructures} PublicStructures → запустите `extract_public_structures` для документации");

        return sb.ToString();
    }
}
