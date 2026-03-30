using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldJobTool
{
    private static readonly JobScaffoldService Service = new();

    [McpServerTool(Name = "scaffold_job")]
    [Description("Создать Background Job: MTD + обработчик + resx.")]
    public async Task<string> ScaffoldJob(
        [Description("Путь к директории модуля")] string outputPath,
        [Description("Имя задания в PascalCase")] string jobName,
        [Description("Пространство имён модуля")] string moduleName,
        [Description("Cron-расписание (по умолчанию: ежедневно в полночь)")] string cronSchedule = "0 0 * * *")
    {
        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        var result = await Service.ScaffoldAsync(outputPath, jobName, moduleName, cronSchedule);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }

    /// <summary>
    /// Legacy entry point for ScaffoldEntityTool (mode=job).
    /// </summary>
    internal static async Task<string> ExecuteAsync(string outputPath, string jobName, string moduleName, string cronSchedule)
    {
        var result = await Service.ScaffoldAsync(outputPath, jobName, moduleName, cronSchedule);
        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
