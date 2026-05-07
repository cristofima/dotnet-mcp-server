# McpBaseline.Shared

SharedKernel project that exists because this repository is an **end-to-end demo** where the MCP Server and the Backend API (MockApi) live in the same solution. In production, each service would be its own repository with its own copy of these types.

## Why This Project Exists

Both the MCP Server (confidential client) and MockApi (resource server) authenticate with Microsoft Entra ID. They share:

- A common base configuration class (`EntraIdBaseOptions`) with tenant, authority, and token validation logic.
- JWT Bearer event logging (`JwtBearerEventFactory`).
- Claim extraction helpers (`ClaimsPrincipalExtensions`, `EntraClaimTypes`).
- Configuration binding utilities (`ConfigurationExtensions`).

Duplicating these files across projects inside the same repo would violate DRY and create divergence risk. Shared avoids that while the demo remains a monorepo.

## Current Contents

```
McpBaseline.Shared/
├── Configuration/
│   └── EntraIdBaseOptions.cs          # Abstract base: Instance, TenantId, authority helpers, token validation
├── Constants/
│   └── EntraClaimTypes.cs             # Entra ID long-form claim URIs (ObjectId, TenantId, Scope)
├── Extensions/
│   ├── ClaimsPrincipalExtensions.cs   # GetUserName() extension method
│   └── ConfigurationExtensions.cs     # GetRequiredSection<T>() for typed config binding
└── Security/
    └── JwtBearerEventFactory.cs       # Standardized JWT Bearer events with Serilog logging
```

`EntraIdServerOptions` and `EntraIdApiOptions` were already moved to their owning projects (`Infrastructure/Configuration/` and `MockApi/Configuration/` respectively).

## Where Each File Belongs in Clean Architecture

When the MCP Server and Backend API are **separate repositories**, the Shared project disappears. Each file must be copied to the correct layer in each repository that needs it.

### Target Location per Repository

| File | MCP Server repo | Backend API repo | Layer | Rationale |
|------|----------------|-----------------|-------|-----------|
| `EntraIdBaseOptions.cs` | `Infrastructure/Configuration/` | `Configuration/` (root or `Infrastructure/`) | Infrastructure | Depends on `Microsoft.IdentityModel.Tokens`; configures external identity provider |
| `EntraClaimTypes.cs` | `Infrastructure/Constants/` | `Constants/` | Infrastructure | Entra ID claim URIs are identity provider specifics, not domain concepts |
| `ClaimsPrincipalExtensions.cs` | `Infrastructure/Extensions/` | `Extensions/` | Infrastructure | Extends `ClaimsPrincipal` (ASP.NET Core type); used by telemetry and security |
| `ConfigurationExtensions.cs` | `Infrastructure/Extensions/` or `Presentation/Extensions/` | `Extensions/` | Infrastructure / Presentation | Extends `IConfiguration` (framework type); used at composition root |
| `JwtBearerEventFactory.cs` | `Infrastructure/Security/` or `Presentation/Extensions/` | `Security/` or `Extensions/` | Infrastructure / Presentation | Depends on `JwtBearerEvents` (ASP.NET Core auth); wires authentication pipeline |

### Current Consumers

This table shows which projects use each file today, confirming that both services need their own copy.

| File | MCP Server consumers | MockApi consumers |
|------|---------------------|-------------------|
| `EntraIdBaseOptions.cs` | `IdentityProviderExtensions`, `AuthenticationExtensions` (Presentation) | `AuthenticationExtensions`, `Program.cs` |
| `EntraClaimTypes.cs` | `McpActivitySource` (Infrastructure/Telemetry) | `ApiTelemetryFilter` |
| `ClaimsPrincipalExtensions.cs` | via `JwtBearerEventFactory` | `TasksController`, `BalancesController`, `AdminController`, `ProjectsController`, via `JwtBearerEventFactory` |
| `ConfigurationExtensions.cs` | `AuthenticationExtensions` (Presentation) | `AuthenticationExtensions` |
| `JwtBearerEventFactory.cs` | `AuthenticationExtensions` (Presentation) | `AuthenticationExtensions` |

### Migration Steps (per repository)

When creating a new MCP Server or Backend API from scratch:

1. **Copy** each file to the target location listed above.
2. **Update the namespace** to match the new project (e.g., `MyMcpServer.Infrastructure.Configuration`).
3. **Update `EntraIdServerOptions`/`EntraIdApiOptions`** to inherit from the local `EntraIdBaseOptions` copy.
4. **Remove** any `using McpBaseline.Shared.*` references.
5. **Verify** the build compiles and all `[Required]` + `ValidateOnStart()` registrations resolve correctly.

## Until There Is an SDK or Template

There is no MCP Server SDK or `dotnet new` template that scaffolds the Entra ID authentication layer today. Until one exists:

- **New MCP Server projects**: manually copy these files from this baseline into the `Infrastructure` layer. Adjust namespaces and configuration section names as needed.
- **New Backend API projects**: same process, copying into the equivalent layer of the API project.
- **GitHub template repository**: when this baseline is published as a GitHub template (`Use this template` button), these files come included. The first task after creating a repo from the template is to inline Shared contents into each project and delete the Shared project reference, since the new repo will contain only one service.

## Dependencies

This project deliberately keeps a minimal dependency footprint:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" />
<PackageReference Include="OpenTelemetry" />
<PackageReference Include="Serilog" />
```

All dependencies are infrastructure-level, which confirms these types belong in the Infrastructure layer (or at the composition root) in a Clean Architecture structure.
