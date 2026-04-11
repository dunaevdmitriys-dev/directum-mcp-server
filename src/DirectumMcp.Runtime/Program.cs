using DirectumMcp.Core.Models;
using DirectumMcp.Core.OData;
using DirectumMcp.Shared;
using Microsoft.Extensions.DependencyInjection;

// Dual-mode: stdio (default for Claude Code) or HTTP (for remote access)
var useHttp = args.Contains("--http");
var port = int.TryParse(Environment.GetEnvironmentVariable("RUNTIME_MCP_PORT"), out var envPort) ? envPort : 3001;

var portArg = args.FirstOrDefault(a => a.StartsWith("--port="));
if (portArg != null && int.TryParse(portArg.Split('=')[1], out var parsedPort))
    port = parsedPort;

if (useHttp)
{
    var builder = WebApplication.CreateBuilder(args);

    var config = DirectumConfig.FromEnvironment();
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton<DirectumODataClient>();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "directum-runtime", Version = "2.0.0" };
        })
        .WithHttpTransport()
        .WithToolsFromAssembly()
        .AddDirectumFilters();

    var app = builder.Build();

    app.MapGet("/health", () => Results.Ok(new
    {
        status = "healthy",
        server = "directum-runtime",
        version = "2.0.0",
        transport = "http",
        timestamp = DateTime.UtcNow
    }));

    app.MapMcp("/mcp");

    Console.Error.WriteLine($"directum-runtime v2.0.0 HTTP mode on port {port}");
    app.Run($"http://0.0.0.0:{port}");
}
else
{
    var builder = Host.CreateApplicationBuilder(args);

    var config = DirectumConfig.FromEnvironment();
    builder.Services.AddSingleton(config);
    builder.Services.AddSingleton<DirectumODataClient>();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new() { Name = "directum-runtime", Version = "2.0.0" };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly()
        .AddDirectumFilters();

    await builder.Build().RunAsync();
}
