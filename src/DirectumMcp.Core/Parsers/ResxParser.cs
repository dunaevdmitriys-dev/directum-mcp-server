using System.Xml.Linq;

namespace DirectumMcp.Core.Parsers;

/// <summary>
/// Parses and validates Directum RX .resx resource files.
/// </summary>
public static class ResxParser
{
    /// <summary>
    /// Reads all key-value pairs from a .resx file.
    /// </summary>
    public static async Task<Dictionary<string, string>> ParseAsync(string filePath, CancellationToken ct = default)
    {
        var doc = await LoadXDocumentAsync(filePath, ct);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var data in doc.Descendants("data"))
        {
            var name = data.Attribute("name")?.Value;
            var value = data.Element("value")?.Value;
            if (name is not null)
            {
                result[name] = value ?? string.Empty;
            }
        }

        return result;
    }

    /// <summary>
    /// Validates that System.resx keys follow the platform convention (Property_X, not Resource_GUID).
    /// Returns list of invalid keys that use Resource_GUID format.
    /// </summary>
    public static async Task<List<ResxKeyIssue>> ValidateSystemResxAsync(string filePath, CancellationToken ct = default)
    {
        var entries = await ParseAsync(filePath, ct);
        var issues = new List<ResxKeyIssue>();

        foreach (var (key, value) in entries)
        {
            if (key.StartsWith("Resource_", StringComparison.Ordinal) && IsGuidSuffix(key.AsSpan(9)))
            {
                issues.Add(new ResxKeyIssue(key, value, filePath,
                    "Key uses Resource_<GUID> format instead of Property_<Name>. " +
                    "Runtime will not resolve property labels correctly."));
            }
        }

        return issues;
    }

    /// <summary>
    /// Finds all property label keys in System.resx.
    /// </summary>
    public static async Task<List<string>> GetPropertyKeysAsync(string filePath, CancellationToken ct = default)
    {
        var entries = await ParseAsync(filePath, ct);
        return entries.Keys
            .Where(k => k.StartsWith("Property_", StringComparison.Ordinal))
            .ToList();
    }

    private static bool IsGuidSuffix(ReadOnlySpan<char> span)
    {
        // Check if the suffix looks like a GUID (32 hex chars with optional hyphens)
        return span.Length >= 32 && Guid.TryParse(span, out _);
    }

    private static async Task<XDocument> LoadXDocumentAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        return await XDocument.LoadAsync(stream, LoadOptions.None, ct);
    }
}

/// <summary>
/// Represents a validation issue found in a .resx file.
/// </summary>
public sealed record ResxKeyIssue(
    string Key,
    string Value,
    string FilePath,
    string Message
);
