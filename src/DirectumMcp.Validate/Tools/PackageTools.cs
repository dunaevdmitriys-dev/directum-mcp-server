using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class PackageTools
{
    [McpServerTool(Name = "check_package")]
    [Description(
        "Валидация пакета перед импортом: 14 проверок (коллекции, ссылки, enum, Code, resx, " +
        "Analyzers, GUID, DisplayName, Controls, CoverFunction, FormTabs, Structures, DomainApi). " +
        "Для автоисправления используй fix_package. Для полной валидации — validate_all.")]
    public async Task<string> Validate(
        PackageValidateService service,
        [Description("Путь к .dat файлу или директории пакета")] string packagePath)
    {
        var result = await service.ValidateAsync(packagePath);

        if (!result.Success && result.Errors.Count > 0 && result.Checks.Count == 0)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }

    [McpServerTool(Name = "fix_package")]
    [Description(
        "Автоисправление пакета: resx-ключи, дубли Code, enum, Constraints. " +
        "По умолчанию dryRun=true (только показывает план). " +
        "Для применения изменений: dryRun=false. " +
        "Сначала запусти check_package чтобы увидеть проблемы.")]
    public async Task<string> Fix(
        PackageFixService service,
        [Description("Путь к .dat или директории")] string packagePath,
        [Description("true = только показать план, false = применить")] bool dryRun = true,
        CancellationToken ct = default)
    {
        var result = await service.FixAsync(packagePath, dryRun, ct);

        if (!result.Success && result.Errors.Count > 0)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
