using System.Text.Json;
using DirectumMcp.Core.Models;

namespace DirectumMcp.Core.Parsers;

/// <summary>
/// Parses Directum RX .mtd (JSON) metadata files into strongly-typed models.
/// </summary>
public static class MtdParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Parses a Module.mtd file into <see cref="ModuleMetadata"/>.
    /// </summary>
    public static async Task<ModuleMetadata> ParseModuleAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<ModuleMetadata>(stream, JsonOptions, ct)
               ?? throw new InvalidOperationException($"Failed to deserialize module metadata from {filePath}");
    }

    /// <summary>
    /// Parses an entity .mtd file into <see cref="EntityMetadata"/>.
    /// </summary>
    public static async Task<EntityMetadata> ParseEntityAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<EntityMetadata>(stream, JsonOptions, ct)
               ?? throw new InvalidOperationException($"Failed to deserialize entity metadata from {filePath}");
    }

    /// <summary>
    /// Parses raw .mtd JSON into a <see cref="JsonDocument"/> for ad-hoc queries.
    /// </summary>
    public static async Task<JsonDocument> ParseRawAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = File.OpenRead(filePath);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    /// <summary>
    /// Synchronously parses a module .mtd from a JSON string.
    /// </summary>
    public static ModuleMetadata ParseModuleFromString(string json)
    {
        return JsonSerializer.Deserialize<ModuleMetadata>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize module metadata from string.");
    }

    /// <summary>
    /// Synchronously parses an entity .mtd from a JSON string.
    /// </summary>
    public static EntityMetadata ParseEntityFromString(string json)
    {
        return JsonSerializer.Deserialize<EntityMetadata>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize entity metadata from string.");
    }
}
