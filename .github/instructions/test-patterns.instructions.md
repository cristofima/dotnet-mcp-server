---
name: "Test Patterns — Deep Reference"
description: "Detailed test patterns for the MCP OAuth2 Security Baseline: AAA, FakeHttpHandler, TestAuthHandler, MCP client setup, and layer-specific test guides. Load explicitly when writing NEW tests or test infrastructure."
---

# Test Patterns — Deep Reference

> This file is not auto-loaded. Reference it explicitly with `#file:.github/instructions/test-patterns.instructions.md` when generating new tests or test helpers.

## 4. Test Patterns

### Arrange-Act-Assert (AAA)

Every test follows AAA. Use blank lines to separate sections. Comments are optional when the structure is clear.

```csharp
[Fact]
public async Task GetProjectsAsync_Attaches_ExchangedOboToken()
{
    _httpHandler.SetResponse("""[{"id": "P1", "name": "Alpha"}]""");
    var service = CreateService();

    await service.GetProjectsAsync(CancellationToken.None);

    var request = _httpHandler.LastRequest!;
    Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
    Assert.Equal(ExchangedToken, request.Headers.Authorization?.Parameter);
}
```

### `[Fact]` for Single Cases, `[Theory]` for Parameterized

- `[Fact]`: One scenario per test.
- `[Theory]` + `[InlineData]`: Simple parameterized data.
- `[Theory]` + `[MemberData]`: Complex parameterized data (arrays, objects).

```csharp
// ✅ MemberData for complex types
public static TheoryData<string, int, string[]> RoleToolMappings => new()
{
    { Permissions.TASK_WRITE, 3, new[] { "create_task", "delete_task", "update_task_status" } },
    { Permissions.PROJECT_READ, 2, new[] { "get_project_details", "get_projects" } },
};

[Theory]
[MemberData(nameof(RoleToolMappings))]
public async Task ListTools_PerRole_Returns_CorrectToolSubset(
    string role, int expectedCount, string[] expectedToolNames)
{
```

### Single Logical Assertion per Test

Each test verifies one logical behavior. Multiple `Assert` calls are acceptable when they verify different aspects of the **same logical assertion** (e.g., checking both the HTTP method and path of a request).

### CancellationToken

Always pass `CancellationToken.None` explicitly in test calls to async service methods. Do not use `default`.

## 5. Application Layer Tests (Serialization Contracts)

### Purpose

Verify that `McpToolResult` serialization produces the exact JSON contract that MCP clients expect. If serialization breaks, every client consuming tools will misinterpret responses.

### What to Test

| Scenario                              | Why it matters                     |
| ------------------------------------- | ---------------------------------- |
| `Ok()` → `success: true` + `data`     | Core success contract              |
| `Fail()` → `success: false` + `error` | Core error contract                |
| `ValidationError()` → 400 + field     | Client-side validation feedback    |
| `NotFoundError()` → 404               | Resource not found                 |
| `GatewayError()` → 502 + retryable    | Downstream failure with retry hint |
| camelCase property names              | JSON contract convention           |
| Null fields omitted                   | Compact responses                  |
| Round-trip parsing                    | JSON validity guarantee            |

### Pattern

Parse `result.ToJson()` with `JsonDocument` and assert on `RootElement` properties:

```csharp
var result = McpToolResult.Ok(data);
var json = result.ToJson();

using var doc = JsonDocument.Parse(json);
var root = doc.RootElement;

Assert.True(root.GetProperty("success").GetBoolean());
Assert.Equal(1, root.GetProperty("data").GetProperty("id").GetInt32());
```

## 6. Infrastructure Layer Tests (OBO Token Exchange and HTTP Routing)

### Purpose

Verify that `DownstreamApiService` (via `AuthenticatedApiClient`) performs OBO token exchange correctly and routes requests to the right downstream API endpoints with proper HTTP methods and bodies.

### FakeHttpHandler Pattern

Intercept outbound HTTP with a minimal `HttpMessageHandler` fake. No external libraries needed:

```csharp
private sealed class FakeHttpHandler : HttpMessageHandler
{
    private string _responseBody = "{}";
    private HttpStatusCode _statusCode = HttpStatusCode.OK;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    public void SetResponse(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = body;
        _statusCode = statusCode;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;

        // Read body eagerly — caller may dispose Content via using statement
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
```

**Critical**: Capture `LastRequestBody` inside `SendAsync` before returning, because `AuthenticatedApiClient` uses `using var request = ...` which disposes `Content` after execution.

### What to Test

| Category             | Tests                                                                                                                                 |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| OBO token exchange   | Exchanged token attached as `Bearer` header; `ITokenExchangeService.ExchangeTokenAsync()` called with correct user token and audience |
| Missing bearer token | Service still sends request (logs warning, does not throw)                                                                            |
| Route correctness    | Each `IDownstreamApiService` method → correct HTTP method + path                                                                      |
| Request body         | POST/PATCH methods serialize correct JSON body                                                                                        |
| Error handling       | Non-2xx response wrapped in error `JsonElement`                                                                                       |
| JSON parsing         | Successful response parsed to `JsonElement`                                                                                           |

### Mock Setup

Mock `ITokenExchangeService` and `IHttpContextAccessor`. Inject real `DownstreamApiOptions`:

```csharp
private readonly Mock<ITokenExchangeService> _tokenExchange = new();

_tokenExchange
    .Setup(x => x.ExchangeTokenAsync(UserBearerToken, "api://mock-api", It.IsAny<CancellationToken>()))
    .ReturnsAsync(ExchangedToken);
```

## 7. Presentation Layer Tests (MCP Authorization Integration)

### Purpose

Verify that MCP tools and prompts are correctly filtered by role-based authorization. This is the **most MCP-specific** test layer: it starts a real Kestrel server, connects an MCP protocol client, and verifies the authorization filter behavior.

### TestAuthHandler Pattern

Fake `AuthenticationHandler` that trusts the `ClaimsPrincipal` already injected by middleware:

```csharp
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult(
                AuthenticateResult.Success(
                    new AuthenticationTicket(Context.User, Scheme.Name)));
        }

        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
```

### Server Setup Pattern

Each test creates a disposable server with fake auth, mocked dependencies, and real MCP tool/prompt registrations:

```csharp
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Fake auth
builder.Services
    .AddAuthentication("Test")
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
builder.Services.AddAuthorization();

// Mock downstream (tools delegate to use cases which call this)
builder.Services.AddSingleton(CreateMockDownstreamService().Object);

// Real use cases from Application layer
builder.Services.AddApplication();

// Real MCP server with all tools and prompts
builder.Services
    .AddMcpServer(options => { options.ServerInfo = new Implementation { Name = "test", Version = "1.0.0" }; })
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithTools<TaskTools>()
    .WithTools<ProjectsTools>()
    .WithTools<BalancesTools>()
    .WithTools<AdminTools>()
    .WithPrompts<TaskPrompts>()
    .WithPrompts<ProjectPrompts>()
    .WithPrompts<AdminPrompts>();
```

### Inject Identity via Middleware

Follow the MCP SDK's own `AuthorizeAttributeTests` pattern: inject `ClaimsPrincipal` via middleware **before** the auth middleware runs:

```csharp
app.Use(next => context =>
{
    var claims = new List<Claim> { new(ClaimTypes.Name, userName) };
    foreach (var role in roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }
    context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    return next(context);
});

app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
```

### MCP Client Connection

Use `McpClient.CreateAsync()` with `HttpClientTransport`:

```csharp
var transport = new HttpClientTransport(
    new HttpClientTransportOptions
    {
        Endpoint = new Uri($"{address}/mcp"),
    });
var client = await McpClient.CreateAsync(transport);
```

**MCP SDK v1.2.0 API**:

- `McpClient` is a **concrete class** (not `IMcpClient` interface).
- Factory method: `McpClient.CreateAsync(IClientTransport)`.
- Transport: `HttpClientTransport` (not `StreamableHttpClientTransport`).
- `CallToolAsync()` returns `ValueTask<CallToolResult>` — use `.AsTask()` for `Assert.ThrowsAnyAsync`.
- Client types: `ModelContextProtocol.Client` namespace.
- Protocol types: `ModelContextProtocol.Protocol` namespace.

### TestServerEnvironment (Disposable Wrapper)

Encapsulate `WebApplication` + `McpClient` in an `IAsyncDisposable` record for clean `await using`:

```csharp
private sealed class TestServerEnvironment(
    WebApplication app,
    McpClient? client,
    string address) : IAsyncDisposable
{
    public McpClient? Client { get; } = client;
    public string Address { get; } = address;

    public async ValueTask DisposeAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }
        await app.DisposeAsync();
    }
}
```

### What to Test

| Category                       | Tests                                       |
| ------------------------------ | ------------------------------------------- |
| Tool registration              | All expected tools visible with all roles   |
| Tool authorization filtering   | Each role → only its permitted tools        |
| No roles                       | Empty tool list                             |
| Tool invocation (authorized)   | `CallToolAsync` succeeds with correct role  |
| Tool invocation (unauthorized) | `CallToolAsync` throws with wrong role      |
| Prompt registration            | All expected prompts visible with all roles |
| Prompt authorization filtering | Each role → only its permitted prompts      |
| Unauthenticated access         | HTTP POST to `/mcp` returns 401             |

## 11. Adding New Tests

### New Serialization Contract Test

1. Add `[Fact]` or `[Theory]` method to the relevant test class in `Application.Tests/Models/`.
2. Follow the parse-and-assert pattern with `JsonDocument.Parse(result.ToJson())`.

### New Downstream API Route Test

1. Add `[Fact]` method to `DownstreamApiServiceTests`.
2. Call `_httpHandler.SetResponse(...)`, invoke the service method, assert on `_httpHandler.LastRequest`.

### New MCP Authorization Test

1. Add `[Fact]` or `[Theory]` method to `McpServerAuthorizationTests`.
2. Call `StartServerAsync(userName, roles)`, use `env.Client` to list/call tools or prompts.
3. If testing a new tool, add it to `StartServerAsync`'s `.WithTools<>()` chain and update the `AllRoles` array if a new permission is needed.

### New Test Helper

1. Place in the test project's `Helpers/` folder.
2. Use `internal sealed class` (not `public`).
3. Keep it minimal — a helper should solve one specific test infrastructure need.
