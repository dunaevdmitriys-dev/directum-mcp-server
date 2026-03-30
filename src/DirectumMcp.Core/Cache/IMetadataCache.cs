using DirectumMcp.Core.Models;

namespace DirectumMcp.Core.Cache;

/// <summary>
/// Cached access to parsed .mtd metadata files.
/// Implementations must be thread-safe (registered as Singleton).
/// </summary>
public interface IMetadataCache
{
    /// <summary>Get all entity metadata from solution. Cached after first call.</summary>
    Task<IReadOnlyList<EntityMetadata>> GetAllEntitiesAsync(CancellationToken ct = default);

    /// <summary>Get all module metadata from solution. Cached after first call.</summary>
    Task<IReadOnlyList<ModuleMetadata>> GetAllModulesAsync(CancellationToken ct = default);

    /// <summary>Find entity by name (case-insensitive).</summary>
    Task<EntityMetadata?> FindEntityAsync(string name, CancellationToken ct = default);

    /// <summary>Find entity by NameGuid.</summary>
    Task<EntityMetadata?> FindEntityByGuidAsync(string guid, CancellationToken ct = default);

    /// <summary>Find module by name (case-insensitive).</summary>
    Task<ModuleMetadata?> FindModuleAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Search entities and modules by query (name, GUID prefix, property name).
    /// Returns file paths with brief descriptions.
    /// </summary>
    Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default);

    /// <summary>Invalidate cache for a specific file, or all if null.</summary>
    void Invalidate(string? filePath = null);

    /// <summary>Number of cached entities.</summary>
    int CachedEntityCount { get; }

    /// <summary>Number of cached modules.</summary>
    int CachedModuleCount { get; }
}

public sealed record MetadataSearchResult
{
    public required string FilePath { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; } // "Entity", "Module"
    public string? NameGuid { get; init; }
    public string? BaseGuid { get; init; }
    public int PropertyCount { get; init; }
}
