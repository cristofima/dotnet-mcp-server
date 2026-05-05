---
name: "C#/.NET Code Generation"
description: "C#/.NET code generation conventions for MCP OAuth2 Security Baseline. Enforces Microsoft .NET best practices, MCP Server patterns, Entra ID OBO, SOLID/DRY/KISS/YAGNI, and enterprise observability."
applyTo: "**/*.cs"
---

# C#/.NET Code Generation Instructions

Generate C# code that strictly follows these rules for the MCP OAuth2 Security Baseline project.

## Important Note

**DO NOT create `.md` documentation files with every prompt unless explicitly requested.**

## 1. C# Style (.NET 10 / Microsoft Conventions)

### Formatting

- Line length: **120 characters** (project-wide).
- Indentation: 4 spaces. No tabs.
- One blank line between members. Two blank lines are not required between top-level types (one is enough).
- Use trailing commas in multi-line collection initializers and array initializers.
- Braces on their own line (Allman style), consistent with Microsoft conventions.
- **All control structures must have curly braces** (`if`, `else`, `else if`, `for`, `foreach`, `while`, `do`), even for single-line bodies. This includes guard clauses. SonarQube rule S121.
- Use file-scoped namespaces: `namespace McpServer.Presentation.Tools;`

### Naming

- `PascalCase` for classes, methods, properties, events, and public fields.
- `camelCase` for parameters and local variables.
- `_camelCase` (underscore prefix) for private fields: `private readonly IDownstreamApiService _downstreamApiService;`
- `UPPER_SNAKE_CASE` for `const` values used in attribute arguments: `public const string TASK_READ = "mcp:task:read";`
- `PascalCase` for static read-only properties that replace `const`: `public static string Name { get; } = "McpServer";` (see § Constants: `const` vs `static`).
- `IPascalCase` for interfaces: `IDownstreamApiService`, `ITokenExchangeService`.
- Never use single-character names except in lambdas, LINQ, or loop counters (`x`, `i`, `_`).
- Async methods must have `Async` suffix: `GetTasksAsync()`, `ExchangeTokenAsync()`.
- Boolean properties/variables start with `Is`, `Has`, `Can`: `IsRetryable`, `HasRoles`.

### Imports (using directives)

- Group: (1) `System.*`, (2) `Microsoft.*`, (3) third-party packages, (4) project namespaces — separated by blank lines.
- Use global usings in `GlobalUsings.cs` for commonly used namespaces.
- Never use `using static` for entire classes unless the type is a well-known helper (e.g., `System.Math`).
- Remove unused usings (IDE0005).

### XML Documentation

- Every public class, method, and property must have `<summary>` documentation.
- Use `<param>`, `<returns>`, `<exception>`, `<example>` sections where they add value.
- One-line summaries for simple members: `/// <summary>Get all tasks for the authenticated user.</summary>`
- MCP tool descriptions use `[Description]` attributes (not XML docs) because they become the tool description for AI agents.

### Type Annotations and Nullability

- Enable nullable reference types project-wide: `<Nullable>enable</Nullable>`.
- Never suppress nullability warnings with `!` (null-forgiving operator) unless justified with a comment.
- Use `string?` for nullable, `string` for non-nullable. Same for all reference types.
- Return concrete types at API boundaries; use `JsonElement` for MCP tool responses.
- Prefer `record` types for immutable DTOs: `public record TaskResponseDto(int Id, string Title);`

## 2. Project Architecture

### Solution Structure

This project follows Clean Architecture with four layers, plus supporting projects:

```
McpServer.Domain/              # Innermost: permission constants, validation rules (zero dependencies)
McpServer.Application/         # Contracts, models, configuration (depends on Domain)
McpServer.Infrastructure/      # HTTP clients, MSAL OBO, telemetry, health checks (depends on Application + Shared)
McpServer.Presentation/        # Presentation: MCP tools, prompts, middleware, composition root
McpServer.BackendApi/             # Downstream REST API (EF Core InMemory) — separate bounded context
McpServer.Shared/              # SharedKernel: Entra ID config models, security helpers (used by Infrastructure + MockApi)
McpServer.ServiceDefaults/     # Aspire defaults: OpenTelemetry, Serilog, health
McpServer.AppHost/             # Aspire orchestrator
```

Dependency direction: Domain → Application → Infrastructure → Presentation (Server). Each layer has its own README.

### File Organization Rules

- **One class per file.** Match the filename to the class name: `TaskTools.cs` contains `TaskTools`.
- **No nested classes.** Each class goes in its own file.
- **Sealed by default.** All tool, prompt, service, and middleware classes are `sealed` unless inheritance is explicitly required.
- **Folder-per-concern** across layers:
  - `Domain/Constants/` — permission constants (`Permissions.cs`)
  - `Domain/Rules/` — validation rules (`TaskRules.cs`)
  - `Application/Abstractions/` — service contracts (`IDownstreamApiService`, `ITokenExchangeService`)
  - `Application/Configuration/` — options classes (`DownstreamApiOptions`)
  - `Application/Constants/` — JSON serialization presets (`McpJsonOptions`)
  - `Application/Models/` — tool result model (`McpToolResult`)
  - `Application/UseCases/` — one sealed class per tool operation, organized by domain (`Tasks/`, `Projects/`, `Balances/`, `Admin/`)
  - `Infrastructure/Http/` — HTTP clients (`AuthenticatedApiClient`, `DownstreamApiService`)
  - `Infrastructure/Identity/` — token exchange (`EntraIdTokenExchangeService`)
  - `Infrastructure/Telemetry/` — activity sources, metrics (`McpActivitySource`, `McpMetrics`)
  - `Infrastructure/Health/` — health checks (`EntraIdHealthCheck`)
  - `Server/Tools/` — one sealed class per domain (e.g., `TaskTools`, `ProjectsTools`, `BalancesTools`)
  - `Server/Prompts/` — one sealed class per domain (e.g., `TaskPrompts`, `ProjectPrompts`, `AdminPrompts`)
  - `Server/Extensions/` — DI registration and pipeline setup extension methods
  - `Server/Middleware/` — request pipeline middleware (`McpCorrelationMiddleware`)
  - `Server/Telemetry/` — MCP SDK-dependent filter (`McpTelemetryFilter`)

### No Magic Numbers or Strings

- All permission strings live in `McpServer.Domain/Constants/Permissions.cs`.
- All claim type constants live in `McpServer.Shared/Constants/EntraClaimTypes.cs`.
- All JSON serialization options live in `McpServer.Application/Constants/McpJsonOptions.cs`.
- Validation limits are defined in `McpServer.Domain/Rules/TaskRules.cs` or a shared rules file.
- Route constants are `private const string` fields in the service class (e.g., `DownstreamApiService`).
- Use `nameof(parameter)` instead of string literals when referencing parameter names in exceptions or error messages.

### Constants: `const` vs `static` (SonarQube S3008)

- **Use `public const`** only when the value is required at compile time: attribute arguments (`[Authorize(Roles = ...)]`), switch labels, and default parameter values.
- **Use `public static string { get; }`** (or other types) for all other public constant-like values. This avoids binary-breaking changes and satisfies SonarQube rule S3008. Example: `public static string Name { get; } = "McpServer.Presentation";`
- **`private const`** is acceptable for private fields (e.g., route constants in `DownstreamApiService`) because they are not part of the public API.
- **`nameof()`**: Use `nameof(parameter)` instead of string literals when referencing parameter names in exceptions or error messages (SonarQube S2302).

## 3. MCP Server Tool Implementation

### Tool Class Pattern

Tools live in `McpServer.Presentation/Tools/`. Each is a sealed class per domain. Follow `TaskTools.cs` as the canonical example:

```csharp
[McpServerToolType]
[Authorize]
public sealed class TaskTools(
    GetTasksUseCase getTasksUseCase,
    CreateTaskUseCase createTaskUseCase)
{
    [McpServerTool(Name = "get_tasks", Title = "Get Tasks",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get all tasks for the authenticated user.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public async Task<string> GetTasksAsync(CancellationToken cancellationToken)
    {
        var result = await getTasksUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }
}
```

### Mandatory Rules for Every Tool

1. **`[Authorize]` at class level AND `[Authorize(Roles = Permissions.XXX)]` at method level** — always both.
2. **Inject use cases** (from `Application/UseCases/`), not `IDownstreamApiService`. Each tool method delegates to exactly one use case via `ExecuteAsync()` and returns `result.ToJson()`.
3. **Never call `McpToolResult.Ok/Fail` directly in tools** — use cases return `McpToolResult`, tools just serialize it.
4. **Permission constants from `Permissions` class** (e.g., `Permissions.TASK_READ`, `Permissions.ADMIN_ACCESS`). Compile-time constants from `TaskRules` (e.g., `TaskRules.TitleMaxLength`) are still used in `[MaxLength]` attributes.
5. **Do NOT add** `Stopwatch`, `McpActivitySource`, `McpMetrics.RecordToolInvocation()`, or `McpMetrics.RecordResponseSize()` — all handled by `McpTelemetryFilter`.
6. **Do NOT add** try/catch for general error handling — the `McpTelemetryFilter` handles exception recording, metrics, and logging. Only catch exceptions for tool-specific business logic.
7. **Do NOT add** `McpMetrics.RecordValidationError()` in tools — validation is in use cases; the `McpTelemetryFilter` records tool failures.
8. **Use `[Description]` on parameters** with `[Required]` and `[MaxLength]` for input validation via data annotations.
9. **Use `snake_case` for tool names** in `[McpServerTool(Name = "...")]`.
10. **Set `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld`** accurately on every tool.
11. **Use primary constructors** for dependency injection in tool classes.
12. **All `if`/`else`/`for`/`foreach`/`while` must have curly braces** — even single-line guard clauses (SonarQube S121).
13. **`CancellationToken` without `= default`** in tool methods — the MCP SDK injects it automatically. Exception: when preceded by other optional parameters (e.g., `string priority = "Medium"`), `CancellationToken cancellationToken = default` is required by C#.

### Tool Registration

Register tools in `McpServerExtensions.cs` by chaining `.WithTools<NewTools>()`:

```csharp
services
    .AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithRequestFilters(filters => filters.AddCallToolFilter(McpTelemetryFilter.Create()))
    .WithTools<TaskTools>()
    .WithTools<ProjectsTools>()
    // chain new tools here
```

### Input Validation in Use Cases

Validation logic lives in Application layer use cases, not in tools. Use cases validate all parameters and return structured errors via `McpToolResult.Fail()`:

```csharp
public sealed class CreateTaskUseCase(IDownstreamApiService downstreamApiService)
{
    public async Task<McpToolResult> ExecuteAsync(
        string title,
        string description,
        string priority,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return McpToolResult.Fail(400, "Title is required.", "title");
        }

        if (!TaskRules.IsValidPriority(priority))
        {
            return McpToolResult.Fail(400,
                $"Priority must be one of: {TaskRules.ValidPrioritiesList}", "priority");
        }

        var result = await downstreamApiService.CreateTaskAsync(title, description, priority, cancellationToken);
        return McpToolResult.Ok(result);
    }
}
```

Tools delegate to use cases and return the serialized result:

```csharp
public async Task<string> CreateTaskAsync(
    [Description("Task title"), Required, MaxLength(TaskRules.TitleMaxLength)] string title,
    [Description("Task description"), Required, MinLength(1)] string description,
    [Description("Priority level")] string priority = "Medium",
    CancellationToken cancellationToken = default)
{
    var result = await createTaskUseCase.ExecuteAsync(title, description, priority, cancellationToken);
    return result.ToJson();
}
```

## 4. MCP Server Prompt Implementation

### Prompt Class Pattern

Prompts live in `McpServer.Presentation/Prompts/`. Each returns `ChatMessage` (from `Microsoft.Extensions.AI`):

```csharp
[McpServerPromptType]
[Authorize]
public sealed class TaskPrompts
{
    [McpServerPrompt(Name = "summarize_tasks")]
    [Description("Generate a summary of all user tasks.")]
    [Authorize(Roles = Permissions.TASK_READ)]
    public ChatMessage SummarizeTasks(
        [Description("Optional status filter")] string? status = null)
    {
        var prompt = status is not null
            ? $"Summarize all tasks with status '{status}'."
            : "Summarize all tasks.";

        return new ChatMessage(ChatRole.User, prompt);
    }
}
```

### Mandatory Rules for Every Prompt

1. **`[McpServerPromptType]`** at class level.
2. **`[Authorize]` at class level AND `[Authorize(Roles = Permissions.XXX)]` at method level.**
3. **Return `ChatMessage(ChatRole.User, text)`** — never raw strings.
4. **`[Description]` on class-level `[McpServerPrompt]`** and on each parameter.
5. **Register** in `McpServerExtensions.cs` via `.WithPrompts<NewPrompts>()`.
6. Prompt classes are stateless — no constructor injection needed.

## 5. Authentication and Authorization (Entra ID + OBO)

### JWT Bearer Configuration

Authentication is configured in `AuthenticationExtensions.cs`:

- JWT Bearer scheme with `.AddMcp()` for RFC 9728 `ProtectedResourceMetadata`.
- Valid audiences: both `{clientId}` and `api://{clientId}`.
- `MapInboundClaims = false` to preserve original claim names; when true, use constants from `EntraClaimTypes`.
- Rate limiting: fixed window 100 req/min per user identity or IP.
- CORS: reads `Cors:AllowedOrigins` from config; falls back to localhost dev ports 5230/5231.

### RFC 9728 and RFC 8414 Endpoints

The MCP Server exposes discovery endpoints as anonymous:

- `/.well-known/oauth-protected-resource` — RFC 9728 protected resource metadata.
- `/.well-known/oauth-authorization-server` — RFC 8414 authorization server metadata.

These are registered in `Program.cs` with `.AllowAnonymous()`.

### Token Exchange (OAuth 2.0 On-Behalf-Of)

The MCP Server does **NOT** forward the user's token. It exchanges via `ITokenExchangeService` (MSAL OBO):

```
MCP Client → (JWT aud:api://{server-client-id}) → MCP Server → (OBO via MSAL) → JWT aud:api://{api-client-id} → MockApi
```

- `EntraIdTokenExchangeService` implements `ITokenExchangeService`.
- Uses MSAL `IConfidentialClientApplication.AcquireTokenOnBehalfOf()`.
- MSAL handles token caching automatically (in-memory by default).
- Handles `MsalUiRequiredException` (re-consent needed) and `MsalServiceException` (Entra ID errors).
- Registered via `IdentityProviderExtensions.AddIdentityProvider()`.

### AuthenticatedApiClient (Base HTTP Service)

All downstream API calls go through `AuthenticatedApiClient`, which provides:

- Bearer token extraction from the current HTTP request.
- OBO token exchange via `ITokenExchangeService`.
- HTTP request creation with the exchanged token.
- JSON response parsing to `JsonElement`.
- Convenience methods: `GetAsync()`, `PostAsync()`, `PatchAsync()`, `DeleteAsync()`.

`DownstreamApiService` inherits from `AuthenticatedApiClient` and provides thin domain methods with static route constants:

```csharp
public sealed class DownstreamApiService(
    HttpClient httpClient,
    ITokenExchangeService tokenExchangeService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<DownstreamApiService> logger)
    : AuthenticatedApiClient(httpClient, tokenExchangeService, httpContextAccessor, logger), IDownstreamApiService
{
    private const string TasksRoute = "api/tasks";

    public async Task<JsonElement> GetTasksAsync(CancellationToken cancellationToken)
        => await GetAsync(TasksRoute, cancellationToken);
}
```

### Adding a New Permission

1. Add constant to `McpServer.Domain/Constants/Permissions.cs`.
2. Add matching App Role in `azure-config/mcp-server-roles.json` (MCP Server) and `azure-config/mock-api-roles.json` (MockApi).
3. Create App Role in **both** Entra ID app registrations (MCP Server and MockApi).
4. Assign users/groups in **both** Enterprise Applications.
5. Use the constant in `[Authorize(Roles = Permissions.NEW_PERMISSION)]`.

## 6. Configuration Pattern

### Options Classes

Use .NET Options pattern with `[Required]` DataAnnotations and `ValidateOnStart`:

```csharp
public class DownstreamApiOptions
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    public string[]? Scopes { get; set; }
}
```

### Configuration Hierarchy

```
EntraIdBaseOptions (abstract)     — Instance, TenantId, GetAuthority(), GetValidIssuers()  [Shared/Configuration]
    ├── EntraIdServerOptions       — ClientId, ClientSecret, Scopes (MCP Server)            [Infrastructure/Configuration]
    └── EntraIdApiOptions          — Audience (MockApi)                                     [MockApi/Configuration]
DownstreamApiOptions               — BaseUrl, Audience, Scopes (Application/Configuration/)
```

### Registration and Validation

- Bind from `IConfiguration` sections: `"EntraId"`, `"DownstreamApi"`.
- Use `GetRequiredSection<T>()` extension method for typed, safe binding with clear error messages.
- Call `ValidateDataAnnotations().ValidateOnStart()` in DI registration.
- **Never** read config values directly from `IConfiguration` in service classes — always inject typed options via `IOptions<T>`.

### Environment-Specific Config

- `appsettings.json` — defaults and structure.
- `appsettings.Development.json` — local development overrides (secrets, tenant IDs).
- Aspire sets `DownstreamApi__BaseUrl` via service discovery in `AppHost.cs`.

## 7. Services and Dependency Injection

### Interface-First Design

- Define interfaces in `Application/Abstractions/` (e.g., `IDownstreamApiService`, `ITokenExchangeService`).
- Implement in `Infrastructure/Http/` or `Infrastructure/Identity/`.
- Use cases in `Application/UseCases/` inject interfaces and contain business logic (validation, orchestration). Registered as `AddTransient<>()` in `ApplicationServiceExtensions.AddApplication()`.
- All methods on service interfaces return `Task<JsonElement>` for API-calling services.
- Register via extension methods in `Extensions/` (e.g., `AddInfrastructure()`, `AddPresentation()`).

### HttpClient Registration

Register typed HTTP clients via `AddHttpClient<TInterface, TImplementation>()` in `InfrastructureServiceExtensions.AddInfrastructure()`:

```csharp
services.AddHttpClient<IDownstreamApiService, DownstreamApiService>(client =>
{
    client.BaseAddress = new Uri(downstreamOptions.BaseUrl);
});
```

### Service Lifetimes

- **Scoped**: `ITokenExchangeService` (per-request token exchange).
- **Singleton**: `IConfidentialClientApplication` (MSAL client with internal cache).
- **HttpClient**: managed by `IHttpClientFactory` (via `AddHttpClient<T>()`).

## 8. Error Handling

### In MCP Tools (System Boundary)

- Tools delegate to use cases and return `result.ToJson()`.
- Do NOT validate input in tools — validation lives in use cases (`Application/UseCases/`).
- Do NOT catch general exceptions — `McpTelemetryFilter` handles them.
- Only catch tool-specific business logic exceptions (e.g., mapping specific errors to user messages).

### In Library/Service Code

- Let exceptions propagate naturally.
- Use `ILogger<T>` from `Microsoft.Extensions.Logging` — never `Console.WriteLine()`.
- Never use bare `catch (Exception)` without logging.
- Use specific exception types: `MsalUiRequiredException`, `HttpRequestException`, `InvalidOperationException`.

### Structured Error Responses

Always use `McpToolResult.Fail()` with:

- `statusCode`: HTTP-compatible status code (400, 401, 403, 404, 500).
- `message`: user-facing error description.
- `field` (optional): the parameter that caused the error.
- `retryable` (optional): whether the client should retry.

```csharp
return McpToolResult.Fail(404, "Project not found.", "projectId", retryable: false).ToJson();
```

## 9. Telemetry and Observability

### Architecture

Telemetry is centralized — tools contain only business logic:

- **`McpTelemetryFilter`** (registered via `AddCallToolFilter`) handles all cross-cutting concerns: activity creation, duration metrics, invocation counts, response size, error recording.
- **`McpActivitySource`** — custom `ActivitySource` with MCP semantic conventions (`mcp.tool.name`, `rpc.system`, `enduser.id`, `tenant.id`).
- **`McpMetrics`** — custom `Meter` with counters and histograms (`mcp.tool.invocations`, `mcp.tool.errors`, `mcp.tool.duration`, `mcp.tool.validation.errors`, `mcp.tool.response.size`).
- **`McpCorrelationMiddleware`** — extracts `Mcp-Session-Id`, W3C trace context, and Azure SDK headers into Activity tags.

### What Tools Should/Should NOT Do

| Do in tools                         | Do NOT do in tools                       |
| ----------------------------------- | ---------------------------------------- |
| Delegate to use cases               | Input validation (belongs in use cases)  |
| Return `result.ToJson()`            | `McpToolResult.Ok/Fail` directly         |
| `[Description]`, `[Required]`, etc. | `Stopwatch` / duration timing            |
|                                     | `McpActivitySource.StartActivity()`      |
|                                     | `McpMetrics.RecordToolInvocation()`      |
|                                     | `McpMetrics.RecordResponseSize()`        |
|                                     | `McpMetrics.RecordValidationError()`     |
|                                     | try/catch for general errors             |
|                                     | Inject `ILogger` (telemetry filter logs) |

### Serilog (Logging)

- Configured centrally in `McpServer.ServiceDefaults/Extensions.cs` via `AddSerilogDefaults()`.
- Console sink (custom output template) + File sink (compact JSON, daily roll, 2 MB per file, 5-file retention).
- `writeToProviders: true` bridges Serilog to MEL/OpenTelemetry.
- Log path: `logs/` (dev) or `%HOME%/LogFiles/dotnet` (Azure App Service).
- Use structured logging: `_logger.LogInformation("Task {TaskId} created by {UserId}", taskId, userId);` — never string interpolation in log messages.

### OpenTelemetry

- Traces, metrics, and logs via OTLP.
- OTLP exporter auto-enabled when `OTEL_EXPORTER_OTLP_ENDPOINT` env var is set.
- Custom `ActivitySourceNames` and `MeterNames` registered per service via `ServiceTelemetryOptions`.
- `HealthCheckActivityFilter` suppresses OTLP export for health check endpoints.

### MockApi Telemetry

- `ApiActivitySource` + `ApiMetrics` + `ApiTelemetryFilter` — same pattern as the MCP Server.
- Controller telemetry is auto-instrumented via action filter.

## 10. Middleware Pipeline

### Exact Order (Server `Program.cs`)

```csharp
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMcpCorrelation();    // extracts Mcp-Session-Id, W3C trace context
app.UseAuthorization();
```

This order is critical: authentication must happen before correlation (so user context is available) and before authorization.

### MCP Endpoint Mapping

```csharp
app.MapMcp("/mcp").RequireAuthorization();
```

### Discovery Endpoints (Anonymous)

```csharp
app.MapGet("/.well-known/oauth-protected-resource", ...).AllowAnonymous();
app.MapGet("/.well-known/oauth-authorization-server", ...).AllowAnonymous();
```

### Health Endpoints

```csharp
app.MapDefaultEndpoints();  // /health (readiness) + /alive (liveness)
```

## 11. Async Patterns

### General

- All I/O operations (API calls, token exchange, database) must be `async`.
- Use `CancellationToken` on all async methods that perform I/O.
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — always `await`.
- No `async void` except for event handlers.

### Timeouts and Resilience

- Use Aspire's resilience defaults (Polly-based retry, circuit breaker) configured in `ServiceDefaults`.
- For custom timeouts, use `CancellationTokenSource` with timeout:

```csharp
using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
cts.CancelAfter(TimeSpan.FromSeconds(30));
var result = await service.CallAsync(cts.Token);
```

### HttpClient

- Never instantiate `HttpClient` directly — use `IHttpClientFactory` via `AddHttpClient<T>()`.
- `HttpClient.BaseAddress` is set at registration time from `DownstreamApiOptions.BaseUrl`.

## 12. SOLID / DRY / KISS / YAGNI

### Single Responsibility

- One class, one purpose. One method, one job.
- Tool classes contain only tool methods and input validation.
- `DownstreamApiService` contains only route constants and one-liner domain methods.
- `AuthenticatedApiClient` encapsulates all HTTP/token infrastructure.
- Telemetry filter handles all cross-cutting observability.

### Open/Closed

- Add new MCP tools by creating new sealed classes in `Tools/` and chaining `.WithTools<T>()` — don't modify existing tools.
- Add new prompts by creating new sealed classes in `Prompts/` and chaining `.WithPrompts<T>()`.
- Add new McpApi endpoints by creating new controllers and service methods.

### Dependency Inversion

- Depend on interfaces (`IDownstreamApiService`, `ITokenExchangeService`), not concrete classes.
- Register via extension methods in `Extensions/` — keep `Program.cs` clean.
- Use primary constructors for DI injection.

### DRY

- Shared HTTP infrastructure lives in `AuthenticatedApiClient` (base class).
- Permission strings are centralized in `Permissions.cs` — never hardcode.
- JSON options are centralized in `McpJsonOptions.cs`.
- Telemetry is centralized in `McpTelemetryFilter` — never duplicate in tools.
- When a string literal appears 3+ times, extract to a constant.

### KISS

- Prefer `sealed class` over deep inheritance hierarchies.
- Prefer primary constructors over explicit constructor + field assignment.
- Prefer pattern matching and `switch` expressions over `if/else` chains.
- Keep method **cognitive complexity low** — use guard clauses (early returns) instead of deep nesting.
- **Cyclomatic complexity ≤ 10** per method (SonarQube S1541). Extract helper methods when complexity grows. See `ApiTelemetryFilter` as an example: `EnrichWithUserId()` and `RecordResult()` were extracted to reduce complexity from 16 to ~5.
- Maximum 2-3 levels of nesting in any method.

### YAGNI

- Don't add configuration options nobody has asked for.
- Don't create base classes with a single subclass (exception: `AuthenticatedApiClient` which is designed for multiple downstream services).
- Don't add generic repositories or CQRS unless the domain requires it.
- Build for today's requirements, refactor when new requirements emerge.

## 13. Constants and Shared Types

### Permissions (`McpServer.Domain/Constants/Permissions.cs`)

Permission values **must** remain `public const string` because they are used in `[Authorize(Roles = ...)]` attribute arguments, which require compile-time constants. SonarQube flags these as S3008; mark them as "Won't Fix".

```csharp
public static class Permissions
{
    public const string TASK_READ = "mcp:task:read";
    public const string TASK_WRITE = "mcp:task:write";
    public const string BALANCE_READ = "mcp:balance:read";
    public const string BALANCE_WRITE = "mcp:balance:write";
    public const string PROJECT_READ = "mcp:project:read";
    public const string PROJECT_WRITE = "mcp:project:write";
    public const string ADMIN_ACCESS = "mcp:admin:access";
}
```

Permission values use the `mcp:` prefix and must exactly match Entra ID App Role values.

### Task Validation Rules (`McpServer.Domain/Rules/TaskRules.cs`)

- `TaskRules.TitleMaxLength` (200), `TaskRules.ValidPriorities`, `TaskRules.ValidStatuses`
- `TaskRules.IsValidPriority()`, `TaskRules.IsValidStatus()` for validation checks

### JSON Options (`McpServer.Application/Constants/McpJsonOptions.cs`)

- `McpJsonOptions.WriteIndented` — indented output with camelCase.
- `McpJsonOptions.Compact` — no indentation with camelCase.

### Claim Types (`McpServer.Shared/Constants/EntraClaimTypes.cs`)

Defined as `public static string { get; }` (not `const`) because they are not used in attribute arguments. Use when `MapInboundClaims = true` remaps standard JWT claims.

## 14. Security

### Authentication

- JWT Bearer with Entra ID — configured via `AuthenticationExtensions.AddMcpAuthentication()` in `Server/Extensions/`.
- Validate both `{clientId}` and `api://{clientId}` audiences.
- `JwtBearerEventFactory` provides standardized logging for auth events.
- Never bypass `[Authorize]` on tools or prompts.

### Authorization

- Role-based via `[Authorize(Roles = Permissions.XXX)]`.
- Authorization filters registered via `.AddAuthorizationFilters()` in `McpServerExtensions.cs`.
- Roles are propagated through OBO — defined in **both** app registrations.

### Security Rules

- Never use `allow_origins=["*"]` in production CORS configuration.
- Never expose internal paths, stack traces, or system details in error responses.
- Never log tokens, secrets, or credentials — even at Debug level.
- Sanitize all user input before using in queries or string formatting.
- All secrets in `appsettings.Development.json` (gitignored) or Azure App Service config.
- Rate limiting: 100 req/min per user identity or IP (configured in `AuthenticationExtensions.cs`).

## 15. Testing

### Test Framework

- Use `xUnit` with `Microsoft.AspNetCore.Mvc.Testing` for integration tests.
- Use `Moq` or `NSubstitute` for mocking interfaces.
- Use `FluentAssertions` for readable assertions.

### Test Organization

- One test class per production class: `TaskToolsTests`, `DownstreamApiServiceTests`.
- Group tests by method: nested classes or descriptive method names.
- Name test methods: `MethodName_Scenario_ExpectedResult` (e.g., `GetTasks_WithValidToken_ReturnsOkResult`).

### Test Patterns

- **Arrange-Act-Assert** (AAA) pattern in all tests.
- Mock `IDownstreamApiService` for tool unit tests — don't call real APIs.
- Test all `[Authorize(Roles)]` constraints.
- Test `McpToolResult.Ok()` and `McpToolResult.Fail()` responses.
- Test input validation paths and edge cases.

## 16. Adding New Features

### New MCP Tool

1. Create sealed class in `Tools/` following the `TaskTools.cs` pattern.
2. Register in `McpServerExtensions.cs` → chain `.WithTools<NewTools>()`.
3. Add permission constant to `Permissions.cs` if needed.
4. Add matching App Role in `azure-config/mcp-server-roles.json` (MCP Server) and `azure-config/mock-api-roles.json` (MockApi), then apply to both Entra ID app registrations.
5. Add corresponding method to `IDownstreamApiService` (in `Application/Abstractions/`) + `DownstreamApiService` (in `Infrastructure/Http/`) if the tool calls the downstream API.

### New MCP Prompt

1. Create class in `Prompts/` with `[McpServerPromptType]` + `[Authorize]`.
2. Register in `McpServerExtensions.cs` → chain `.WithPrompts<NewPrompts>()`.

### New MockApi Endpoint

1. Add controller in `MockApi/Controllers/` with `[Route("api/[controller]")]`.
2. Add service interface + implementation in `MockApi/Services/`.
3. Register scoped service in `MockApi/Program.cs`.
4. Add corresponding method to `IDownstreamApiService` (in `Application/Abstractions/`) + `DownstreamApiService` (in `Infrastructure/Http/`).

### New Downstream Service

1. Create options class in `Application/Configuration/`.
2. Create interface in `Application/Abstractions/`.
3. Create implementation extending `AuthenticatedApiClient` in `Infrastructure/Http/`.
4. Register via `AddHttpClient<TInterface, TImpl>()` in an extension method.

## 17. Aspire Orchestration

### AppHost Pattern

- `McpServer.AppHost/AppHost.cs` wires all services with service discovery.
- Environment variables are passed via `.WithEnvironment()` or `.WithReference()`.
- `DownstreamApi__BaseUrl` is set via Aspire service discovery.
- Start the entire stack: `cd McpServer.AppHost && dotnet run`.

### Service Defaults

- `McpServer.ServiceDefaults/Extensions.cs` centralizes:
  - Service discovery, resilience, and health checks.
  - OpenTelemetry registration (traces, metrics, logs).
  - Serilog configuration (console + file sinks).
  - Custom `ActivitySource` and `Meter` registration per service via `ServiceTelemetryOptions`.

## 18. Summary Checklist

When generating C# code for this project, ensure:

- [ ] `sealed class` by default for tools, prompts, services, middleware
- [ ] `[Authorize]` at class AND method level on all tools and prompts
- [ ] Permission values from `Permissions.cs` constants — never hardcoded
- [ ] `McpToolResult.Ok(JsonElement).ToJson()` / `McpToolResult.Fail()` for all tool returns
- [ ] No telemetry boilerplate in tools — `McpTelemetryFilter` handles it
- [ ] `IDownstreamApiService` for downstream calls — never raw `HttpClient`
- [ ] `CancellationToken` on all async I/O methods
- [ ] Structured logging with `ILogger<T>` — no string interpolation in log templates
- [ ] Nullable reference types enabled — no suppression without justification
- [ ] Primary constructors for DI
- [ ] File-scoped namespaces
- [ ] **All control structures have curly braces** — even single-line bodies (SonarQube S121)
- [ ] `public const` only for attribute arguments; `public static T { get; }` for other public constants (SonarQube S3008)
- [ ] `nameof()` for parameter names in exceptions and error messages
- [ ] Cyclomatic complexity ≤ 10 per method (SonarQube S1541)
- [ ] `CancellationToken` without `= default` in tool methods (unless preceded by other optional params)
- [ ] Constants for all repeated strings and magic numbers
- [ ] Register new tools/prompts via chaining in `McpServerExtensions.cs`
- [ ] No `.md` documentation files unless explicitly requested
