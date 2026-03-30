using System.Text.Json;

namespace DirectumMcp.Core.Helpers;

/// <summary>
/// Shared constants and helpers for Directum RX metadata processing.
/// </summary>
public static class DirectumConstants
{
    /// <summary>
    /// Well-known base type GUIDs mapped to human-readable type names.
    /// </summary>
    public static readonly Dictionary<string, string> KnownBaseGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["04581d26-0780-4cfd-b3cd-c2cafc5798b0"] = "DatabookEntry",
        ["58cca102-1e97-4f07-b6ac-fd866a8b7cb1"] = "Document",
        ["d795d1f6-45c1-4e5e-9677-b53fb7280c7e"] = "Task",
        ["91cbfdc8-5d5d-465e-95a4-3a987e1a0c24"] = "Assignment",
        ["4e09273f-8b3a-489e-814e-a4ebfbba3e6c"] = "Notice",
    };

    /// <summary>
    /// Reverse lookup: type name to GUID.
    /// </summary>
    public static readonly Dictionary<string, string> BaseTypeToGuid = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DatabookEntry"] = "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
        ["Document"] = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
        ["Task"] = "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
        ["Assignment"] = "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24",
        ["Notice"] = "4e09273f-8b3a-489e-814e-a4ebfbba3e6c",
    };

    /// <summary>
    /// Resolves a base GUID to a human-readable type name, or "Unknown" if not found.
    /// </summary>
    public static string ResolveBaseType(string guid)
    {
        return KnownBaseGuids.TryGetValue(guid, out var name) ? name : "Unknown";
    }
}

/// <summary>
/// Extension methods for <see cref="JsonElement"/> to reduce boilerplate in MTD parsing.
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Gets a string property value from a JsonElement, returning empty string if missing or not a string.
    /// </summary>
    public static string GetStringProp(this JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }
}
