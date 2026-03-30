using System.Diagnostics;
using DirectumMcp.Core.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DirectumMcp.Shared;

/// <summary>
/// Common MCP server setup: telemetry filter, path guard filter, error handling.
/// Each v2 server calls AddDirectumFilters() in its Program.cs.
/// </summary>
public static class ServerSetup
{
    /// <summary>
    /// Register standard cross-cutting filters for all Directum MCP servers.
    /// - Telemetry: logs tool name, duration, success/error to stderr
    /// - PathGuard: blocks any path argument outside allowed directories
    /// - ErrorHandler: catches unhandled exceptions, returns isError=true
    /// </summary>
    public static IMcpServerBuilder AddDirectumFilters(this IMcpServerBuilder builder)
    {
        return builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next => async (context, ct) =>
            {
                var toolName = context.Params?.Name ?? "unknown";
                var sw = Stopwatch.StartNew();

                try
                {
                    // PathGuard: validate path-like arguments
                    if (context.Params?.Arguments is { } args)
                    {
                        foreach (var (key, value) in args)
                        {
                            if (!IsPathLikeParam(key)) continue;

                            var path = value.ToString();
                            if (!string.IsNullOrEmpty(path) && !PathGuard.IsAllowed(path))
                            {
                                Console.Error.WriteLine($"[BLOCKED] {toolName}: path denied: {path}");
                                return ToolHelpers.Fail(PathGuard.DenyMessage(path));
                            }
                        }
                    }

                    var result = await next(context, ct);

                    Console.Error.WriteLine(
                        $"[OK] {toolName} ({sw.ElapsedMilliseconds}ms)");

                    return result;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine($"[CANCELLED] {toolName} ({sw.ElapsedMilliseconds}ms)");
                    return ToolHelpers.Fail($"Operation cancelled after {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[ERROR] {toolName} ({sw.ElapsedMilliseconds}ms): {ex.GetType().Name}: {ex.Message}");

                    return ToolHelpers.Fail($"Internal error in {toolName}: {ex.Message}");
                }
            });
        });
    }

    private static bool IsPathLikeParam(string key) =>
        key.Contains("path", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("output", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("directory", StringComparison.OrdinalIgnoreCase);
}
