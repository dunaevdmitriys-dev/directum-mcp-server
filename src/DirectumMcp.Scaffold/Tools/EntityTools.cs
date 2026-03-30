using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Models;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class EntityTools
{
    [McpServerTool(Name = "scaffold_entity")]
    [Description(
        "Создать скелет сущности Directum RX: MTD + resx + C# стабы. " +
        "Используй ПОСЛЕ dds-entity-design для определения типа и свойств. " +
        "Для переопределения существующей сущности используй mode=override с ancestorGuid. " +
        "Для документов используй baseType=Document. " +
        "Макс 30 свойств. Формат свойств: 'Name:string,Amount:int,Status:enum(Active|Closed)'.")]
    public async Task<string> ScaffoldEntity(
        EntityScaffoldService service,
        SolutionPathConfig config,
        [Description("Путь для создания файлов (внутри solution)")] string outputPath,
        [Description("Имя сущности в PascalCase (например 'ContractDocument')")] string entityName,
        [Description("Пространство имён модуля (например 'DirRX.Contracts')")] string moduleName,
        [Description("Базовый тип: DatabookEntry, Document, Task, Assignment, Notice")] string baseType = "DatabookEntry",
        [Description("Режим: 'new' — создание, 'override' — переопределение")] string mode = "new",
        [Description("Свойства: 'Name:string,Amount:int,Status:enum(Active|Closed),Counterparty:navigation'")] string properties = "",
        [Description("GUID переопределяемой сущности (только для mode=override)")] string ancestorGuid = "",
        [Description("Русское название для ru.resx")] string russianName = "")
    {
        var result = await service.ScaffoldAsync(
            outputPath, entityName, moduleName, baseType, mode,
            properties, ancestorGuid, russianName);

        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
