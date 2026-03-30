using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Deploy.Tools;

[McpServerToolType]
public class BuildTools
{
    [McpServerTool(Name = "build_dat")]
    [Description(
        "Собрать .dat пакет из исходников решения. " +
        "Может занять 30-120 секунд. Запусти validate_all ПЕРЕД сборкой. " +
        "Результат: .dat файл. Для деплоя — deploy_to_stand.")]
    public async Task<string> BuildDat(
        PackageBuildService service,
        [Description("Путь к source/ директории пакета")] string packagePath,
        [Description("Версия пакета")] string? version = null,
        [Description("Директория для .dat (по умолчанию — рядом с source)")] string? outputPath = null,
        CancellationToken ct = default)
    {
        var result = await service.BuildAsync(packagePath, outputPath, version, ct);
        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
