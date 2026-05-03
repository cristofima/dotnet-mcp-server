using McpServer.BackendApi.Data;
using McpServer.BackendApi.Extensions;
using McpServer.BackendApi.Filters;
using McpServer.BackendApi.Services;
using McpServer.BackendApi.Telemetry;
using McpServer.BackendApi.Configuration;
using McpServer.Shared.Configuration;
using McpServer.ServiceDefaults.Extensions;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Information("Starting MockAPI...");

var builder = WebApplication.CreateBuilder(args);

// Add service defaults with MockAPI-specific telemetry sources
builder.AddServiceDefaults(telemetry =>
{
    telemetry.ActivitySourceNames.Add(ApiActivitySource.Name);
    telemetry.MeterNames.Add(ApiMetrics.MeterName);
});

builder.Host.AddSerilogDefaults();

// Configure options with validation
builder.Services.AddOptions<EntraIdApiOptions>()
    .Bind(builder.Configuration.GetSection(EntraIdBaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure services
builder.Services.AddControllers(options =>
{
    // Global telemetry filter — auto-instruments all controller actions
    options.Filters.Add<ApiTelemetryFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();

// Configure EF Core with SQL Server provider
builder.Services.AddDbContext<MockApiDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Seed demo data at startup before the app starts serving requests
builder.Services.AddHostedService<DatabaseSeedingService>();

// Register infrastructure services
builder.Services.AddSingleton(TimeProvider.System);

// Register application services
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IBalanceService, BalanceService>();

var app = builder.Build();

// Configure middleware pipeline
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map default health check endpoints (/health, /alive)
app.MapDefaultEndpoints();

Log.Information("MockAPI started successfully");

await app.RunAsync();
