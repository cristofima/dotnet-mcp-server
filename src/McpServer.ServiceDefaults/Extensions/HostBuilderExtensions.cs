using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace McpServer.ServiceDefaults.Extensions;

// Configures Serilog with console and file sinks for IHostBuilder, integrating with OpenTelemetry via writeToProviders:true.
// See https://aka.ms/dotnet/aspire/service-defaults
public static class HostBuilderExtensions
{
    extension(IHostBuilder host)
    {
        /// <summary>
        /// Configures Serilog with console and file sinks, integrating with OpenTelemetry for centralized logging.
        /// </summary>
        /// <remarks>
        /// Log files: <c>logs/{assembly-name}-{date}.log</c> with daily rolling and 5-file retention.
        /// See README for detailed configuration.
        /// </remarks>
        /// <returns>The configured host builder for method chaining.</returns>
        public void AddSerilogDefaults()
        {
            host.UseSerilog((context, configuration) =>
            {
                // Generate log path from assembly name
                var assemblyName = context.HostingEnvironment.ApplicationName;
#pragma warning disable CA1308 // Lowercase is appropriate for file paths
                var sanitizedName = assemblyName.ToLowerInvariant().Replace(".", "-", StringComparison.Ordinal);
#pragma warning restore CA1308

                // In Azure App Service (Windows or Linux), write to %HOME%\LogFiles\dotnet\ so the
                // Datadog extension/agent can tail the files. HOME resolves to D:\home on Windows
                // and /home on Linux. In local development, write to logs/ (relative to CWD).
                var logPath = context.HostingEnvironment.IsDevelopment()
                    ? $"logs/{sanitizedName}-"
                    : Path.Combine(
                        Environment.GetEnvironmentVariable("HOME") ?? "logs",
                        "LogFiles", "dotnet", $"{sanitizedName}-");

                configuration
                    .MinimumLevel.Information()
                    // General Microsoft override — catch-all at Warning
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                    // Specific overrides from NLog rules (finalMinLevel=Error)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting.Diagnostics", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Infrastructure", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerHandler", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.AspNetCore.Mvc.Infrastructure.ObjectResultExecutor", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Model.Validation", LogEventLevel.Error)
                    .MinimumLevel.Override("Microsoft.Identity.Web.TokenAcquisition", LogEventLevel.Error)
                    // EF Core DB commands: finalMinLevel=Info — keep Info+ despite general Microsoft override at Warning
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Application", context.HostingEnvironment.ApplicationName)
                    .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                    .Enrich.WithSpan()          // TraceId, SpanId, ParentId (W3C hex format)
                    .Enrich.WithMachineName()   // MachineName for multi-instance identification
                    .Enrich.WithThreadId()      // ThreadId for concurrency debugging
                    .WriteTo.Console(
                        // Mirrors NLog console layout: timestamp|level|logger|message|exception
                        // (callsite and line number are not available in Serilog output templates)
                        outputTemplate: "{Timestamp:yyyy-MM-ddTHH:mm:ss.fffzzz}|{Level:u4}|{SourceContext}|{Message:lj}|{Exception}{NewLine}")
                    .WriteTo.File(
                        formatter: new RenderedCompactJsonFormatter(),
                        path: $"{logPath}.log",
                        rollingInterval: RollingInterval.Day,
                        // archiveAboveSize=2000000 → roll also when file exceeds 2 MB
                        fileSizeLimitBytes: 2_000_000,
                        rollOnFileSizeLimit: true,
                        // maxArchiveFiles=5
                        retainedFileCountLimit: 5,
                        // autoFlush=false + openFileFlushTimeout=2 → buffer writes, flush every 2 s
                        buffered: true,
                        flushToDiskInterval: TimeSpan.FromSeconds(2));

                // Development: Include debug logs for better diagnostics
                if (context.HostingEnvironment.IsDevelopment())
                {
                    configuration.MinimumLevel.Debug();
                }
            }, writeToProviders: true); // CRITICAL: writeToProviders:true sends logs to Microsoft.Extensions.Logging (OpenTelemetry)
        }
    }
}
