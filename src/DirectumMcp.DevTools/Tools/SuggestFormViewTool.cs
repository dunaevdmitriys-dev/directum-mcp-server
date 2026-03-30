using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class SuggestFormViewTool
{
    [McpServerTool(Name = "suggest_form_view")]
    [Description("Предложить FormView JSON для многоформенности сущности: разные наборы полей для разных ролей/условий.")]
    public async Task<string> SuggestFormView(
        [Description("Путь к .mtd файлу сущности")] string entityPath,
        [Description("Сценарии через точку с запятой: 'Manager:Name,Amount,Status;Accountant:Name,Amount,Account,TIN'")] string scenarios = "")
    {
        if (!PathGuard.IsAllowed(entityPath))
            return PathGuard.DenyMessage(entityPath);

        if (!File.Exists(entityPath))
            return $"**ОШИБКА**: Файл не найден: `{entityPath}`";

        var json = await File.ReadAllTextAsync(entityPath);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch { return "**ОШИБКА**: Невалидный JSON в .mtd"; }

        using (doc)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";

            // Get all properties
            var allProps = new List<string>();
            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props.EnumerateArray())
                {
                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(propName))
                        allProps.Add(propName);
                }
            }

            var parsedScenarios = ParseScenarios(scenarios, allProps);

            var sb = new StringBuilder();
            sb.AppendLine($"# Многоформенность для {entityName}");
            sb.AppendLine();
            sb.AppendLine($"**Всего свойств:** {allProps.Count}");
            sb.AppendLine($"**Свойства:** {string.Join(", ", allProps)}");
            sb.AppendLine();

            if (parsedScenarios.Count == 0)
            {
                // Auto-suggest based on property count
                sb.AppendLine("## Предложение (автоматическое)");
                sb.AppendLine();

                if (allProps.Count <= 5)
                {
                    sb.AppendLine("Мало свойств — многоформенность не нужна. Используйте одну форму.");
                }
                else
                {
                    sb.AppendLine("### Вариант 1: Основная + Расширенная");
                    var half = allProps.Count / 2;
                    sb.AppendLine($"- **Основная:** {string.Join(", ", allProps.Take(half))}");
                    sb.AppendLine($"- **Расширенная:** {string.Join(", ", allProps)}");
                    sb.AppendLine();
                    sb.AppendLine("### Вариант 2: По ролям");
                    sb.AppendLine($"- **Менеджер:** {string.Join(", ", allProps.Where(p => !p.Contains("Account") && !p.Contains("TIN")))}");
                    sb.AppendLine($"- **Бухгалтер:** {string.Join(", ", allProps.Where(p => !p.Contains("Status") && !p.Contains("Stage")))}");
                }
            }
            else
            {
                sb.AppendLine("## Сценарии");
                sb.AppendLine();

                foreach (var (scenarioName, visibleProps) in parsedScenarios)
                {
                    var hiddenProps = allProps.Except(visibleProps).ToList();

                    sb.AppendLine($"### {scenarioName}");
                    sb.AppendLine($"**Видимые ({visibleProps.Count}):** {string.Join(", ", visibleProps)}");
                    sb.AppendLine($"**Скрытые ({hiddenProps.Count}):** {string.Join(", ", hiddenProps)}");
                    sb.AppendLine();
                }
            }

            // FormView JSON suggestion
            sb.AppendLine("## Реализация");
            sb.AppendLine();
            sb.AppendLine("### Вариант A: Через HandledEvents (Showing/Refresh)");
            sb.AppendLine("```csharp");
            sb.AppendLine("public override void Showing(ShowingEventArgs e)");
            sb.AppendLine("{");
            sb.AppendLine("    // Скрыть поля по роли");
            sb.AppendLine("    if (!Users.Current.IncludedIn(Constants.Module.AccountantRole))");
            sb.AppendLine("    {");
            sb.AppendLine("        _obj.State.Properties.Account.IsVisible = false;");
            sb.AppendLine("        _obj.State.Properties.TIN.IsVisible = false;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("### Вариант B: Через условную видимость в Controls");
            sb.AppendLine("Добавить HandledEvents: ShowingClient в .mtd сущности.");
            sb.AppendLine("В обработчике управлять `_obj.State.Properties.<Name>.IsVisible`.");
            sb.AppendLine();
            sb.AppendLine("### Вариант C: Разные формы (Forms[])");
            sb.AppendLine("Несколько StandaloneFormMetadata в Forms[] с разным набором Controls.");
            sb.AppendLine("Переключение через Action или программно.");

            return sb.ToString();
        }
    }

    private static List<(string Name, List<string> Properties)> ParseScenarios(string scenarios, List<string> allProps)
    {
        var result = new List<(string, List<string>)>();
        if (string.IsNullOrWhiteSpace(scenarios)) return result;

        foreach (var part in scenarios.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx <= 0) continue;

            var name = part[..colonIdx].Trim();
            var props = part[(colonIdx + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => allProps.Contains(p, StringComparer.OrdinalIgnoreCase) || allProps.Any(ap => ap.Equals(p, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            result.Add((name, props));
        }
        return result;
    }
}
