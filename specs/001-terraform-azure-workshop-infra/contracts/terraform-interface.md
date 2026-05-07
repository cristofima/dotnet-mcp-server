# Terraform Interface Contract

**Feature**: `001-terraform-azure-workshop-infra`  
**Branch**: `feature/001-terraform-azure-workshop-infra`  
**Phase**: 1 — Produced by `/speckit.plan`

This document defines the public interface of the Terraform root module in `infra/terraform/`. It covers inputs (variables), outputs, and the backend pre-requisites that callers (workshop operators) must satisfy before running Terraform commands.

---

## Input Variables (variables.tf)

These are the values that must be supplied via `terraform.tfvars` or `-var` flags.

### Required Variables (no default)

| Variable | Type | Sensitive | Validation |
|---|---|---|---|
| `tenant_id` | `string` | No | Must be a valid GUID — the Entra ID tenant ID |
| `subscription_id` | `string` | No | Must be a valid GUID — the Azure subscription ID |
| `sql_admin_username` | `string` | Yes | Non-empty; avoid reserved words (`admin`, `sa`, `root`) |
| `sql_admin_password` | `string` | Yes | Must satisfy Azure SQL complexity: ≥ 8 chars, uppercase, lowercase, digit, special char |

### Optional Variables (with defaults)

| Variable | Type | Default | Notes |
|---|---|---|---|
| `location` | `string` | `"centralus"` | Any valid Azure region identifier |
| `dotnet_version` | `string` | `"10.0"` | App Service application stack version (azurerm ~> 4.0 required) |

### Example `terraform.tfvars.example`

```hcl
# Copy this file to terraform.tfvars and fill in real values.
# Never commit terraform.tfvars to version control.

tenant_id        = "<your-entra-id-tenant-id>"
subscription_id  = "<your-azure-subscription-id>"
location         = "centralus"

sql_admin_username = "sqladmin"
sql_admin_password = "<your-secure-sql-password>"
```

---

## Outputs (outputs.tf)

These values are available after `terraform apply` completes.

| Output | Sensitive | Description | Example Value |
|---|---|---|---|
| `mcp_server_url` | No | MCP Server public HTTPS URL | `https://app-mcp-server.azurewebsites.net` |
| `backend_api_url` | No | Backend API public HTTPS URL | `https://app-backend-api.azurewebsites.net` |
| `app_insights_connection_string` | **Yes** | Application Insights connection string | *(hidden — read from Key Vault)* |
| `backend_api_client_id` | No | Backend API Entra ID application (client) ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `mcp_server_client_id` | No | MCP Server Entra ID application (client) ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `foundry_agent_client_id` | No | Foundry agent Entra ID application (client) ID | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `key_vault_uri` | No | Key Vault URI for manual secret retrieval | `https://kv-mcp-server.vault.azure.net/` |

To view all outputs after apply:

```bash
terraform output
# For sensitive values:
terraform output -json app_insights_connection_string
```

---

## Backend Pre-requisites

The `azurerm` backend uses partial configuration (R-011). The storage infrastructure must be created **before** running `terraform init`.

### Required Storage Resources (create manually)

| Resource | Name | Notes |
|---|---|---|
| Resource Group | operator's choice | Does not have to be `mcp-server-baseline` |
| Storage Account | operator's choice | LRS is sufficient; must have hierarchical namespace disabled |
| Blob Container | `tfstate` (recommended) | Private access level |

### Backend Configuration File (`backend.conf`)

Create this file locally (not committed to version control):

```hcl
resource_group_name  = "<rg-containing-storage-account>"
storage_account_name = "<storage-account-name>"
container_name       = "tfstate"
key                  = "mcp-server.tfstate"
```

### Init Command

```bash
terraform init -backend-config=backend.conf
```

---

## Identity / Permissions Pre-requisites

The operator running `terraform apply` needs the following permissions:

| Scope | Role / Permission |
|---|---|
| Azure Subscription | `Contributor` (to create resource group and all resources) |
| Azure Subscription | `User Access Administrator` (to create role assignments for managed identities) |
| Entra ID Tenant | `Application Administrator` or `Cloud Application Administrator` (to create app registrations) |

**Note**: `Owner` on the subscription covers all of the above. `Contributor` alone is insufficient because it cannot create role assignments (`azurerm_role_assignment`).

---

## Resource Naming Convention

All Azure resources follow the `{resource_type}-{project}` pattern (FR-016).

| Azure Resource | Azure Name |
|---|---|
| Resource Group | `mcp-server-baseline` |
| App Service Plan | `plan-mcp-server` |
| MCP Server App | `app-mcp-server` |
| Backend API App | `app-backend-api` |
| SQL Server | `sql-backend-api` |
| SQL Database | `db-backend-api` |
| Log Analytics Workspace | `log-mcp-server` |
| Application Insights | `appi-mcp-server` |
| Key Vault | `kv-mcp-server` |

**Note**: Azure resource names must be globally unique for some resource types (Key Vault, SQL Server, App Service). If the names above are already taken in another subscription, the operator must adjust them and update the Terraform configuration accordingly.

---

## Constraints and Non-Goals

- **No child modules**: The root module is flat. All resources are defined directly in `infra/terraform/`. No `module {}` blocks are used.
- **No `app_role` blocks**: No App Role definitions appear on any `azuread_application` resource (FR-014). Roles are expected to be added manually in Entra ID after apply, per the main project's permission setup guide.
- **Foundry redirect URI not managed**: The Foundry agent app registration's redirect URI (`web.redirect_uris`) is provisioned as an empty list. The actual Foundry project URL must be added manually after the Foundry project is created (FR-013).
- **Single environment**: The configuration targets a single environment. Multi-environment support (dev/staging/prod workspaces) is out of scope.
- **No application code**: The Terraform configuration provisions infrastructure only. Application deployment is handled by GitHub Actions (`deploy-mcp-server.yml`, `deploy-mock-api.yml`).
