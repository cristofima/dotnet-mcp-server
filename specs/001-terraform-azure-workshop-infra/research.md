# Research: Terraform Azure Workshop Infrastructure

**Feature**: `001-terraform-azure-workshop-infra`  
**Branch**: `feature/001-terraform-azure-workshop-infra`  
**Phase**: 0 — Produced by `/speckit.plan`

---

## R-001: Provider Version Resolution

**Question**: The spec assumed `azurerm ≥ 4.0` and `azuread ≥ 3.0`, but plan arguments specify `azurerm ~3.x` and `azuread ~2.x`. Which versions apply?

**Decision**: Use plan arguments — `azurerm ~> 3.0` and `azuread ~> 2.0`.

**Rationale**: Plan arguments are explicit operator intent provided at planning time and override earlier spec assumptions. The ~3.x series has been stable for workshop use since mid-2022. Using ~2.x for `azuread` avoids breaking attribute renames introduced in 3.0 (see R-002).

**Alternatives considered**:
- `azurerm ~> 4.0`: Requires stricter `features {}` block; the resources used (App Service, SQL, Key Vault, App Insights) are available in both 3.x and 4.x with the same API surface. No functional benefit for this feature set.
- `azuread ~> 3.0`: Renames `application_id` → `client_id` and `application_object_id` → `application_id` on `azuread_application_password`. No new capabilities needed for this feature.

**Required provider block**:
```hcl
required_providers {
  azurerm = {
    source  = "hashicorp/azurerm"
    version = "~> 3.0"
  }
  azuread = {
    source  = "hashicorp/azuread"
    version = "~> 2.0"
  }
}
```

---

## R-002: azuread ~2.x Attribute Naming

**Question**: What are the correct attribute names for `azuread_application`, `azuread_service_principal`, and `azuread_application_password` in provider 2.x?

**Decision**: Use the 2.x attribute names listed below. Do not use 3.x names.

| Resource | 2.x Attribute | 3.x Equivalent (do not use) |
|---|---|---|
| `azuread_application` output | `application_id` | `client_id` |
| `azuread_service_principal` input | `application_id` | `client_id` |
| `azuread_application_password` input | `application_object_id` | `application_id` |

**Rationale**: Using 3.x names with a 2.x provider will cause `terraform plan` to fail with "An argument named X is not expected here."

**Usage pattern** (2.x):
```hcl
resource "azuread_application" "example" {
  display_name     = "example"
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"
}

resource "azuread_service_principal" "example" {
  application_id = azuread_application.example.application_id
}

resource "azuread_application_password" "example" {
  application_object_id = azuread_application.example.object_id
  display_name          = "Terraform-managed secret"
}
```

---

## R-003: azurerm 3.x App Service Resource Names

**Question**: Which resource types should be used for App Service Plan and App Services in azurerm 3.x?

**Decision**: Use `azurerm_service_plan` (not `azurerm_app_service_plan`) and `azurerm_linux_web_app` (not `azurerm_app_service`).

**Rationale**: `azurerm_app_service_plan` and `azurerm_app_service` were deprecated in azurerm 3.0 and replaced by `azurerm_service_plan` and `azurerm_linux_web_app` / `azurerm_windows_web_app`. The deprecated resources still work in 3.x but emit deprecation warnings and will be removed in a future major version.

**Resource types used**:
| Azure Resource | Terraform Resource |
|---|---|
| App Service Plan (Linux) | `azurerm_service_plan` |
| App Service (Linux) | `azurerm_linux_web_app` |

---

## R-004: azurerm_linux_web_app .NET Version

**Question**: What value should `site_config.application_stack.dotnet_version` be set to for a .NET 10 application?

**Decision**: Use `"10.0"` as the default for `dotnet_version`, enabled by the azurerm ~> 4.0 provider upgrade.

**Rationale**: azurerm ~> 3.x only accepted values up to `"8.0"`. Upgrading to azurerm 4.x (v4.71.0) unlocks `"10.0"` support, aligning the infrastructure stack with the project's .NET 10 runtime target.

**Alternatives considered**:
- Stay on `"8.0"` with azurerm 3.x: Mismatches the application runtime. Rejected.
- Omit `application_stack` block: Not recommended — explicit version avoids platform version drift.

---

## R-005: Key Vault RBAC Mode

**Question**: Should the Key Vault use RBAC authorization or legacy access policies?

**Decision**: Use `enable_rbac_authorization = true` on `azurerm_key_vault`.

**Rationale**: RBAC authorization is the current recommended model. It allows granting the `Key Vault Secrets User` built-in role at the vault scope to App Service managed identities via `azurerm_role_assignment`. Legacy access policies require a separate `azurerm_key_vault_access_policy` resource per principal and don't integrate with Azure RBAC.

**RBAC assignment pattern**:
```hcl
resource "azurerm_role_assignment" "app_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.mcp_server.identity[0].principal_id
}
```

---

## R-006: azurerm Features Block for Key Vault Destroy Behavior

**Question**: How should `purge_protection_enabled = false` interact with `terraform destroy`? Does the provider need a `features {}` block?

**Decision**: Add a `key_vault` features sub-block to enable purge-on-destroy and soft-delete recovery. This makes `terraform destroy` fully clean up the vault.

```hcl
provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
  }
  subscription_id = var.subscription_id
}
```

**Rationale**: Without `purge_soft_delete_on_destroy = true`, `terraform destroy` soft-deletes the vault but does not purge it. The name remains unavailable for 7–90 days. Since `purge_protection_enabled = false` (FR-007), purging is allowed. Setting both options together makes the destroy → recreate cycle reliable for repeated workshop runs.

---

## R-007: Key Vault Reference Syntax for App Service App Settings

**Question**: What is the correct syntax for referencing Key Vault secrets in Azure App Service application settings?

**Decision**: Use the `versionless_id` output attribute from `azurerm_key_vault_secret` to construct Key Vault references without pinning to a specific secret version.

**Syntax**:
```
@Microsoft.KeyVault(SecretUri={versionless_id})
```

**Terraform usage**:
```hcl
app_settings = {
  "EntraId__ClientSecret" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.mcp_client_secret.versionless_id})"
}
```

**Rationale**: The `versionless_id` attribute (e.g., `https://kv-mcp-server.vault.azure.net/secrets/mcp-server-client-secret`) instructs App Service to always resolve the latest version of the secret, so rotating the secret in Key Vault does not require redeploying the app or changing Terraform configuration.

**Alternatives considered**:
- `VaultName=...;SecretName=...` format: Works but is less precise; ignores version identifiers and relies on implicit latest resolution.
- `version_id` with specific version: Pins to a specific version; requires Terraform change on each secret rotation.

---

## R-008: OAuth2 Scope UUID Generation

**Question**: How should the UUIDs for `oauth2_permission_scope.id` be generated in a way that is stable across plan/apply cycles?

**Decision**: Use Terraform's built-in `uuidv5()` function (available since v1.5.0; included in the required ~1.9 range) to generate deterministic UUIDs as local values.

**Formula**:
```hcl
locals {
  backend_api_user_impersonation_scope_id = uuidv5("url", "https://backend-api/scopes/user_impersonation")
  mcp_server_access_scope_id              = uuidv5("url", "https://mcp-server/scopes/mcp.access")
}
```

**Rationale**: `uuidv5()` produces the same UUID for the same inputs on every run — no state dependency, no extra provider (`hashicorp/random`), and no hardcoded constants to maintain. The URL namespace is appropriate because these are scoped OAuth2 identifiers.

**Alternatives considered**:
- `random_uuid` resource: Stable once created (persisted in state) but generates a new UUID if state is lost, causing app registration scope ID churn. Requires the `hashicorp/random` provider.
- Hardcoded UUID constants: Stable but requires one-time manual generation and documentation. `uuidv5()` is strictly better.

---

## R-009: SQL Connection String Format

**Question**: What connection string format should be used for the Backend API's `ConnectionStrings__Default` Key Vault secret?

**Decision**: Use the ADO.NET format compatible with Entity Framework Core's SQL Server provider.

**Format**:
```
Server=tcp:{fqdn},1433;Initial Catalog={db_name};Persist Security Info=False;User ID={username};Password={password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

**Terraform value**:
```hcl
value = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

**Rationale**: Matches the EF Core SQL Server provider default format. `Encrypt=True` is required by Azure SQL. `TrustServerCertificate=False` uses the proper certificate chain.

---

## R-010: Application Insights Resource Type

**Question**: Should `azurerm_application_insights` use classic or workspace-based mode?

**Decision**: Use workspace-based Application Insights by setting `workspace_id = azurerm_log_analytics_workspace.main.id`.

**Rationale**: Classic Application Insights (without `workspace_id`) is deprecated and will be retired by Azure. Workspace-based mode requires provisioning a Log Analytics Workspace first, which FR-006 already requires. The `azurerm_log_analytics_workspace` + `azurerm_application_insights` (with `workspace_id`) combination is the standard pattern in azurerm 3.x.

**Resource snippet**:
```hcl
resource "azurerm_log_analytics_workspace" "main" {
  name                = "log-mcp-server"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_application_insights" "main" {
  name                = "appi-mcp-server"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
}
```

---

## R-011: azurerm Backend Partial Configuration

**Question**: How should the `azurerm` backend be configured when storage account details are not known at template commit time?

**Decision**: Use a partial backend configuration with a backend config file or `-backend-config` flags at `terraform init` time.

**Committed `main.tf` backend block** (empty — no values hardcoded):
```hcl
terraform {
  backend "azurerm" {}
}
```

**Operator-supplied `backend.conf`** (not committed; listed in `.gitignore`):
```hcl
resource_group_name  = "rg-tf-state"
storage_account_name = "stworkshoptfstate"
container_name       = "tfstate"
key                  = "mcp-server.tfstate"
```

**Init command**:
```bash
terraform init -backend-config=backend.conf
```

**Rationale**: Hardcoding storage account name and resource group in `main.tf` forces everyone to use the same backend, which breaks parallel workshop setups. The partial configuration pattern lets each operator point to their own storage account.

**Alternatives considered**:
- Fully specified backend block: Forces a single shared storage account; fine for single-operator workshops but brittle.
- Environment variable `ARM_*` approach: Still requires a backend block; partial config file is more explicit and easier to document for workshop operators.

---

## R-012: Required azuread_service_principal Resources

**Question**: Do `azuread_service_principal` resources need to be created alongside each `azuread_application`?

**Decision**: Yes. Create one `azuread_service_principal` per `azuread_application`. Without it, the app registration exists only in "App registrations" — not as an Enterprise Application — and Entra ID cannot issue tokens for it.

**Rationale**: `azuread_application` creates the application object. `azuread_service_principal` creates the service principal (enterprise app) in the tenant, which is required for authentication flows and role assignment in "Enterprise Applications → Users and groups."

**Pattern** (azuread ~2.x):
```hcl
resource "azuread_service_principal" "backend_api" {
  application_id = azuread_application.backend_api.application_id
}
```

---

## R-013: Managed Identity Role Assignment Ordering

**Question**: Does Terraform need explicit `depends_on` to ensure managed identities are created before role assignments?

**Decision**: No explicit `depends_on` is needed. Reference `azurerm_linux_web_app.xxx.identity[0].principal_id` directly in the `azurerm_role_assignment` block. Terraform's implicit dependency graph handles ordering.

**Rationale**: Accessing `.identity[0].principal_id` from the web app resource creates an implicit dependency, so Terraform will always create the App Service (and its managed identity) before the role assignment. Explicit `depends_on` would be redundant.

**Pattern**:
```hcl
resource "azurerm_role_assignment" "mcp_server_kv" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.mcp_server.identity[0].principal_id
}
```
