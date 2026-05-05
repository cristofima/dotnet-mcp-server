# McpServer.Infrastructure.Tests

Unit tests for the **Infrastructure layer**: HTTP routing, OBO token exchange, and downstream API communication.

## What This Layer Tests

The Infrastructure layer owns `DownstreamApiService` (inherits `AuthenticatedApiClient`), the single gateway to the downstream MockApi. These tests verify that every domain method sends the correct HTTP verb and route, that Bearer tokens are exchanged via OBO before attaching to outbound requests, and that non-2xx responses are wrapped into structured error `JsonElement` values.

All outbound HTTP is intercepted by a `FakeHttpHandler`: no real network calls are made.

## Test Classes

### `Http/DownstreamApiServiceTests` (12 tests)

Validates `DownstreamApiService` using a fake `HttpMessageHandler` and a mocked `ITokenExchangeService`.

#### OBO Token Exchange (3 tests)

| Test                                                     | What it verifies                                                                      |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| `GetProjectsAsync_Attaches_ExchangedOboToken`            | Outbound request carries `Bearer {exchanged-obo-token}`, not the original user token  |
| `GetProjectsAsync_Calls_TokenExchange_WithUserToken`     | `ITokenExchangeService.ExchangeTokenAsync` is called exactly once with the user's JWT |
| `GetTasksAsync_WithMissingBearerToken_StillSendsRequest` | Missing Bearer token logs a warning but does not throw                                |

#### Route Correctness (8 tests)

| Test                                               | What it verifies                                                       |
| -------------------------------------------------- | ---------------------------------------------------------------------- |
| `GetProjectsAsync_Sends_GET_ToProjectsRoute`       | `GET /api/projects`                                                    |
| `GetProjectByIdAsync_Sends_GET_ToProjectIdRoute`   | `GET /api/projects/{id}`                                               |
| `GetBalanceAsync_Sends_GET_ToBalancesRoute`        | `GET /api/balances/{id}`                                               |
| `GetTasksAsync_Sends_GET_ToTasksRoute`             | `GET /api/tasks`                                                       |
| `GetUsersAsync_Sends_GET_ToAdminUsersRoute`        | `GET /api/admin/users`                                                 |
| `CreateTaskAsync_Sends_POST_WithJsonBody`          | `POST /api/tasks` with `title`, `description`, `priority` in JSON body |
| `UpdateTaskStatusAsync_Sends_PATCH_WithStatusBody` | `PATCH /api/tasks/{id}/status` with `status` in JSON body              |
| `DeleteTaskAsync_Sends_DELETE_ToTaskIdRoute`       | `DELETE /api/tasks/{id}`                                               |

#### Error Handling and Response Parsing (1 test)

| Test                                                            | What it verifies                                                  |
| --------------------------------------------------------------- | ----------------------------------------------------------------- |
| `GetProjectsAsync_DownstreamReturns500_ReturnsErrorJsonElement` | 5xx responses are wrapped into `{ error: true, statusCode: 500 }` |

## Running

```bash
dotnet test tests/McpServer.Infrastructure.Tests/
```

## Dependencies

- **xUnit v3**, **Moq 4.x**
- References `McpServer.Infrastructure` (the layer under test)
- References `McpServer.Application` (for `ITokenExchangeService` and `DownstreamApiOptions`)
