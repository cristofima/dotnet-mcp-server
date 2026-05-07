---
name: "C#/.NET Test Generation"
description: "Test generation conventions for MCP OAuth2 Security Baseline. Enforces xUnit v3 patterns, MCP SDK integration testing, Clean Architecture test boundaries, and SOLID/DRY/KISS/YAGNI in tests."
applyTo: "tests/**/*.cs"
---

# C#/.NET Test Generation Instructions

Generate test code that strictly follows these rules for the MCP OAuth2 Security Baseline project.

## Important Note

**DO NOT create `.md` documentation files with every prompt unless explicitly requested.**

## 1. Test Framework and Packages

### Framework: xUnit v3

This project uses **xUnit v3** exclusively. Do not use NUnit, MSTest, FluentAssertions, AutoFixture, Bogus, or any other testing framework or assertion library.

| Package                           | Version | Purpose                                      |
| --------------------------------- | ------- | -------------------------------------------- |
| `xunit.v3`                        | `3.*`   | Test framework (attributes, assertions)      |
| `xunit.runner.visualstudio`       | `3.*`   | VS Test adapter                              |
| `Microsoft.NET.Test.Sdk`          | `17.*`  | Test host                                    |
| `Moq`                             | `4.*`   | Mocking (Infrastructure + Presentation only) |
| `ModelContextProtocol.Core`       | `1.2.0` | MCP client types (Presentation only)         |
| `ModelContextProtocol.AspNetCore` | `1.2.0` | MCP server registration (Presentation only)  |

### Assertions: Plain `Assert.*`

Use xUnit's built-in `Assert` class. Do **not** add FluentAssertions, Shouldly, or any wrapper library. They add a dependency without meaningful value when `Assert.*` already covers the project's needs.

```csharp
// ✅ Correct — xUnit Assert
Assert.Equal(expected, actual);
Assert.True(condition);
Assert.Single(collection);
Assert.Contains("substring", text);
Assert.Throws<ArgumentException>(() => action());

// ❌ Wrong — FluentAssertions (not used in this project)
actual.Should().Be(expected);
collection.Should().ContainSingle();
```

### Explicit `using Xunit;`

xUnit v3 (`xunit.v3` package) still uses the `Xunit` namespace but requires an explicit `using` directive. Always add:

```csharp
using Xunit;
```

## 2. Test Project Structure

### One Test Project per Architecture Layer

```
tests/
├── McpServer.Application.Tests/      # Application layer: serialization contracts, McpToolResult
│   └── Models/
│       └── McpToolResultSerializationTests.cs
├── McpServer.Infrastructure.Tests/   # Infrastructure layer: HTTP clients, OBO token exchange
│   └── Http/
│       └── DownstreamApiServiceTests.cs
└── McpServer.Presentation.Tests/     # Presentation layer: MCP tool/prompt auth integration
    ├── Helpers/
    │   └── TestAuthHandler.cs
    └── McpServerAuthorizationTests.cs
```

### Folder Mirrors the Source Project

Test folders mirror the source project structure. A test for `Infrastructure/Http/DownstreamApiService.cs` goes in `Infrastructure.Tests/Http/DownstreamApiServiceTests.cs`.

### Project References

Each test project references only its corresponding source layer project:

```xml
<!-- Application.Tests → Application -->
<ProjectReference Include="..\..\src\MCP-Server\McpServer.Application\McpServer.Application.csproj" />

<!-- Infrastructure.Tests → Infrastructure -->
<ProjectReference Include="..\..\src\MCP-Server\McpServer.Infrastructure\McpServer.Infrastructure.csproj" />

<!-- Presentation.Tests → Presentation -->
<ProjectReference Include="..\..\src\MCP-Server\McpServer.Presentation\McpServer.Presentation.csproj" />
```

Test projects that need ASP.NET Core types (HTTP, auth, Kestrel) add:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

### .csproj Template

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit.v3" Version="3.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.*">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\MCP-Server\McpServer.{Layer}\McpServer.{Layer}.csproj" />
  </ItemGroup>

</Project>
```

## 3. Test Class Conventions

### Naming

- **Test class**: `{ClassUnderTest}Tests` (e.g., `McpToolResultSerializationTests`, `DownstreamApiServiceTests`).
- **Test method**: `MethodName_Scenario_ExpectedResult` (e.g., `GetProjectsAsync_Attaches_ExchangedOboToken`).
- **File**: One test class per file. Filename matches the class name.

### Class Declaration

- All test classes are `sealed`.
- Use `public sealed class`.
- File-scoped namespaces: `namespace McpServer.Application.Tests.Models;`
- Add `/// <summary>` on test classes explaining what aspect is being tested and why it matters.

```csharp
/// <summary>
/// Tests that McpToolResult serialization produces the expected JSON contract.
/// If this breaks, every MCP client consuming tools will misinterpret responses.
/// </summary>
public sealed class McpToolResultSerializationTests
{
```

### Lifecycle and Cleanup

- Implement `IDisposable` for synchronous cleanup (e.g., `HttpClient`).
- Implement `IAsyncDisposable` for async cleanup (e.g., `WebApplication`, `McpClient`).
- Do **not** use base test classes or `IAsyncLifetime` unless shared setup is genuinely needed across 5+ test classes. YAGNI.
- Do **not** create `TestBase` classes with `IFixture`, `AutoFixture`, or generic test data factories.

```csharp
// ✅ Correct — IDisposable for HttpClient cleanup
public sealed class DownstreamApiServiceTests : IDisposable
{
    private readonly HttpClient _httpClient;
    public void Dispose() => _httpClient.Dispose();
}

// ❌ Wrong — base class for simple cleanup
public abstract class TestBase : IAsyncLifetime { ... }
```

### Constants in Tests

Define test-specific constants as `private const` fields, not magic strings:

```csharp
private const string ExchangedToken = "exchanged-obo-token";
private const string UserBearerToken = "user-jwt-token";
```

## 4. Test Patterns, Layer-Specific Guides, and Test Helpers

> Deep reference for AAA patterns, FakeHttpHandler, TestAuthHandler, MCP client setup, and adding new tests: see [test-patterns.instructions.md](.github/instructions/test-patterns.instructions.md) — load explicitly when writing NEW tests or test infrastructure.

### Quick reference: Arrange-Act-Assert (AAA)

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

## 8. SOLID / DRY / KISS / YAGNI in Tests

### Single Responsibility (SRP)

- **One test class per production class or concern**: `McpToolResultSerializationTests` tests serialization only, `DownstreamApiServiceTests` tests HTTP/OBO only.
- **One test method per behavior**: Don't combine "OBO token exchange" and "correct route" in one test.

### Open/Closed (OCP)

- Add new tests by creating new methods or classes. Don't modify existing tests unless fixing correctness.
- When a new tool is added, add `[InlineData]` or `[MemberData]` entries, don't restructure existing tests.

### DRY

- Extract shared setup to private helper methods (e.g., `CreateService()`, `CreateMockDownstreamService()`, `StartServerAsync()`).
- Extract reusable test infrastructure to `Helpers/` folder (e.g., `TestAuthHandler`, `FakeHttpHandler`).
- Do **not** create shared base classes with `IFixture` or generic factories. Shared helpers as private methods or internal classes suffice.

### KISS

- No assertion libraries beyond `Assert.*`.
- No `TestBase` class hierarchies.
- No `AutoFixture`, `Bogus`, or random data generators — use deterministic values.
- Inline test data directly in methods. Extract to `TheoryData` only when 3+ cases share the same structure.
- Do not create per-test `IServiceProvider` or full DI containers for unit tests. Reserve DI containers for integration tests only.

### YAGNI

- No test categories or `[Trait]` unless CI pipeline requires them.
- No performance benchmarks unless explicitly requested.
- No test coverage reports unless CI is configured for them.
- No `WebApplicationFactory<Program>` pattern — use `WebApplication.CreateSlimBuilder()` directly for MCP integration tests.

## 9. Style Rules (Same as Production Code)

All C# style rules from `dotnet-code-generation.instructions.md` apply to test code:

- **Line length**: 120 characters.
- **Indentation**: 4 spaces, no tabs.
- **Braces**: All control structures must have curly braces (SonarQube S121).
- **File-scoped namespaces**.
- **`sealed` classes** by default.
- **Primary constructors** for types that need DI (e.g., `TestAuthHandler`).
- **Nullable reference types** enabled.
- **`using` directives**: System → Microsoft → third-party → project namespaces.
- **XML documentation** on test classes (summary explaining what and why).
- **No string interpolation** in structured logging (N/A in tests, but applies if `ILogger` is used).

## 10. Do NOT Include in Tests

| Anti-pattern                  | Why                                                          |
| ----------------------------- | ------------------------------------------------------------ |
| FluentAssertions / Shouldly   | Extra dependency, `Assert.*` covers all cases                |
| AutoFixture / Bogus           | Deterministic test data is clearer and more debuggable       |
| Testcontainers                | No database in this project; MCP Server is stateless         |
| WireMock                      | `FakeHttpHandler` is simpler for intercepting outbound HTTP  |
| `[SetUp]` / `[TearDown]`      | NUnit attributes, not xUnit                                  |
| `IClassFixture<T>` for server | `WebApplication` per-test with `IAsyncDisposable` is simpler |
| Coverage attributes           | Not needed unless CI explicitly requires them                |
| Mock `ILogger<T>`             | Tests should not assert on log output; use `NullLogger<T>`   |
| `Thread.Sleep` / `Task.Delay` | Tests must be deterministic; no timing-dependent assertions  |

## 11. Adding New Tests

> Step-by-step guides for each test layer: see [test-patterns.instructions.md](.github/instructions/test-patterns.instructions.md) § 11.

## 12. Checklist

When generating test code for this project, ensure:

- [ ] `sealed` test classes
- [ ] `using Xunit;` present
- [ ] Plain `Assert.*` assertions (no FluentAssertions)
- [ ] AAA pattern with blank-line separation
- [ ] Method naming: `MethodName_Scenario_ExpectedResult`
- [ ] `CancellationToken.None` passed explicitly
- [ ] `private const` for test strings (no magic values)
- [ ] `IDisposable` or `IAsyncDisposable` for cleanup (no base classes)
- [ ] File-scoped namespaces
- [ ] XML `<summary>` on test class
- [ ] Test folder mirrors source folder
- [ ] No FluentAssertions, AutoFixture, Bogus, Testcontainers, WireMock
- [ ] `FakeHttpHandler` for HTTP interception (Infrastructure tests)
- [ ] `TestAuthHandler` + middleware identity injection (Presentation tests)
- [ ] `McpClient.CreateAsync(HttpClientTransport)` for MCP client (not `IMcpClient`)
- [ ] `.AsTask()` on `ValueTask` returns for `Assert.ThrowsAnyAsync`
- [ ] All `if`/`else`/`for`/`foreach`/`while` have curly braces (SonarQube S121)
- [ ] No `.md` documentation files unless explicitly requested
