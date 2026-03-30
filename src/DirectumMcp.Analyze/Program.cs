using DirectumMcp.Core.Cache;
using DirectumMcp.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var config = SolutionPathConfig.FromEnvironment();
builder.Services.AddSingleton(config);

// MetadataCache — LRU cache for parsed .mtd files
builder.Services.AddSingleton<IMetadataCache>(new MetadataCache(config.Path));

// MCP server
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "directum-analyze", Version = "2.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .AddDirectumFilters();

await builder.Build().RunAsync();
