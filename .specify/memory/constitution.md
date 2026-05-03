<!--
  Sync Impact Report
  ==================
  Version change: 0.0.0 (template) → 1.0.0
  Type of bump: MINOR — first substantive fill; all principles and sections added.

  Modified principles (template → constitution):
  - [PRINCIPLE_1_NAME] → I. Security-First (OAuth2 + OBO)
  - [PRINCIPLE_2_NAME] → II. Clean Architecture (Non-Negotiable)
  - [PRINCIPLE_3_NAME] → III. Single Responsibility in Tools
  - [PRINCIPLE_4_NAME] → IV. Observability by Default
  - [PRINCIPLE_5_NAME] → V. Test-First Discipline

  Added sections:
  - Technology Stack & Deployment
  - Development Workflow & Quality Gates

  Templates updated:
  - .specify/templates/plan-template.md: Constitution Check gates updated ✅
  - .specify/templates/spec-template.md: No structural changes required ✅
  - .specify/templates/tasks-template.md: No structural changes required ✅

  Deferred TODOs: None
-->

# MCP OAuth2 Security Baseline Constitution

## Core Principles

### I. Security-First (OAuth2 + OBO)

Every MCP endpoint, tool, and prompt MUST be protected by Microsoft Entra ID JWT
authentication and App Role authorization. Specific rules:

- `[Authorize]` MUST appear at both class level and method level on every MCP tool
  type; omitting either attribute is a blocking defect.
- Token passthrough from the MCP client to the downstream API is NEVER permitted.
  The OBO (On-Behalf-Of) exchange via MSAL is the sole allowed pattern for obtaining
  downstream access tokens.
- Permission constants MUST be sourced from `McpServer.Domain.Constants.Permissions`;
  hardcoded string literals for role names are prohibited.
- Rate limiting (fixed window, 100 req/min per identity) and CORS (configured via
  `Cors:AllowedOrigins`) MUST be active in all non-development environments.
- `enduser.id` in telemetry spans MUST use the `oid` claim (Entra ID Object ID),
  never `sub` (pairwise-per-app), to enable consistent cross-service correlation.

**Rationale**: The OBO pattern prevents privilege escalation and ensures the downstream
API enforces its own authorization. Dual `[Authorize]` guards prevent accidental
exposure introduced by refactoring. Rate limiting and CORS are minimal blast-radius
controls that MUST be in place before any feature reaches production.

### II. Clean Architecture (Non-Negotiable)

Dependency direction is strictly enforced in one direction only:
Domain → Application → Infrastructure → Presentation.

- Domain has zero external dependencies.
- Application references only Domain; it MUST NOT reference Infrastructure or
  Presentation.
- Infrastructure references Domain and Application; it MUST NOT reference Presentation.
- Presentation (MCP Server) is the composition root; it references all lower layers but
  contains no business logic.
- Cross-cutting concerns (OpenTelemetry, Serilog, health checks, resilience) belong in
  `McpServer.ServiceDefaults`, referenced by service entry points only.

**Rationale**: Enforcing the dependency rule keeps business logic testable without HTTP,
MSAL, or telemetry dependencies, and prevents infrastructure details from leaking into
use cases.

### III. Single Responsibility in Tools

Each MCP tool method MUST delegate to exactly one use case via `ExecuteAsync()` and
return `result.ToJson()`. No other logic is permitted in tool classes.

- Tools MUST NOT inject `IDownstreamApiService`, `IHttpContextAccessor`, or `ILogger`.
- Tools MUST NOT contain `if`/`else` branching for business decisions, validation, or
  error mapping (except tool-specific exception mapping when a specific exception type
  must become a validation response).
- All validation, permission checks beyond `[Authorize]`, and data transformation belong
  in the use case.
- Use cases are registered as `AddTransient<>()` in
  `ApplicationServiceExtensions.AddApplication()`; one use case class per tool method.
- `McpToolResult.Ok(JsonElement)` / `McpToolResult.Fail(...)` MUST only be called
  inside use cases, never in tool classes.

**Rationale**: Tools that contain business logic bypass the use-case layer, making logic
untestable without standing up the full MCP server. Single-responsibility ensures
each use case can be tested, replaced, and reasoned about independently.

### IV. Observability by Default

Every tool invocation MUST produce a trace span, at least one metric, and a structured
log entry. These MUST be centralized; tools MUST NOT add telemetry directly.

- `McpTelemetryFilter` (registered via `AddCallToolFilter` in `McpServerExtensions.cs`)
  handles all tool-level tracing, metrics (`McpMetrics.RecordToolInvocation`,
  `RecordResponseSize`), and structured logging.
- Tools MUST NOT add `Stopwatch`, `McpActivitySource`, `McpMetrics.*`, or try/catch
  for general error recording; the filter covers all of these.
- `McpCorrelationMiddleware` MUST propagate `Mcp-Session-Id` and W3C trace context into
  Activity tags on every request.
- `OTEL_EXPORTER_OTLP_ENDPOINT` controls OTLP export; when unset, telemetry is emitted
  to the Aspire Dashboard only (console/file sinks remain active in all environments).
- Serilog console + compact JSON file sinks are configured in `ServiceDefaults` and
  MUST NOT be disabled or overridden per-service.

**Rationale**: Centralizing telemetry in a filter eliminates duplication, ensures
consistent tag names, and prevents tools from accidentally skipping metrics on error
paths.

### V. Test-First Discipline

Tests MUST be written before implementation is complete. Every use case and
infrastructure component that changes MUST have corresponding test coverage.

- xUnit v3 is the only permitted test framework. NUnit, MSTest, FluentAssertions,
  AutoFixture, and Bogus are prohibited.
- Assertions MUST use plain `Assert.*`; no wrapper assertion libraries.
- Moq 4.x is permitted only in `McpServer.Infrastructure.Tests` and
  `McpServer.Presentation.Tests`. Application tests MUST use no mocks.
- One test project per Clean Architecture layer; cross-layer concerns MUST NOT be
  tested in the same class.
- Test naming convention: `MethodName_StateUnderTest_ExpectedBehavior`
  (e.g., `CreateTask_WithEmptyTitle_ReturnsValidationError`).
- Use cases MUST be testable without `HttpContext`, `ILogger`, or telemetry
  dependencies.

**Rationale**: Mocking at the wrong layer hides integration failures. Framework
uniformity removes cognitive overhead and prevents incompatible test execution
environments in CI.

## Technology Stack & Deployment

| Concern | Choice | Notes |
|---------|--------|-------|
| Runtime | .NET 10, C# 13 | Aspire workload required |
| MCP SDK | ModelContextProtocol.AspNetCore v1.2.0 | Streamable HTTP at `/mcp` |
| Identity | Microsoft Entra ID | JWT Bearer + App Roles + MSAL OBO |
| Orchestration (local) | .NET Aspire | `McpServer.AppHost` |
| Orchestration (prod) | Azure App Services | Linux, `DOTNETCORE\|10.0` |
| Telemetry | OpenTelemetry SDK 1.15.0 | Traces, metrics, logs via OTLP |
| Logging | Serilog | Console + file (compact JSON, daily roll) |
| Code quality | SonarQube IDB Way profile | 243 rules; all MUST pass on PRs |

Deployment artifacts: `deploy-mcp-server.yml` and `deploy-mock-api.yml` via
`azure/webapps-deploy@v3`. Full CI/CD with OIDC, staging/production, and rollback is
defined in `azure-deploy-oidc.yml`.

Environment variable `DownstreamApi__BaseUrl` MUST be set by the Aspire AppHost (local)
or `appsettings.*.json` (non-Aspire). No base URL may be hardcoded in source.

## Development Workflow & Quality Gates

Every contribution MUST satisfy all of the following before merge:

- All five Core Principles are satisfied; violations require written justification in the
  PR description and a corresponding entry in the `Complexity Tracking` table of
  `plan.md`.
- `dotnet test` passes for all three test projects:
  `McpServer.Application.Tests`, `McpServer.Infrastructure.Tests`,
  `McpServer.Presentation.Tests`.
- All curly braces are present on `if`/`else`/`for`/`foreach`/`while` blocks, including
  single-line guard clauses (SonarQube S121).
- Public constant-like values use `public static string { get; }` except where
  compile-time constants are required for attributes or switch labels (SonarQube S3008).
- `CancellationToken` parameters in MCP tool methods MUST NOT carry `= default` unless
  preceded by other optional parameters.
- New permissions MUST be added to both `Permissions.cs` (Domain) and the appropriate
  `azure-config/*.json` App Role definition files before merging the feature.
- New tools MUST be registered via `.WithTools<T>()` in `McpServerExtensions.cs`.

Branch naming, commit messages, and PR labels follow the conventions in `CONTRIBUTING.md`.

## Governance

This constitution supersedes all other guidelines, README instructions, and inline
comments when a conflict exists. All contributors are bound by it from the moment it
is ratified.

**Amendment procedure**:

1. Open a PR that modifies this file with the proposed change and a clear rationale.
2. Bump `CONSTITUTION_VERSION` according to semantic versioning:
   - MAJOR: removal or redefinition of a principle, or removal of a mandatory constraint.
   - MINOR: new principle or section added, or material expansion of existing guidance.
   - PATCH: wording, typo, or non-semantic clarification.
3. Update `LAST_AMENDED_DATE` to the merge date (ISO 8601: YYYY-MM-DD).
4. Update `Sync Impact Report` comment with the new delta.
5. Propagate changes to affected `.specify/templates/` files in the same PR.
6. At least one reviewer MUST explicitly confirm principle compliance before approval.

Compliance is verified on every PR. Non-compliant code MUST be corrected before merge;
exceptions require explicit constitution amendment, not ad-hoc override.

Runtime development guidance: `.github/copilot-instructions.md` and the instruction
files under `.github/instructions/`.

**Version**: 1.0.0 | **Ratified**: 2026-05-02 | **Last Amended**: 2026-05-02
