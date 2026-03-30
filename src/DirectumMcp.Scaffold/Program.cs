using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var config = SolutionPathConfig.FromEnvironment();
builder.Services.AddSingleton(config);

// Core services used by scaffold tools
builder.Services.AddSingleton<EntityScaffoldService>();
builder.Services.AddSingleton<ModuleScaffoldService>();
builder.Services.AddSingleton<FunctionScaffoldService>();
builder.Services.AddSingleton<JobScaffoldService>();

// MCP server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "directum-scaffold", Version = "2.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .AddDirectumFilters();

await builder.Build().RunAsync();
