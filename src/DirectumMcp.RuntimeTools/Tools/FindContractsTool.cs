using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class FindContractsTool
{
    private readonly DirectumODataClient _client;

    public FindContractsTool(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "find_contracts")]
    [Description("Поиск договоров в Directum RX по контрагенту, сумме, сроку действия, статусу согласования")]
    public async Task<string> Search(
        [Description("Текст для поиска в названии договора")] string? query = null,
        [Description("Имя контрагента (поиск по contains)")] string? counterparty = null,
        [Description("Минимальная сумма договора")] decimal? amountFrom = null,
        [Description("Максимальная сумма договора")] decimal? amountTo = null,
        [Description("Действует от (yyyy-MM-dd)")] string? validFrom = null,
        [Description("Действует до (yyyy-MM-dd)")] string? validTill = null,
        [Description("Статус жизненного цикла: Draft, Active, Obsolete")] string? status = null,
        [Description("Статус согласования: OnApproval, PendingSign, Signed")] string? approvalState = null,
        [Description("Только просроченные (с истекшим сроком действия)")] bool expired = false,
        [Description("Только рамочные договоры")] bool frameworkOnly = false,
        [Description("Максимальное количество результатов")] int top = 20)
    {
        top = Math.Clamp(top, 1, 100);

        try
        {
            var filters = BuildFilters(query, counterparty, amountFrom, amountTo,
                validFrom, validTill, status, approvalState, expired, frameworkOnly);

            var filter = filters.Count > 0 ? string.Join(" and ", filters) : null;

            var result = await _client.GetAsync("IContracts",
                filter: filter,
                select: "Id,Name,TotalAmount,ValidFrom,ValidTill,RegistrationNumber,RegistrationDate,Subject,Note,LifeCycleState,InternalApprovalState,ExternalApprovalState,IsFrameworkContract",
                expand: "Counterparty,ResponsibleEmployee,OurSignatory",
                orderby: "Created desc",
                top: top);

            return FormatResults(result, filter);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}";
        }
    }

    private static List<string> BuildFilters(string? query, string? counterparty,
        decimal? amountFrom, decimal? amountTo, string? validFrom, string? validTill,
        string? status, string? approvalState, bool expired, bool frameworkOnly)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(query))
            filters.Add($"contains(Name, '{EscapeOData(query)}')");

        if (!string.IsNullOrWhiteSpace(counterparty))
            filters.Add($"Counterparty/Name ne null and contains(Counterparty/Name, '{EscapeOData(counterparty)}')");

        if (amountFrom.HasValue)
            filters.Add($"TotalAmount ge {amountFrom.Value.ToString(CultureInfo.InvariantCulture)}");

        if (amountTo.HasValue)
            filters.Add($"TotalAmount le {amountTo.Value.ToString(CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(validFrom))
            filters.Add($"ValidFrom ge {validFrom}T00:00:00Z");

        if (!string.IsNullOrWhiteSpace(validTill))
            filters.Add($"ValidTill le {validTill}T23:59:59Z");

        if (!string.IsNullOrWhiteSpace(status))
            filters.Add($"LifeCycleState eq '{EscapeOData(status)}'");

        if (!string.IsNullOrWhiteSpace(approvalState))
            filters.Add($"InternalApprovalState eq '{EscapeOData(approvalState)}'");

        if (expired)
            filters.Add($"ValidTill lt {DateTime.UtcNow:yyyy-MM-dd}T00:00:00Z");

        if (frameworkOnly)
            filters.Add("IsFrameworkContract eq true");

        return filters;
    }

    private static string FormatResults(JsonElement result, string? filter)
    {
        var items = GetItems(result);
        if (items.Count == 0)
        {
            var sb2 = new StringBuilder();
            sb2.AppendLine("Договоры не найдены.");
            if (filter is not null) sb2.AppendLine($"_Фильтр: {filter}_");
            return sb2.ToString();
        }

        var sb = new StringBuilder();
        sb.AppendLine($"**Договоры** ({items.Count} найдено)");
        if (filter is not null) sb.AppendLine($"_Фильтр: {filter}_");
        sb.AppendLine();

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var name = GetString(item, "Name");
            var regNum = GetString(item, "RegistrationNumber");
            var regDate = FormatDate(GetString(item, "RegistrationDate"));
            var cp = GetNestedString(item, "Counterparty", "Name");
            var responsible = GetNestedString(item, "ResponsibleEmployee", "Name");
            var signatory = GetNestedString(item, "OurSignatory", "Name");
            var subject = GetString(item, "Subject");
            var amount = FormatAmount(item);
            var validFrom = FormatDate(GetString(item, "ValidFrom"), "dd.MM.yyyy");
            var validTill = FormatDate(GetString(item, "ValidTill"), "dd.MM.yyyy");
            var state = GetString(item, "LifeCycleState");
            var approval = GetString(item, "InternalApprovalState");
            var extApproval = GetString(item, "ExternalApprovalState");
            var isFramework = GetString(item, "IsFrameworkContract") == "True" ? "Да" : "Нет";

            sb.AppendLine($"### Договор #{id}");
            sb.AppendLine($"**{name}**");
            sb.AppendLine();
            sb.AppendLine($"| Параметр | Значение |");
            sb.AppendLine($"|---|---|");
            if (regNum != "-") sb.AppendLine($"| Рег. номер | {regNum} от {regDate} |");
            sb.AppendLine($"| Контрагент | {cp} |");
            sb.AppendLine($"| Предмет | {subject} |");
            sb.AppendLine($"| Сумма | {amount} |");
            sb.AppendLine($"| Срок действия | {validFrom} — {validTill} |");
            sb.AppendLine($"| Статус | {state} |");
            sb.AppendLine($"| Согласование | внутр: {approval}, внеш: {extApproval} |");
            sb.AppendLine($"| Ответственный | {responsible} |");
            sb.AppendLine($"| Подписант | {signatory} |");
            sb.AppendLine($"| Рамочный | {isFramework} |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string FormatAmount(JsonElement item)
    {
        if (item.TryGetProperty("TotalAmount", out var amt) && amt.ValueKind == JsonValueKind.Number)
            return amt.GetDecimal().ToString("N2", new CultureInfo("ru-RU")) + " руб.";
        return "-";
    }
}
