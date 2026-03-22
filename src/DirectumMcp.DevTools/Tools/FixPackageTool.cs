using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class FixPackageTool
{
    private readonly PackageFixService _service = new();

    [McpServerTool(Name = "fix_package")]
    [Description("Автоисправление .dat: resx-ключи, дубли Code, enum, Constraints. dryRun по умолчанию.")]
    public async Task<string> Fix(
        [Description("Путь к .dat файлу или директории с распакованным пакетом")]
        string packagePath,

        [Description("Если true (по умолчанию) - показывает план исправлений без изменения файлов. " +
                     "Если false - применяет исправления и перепаковывает .dat")]
        bool dryRun = true,

        CancellationToken cancellationToken = default)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        var result = await _service.FixAsync(packagePath, dryRun, cancellationToken);

        if (!result.Success && result.Errors.Count > 0)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
