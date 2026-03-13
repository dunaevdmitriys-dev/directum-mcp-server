using System.Text.Json.Serialization;

namespace DirectumMcp.Core.Models;

/// <summary>
/// Top-level module descriptor parsed from Module.mtd.
/// </summary>
public sealed record ModuleMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("Dependencies")]
    public List<DependencyMetadata> Dependencies { get; init; } = [];

    [JsonPropertyName("ExplorerTreeOrder")]
    public int ExplorerTreeOrder { get; init; }

    [JsonPropertyName("IconName")]
    public string? IconName { get; init; }

    [JsonPropertyName("IsVisible")]
    public bool IsVisible { get; init; } = true;
}

public sealed record DependencyMetadata
{
    [JsonPropertyName("Id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("IsSolutionModule")]
    public bool IsSolutionModule { get; init; }

    [JsonPropertyName("MaxVersion")]
    public string MaxVersion { get; init; } = string.Empty;

    [JsonPropertyName("MinVersion")]
    public string MinVersion { get; init; } = string.Empty;
}

/// <summary>
/// Entity descriptor parsed from Entity.mtd files.
/// </summary>
public sealed record EntityMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("BaseGuid")]
    public string? BaseGuid { get; init; }

    [JsonPropertyName("IsAbstract")]
    public bool IsAbstract { get; init; }

    [JsonPropertyName("IsVisible")]
    public bool IsVisible { get; init; } = true;

    [JsonPropertyName("Properties")]
    public List<PropertyMetadata> Properties { get; init; } = [];

    [JsonPropertyName("Actions")]
    public List<ActionMetadata> Actions { get; init; } = [];

    [JsonPropertyName("RibbonCardMetadata")]
    public RibbonMetadata? RibbonCardMetadata { get; init; }

    [JsonPropertyName("RibbonCollectionMetadata")]
    public RibbonMetadata? RibbonCollectionMetadata { get; init; }

    [JsonPropertyName("OperationsClass")]
    public string? OperationsClass { get; init; }

    [JsonPropertyName("EntityGroup")]
    public string? EntityGroup { get; init; }
}

/// <summary>
/// Property descriptor within an entity.
/// </summary>
public sealed record PropertyMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("Code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("IsRequired")]
    public bool IsRequired { get; init; }

    [JsonPropertyName("IsVisibleInCollectionByDefault")]
    public bool IsVisibleInCollectionByDefault { get; init; }

    [JsonPropertyName("IsVisibleInFolderByDefault")]
    public bool IsVisibleInFolderByDefault { get; init; }

    [JsonPropertyName("$type")]
    public string? PropertyType { get; init; }

    [JsonPropertyName("EntityGuid")]
    public string? EntityGuid { get; init; }

    [JsonPropertyName("Length")]
    public int? Length { get; init; }
}

/// <summary>
/// Action descriptor within an entity.
/// </summary>
public sealed record ActionMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("IsAncestorMetadata")]
    public bool IsAncestorMetadata { get; init; }

    [JsonPropertyName("GenerateHandler")]
    public bool GenerateHandler { get; init; }
}

/// <summary>
/// Ribbon UI metadata.
/// </summary>
public sealed record RibbonMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Groups")]
    public List<RibbonGroupMetadata> Groups { get; init; } = [];
}

public sealed record RibbonGroupMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Elements")]
    public List<RibbonElementMetadata> Elements { get; init; } = [];
}

public sealed record RibbonElementMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("ActionGuid")]
    public string? ActionGuid { get; init; }
}

/// <summary>
/// Cover (module landing page) metadata from Module.mtd.
/// </summary>
public sealed record CoverMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Groups")]
    public List<CoverGroupMetadata> Groups { get; init; } = [];
}

public sealed record CoverGroupMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("Actions")]
    public List<CoverActionMetadata> Actions { get; init; } = [];
}

public sealed record CoverActionMetadata
{
    [JsonPropertyName("NameGuid")]
    public string NameGuid { get; init; } = string.Empty;

    [JsonPropertyName("Name")]
    public string? Name { get; init; }

    [JsonPropertyName("$type")]
    public string? ActionType { get; init; }

    [JsonPropertyName("FunctionName")]
    public string? FunctionName { get; init; }

    [JsonPropertyName("EntityGuid")]
    public string? EntityGuid { get; init; }
}
