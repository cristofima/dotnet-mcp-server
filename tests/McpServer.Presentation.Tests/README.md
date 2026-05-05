# McpServer.Presentation.Tests

Integration tests for the **Presentation layer**: MCP tool/prompt authorization, well-known discovery endpoints, and server behavior.

## What This Layer Tests

The Presentation layer is the composition root: it wires MCP tools, prompts, authentication, and middleware into a running server. These tests start a real Kestrel server per test with fake authentication (no Entra ID connection required) and verify that the MCP SDK correctly enforces role-based authorization and that OAuth2 discovery endpoints return valid metadata.

## Test Classes

Test classes are organized under `Extensions/` to mirror the `McpServer.Presentation/Extensions/` source folder containing the server wiring code they exercise.

### `Extensions/McpServerAuthorizationTests` (18 tests)

Validates MCP tool registration, role-based tool/prompt filtering, and tool invocation authorization using a real MCP client over Streamable HTTP plus targeted raw HTTP assertions.

#### Tool Registration (1 test)

| Test                                           | What it verifies                                        |
| ---------------------------------------------- | ------------------------------------------------------- |
| `ListTools_WithAllRoles_Returns_AllEightTools` | All 8 tools are registered when the user has every role |

#### Tool Authorization Filtering (6 tests)

| Test                                              | What it verifies                                   |
| ------------------------------------------------- | -------------------------------------------------- |
| `ListTools_WithTaskReadOnly_Returns_OnlyGetTasks` | Only `get_tasks` is visible with `mcp:task:read`   |
| `ListTools_WithNoRoles_Returns_NoTools`           | Zero tools are returned when no roles are assigned |

`ListTools_PerRole_Returns_CorrectToolSubset` is a parameterized theory (4 cases):

| Role               | Expected Tools                                     |
| ------------------ | -------------------------------------------------- |
| `mcp:task:write`   | `create_task`, `delete_task`, `update_task_status` |
| `mcp:project:read` | `get_project_details`, `get_projects`              |
| `mcp:balance:read` | `get_project_balance`                              |
| `mcp:admin:access` | `get_backend_users`                                |

#### Tool Invocation (2 tests)

| Test                                               | What it verifies                                                   |
| -------------------------------------------------- | ------------------------------------------------------------------ |
| `CallTool_WithCorrectRole_Returns_SuccessResponse` | Invoking `get_tasks` with `mcp:task:read` succeeds                 |
| `CallTool_WithWrongRole_IsRejected`                | Invoking `get_backend_users` with only `mcp:task:read` is rejected |

#### Prompt Authorization (2 tests)

| Test                                                   | What it verifies                                                                  |
| ------------------------------------------------------ | --------------------------------------------------------------------------------- |
| `ListPrompts_WithAllRoles_Returns_AllSixPrompts`       | All 6 prompts visible with full roles                                             |
| `ListPrompts_WithTaskReadOnly_Returns_OnlyTaskPrompts` | Only `summarize_tasks` and `analyze_task_priorities` visible with `mcp:task:read` |

#### Transport and Unauthenticated Access (7 tests)

| Test                                                               | What it verifies                                                                                   |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `Unauthenticated_PostToMcp_Returns_Unauthorized`                   | POST to `/mcp` without identity returns 401                                                        |
| `LegacySseEndpoints_AreNotExposed_Returns_NotFound`                | Theory with 4 cases: legacy SSE routes (`/sse`, `/message`, `/mcp/sse`, `/mcp/message`) return 404 |
| `PostOnlyFlow_InitializeAndListTools_ReturnsExpectedToolSubset`    | POST-only flow (Copilot Studio style) completes `initialize` and `tools/list`                      |
| `GetPlusPostFlow_InitializeAndListTools_ReturnsExpectedToolSubset` | GET+POST flow (VS Code style) completes session handshake and `tools/list`                         |

Transport assertions also validate MCP server requirements enforced by the SDK:

- POST requests to `/mcp` must accept both `application/json` and `text/event-stream`.
- Session mode requires `Mcp-Session-Id` for `GET /mcp` after initialization.

### `Extensions/WellKnownEndpointTests` (9 tests)

Validates the OAuth2 well-known discovery endpoints (RFC 9728 and RFC 8414) without connecting to Entra ID. Uses a `FakeOpenIdConfigHandler` to intercept the authorization server metadata proxy call.

#### RFC 9728: Protected Resource Metadata (7 tests)

| Test                                                        | What it verifies                                              |
| ----------------------------------------------------------- | ------------------------------------------------------------- |
| `ProtectedResourceMetadata_Returns_200_WithValidJson`       | Endpoint returns 200 with `application/json` content type     |
| `ProtectedResourceMetadata_Contains_ResourceField`          | `resource` field matches the server base URL                  |
| `ProtectedResourceMetadata_Contains_AuthorizationServers`   | `authorization_servers` array contains the Entra ID authority |
| `ProtectedResourceMetadata_Contains_BearerMethodsSupported` | `bearer_methods_supported` includes `"header"`                |
| `ProtectedResourceMetadata_Contains_ScopesSupported`        | `scopes_supported` includes the configured scope              |
| `ProtectedResourceMetadata_Contains_ResourceDocumentation`  | `resource_documentation` URL is present when configured       |
| `ProtectedResourceMetadata_IsAccessibleAnonymously`         | Endpoint returns 200 without any user identity                |

#### RFC 8414: Authorization Server Metadata (2 tests)

| Test                                                    | What it verifies                                          |
| ------------------------------------------------------- | --------------------------------------------------------- |
| `AuthorizationServerMetadata_Returns_200_WithValidJson` | Endpoint returns 200 with `application/json` content type |
| `AuthorizationServerMetadata_IsAccessibleAnonymously`   | Endpoint returns 200 without any user identity            |

## Shared Test Infrastructure

Reusable helpers in `Helpers/`:

| File                       | Purpose                                                                                                                                                                                             |
| -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TestServerBuilder.cs`     | Builds a lightweight Kestrel server with fake auth, mocked downstream services, and real MCP tool/prompt registrations. Accepts `configureServices` and `configureApp` callbacks for extensibility. |
| `TestServerEnvironment.cs` | `IAsyncDisposable` wrapper for `WebApplication` + `McpClient` + server address.                                                                                                                     |
| `TestAuthHandler.cs`       | Fake `AuthenticationHandler` that trusts a pre-injected `ClaimsPrincipal` (no real token validation).                                                                                               |

## Running

```bash
dotnet test tests/McpServer.Presentation.Tests/
```

## Dependencies

- **xUnit v3**, **Moq 4.x**
- **ModelContextProtocol.Core 1.2.0**, **ModelContextProtocol.AspNetCore 1.2.0**
- References `McpServer.Presentation` (the composition root under test)
