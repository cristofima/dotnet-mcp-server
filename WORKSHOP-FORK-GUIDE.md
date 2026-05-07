# Workshop Fork Guide

Instructions for adapting `az-api-oper-mcp-server-dotnet` into a standalone workshop repo.

The workshop repo is a simplified fork of this one. The table below summarizes every divergence.

| #   | Change                                                | Affected area                   |
| --- | ----------------------------------------------------- | ------------------------------- |
| 1   | Replace Datadog / DogStatsD with Application Insights | `McpServer.ServiceDefaults`   |
| 2   | Replace EF Core InMemory with SQL Server              | `McpServer.BackendApi`           |
| 3   | Remove Entra ID JWT auth from MCP Server              | `McpServer.Presentation`      |
| 4   | Remove Entra ID JWT auth from MockApi                 | `McpServer.BackendApi`           |
| 5   | Remove MSAL OBO token exchange                        | `McpServer.Infrastructure`    |
| 6   | Implement `transfer_budget` (H-i-t-L demo tool)       | MockApi + MCP Server all layers |
| 7   | Keep AppHost for local testing (no changes needed)    | `McpServer.AppHost`           |
| 8   | Simplify rate limiting (no per-user OID partition)    | `McpServer.Presentation`      |

---

## 1. Application Insights

Replace the Datadog DogStatsD bridge with Azure Monitor / Application Insights via OpenTelemetry.

### 1.1 `McpServer.ServiceDefaults.csproj`

Remove:

```xml
<PackageReference Include="DogStatsD-CSharp-Client" Version="9.1.0" />
```

Add:

```xml
<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.*" />
```

### 1.2 `ServiceDefaults/Extensions/HostApplicationBuilderExtensions.cs`

Replace the `AddOpenTelemetryExporters` private method:

```csharp
private void AddOpenTelemetryExporters(ServiceTelemetryOptions telemetryOptions)
{
    // OTLP exporter (Aspire dashboard, Grafana, etc.)
    var useOtlpExporter = !string.IsNullOrWhiteSpace(
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    // Application Insights — activated by APPLICATIONINSIGHTS_CONNECTION_STRING
    var appInsightsConnStr = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    if (!string.IsNullOrWhiteSpace(appInsightsConnStr))
    {
        builder.Services.AddOpenTelemetry().UseAzureMonitor();
    }
}
```

Remove the using for `StatsdClient`. Add:

```csharp
using Azure.Monitor.OpenTelemetry.Extensions;
```

### 1.3 Delete `ServiceDefaults/Telemetry/DogStatsDMetricBridge.cs`

This file is entirely Datadog-specific. Delete it; nothing else in the solution references it after step 1.2.

### 1.4 Environment variable

Add to both service `.env` / App Service configuration:

```env
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=<key>;IngestionEndpoint=https://...
```

When `APPLICATIONINSIGHTS_CONNECTION_STRING` is absent the exporter is simply not registered — no error.

---

## 2. SQL Server

### 2.1 `McpServer.BackendApi.csproj`

Remove:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.7" />
```

Add:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.7" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.7">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

### 2.2 `MockApi/Program.cs`

Replace:

```csharp
builder.Services.AddDbContext<MockApiDbContext>(options =>
    options.UseInMemoryDatabase("IADB"));
```

With:

```csharp
builder.Services.AddDbContext<MockApiDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MockApiDb")));
```

Remove `using Microsoft.EntityFrameworkCore;` (already present) and ensure `using Microsoft.EntityFrameworkCore;` is the only EF namespace needed (no more `InMemory` namespace).

### 2.3 `MockApi/appsettings.Development.json`

Add a connection string block:

```json
{
  "ConnectionStrings": {
    "MockApiDb": "Server=(localdb)\\mssqllocaldb;Database=McpWorkshop;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

For Azure SQL use:

```json
"MockApiDb": "Server=<server>.database.windows.net;Database=McpWorkshop;Authentication=Active Directory Default"
```

### 2.4 `MockApi/Data/DatabaseSeedingService.cs`

Replace `EnsureCreatedAsync` with `MigrateAsync` so pending migrations are applied at startup:

```csharp
public async Task StartAsync(CancellationToken cancellationToken)
{
    logger.LogInformation("Applying migrations and seeding demo data...");

    using var scope = serviceProvider.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MockApiDbContext>();

    await context.Database.MigrateAsync(cancellationToken);

    await Task.Run(() => DbSeeder.SeedData(context), cancellationToken);

    logger.LogInformation("Database ready");
}
```

`DbSeeder.SeedData` already guards every entity type with `if (context.X.Any()) return;` so it is safe to call on every startup.

### 2.5 Add initial EF Core migration

```powershell
cd src/McpServer.BackendApi
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

Commit the generated `Migrations/` folder. The migration runs automatically on startup via `MigrateAsync`.

---

## 3. Remove Entra ID from the MCP Server

The workshop MCP Server is anonymous — no JWT, no rate limiting per identity.

### 3.1 `Presentation/Program.cs`

Replace the full middleware and endpoint block:

```csharp
// Before (production)
app.UseSerilogRequestLogging();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMcpCorrelation();
app.UseAuthorization();
app.MapWellKnownEndpoints();
app.MapMcp("/mcp").RequireAuthorization();
app.MapDefaultEndpoints();
```

```csharp
// After (workshop)
app.UseSerilogRequestLogging();
app.UseCors();
app.UseMcpCorrelation();
app.MapMcp("/mcp");
app.MapDefaultEndpoints();
```

Remove the `AddPresentation` call for auth and rate limiting (see 3.2).

### 3.2 `Presentation/Extensions/PresentationServiceExtensions.cs`

Remove from `AddPresentation`:

```csharp
services.AddMcpAuthentication(configuration, environment);
services.AddMcpRateLimiting(configuration);
services.AddAuthorization();
```

Replace CORS with a permissive development policy:

```csharp
services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
```

### 3.3 `Presentation/Extensions/AuthenticationExtensions.cs`

Delete or leave empty. Remove its call from `AddPresentation`.

### 3.4 `Presentation/Extensions/WellKnownEndpointExtensions.cs`

Delete. Not needed without RFC 9728 protected resource metadata.

### 3.5 Remove `[Authorize]` from all tool and prompt classes

In every file under `Presentation/Tools/` and `Presentation/Prompts/`, remove:

```csharp
[Authorize]                             // class-level
[Authorize(Roles = Permissions.XXX)]    // method-level
```

### 3.6 `Presentation/appsettings.json`

Remove the `EntraId` section entirely. Keep `DownstreamApi`, `Cors`, and `RateLimit` (the last two are harmless even if not used).

---

## 4. Remove Entra ID from MockApi

### 4.1 `MockApi/Program.cs`

Remove:

```csharp
builder.Services.AddOptions<EntraIdApiOptions>()
    .Bind(builder.Configuration.GetSection(EntraIdBaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddConfiguredAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAuthorization();
```

And in the middleware pipeline:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Remove the related `using` directives.

### 4.2 `MockApi/Extensions/AuthenticationExtensions.cs`

Delete the file. It is no longer called.

### 4.3 Remove `[Authorize]` from controllers

In every file under `MockApi/Controllers/`, remove the class-level and method-level `[Authorize]` attributes.

### 4.4 `MockApi/appsettings.json`

Remove the `EntraId` section.

---

## 5. Remove MSAL OBO Token Exchange

The MCP Server calls MockApi directly with no auth header. The entire MSAL/OBO layer is removed.

### 5.1 `Infrastructure/Extensions/InfrastructureServiceExtensions.cs`

Remove:

```csharp
services.AddIdentityProvider(configuration);
services.AddScoped<ApiTokenProvider>();
```

### 5.2 Replace `AuthenticatedApiClient` with a plain HTTP base class

`DownstreamApiService` currently inherits from `AuthenticatedApiClient`, which handles token extraction and OBO exchange. For the workshop, replace the base class with a simple wrapper.

Create `Infrastructure/Http/SimpleHttpApiClient.cs`:

```csharp
namespace McpServer.Infrastructure.Http;

public abstract class SimpleHttpApiClient(HttpClient httpClient)
{
    protected async Task<TResult?> GetAsync<TResult>(string path, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: cancellationToken);
    }

    protected async Task<TResult?> PostAsync<TBody, TResult>(
        string path, TBody body, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResult>(cancellationToken: cancellationToken);
    }
}
```

Update `DownstreamApiService` to inherit from `SimpleHttpApiClient` instead of `AuthenticatedApiClient`. Remove every method that called `GetTokenAsync()` or `CreateAuthenticatedRequestAsync()`.

### 5.3 Delete `Infrastructure/Identity/`

The entire folder (`EntraIdTokenExchangeService`, `IdentityProviderExtensions`, `ApiTokenProvider`) can be deleted.

### 5.4 `Infrastructure/Http/AuthenticatedApiClient.cs`

Delete. No longer referenced.

### 5.5 `Infrastructure.csproj`

Remove:

```xml
<PackageReference Include="Microsoft.Identity.Client" Version="4.83.3" />
```

### 5.6 `Presentation/appsettings.json` — remove `DownstreamApi.Audience` and `DownstreamApi.Scopes`

Only `DownstreamApi.BaseUrl` is needed:

```json
"DownstreamApi": {
  "BaseUrl": "http://localhost:5050"
}
```

---

## 6. Implement `transfer_budget`

This is the core H-i-t-L demo tool. Follow these steps in order.

### 6.1 MockApi — `TransferRequest` model

Create `MockApi/Models/Requests/TransferRequest.cs`:

```csharp
namespace McpServer.BackendApi.Models.Requests;

public sealed record TransferRequest(
    string SourceProjectId,
    string TargetProjectId,
    decimal Amount);
```

### 6.2 MockApi — `TransferResult` model

Create `MockApi/Models/Responses/TransferResult.cs`:

```csharp
namespace McpServer.BackendApi.Models.Responses;

public sealed record TransferResult(
    bool Success,
    string? ErrorMessage,
    decimal UpdatedSourceBalance,
    decimal UpdatedTargetBalance);
```

### 6.3 MockApi — `IBalanceService`

Add to `MockApi/Services/IBalanceService.cs`:

```csharp
Task<TransferResult> TransferAsync(
    string sourceProjectId,
    string targetProjectId,
    decimal amount,
    CancellationToken cancellationToken);
```

### 6.4 MockApi — `BalanceService`

Add the implementation to `MockApi/Services/BalanceService.cs`:

```csharp
public async Task<TransferResult> TransferAsync(
    string sourceProjectId,
    string targetProjectId,
    decimal amount,
    CancellationToken cancellationToken)
{
    var source = await context.Balances
        .FirstOrDefaultAsync(b => b.ProjectNumber == sourceProjectId, cancellationToken);
    var target = await context.Balances
        .FirstOrDefaultAsync(b => b.ProjectNumber == targetProjectId, cancellationToken);

    if (source == null)
    {
        return new TransferResult(false, $"Source project {sourceProjectId} not found.", 0, 0);
    }

    if (target == null)
    {
        return new TransferResult(false, $"Target project {targetProjectId} not found.", 0, 0);
    }

    if (source.Available < amount)
    {
        return new TransferResult(
            false,
            $"Insufficient available balance in {sourceProjectId}. Available: {source.Available:N0}.",
            source.Available,
            target.Available);
    }

    source.Available -= amount;
    target.Available += amount;
    source.LastUpdated = DateTimeOffset.UtcNow;
    target.LastUpdated = DateTimeOffset.UtcNow;

    await context.SaveChangesAsync(cancellationToken);

    return new TransferResult(true, null, source.Available, target.Available);
}
```

> Note: `BalanceEntity.Available` must be a persisted column, not a computed property. Verify the entity configuration in `Data/EntityConfigurations/`. If `Available` is computed as `Allocated - Spent - Committed`, add a dedicated `Available` column and update the seeder and entity accordingly.

### 6.5 MockApi — `BalancesController`

Add the endpoint to `MockApi/Controllers/BalancesController.cs`:

```csharp
[HttpPost("transfer")]
public async Task<IActionResult> Transfer(
    [FromBody] TransferRequest request,
    CancellationToken cancellationToken)
{
    var result = await _balanceService.TransferAsync(
        request.SourceProjectId,
        request.TargetProjectId,
        request.Amount,
        cancellationToken);

    if (!result.Success)
    {
        return BadRequest(new { error = result.ErrorMessage });
    }

    return Ok(new
    {
        message = $"Transfer completed: ${request.Amount:N0} from {request.SourceProjectId} to {request.TargetProjectId}",
        sourceBalance = result.UpdatedSourceBalance,
        targetBalance = result.UpdatedTargetBalance
    });
}
```

Add `using McpServer.BackendApi.Models.Requests;` to the controller file.

### 6.6 MCP Server Application — `IDownstreamApiService`

Add to `Application/Abstractions/IDownstreamApiService.cs`:

```csharp
Task<McpToolResult> TransferBudgetAsync(
    string sourceProjectId,
    string targetProjectId,
    decimal amount,
    CancellationToken cancellationToken);
```

### 6.7 MCP Server Infrastructure — `DownstreamApiService`

Add the route constant and implementation to `Infrastructure/Http/DownstreamApiService.cs`:

```csharp
private const string TransferRoute = "api/balances/transfer";

public async Task<McpToolResult> TransferBudgetAsync(
    string sourceProjectId,
    string targetProjectId,
    decimal amount,
    CancellationToken cancellationToken)
{
    var body = new { sourceProjectId, targetProjectId, amount };
    var result = await PostAsync<object, JsonElement>(TransferRoute, body, cancellationToken);
    return McpToolResult.Ok(result);
}
```

Handle `HttpRequestException` for non-2xx responses and return `McpToolResult.Fail(...)` accordingly. Follow the existing pattern in `DownstreamApiService`.

### 6.8 MCP Server Application — `TransferBudgetUseCase`

Create `Application/UseCases/Balances/TransferBudgetUseCase.cs`:

```csharp
using McpServer.Application.Abstractions;
using McpServer.Application.Models;
using McpServer.Domain.Constants;

namespace McpServer.Application.UseCases.Balances;

public sealed class TransferBudgetUseCase(IDownstreamApiService api)
{
    public async Task<McpToolResult> ExecuteAsync(
        string sourceProjectId,
        string targetProjectId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceProjectId))
        {
            return McpToolResult.Fail(400, "sourceProjectId is required.", nameof(sourceProjectId));
        }

        if (string.IsNullOrWhiteSpace(targetProjectId))
        {
            return McpToolResult.Fail(400, "targetProjectId is required.", nameof(targetProjectId));
        }

        if (sourceProjectId.Equals(targetProjectId, StringComparison.OrdinalIgnoreCase))
        {
            return McpToolResult.Fail(400, "Source and target projects must be different.", nameof(targetProjectId));
        }

        if (amount <= 0)
        {
            return McpToolResult.Fail(400, "Amount must be greater than zero.", nameof(amount));
        }

        return await api.TransferBudgetAsync(sourceProjectId, targetProjectId, amount, cancellationToken);
    }
}
```

### 6.9 MCP Server Application — Register use case

In `Application/ApplicationServiceExtensions.cs`, add:

```csharp
services.AddTransient<TransferBudgetUseCase>();
```

### 6.10 MCP Server Presentation — `BalancesTools`

Add the tool method to `Presentation/Tools/BalancesTools.cs`:

```csharp
[McpServerTool(
    Name = "transfer_budget",
    Title = "Transfer Budget Between Projects",
    ReadOnly = false,
    Destructive = true,
    Idempotent = false,
    OpenWorld = false)]
[Description("Transfers budget from one project to another. ALWAYS ask for explicit user confirmation before calling this tool.")]
public async Task<string> TransferBudget(
    [Description("Source project ID (budget is deducted from here)"), Required] string sourceProjectId,
    [Description("Target project ID (budget is added here)"), Required] string targetProjectId,
    [Description("Amount to transfer in USD (must be positive)"), Required, Range(1, 1_000_000)] decimal amount,
    CancellationToken cancellationToken)
{
    var result = await transferBudgetUseCase.ExecuteAsync(
        sourceProjectId, targetProjectId, amount, cancellationToken);
    return result.ToJson();
}
```

Inject `TransferBudgetUseCase transferBudgetUseCase` in the primary constructor of `BalancesTools`.

### 6.11 Register the updated `BalancesTools`

`BalancesTools` is already registered via `.WithTools<BalancesTools>()` in `McpServerExtensions.cs`. No change needed there — the new method is picked up automatically.

---

## 7. AppHost for local testing

Keep `McpServer.AppHost` as-is. It remains the recommended way to run both services together locally: it wires service discovery, injects `DownstreamApi__BaseUrl` automatically, and opens the Aspire dashboard for traces and logs.

### 7.1 Start everything with one command

```powershell
cd src/McpServer.AppHost
dotnet run
```

Aspire prints the dashboard URL and the ports for both services at startup.

### 7.2 `AppHost.cs` — no changes required

The existing wiring already handles `DownstreamApi__BaseUrl` via `WithEnvironment`. No edits needed after removing auth from both services.

### 7.3 Running services standalone (optional fallback)

If a workshop participant cannot install the Aspire workload, both services can still run independently:

```powershell
# Terminal 1 — MockApi (port from launchSettings.json)
cd src/McpServer.BackendApi
dotnet run

# Terminal 2 — MCP Server
cd src/MCP-Server/McpServer.Presentation
$env:DownstreamApi__BaseUrl = "http://localhost:5050"
dotnet run
```

Set the MockApi HTTP port in `MockApi/Properties/launchSettings.json` and match it in the env var above.

---

## 8. Simplify Rate Limiting

The production rate limiter partitions by Entra ID OID claim. Without auth, fall back to IP-based limiting or remove it.

### Option A — Remove rate limiting entirely

In `Presentation/Program.cs`, remove `app.UseRateLimiter()`.  
In `PresentationServiceExtensions.cs`, remove `services.AddMcpRateLimiting(configuration)`.

### Option B — IP-based fixed window (safe default)

In `AuthenticationExtensions.cs` (Presentation), replace the partition key logic:

```csharp
// Before: partition by OID claim
partitionKey = context.User.FindFirst("oid")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

// After: always use IP
partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
```

---

## Appendix: Re-enabling Entra ID authentication

The workshop removes auth to reduce setup complexity. When adapting this fork back to a secured environment, the steps below summarise what to restore. They are the reverse of sections 3–5.

**MCP Server**

- Add back `EntraId` config section in `appsettings.json` (`Instance`, `TenantId`, `ClientId`, `ClientSecret`, `Scopes`, `ResourceDocumentation`).
- Restore `AddMcpAuthentication(configuration, environment)` and `AddMcpRateLimiting(configuration)` inside `AddPresentation`.
- Restore middleware: `UseRateLimiter()`, `UseAuthentication()`, `UseAuthorization()`.
- Restore `.RequireAuthorization()` on `MapMcp("/mcp")`.
- Restore `MapWellKnownEndpoints()` for RFC 9728 protected resource metadata.
- Re-add `[Authorize]` at class level and `[Authorize(Roles = Permissions.XXX)]` at method level on every tool and prompt class.

**MockApi**

- Add back `EntraId` config section (`Instance`, `TenantId`, `Audience`).
- Restore `AddConfiguredAuthentication` and `AddAuthorization` in `Program.cs`.
- Restore `UseAuthentication()` and `UseAuthorization()` in the middleware pipeline.
- Re-add `[Authorize(Roles = Permissions.XXX)]` on controllers.

**Infrastructure (OBO)**

- Add back `Microsoft.Identity.Client` package.
- Restore `Infrastructure/Identity/` folder (copy from the original repo).
- Restore `services.AddIdentityProvider(configuration)` and `services.AddScoped<ApiTokenProvider>()`.
- Revert `DownstreamApiService` base class from `SimpleHttpApiClient` back to `AuthenticatedApiClient`.
- Add back `DownstreamApi.Audience` and `DownstreamApi.Scopes` config values.

See the original repo's [README](../src/MCP-Server/README.md) and [PERMISSION-SETUP-GUIDE.md](PERMISSION-SETUP-GUIDE.md) for Entra ID app registration details.

---

## Environment Variable Summary

Minimum set to run the workshop locally:

**MockApi**

```env
ConnectionStrings__MockApiDb=Server=(localdb)\mssqllocaldb;Database=McpWorkshop;Trusted_Connection=True
APPLICATIONINSIGHTS_CONNECTION_STRING=<optional>
```

**MCP Server**

```env
DownstreamApi__BaseUrl=http://localhost:5050
APPLICATIONINSIGHTS_CONNECTION_STRING=<optional>
```

---

## Checklist

- [ ] `DogStatsDMetricBridge.cs` deleted
- [ ] `DogStatsD-CSharp-Client` package removed from ServiceDefaults
- [ ] `Azure.Monitor.OpenTelemetry.AspNetCore` added to ServiceDefaults
- [ ] `UseAzureMonitor()` wired in `AddOpenTelemetryExporters`
- [ ] `Microsoft.EntityFrameworkCore.InMemory` replaced with `SqlServer` in MockApi
- [ ] `UseInMemoryDatabase` replaced with `UseSqlServer` in MockApi `Program.cs`
- [ ] Connection string added to `appsettings.Development.json`
- [ ] `MigrateAsync` replaces `EnsureCreatedAsync` in `DatabaseSeedingService`
- [ ] EF migration created and committed (`dotnet ef migrations add InitialCreate`)
- [ ] Entra ID removed from MCP Server (`[Authorize]`, JWT Bearer, RFC 9728 endpoints, rate limit by OID)
- [ ] Entra ID removed from MockApi (JWT Bearer, `[Authorize]` on controllers)
- [ ] `AuthenticatedApiClient` / MSAL / OBO replaced with `SimpleHttpApiClient`
- [ ] `Microsoft.Identity.Client` package removed from Infrastructure
- [ ] `TransferRequest` and `TransferResult` models created in MockApi
- [ ] `IBalanceService.TransferAsync` added and implemented
- [ ] `POST /api/balances/transfer` endpoint added to `BalancesController`
- [ ] `IDownstreamApiService.TransferBudgetAsync` added
- [ ] `DownstreamApiService.TransferBudgetAsync` implemented
- [ ] `TransferBudgetUseCase` created and registered in `ApplicationServiceExtensions`
- [ ] `transfer_budget` tool method added to `BalancesTools`
- [ ] AppHost starts cleanly with `dotnet run` from `src/McpServer.AppHost`
- [ ] Both services start cleanly (via AppHost or standalone)
- [ ] MCP Server reachable at `http://localhost:5230/mcp`
- [ ] `transfer_budget` tool appears in MCP tool list
