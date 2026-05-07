using McpBaseline.Application;
using McpBaseline.Infrastructure.Extensions;
using McpBaseline.Infrastructure.Telemetry;
using McpBaseline.Presentation.Extensions;
using McpBaseline.ServiceDefaults.Extensions;
using Serilog;

Log.Information("Starting MCP Server...");

var builder = WebApplication.CreateBuilder(args);

// Add service defaults with MCP Server-specific telemetry sources
builder.AddServiceDefaults(telemetry =>
{
    telemetry.ActivitySourceNames.Add(McpActivitySource.Name);
    telemetry.MeterNames.Add(McpMetrics.MeterName);
});

builder.Host.AddSerilogDefaults();

// Register application layers
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();

// Configure middleware pipeline
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMcpCorrelation();
app.UseAuthorization();

// Map endpoints
app.MapWellKnownEndpoints();
app.MapMcp("/mcp").RequireAuthorization();
app.MapDefaultEndpoints();

Log.Information("MCP Server started successfully");

await app.RunAsync();
