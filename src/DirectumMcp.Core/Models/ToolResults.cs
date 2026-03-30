using System.Text.Json.Serialization;

namespace DirectumMcp.Core.Models;

/// <summary>
/// Structured output for scaffold tools.
/// Returned as structuredContent in MCP CallToolResult.
/// </summary>
public sealed record ScaffoldResult
{
    [JsonPropertyName("createdFiles")]
    public required string[] CreatedFiles { get; init; }

    [JsonPropertyName("warnings")]
    public string[] Warnings { get; init; } = [];

    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    [JsonPropertyName("entityName")]
    public string? EntityName { get; init; }

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }
}

/// <summary>
/// Structured output for validation tools.
/// </summary>
public sealed record ValidationResult
{
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("issues")]
    public ValidationIssue[] Issues { get; init; } = [];

    [JsonPropertyName("errorCount")]
    public int ErrorCount => Issues.Count(i => i.Severity == "error");

    [JsonPropertyName("warningCount")]
    public int WarningCount => Issues.Count(i => i.Severity == "warning");

    [JsonPropertyName("checkedPath")]
    public string? CheckedPath { get; init; }
}

/// <summary>
/// Single validation issue with actionable details.
/// </summary>
public sealed record ValidationIssue
{
    /// <summary>"error", "warning", "info"</summary>
    [JsonPropertyName("severity")]
    public required string Severity { get; init; }

    /// <summary>Machine-readable code: "GUID_MISMATCH", "MISSING_RESX", etc.</summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    /// <summary>Human-readable description.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>File where the issue was found.</summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    /// <summary>Line number (if applicable).</summary>
    [JsonPropertyName("line")]
    public int? Line { get; init; }

    /// <summary>Suggested fix description.</summary>
    [JsonPropertyName("fix")]
    public string? Fix { get; init; }
}

/// <summary>
/// Structured output for analysis tools.
/// </summary>
public sealed record AnalysisResult
{
    [JsonPropertyName("entityName")]
    public string? EntityName { get; init; }

    [JsonPropertyName("moduleName")]
    public string? ModuleName { get; init; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, object> Metrics { get; init; } = new();

    [JsonPropertyName("recommendations")]
    public string[] Recommendations { get; init; } = [];

    [JsonPropertyName("dependencies")]
    public string[] Dependencies { get; init; } = [];
}

/// <summary>
/// Structured output for build/deploy tools.
/// </summary>
public sealed record DeployResult
{
    [JsonPropertyName("status")]
    public required string Status { get; init; } // "completed", "failed"

    [JsonPropertyName("artifactPath")]
    public string? ArtifactPath { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("logs")]
    public string[] Logs { get; init; } = [];

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; init; }
}

/// <summary>
/// Structured output for metadata search.
/// </summary>
public sealed record SearchResult
{
    [JsonPropertyName("matches")]
    public required SearchMatch[] Matches { get; init; }

    [JsonPropertyName("totalFound")]
    public int TotalFound { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }
}

public sealed record SearchMatch
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; } // "Entity", "Module", "Property"

    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("nameGuid")]
    public string? NameGuid { get; init; }

    [JsonPropertyName("baseGuid")]
    public string? BaseGuid { get; init; }

    [JsonPropertyName("propertyCount")]
    public int PropertyCount { get; init; }
}
