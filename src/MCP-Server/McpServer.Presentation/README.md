# McpServer.Presentation — Presentation Layer

## Overview

Outermost layer of the Clean Architecture. Acts as the composition root and MCP transport host. Contains MCP tools, prompts, middleware, telemetry filter, and all ASP.NET Core / MCP SDK wiring. No business logic lives here: tools delegate to use cases in `McpServer.Application`.

- **Target Framework**: .NET 10 (Web SDK)
- **NuGet packages**: `ModelContextProtocol.AspNetCore` v1.2.0
- **Project references**: `McpServer.Application`, `McpServer.Infrastructure`, `McpServer.ServiceDefaults`, `McpServer.Shared`

For the full project overview (architecture, tools catalog, authorization, OBO security posture, configuration, running), see the [project-level README](../README.md).

## Contents

### Composition Root (`Program.cs`)

Minimal startup (~44 lines) orchestrating the three Clean Architecture layers:

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation(builder.Configuration, builder.Environment);
```

### Extensions (`Extensions/`)

#### PresentationServiceExtensions (`Extensions/PresentationServiceExtensions.cs`)

Public entry point for Presentation DI registration:

```csharp
services.AddPresentation(configuration, environment);
```

Orchestrates: authentication, CORS, rate limiting, and MCP server setup.

#### AuthenticationExtensions (`Extensions/AuthenticationExtensions.cs`)

Configures JWT Bearer authentication for Microsoft Entra ID with MCP-aware challenge scheme.

- Validates JWTs against `{ClientId}` and `api://{ClientId}` audiences
- Uses `BuildTokenValidationParameters()` from `EntraIdBaseOptions` for issuer/signing key validation
- Default authenticate: `JwtBearerDefaults.AuthenticationScheme`
- Default challenge: `McpAuthenticationDefaults.AuthenticationScheme` (MCP-aware 401 with [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728) metadata)
- Rate limiting: fixed window 100 req/min per user identity or IP

Stays in Server because it depends on `.AddMcp()` from the MCP SDK.

#### McpServerExtensions (`Extensions/McpServerExtensions.cs`)

Registers the MCP server with tools, prompts, and filters. Tools and prompts are registered explicitly with the generic `WithTools<T>()` / `WithPrompts<T>()` overloads, which are AOT-safe (`[DynamicallyAccessedMembers]`) and avoid an assembly scan at startup. Consider switching to `WithToolsFromAssembly()` / `WithPromptsFromAssembly()` when the number of types exceeds ~8 to reduce boilerplate, accepting the `[RequiresUnreferencedCode]` constraint and a one-time startup scan.

```csharp
.AddAuthorizationFilters()
.WithRequestFilters(filters =>
{
    filters.AddCallToolFilter(McpTelemetryFilter.Create());
})
.WithTools<TaskTools>()
.WithTools<ProjectsTools>()
.WithTools<BalancesTools>()
.WithPrompts<TaskPrompts>()
.WithPrompts<ProjectPrompts>()
```

#### WellKnownEndpointExtensions (`Extensions/WellKnownEndpointExtensions.cs`)

Maps anonymous endpoints for OAuth/MCP discovery:

- `GET /.well-known/oauth-protected-resource`: [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728) protected resource metadata
- `GET /.well-known/oauth-authorization-server`: [RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) proxy to Entra ID OpenID configuration

#### McpCorrelationMiddlewareExtensions (`Extensions/McpCorrelationMiddlewareExtensions.cs`)

Registers `McpCorrelationMiddleware` in the ASP.NET Core pipeline via `UseMcpCorrelation()`.

### Middleware (`Middleware/`)

#### McpCorrelationMiddleware (`Middleware/McpCorrelationMiddleware.cs`)

Extracts correlation headers from MCP requests and sets them as Activity tags on the HTTP-level span:

- `Mcp-Session-Id` → `mcp.session.id`
- W3C `traceparent` / `tracestate` → standard trace context
- Azure SDK headers (`x-ms-client-request-id`, `x-ms-correlation-request-id`)

### Configuration (`Configuration/`)

#### RateLimitOptions (`Configuration/RateLimitOptions.cs`)

Strongly-typed options class for the in-process fixed-window rate limiter. Bound from the `RateLimit` section in `appsettings.json` and validated on start with `[Range]` data annotations.

| Property        | Type  | Default | Description                                       |
| --------------- | ----- | ------- | ------------------------------------------------- |
| `PermitLimit`   | `int` | 100     | Maximum requests per user or IP within the window |
| `WindowSeconds` | `int` | 60      | Fixed window duration in seconds                  |
| `QueueLimit`    | `int` | 10      | Queued requests before returning HTTP 429         |

The queue absorbs short bursts without dropping requests and enforces backpressure before business logic, protecting downstream infrastructure even when APIM is bypassed (direct App Service URL).

```json
"RateLimit": {
  "PermitLimit": 100,
  "WindowSeconds": 60,
  "QueueLimit": 10
}
```

### Tools (`Tools/`)

Three sealed tool classes, one per domain. Each class uses primary constructor to inject use cases (from `McpServer.Application/UseCases/`). Every method delegates to exactly one use case via `ExecuteAsync()` and returns `result.ToJson()`.

**Mandatory patterns** (enforced by project conventions):

- `[Authorize]` at class level AND `[Authorize(Roles = Permissions.XXX)]` at method level
- Inject use cases only, not `IDownstreamApiService`, `IHttpContextAccessor`, or `ILogger`
- No `Stopwatch`, `McpActivitySource`, `McpMetrics.RecordToolInvocation()`, or `McpMetrics.RecordResponseSize()` (handled by `McpTelemetryFilter`)
- No try/catch for general error handling (handled by `McpTelemetryFilter`)
- All `if`/`else`/`for`/`foreach`/`while` must have curly braces (SonarQube S121)
- `CancellationToken` without `= default` (MCP SDK injects it)

```csharp
[McpServerToolType]
[Authorize]
public sealed class TaskTools(
    GetTasksUseCase getTasksUseCase,
    CreateTaskUseCase createTaskUseCase)
{
    [McpServerTool(Name = "get_tasks", Title = "Get Tasks", ReadOnly = true)]
    [Description("Get all tasks for the authenticated user.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public async Task<string> GetTasks(CancellationToken cancellationToken)
    {
        var result = await getTasksUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }
}
```

| Class           | Tools | Domain   |
| --------------- | ----- | -------- |
| `TaskTools`     | 4     | Tasks    |
| `ProjectsTools` | 2     | Projects |
| `BalancesTools` | 2     | Balances |

For the full tools catalog with parameters, see the [project-level README](../README.md#tools-catalog).

### Prompts (`Prompts/`)

Two sealed prompt classes. Return `ChatMessage` (from `Microsoft.Extensions.AI`) with structured prompt text. Same authorization pattern as tools.

```csharp
[McpServerPromptType]
[Authorize]
public sealed class TaskPrompts
{
    [McpServerPrompt(Name = "summarize_tasks")]
    [Description("Generate a summary of all user tasks.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public ChatMessage SummarizeTasks([Description("Optional status filter")] string? statusFilter = null)
    {
        return new ChatMessage(ChatRole.User, "Structured prompt text...");
    }
}
```

| Class            | Prompts | Domain   |
| ---------------- | ------- | -------- |
| `TaskPrompts`    | 2       | Tasks    |
| `ProjectPrompts` | 2       | Projects |

For the full prompts catalog with arguments, see the [project-level README](../README.md#prompts-catalog).

### Telemetry (`Telemetry/`)

#### McpTelemetryFilter (`Telemetry/McpTelemetryFilter.cs`)

Centralized CallTool filter registered via `AddCallToolFilter`. Handles all tool telemetry so that tool classes contain only business logic.

Stays in Server (not Infrastructure) because it depends on `ModelContextProtocol.Protocol` types (`CallToolRequestParams`, `CallToolResult`).

The filter executes in the MCP request pipeline after authorization:

```
AuthorizationFilter → McpTelemetryFilter → ToolHandler
```

For every tool invocation, the filter automatically:

1. Starts an OpenTelemetry `Activity` via `McpActivitySource.StartToolActivity(toolName)`
2. Enriches the activity with user context (`oid`, `azp`, roles, `tid`, scopes, IP) from `HttpContext`
3. Propagates `mcp.session.id` from the `Mcp-Session-Id` HTTP header (child spans do not inherit parent tags)
4. Tags tools with `mcp.tool.data_classification` when registered in the classification dictionary
5. Records execution time via `Stopwatch` and `McpMetrics.RecordToolInvocation()`
6. Measures response size via `McpMetrics.RecordResponseSize()`
7. Logs invocation (`LogInformation`) and errors (`LogError`) with structured `{ToolName}` property
8. On exceptions, records the error on the activity via `McpActivitySource.RecordError()`

**Log SourceContext**: `McpServer.Presentation.Telemetry.McpTelemetryFilter` (the class emitting the log, not the tool class). The tool name is a structured property `{ToolName}` filterable in Serilog/Seq/Aspire Dashboard.

#### Data Classification

`McpTelemetryFilter` maintains a static `ToolDataClassifications` dictionary mapping tool names to classification labels. The PDP uses this label to apply tighter controls.

No tools are currently classified as sensitive. Add entries to `ToolDataClassifications` when tools return PII or security-sensitive data.

#### Session ID Span Propagation

Span hierarchy showing how `mcp.session.id` flows:

```
POST /mcp (HTTP span)              ← mcp.session.id set by McpCorrelationMiddleware
  └── mcp.tool.get_tasks (custom)   ← mcp.session.id propagated by McpTelemetryFilter
        └── GET /api/tasks (HTTP)   ← auto-instrumented, no mcp.session.id
```

## Observability

### Logging (Serilog)

Centralized in `McpServer.ServiceDefaults` via `AddSerilogDefaults()`. The MCP Server calls `builder.Host.AddSerilogDefaults()` and `app.UseSerilogRequestLogging()`. No Serilog section in `appsettings.json`.

### Traces & Metrics (OTel SDK)

Custom sources registered via `ServiceTelemetryOptions` in `Program.cs`:

```csharp
builder.AddServiceDefaults(telemetry =>
{
    telemetry.ActivitySourceNames.Add(McpActivitySource.Name);
    telemetry.MeterNames.Add(McpMetrics.MeterName);
});
```

**Custom Metrics** (meter: `McpServer.Presentation`):

| Metric                       | Type            | Tags                                    | Description                   |
| ---------------------------- | --------------- | --------------------------------------- | ----------------------------- |
| `mcp.tool.invocations`       | Counter → Dist. | `mcp.tool.name`, `mcp.tool.success`     | Tool invocation count         |
| `mcp.tool.errors`            | Counter → Dist. | `mcp.tool.name`, `mcp.tool.success`     | Tool error count              |
| `mcp.tool.duration`          | Histogram       | `mcp.tool.name`, `mcp.tool.success`     | Execution time in ms          |
| `mcp.tool.validation.errors` | Counter → Dist. | `mcp.tool.name`, `validation.parameter` | Input validation failures     |
| `mcp.tool.response.size`     | Histogram       | `mcp.tool.name`                         | Response payload size (bytes) |

### Span Enrichment & Claim Mapping

`McpActivitySource.EnrichWithUserContext()` (in Infrastructure) extracts JWT claims from `HttpContext.User` and sets them as span tags. The code handles `MapInboundClaims = true` (ASP.NET Core default) by using fallback URIs from `EntraClaimTypes`:

| JWT Claim (raw) | Span Tag          | Lookup Strategy                                           |
| --------------- | ----------------- | --------------------------------------------------------- |
| `oid`           | `enduser.id`      | Try `"oid"` → try `EntraClaimTypes.ObjectId`              |
| `roles` (array) | `enduser.roles`   | Use `ClaimsIdentity.RoleClaimType` (respects any mapping) |
| `tid`           | `tenant.id`       | Try `"tid"` → try `EntraClaimTypes.TenantId`              |
| `scp`           | `enduser.scope`   | Try `"scp"` → try `EntraClaimTypes.Scope` → try `"scope"` |
| `azp`           | `oauth.client.id` | Try `"azp"` → try `"client_id"`                           |
| (connection)    | `client.address`  | `HttpContext.Connection.RemoteIpAddress`                  |

`enduser.id` always uses `oid` (Entra ID Object ID), never `sub`, for cross-service span correlation.

### Export Paths

| Environment              | Exporter          | Target                           |
| ------------------------ | ----------------- | -------------------------------- |
| Local dev (Aspire)       | OTLP/gRPC         | Aspire Dashboard                 |
| Production (App Service) | Azure Monitor SDK | Application Insights             |

## CORS Configuration

Configured in `PresentationServiceExtensions` for browser-based MCP clients. Origins read from `Cors:AllowedOrigins` in config, with localhost dev port fallbacks:

| Origin                                            | Purpose          |
| ------------------------------------------------- | ---------------- |
| `http://localhost:6274` / `http://127.0.0.1:6274` | MCP Inspector    |
| `http://localhost:5173` / `http://127.0.0.1:5173` | Vite dev server  |
| `http://localhost:3000` / `http://127.0.0.1:3000` | React dev server |

## Middleware Order

```
UseSerilogRequestLogging() → UseCors() → UseRateLimiter() → UseAuthentication() → UseMcpCorrelation() → UseAuthorization()
```

## File Structure

```
McpServer.Presentation/
├── Program.cs                              # Composition root (~44 lines)
├── McpServer.Presentation.csproj
├── appsettings.json                        # Application configuration (EntraId, DownstreamApi, Cors, RateLimit)
├── appsettings.Development.json            # Development overrides
│
├── Configuration/
│   └── RateLimitOptions.cs                 # Strongly-typed options for the in-process rate limiter
│
├── Extensions/
│   ├── PresentationServiceExtensions.cs    # AddPresentation: orchestrates all Presentation DI
│   ├── AuthenticationExtensions.cs         # JWT + MCP SDK challenge scheme
│   ├── McpServerExtensions.cs             # Tools, prompts, filters registration
│   ├── McpCorrelationMiddlewareExtensions.cs
│   └── WellKnownEndpointExtensions.cs     # RFC 9728 / 8414 endpoints
│
├── Middleware/
│   └── McpCorrelationMiddleware.cs         # Session ID and trace context propagation
│
├── Telemetry/
│   └── McpTelemetryFilter.cs              # Centralized CallTool telemetry filter
│
├── Tools/
│   ├── TaskTools.cs                       # 4 tools: CRUD tasks
│   ├── ProjectsTools.cs                   # 2 tools: backend projects
│   ├── BalancesTools.cs                   # 1 tool: backend balance
│   └── AdminTools.cs                      # 1 tool: admin operations
│
├── Prompts/
│   ├── TaskPrompts.cs                     # 2 prompts: task summarization/analysis
│   ├── ProjectPrompts.cs                  # 2 prompts: project analysis/comparison
│   └── AdminPrompts.cs                    # 2 prompts: compliance/audit
│
├── Properties/
│   └── launchSettings.json
└── logs/                                  # Rolling log files
```

## Design Decisions

1. **Minimal composition root**: `Program.cs` calls `.AddApplication().AddInfrastructure().AddPresentation()`. Each layer registers its own services.

2. **`McpTelemetryFilter` stays in Server**: It depends on `ModelContextProtocol.Protocol` types (`CallToolRequestParams`, `CallToolResult`). Moving it to Infrastructure would add an MCP SDK dependency to a layer that should not have it.

3. **`AuthenticationExtensions` stays in Server**: It depends on `.AddMcp()` from the MCP SDK for the challenge scheme.

4. **Tools inject use cases only**: No direct `IDownstreamApiService`, `IHttpContextAccessor`, `ILogger`, or telemetry primitives. Use cases handle validation and orchestration; the filter handles telemetry.

5. **MCP authentication challenge**: Uses `McpAuthenticationDefaults.AuthenticationScheme` for MCP-specification-compliant 401 responses with [RFC 9728](https://datatracker.ietf.org/doc/html/rfc9728) protected resource metadata.

## Dependency Graph

```
McpServer.Domain
    ↑
McpServer.Application
    ↑
McpServer.Infrastructure
    ↑
McpServer.Presentation  (this project)
```

For layer details: [Domain README](../McpServer.Domain/README.md), [Application README](../McpServer.Application/README.md), [Infrastructure README](../McpServer.Infrastructure/README.md).
