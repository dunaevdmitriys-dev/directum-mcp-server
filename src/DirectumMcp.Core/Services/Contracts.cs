using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Base result for all service operations. Typed results inherit this.
/// Pipeline uses this as common return type.
/// </summary>
public abstract record ServiceResult
{
    public bool Success { get; init; } = true;
    public List<string> Errors { get; init; } = [];
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// Formats result as Markdown for MCP tool output.
    /// </summary>
    public abstract string ToMarkdown();

    /// <summary>
    /// Serializes result to JSON for pipeline $prev/$steps[] resolution.
    /// </summary>
    public string ToJson()
    {
        return JsonSerializer.Serialize(this, GetType(), JsonOptions);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

/// <summary>
/// Interface for services that can be called from pipeline.
/// Each service registers with a tool name.
/// </summary>
public interface IPipelineStep
{
    string ToolName { get; }

    Task<ServiceResult> ExecuteAsync(
        Dictionary<string, JsonElement> parameters,
        CancellationToken ct = default);
}
