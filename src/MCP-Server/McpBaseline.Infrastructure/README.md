# McpBaseline.Infrastructure — Implementations and External Dependencies

## Overview

Infrastructure layer in the Clean Architecture. Implements the contracts defined in `McpBaseline.Application` and owns all external dependencies: HTTP clients, MSAL token exchange, health checks, and OpenTelemetry instrumentation. No other layer references external packages directly (except Presentation for MCP SDK and Aspire).

- **Target Framework**: .NET 10
- **NuGet packages**: `Microsoft.Identity.Client` v4.83.1
- **Framework references**: `Microsoft.AspNetCore.App`
- **Project references**: `McpBaseline.Application`, `McpBaseline.Shared`

## Contents

### Configuration (`Configuration/`)

#### EntraIdServerOptions (`Configuration/EntraIdServerOptions.cs`)

Microsoft Entra ID configuration for the MCP Server (confidential client). Inherits from `EntraIdBaseOptions` (in `McpBaseline.Shared/Configuration/`). Contains `ClientId`, `ClientSecret`, `Scopes`, and optional `ResourceDocumentation` for RFC 9728. Registered with `[Required]` + `ValidateOnStart()` in `IdentityProviderExtensions`.

### HTTP Clients (`Http/`)

#### ApiTokenProvider (`Http/ApiTokenProvider.cs`)

Sealed class responsible for token acquisition. Extracts the caller's `Bearer` token from the current HTTP context and exchanges it for a downstream API access token via OBO (On-Behalf-Of / RFC 8693). Token passthrough is not supported.

| Method            | Purpose                                                                                                             |
| ----------------- | ------------------------------------------------------------------------------------------------------------------- |
| `GetTokenAsync()` | Reads `Authorization` header, runs OBO exchange via `ITokenExchangeService`, returns the downstream token or `null` |

Registered as `AddScoped<ApiTokenProvider>()` in `InfrastructureServiceExtensions` so DI can inject it into `DownstreamApiService`.

#### AuthenticatedApiClient (`Http/AuthenticatedApiClient.cs`)

Abstract base class for all authenticated downstream API clients. Delegates token acquisition to `ApiTokenProvider` and owns only HTTP execution and response parsing.

| Responsibility      | Method / Flow                                                                                                 |
| ------------------- | ------------------------------------------------------------------------------------------------------------- |
| Request creation    | Builds `HttpRequestMessage` with bearer auth and optional JSON body; calls `ApiTokenProvider.GetTokenAsync()` |
| Response parsing    | Executes requests, parses JSON responses, handles errors                                                      |
| Convenience methods | `GetAsync`, `PostAsync`, `PatchAsync`, `DeleteAsync`                                                          |

Token passthrough is **not supported**. Every call goes through OBO exchange; if exchange fails, the request fails with an error. See `McpBaseline.Presentation/README.md` § OBO Security Posture.

#### DownstreamApiService (`Http/DownstreamApiService.cs`)

- **Implements**: `IDownstreamApiService` (from Application)
- **Inherits**: `AuthenticatedApiClient`
- **Lifetime**: Scoped (via `AddHttpClient<IDownstreamApiService, DownstreamApiService>()`)
- **Pattern**: Thin domain class with route constants and one-liner methods; constructor takes `HttpClient`, `ApiTokenProvider`, and `ILogger<DownstreamApiService>`
- **Base URL**: Set from `DownstreamApiOptions.BaseUrl` at registration time

Each method is a single line delegating to the base class convenience methods:

```csharp
public Task<JsonElement> GetProjectsAsync(CancellationToken ct) => GetAsync("/api/projects", ct);
```

### Identity (`Identity/`)

#### EntraIdTokenExchangeService (`Identity/EntraIdTokenExchangeService.cs`)

- **Implements**: `ITokenExchangeService` (from Application)
- **Protocol**: OAuth 2.0 On-Behalf-Of (OBO) flow via MSAL's `AcquireTokenOnBehalfOf()`
- **Lifetime**: Scoped (via DI)
- **Scopes**: Configured via `DownstreamApiOptions.Scopes` or defaults to `api://{audience}/.default`
- **Caching**: MSAL handles token caching automatically, partitioned by SHA-256 hash of the `UserAssertion`
- **Error handling**: Returns `null` on `MsalUiRequiredException` (consent/MFA required) or `MsalServiceException`

Registration is handled by `IdentityProviderExtensions.AddIdentityProvider()` (internal), which configures `EntraIdServerOptions`, creates the MSAL `IConfidentialClientApplication` singleton, and registers the token exchange service.

### Telemetry (`Telemetry/`)

Custom OpenTelemetry instrumentation for MCP tool-level observability. Both classes are registered in the OTel SDK via `ServiceTelemetryOptions` in `Program.cs`.

#### McpActivitySource (`Telemetry/McpActivitySource.cs`)

ActivitySource for tool execution tracing with MCP semantic convention attributes.

| Method                    | Purpose                                               |
| ------------------------- | ----------------------------------------------------- |
| `StartToolActivity(name)` | Creates `mcp.tool.{name}` span with RPC/MCP tags      |
| `EnrichWithUserContext()` | Sets `enduser.id`, `enduser.roles`, `tenant.id`, etc. |
| `RecordError()`           | Tags span with error details on exception             |

Uses `oid` (not `sub`) as `enduser.id` for cross-service span correlation. See `McpBaseline.Presentation/README.md` § Span Enrichment for the full claim mapping table.

#### McpMetrics (`Telemetry/McpMetrics.cs`)

Meter for tool invocation metrics.

| Metric                       | Type      | Description                   |
| ---------------------------- | --------- | ----------------------------- |
| `mcp.tool.invocations`       | Counter   | Total tool invocations        |
| `mcp.tool.errors`            | Counter   | Total tool errors             |
| `mcp.tool.duration`          | Histogram | Execution time (ms)           |
| `mcp.tool.validation.errors` | Counter   | Input validation failures     |
| `mcp.tool.response.size`     | Histogram | Response payload size (bytes) |

`McpMetrics.RecordValidationError()` is called directly by tool methods for business-logic validation. All other metrics are recorded by `McpTelemetryFilter` (in Presentation).

### Health (`Health/`)

#### EntraIdHealthCheck (`Health/EntraIdHealthCheck.cs`)

Readiness health check that verifies connectivity to Microsoft Entra ID's OpenID Connect discovery endpoint. Tagged `"ready"` so it runs on `/health` but not on `/alive`.

Uses a dedicated named `HttpClient` (`EntraIdHealthCheck.HttpClientName`) with a 5-second timeout, registered in `InfrastructureServiceExtensions`.

### Service Registration (`Extensions/`)

#### InfrastructureServiceExtensions (`Extensions/InfrastructureServiceExtensions.cs`)

Public entry point for Infrastructure DI registration:

```csharp
services.AddInfrastructure(configuration);
```

Registers:

1. Identity provider (MSAL client, OBO token exchange, `EntraIdServerOptions`)
2. `ApiTokenProvider` as scoped (token extraction and OBO exchange, consumed by `DownstreamApiService`)
3. `IDownstreamApiService` → `DownstreamApiService` via `AddHttpClient` with base URL from `DownstreamApiOptions`
4. Named `HttpClient` for Entra ID health checks (5s timeout)
5. `EntraIdHealthCheck` tagged `"ready"`

#### IdentityProviderExtensions (`Extensions/IdentityProviderExtensions.cs`)

Internal helper called by `AddInfrastructure()`. Binds `EntraIdServerOptions` and `DownstreamApiOptions` from configuration, creates the MSAL `IConfidentialClientApplication` singleton, and registers `EntraIdTokenExchangeService`.

## Design Decisions

1. **Infrastructure owns `EntraIdServerOptions`**: The MCP Server-specific Entra ID configuration lives in `Infrastructure/Configuration/`, inheriting from `EntraIdBaseOptions` (in `McpBaseline.Shared`). This reflects the production topology where the MCP Server and backend API are separate repositories; each project owns its concrete options class. `McpBaseline.Shared` retains only the abstract base class.

2. **Token responsibility split between `ApiTokenProvider` and `AuthenticatedApiClient`**: `ApiTokenProvider` owns bearer extraction and OBO exchange (depends on `IHttpContextAccessor`, `ITokenExchangeService`, `DownstreamApiOptions`). `AuthenticatedApiClient` owns HTTP execution and response parsing (depends only on `HttpClient`, `ApiTokenProvider`, `ILogger`). This keeps each class below the SonarQube coupling threshold and follows SRP.

3. **No interface for `ApiTokenProvider`**: Both the provider and its consumer live in the `Infrastructure` layer; no outer layer depends on it. An interface would be YAGNI. `ApiTokenProvider` is registered as a concrete type: `AddScoped<ApiTokenProvider>()`.

4. **Telemetry in Infrastructure, filter in Presentation**: `McpActivitySource` and `McpMetrics` are instrumentation primitives (counters, histograms, activity sources) with no MCP SDK dependency. `McpTelemetryFilter`, which orchestrates them per tool call, lives in Server because it depends on `ModelContextProtocol.Protocol` types (`CallToolRequestParams`, `CallToolResult`).

5. **Internal extension methods**: `IdentityProviderExtensions` is `internal` because it is an implementation detail of `AddInfrastructure()`. Only the public `InfrastructureServiceExtensions.AddInfrastructure()` is visible to the composition root.

6. **Named HttpClient for health checks**: The health check client is separate from the `DownstreamApiService` client to avoid handler staleness and allow independent timeout configuration.

## Dependency Graph

```
McpBaseline.Domain
    ↑
McpBaseline.Application
    ↑
McpBaseline.Infrastructure  (this project)
    ↑
McpBaseline.Presentation
```

For Application contract details, see `McpBaseline.Application/README.md`.
For Domain constants and rules, see `McpBaseline.Domain/README.md`.
