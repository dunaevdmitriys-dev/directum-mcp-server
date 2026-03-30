using System.Globalization;
using System.Text.Json;

namespace DirectumMcp.Core.Helpers;

public static class ODataHelpers
{
    public static string EscapeOData(string value) => value.Replace("'", "''");

    public static List<JsonElement> GetItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().ToList();
        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().ToList();
        return new List<JsonElement>();
    }

    public static string GetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind != JsonValueKind.Null)
            return prop.ToString();
        return "-";
    }

    public static string GetNestedString(JsonElement element, string objectName, string propertyName)
    {
        if (element.TryGetProperty(objectName, out var prop) &&
            prop.ValueKind == JsonValueKind.Object &&
            prop.TryGetProperty(propertyName, out var val) &&
            val.ValueKind != JsonValueKind.Null)
            return val.ToString();
        return "-";
    }

    public static string FormatDate(string? isoDate, string format = "dd.MM.yyyy")
    {
        if (string.IsNullOrEmpty(isoDate) || isoDate == "-") return "-";
        if (DateTime.TryParse(isoDate, out var dt))
            return dt.ToString(format, CultureInfo.InvariantCulture);
        return isoDate;
    }

    public static long GetLong(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
                return prop.GetInt64();
            if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out var val))
                return val;
        }
        return 0;
    }
}
