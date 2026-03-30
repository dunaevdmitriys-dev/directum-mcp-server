using System.ComponentModel;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class PreviewTools
{
    private readonly PreviewCardService _service = new();

    [McpServerTool(Name = "preview_card")]
    [Description("Предпросмотр карточки сущности: свойства, подписи, раскладка формы, обязательные поля. Без импорта в DDS.")]
    public async Task<string> PreviewCard(
        [Description("Путь к .mtd файлу сущности или к директории с .mtd файлами")] string entityPath)
    {
        var result = await _service.PreviewAsync(entityPath);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
