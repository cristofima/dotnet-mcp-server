# Terraform Infrastructure

Provisions all Azure and Entra ID resources for the MCP OAuth2 Security Baseline workshop.
State is stored in Azure Blob Storage (remote backend).

## Prerequisites

- Terraform `~> 1.9`
- Azure CLI, authenticated (`az login`)
- An Entra ID tenant where you have permission to register applications
- The bootstrap script run once to create the remote state backend

## Providers

| Provider | Source | Version |
|---|---|---|
| `azurerm` | `hashicorp/azurerm` | `~> 4.0` |
| `azuread` | `hashicorp/azuread` | `~> 2.0` |
| `azapi` | `azure/azapi` | `~> 2.0` |

## First-time Setup

Run the bootstrap script before `terraform init`. It creates a resource group, storage account, and blob container for remote state, then writes `backend.conf`:

```powershell
../bootstrap.ps1
```

Then initialize Terraform with the generated backend config:

```powershell
terraform init '-backend-config=backend.conf'
```

## Variables

Copy `terraform.tfvars.example` to `terraform.tfvars` and fill in your values. The file is gitignored.

| Variable | Required | Description |
|---|---|---|
| `prefix` | Yes | 3-8 lowercase alphanumeric chars; prepended to globally unique names (Key Vault, SQL Server, App Services) |
| `tenant_id` | Yes | Entra ID tenant ID — `az account show --query tenantId -o tsv` |
| `subscription_id` | Yes | Azure subscription ID — `az account show --query id -o tsv` |
| `location` | No | Region for SQL Server, Key Vault, Log Analytics, App Insights. Default: `centralus` |
| `app_service_location` | No | Region for the App Service Plan and Web Apps. Default: `centralus` |
| `sql_admin_username` | Yes | SQL Server admin login. Reserved words (`admin`, `sa`, `root`) are rejected by Azure |
| `sql_admin_password` | Yes | SQL Server admin password. Must include uppercase, lowercase, digit, and special character |
| `dotnet_version` | No | .NET application stack version for App Services. Default: `10.0` |

> `location` and `app_service_location` can point to different regions. Some Azure subscriptions have quota restrictions that differ by resource type within the same region. Splitting the two variables lets you target each resource type to a region where your subscription has capacity.

## Apply

```powershell
terraform plan  -var-file=terraform.tfvars
terraform apply -var-file=terraform.tfvars -auto-approve
```

## Resources Created

| File | Resources |
|---|---|
| `main.tf` | Resource group, Log Analytics workspace, Application Insights |
| `entra-id.tf` | 3 app registrations (`app-backend-api`, `app-mcp-server`, `app-foundry-agent`), 3 service principals, 2 app passwords |
| `keyvault.tf` | Key Vault (`kv-<prefix>`), Secrets Officer role for the Terraform operator, 4 secrets |
| `sql.tf` | SQL Server (`sql-<prefix>`), database (`db-backend-api`), firewall rule for Azure services |
| `app-service.tf` | App Service Plan (`plan-<prefix>`, B1 Linux), 2 web apps with unique default hostnames (`<name>-<hash>.<region>.azurewebsites.net`), Key Vault Secrets User role for each app's managed identity |

## Outputs

| Output | Description |
|---|---|
| `mcp_server_url` | MCP Server public HTTPS URL |
| `backend_api_url` | Backend API public HTTPS URL |
| `key_vault_uri` | Key Vault URI |
| `mcp_server_client_id` | MCP Server Entra ID application client ID |
| `backend_api_client_id` | Backend API Entra ID application client ID |
| `foundry_agent_client_id` | Foundry agent Entra ID application client ID |
| `app_insights_connection_string` | Application Insights connection string (sensitive) |

View sensitive outputs with:

```powershell
terraform output -json
```

## Post-Apply Manual Steps

Two actions cannot be automated via Terraform and must be done in the Entra ID portal after `apply` completes:

1. **Grant admin consent**: In [Entra ID Portal](https://entra.microsoft.com) → App registrations → `app-mcp-server` → API permissions → "Grant admin consent". Repeat for `app-foundry-agent`.
2. **Foundry redirect URI**: Add the Foundry project redirect URI to the `app-foundry-agent` app registration once the project is created.

## State Backend

State is stored in Azure Blob Storage, configured via `backend.conf` (gitignored). The bootstrap script creates a dedicated resource group `rg-tf-state` separate from the workshop resources so that `terraform destroy` on the main infrastructure does not affect the state storage.

## Destroy

```powershell
terraform destroy -var-file=terraform.tfvars
```

The Entra ID app registrations, Key Vault, SQL Server, and App Services are all destroyed. The remote state backend (`rg-tf-state`) is not managed by this configuration and must be deleted separately if needed.
