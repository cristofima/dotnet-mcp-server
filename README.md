# MCP OAuth2 Security Baseline

Reference implementation of a **Model Context Protocol (MCP) server** secured with **Microsoft Entra ID** (OAuth2 + JWT) and orchestrated with **.NET Aspire**. The server exposes tools and prompts protected by role-based access control (App Roles) and calls a downstream API via OAuth 2.0 On-Behalf-Of (OBO) token exchange.

**Features**:

- **8 MCP Tools**: task CRUD, project queries, balance inquiries, budget transfer
- **4 MCP Prompts**: templates for task and project analysis
- **OAuth 2.0 On-Behalf-Of (OBO)**: token exchange for downstream API calls
- **Role-Based Access Control**: granular permissions via Entra ID App Roles

## Project Structure

```
MCP-Server/                        # MCP Server project (Clean Architecture, 4 layers)
    McpServer.Presentation/        #   Presentation: MCP tools, prompts, middleware, composition root
    McpServer.Domain/              #   Domain: permission constants, validation rules (zero dependencies)
    McpServer.Application/         #   Application: use cases, service contracts, tool result model, configuration
    McpServer.Infrastructure/      #   Infrastructure: HTTP clients, MSAL OBO, telemetry, health checks
McpServer.AppHost/                 # .NET Aspire orchestrator (starts all services)
McpServer.BackendApi/              # Backend API (EF Core SQL Server) demonstrating token exchange
McpServer.ServiceDefaults/         # Shared Aspire configuration (telemetry, health checks)
McpServer.Shared/                  # Shared Entra ID configuration models (SharedKernel)
tests/                             # Automated tests (one project per Clean Architecture layer)
```

The MCP Server follows **Clean Architecture** with explicit dependency direction: Domain → Application → Infrastructure → Presentation (Server). Each layer is a separate project with its own README.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [.NET Aspire workload](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)
- [Microsoft Entra ID tenant](https://learn.microsoft.com/en-us/entra/fundamentals/create-new-tenant) with appropriate App Roles

## Quick Start

```bash
cd src/McpServer.AppHost
dotnet run
```

This starts:

1. **MCP Server** (`:5230`): Streamable HTTP transport at `/mcp`, JWT-protected tools and prompts
2. **Backend API**: backend REST API (EF Core SQL Server) used as downstream target for OBO token exchange. Pending migrations are applied and demo data is seeded automatically at startup.

Open the **Aspire Dashboard** (URL in console) to monitor all services.

### Microsoft Entra ID Setup

**Required Configuration**:

1. Create `src/MCP-Server/McpServer.Presentation/appsettings.Development.json` using the structure in `appsettings.json` as reference
2. Configure:
   - `EntraId:TenantId`: your Azure AD tenant ID
   - `EntraId:ClientId`: Application (client) ID
   - `EntraId:ClientSecret`: client secret (use Azure Key Vault in production)
   - `DownstreamApi:Audience`: target API audience (e.g., `"api://{your-backend-api-client-id}"`)
3. Create `src/McpServer.BackendApi/appsettings.Development.json` and configure:
   - `EntraId:TenantId`, `EntraId:Audience`: same tenant, Backend API's Application ID URI

### Configuration Structure

The project uses **inheritance-based configuration** for type safety:

```
EntraIdBaseOptions (abstract)     # Instance, TenantId, common methods  [Shared/Configuration]
    ↓
    ├── EntraIdServerOptions       # ClientId, ClientSecret, Scopes, ResourceDocumentation
    │   (MCP Server)               [Infrastructure/Configuration]
    │
    └── EntraIdApiOptions          # Audience
        (Backend API)              [BackendApi/Configuration]
```

All required properties use `[Required]` DataAnnotations for early validation.

## MCP Tools

### Task Management

| Tool                 | Description                                        |
| -------------------- | -------------------------------------------------- |
| `get_tasks`          | Get all tasks for authenticated user               |
| `create_task`        | Create a new task (title, description, priority)   |
| `update_task_status` | Update task status (Pending/In Progress/Completed) |
| `delete_task`        | Delete a task by ID                                |

### Project & Balance (Token Exchange)

| Tool                  | Description                                  |
| --------------------- | -------------------------------------------- |
| `get_projects`        | List all projects from Backend API           |
| `get_project_details` | Get project details by ID                    |
| `get_project_balance` | Get financial balance for a project          |
| `transfer_budget`     | Transfer budget between projects (destructive) |

## MCP Prompts

Prompts are reusable templates that MCP clients invoke for structured analysis.

### Task Analysis

| Prompt                    | Description                           | Arguments           |
| ------------------------- | ------------------------------------- | ------------------- |
| `summarize_tasks`         | Generate a summary of all user tasks  | `status` (optional) |
| `analyze_task_priorities` | Analyze task distribution by priority | (none)              |

### Project Analysis

| Prompt             | Description                             | Arguments              |
| ------------------ | --------------------------------------- | ---------------------- |
| `analyze_project`  | Detailed analysis of a specific project | `projectId` (required) |
| `compare_projects` | Compare all projects side-by-side       | (none)                 |

For detailed tool parameters, prompt arguments, and per-tool authorization, see the [MCP Server project README](src/MCP-Server/README.md#tools-catalog).

## Authorization (App Roles)

| Permission          | Description                    | Typical Users                  |
| ------------------- | ------------------------------ | ------------------------------ |
| `mcp:task:read`     | View tasks                     | All authenticated users        |
| `mcp:task:write`    | Create/update/delete tasks     | Supervisors, managers          |
| `mcp:balance:read`  | View financial balances        | Tellers, supervisors, managers |
| `mcp:balance:write` | Transfer budget between projects | Supervisors, managers        |
| `mcp:project:read`  | View projects                  | All authenticated users        |
| `mcp:project:write` | Modify projects                | Managers only                  |

**Quick Setup for Microsoft Entra ID**:

1. Go to Azure Portal → App registrations → your MCP Server app
2. Navigate to **App roles** and create roles with these exact **Value** fields (including the `mcp:` prefix)
3. Repeat for the Backend API app registration (required for OBO flow) — use the plain values without `mcp:` prefix (e.g., `balance:write`)
4. Go to **Enterprise Applications** → Users and groups and assign users to roles in **both** Enterprise Apps
5. Roles appear in the JWT `roles` claim automatically

## Role Management in OBO Architecture

The OBO flow requires roles defined in **both** app registrations (MCP Server and Backend API). Adding a new permission involves:

1. Define the App Role in both app registrations
2. Add the constant to `Permissions.cs`
3. Assign users/groups in both Enterprise Applications

## Token Exchange Flow

**Microsoft Entra ID (OAuth 2.0 On-Behalf-Of)**:

```
MCP Client → (JWT aud:api://{server-client-id}) → MCP Server → (OBO via MSAL) → JWT aud:api://{api-client-id} → Backend API
                                                         ↕
                                                 Microsoft Entra ID
```

The MCP Server does **not** forward the user's token to Backend API. It exchanges the token via OBO to obtain a new token with the downstream API's audience. See the [MCP Server project README](src/MCP-Server/README.md#obo-security-posture) for the full security posture.

## Observability

**Local development**: The **Aspire Dashboard** (URL shown in console) provides distributed traces, metrics, and logs automatically via the OTel SDK.

**Production**: **Azure Application Insights** receives traces, metrics, and logs via the OpenTelemetry SDK OTLP exporter. Configure the `APPLICATIONINSIGHTS_CONNECTION_STRING` App Service setting to enable it. Both services use **Serilog** for structured logging, centralized in `McpServer.ServiceDefaults`.

For the OTel SDK pipeline, EF Core instrumentation, and health check filtering, see [McpServer.ServiceDefaults/README.md](src/McpServer.ServiceDefaults/README.md). For MCP Server-specific telemetry (tool spans, claim enrichment, data classification), see the [MCP Server Presentation README](src/MCP-Server/McpServer.Presentation/README.md#observability). For Backend API telemetry, see [McpServer.BackendApi/README.md](src/McpServer.BackendApi/README.md#observability).

### Production: App Service Settings

Configure the following Application Settings on each Azure App Service:

| Setting                                 | Description                                                                                       |
| --------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string from Azure Portal: Application Insights resource → Overview → Connection String |

> **Secret handling**: Store `APPLICATIONINSIGHTS_CONNECTION_STRING` in Azure Key Vault and reference it via an [App Service Key Vault reference](https://learn.microsoft.com/azure/app-service/app-service-key-vault-references). Do not embed it in source control.

### Custom Metrics

Both services emit custom metrics via `System.Diagnostics.Metrics`. Locally these flow through the OTel SDK to the Aspire Dashboard. In production, they are exported to Application Insights as custom metrics via the same OTLP pipeline. For metric definitions per service, see the [MCP Server Presentation README](src/MCP-Server/McpServer.Presentation/README.md#traces--metrics-otel-sdk) and [McpServer.BackendApi/README.md](src/McpServer.BackendApi/README.md#traces--metrics-otel-sdk).

## Testing

### Test Projects

The solution includes automated tests for each Clean Architecture layer. No Entra ID tenant or external services required: all tests use fakes and mocks.

```bash
dotnet test tests/   # run all test projects
```

| Project                                                                     | Layer          | Tests | What it covers                                                                                 |
| --------------------------------------------------------------------------- | -------------- | ----: | ---------------------------------------------------------------------------------------------- |
| [McpServer.Application.Tests](tests/McpServer.Application.Tests/)       | Application    |     9 | `McpToolResult` JSON serialization contract (field names, casing, null handling, error shapes) |
| [McpServer.Infrastructure.Tests](tests/McpServer.Infrastructure.Tests/) | Infrastructure |    12 | `DownstreamApiService` HTTP routing, OBO token exchange, response parsing, error wrapping      |
| [McpServer.Presentation.Tests](tests/McpServer.Presentation.Tests/)     | Presentation   |    27 | MCP tool/prompt authorization filtering, tool invocation, RFC 9728/8414 well-known endpoints   |

Each test project has its own README with the full test inventory.

### With MCP Inspector

```bash
npx @modelcontextprotocol/inspector
```

1. Open `http://localhost:6274`
2. Connect to `http://localhost:5230/mcp` (MCP transport)
3. Complete OAuth2 flow with your configured identity provider
4. Execute tools with authenticated session

### With VS Code Copilot Chat

**Note**: VS Code authentication requires a **work/school account** (not personal Microsoft accounts). Personal accounts can be invited as B2B guests.

## Deployment

### Infrastructure Provisioning (Terraform)

All Azure resources (App Service Plan, Web Apps, SQL Server, Key Vault, Application Insights, Entra ID app registrations) are defined in `infra/terraform/`. A single `terraform apply` provisions the full environment and sets all App Service application settings automatically — no manual portal configuration required.

See [infra/terraform/README.md](infra/terraform/README.md) for setup, variables, and first-run instructions.

### Foundry Agent Setup

After `terraform apply`, register the MCP Server as a tool in [Microsoft Foundry](https://ai.azure.com) → your project → **Tools** → **Connect a tool** → **Custom MCP Server** → **OAuth2**. Use the following values (replace `<tenant_id>` with your Entra ID tenant ID):

| Field | Value |
| ----- | ----- |
| Client ID | `foundry_agent_client_id` output |
| Client Secret | `foundry-agent-client-secret` from Key Vault |
| Scope | `api://<mcp_server_client_id>/mcp.access` |
| MCP Server URL | `mcp_server_url` output + `/mcp` |
| Token URL | `https://login.microsoftonline.com/<tenant_id>/oauth2/v2.0/token` |
| Auth URL | `https://login.microsoftonline.com/<tenant_id>/oauth2/v2.0/authorize` |
| Refresh URL | `https://login.microsoftonline.com/<tenant_id>/oauth2/v2.0/token` |

After saving, copy the **Redirect URI** shown by the portal and add it to the `app-foundry-agent` app registration under **Authentication**.

### GitHub Actions Workflows

Three workflows handle CI/CD. All use official GitHub Actions; no artifact registries required.

| Workflow | Trigger | Purpose |
| -------- | ------- | ------- |
| `ci.yml` | Push to `feature/**`, `fix/**`, `refactor/**`; PR to `main` | Build + test all projects |
| `cd-mcp-server.yml` | Push to `main` (MCP Server paths); `workflow_dispatch` | Publish and deploy MCP Server |
| `cd-backend-api.yml` | Push to `main` (Backend API paths); `workflow_dispatch` | Publish and deploy Backend API |

**Branch protection**: configure the `main` branch to require the `CI / Build and Test` status check before merging. This ensures `cd-*.yml` only triggers on commits that passed CI.

### CI/CD Strategy

No `develop` branch. The flow is: `feature/NNN-slug` → PR (CI runs) → merge to `main` (CD deploys). Each CD workflow path-filters independently: a change to MCP Server code triggers only `cd-mcp-server.yml`, and vice versa.

### Azure Setup (OIDC — no client secrets)

The CD workflows use OpenID Connect (`azure/login@v2`) — no long-lived credentials stored in GitHub.

**One-time setup per service principal**:

```bash
# 1. Create app registration
appId=$(az ad app create --display-name "github-mcp-deploy" --query appId -o tsv)

# 2. Create service principal and assign Website Contributor on each App Service
spId=$(az ad sp create --id $appId --query id -o tsv)
az role assignment create --role "Website Contributor" \
  --assignee-object-id $spId --assignee-principal-type ServicePrincipal \
  --scope /subscriptions/{subscriptionId}/resourceGroups/{rg}/providers/Microsoft.Web/sites/{appName}

# 3. Add federated credential for main branch
az ad app federated-credential create --id $appId --parameters '{
  "name": "github-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:cristofima/dotnet-mcp-server:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

**GitHub repository secrets** (Settings → Secrets and variables → Actions):

| Secret | Value |
| ------ | ----- |
| `AZURE_CLIENT_ID` | Application (client) ID |
| `AZURE_TENANT_ID` | Directory (tenant) ID |
| `AZURE_SUBSCRIPTION_ID` | Subscription ID |

**GitHub repository variables** (Settings → Secrets and variables → Actions → Variables):

| Variable | Value |
| -------- | ----- |
| `AZURE_MCP_SERVER_APP_NAME` | App Service name for the MCP Server |
| `AZURE_BACKEND_API_APP_NAME` | App Service name for the Backend API |

### App Service Configuration

When provisioning with Terraform (`infra/terraform/`), all application settings are set automatically during `terraform apply`: `EntraId__ClientId`, `EntraId__TenantId`, `EntraId__Scopes__0`, `EntraId__ClientSecret` (Key Vault reference), `DownstreamApi__Audience`, `DownstreamApi__Scopes__0`, `DownstreamApi__BaseUrl`, `APPLICATIONINSIGHTS_CONNECTION_STRING` (Key Vault reference), and `ConnectionStrings__Default` (Key Vault reference for the Backend API).

For manual or non-Terraform setups, configure settings via the Azure portal (App Service → Configuration → Application settings) or the CLI. Use Key Vault references for all secrets:

```bash
az webapp config appsettings set \
  --name {your-mcp-server-app} \
  --resource-group {your-rg} \
  --settings \
    EntraId__ClientId="{client-id}" \
    EntraId__TenantId="{tenant-id}" \
    EntraId__Scopes__0="api://{client-id}/mcp.access" \
    EntraId__ClientSecret="@Microsoft.KeyVault(SecretUri=https://your-kv.vault.azure.net/secrets/mcp-server-client-secret/)" \
    DownstreamApi__Audience="api://{backend-api-client-id}" \
    DownstreamApi__Scopes__0="api://{backend-api-client-id}/.default" \
    DownstreamApi__BaseUrl="https://{your-backend-api-app}.azurewebsites.net"
```

## Development Workflow (GitHub Spec Kit)

This project uses [GitHub Spec Kit](https://github.com/github/spec-kit) for Spec-Driven Development. Specifications, implementation plans, and task breakdowns live in `.specify/specs/` and are the source of truth for all feature development.

### Setup

Install the Specify CLI once (requires Python 3.11+ and [uv](https://docs.astral.sh/uv/)):

```bash
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git
```

After cloning the repository, regenerate the local scaffolding scripts (they are excluded from source control):

```bash
specify init . --integration copilot --script ps --force
```

If any extension scripts are missing, reinstall each extension:

```bash
specify extension add git
specify extension add spec-kit-branch-convention --from https://github.com/Quratulain-bilal/spec-kit-branch-convention/archive/refs/tags/v1.0.0.zip
```

### Files excluded from the repo

The following paths are in `.gitignore` because they are auto-generated and can be fully restored with the commands above:

| Path | Regenerated by |
| ---- | -------------- |
| `.specify/scripts/` | `specify init . --integration copilot --script ps --force` |
| `.specify/extensions/.cache/` | `specify extension add <name>` (download cache) |
| `.specify/extensions/*/scripts/` | `specify extension add <name>` |
| `.specify/extensions/.registry` | `specify extension add <name>` (contains local timestamps) |
| `.specify/integrations/` | `specify init` (contains local timestamps and file hashes) |

### Files committed to the repo

Everything else under `.specify/` is committed: `memory/constitution.md`, `templates/`, `extensions/*/commands/`, `extensions/*.yml`, `workflows/`, `init-options.json`, `integration.json`, `extensions.yml`, and `branch-convention.yml`.

### Branch naming convention

Feature branches follow the `gitflow` preset configured in `.specify/branch-convention.yml`:

| Type | Pattern | Example |
| ---- | ------- | ------- |
| feature (default) | `feature/{seq}-{kebab}` | `feature/001-update-mcp-tools` |
| bugfix | `fix/{seq}-{kebab}` | `fix/002-obo-token-exchange` |
| hotfix | `hotfix/{seq}-{kebab}` | `hotfix/003-token-expiry` |
| refactor | `refactor/{seq}-{kebab}` | `refactor/004-extract-use-case` |

Spec folders are always flat: `.specify/specs/{seq}-{kebab}/` (no type prefix).

## References

- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP Streamable HTTP Transport](https://modelcontextprotocol.io/specification/2025-03-26/basic/transports#streamable-http)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [Microsoft Entra ID](https://learn.microsoft.com/entra/fundamentals/)
- [OAuth 2.0 On-Behalf-Of Flow](https://learn.microsoft.com/entra/identity-platform/v2-oauth2-on-behalf-of-flow)
- [Azure Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/app-insights-overview)
- [Azure Monitor OpenTelemetry for .NET](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-enable?tabs=net)
- [Copilot Studio MCP Connector](https://learn.microsoft.com/microsoft-copilot-studio/agent-extend-action-mcp)
- [Deploy a Remote MCP Server and Connect to Copilot Studio](https://learn.microsoft.com/azure/developer/azure-mcp-server/how-to/deploy-remote-mcp-server-copilot-studio)
- [GitHub Spec Kit](https://github.com/github/spec-kit)
