# Contributing to MCP OAuth2 Security Baseline

Thank you for your interest in contributing. This document covers how to set up your environment, the workflows for submitting changes, and the conventions every contributor must follow. The target audience is developers familiar with .NET and clean architecture who want to extend the MCP Server, fix bugs, or improve observability and security.

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Local setup](#2-local-setup)
3. [Branching and pull request workflow](#3-branching-and-pull-request-workflow)
4. [Clean Architecture rules](#4-clean-architecture-rules)
5. [Adding new MCP features](#5-adding-new-mcp-features)
6. [Security requirements](#6-security-requirements)
7. [Observability requirements](#7-observability-requirements)
8. [Testing requirements](#8-testing-requirements)
9. [Code style checklist](#9-code-style-checklist)
10. [Common pitfalls](#10-common-pitfalls)
11. [Documentation](#11-documentation)
12. [Getting help](#12-getting-help)

---

## 1. Prerequisites

| Requirement               | Version | Notes                                                                                                   |
| ------------------------- | ------- | ------------------------------------------------------------------------------------------------------- |
| Visual Studio             | 2026+   | Install the **ASP.NET and web development** workload; .NET 10 SDK and Aspire are included automatically |
| Microsoft Entra ID tenant | any     | Any work/school account; personal Microsoft accounts must be added as B2B guests                        |

Verify your setup:

```powershell
dotnet --version    # must be 10.x
```

---

## 2. Local setup

```powershell
# Clone the repository
git clone https://github.com/cristofima/dotnet-mcp-server.git
cd dotnet-mcp-server

# Restore dependencies
dotnet restore McpServer.slnx

# Run all services via Aspire (MCP Server :5230 + MockApi)
cd src/McpServer.AppHost && dotnet run

# Run the full test suite
dotnet test McpServer.slnx

# Run tests for a specific layer
dotnet test tests/McpServer.Application.Tests/
dotnet test tests/McpServer.Infrastructure.Tests/
dotnet test tests/McpServer.Presentation.Tests/
```

Before running end-to-end flows that require a real token, follow [docs/ENTRA-ID-TESTING-GUIDE.md](docs/ENTRA-ID-TESTING-GUIDE.md) to acquire a JWT and assign App Roles.

---

## 3. Branching and pull request workflow

1. **Fork the repository** to your GitHub account and work on a branch there.
2. **Open an issue** before starting work on a non-trivial change. Describe the problem or feature and wait for maintainer feedback.
3. **Branch from `main`** using a descriptive name: `feature/add-balance-write-tools`, `fix/obo-token-extraction`, `docs/update-entra-setup`.
4. **Keep PRs focused.** One logical change per PR. A PR that adds a new tool, refactors the telemetry filter, and updates docs in one diff is hard to review.
5. **Write tests first or alongside code**, not after. Every new use case and tool must have tests before the PR is merged (see [section 8](#8-testing-requirements)).
6. **Update the changelog.** Add an entry to [CHANGELOG.md](CHANGELOG.md) under `[Unreleased]` following the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.
7. **Ensure all tests pass** locally before opening the PR: `dotnet test McpServer.slnx`.
8. **Assign the PR** to a maintainer for review.

### PR checklist

Before requesting a review, confirm each item:

- [ ] New use case(s) registered in `ApplicationServiceExtensions.cs`
- [ ] New tool(s) chained in `McpServerExtensions.cs` via `.WithTools<T>()`
- [ ] Tests added for each new use case and tool
- [ ] No hardcoded strings, magic numbers, or inline permission values
- [ ] `McpTelemetryFilter` not modified unless strictly necessary (telemetry is centralized)
- [ ] CHANGELOG entry added
- [ ] No `dotnet build` warnings introduced

---

## 4. Clean Architecture rules

This project enforces strict dependency direction across four layers. Violating this direction breaks the separation of concerns.

```
Domain → Application → Infrastructure → Presentation
```

### Layer responsibilities

| Layer              | Project                      | Allowed dependencies                | Forbidden                                             |
| ------------------ | ---------------------------- | ----------------------------------- | ----------------------------------------------------- |
| **Domain**         | `McpServer.Domain`         | None (zero dependencies)            | Any framework, NuGet package                          |
| **Application**    | `McpServer.Application`    | Domain                              | Infrastructure, Presentation, HttpContext             |
| **Infrastructure** | `McpServer.Infrastructure` | Application, Shared                 | Presentation, MCP SDK types                           |
| **Presentation**   | `McpServer.Presentation`   | Infrastructure, Application, Domain | Direct HTTP calls, MSAL (use Infrastructure services) |

### Where things live

| Concern                                | Location                                                          |
| -------------------------------------- | ----------------------------------------------------------------- |
| Permission constants (`mcp:task:read`) | `Domain/Constants/Permissions.cs`                                 |
| Validation rules and limits            | `Domain/Rules/TaskRules.cs` (or a new `*Rules.cs` for the domain) |
| Service contracts                      | `Application/Abstractions/IDownstreamApiService.cs`               |
| Use cases (one per tool operation)     | `Application/UseCases/{Domain}/`                                  |
| Tool result model                      | `Application/Models/McpToolResult.cs`                             |
| JSON serialization presets             | `Application/Constants/McpJsonOptions.cs`                         |
| HTTP client and OBO exchange           | `Infrastructure/Http/`, `Infrastructure/Identity/`                |
| Tool classes                           | `Presentation/Tools/`                                             |
| Prompt classes                         | `Presentation/Prompts/`                                           |
| Middleware                             | `Presentation/Middleware/`                                        |
| DI registrations                       | `*ServiceExtensions.cs` per layer                                 |

Never place business logic in a tool class. Tools call `ExecuteAsync()` and return `result.ToJson()`. If it involves a condition, a validation, or a domain rule, it belongs in the use case.

---

## 5. Adding new MCP features

### Adding a tool

1. **Use cases first**: create one sealed use case class per tool method in `Application/UseCases/{Domain}/`. The use case injects `IDownstreamApiService`, validates all inputs, and returns `McpToolResult`.
2. **Register the use case** as `AddTransient<>()` in `Application/ApplicationServiceExtensions.cs`.
3. **Add the interface method** to `IDownstreamApiService` and implement it in `Infrastructure/Http/DownstreamApiService.cs`.
4. **Create the tool class** in `Presentation/Tools/` following the pattern below.
5. **Chain `.WithTools<NewTools>()`** in `Presentation/Extensions/McpServerExtensions.cs`.

#### Tool class pattern

```csharp
[McpServerToolType]
[Authorize]
public sealed class MyDomainTools(
    GetItemsUseCase getItemsUseCase,
    CreateItemUseCase createItemUseCase)
{
    [McpServerTool(Name = "get_items", Title = "Get Items",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get all items for the authenticated user.")]
    public async Task<string> GetItemsAsync(CancellationToken cancellationToken)
    {
        var result = await getItemsUseCase.ExecuteAsync(cancellationToken);
        return result.ToJson();
    }

    [McpServerTool(Name = "create_item", Title = "Create Item",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a new item.")]
    public async Task<string> CreateItemAsync(
        [Description("Item name"), Required, MaxLength(200)] string name,
        CancellationToken cancellationToken)
    {
        var result = await createItemUseCase.ExecuteAsync(name, cancellationToken);
        return result.ToJson();
    }
}
```

#### Mandatory rules for every tool

- `[Authorize]` at class level: required, no exceptions.
- Inject use cases (not `IDownstreamApiService`, not `ILogger`, not `IHttpContextAccessor`).
- Return `result.ToJson()` only. Never call `McpToolResult.Ok` or `McpToolResult.Fail` in a tool.
- Do not add `Stopwatch`, `McpActivitySource`, `McpMetrics.*`, or try/catch for general errors: `McpTelemetryFilter` handles all of that centrally.
- Use `snake_case` for tool names in `[McpServerTool(Name = "...")]`.
- Set `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` accurately on every tool. A tool that writes data must have `ReadOnly = false`.
- `CancellationToken` without `= default` in tool methods, unless preceded by other optional parameters.
- All control flow (`if`, `else`, `for`, `foreach`, `while`) must have curly braces, including single-line guard clauses.

### Adding a prompt

1. Create a sealed class in `Presentation/Prompts/` with `[McpServerPromptType]` and `[Authorize]`.
2. Return `new ChatMessage(ChatRole.User, "...")` from `Microsoft.Extensions.AI`.
3. Chain `.WithPrompts<NewPrompts>()` in `McpServerExtensions.cs`.

```csharp
[McpServerPromptType]
[Authorize]
public sealed class MyDomainPrompts
{
    [McpServerPrompt(Name = "my_prompt")]
    [Description("Returns a structured prompt for domain analysis.")]
    public ChatMessage MyPromptMethod([Description("Optional filter")] string? filter = null)
    {
        return new ChatMessage(ChatRole.User, $"Analyze all items{(filter is null ? "" : $" matching {filter}")}.");
    }
}
```

### Adding a MockApi endpoint

> The MockApi is a demo artifact for end-to-end testing only. Keep changes minimal and focused.

1. Add a controller in `McpServer.BackendApi/Controllers/` with `[Route("api/[controller]")]`.
2. Add service interface and implementation in `McpServer.BackendApi/Services/`.
3. Register the service as scoped in `McpServer.BackendApi/Program.cs`.
4. Add the corresponding method(s) to `IDownstreamApiService` and implement them in `DownstreamApiService`.

---

## 6. Security requirements

All contributions that touch authentication, authorization, or token handling must satisfy these requirements. This is not optional; PRs that fail any of these will not be merged.

### Authorization

- `[Authorize]` at the tool class level is required for every class. It ensures no tool method can be called without a valid JWT.
- Authorization is based on successful authentication only: a valid JWT (audience, issuer, signature) is the only requirement. No App Roles or scope policies are enforced.
- Never log, trace, or record full tokens.

### Token handling

- The MCP Server does not pass the user's incoming JWT directly to the downstream API. The OBO exchange in `InfraEntraIdTokenExchangeService` is the only allowed path.
- Never log, trace, or record full tokens. The `enduser.id` tag in spans always uses `oid` (Entra ID Object ID), never `sub` (which is pairwise-per-app).
- Do not add new ways to extract or cache tokens outside `Infrastructure/Identity/` and `Infrastructure/Http/ApiTokenProvider.cs`.

### OWASP Top 10 baseline

| Risk                      | Mitigation in this project                                                                                             |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Broken access control     | `[Authorize]` at class + method level; OBO enforces downstream permission check                                        |
| Cryptographic failures    | No secrets in code or config files; use environment variables and Entra ID                                             |
| Injection                 | MCP parameters validated via data annotations and use case logic                                                       |
| Insecure design           | Clean Architecture prevents UI from calling infrastructure directly                                                    |
| Security misconfiguration | Rate limiting (100 req/min), CORS from config, no wildcard audiences                                                   |
| Vulnerable dependencies   | Keep NuGet packages up to date; check for CVEs before merging                                                          |
| Authentication failures   | JWT validation enforced by `AuthenticationExtensions.cs`; both `{clientId}` and `api://{clientId}` audiences validated |

Do not add inline credentials, connection strings, or secrets to any file. Do not disable validators, suppress nullability warnings with `!`, or bypass authentication middleware.

---

## 7. Observability requirements

Telemetry is centralized in `McpTelemetryFilter`. Do not add logging, metrics, or tracing directly inside tool classes. This section describes what contributors must do when adding new components.

### What is already handled automatically

- Tool invocation count and latency: `McpTelemetryFilter` records these for every tool call.
- Error recording and exception logging: handled by `McpTelemetryFilter`. Do not add try/catch in tools.
- Response size metrics: handled by `McpTelemetryFilter`.
- Session and W3C trace context propagation: handled by `McpCorrelationMiddleware`.

### What contributors must do

| Action                                         | How                                                                                                                          |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| Add a data classification for a sensitive tool | Register it in the `ToolDataClassifications` dictionary in `McpTelemetryFilter`                                              |
| Emit a span for a new infrastructure operation | Create a child activity from `McpActivitySource` inside the infrastructure class, not in the tool                            |
| Record a custom metric                         | Add it to `McpMetrics.cs` in `Infrastructure/Telemetry/`, not inline in a use case or tool                                   |
| Add structured logging                         | Use the injected `ILogger<T>` in use cases or infrastructure classes only; do not inject `ILogger` in tool or prompt classes |

### Span tags standard

- `enduser.id`: always the `oid` claim, never `sub`.
- `mcp.tool.name`: set automatically by `McpTelemetryFilter`.
- `mcp.session.id`: set by `McpTelemetryFilter` from the correlation middleware; do not re-tag manually.

The Aspire Dashboard (URL printed at startup) shows local traces, metrics, and logs. Use it to verify that your new tool produces correct spans before submitting a PR.

Refer to [McpServer.ServiceDefaults README](src/McpServer.ServiceDefaults/README.md) for infrastructure span filtering rules.

---

## 8. Testing requirements

### Framework and packages

This project uses **xUnit v3** exclusively. Do not introduce NUnit, MSTest, FluentAssertions, AutoFixture, Bogus, or any other testing framework or assertion library.

| Package                     | Version | Purpose                                      |
| --------------------------- | ------- | -------------------------------------------- |
| `xunit.v3`                  | `3.*`   | Test framework                               |
| `xunit.runner.visualstudio` | `3.*`   | VS Test adapter                              |
| `Microsoft.NET.Test.Sdk`    | `18.*`  | Test host                                    |
| `Moq`                       | `4.*`   | Mocking (Infrastructure + Presentation only) |

Use `Assert.*` from xUnit directly. The existing tests in `tests/` are the canonical reference.

### One test project per architectural layer

```
tests/
├── McpServer.Application.Tests/      # Use case logic, McpToolResult serialization
├── McpServer.Infrastructure.Tests/   # HTTP client routing, OBO token propagation
└── McpServer.Presentation.Tests/     # MCP tool/prompt authorization, well-known endpoints
```

Test files mirror the source structure: a test for `Infrastructure/Http/DownstreamApiService.cs` goes in `Infrastructure.Tests/Http/DownstreamApiServiceTests.cs`. Do not add cross-layer tests to a single class.

### Test naming convention

```
MethodName_StateUnderTest_ExpectedBehavior
```

Examples: `ExecuteAsync_NullTitle_ReturnsValidationError`, `GetProjectsAsync_AttachesExchangedOboToken`, `CallTool_WithoutRole_Returns403`.

### What to test

| Layer              | What to cover                                                                                                                                                                                                    |
| ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Application**    | Each use case: valid input reaches `IDownstreamApiService`; invalid input returns `McpToolResult.Fail` with the correct `statusCode`, `message`, and `field`; boundary values (e.g., `TaskRules.TitleMaxLength`) |
| **Infrastructure** | `DownstreamApiService` attaches the exchanged OBO token; requests go to the correct routes; `FakeHttpHandler` captures the outgoing `HttpRequestMessage`                                                         |
| **Presentation**   | Tool methods return HTTP 401 when unauthenticated; return HTTP 403 when authenticated but missing the required role; return HTTP 200 with valid role; well-known endpoints are anonymous                         |

### Test patterns

```csharp
// ✅ Arrange-Act-Assert with blank lines between sections
[Fact]
public async Task ExecuteAsync_EmptyTitle_ReturnsValidationError()
{
    var useCase = new CreateTaskUseCase(_api.Object);

    var result = await useCase.ExecuteAsync("", "description", "Medium", CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(400, result.Error?.StatusCode);
    Assert.Equal("title", result.Error?.Field);
}

// ✅ Theory for parameterized validation
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task ExecuteAsync_InvalidTitle_ReturnsValidationError(string? title)
{
    var useCase = new CreateTaskUseCase(_api.Object);

    var result = await useCase.ExecuteAsync(title!, "description", "Medium", CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal("title", result.Error?.Field);
}
```

---

## 9. Code style checklist

This project enforces 243 active SonarQube rules, documented in `.github/instructions/sonarqube-rules.instructions.md`. The following are the most common violations to watch for:

| Rule    | What to avoid                                                                     |
| ------- | --------------------------------------------------------------------------------- |
| S121    | Single-line `if` without braces: always use `{ }`, even for guard clauses         |
| S3008   | `public static readonly` field instead of `public const` for non-attribute values |
| S2302   | String literal for parameter name in exception messages: use `nameof(param)`      |
| S1118   | Utility class with public constructor: add `private` constructor                  |
| S1659   | Multiple declarations on one line: one variable per declaration                   |
| IDE0005 | Unused `using` directives: remove them                                            |

Additional style rules that apply project-wide:

- Line length: 120 characters.
- Indentation: 4 spaces (no tabs).
- Allman brace style (opening brace on its own line).
- File-scoped namespaces: `namespace McpServer.Presentation.Tools;`
- `sealed` on every class that does not need inheritance.
- Primary constructors for DI in tool, prompt, and service classes.
- Async methods must end with `Async`: `GetTasksAsync()`, not `GetTasks()`.
- No `using static` for entire classes.
- Nullable reference types enabled project-wide: do not suppress with `!` without a comment.

---

## 10. Common pitfalls

These are the most frequent mistakes that make PRs fail or cause runtime issues.

| Problem                                     | Cause                                                                                       | Fix                                                                        |
| ------------------------------------------- | ------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| Tool is not visible to MCP clients          | Not chained in `McpServerExtensions.cs`                                                     | Add `.WithTools<MyTools>()`                                                |
| 403 on every call despite correct role      | `[Authorize(Roles)]` value does not match `Permissions.cs` constant or Entra App Role value | Make sure all three match exactly                                          |
| OBO exchange fails                          | MockApi not registered as an Exposed API, or MCP Server missing API permissions             | Follow [docs/PERMISSION-SETUP-GUIDE.md](docs/PERMISSION-SETUP-GUIDE.md)    |
| App Roles not in JWT                        | Users assigned in App Registrations, not in Enterprise Applications                         | Assign users in Enterprise Applications, Users and groups                  |
| Entra auth fails with personal account      | Personal MSA is not supported as a direct member                                            | Add it as a B2B guest                                                      |
| Tests fail with null `IHttpContextAccessor` | Tool test bypasses the full pipeline                                                        | Use `TestServerBuilder` from `tests/Helpers/` for Presentation-layer tests |
| `McpToolResult` serialization mismatch      | Using a custom `JsonSerializerOptions` outside `McpJsonOptions`                             | Always use `McpJsonOptions.WriteIndented` or `McpJsonOptions.Compact`      |
| Span attribute overwrite                    | Adding `mcp.session.id` manually in a tool                                                  | Remove it: `McpTelemetryFilter` sets this tag for every tool span          |

---

## 11. Documentation

Update documentation when your change affects behavior that is already described in `docs/`. You do not need to add a new doc for every PR. The bar for a doc update is: would a reader of that file be misled by the current content after your change?

| Doc                                                                | Update when                                                                  |
| ------------------------------------------------------------------ | ---------------------------------------------------------------------------- |
| [docs/PERMISSION-SETUP-GUIDE.md](docs/PERMISSION-SETUP-GUIDE.md)   | Adding a new App Role                                                        |
| [docs/TESTING-STRATEGY.md](docs/TESTING-STRATEGY.md)               | Introducing a new test pattern or layer                                      |
| [docs/FUTURE-FEATURES-ROADMAP.md](docs/FUTURE-FEATURES-ROADMAP.md) | Implementing a roadmap item (remove it from the file) or proposing a new one |
| [CHANGELOG.md](CHANGELOG.md)                                       | Every PR (add entry under `[Unreleased]`)                                    |

Follow the writing style from the project:

- Direct and factual: short declarative sentences, no filler words.
- Natural language: write as you would explain to a colleague.
- No em-dashes: use commas, semicolons, or colons instead.
- Labels in lists with colons: `**Label**: description`.
- Tables over prose for structured data.

---

## 12. Getting help

- **Entra ID setup**: see [docs/ENTRA-ID-TESTING-GUIDE.md](docs/ENTRA-ID-TESTING-GUIDE.md) for token acquisition, B2B guests, and Postman/curl examples.
- **OBO flows by client type**: see [docs/OAUTH2-FLOWS-BY-CLIENT.md](docs/OAUTH2-FLOWS-BY-CLIENT.md).
- **Connecting Copilot Studio**: see [docs/COPILOT-STUDIO-CONNECTOR-SETUP.md](docs/COPILOT-STUDIO-CONNECTOR-SETUP.md).
- **Architecture diagrams**: see [docs/diagrams/README.md](docs/diagrams/README.md).
- **Questions**: open an issue with the `question` label.
- **Bug reports**: open an issue with the `bug` label, include steps to reproduce, the expected behavior, and the actual behavior.
- **Feature requests**: open an issue with the `enhancement` label and describe the use case before writing any code.
