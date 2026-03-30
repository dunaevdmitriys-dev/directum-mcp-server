using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class ModuleTools
{
    [McpServerTool(Name = "scaffold_module")]
    [Description(
        "Создать модуль Directum RX с нуля: Module.mtd, обложка, C# стабы, .sds, resx. " +
        "Готов к открытию в DDS. Для добавления сущностей используй scaffold_entity. " +
        "Для готовых паттернов модуля используй suggest_pattern.")]
    public async Task<string> ScaffoldModule(
        ModuleScaffoldService service,
        [Description("Базовая директория (модуль создаётся внутри)")] string outputPath,
        [Description("Имя модуля в PascalCase (например 'HRManagement')")] string moduleName,
        [Description("Код компании (например 'DirRX')")] string companyCode = "DirRX",
        [Description("Русское название модуля")] string displayNameRu = "",
        [Description("Версия модуля (например '1.0.0.0')")] string version = "1.0.0.0",
        [Description("GUID зависимых модулей через запятую")] string dependencies = "",
        [Description("Создать обложку модуля (Cover)")] bool hasCover = true,
        [Description("Группы обложки через запятую")] string coverGroups = "")
    {
        var result = await service.ScaffoldAsync(
            outputPath, moduleName, companyCode, displayNameRu, version,
            dependencies, hasCover, coverGroups);

        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
