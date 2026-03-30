using System.Text.Json;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Protocol;

namespace DirectumMcp.Shared;

/// <summary>
/// Helper methods for building MCP tool responses.
/// In SDK v1.1.0, tools can return: string, ContentBlock, IEnumerable&lt;ContentBlock&gt;, or CallToolResult.
/// These helpers build CallToolResult for full control (isError, structuredContent).
/// </summary>
public static class ToolHelpers
{
    /// <summary>
    /// Success response with text content.
    /// </summary>
    public static CallToolResult Ok(string text) =>
        new()
        {
            Content = [new TextContentBlock { Text = text }]
        };

    /// <summary>
    /// Success response with text + structured JSON content.
    /// </summary>
    public static CallToolResult Ok(string text, object structuredContent) =>
        new()
        {
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = JsonSerializer.SerializeToElement(structuredContent, JsonOptions)
        };

    /// <summary>
    /// Error response (isError=true). LLM sees the error and can retry/adjust.
    /// </summary>
    public static CallToolResult Fail(string message) =>
        new()
        {
            Content = [new TextContentBlock { Text = $"ERROR: {message}" }],
            IsError = true
        };

    /// <summary>
    /// Convert ServiceResult from Core services to MCP CallToolResult.
    /// </summary>
    public static CallToolResult FromService(ServiceResult result) =>
        result.Success
            ? Ok(result.ToMarkdown())
            : Fail(string.Join("\n", result.Errors));

    /// <summary>
    /// Convert ServiceResult with structured output.
    /// </summary>
    public static CallToolResult FromService(ServiceResult result, object structuredContent) =>
        result.Success
            ? Ok(result.ToMarkdown(), structuredContent)
            : Fail(string.Join("\n", result.Errors));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
