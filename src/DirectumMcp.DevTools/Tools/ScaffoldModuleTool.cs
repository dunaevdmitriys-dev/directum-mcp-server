using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldModuleTool
{
    private readonly ModuleScaffoldService _service = new();

    [McpServerTool(Name = "scaffold_module")]
    [Description("Создать модуль Directum RX с нуля: Module.mtd, обложка, C# стабы, .sds, resx. Готов к открытию в DDS.")]
    public async Task<string> ScaffoldModule(
        [Description("Базовая директория (например work/). Модуль создаётся внутри.")] string outputPath,
        [Description("Имя модуля в PascalCase (например 'HRManagement')")] string moduleName,
        [Description("Код компании (например 'DirRX')")] string companyCode = "DirRX",
        [Description("Русское название модуля для отображения")] string displayNameRu = "",
        [Description("Версия модуля (например '1.0.0.0')")] string version = "1.0.0.0",
        [Description("GUID зависимых модулей через запятую. Формат: 'guid1,guid2' или 'guid:solution' для модулей решения")] string dependencies = "",
        [Description("Создать обложку модуля (Cover)")] bool hasCover = true,
        [Description("Группы обложки через запятую (например 'Продажи,Аналитика,Настройки')")] string coverGroups = "")
    {
        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        var result = await _service.ScaffoldAsync(
            outputPath, moduleName, companyCode, displayNameRu, version,
            dependencies, hasCover, coverGroups);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
