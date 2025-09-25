using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using swmcp.server.Controllers;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to write to stderr (important for STDIO transport)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

// Register the MCP server and use STDIO as the transport
builder.Services
    .AddSingleton<SolidWorksController>()
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
