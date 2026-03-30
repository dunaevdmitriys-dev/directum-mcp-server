using DirectumMcp.Core.Services;
using DirectumMcp.Core.Validators;
using DirectumMcp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var config = SolutionPathConfig.FromEnvironment();
builder.Services.AddSingleton(config);

// Core services used by validation tools
builder.Services.AddSingleton<PackageValidateService>();
builder.Services.AddSingleton<PackageFixService>();

// MCP server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "directum-validate", Version = "2.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .AddDirectumFilters();

await builder.Build().RunAsync();
