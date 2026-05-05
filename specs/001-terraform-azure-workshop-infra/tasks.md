---
description: "Task list for Terraform Azure Workshop Infrastructure"
---

# Tasks: Terraform Azure Workshop Infrastructure

**Input**: Design documents from `specs/001-terraform-azure-workshop-infra/`  
**Prerequisites**: [plan.md](plan.md) ✅, [spec.md](spec.md) ✅, [research.md](research.md) ✅, [data-model.md](data-model.md) ✅, [contracts/terraform-interface.md](contracts/terraform-interface.md) ✅

**Organization**: Tasks are grouped by user story. US1 (infrastructure baseline) must complete before US2–US4. US2–US4 can be implemented in any order once US1 is done, with the constraint that US3 depends on US2 (Key Vault secrets reference `azuread_application_password` values).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with other [P] tasks in the same phase (different files, no unmet dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Create the `infra/terraform/` directory and commit-safe support files. No Terraform resources yet.

- [ ] T001 Create directory `infra/terraform/` at repository root
- [ ] T002 [P] Create `infra/terraform/.gitignore` — exclude `.terraform/`, `terraform.tfstate`, `terraform.tfstate.backup`, `*.tfvars` (but NOT `*.tfvars.example`), `backend.conf`, `*.tfplan`, and `override.tf`
- [ ] T003 [P] Create `infra/terraform/terraform.tfvars.example` — placeholder values for `tenant_id`, `subscription_id`, `location` (`"westeurope"`), `sql_admin_username`, `sql_admin_password`; include comments explaining each variable

**Checkpoint**: Directory exists; `.gitignore` and `terraform.tfvars.example` are committed-safe files ready for version control.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Provider configuration, shared variables, and the Azure resources all subsequent files depend on (`azurerm_resource_group`, `azurerm_log_analytics_workspace`, `azurerm_application_insights`). No user story can proceed until this phase is complete.

**⚠️ CRITICAL**: `sql.tf`, `app-service.tf`, `keyvault.tf`, and `entra-id.tf` all reference `azurerm_resource_group.main` — this phase must be complete first.

- [ ] T004 Create `infra/terraform/variables.tf` — define all 6 input variables: `tenant_id` (string, required), `subscription_id` (string, required), `location` (string, default `"westeurope"`), `sql_admin_username` (string, sensitive, required), `sql_admin_password` (string, sensitive, required), `dotnet_version` (string, default `"v8.0"`)

- [ ] T005 Create `infra/terraform/main.tf` — include: (1) `terraform {}` block with `required_version = "~> 1.9"` and `required_providers` for `azurerm ~> 3.0` and `azuread ~> 2.0`; (2) `provider "azurerm"` block with `subscription_id = var.subscription_id` and `features { key_vault { purge_soft_delete_on_destroy = true, recover_soft_deleted_key_vaults = true } }`; (3) `provider "azuread"` block with `tenant_id = var.tenant_id`; (4) `locals` block with `backend_api_user_impersonation_scope_id = uuidv5("url", "https://backend-api/scopes/user_impersonation")` and `mcp_server_access_scope_id = uuidv5("url", "https://mcp-server/scopes/mcp.access")`; (5) `azurerm_resource_group "main"` named `mcp-server-baseline`; (6) `azurerm_log_analytics_workspace "main"` named `log-mcp-server` (PerGB2018, 30-day retention); (7) `azurerm_application_insights "main"` named `appi-mcp-server` (application_type = "web", workspace_id references log analytics workspace)

**Checkpoint**: Foundation complete — `terraform validate` should pass on `main.tf` and `variables.tf` alone. All other files can now be created.

---

## Phase 3: User Story 1 — Provision Infrastructure from Scratch (Priority: P1) 🎯 MVP

**Goal**: A workshop operator can run `terraform apply` and get a working Azure environment: resource group, App Service Plan, two App Services, SQL Server and Database, and Application Insights — all provisioned with correct names and settings. App settings are populated with the non-secret App Insights instrumentation key; Key Vault references are added in US3.

**Independent Test**: Run `terraform init && terraform apply -var-file=terraform.tfvars` in a clean subscription. Verify all resources appear in the Azure Portal under `mcp-server-baseline`. Run `terraform apply` a second time and confirm the plan shows zero changes.

- [ ] T006 [P] [US1] Create `infra/terraform/sql.tf` — define `azurerm_mssql_server "main"` named `sql-backend-api` (version `"12.0"`, minimum_tls_version `"1.2"`, administrator_login/password from variables, resource_group_name and location from `azurerm_resource_group.main`); define `azurerm_mssql_database "main"` named `db-backend-api` (server_id references mssql_server, sku_name `"Basic"`, max_size_gb `2`); define `azurerm_mssql_firewall_rule "allow_azure_services"` named `AllowAzureServices` (start_ip_address `"0.0.0.0"`, end_ip_address `"0.0.0.0"`)

- [ ] T007 [P] [US1] Create `infra/terraform/app-service.tf` — define `azurerm_service_plan "main"` named `plan-mcp-server` (os_type `"Linux"`, sku_name `"B1"`); define `azurerm_linux_web_app "mcp_server"` named `app-mcp-server` with `identity { type = "SystemAssigned" }`, `site_config { application_stack { dotnet_version = var.dotnet_version } }`, and `app_settings = { "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.main.instrumentation_key }` (KV reference settings are added in T014); define `azurerm_linux_web_app "backend_api"` named `app-backend-api` with the same identity and stack config and `app_settings = { "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.main.instrumentation_key }`

- [ ] T008 [US1] Create `infra/terraform/outputs.tf` — define `mcp_server_url` as `"https://${azurerm_linux_web_app.mcp_server.default_hostname}"` and `backend_api_url` as `"https://${azurerm_linux_web_app.backend_api.default_hostname}"` (additional outputs for KV URI, connection string, and client IDs are added in T011 and T015)

- [ ] T009 [US1] Run `terraform fmt -check infra/terraform/` and `terraform init && terraform validate` from `infra/terraform/` to confirm all Phase 1–3 files are syntactically valid and all resource references resolve

**Checkpoint**: US1 is independently deployable. A `terraform apply` with only these files (main.tf, variables.tf, sql.tf, app-service.tf, outputs.tf) creates the full infrastructure baseline minus Key Vault and Entra ID. App Service URLs are reachable after application deployment.

---

## Phase 4: User Story 2 — Entra ID App Registrations (Priority: P2)

**Goal**: All three Entra ID app registrations exist with correct scopes, audiences, and cross-app `required_resource_access` declarations. The Backend API exposes `user_impersonation`; the MCP Server exposes `mcp.access` and requires `user_impersonation`; the Foundry agent requires `mcp.access`. All use `sign_in_audience = "AzureADandPersonalMicrosoftAccount"`. No `app_role` blocks on any registration.

**Independent Test**: Inspect the three app registrations in Entra ID Portal. Confirm scopes, `required_resource_access`, client IDs, and audience without deploying application code.

- [ ] T010 [P] [US2] Create `infra/terraform/entra-id.tf` — define `azuread_application "backend_api"` (display_name `app-backend-api`, sign_in_audience `AzureADandPersonalMicrosoftAccount`, `api` block with one `oauth2_permission_scope` for `user_impersonation` using `local.backend_api_user_impersonation_scope_id`, type `"User"`, admin_consent_required `false`); define `azuread_service_principal "backend_api"` (application_id = `azuread_application.backend_api.application_id`); define `azuread_application "mcp_server"` (display_name `app-mcp-server`, `api` block with `mcp.access` scope using `local.mcp_server_access_scope_id`, `required_resource_access` block referencing `azuread_application.backend_api.application_id` with `user_impersonation` scope ID); define `azuread_service_principal "mcp_server"`; define `azuread_application_password "mcp_server"` (application_object_id = `azuread_application.mcp_server.object_id`, display_name `"Terraform-managed secret"`); define `azuread_application "agent"` (display_name `app-foundry-agent`, `required_resource_access` for `mcp.access`, `web { redirect_uris = [] }`); define `azuread_service_principal "agent"`; define `azuread_application_password "agent"` (application_object_id = `azuread_application.agent.object_id`, display_name `"Terraform-managed secret"`)

- [ ] T011 [US2] Add client ID outputs to `infra/terraform/outputs.tf` — `backend_api_client_id` (`azuread_application.backend_api.application_id`), `mcp_server_client_id` (`azuread_application.mcp_server.application_id`), `foundry_agent_client_id` (`azuread_application.agent.application_id`)

**Checkpoint**: US2 is independently verifiable. After `terraform apply`, the three app registrations appear in Entra ID with correct scopes, audiences, and cross-app permissions. Client IDs are available in outputs.

---

## Phase 5: User Story 3 — Secrets in Key Vault (Priority: P3)

**Goal**: No plaintext secrets in App Service application settings. The Key Vault stores four secrets (MCP Server client secret, Foundry agent client secret, App Insights connection string, SQL connection string). Both App Services retrieve secrets via Key Vault references and have the Key Vault Secrets User RBAC role on their managed identities.

**Independent Test**: Inspect App Service application settings and confirm every secret-valued setting uses `@Microsoft.KeyVault(SecretUri=...)` syntax. Confirm both App Services have system-assigned identities with the Key Vault Secrets User role.

**⚠️ Dependency**: US3 depends on US2 — `keyvault.tf` references `azuread_application_password.mcp_server.value` and `azuread_application_password.agent.value` from `entra-id.tf`.

- [ ] T012 [US3] Create `infra/terraform/keyvault.tf` — define `azurerm_key_vault "main"` named `kv-mcp-server` (sku_name `"standard"`, tenant_id `var.tenant_id`, purge_protection_enabled `false`, enable_rbac_authorization `true`); define `azurerm_key_vault_secret "mcp_client_secret"` named `mcp-server-client-secret` (value = `azuread_application_password.mcp_server.value`); define `azurerm_key_vault_secret "foundry_client_secret"` named `foundry-agent-client-secret` (value = `azuread_application_password.agent.value`); define `azurerm_key_vault_secret "app_insights_conn_str"` named `app-insights-connection-string` (value = `azurerm_application_insights.main.connection_string`); define `azurerm_key_vault_secret "backend_api_sql_conn_str"` named `backend-api-sql-connection-string` (value = constructed ADO.NET connection string using `azurerm_mssql_server.main.fully_qualified_domain_name`, `azurerm_mssql_database.main.name`, `var.sql_admin_username`, `var.sql_admin_password`, with Encrypt=True and TrustServerCertificate=False)

- [ ] T013 [US3] Add Key Vault role assignments to `infra/terraform/app-service.tf` — define `azurerm_role_assignment "mcp_server_kv"` (scope = `azurerm_key_vault.main.id`, role_definition_name = `"Key Vault Secrets User"`, principal_id = `azurerm_linux_web_app.mcp_server.identity[0].principal_id`); define `azurerm_role_assignment "backend_api_kv"` (scope = `azurerm_key_vault.main.id`, role_definition_name = `"Key Vault Secrets User"`, principal_id = `azurerm_linux_web_app.backend_api.identity[0].principal_id`)

- [ ] T014 [US3] Update `app_settings` blocks in both `azurerm_linux_web_app` resources in `infra/terraform/app-service.tf` — MCP Server: add `"EntraId__ClientSecret" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.mcp_client_secret.versionless_id})"` and `"APPLICATIONINSIGHTS_CONNECTION_STRING" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})"`; Backend API: add `"ConnectionStrings__Default" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.backend_api_sql_conn_str.versionless_id})"` and `"APPLICATIONINSIGHTS_CONNECTION_STRING" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})"`; retain `APPINSIGHTS_INSTRUMENTATIONKEY` on both

- [ ] T015 [US3] Add `key_vault_uri` (`azurerm_key_vault.main.vault_uri`) and `app_insights_connection_string` (`azurerm_application_insights.main.connection_string`, sensitive = true) outputs to `infra/terraform/outputs.tf`

**Checkpoint**: US3 is independently verifiable. After `terraform apply`, inspect both App Services — all secret settings show `@Microsoft.KeyVault(...)` references. Both managed identities have the Key Vault Secrets User role.

---

## Phase 6: User Story 4 — Remote State in Azure Blob Storage (Priority: P4)

**Goal**: Terraform state is stored in Azure Blob Storage with native locking. No local `terraform.tfstate` file is produced. Multiple operators can run `terraform plan` from different machines and read the same state.

**Independent Test**: After `terraform init -backend-config=backend.conf`, confirm the `mcp-server.tfstate` blob exists in the designated container and no local `terraform.tfstate` file is created in `infra/terraform/`.

- [ ] T016 [US4] Add `backend "azurerm" {}` partial configuration block inside the `terraform {}` block in `infra/terraform/main.tf` (empty body — all values supplied via `-backend-config=backend.conf` at init time per R-011); verify the backend block does not conflict with `required_providers` or `required_version`

**Checkpoint**: US4 is verifiable by running `terraform init -backend-config=backend.conf` with a `backend.conf` pointing to the pre-created storage account. The state blob appears in Azure Storage; no local state file is written.

---

## Phase 7: Polish and Cross-Cutting

**Purpose**: Final formatting, full validation of the complete module, and removing any residual template content.

- [ ] T017 Run `terraform fmt` (auto-fix mode) across `infra/terraform/` to normalize all HCL formatting: `terraform fmt infra/terraform/`

- [ ] T018 [P] Run `terraform validate` from `infra/terraform/` against the complete file set (all 7 `.tf` files) to confirm zero errors: `cd infra/terraform && terraform init -backend=false && terraform validate`

- [ ] T019 [P] Review `infra/terraform/outputs.tf` for completeness — confirm all 7 outputs from data-model.md are present (`mcp_server_url`, `backend_api_url`, `app_insights_connection_string`, `backend_api_client_id`, `mcp_server_client_id`, `foundry_agent_client_id`, `key_vault_uri`) and that no client secret values appear

---

## Dependencies

```
Phase 1 (Setup)
  └── Phase 2 (Foundational: main.tf + variables.tf)
        ├── Phase 3 / US1 (sql.tf, app-service.tf, outputs.tf)  ← MVP deliverable
        │     └── Phase 4 / US2 (entra-id.tf)
        │           └── Phase 5 / US3 (keyvault.tf, app-service.tf update, outputs.tf update)
        │                 └── Phase 6 / US4 (main.tf backend block)
        │                       └── Phase 7 (fmt, validate, review)
        └── Phase 7 can also validate Phase 3 files independently
```

**US2 → US3 hard dependency**: `keyvault.tf` (`T012`) references `azuread_application_password.mcp_server.value` and `.agent.value` from `entra-id.tf` (`T010`). US3 cannot begin until US2's `T010` is complete.

**US3 → App Service settings dependency**: `T014` (KV references in `app-service.tf`) references `azurerm_key_vault_secret.*` resources from `T012`. T014 must follow T012.

---

## Parallel Execution Examples

### Phase 1 (after T001)
T002 (`.gitignore`) and T003 (`terraform.tfvars.example`) can be written simultaneously — different files, no dependencies.

### Phase 3 (after T005)
T006 (`sql.tf`) and T007 (`app-service.tf`) can be written simultaneously — different files, both only reference `azurerm_resource_group.main` from `main.tf`.

### Phase 7 (after T016)
T017 (fmt), T018 (validate), and T019 (outputs review) can be executed simultaneously after all `.tf` files are finalized.

---

## Implementation Strategy

**MVP scope**: Complete Phases 1–3 (T001–T009). This delivers a fully provisionable infrastructure baseline (US1) that can be tested independently without Entra ID or Key Vault.

**Increment 1**: Add Phase 4 (T010–T011) for Entra ID app registrations (US2). Verifiable independently in the Azure Portal.

**Increment 2**: Add Phase 5 (T012–T015) for Key Vault and secret management (US3). Depends on Increment 1.

**Increment 3**: Add Phase 6 (T016) for remote state backend (US4). Can be done at any point after Phase 2 since the backend block is independent of resource content.

**Final**: Phase 7 (T017–T019) polish — run before submitting the PR.
