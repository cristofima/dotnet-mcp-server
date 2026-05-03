# MockApi — JWT-Protected Backend API

## Overview

`MockApi` is a backend REST API secured with **JWT Bearer authentication** using **Microsoft Entra ID**. It acts as the downstream API that the MCP Server calls on behalf of users via [OAuth 2.0 On-Behalf-Of (OBO)](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow).

End users do not call this API directly. The MCP Server exchanges the user's token for a new one with `aud: api://{api-client-id}` and forwards that to this API.

- **Ports**: HTTPS (dynamic via Aspire), HTTP fallback
- **Target Framework**: .NET 10
- **Authentication**: JWT Bearer (Microsoft Entra ID)
- **Logging**: Centralized via ServiceDefaults (`AddSerilogDefaults()`, structured JSON, trace correlation)
- **Database**: EF Core SQL Server. No external database required. Demo data seeded on first run.
- **Architecture**: Controllers + EF Core (Fluent API configuration via `IEntityTypeConfiguration<T>`)

## Architecture

```
MCP Client → MCP Server → [OBO Token Exchange] → MockApi
                                ↓
              aud: api://{server-client-id} → aud: api://{api-client-id}
```

MockApi **only accepts** tokens with `aud: api://{api-client-id}`. Tokens are issued via [OAuth 2.0 On-Behalf-Of (OBO)](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow) using Microsoft Entra ID.

## Authentication

### JWT Validation

| Parameter            | Value                                               |
| -------------------- | --------------------------------------------------- |
| Authority (metadata) | Microsoft Entra ID authority URL                    |
| Valid Issuers        | Both V1 and V2 endpoints for compatibility          |
| Valid Audience       | `api://{api-client-id}` (Application ID URI format) |
| HTTPS metadata       | Required in production, relaxed in Development      |

**Key detail**: The `Authority` is constructed from `EntraIdApiOptions` (Instance + TenantId) and used for OIDC metadata discovery and JWT signature validation.

### Configuration

**Required Configuration** - All values must be present in `appsettings.json` (or overridden via environment variables / Azure App Service Application Settings):

```json
{
  "EntraId": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
    "Audience": "api://YOUR_API_CLIENT_ID"
  }
}
```

**Configuration Structure** - `EntraIdApiOptions` inherits from `EntraIdBaseOptions` (`McpServer.Shared`):

- **EntraIdBaseOptions** (abstract, in `McpServer.Shared/Configuration/`): Common properties
  - `Instance`: Azure AD instance URL (e.g., `https://login.microsoftonline.com/`)
  - `TenantId`: Azure AD tenant ID
  - Methods: `GetAuthority()`, `GetAuthorityV2()`, `GetValidIssuers()`

- **EntraIdApiOptions** (concrete, in `McpServer.BackendApi/Configuration/`): API-specific properties
  - `Audience`: required JWT audience in Application ID URI format (`api://{client-id}`)

**Critical Configuration Rules**:

- `EntraId:Instance` must be a valid Azure AD instance URL
- `EntraId:TenantId` must be your Azure AD tenant GUID
- `EntraId:Audience` must use Application ID URI format: `"api://{client-id}"`
- No hardcoded defaults; the application throws at startup if required values are missing
- DataAnnotations validation with `[Required]` attributes provides early failure on misconfiguration

## Authorization

Controllers use **role-based authorization** directly with **granular permissions** (App Roles) via `[Authorize(Roles = Permissions.XXX)]`:

| Permission     | Controllers/Endpoints                                                                         |
| -------------- | --------------------------------------------------------------------------------------------- |
| `task:read`    | TasksController (GET `/api/tasks`, GET `/api/tasks/{id}`)                                     |
| `task:write`   | TasksController (POST `/api/tasks`, PATCH `/api/tasks/{id}/status`, DELETE `/api/tasks/{id}`) |
| `project:read` | ProjectsController (All endpoints: `/api/projects/*`)                                         |
| `balance:read` | BalancesController (GET `/api/balances/{projectNumber}`)                                      |
| `admin:access` | AdminController (All endpoints: `/api/admin/*`)                                               |

These permissions are extracted from the JWT `roles` claim and must be configured as **App Roles** in the Entra ID application registration. See `docs/PERMISSION-SETUP-GUIDE.md` for complete configuration instructions.

## Endpoints

### Public

| Method | Path      | Description            |
| ------ | --------- | ---------------------- |
| GET    | `/health` | Health check (no auth) |

### Authenticated

| Method | Path                            | Required Role  | Description           |
| ------ | ------------------------------- | -------------- | --------------------- |
| GET    | `/api/projects`                 | `project:read` | List all projects     |
| GET    | `/api/projects/{id}`            | `project:read` | Get project details   |
| GET    | `/api/balances/{projectNumber}` | `balance:read` | Get financial balance |
| GET    | `/api/admin/users`              | `admin:access` | List users            |
| GET    | `/api/tasks`                    | `task:read`    | List all tasks        |
| GET    | `/api/tasks/{id}`               | `task:read`    | Get task by ID        |
| POST   | `/api/tasks`                    | `task:write`   | Create new task       |
| PATCH  | `/api/tasks/{id}/status`        | `task:write`   | Update task status    |
| DELETE | `/api/tasks/{id}`               | `task:write`   | Delete task           |

### Sample Responses

**List endpoint** (`GET /api/projects`):

```json
{
  "metadata": {
    "count": 3
  },
  "data": [
    {
      "id": "PRJ001",
      "name": "Project Alpha",
      "status": "Active",
      "budget": 150000
    },
    {
      "id": "PRJ002",
      "name": "Project Beta",
      "status": "Planning",
      "budget": 75000
    },
    {
      "id": "PRJ003",
      "name": "Project Gamma",
      "status": "Completed",
      "budget": 200000
    }
  ]
}
```

**Single item** (`GET /api/projects/{id}`):

```json
{
  "metadata": {},
  "data": {
    "id": "PRJ001",
    "name": "Project Alpha",
    "status": "Active",
    "budget": 150000,
    "details": {}
  }
}
```

**Item with context** (`GET /api/balances/PRJ001`):

```json
{
  "metadata": {
    "projectNumber": "PRJ001"
  },
  "data": {
    "allocated": 150000.0,
    "spent": 92500.75,
    "remaining": 57499.25,
    "committed": 18000.0,
    "available": 39499.25,
    "currency": "USD",
    "lastUpdated": "2026-03-18T10:00:00+00:00"
  }
}
```

## Running

### Via .NET Aspire (recommended)

```powershell
cd McpServer.AppHost
dotnet run
```

MockApi starts automatically with service discovery.

### Standalone

```powershell
cd McpServer.BackendApi
dotnet run
```

Requires Microsoft Entra ID configured with appropriate App Roles. See `docs/ENTRA-ID-TESTING-GUIDE.md` for setup instructions.

## File Structure

```
McpServer.BackendApi/
├── Controllers/
│   ├── AdminController.cs         # Admin-only operations (/api/admin/*)
│   ├── BalancesController.cs      # Balance queries (/api/balances/*)
│   ├── ProjectsController.cs      # Project operations (/api/projects/*)
│   └── TasksController.cs         # Task CRUD operations (/api/tasks/*)
├── Data/
│   ├── MockApiDbContext.cs        # EF Core DbContext (SQL Server): Tasks, Projects, Users, Balances
│   ├── DbSeeder.cs                # Seeds demo data with fixed timestamps (tasks, projects, users, balances)
│   ├── DatabaseSeedingService.cs  # IHostedService: seeds demo data at startup
│   ├── Entities/
│   │   ├── TaskEntity.cs          # Task entity
│   │   ├── ProjectEntity.cs       # Project entity
│   │   ├── UserEntity.cs          # User entity
│   │   └── BalanceEntity.cs       # Balance entity per project
│   └── EntityConfigurations/
│   │   ├── TaskEntityConfiguration.cs     # Fluent API: key, column lengths, indexes
│   │   ├── ProjectEntityConfiguration.cs  # Fluent API: key, column lengths, index
│   │   ├── BalanceEntityConfiguration.cs  # Fluent API: key, column length, index
│   │   └── UserEntityConfiguration.cs     # Fluent API: key, column lengths
├── Configuration/
│   └── EntraIdApiOptions.cs       # Entra ID config for MockApi (inherits EntraIdBaseOptions from Shared)
├── Extensions/
│   └── AuthenticationExtensions.cs # JWT Bearer + Entra ID configuration
├── Filters/
│   └── ApiTelemetryFilter.cs       # Action Filter for creating spans around controller actions
├── Models/
│   ├── TaskModels.cs              # Task request models (CreateTask, UpdateStatus)
│   └── Responses/
│       ├── AdminResponses.cs      # Admin DTOs (UserInfo, AdminConfigMetadata)
│       ├── ApiResponse.cs         # Generic response wrappers (ApiResponse, ApiListResponse)
│       ├── BalanceResponses.cs    # Balance DTOs (BalanceDetails, BalanceMetadata)
│       ├── CommonResponses.cs     # Shared response types
│       ├── ProjectResponses.cs    # Project DTOs (ProjectSummary, ProjectWithDetails)
│       └── TaskResponses.cs       # Task DTOs (TaskItemResponse, TaskDeleteMetadata)
├── Services/
│   ├── ITaskService.cs            # Task service interface
│   ├── TaskService.cs             # EF Core task CRUD operations
│   ├── IProjectService.cs         # Project service interface
│   ├── ProjectService.cs          # EF Core project queries
│   ├── IUserService.cs            # User service interface
│   ├── UserService.cs             # EF Core user queries
│   ├── IBalanceService.cs         # Balance service interface
│   └── BalanceService.cs          # EF Core balance queries
├── Telemetry/
│   ├── ApiActivitySource.cs       # OpenTelemetry ActivitySource for MockAPI backend operations
│   └── ApiMetrics.cs              # OpenTelemetry Metrics for MockAPI backend operations
├── Program.cs                     # Entry point with DI and EF Core setup
├── appsettings.json               # Entra ID config
├── appsettings.Development.json
├── Properties/
│   └── launchSettings.json        # OTEL_SEMCONV_STABILITY_OPT_IN=database set per profile
└── logs/                          # Rolling log files (mockapi-{date}.log)
```

**Note**: `EntraIdApiOptions` is in `Configuration/` (this project), inheriting from `EntraIdBaseOptions` in `McpServer.Shared/Configuration/`. No custom validators are needed; DataAnnotations provide all validation.

### Design Decisions

- **EF Core SQL Server**: relational store backed by `(localdb)\mssqllocaldb` in development and Azure SQL in production. EF Core SQL Server spans are captured by `AddEntityFrameworkCoreInstrumentation()` and visible in both the Aspire Dashboard and Azure Monitor.
- **Fluent API over Data Annotations**: all schema constraints (column lengths, decimal precision, indexes, keys) are centralized in `EntityConfigurations/` via `IEntityTypeConfiguration<T>` and auto-applied via `ApplyConfigurationsFromAssembly()`. Entities are clean POCOs with no persistence concerns.
- **`DatabaseSeedingService`**: `IHostedService` that runs `MigrateAsync()` then `DbSeeder.SeedData()` sequentially before the app starts accepting requests. This applies any pending migrations and guarantees tables exist before any controller handles a request.
- **`DbSeeder` is idempotent**: each seed method checks whether data already exists before inserting, so restarting the app does not duplicate rows.
- **Controllers** for service endpoints requiring authorization and business logic
- **Minimal APIs** for lightweight endpoints (health check, public info)
- **Extension methods** for cross-cutting concerns (authentication, user extraction)
- **Records** for immutable response DTOs with type-safe generics
- **DateTimeOffset** for all timestamps (timezone-safe, banking standard)
- **Envelope pattern** (`{ metadata, data }`)

## Observability

### Logging (Serilog — Centralized via ServiceDefaults)

Logging is fully centralized in `McpServer.ServiceDefaults` via the `AddSerilogDefaults()` extension method. MockApi's `Program.cs` simply calls:

```csharp
builder.Host.AddSerilogDefaults();
```

This configures Serilog with:

- **Console sink**: text template with timestamp, level, source context, message, exception
- **File sink**: `RenderedCompactJsonFormatter` (structured JSON), daily rolling, 5-file retention (`logs/McpServer-mockapi-*.log`)
- **Enrichers**: `FromLogContext`, `WithSpan()` (TraceId/SpanId/ParentId), `WithMachineName()`, `WithThreadId()`
- **`writeToProviders: true`**: bridges Serilog into `Microsoft.Extensions.Logging`, which feeds the OTel SDK

Logs are exported via the same OTel SDK pipeline as traces and metrics; there is no separate Serilog OTLP sink.

### Traces & Metrics (OTel SDK)

Traces and metrics use the standard OpenTelemetry .NET SDK configured in `McpServer.ServiceDefaults`:

- **Traces**: ASP.NET Core, HttpClient, plus custom `ApiActivitySource` (controller-level spans via `ApiTelemetryFilter`). `enduser.id` uses the Entra ID `oid` claim exclusively (same standard as MCP Server) for cross-app span correlation.
- **Metrics**: ASP.NET Core, HttpClient, Runtime, plus custom `ApiMetrics`
- **Export**: `UseOtlpExporter()` activates when `OTEL_EXPORTER_OTLP_ENDPOINT` is set

**Custom Metrics** (meter: `McpServer.BackendApi`):

| Metric                     | Type            | Tags                                                        | Description                |
| -------------------------- | --------------- | ----------------------------------------------------------- | -------------------------- |
| `api.endpoint.invocations` | Counter → Dist. | `api.controller`, `api.action`, `http.response.status_code` | Endpoint invocation count  |
| `api.endpoint.errors`      | Counter → Dist. | `api.controller`, `api.action`, `http.response.status_code` | API errors (4xx/5xx)       |
| `api.endpoint.duration`    | Histogram       | `api.controller`, `api.action`, `http.response.status_code` | Endpoint execution time ms |

Locally these flow to the Aspire Dashboard via OTLP. In production, they are exported to Azure Monitor as custom metrics via `UseAzureMonitor()`, activated by the `APPLICATIONINSIGHTS_CONNECTION_STRING` App Service setting.

| Variable                                | Environment | Purpose                                              |
| --------------------------------------- | ----------- | ---------------------------------------------------- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Production  | Azure Monitor export: traces, metrics, and logs      |
| `OTEL_EXPORTER_OTLP_ENDPOINT`           | Development | Aspire Dashboard collector endpoint                  |
| `OTEL_EXPORTER_OTLP_PROTOCOL`           | Development | Wire protocol (e.g., `grpc`)                         |
| `OTEL_SERVICE_NAME`                     | Both        | Service identity in telemetry                        |
| `OTEL_SEMCONV_STABILITY_OPT_IN`         | Both        | EF Core DB attribute conventions (see note below)    |

> **`OTEL_SEMCONV_STABILITY_OPT_IN` scope**: controls the EF Core DB attribute schema emitted by `AddEntityFrameworkCoreInstrumentation()`. With SQL Server, database spans are visible both locally via the Aspire Dashboard and in production via Azure Monitor. See `McpServer.ServiceDefaults/README.md` for the full attribute convention table.

See `McpServer.ServiceDefaults/README.md` for shared telemetry configuration.
