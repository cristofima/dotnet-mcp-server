# McpBaseline.ServiceDefaults

Shared Aspire service defaults referenced by every service project. Configures OpenTelemetry, health checks, service discovery, and HTTP client resilience in one place.

## What it provides

- **OpenTelemetry**: traces, metrics, and logs with ASP.NET, HTTP client, EF Core, and runtime instrumentation. Custom activity sources and meters are registered per-service via `ServiceTelemetryOptions`.
- **Health checks**: services registered via `AddServiceDefaults()`, endpoints mapped via `MapDefaultEndpoints()` — `/health` (readiness) and `/alive` (liveness), always enabled with environment-aware response detail.
- **Service discovery**: `AddServiceDiscovery()` on all HTTP clients.
- **Resilience**: `AddStandardResilienceHandler()` on all HTTP clients.

## Project Structure

```
McpBaseline.ServiceDefaults/
├── Extensions/
│   ├── HostApplicationBuilderExtensions.cs
│   ├── WebApplicationExtensions.cs
│   └── HostBuilderExtensions.cs
├── Configuration/
│   └── ServiceTelemetryOptions.cs
└── Telemetry/
    ├── DogStatsDMetricBridge.cs
    └── HealthCheckActivityFilter.cs
```

Each extension class owns only the private static helpers it uses:

- `HostApplicationBuilderExtensions.cs` (`IHostApplicationBuilder`): registers DI services — OpenTelemetry pipeline, health check services (`AddHealthChecks`), service discovery, and HTTP client resilience. Entry point: `builder.AddServiceDefaults()`.
- `WebApplicationExtensions.cs` (`WebApplication`): maps `/health` and `/alive` HTTP endpoints into the request pipeline. Requires services registered by the above. Entry point: `app.MapDefaultEndpoints()`.
- `HostBuilderExtensions.cs` (`IHostBuilder`): configures Serilog with console and file sinks, bridged to the OTel logging provider via `writeToProviders: true`. Entry point: `builder.Host.AddSerilogDefaults()`.
- `ServiceTelemetryOptions.cs`: options model for registering custom meter names and activity source names per service.
- `DogStatsDMetricBridge.cs`: forwards custom metrics to Datadog via DogStatsD when no OTLP endpoint is configured (Azure App Service). See [DogStatsD Metric Bridge](#dogstatsd-metric-bridge).
- `HealthCheckActivityFilter.cs`: OTel `BaseProcessor<Activity>` that suppresses health probe traces from OTLP export. See [Health Check Trace Filtering](#health-check-trace-filtering).

## Health Checks

Both endpoints are **always mapped**, including production. Azure App Service health probes require a path that returns 200 OK; without one, probes hit `GET /` and generate 404 traces in Datadog.

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
> **This setting has no effect on Datadog spans.** In production, `OTEL_EXPORTER_OTLP_ENDPOINT` is not set, so the OTel SDK export path is inactive. EF Core instrumentation in production is handled entirely by the **Datadog CLR profiler**, which uses its own attribute schema (`db.system`, `db.instance`, `db.type`) independently of OTel semantic conventions. The attributes visible in Datadog are always Datadog's native schema regardless of this setting. `OTEL_SEMCONV_STABILITY_OPT_IN` only affects the OTel SDK output — observable locally via the Aspire Dashboard.

Query parameters can be emitted via `OTEL_DOTNET_EXPERIMENTAL_EFCORE_ENABLE_TRACE_DB_QUERY_PARAMETERS=true`, but this is experimental and may expose sensitive data.

**Safe for non-EF projects**: The instrumentation subscribes to the `Microsoft.EntityFrameworkCore` `DiagnosticSource`. If EF Core is not referenced or no commands are executed (e.g., MCP Server), the diagnostic listener simply never fires. There is no runtime error, no exception, and negligible overhead.

#### Infrastructure Span Filtering

`AddHttpClientInstrumentation` uses `FilterHttpRequestMessage` to suppress **OTel SDK HTTP request spans** for infrastructure-level outbound calls. When `DD_TRACE_OTEL_ENABLED=true` (required for MCP Server's Streamable HTTP transport), the OTel SDK instruments all `HttpClient` calls, including Datadog's own log submission and OIDC metadata refresh calls to `login.microsoftonline.com`.

The CLR profiler on services without the OTel bridge (e.g., MockApi) excludes its own internal connections automatically, but the OTel SDK does not.

> **Dual-layer filtering**: When `DD_TRACE_OTEL_ENABLED=true`, both the OTel SDK and the CLR profiler instrument outbound HTTP calls independently. `FilterHttpRequestMessage` only suppresses OTel SDK spans. The CLR profiler still generates its own traces for the same calls: standalone "TLS client handshake" spans (`System.Net.Security` integration) and HTTP client spans (e.g., `GET login.microsoftonline.com/...` for OIDC metadata refresh). Use `DD_APM_IGNORE_RESOURCES` to filter these CLR profiler traces (see Health Check Trace Filtering section below for the full pattern).

**Suppressed hosts/paths**:

| Host pattern                | Path pattern                                   | Reason                                 |
| --------------------------- | ---------------------------------------------- | -------------------------------------- |
| `*.datadoghq.*`             | (all)                                          | Datadog intake (logs, traces, metrics) |
| `login.microsoftonline.com` | `openid-configuration`, `/discovery/`, `/keys` | OIDC metadata and JWKS refresh         |

**Intentionally kept visible**: `login.microsoftonline.com/.../oauth2/v2.0/token` (OBO token exchange) spans are business-relevant for debugging tool invocation latency.

> **Clarification on OIDC metadata calls**: The Copilot Agent (MS365) authenticates with Entra ID using client credentials (client_id + client_secret) and does not consume `.well-known` discovery endpoints. The OIDC metadata calls filtered here originate from the **server-side JWT Bearer middleware**, which periodically refreshes the OpenID Connect configuration and signing keys to validate incoming tokens. These calls are infrequent (cached by the middleware, refreshed roughly every 24h or on key rotation) but without the filter they would appear as infrastructure spans when `DD_TRACE_OTEL_ENABLED=true`.

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

> **Datadog note**: This processor only affects the OTel SDK export path (Aspire Dashboard). In production, the Datadog CLR profiler auto-instruments all requests independently. Use `DD_APM_IGNORE_RESOURCES` to filter infrastructure noise in Datadog:
>
> ```
> DD_APM_IGNORE_RESOURCES=^GET /(health|alive)?$|^TLS client handshake|^GET login\.microsoftonline\.com|datadoghq\.com
> ```
>
> This drops four categories of standalone traces: health probes, TLS handshake spans, OIDC metadata refresh (`GET login.microsoftonline.com`), and Datadog intake HTTP calls (`datadoghq.com` for log submission, telemetry, etc.). OBO token exchange calls (`POST login.microsoftonline.com/.../oauth2/v2.0/token`) are not matched because the regex targets `GET` only. When these spans appear as child spans inside a business trace (e.g., `tools/call get_projects`), they are preserved because `DD_APM_IGNORE_RESOURCES` only filters by the root span resource.

**Metrics** (`WithMetrics`):

- `AddAspNetCoreInstrumentation()`: request count, duration, response size
- `AddHttpClientInstrumentation()`: outbound HTTP metrics
- `AddRuntimeInstrumentation()`: GC, ThreadPool, memory metrics
- Custom meters registered via `ServiceTelemetryOptions.MeterNames`

**Export**: `UseOtlpExporter()` activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. The OTel SDK reads this variable automatically along with `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_SERVICE_NAME`, and `OTEL_RESOURCE_ATTRIBUTES`.

### Logs (Serilog)

Both MCP Server and MockApi call `builder.Host.AddSerilogDefaults()` (defined in this project), which invokes `UseSerilog(..., writeToProviders: true)`. The **`writeToProviders: true`** flag bridges Serilog records into `Microsoft.Extensions.Logging` providers, including the OTel logging provider.

The active export path depends on the environment:

| Environment | Signal  | Export Path                                                                          |
| ----------- | ------- | ------------------------------------------------------------------------------------ |
| Local dev   | Traces  | OTel SDK → `UseOtlpExporter()` → Aspire Dashboard                                    |
| Local dev   | Metrics | OTel SDK → `UseOtlpExporter()` → Aspire Dashboard                                    |
| Local dev   | Logs    | Serilog → `writeToProviders` → OTel SDK → Aspire Dashboard                           |
| Production  | Traces  | Datadog tracer → named pipe → Datadog extension agent                                |
| Production  | Metrics | Datadog tracer → named pipe → Datadog extension agent                                |
| Production  | Logs    | Serilog → Datadog tracer (`DD_LOGS_DIRECT_SUBMISSION_INTEGRATIONS`) → Datadog intake |

> In production, `OTEL_EXPORTER_OTLP_ENDPOINT` is not set, so the OTLP path is inactive. The Datadog .NET extension handles all three signals via its own transport.

#### AddSerilogDefaults() — Centralized Logging

All Serilog configuration is centralized in the `AddSerilogDefaults()` extension method. Individual services do **not** configure Serilog in their `Program.cs` or `appsettings.json`; they simply call `builder.Host.AddSerilogDefaults()`.

**Sinks**:

- **Console**: text template with timestamp, level, source context, message, exception
- **File**: `RenderedCompactJsonFormatter` (structured JSON), daily rolling, 5-file retention, path derived from assembly name (`logs/` locally, `%HOME%\LogFiles\dotnet\` in App Service)

**Enrichers**:

- `FromLogContext`: scoped properties
- `WithProperty("Application", ...)` / `WithProperty("Environment", ...)`: service identity
- `WithSpan()`: W3C TraceId, SpanId, ParentId as hex strings (used by Aspire Dashboard locally)
- Datadog correlation (`dd_trace_id`, `dd_span_id`, `dd_env`, `dd_service`, `dd_version`): auto-injected by the Datadog .NET tracer via `DD_LOGS_INJECTION=true` (default since tracer v3.24.0)
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

## DogStatsD Metric Bridge

`DogStatsDMetricBridge` forwards `System.Diagnostics.Metrics` measurements to Datadog via DogStatsD when no OTLP endpoint is configured. This covers Azure App Service deployments where the Datadog extension provides `dogstatsd.exe` (named pipes) but **not** an OTLP receiver.

### When it activates

In `AddOpenTelemetryExporters()`, if `OTEL_EXPORTER_OTLP_ENDPOINT` is **not** set and the service registered at least one custom meter via `ServiceTelemetryOptions.MeterNames`, the bridge is registered as an `IHostedService`:

```csharp
// HostApplicationBuilderExtensions.cs — conditional registration in AddOpenTelemetryExporters()
if (!useOtlpExporter && telemetryOptions.MeterNames.Count > 0)
{
    builder.Services.AddHostedService(sp =>
        new DogStatsDMetricBridge(
            sp.GetRequiredService<ILogger<DogStatsDMetricBridge>>(),
            meterNames));
}
```

### How it works

1. **Transport**: Reads `DD_DOGSTATSD_PIPE_NAME` (or `DD_DOGSTATSD_WINDOWS_PIPE_NAME`) from the process environment and sets `StatsdConfig.PipeName`. The DogStatsD-CSharp-Client does **not** auto-detect these variables — it only reads `DD_DOGSTATSD_URL`, which the extension doesn't set. Without this explicit mapping, the client defaults to UDP `localhost:8125`, which the extension disables (`DD_DOGSTATSD_PORT=0`).

2. **Instrument interception**: A `MeterListener` subscribes to `Counter<long>`, `Histogram<double>`, and `Histogram<long>` instruments from the registered meter names.

3. **All instruments → Distribution (`|d`)**: Every measurement is sent as `DogStatsd.Distribution()`, including counters. This is intentional:

   | DogStatsD type       | Datadog metric type | Problem with counters                                         |
   | -------------------- | ------------------- | ------------------------------------------------------------- |
   | Counter (`\|c`)      | **Rate**            | Value ÷ flush interval (10 s) → 1 invocation shows as 0.1     |
   | Distribution (`\|d`) | **Distribution**    | Raw values stored; `count` aggregation returns integer totals |

4. **Tags**: OTel-style `KeyValuePair<string, object?>` tags are converted to DogStatsD `"key:value"` string arrays.

### Metric mapping

| .NET Instrument     | DogStatsD call              | Datadog type | How to query                       |
| ------------------- | --------------------------- | ------------ | ---------------------------------- |
| `Counter<long>`     | `Distribution(name, value)` | Distribution | `count:name{*}` for integer totals |
| `Histogram<double>` | `Distribution(name, value)` | Distribution | `avg/p95/p99:name{*}` for latency  |
| `Histogram<long>`   | `Distribution(name, value)` | Distribution | `avg/p95:name{*}` for sizes        |

### Metrics emitted

Each service defines its own custom metrics (names, tags, descriptions) in its README:

- **MCP Server** (meter: `McpBaseline.Presentation`): see [McpBaseline.Presentation/README.md](../MCP-Server/McpBaseline.Presentation/README.md#traces--metrics-otel-sdk) — 5 instruments (invocations, errors, duration, validation errors, response size)
- **MockApi** (meter: `McpBaseline.MockApi`): see [McpBaseline.MockApi/README.md](../McpBaseline.MockApi/README.md#traces--metrics-otel-sdk) — 3 instruments (invocations, errors, duration)

The bridge forwards all registered meters without transformation; only the DogStatsD type mapping (see table above) applies.

### Key environment variables (set by Datadog extension)

| Variable                         | Value (example)                                  | Used by         |
| -------------------------------- | ------------------------------------------------ | --------------- |
| `DD_DOGSTATSD_PIPE_NAME`         | `dogstatsd-E92B281E-8208-4F62-B5E1-259BFB5F6804` | Bridge          |
| `DD_DOGSTATSD_WINDOWS_PIPE_NAME` | (same as above)                                  | Bridge fallback |
| `DD_DOGSTATSD_PORT`              | `0` (UDP disabled)                               | Extension only  |

### NuGet dependency

| Package                   | Version | Purpose                        |
| ------------------------- | ------- | ------------------------------ |
| `DogStatsD-CSharp-Client` | 9.0.0   | DogStatsD protocol + transport |

## Observability: OTLP Exporter

The OTLP exporter activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set. In local development, Aspire AppHost auto-injects this variable pointing at the Aspire Dashboard collector, so no manual configuration is needed.

| Environment              | OTLP active? | How signals are exported                          |
| ------------------------ | ------------ | ------------------------------------------------- |
| Local dev (Aspire)       | Yes          | All signals → Aspire Dashboard via gRPC           |
| Production (App Service) | No           | Datadog extension handles all signals (see above) |

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
