---
name: "MCP Infrastructure: Authentication and Configuration"
description: "Authentication/authorization (Entra ID + OBO) and configuration patterns for McpServer.Infrastructure. Covers JWT Bearer, OBO token exchange, AuthenticatedApiClient, Options classes, and configuration hierarchy."
applyTo: "src/MCP-Server/McpServer.Infrastructure/**/*.cs"
---

# MCP Infrastructure: Authentication and Configuration

## 1. Authentication and Authorization (Entra ID + OBO)

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

## 2. Configuration Pattern

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
