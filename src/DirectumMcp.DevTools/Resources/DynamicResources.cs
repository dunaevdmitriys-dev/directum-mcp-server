using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Resources;

/// <summary>
/// Dynamic Resource Templates — live data from solution repository.
/// </summary>
[McpServerResourceType]
public class DynamicResources
{
    private static string SolutionPath =>
        Environment.GetEnvironmentVariable("SOLUTION_PATH") ?? "";

    [McpServerResource(UriTemplate = "directum://solution/modules", Name = "Solution Modules", MimeType = "text/plain")]
    [Description("Список всех модулей в текущем решении (base/ + work/) — имена, GUID, количество сущностей")]
    public static string GetSolutionModules()
    {
        var solutionPath = SolutionPath;
        if (string.IsNullOrEmpty(solutionPath) || !Directory.Exists(solutionPath))
            return "Переменная SOLUTION_PATH не установлена или путь не существует.";

        var sb = new StringBuilder();
        sb.AppendLine("# Модули решения");
        sb.AppendLine();

        foreach (var subDir in new[] { "base", "work" })
        {
            var dir = Path.Combine(solutionPath, subDir);
            if (!Directory.Exists(dir)) continue;

            sb.AppendLine($"## {subDir}/");
            sb.AppendLine("| Модуль | GUID | Сущностей |");
            sb.AppendLine("|--------|------|-----------|");

            foreach (var moduleDir in Directory.GetDirectories(dir))
            {
                var moduleName = Path.GetFileName(moduleDir);
                var mtdFiles = FindModuleMtd(moduleDir);

                string guid = "—";
                int entityCount = 0;

                if (mtdFiles.moduleMtd != null)
                {
                    try
                    {
                        var json = File.ReadAllText(mtdFiles.moduleMtd);
                        using var doc = JsonDocument.Parse(json);
                        guid = doc.RootElement.TryGetProperty("NameGuid", out var g)
                            ? g.GetString() ?? "—" : "—";
                    }
                    catch { }
                }

                entityCount = mtdFiles.entityMtds;
                sb.AppendLine($"| {moduleName} | {guid} | {entityCount} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [McpServerResource(UriTemplate = "directum://solution/entities", Name = "Solution Entities", MimeType = "text/plain")]
    [Description("Список всех сущностей в work/ модулях — имя, GUID, тип, модуль")]
    public static string GetSolutionEntities()
    {
        var solutionPath = SolutionPath;
        if (string.IsNullOrEmpty(solutionPath) || !Directory.Exists(solutionPath))
            return "Переменная SOLUTION_PATH не установлена.";

        var workDir = Path.Combine(solutionPath, "work");
        if (!Directory.Exists(workDir))
            return "Директория work/ не найдена.";

        var sb = new StringBuilder();
        sb.AppendLine("# Сущности кастомных модулей (work/)");
        sb.AppendLine();
        sb.AppendLine("| Модуль | Сущность | GUID | Тип |");
        sb.AppendLine("|--------|----------|------|-----|");

        foreach (var moduleDir in Directory.GetDirectories(workDir))
        {
            var moduleName = Path.GetFileName(moduleDir);
            var sharedDirs = Directory.GetDirectories(moduleDir, "*.Shared", SearchOption.TopDirectoryOnly);

            foreach (var sharedDir in sharedDirs)
            {
                var mtdFiles = Directory.GetFiles(sharedDir, "*.mtd", SearchOption.AllDirectories)
                    .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));

                foreach (var mtdFile in mtdFiles)
                {
                    try
                    {
                        var json = File.ReadAllText(mtdFile);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        var name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
                        var guid = root.TryGetProperty("NameGuid", out var g) ? g.GetString() ?? "?" : "?";
                        var baseGuid = root.TryGetProperty("BaseGuid", out var bg) ? bg.GetString() ?? "" : "";
                        var baseType = DirectumMcp.Core.Helpers.DirectumConstants.ResolveBaseType(baseGuid);

                        sb.AppendLine($"| {moduleName} | {name} | {guid} | {baseType} |");
                    }
                    catch { }
                }
            }
        }

        return sb.ToString();
    }

    [McpServerResource(UriTemplate = "directum://solution/status", Name = "Solution Status", MimeType = "text/plain")]
    [Description("Статус решения: количество модулей, сущностей, последние изменения")]
    public static string GetSolutionStatus()
    {
        var solutionPath = SolutionPath;
        if (string.IsNullOrEmpty(solutionPath) || !Directory.Exists(solutionPath))
            return "Переменная SOLUTION_PATH не установлена.";

        var sb = new StringBuilder();
        sb.AppendLine("# Статус решения");
        sb.AppendLine();
        sb.AppendLine($"**Путь:** `{solutionPath}`");

        int baseModules = 0, workModules = 0, totalEntities = 0;

        var baseDir = Path.Combine(solutionPath, "base");
        if (Directory.Exists(baseDir))
            baseModules = Directory.GetDirectories(baseDir).Length;

        var workDir = Path.Combine(solutionPath, "work");
        if (Directory.Exists(workDir))
        {
            workModules = Directory.GetDirectories(workDir).Length;
            foreach (var modDir in Directory.GetDirectories(workDir))
            {
                var sharedDirs = Directory.GetDirectories(modDir, "*.Shared", SearchOption.TopDirectoryOnly);
                foreach (var sd in sharedDirs)
                    totalEntities += Directory.GetFiles(sd, "*.mtd", SearchOption.AllDirectories)
                        .Count(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));
            }
        }

        sb.AppendLine($"**Платформенных модулей (base/):** {baseModules}");
        sb.AppendLine($"**Кастомных модулей (work/):** {workModules}");
        sb.AppendLine($"**Сущностей в work/:** {totalEntities}");

        return sb.ToString();
    }

    private static (string? moduleMtd, int entityMtds) FindModuleMtd(string moduleDir)
    {
        string? moduleMtd = null;
        int entityCount = 0;

        var sharedDirs = Directory.GetDirectories(moduleDir, "*.Shared", SearchOption.TopDirectoryOnly);
        foreach (var sd in sharedDirs)
        {
            var mtds = Directory.GetFiles(sd, "*.mtd", SearchOption.AllDirectories);
            foreach (var m in mtds)
            {
                if (Path.GetFileName(m).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                    moduleMtd = m;
                else
                    entityCount++;
            }
        }

        return (moduleMtd, entityCount);
    }
}
