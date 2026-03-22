using DirectumMcp.Core.Models;
using DirectumMcp.Core.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
