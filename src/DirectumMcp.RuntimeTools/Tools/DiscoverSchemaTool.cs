using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class DiscoverSchemaTool
{
    private readonly DirectumODataClient _client;

    public DiscoverSchemaTool(DirectumODataClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Built-in catalog of common Directum RX OData entity sets.
    /// </summary>
    private static readonly EntityCatalogEntry[] Catalog =
    [
        new("IDocuments", "Документы", "Все документы",
            ["Id", "Name", "Created", "Modified", "Author", "LifeCycleState"],
            ["Author", "DocumentKind"]),

        new("IOfficialDocuments", "Официальные документы", "Зарегистрированные документы с номерами",
            ["Id", "Name", "Created", "Modified", "RegistrationNumber", "RegistrationDate", "DocumentDate",
             "Subject", "LifeCycleState", "RegistrationState", "InternalApprovalState"],
            ["Author", "DocumentKind", "DocumentRegister", "OurSignatory", "PreparedBy"]),

        new("IContracts", "Договоры", "Договоры с контрагентами",
            ["Id", "Name", "Created", "TotalAmount", "ValidFrom", "ValidTill", "Subject",
             "LifeCycleState", "InternalApprovalState", "ExternalApprovalState",
             "RegistrationNumber", "RegistrationDate", "IsStandard", "IsFrameworkContract"],
            ["Counterparty", "ResponsibleEmployee", "OurSignatory", "DocumentKind"]),

        new("IContractualDocuments", "Договорные документы", "Договоры и дополнительные соглашения",
            ["Id", "Name", "Created", "TotalAmount", "ValidFrom", "ValidTill",
             "LifeCycleState", "InternalApprovalState"],
            ["Counterparty", "ResponsibleEmployee"]),

        new("IAssignments", "Задания", "Все задания (на исполнении, выполненные, прерванные)",
            ["Id", "Subject", "Created", "Deadline", "Status", "Importance", "Result"],
            ["Performer", "Author", "Task"]),

        new("ISimpleTasks", "Простые задачи", "Задачи на исполнение",
            ["Id", "Subject", "Created", "Deadline", "Status", "Importance"],
            ["Author", "StartedBy"]),

        new("IEmployees", "Сотрудники", "Справочник сотрудников",
            ["Id", "Name", "TabNumber", "Phone", "Email"],
            ["Department", "JobTitle", "Person"]),

        new("IDepartments", "Подразделения", "Организационная структура",
            ["Id", "Name", "Code", "ShortName"],
            ["HeadOffice", "Manager"]),

        new("ICompanies", "Контрагенты", "Организации-контрагенты",
            ["Id", "Name", "TIN", "TRRC", "PSRN", "LegalName", "City", "Region", "LegalAddress",
             "PostalAddress", "Phones", "Email", "Homepage", "Note", "Status"],
            []),

        new("IPersons", "Персоны", "Физические лица — контакты",
            ["Id", "Name", "TIN", "Phones", "Email", "Status"],
            []),

        new("IDocumentKinds", "Виды документов", "Справочник видов документов",
            ["Id", "Name", "ShortName", "Code", "Status"],
            ["DocumentType"]),

        new("ICaseFiles", "Дела (номенклатура)", "Номенклатура дел",
            ["Id", "Name", "Index", "StartDate", "EndDate", "Note", "Status"],
            ["BusinessUnit", "Department"]),

        new("IDocumentRegisters", "Журналы регистрации", "Журналы входящих/исходящих документов",
            ["Id", "Name", "Index", "RegistrationGroup", "NumberFormatItems", "Status"],
            []),
    ];

    [McpServerTool(Name = "discover")]
    [Description("Каталог доступных сущностей Directum RX — имена, описания, ключевые поля, навигационные свойства. Используйте для построения OData-запросов.")]
    public async Task<string> Discover(
        [Description("Фильтр по имени или описанию (например: 'договор', 'сотрудник'). Пусто = показать все.")] string? query = null,
        [Description("Имя конкретного EntitySet для получения реальных полей со стенда (например: IContracts)")] string? probe = null)
    {
        // Mode 1: Probe a specific entity — fetch sample and show real properties
        if (!string.IsNullOrWhiteSpace(probe))
            return await ProbeEntity(probe);

        // Mode 2: Show catalog, optionally filtered
        return ShowCatalog(query);
    }

    private static string ShowCatalog(string? query)
    {
        var entries = Catalog.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLowerInvariant();
            entries = entries.Where(e =>
                e.RuName.ToLowerInvariant().Contains(q) ||
                e.Description.ToLowerInvariant().Contains(q) ||
                e.EntitySet.ToLowerInvariant().Contains(q));
        }

        var list = entries.ToList();
        if (list.Count == 0)
            return $"Сущности по запросу '{query}' не найдены. Используйте discover без параметров для полного списка.";

        var sb = new StringBuilder();
        sb.AppendLine($"## Каталог сущностей Directum RX ({list.Count})");
        sb.AppendLine();

        foreach (var e in list)
        {
            sb.AppendLine($"### {e.RuName} — `{e.EntitySet}`");
            sb.AppendLine(e.Description);
            sb.AppendLine();
            sb.AppendLine($"**Ключевые поля:** {string.Join(", ", e.KeyProperties)}");
            if (e.NavigationProperties.Length > 0)
                sb.AppendLine($"**Навигация ($expand):** {string.Join(", ", e.NavigationProperties)}");
            sb.AppendLine();
            sb.AppendLine($"Пример: `odata_query entity={e.EntitySet} top=5 select={string.Join(",", e.KeyProperties.Take(5))}`");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("Для получения реальных полей со стенда: `discover probe=IContracts`");
        sb.AppendLine("Для произвольных запросов: `odata_query entity=<EntitySet> filter=<OData filter>`");

        return sb.ToString();
    }

    private async Task<string> ProbeEntity(string entitySet)
    {
        try
        {
            var result = await _client.GetAsync(entitySet, top: 1);
            var items = Core.Helpers.ODataHelpers.GetItems(result);

            if (items.Count == 0)
                return $"Сущность `{entitySet}` доступна, но записей нет. Поля недоступны для анализа.";

            var sample = items[0];
            var sb = new StringBuilder();
            sb.AppendLine($"## Реальные поля `{entitySet}` (со стенда)");
            sb.AppendLine();

            var scalars = new List<(string Name, string Type, string Sample)>();
            var navs = new List<string>();

            foreach (var prop in sample.EnumerateObject())
            {
                if (prop.Name.StartsWith("@odata")) continue;

                switch (prop.Value.ValueKind)
                {
                    case JsonValueKind.Object:
                        navs.Add(prop.Name);
                        break;
                    case JsonValueKind.Array:
                        navs.Add($"{prop.Name} (коллекция)");
                        break;
                    default:
                        var type = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => "string",
                            JsonValueKind.Number => "number",
                            JsonValueKind.True or JsonValueKind.False => "bool",
                            JsonValueKind.Null => "null",
                            _ => prop.Value.ValueKind.ToString()
                        };
                        var val = prop.Value.ValueKind == JsonValueKind.Null ? "-" : prop.Value.ToString();
                        if (val.Length > 50) val = val[..47] + "...";
                        scalars.Add((prop.Name, type, val));
                        break;
                }
            }

            sb.AppendLine("### Скалярные поля");
            sb.AppendLine("| Поле | Тип | Пример значения |");
            sb.AppendLine("|---|---|---|");
            foreach (var (name, type, sampleVal) in scalars)
                sb.AppendLine($"| {name} | {type} | {sampleVal} |");

            if (navs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"### Навигационные свойства ($expand)");
                sb.AppendLine(string.Join(", ", navs.Select(n => $"`{n}`")));
            }

            // Show catalog description if available
            var catalogEntry = Catalog.FirstOrDefault(c =>
                c.EntitySet.Equals(entitySet, StringComparison.OrdinalIgnoreCase));
            if (catalogEntry is not null)
            {
                sb.AppendLine();
                sb.AppendLine($"> **{catalogEntry.RuName}**: {catalogEntry.Description}");
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА** при обращении к `{entitySet}`: {ex.Message}\n\nВозможно, EntitySet не существует. Используйте `discover` без параметров для списка известных сущностей.";
        }
    }

    private sealed record EntityCatalogEntry(
        string EntitySet,
        string RuName,
        string Description,
        string[] KeyProperties,
        string[] NavigationProperties);
}
