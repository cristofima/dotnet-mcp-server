# Data Model: Terraform Azure Workshop Infrastructure

**Feature**: `001-terraform-azure-workshop-infra`  
**Branch**: `feature/001-terraform-azure-workshop-infra`  
**Phase**: 1 — Produced by `/speckit.plan`

---

## Provider Versions

| Provider | Source | Version Constraint |
|---|---|---|
| `azurerm` | `hashicorp/azurerm` | `~> 3.0` |
| `azuread` | `hashicorp/azuread` | `~> 2.0` |

**Terraform version**: `~> 1.9`

---

## Input Variables

Defined in `variables.tf`.

| Name | Type | Sensitive | Default | Required | Description |
|---|---|---|---|---|---|
| `tenant_id` | `string` | No | — | Yes | Entra ID tenant ID (GUID) |
| `subscription_id` | `string` | No | — | Yes | Azure subscription ID (GUID) |
| `location` | `string` | No | `"westeurope"` | No | Azure region for all resources |
| `sql_admin_username` | `string` | Yes | — | Yes | SQL Server administrator login |
| `sql_admin_password` | `string` | Yes | — | Yes | SQL Server administrator password |
| `dotnet_version` | `string` | No | `"v8.0"` | No | .NET stack version for App Services (e.g., `"v8.0"`, `"v10.0"`) |

---

## Local Values

Defined in `main.tf` (or a `locals.tf` if preferred). All names follow the `{resource_type}-{project}` convention from FR-016.

| Local Name | Value | Purpose |
|---|---|---|
| `backend_api_user_impersonation_scope_id` | `uuidv5("url", "https://backend-api/scopes/user_impersonation")` | Stable UUID for `user_impersonation` scope on Backend API app |
| `mcp_server_access_scope_id` | `uuidv5("url", "https://mcp-server/scopes/mcp.access")` | Stable UUID for `mcp.access` scope on MCP Server app |

---

## Azure Resources

### Infrastructure (`main.tf`)

| Terraform Resource | Terraform Name | Azure Name | Notes |
|---|---|---|---|
| `azurerm_resource_group` | `main` | `mcp-server-baseline` | Single RG for all resources (FR-001) |

### App Services (`app-service.tf`)

| Terraform Resource | Terraform Name | Azure Name | Notes |
|---|---|---|---|
| `azurerm_service_plan` | `main` | `plan-mcp-server` | Linux, B1 tier (FR-002) |
| `azurerm_linux_web_app` | `mcp_server` | `app-mcp-server` | System-assigned identity; KV references (FR-003, FR-008) |
| `azurerm_linux_web_app` | `backend_api` | `app-backend-api` | System-assigned identity; KV references (FR-003, FR-008) |

#### `azurerm_service_plan` Key Attributes

| Attribute | Value |
|---|---|
| `os_type` | `"Linux"` |
| `sku_name` | `"B1"` |

#### `azurerm_linux_web_app` — MCP Server App Settings

| App Setting Key | Value |
|---|---|
| `EntraId__ClientSecret` | `@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.mcp_client_secret.versionless_id})` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})` |
| `APPINSIGHTS_INSTRUMENTATIONKEY` | `azurerm_application_insights.main.instrumentation_key` (direct value; not secret) |

#### `azurerm_linux_web_app` — Backend API App Settings

| App Setting Key | Value |
|---|---|
| `ConnectionStrings__Default` | `@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.backend_api_sql_conn_str.versionless_id})` |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | `@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})` |
| `APPINSIGHTS_INSTRUMENTATIONKEY` | `azurerm_application_insights.main.instrumentation_key` (direct value; not secret) |

**Note**: `APPINSIGHTS_INSTRUMENTATIONKEY` is included as a non-secret direct reference for legacy SDK compatibility. The primary telemetry key is `APPLICATIONINSIGHTS_CONNECTION_STRING` via KV reference.

### SQL (`sql.tf`)

| Terraform Resource | Terraform Name | Azure Name | Notes |
|---|---|---|---|
| `azurerm_mssql_server` | `main` | `sql-backend-api` | FR-004; admin credentials from variables |
| `azurerm_mssql_database` | `main` | `db-backend-api` | FR-004 |
| `azurerm_mssql_firewall_rule` | `allow_azure_services` | `AllowAzureServices` | FR-005; `0.0.0.0/0.0.0.0` |

#### `azurerm_mssql_server` Key Attributes

| Attribute | Value |
|---|---|
| `administrator_login` | `var.sql_admin_username` |
| `administrator_login_password` | `var.sql_admin_password` |
| `version` | `"12.0"` |
| `minimum_tls_version` | `"1.2"` |

#### `azurerm_mssql_database` Key Attributes

| Attribute | Value |
|---|---|
| `server_id` | `azurerm_mssql_server.main.id` |
| `sku_name` | `"Basic"` |
| `max_size_gb` | `2` |

### Key Vault (`keyvault.tf`)

| Terraform Resource | Terraform Name | Azure Name | Notes |
|---|---|---|---|
| `azurerm_key_vault` | `main` | `kv-mcp-server` | RBAC model; `purge_protection_enabled = false` (FR-007) |
| `azurerm_key_vault_secret` | `mcp_client_secret` | `mcp-server-client-secret` | MCP Server Entra ID client secret |
| `azurerm_key_vault_secret` | `foundry_client_secret` | `foundry-agent-client-secret` | Foundry OAuth passthrough client secret |
| `azurerm_key_vault_secret` | `app_insights_conn_str` | `app-insights-connection-string` | App Insights connection string |
| `azurerm_key_vault_secret` | `backend_api_sql_conn_str` | `backend-api-sql-connection-string` | SQL connection string for Backend API |
| `azurerm_role_assignment` | `mcp_server_kv` | — | Key Vault Secrets User → MCP Server managed identity |
| `azurerm_role_assignment` | `backend_api_kv` | — | Key Vault Secrets User → Backend API managed identity |

#### `azurerm_key_vault` Key Attributes

| Attribute | Value |
|---|---|
| `sku_name` | `"standard"` |
| `tenant_id` | `var.tenant_id` |
| `purge_protection_enabled` | `false` |
| `enable_rbac_authorization` | `true` |

#### Secret Values

| Secret Resource Name | Secret Azure Name | Value Source |
|---|---|---|
| `mcp_client_secret` | `mcp-server-client-secret` | `azuread_application_password.mcp_server.value` |
| `foundry_client_secret` | `foundry-agent-client-secret` | `azuread_application_password.agent.value` |
| `app_insights_conn_str` | `app-insights-connection-string` | `azurerm_application_insights.main.connection_string` |
| `backend_api_sql_conn_str` | `backend-api-sql-connection-string` | Constructed string (see R-009) |

#### SQL Connection String Template

```
Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

### Observability (`app-service.tf` or `main.tf`)

| Terraform Resource | Terraform Name | Azure Name | Notes |
|---|---|---|---|
| `azurerm_log_analytics_workspace` | `main` | `log-mcp-server` | FR-006; `PerGB2018` SKU |
| `azurerm_application_insights` | `main` | `appi-mcp-server` | FR-006; workspace-based (R-010) |

### Entra ID (`entra-id.tf`)

| Terraform Resource | Terraform Name | Display Name | Notes |
|---|---|---|---|
| `azuread_application` | `backend_api` | `app-backend-api` | Exposes `user_impersonation` scope (FR-011) |
| `azuread_service_principal` | `backend_api` | — | Enterprise app for `backend_api` (R-012) |
| `azuread_application` | `mcp_server` | `app-mcp-server` | Exposes `mcp.access`; requires `user_impersonation` (FR-012) |
| `azuread_service_principal` | `mcp_server` | — | Enterprise app for `mcp_server` (R-012) |
| `azuread_application_password` | `mcp_server` | `Terraform-managed secret` | Client secret for MCP Server |
| `azuread_application` | `agent` | `app-foundry-agent` | Requires `mcp.access`; redirect URI added manually (FR-013) |
| `azuread_service_principal` | `agent` | — | Enterprise app for `agent` (R-012) |
| `azuread_application_password` | `agent` | `Terraform-managed secret` | Client secret for Foundry agent |

#### App Registration Scope Definitions

**Backend API** (`azuread_application.backend_api`):

```hcl
api {
  oauth2_permission_scope {
    admin_consent_description  = "Allow the application to access the Backend API on behalf of the signed-in user."
    admin_consent_display_name = "Access Backend API"
    enabled                    = true
    id                         = local.backend_api_user_impersonation_scope_id
    type                       = "User"
    user_consent_description   = "Allow the application to access the Backend API on your behalf."
    user_consent_display_name  = "Access Backend API"
    value                      = "user_impersonation"
  }
}
```

**MCP Server** (`azuread_application.mcp_server`):

```hcl
api {
  oauth2_permission_scope {
    admin_consent_description  = "Allow the application to access the MCP Server on behalf of the signed-in user."
    admin_consent_display_name = "Access MCP Server"
    enabled                    = true
    id                         = local.mcp_server_access_scope_id
    type                       = "User"
    user_consent_description   = "Allow the application to access the MCP Server on your behalf."
    user_consent_display_name  = "Access MCP Server"
    value                      = "mcp.access"
  }
}

required_resource_access {
  resource_app_id = azuread_application.backend_api.application_id

  resource_access {
    id   = local.backend_api_user_impersonation_scope_id
    type = "Scope"
  }
}
```

**Foundry Agent** (`azuread_application.agent`):

```hcl
required_resource_access {
  resource_app_id = azuread_application.mcp_server.application_id

  resource_access {
    id   = local.mcp_server_access_scope_id
    type = "Scope"
  }
}

web {
  redirect_uris = []
}
```

---

## Outputs

Defined in `outputs.tf`.

| Output Name | Source | Sensitive | Description |
|---|---|---|---|
| `mcp_server_url` | `https://${azurerm_linux_web_app.mcp_server.default_hostname}` | No | MCP Server HTTPS URL |
| `backend_api_url` | `https://${azurerm_linux_web_app.backend_api.default_hostname}` | No | Backend API HTTPS URL |
| `app_insights_connection_string` | `azurerm_application_insights.main.connection_string` | Yes | Application Insights connection string (marked sensitive) |
| `backend_api_client_id` | `azuread_application.backend_api.application_id` | No | Backend API app client ID |
| `mcp_server_client_id` | `azuread_application.mcp_server.application_id` | No | MCP Server app client ID |
| `foundry_agent_client_id` | `azuread_application.agent.application_id` | No | Foundry agent app client ID |
| `key_vault_uri` | `azurerm_key_vault.main.vault_uri` | No | Key Vault URI (for manual secret retrieval) |

**Note**: Client secret values (`azuread_application_password.*.value`) MUST NOT appear in outputs (FR-018). Operators retrieve them from Key Vault directly.

---

## Dependency Graph (Key Ordering)

The following dependencies drive resource creation order. Terraform resolves these automatically via implicit references — no `depends_on` blocks are needed.

```
azurerm_resource_group.main
  └── azurerm_service_plan.main
  │     ├── azurerm_linux_web_app.mcp_server
  │     └── azurerm_linux_web_app.backend_api
  ├── azurerm_log_analytics_workspace.main
  │     └── azurerm_application_insights.main
  ├── azurerm_mssql_server.main
  │     ├── azurerm_mssql_database.main
  │     └── azurerm_mssql_firewall_rule.allow_azure_services
  └── azurerm_key_vault.main
        ├── azuread_application_password.mcp_server  ─── azuread_application.mcp_server
        │     └── azurerm_key_vault_secret.mcp_client_secret
        ├── azuread_application_password.agent       ─── azuread_application.agent
        │     └── azurerm_key_vault_secret.foundry_client_secret
        ├── azurerm_application_insights.main
        │     └── azurerm_key_vault_secret.app_insights_conn_str
        ├── (sql_conn_str constructed from mssql_server + mssql_database + vars)
        │     └── azurerm_key_vault_secret.backend_api_sql_conn_str
        ├── azurerm_linux_web_app.mcp_server
        │     └── azurerm_role_assignment.mcp_server_kv
        └── azurerm_linux_web_app.backend_api
              └── azurerm_role_assignment.backend_api_kv
```

---

## File Layout

All files in `infra/terraform/` (flat, no subdirectories):

| File | Contents |
|---|---|
| `main.tf` | `terraform {}` block (required_providers, backend), `provider "azurerm"` (with features), `provider "azuread"`, `locals`, `azurerm_resource_group.main`, `azurerm_log_analytics_workspace.main`, `azurerm_application_insights.main` |
| `variables.tf` | All 6 input variable definitions |
| `outputs.tf` | All 7 output definitions |
| `app-service.tf` | `azurerm_service_plan.main`, both `azurerm_linux_web_app` resources, both `azurerm_role_assignment` resources for Key Vault |
| `sql.tf` | `azurerm_mssql_server.main`, `azurerm_mssql_database.main`, `azurerm_mssql_firewall_rule.allow_azure_services` |
| `keyvault.tf` | `azurerm_key_vault.main`, 4 `azurerm_key_vault_secret` resources |
| `entra-id.tf` | 3 `azuread_application` resources, 3 `azuread_service_principal` resources, 2 `azuread_application_password` resources |
| `terraform.tfvars.example` | Placeholder values for all required variables |
| `.gitignore` | Standard Terraform ignore rules |
