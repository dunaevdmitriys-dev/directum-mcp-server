using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldEntityTool
{
    private readonly EntityScaffoldService _service = new();

    [McpServerTool(Name = "scaffold_entity")]
    [Description("Создать скелет сущности: MTD + resx + C# стабы. Режимы: new, override.")]
    public async Task<string> ScaffoldEntity(
        [Description("Путь к директории, где будут созданы файлы сущности")] string outputPath,
        [Description("Имя сущности в PascalCase (например 'ContractDocument')")] string entityName,
        [Description("Пространство имён модуля (например 'DirRX.Contracts')")] string moduleName,
        [Description("Базовый тип: DatabookEntry, Document, Task, Assignment, Notice")] string baseType = "DatabookEntry",
        [Description("Режим: 'new' — создание с нуля, 'override' — переопределение существующей сущности")] string mode = "new",
        [Description("Свойства через запятую: 'Name:string,Amount:int,Status:enum(Active|Closed),Counterparty:navigation'")] string properties = "",
        [Description("GUID переопределяемой сущности (только для mode=override)")] string ancestorGuid = "",
        [Description("Русское название сущности для ru.resx (необязательно, по умолчанию '[RU] EntityName')")] string russianName = "")
    {
        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        if (mode == "job")
            return await ScaffoldJobTool.ExecuteAsync(outputPath, entityName, moduleName, properties);

        var result = await _service.ScaffoldAsync(
            outputPath, entityName, moduleName, baseType, mode, properties, ancestorGuid, russianName);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
