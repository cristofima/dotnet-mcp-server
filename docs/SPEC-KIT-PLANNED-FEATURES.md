# Spec Kit: Planned Features Workflow

This document contains the GitHub Spec Kit commands and snippets for the three planned features in this repository. Run each workflow in order, one feature at a time, starting from `main`.

## Branch Naming

The `spec-kit-branch-convention` extension is already installed and configured in `.specify/branch-convention.yml`. Branches follow the gitflow pattern `{type}/{seq}-{kebab}` with sequential 3-digit numbering.

Running `/speckit.specify` will create branches like:

| Feature | Branch |
|---|---|
| Feature 1 | `feature/001-update-mcp-tools` |
| Feature 2 | `feature/002-terraform-azure-resources` |

If the extension is missing (e.g., after a fresh clone), reinstall it:

```bash
specify extension add spec-kit-branch-convention --from https://github.com/Quratulain-bilal/spec-kit-branch-convention/archive/refs/tags/v1.0.0.zip
```

---

## Prerequisites

Make sure you are on `main` and your working tree is clean before starting each feature:

```bash
git checkout main
git pull
```

---

## Feature 1: Update MCP Tools

Simplify authorization from app roles to authentication-only (workshop scope), remove Datadog-specific references from tool classes and descriptions, and align tool metadata with the current domain model.

### 1. Create the specification

```text
/speckit.specify

Update the existing MCP tools in McpServer.Presentation/Tools/.

Authorization simplification: this is a workshop repository — authorization
is based on successful authentication only. Remove [Authorize(Roles = ...)]
from every tool method. Keep [Authorize] at the class level. Do not add
scope-based policies. After removing role attributes, Permissions.cs in
McpServer.Domain/Constants/ will be unused — delete it and all references to it.
Also delete azure-config/mcp-server-roles.json and azure-config/mock-api-roles.json;
app roles are no longer used.

For the Backend API (McpServer.BackendApi), controllers must not validate
roles or scopes — JWT Bearer validation (audience, issuer, signature) is
the only authorization check. Remove any [Authorize(Roles = ...)] attributes
from controllers and any scope validation middleware.

Additional cleanup:
- Remove any Datadog-specific concerns from tool classes. Telemetry is handled
  via OpenTelemetry and McpTelemetryFilter; tool classes must not reference
  Datadog spans, DD_* environment variables, or Datadog-specific libraries.
- Ensure all tool metadata (Name, Title, ReadOnly, Destructive, Idempotent,
  OpenWorld) is accurate for each tool.
- Review all tool [Description] attributes for internal-organization references
  and rewrite them as generic, workshop-friendly text.

No new tools are added in this feature.
```

### 2. Clarify ambiguities (optional but recommended)

```text
/speckit.clarify
```

### 3. Generate the implementation plan

```text
/speckit.plan

.NET 10 Clean Architecture. Tools live in McpServer.Presentation/Tools/.
Backend API controllers live in McpServer.BackendApi/Controllers/.
Telemetry is centralized in McpTelemetryFilter — tool classes must not contain
any Stopwatch, McpActivitySource, McpMetrics, or try/catch for general errors.
All tool changes stay in the Presentation layer; use case logic stays
in Application/UseCases/; Backend API controller changes stay in McpServer.BackendApi.
Removing [Authorize(Roles = ...)] must not affect the OBO token exchange flow —
McpServer.Infrastructure still performs OBO and the Backend API still validates
the incoming JWT audience and issuer.
Files to delete: McpServer.Domain/Constants/Permissions.cs,
azure-config/mcp-server-roles.json, azure-config/mock-api-roles.json.
```

### 4. Generate the task breakdown

```text
/speckit.tasks
```

### 5. Implement

```text
/speckit.implement
```

---

## Feature 2: Terraform Configuration for Azure Resources

Create Terraform configuration to provision all Azure resources required by the MCP Server and Backend API: App Service Plan, App Services, SQL Server, Key Vault, Application Insights, and three Entra ID app registrations with the full OAuth2 delegated scope chain.

### 1. Create the specification

```text
/speckit.specify

Create Terraform configuration to provision the Azure infrastructure for
this MCP server workshop using two providers: azurerm (infrastructure)
and azuread (Entra ID / identity).

Infrastructure resources (azurerm):
- Azure App Service Plan (Linux, .NET 10) — shared by both services
- Azure App Service for the MCP Server (McpServer.Presentation)
- Azure App Service for the Backend API (McpServer.BackendApi)
- Azure SQL Server and Azure SQL Database (used by the Backend API via EF Core)
- Azure Application Insights (shared by both services)
- Azure Log Analytics Workspace (required by Application Insights)
- Azure Key Vault (stores the MCP Server client secret and App Insights
  connection string; App Services read them via Key Vault references)

Identity resources (azuread) — all three with
sign_in_audience = AzureADandPersonalMicrosoftAccount (supports hotmail,
outlook.com, live.com, and any work/school account):

1. Backend API app registration (resource server, no client secret):
   Exposes one OAuth2 delegated scope: user_impersonation
   (admin_consent_required = false).
   No granular scopes, no app roles. The Backend API validates JWT audience
   and issuer only; it does not enforce scopes or roles in controllers.

2. MCP Server app registration (confidential client):
   Exposes one OAuth2 delegated scope: mcp.access
   (this is what Agent clients request when authenticating users).
   Has required_resource_access pointing to the Backend API user_impersonation
   scope (requested during the OBO exchange).
   Has one client secret; store it in Key Vault.

3. Foundry OAuth Passthrough registration (confidential client):
   Used to register the MCP Server as a tool inside Azure AI Foundry via the
   OAuth Passthrough connection type. Foundry performs the auth code flow on
   behalf of the logged-in user using this registration's credentials, obtains
   a delegated token scoped to mcp.access, and passes it through to the MCP
   Server on every tool call. No custom frontend or backend is needed.
   Has required_resource_access pointing to the MCP Server mcp.access scope.
   Has one client secret; store it in Key Vault.
   The Foundry project OAuth callback redirect URI (e.g.,
   https://<project>.services.ai.azure.com/...) cannot be known at Terraform
   apply time — add it to the web platform redirect URIs manually after the
   Foundry project is created.
   Works with organizational accounts. Personal MSA accounts (hotmail, outlook,
   live) should also work provided Foundry resolves the authorization endpoint
   via /common; validate after Foundry project setup.

No app_role blocks on any of the three registrations.

Remote state in Azure Blob Storage (container created manually before first apply).
Naming convention: {resource_type}-{project}
(e.g., app-mcp-server, app-backend-api, sql-backend-api, kv-mcp-server).
Single environment (production).
terraform.tfvars.example committed; actual tfvars and .terraform/ excluded
via .gitignore.
Entra ID tenant ID and Azure subscription ID are required input variables.
Outputs: App Service URLs, Application Insights connection string,
app registration client IDs for all three (not secrets).
```

### 2. Clarify ambiguities

```text
/speckit.clarify
```

### 3. Generate the implementation plan

```text
/speckit.plan

Terraform ~1.9, AzureRM provider ~3.x, AzureAD provider ~2.x.
Flat module structure: one root module with locals and variables,
no child modules for this initial phase.
Remote state: azurerm backend, Azure Blob Storage container created manually.
Naming convention: {resource_type}-{project}
(e.g., app-mcp-server, kv-mcp-server, sql-backend-api).
Secrets: MCP Server client secret and Foundry OAuth Passthrough client secret
  go to Key Vault. The Backend API registration has no client secret.
No secrets in tfvars; use terraform.tfvars.example with placeholder values.
Add standard Terraform .gitignore entries: .terraform/, terraform.tfstate,
terraform.tfstate.backup, *.tfvars (except *.tfvars.example).
No app_role blocks on any azuread_application resource.
Scope chain implemented via:
  - azuread_application.backend_api: one oauth2_permission_scope block (user_impersonation)
  - azuread_application.mcp_server: one oauth2_permission_scope block (mcp.access)
    plus required_resource_access referencing backend_api user_impersonation
  - azuread_application.agent: confidential client, required_resource_access
    referencing mcp.access, client secret in Key Vault; web platform redirect
    URI left empty (added manually post-apply with the Foundry project URL).
sign_in_audience = AzureADandPersonalMicrosoftAccount on all three.
Key Vault access: App Service managed identity granted Key Vault Secrets User role.
```

### 4. Generate the task breakdown

```text
/speckit.tasks
```

### 5. Implement

```text
/speckit.implement
```

---

## Feature 3: GitHub Actions Workflow Updates

> **Status: implemented.** The three workflows below were created directly during the initial open-source cleanup and do not need to be generated via Spec Kit.
>
> | Workflow | File | Status |
> |---|---|---|
> | CI — build + test | `.github/workflows/ci.yml` | Done |
> | CD — MCP Server | `.github/workflows/cd-mcp-server.yml` | Done |
> | CD — Backend API | `.github/workflows/cd-backend-api.yml` | Done |
>
> If future workflow changes are needed (e.g., adding `APPLICATIONINSIGHTS_CONNECTION_STRING` as an App Service Application Setting in CD workflows, or adding branch protection rules), start a new feature with `/speckit.specify` describing the delta changes.

---

## Spec artifacts location

Each feature generates its artifacts in `.specify/specs/{branch-name}/`:

```
.specify/specs/
├── 001-update-mcp-tools/
│   ├── spec.md
│   ├── plan.md
│   └── tasks.md
├── 002-terraform-azure-resources/
│   ├── spec.md
│   ├── plan.md
│   ├── data-model.md        # resource naming, variable definitions, provider versions
│   ├── research.md          # AzureAD provider azuread_application API, sign_in_audience values
│   └── tasks.md
└── 003-github-workflows/     ← already implemented; no spec artifacts expected
```

All files under `.specify/specs/` are committed to the repository. Scripts and installation state files are excluded — see `.gitignore` and [README.md](../README.md#development-workflow-github-spec-kit) for details.
