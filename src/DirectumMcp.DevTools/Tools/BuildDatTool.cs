using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class BuildDatTool
{
    private readonly PackageBuildService _service = new();

    [McpServerTool(Name = "build_dat")]
    [Description("Собрать .dat пакет из директории. Используй после scaffold/fix/sync.")]
    public async Task<string> BuildDat(
        [Description("Путь к директории пакета (должна содержать source/ и/или settings/)")]
        string packagePath,
        [Description("Путь для сохранения .dat файла. По умолчанию: родительская директория packagePath, имя файла = имя директории + \".dat\"")]
        string? outputPath = null,
        [Description("Строка версии для PackageInfo.xml (например, \"1.0.0.0\"). Если не указана — читается из Module.mtd, если доступен.")]
        string? version = null)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);
        if (outputPath != null && !PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        var result = await _service.BuildAsync(packagePath, outputPath, version);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
