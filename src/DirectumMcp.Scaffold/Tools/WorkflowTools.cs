using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class WorkflowTools
{
    [McpServerTool(Name = "scaffold_job")]
    [Description(
        "Создать Background Job Directum RX: MTD + обработчик + resx. " +
        "Задание регистрируется в Module.mtd автоматически. " +
        "Для async обработчика используй scaffold_async_handler.")]
    public async Task<string> ScaffoldJob(
        JobScaffoldService service,
        [Description("Путь к директории модуля")] string outputPath,
        [Description("Имя задания в PascalCase")] string jobName,
        [Description("Простран��тво имён модуля")] string moduleName,
        [Description("Cron-расписание (по умолчанию: ежедневно в полночь)")] string cronSchedule = "0 0 * * *")
    {
        var result = await service.ScaffoldAsync(outputPath, jobName, moduleName, cronSchedule);
        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
