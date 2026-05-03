# McpServer.ServiceDefaults

Shared Aspire service defaults referenced by every service project. Configures OpenTelemetry, health checks, service discovery, and HTTP client resilience in one place.

## What it provides

- **OpenTelemetry**: traces, metrics, and logs with ASP.NET, HTTP client, EF Core, and runtime instrumentation. Custom activity sources and meters are registered per-service via `ServiceTelemetryOptions`.
- **Health checks**: services registered via `AddServiceDefaults()`, endpoints mapped via `MapDefaultEndpoints()` — `/health` (readiness) and `/alive` (liveness), always enabled with environment-aware response detail.
- **Service discovery**: `AddServiceDiscovery()` on all HTTP clients.
- **Resilience**: `AddStandardResilienceHandler()` on all HTTP clients.

## Project Structure

```
McpServer.ServiceDefaults/
├── Extensions/
│   ├── HostApplicationBuilderExtensions.cs
│   ├── WebApplicationExtensions.cs
│   └── HostBuilderExtensions.cs
├── Configuration/
│   └── ServiceTelemetryOptions.cs
└── Telemetry/
    └── HealthCheckActivityFilter.cs
```

Each extension class owns only the private static helpers it uses:

- `HostApplicationBuilderExtensions.cs` (`IHostApplicationBuilder`): registers DI services — OpenTelemetry pipeline, health check services (`AddHealthChecks`), service discovery, and HTTP client resilience. Entry point: `builder.AddServiceDefaults()`.
- `WebApplicationExtensions.cs` (`WebApplication`): maps `/health` and `/alive` HTTP endpoints into the request pipeline. Requires services registered by the above. Entry point: `app.MapDefaultEndpoints()`.
- `HostBuilderExtensions.cs` (`IHostBuilder`): configures Serilog with console and file sinks, bridged to the OTel logging provider via `writeToProviders: true`. Entry point: `builder.Host.AddSerilogDefaults()`.
- `ServiceTelemetryOptions.cs`: options model for registering custom meter names and activity source names per service.
- `HealthCheckActivityFilter.cs`: OTel `BaseProcessor<Activity>` that suppresses health probe traces from export. See [Health Check Trace Filtering](#health-check-trace-filtering).

## Health Checks

Both endpoints are **always mapped**, including production. Azure App Service health probes require a path that returns 200 OK; without one, probes hit `GET /` and generate noisy 404 traces in Application Insights.

### Endpoints

| Endpoint  | Checks                                       | Purpose                                                                  |
| --------- | -------------------------------------------- | ------------------------------------------------------------------------ |
| `/alive`  | Only `self` (liveness tag)                   | Confirms the process is running. Used by Azure App Service health probe. |
| `/health` | All registered checks (liveness + readiness) | Full readiness: Entra ID connectivity, DB, downstream APIs.              |

### Response Detail by Environment

| Environment | `/alive`               | `/health`                                               |
| ----------- | ---------------------- | ------------------------------------------------------- |
| Development | `{"status":"Healthy"}` | Detailed: per-check name, status, duration, description |
| Production  | `{"status":"Healthy"}` | Minimal: `{"status":"Healthy"}` only                    |

**Production security**: The minimal response writer returns only the aggregate status string (`Healthy`, `Degraded`, or `Unhealthy`). No internal check names (e.g., `identity-provider`), exception details, or timing data are exposed.

Both endpoints use `AllowAnonymous()` because Azure load balancers and health probes do not send authentication tokens. The response content itself is what is protected (no sensitive data in production).

### Azure App Service Configuration

Configure the health probe path so App Service uses `/alive` instead of `GET /`:

```bash
az webapp config set -n <app-name> -g <resource-group> \
  --generic-configurations "{\"healthCheckPath\": \"/alive\"}"
```

### Adding Custom Health Checks

Services register additional checks in their `Program.cs`:

```csharp
// MCP Server adds an Entra ID connectivity check (tagged "ready")
builder.Services.AddHealthChecks()
    .AddCheck<EntraIdHealthCheck>("identity-provider", tags: ["ready"]);
```

Custom checks run on `/health` (readiness) but not on `/alive` (liveness only runs `self`).

## Usage

```csharp
// In each service's Program.cs
builder.AddServiceDefaults();

// With custom telemetry (optional)
builder.AddServiceDefaults(opts =>
{
    opts.MeterNames.Add(MyMetrics.MeterName);
    opts.ActivitySourceNames.Add(MyActivitySource.Name);
});
```

```csharp
// In each service's app pipeline (WebApplication)
app.MapDefaultEndpoints();
```

## Observability: Three-Signal Architecture

### Traces & Metrics (OTel SDK)

`ConfigureOpenTelemetry()` registers the standard OpenTelemetry .NET SDK pipeline:

**Traces** (`WithTracing`):

- `AddAspNetCoreInstrumentation()`: HTTP request spans
- `AddHttpClientInstrumentation()`: outbound HTTP call spans, with infrastructure span filtering (see below)
- `AddEntityFrameworkCoreInstrumentation()`: EF Core database command spans (see below)
- `AddSource(applicationName)`: the service's own `ActivitySource`
- Custom sources registered via `ServiceTelemetryOptions.ActivitySourceNames`
- `HealthCheckActivityFilter` processor: suppresses health probe traces (see below)

#### Entity Framework Core Instrumentation

`AddEntityFrameworkCoreInstrumentation()` instruments EF Core database commands for relational providers (SQL Server, PostgreSQL, SQLite, MySQL, Oracle, etc.). The library automatically detects the database engine from the EF Core provider name and sets `db.system` / `db.system.name` accordingly: no manual tag configuration is needed.

**Auto-detected tags** (set by the library, not by custom enrichment):

| Tag (old / new)                   | Description                                   | Example value                    |
| --------------------------------- | --------------------------------------------- | -------------------------------- |
| `db.system` / `db.system.name`    | Database engine (auto-detected from provider) | `mssql` / `microsoft.sql_server` |
| `db.name` / `db.namespace`        | Database name                                 | `MockApiDb`                      |
| `peer.service` / `server.address` | Server host                                   | `localhost`                      |
| `server.port`                     | Server port (new conventions only)            | `1433`                           |
| `db.statement` / `db.query.text`  | SQL text (sanitized for SQL-like providers)   | `SELECT ... FROM ...`            |
| `db.query.summary`                | Query summary (new conventions only)          | `SELECT Items`                   |

The new semantic convention attributes (`db.system.name`, `db.query.text`, `db.query.summary`, `server.port`) are opt-in via the `OTEL_SEMCONV_STABILITY_OPT_IN` environment variable:

| Value          | Behavior                                                          |
| -------------- | ----------------------------------------------------------------- |
| _(unset)_      | Emit old attributes only (`db.system`, `db.name`, `db.statement`) |
| `database/dup` | Emit both old and new attributes (gradual transition)             |
| `database`     | Emit new attributes only                                          |

> **This must be a real OS environment variable.** The OTel SDK reads it via `System.Environment.GetEnvironmentVariable()` during SDK initialization, before the ASP.NET Core host loads `appsettings.json`. Setting it in `appsettings.json` has no effect.
>
> | Context           | How to set it                                                                                                                                                 |
> | ----------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
> | Azure App Service | Add `OTEL_SEMCONV_STABILITY_OPT_IN` = `database` under **Configuration → Application settings** — App Service injects these as process environment variables. |
> | Local via Aspire  | `.WithEnvironment("OTEL_SEMCONV_STABILITY_OPT_IN", "database")` on the project resource in `AppHost.cs`.                                                      |
> | Local direct run  | Add to `environmentVariables` in `Properties/launchSettings.json` for each profile.                                                                           |
>
> **Azure Monitor note**: In production, `UseAzureMonitor()` exports EF Core spans to Application Insights using the OTel SDK attribute schema. `OTEL_SEMCONV_STABILITY_OPT_IN` controls which attribute names appear in Application Insights: old (`db.system`, `db.name`) or new (`db.system.name`, `db.namespace`). Set it as an App Service Application setting so the SDK reads it at process startup, before `appsettings.json` is loaded.

Query parameters can be emitted via `OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS=true`, but this is experimental and may expose sensitive data.

**Safe for non-EF projects**: The instrumentation subscribes to the `Microsoft.EntityFrameworkCore` `DiagnosticSource`. If EF Core is not referenced or no commands are executed (e.g., MCP Server), the diagnostic listener simply never fires. There is no runtime error, no exception, and negligible overhead.

#### Infrastructure Span Filtering

`AddHttpClientInstrumentation` uses `FilterHttpRequestMessage` to suppress **OTel SDK HTTP request spans** for infrastructure-level outbound calls that would otherwise clutter distributed traces in Application Insights and the Aspire Dashboard.

**Suppressed hosts/paths**:

| Host pattern                | Path pattern                                   | Reason                         |
| --------------------------- | ---------------------------------------------- | ------------------------------ |
| `login.microsoftonline.com` | `openid-configuration`, `/discovery/`, `/keys` | OIDC metadata and JWKS refresh |

**Intentionally kept visible**: `login.microsoftonline.com/.../oauth2/v2.0/token` (OBO token exchange) spans are business-relevant for debugging tool invocation latency.

> **Clarification on OIDC metadata calls**: The Copilot Agent (MS365) authenticates with Entra ID using client credentials (client_id + client_secret) and does not consume `.well-known` discovery endpoints. The OIDC metadata calls filtered here originate from the **server-side JWT Bearer middleware**, which periodically refreshes the OpenID Connect configuration and signing keys to validate incoming tokens. These calls are infrequent (cached by the middleware, refreshed roughly every 24h or on key rotation) but without the filter they would appear as noisy infrastructure spans in Application Insights and the Aspire Dashboard.

#### Health Check Trace Filtering

Health probes (`/health`, `/alive`, `GET /`) are noise; they fire automatically every ~10–30s and always return 200 OK. They are excluded from OTLP export via `HealthCheckActivityFilter`, a `BaseProcessor<Activity>` registered via `ConfigureOpenTelemetryTracerProvider`:

```csharp
// In ConfigureOpenTelemetry():
builder.Services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
    tracing.AddProcessor(
        new HealthCheckActivityFilter(
            sp.GetRequiredService<IHttpContextAccessor>())));
```

The processor's `OnStart` checks `IHttpContextAccessor.HttpContext.Request.Path` and sets `Activity.IsAllDataRequested = false` for health/probe paths. This is the [Microsoft-recommended approach](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-filter#filter-telemetry-using-span-processors) and works because:

1. ASP.NET Core sets `HttpContext` on `IHttpContextAccessor` **before** calling `ActivitySource.StartActivity()`
2. Setting `IsAllDataRequested = false` in `OnStart` prevents the SDK from collecting data
3. The OTLP exporter never enqueues the activity

**Why this approach instead of alternatives?**

| Approach                                            | Problem                                                                                                                                                                                |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Filter` callback on `AddAspNetCoreInstrumentation` | Deprecated since v1.10.0 — the library no longer creates activities, only enriches them. The callback may be silently ignored.                                                         |
| `BaseProcessor<Activity>.OnEnd`                     | `TracerProviderSdk` checks `activity.Recorded` **before** invoking the processor chain. By the time the processor clears the flag, the SDK already dispatched to the export processor. |
| Middleware clearing `Recorded` flag                 | Unreliable timing — the SDK may snapshot flags or the batch exporter may have already enqueued the activity before the middleware runs.                                                |
| **`BaseProcessor.OnStart` (current)**               | Fires at activity creation, with `HttpContext` already available. Setting `IsAllDataRequested = false` prevents all downstream collection. **Reliable.**                               |

> **Azure Monitor note**: `HealthCheckActivityFilter` suppresses health probe spans from both the Aspire Dashboard (OTLP) and Azure Monitor Application Insights (`UseAzureMonitor()`), keeping the Application Map and Live Metrics free of noisy probe traces.

**Metrics** (`WithMetrics`):

- `AddAspNetCoreInstrumentation()`: request count, duration, response size
- `AddHttpClientInstrumentation()`: outbound HTTP metrics
- `AddRuntimeInstrumentation()`: GC, ThreadPool, memory metrics
- Custom meters registered via `ServiceTelemetryOptions.MeterNames`

**Export**: `UseOtlpExporter()` activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The OTel SDK reads this variable automatically along with `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_SERVICE_NAME`, and `OTEL_RESOURCE_ATTRIBUTES`.

### Logs (Serilog)

Both MCP Server and MockApi call `builder.Host.AddSerilogDefaults()` (defined in this project), which invokes `UseSerilog(..., writeToProviders: true)`. The **`writeToProviders: true`** flag bridges Serilog records into `Microsoft.Extensions.Logging` providers, including the OTel logging provider.

The active export path depends on the environment:

| Environment | Signal  | Export Path                                                                      |
| ----------- | ------- | -------------------------------------------------------------------------------- |
| Local dev   | Traces  | OTel SDK → `UseOtlpExporter()` → Aspire Dashboard                                |
| Local dev   | Metrics | OTel SDK → `UseOtlpExporter()` → Aspire Dashboard                                |
| Local dev   | Logs    | Serilog → `writeToProviders` → OTel SDK → Aspire Dashboard                       |
| Production  | Traces  | OTel SDK → `UseAzureMonitor()` → Azure Monitor Application Insights              |
| Production  | Metrics | OTel SDK → `UseAzureMonitor()` → Azure Monitor Application Insights              |
| Production  | Logs    | Serilog → `writeToProviders` → OTel SDK → Azure Monitor Application Insights     |

#### AddSerilogDefaults() — Centralized Logging

All Serilog configuration is centralized in the `AddSerilogDefaults()` extension method. Individual services do **not** configure Serilog in their `Program.cs` or `appsettings.json`; they simply call `builder.Host.AddSerilogDefaults()`.

**Sinks**:

- **Console**: text template with timestamp, level, source context, message, exception
- **File**: `RenderedCompactJsonFormatter` (structured JSON), daily rolling, 5-file retention, path derived from assembly name (`logs/` locally, `%HOME%\LogFiles\dotnet\` in App Service)

**Enrichers**:

- `FromLogContext`: scoped properties
- `WithProperty("Application", ...)` / `WithProperty("Environment", ...)`: service identity
- `WithSpan()`: W3C TraceId, SpanId, ParentId as hex strings (used by Aspire Dashboard locally)
- `WithMachineName()`: instance identification in multi-instance deployments
- `WithThreadId()`: concurrency debugging

### NuGet Packages

| Package                                             | Version       | Purpose                                                    |
| --------------------------------------------------- | ------------- | ---------------------------------------------------------- |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol`      | 1.15.0        | Traces + Metrics + Logs export (OTLP)                      |
| `OpenTelemetry.Extensions.Hosting`                  | 1.15.0        | OTel SDK hosting integration                               |
| `OpenTelemetry.Instrumentation.AspNetCore`          | 1.15.0        | HTTP request traces + metrics                              |
| `OpenTelemetry.Instrumentation.Http`                | 1.15.0        | HttpClient traces + metrics                                |
| `OpenTelemetry.Instrumentation.Runtime`             | 1.15.0        | GC, ThreadPool, memory metrics                             |
| `OpenTelemetry.Instrumentation.EntityFrameworkCore` | 1.15.0-beta.1 | EF Core database command traces (auto db.system detection) |
| `Serilog.AspNetCore`                                | 10.0.0        | UseSerilog() + RequestLogging middleware                   |
| `Serilog.Enrichers.Environment`                     | 3.0.1         | WithMachineName() enricher                                 |
| `Serilog.Enrichers.Span`                            | 3.1.0         | WithSpan(): TraceId/SpanId/ParentId correlation            |
| `Serilog.Enrichers.Thread`                          | 4.0.0         | WithThreadId() enricher                                    |
| `Serilog.Formatting.Compact`                        | 3.0.0         | RenderedCompactJsonFormatter for file sink                 |
| `Serilog.Sinks.Console`                             | 6.1.1         | Console log output                                         |
| `Serilog.Sinks.File`                                | 7.0.0         | File log output (rolling daily, 5-file retention)          |
| `Azure.Monitor.OpenTelemetry.AspNetCore`             | 1.*           | Azure Monitor exporter: traces, metrics, and logs to Application Insights |

## Observability: Export Paths

Two export paths are configured independently in `AddOpenTelemetryExporters()`:

| Environment              | Exporter                    | Activation                                                              |
| ------------------------ | --------------------------- | ----------------------------------------------------------------------- |
| Local dev (Aspire)       | OTLP → Aspire Dashboard      | `OTEL_EXPORTER_OTLP_ENDPOINT` auto-injected by Aspire AppHost           |
| Production (App Service) | Azure Monitor App Insights  | `APPLICATIONINSIGHTS_CONNECTION_STRING` App Service Application setting |

## Configuration Model

```csharp
public class ServiceTelemetryOptions
{
    public List<string> ActivitySourceNames { get; } = [];
    public List<string> MeterNames { get; } = [];
}
```

Each service registers its custom telemetry sources at startup:

| Service    | ActivitySource      | Meter        |
| ---------- | ------------------- | ------------ |
| MCP Server | `McpActivitySource` | `McpMetrics` |
| MockApi    | `ApiActivitySource` | `ApiMetrics` |
