# McpServer.Application — Use Cases, Contracts, Models, and Configuration

## Overview

Application layer in the Clean Architecture. Contains use cases (business logic and validation), service contracts (interfaces), the standardized tool result envelope, JSON serialization options, and downstream API configuration. Concrete infrastructure implementations live in `McpServer.Infrastructure`.

- **Target Framework**: .NET 10
- **NuGet packages**: `Microsoft.Extensions.DependencyInjection.Abstractions` v10.0.5
- **Project references**: `McpServer.Domain`

## Contents

### Use Cases (`UseCases/`)

One sealed class per tool operation, organized by domain. Each use case injects `IDownstreamApiService`, contains input validation, and returns `McpToolResult`. Registered as `AddTransient<>()` in `ApplicationServiceExtensions.AddApplication()`.

| Domain   | Use Case                   | Validation                                           |
| -------- | -------------------------- | ---------------------------------------------------- |
| Tasks    | `GetTasksUseCase`          | None (passthrough)                                   |
| Tasks    | `CreateTaskUseCase`        | Title required, description required, valid priority |
| Tasks    | `UpdateTaskStatusUseCase`  | Task ID required, valid status                       |
| Tasks    | `DeleteTaskUseCase`        | Task ID required                                     |
| Projects | `GetProjectsUseCase`       | None (passthrough)                                   |
| Projects | `GetProjectDetailsUseCase` | Project ID required                                  |
| Balances | `GetProjectBalanceUseCase` | Project ID required                                  |
| Admin    | `GetBackendUsersUseCase`   | None (passthrough)                                   |

Use cases use `McpServer.Domain.Rules.TaskRules` for validation (e.g., `IsValidPriority()`, `IsValidStatus()`). They do NOT reference Infrastructure or telemetry; the `McpTelemetryFilter` captures tool-level failures.

### Abstractions (`Abstractions/`)

Service contracts that Infrastructure implements and Presentation (Server) consumes via DI.

#### IDownstreamApiService (`Abstractions/IDownstreamApiService.cs`)

Contract for all downstream API operations. Every method returns `Task<JsonElement>`: the raw JSON from the backend API, passed through to MCP clients without deserialization.

| Method                  | Parameters                         | Description         |
| ----------------------- | ---------------------------------- | ------------------- |
| `GetTasksAsync`         | —                                  | List user tasks     |
| `GetTaskByIdAsync`      | `taskId`                           | Get task by ID      |
| `CreateTaskAsync`       | `title`, `description`, `priority` | Create task         |
| `UpdateTaskStatusAsync` | `taskId`, `status`                 | Update task status  |
| `DeleteTaskAsync`       | `taskId`                           | Delete task         |
| `GetProjectsAsync`      | —                                  | List projects       |
| `GetProjectByIdAsync`   | `projectId`                        | Get project details |
| `GetBalanceAsync`       | `projectId`                        | Get project balance |
| `GetUsersAsync`         | —                                  | List users (admin)  |

Each method has a convenience overload without `CancellationToken` that delegates to the full signature via default interface methods.

> **Note**: The name `IDownstreamApiService` is intentionally backend-agnostic. The current implementation (`DownstreamApiService` in Infrastructure) calls MockApi, but the contract does not encode that.

#### ITokenExchangeService (`Abstractions/ITokenExchangeService.cs`)

Contract for OAuth 2.0 token exchange. The MCP Server never passes its own JWT to backend APIs; it exchanges it for a new token scoped to the downstream API.

```csharp
Task<string?> ExchangeTokenAsync(string subjectToken, CancellationToken cancellationToken);
```

The target audience is determined by the implementation via configured scopes (`DownstreamApiOptions.Scopes`). In MSAL OBO, the audience is encoded within the scopes (e.g., `api://{client-id}/.default`), so a separate audience parameter is not needed.

Implementations: `EntraIdTokenExchangeService` (OBO via MSAL) in `McpServer.Infrastructure/Identity/`.

### Models (`Models/`)

#### McpToolResult (`Models/McpToolResult.cs`)

Standardized result envelope for all MCP tool responses. Provides consistent structure for success and error cases.

| Factory method                                | Parameters                         | Purpose                  |
| --------------------------------------------- | ---------------------------------- | ------------------------ |
| `Ok(data)`                                    | `JsonElement`                      | Successful result        |
| `Ok(data, metadata)`                          | `JsonElement`, `McpToolMetadata`   | Successful with metadata |
| `ValidationError(message, field)`             | `string`, `string`                 | Validation error (400)   |
| `NotFoundError(message, field?)`              | `string`, `string?`                | Resource not found (404) |
| `GatewayError(message, retryable?)`           | `string`, `bool`                   | Downstream failure (502) |
| `Fail(statusCode, message)`                   | `int`, `string`                    | Error (minimal)          |
| `Fail(statusCode, message, field)`            | `int`, `string`, `string?`         | Error with field         |
| `Fail(statusCode, message, field, retryable)` | `int`, `string`, `string?`, `bool` | Full error details       |

Semantic factory methods (`ValidationError`, `NotFoundError`, `GatewayError`) are preferred over raw `Fail()` calls. They encapsulate HTTP status codes as private constants, eliminating magic numbers from use cases. The `Fail()` overloads remain available for non-standard status codes.

Serialized via `ToJson()` using `McpJsonOptions.WriteIndented`.

### Constants (`Constants/`)

#### McpJsonOptions (`Constants/McpJsonOptions.cs`)

Two pre-configured `JsonSerializerOptions` instances with `camelCase` naming:

| Preset          | `WriteIndented` | Used by                                |
| --------------- | --------------- | -------------------------------------- |
| `WriteIndented` | `true`          | `McpToolResult.ToJson()` (tool output) |
| `Compact`       | `false`         | Internal serialization                 |

### Configuration (`Configuration/`)

#### DownstreamApiOptions (`Configuration/DownstreamApiOptions.cs`)

IOptions-pattern configuration for the downstream API connection. Bound from the `"DownstreamApi"` section with startup validation.

| Property   | Type       | Required | Description                                  |
| ---------- | ---------- | -------- | -------------------------------------------- |
| `BaseUrl`  | `string`   | Yes      | Base URL (set via Aspire or appsettings)     |
| `Audience` | `string`   | Yes      | Target audience for OBO token exchange       |
| `Scopes`   | `string[]` | Yes      | Scopes for OBO (e.g., `api://{id}/.default`) |

### Service Registration (`ApplicationServiceExtensions.cs`)

```csharp
services.AddApplication();
```

Registers all use cases as transient services, grouped by domain (Tasks, Projects, Balances, Admin).

## Design Decisions

1. **`JsonElement` over DTOs**: All `IDownstreamApiService` methods return `JsonElement`. The MCP Server is a passthrough: it validates inputs, calls the backend, and wraps the raw JSON in `McpToolResult.Ok()`. No deserialization or mapping occurs, so response DTOs would be dead code.

2. **Default interface methods for CancellationToken overloads**: Each `IDownstreamApiService` method has a parameter-less overload that delegates to the full signature. This avoids boilerplate in tests and non-async callers without requiring a separate extension class.

3. **McpToolResult in Application, not Domain**: The result envelope is an application concern (how tools communicate results), not a business rule. It depends on `System.Text.Json` and HTTP status codes, which are infrastructure concepts that Domain should not know about.

4. **Use cases as Transient**: Use cases are stateless orchestrators: they receive parameters, validate, call `IDownstreamApiService`, and return `McpToolResult`. No state is held between calls, so Transient is the natural lifetime. Scoped would also work but adds no value since there is nothing to share within a request scope. Singleton would be incorrect because use cases inject Scoped services (`IDownstreamApiService` via `IHttpClientFactory`), which would create a captive dependency.

5. **Concrete classes, no interfaces**: Each use case has exactly one implementation and no polymorphism requirement. Adding `IGetTasksUseCase`, `ICreateTaskUseCase`, etc. would create 8 interfaces that mirror the classes 1:1 without enabling substitution. Interfaces add value when there are multiple implementations (strategy pattern), when the boundary needs to be swappable (like `IDownstreamApiService` for HTTP vs gRPC, or `ITokenExchangeService` for Entra ID vs Auth0), or when mocking is needed in tests. Use cases satisfy none of these: they are internal Application logic, and tests can instantiate them directly by mocking `IDownstreamApiService`. If a need arises (e.g., a second task provider with different logic), interfaces can be introduced at that point.

## Dependency Graph

```
McpServer.Domain
    ↑
McpServer.Application  (this project)
    ↑
McpServer.Infrastructure
    ↑
McpServer.Presentation
```

For Domain layer details, see `McpServer.Domain/README.md`.
