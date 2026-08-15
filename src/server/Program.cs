using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwBridge;
using swmcp.server.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to write to stderr (important for STDIO transport)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

// Register the MCP server and use STDIO as the transport.
// SwConnection attaches lazily and re-attaches if SolidWorks restarts,
// so registration order and SolidWorks startup timing don't matter.
builder.Services
    .AddSingleton<SwConnection>()
    .AddSingleton<DocumentManager>()
    .AddSingleton<SchemaManager>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
