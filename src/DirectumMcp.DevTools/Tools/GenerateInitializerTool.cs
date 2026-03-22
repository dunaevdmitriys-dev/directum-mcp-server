using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class GenerateInitializerTool
{
    private readonly InitializerGenerateService _service = new();

    [McpServerTool(Name = "generate_initializer")]
    [Description("Генерация ModuleInitializer: CreateOrUpdate справочников, создание ролей, выдача прав. Готовый код для инициализации.")]
    public async Task<string> GenerateInitializer(
        [Description("Путь к корню модуля")] string modulePath,
        [Description("Полное имя модуля (например 'DirRX.CRM')")] string moduleName,
        [Description("Записи справочников: 'EntityName:Значение1|Значение2;Entity2:Val1|Val2'")] string records = "",
        [Description("Роли: 'Admin:Администратор:Описание;Manager:Менеджер:Описание'")] string roles = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var result = await _service.GenerateAsync(modulePath, moduleName, records, roles);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
