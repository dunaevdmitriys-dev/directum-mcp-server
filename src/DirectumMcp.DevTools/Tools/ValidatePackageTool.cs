using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidatePackageTool
{
    private readonly PackageValidateService _service = new();

    [McpServerTool(Name = "check_package")]
    [Description("Валидация .dat перед импортом в DDS: 14 проверок (коллекции, ссылки, enum, Code, resx, Analyzers, GUID, DisplayName, Controls, CoverFunction, FormTabs, Structures, DomainApi).")]
    public async Task<string> Validate(string packagePath)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        var result = await _service.ValidateAsync(packagePath);

        if (!result.Success && result.Errors.Count > 0 && result.Checks.Count == 0)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
