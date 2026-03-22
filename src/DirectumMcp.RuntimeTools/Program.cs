using DirectumMcp.Core.Models;
using DirectumMcp.Core.OData;
using Microsoft.Extensions.DependencyInjection;

// Dual-mode: stdio (default for Claude Code) or HTTP (for Telegram/Cowork/OpenClaw)
var useHttp = args.Contains("--http");
var port = 3001;

// Parse --port=XXXX
var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
if (portArg != null && int.TryParse(portArg.Split('=')[1], out var parsedPort))
    port = parsedPort;

if (useHttp)
{
    // === HTTP MODE (Streamable HTTP for Telegram/Cowork/OpenClaw) ===
    var builder = WebApplication.CreateBuilder(args);

    var config = DirectumConfig.FromEnvironment();
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton<DirectumODataClient>();

    builder.Services
        .AddMcpServer()
        .WithToolsFromAssembly(typeof(Program).Assembly)
        .WithResourcesFromAssembly(typeof(Program).Assembly)
        .WithPromptsFromAssembly(typeof(Program).Assembly);

    var app = builder.Build();

    // API Key middleware (optional)
    var apiKeys = Environment.GetEnvironmentVariable("MCP_API_KEYS")?.Split(',', StringSplitOptions.RemoveEmptyEntries);
    if (apiKeys is { Length: > 0 })
    {
        app.Use(async (context, next) =>
        {
            // Skip health check
            if (context.Request.Path == "/health")
            {
                await next();
                return;
            }

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader == null || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Missing or invalid Authorization header. Use: Bearer <api-key>");
                return;
            }

            var token = authHeader["Bearer ".Length..];
            if (!apiKeys.Contains(token))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Invalid API key");
                return;
            }

            await next();
        });
    }

    // Health check endpoint
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        server = "DirectumMcp.RuntimeTools",
        tools = 30,
        transport = "http",
        timestamp = DateTime.UtcNow
    }));

    // MCP endpoint (Streamable HTTP)
    app.MapMcp("/mcp");

    Console.Error.WriteLine($"DirectumMcp.RuntimeTools running in HTTP mode on port {port}");
    Console.Error.WriteLine($"MCP endpoint: http://localhost:{port}/mcp");
    Console.Error.WriteLine($"Health check: http://localhost:{port}/health");
    Console.Error.WriteLine($"API key auth: {(apiKeys is { Length: > 0 } ? "enabled" : "disabled (set MCP_API_KEYS)")}");

    app.Run($"http://0.0.0.0:{port}");
}
else
{
    // === STDIO MODE (default, for Claude Code) ===
    var builder = Host.CreateApplicationBuilder(args);

    var config = DirectumConfig.FromEnvironment();
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton<DirectumODataClient>();

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(Program).Assembly)
        .WithResourcesFromAssembly(typeof(Program).Assembly)
        .WithPromptsFromAssembly(typeof(Program).Assembly);

    var app = builder.Build();
    await app.RunAsync();
}
