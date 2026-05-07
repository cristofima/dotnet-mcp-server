# Feature Specification: Terraform Azure Workshop Infrastructure

**Feature Branch**: `feature/001-terraform-azure-workshop-infra`
**Created**: May 4, 2026
**Status**: Draft
**Input**: User description: "Create Terraform configuration to provision the Azure infrastructure for this MCP server workshop using two providers: azurerm (infrastructure) and azuread (Entra ID / identity)."

## Clarifications

### Session 2026-05-04

- Q: How should Azure Resource Groups be structured? → A: Single resource group for all resources, named `mcp-server-baseline`.
- Q: What App Service application setting key names should be used for Key Vault references? → A: Use the keys the apps already declare in `appsettings.json`: `EntraId__ClientSecret` and `APPLICATIONINSIGHTS_CONNECTION_STRING` on the MCP Server; `ConnectionStrings__Default` and `APPLICATIONINSIGHTS_CONNECTION_STRING` on the Backend API. The Foundry OAuth client secret is stored in Key Vault for operator retrieval but is not consumed via an App Service reference.
- Q: Where should Terraform files be placed and how should they be structured? → A: `infra/terraform/` at repo root, flat directory (no child modules), split into one file per concern.
- Q: How should the Azure SQL firewall be configured to allow App Service access? → A: Enable the "Allow Azure services" toggle (`0.0.0.0/0.0.0.0` firewall rule) — simple, no outbound IP tracking, workshop-appropriate.
- Q: How should the Key Vault soft-delete name collision be handled for repeated workshop runs? → A: Set `purge_protection_enabled = false` so `terraform destroy` fully removes the vault and the name can be reused immediately on the next apply.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Provision Infrastructure from Scratch (Priority: P1)

A workshop operator with Contributor access on an Azure subscription runs `terraform apply` on a clean environment and has all required infrastructure running within minutes, ready to receive application deployments.

**Why this priority**: Without a working apply, no other scenario is possible. This is the foundational deliverable.

**Independent Test**: Can be fully tested by running `terraform init && terraform apply -var-file=terraform.tfvars` in a clean Azure subscription and verifying all resources appear in the Azure Portal.

**Acceptance Scenarios**:

1. **Given** a valid `terraform.tfvars` with tenant ID, subscription ID, SQL admin credentials, and target region, **When** `terraform apply` is executed, **Then** all infrastructure resources are created with no errors and both App Service URLs are reachable over HTTPS.
2. **Given** an already-provisioned environment, **When** `terraform apply` is run a second time, **Then** the plan shows zero changes (fully idempotent).
3. **Given** a provisioned environment, **When** `terraform destroy` is executed, **Then** all managed resources are removed cleanly with no orphaned objects.

---

### User Story 2 - Entra ID App Registrations Are Correctly Configured (Priority: P2)

A developer setting up MCP Server authentication verifies that all three Entra ID app registrations exist with the correct scopes, audience, and cross-app permissions — with no manual portal steps required beyond adding the Foundry redirect URI.

**Why this priority**: Incorrect app registrations break authentication for both the MCP Server and the AI Foundry integration.

**Independent Test**: Can be tested independently by inspecting the three app registrations in Entra ID and confirming scopes, `required_resource_access`, and `sign_in_audience` without deploying application code.

**Acceptance Scenarios**:

1. **Given** applied Terraform, **When** the Backend API app registration is inspected, **Then** it exposes a `user_impersonation` delegated scope with `admin_consent_required = false`, has no client secret, and carries no app roles.
2. **Given** applied Terraform, **When** the MCP Server app registration is inspected, **Then** it exposes `mcp.access`, declares `required_resource_access` for the Backend API `user_impersonation` scope, and its client secret is stored in Key Vault (not in the app registration output).
3. **Given** applied Terraform, **When** the Foundry OAuth Passthrough app registration is inspected, **Then** it declares `required_resource_access` for the MCP Server `mcp.access` scope and its client secret is stored in Key Vault.
4. **Given** any of the three registrations, **When** a personal Microsoft account (hotmail, outlook.com, live.com) attempts to acquire a token, **Then** the request is accepted because all registrations use the `AzureADandPersonalMicrosoftAccount` audience.

---

### User Story 3 - Secrets Are Stored in Key Vault, Not in App Settings (Priority: P3)

A security reviewer confirms that no plaintext client secrets or connection strings appear in the App Service application settings. Both App Services resolve secrets at runtime using Key Vault references.

**Why this priority**: Plaintext secrets in App Service configuration are a security violation and would fail any compliance review.

**Independent Test**: Can be tested independently by inspecting App Service application settings and verifying that all secret-valued settings follow the `@Microsoft.KeyVault(...)` reference syntax.

**Acceptance Scenarios**:

1. **Given** applied Terraform, **When** the MCP Server App Service application settings are inspected, **Then** the client secret setting contains a Key Vault reference, not a plaintext value.
2. **Given** applied Terraform, **When** both App Service application settings are inspected, **Then** the Application Insights connection string setting also contains a Key Vault reference.
3. **Given** applied Terraform, **When** Key Vault access is checked, **Then** both App Services have a system-assigned managed identity with the Key Vault Secrets User RBAC role.

---

### User Story 4 - Remote State Is Backed by Azure Blob Storage (Priority: P4)

Workshop operators can collaborate on the same Terraform state without conflicts, because state is stored remotely in Azure Blob Storage with native state locking.

**Why this priority**: Without remote state, concurrent applies from multiple operators would corrupt the state file.

**Independent Test**: Can be tested by confirming the `terraform.tfstate` blob appears in the designated container after apply, and that no local state file is created.

**Acceptance Scenarios**:

1. **Given** a manually pre-created storage account and container, **When** `terraform init` is run with the backend configuration, **Then** state is initialized in Azure Blob Storage and no local `terraform.tfstate` file is produced.
2. **Given** remote state is configured, **When** a second operator runs `terraform plan`, **Then** they read the same current state without manual file sharing.

---

### Edge Cases

- What happens when the Key Vault name conflicts with a soft-deleted vault from a previous workshop run? Resolved: `purge_protection_enabled = false` allows `terraform destroy` to fully purge the vault, so the name `kv-mcp-server` is available immediately on the next apply.
- What happens if the Entra ID tenant has disabled personal MSA account sign-in at the tenant level, overriding the app registration audience setting?
- What happens when the SQL admin password provided in tfvars does not meet Azure SQL complexity requirements?
- What happens if the storage container for remote state has not been created before running `terraform init`? (The configuration does not create it; operators must create it as a prerequisite step.)
- What happens if a managed identity does not yet have its Key Vault role propagated when the App Service first starts?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The configuration MUST provision a single Azure Resource Group named `mcp-server-baseline` that contains all provisioned resources.
- **FR-002**: The configuration MUST provision a shared Linux App Service Plan (B1 tier) used by both App Services.
- **FR-003**: The configuration MUST provision one App Service for the MCP Server (`McpServer.Presentation`) and one for the Backend API (`McpServer.BackendApi`), both running on the shared App Service Plan.
- **FR-004**: The configuration MUST provision an Azure SQL Server and an Azure SQL Database accessible by the Backend API App Service; SQL admin credentials are provided as sensitive Terraform variables.
- **FR-005**: The Azure SQL Server firewall MUST enable the "Allow Azure services" rule (`start_ip_address = "0.0.0.0"`, `end_ip_address = "0.0.0.0"`) so that both App Services can reach the database without explicit outbound IP management.
- **FR-006**: The configuration MUST provision a shared Application Insights resource backed by a Log Analytics Workspace.
- **FR-007**: The configuration MUST provision an Azure Key Vault that stores the MCP Server client secret, the Foundry OAuth Passthrough client secret, and the Application Insights connection string. The Key Vault MUST have `purge_protection_enabled = false` (soft-delete remains enabled per Azure default) to allow immediate name reuse after `terraform destroy`.
- **FR-008**: Both App Services MUST use system-assigned managed identities and MUST retrieve secrets from Key Vault using Key Vault references in application settings — no plaintext secrets in app configuration. The Key Vault reference mappings are: MCP Server — `EntraId__ClientSecret` and `APPLICATIONINSIGHTS_CONNECTION_STRING`; Backend API — `ConnectionStrings__Default` and `APPLICATIONINSIGHTS_CONNECTION_STRING`. The Foundry OAuth client secret is stored in Key Vault for operator retrieval only and is not surfaced as an App Service setting.
- **FR-009**: The Key Vault MUST grant the Key Vault Secrets User RBAC role to the managed identity of each App Service.
- **FR-010**: The configuration MUST create three Entra ID app registrations, all with `sign_in_audience = AzureADandPersonalMicrosoftAccount`.
- **FR-011**: The Backend API app registration MUST expose one delegated OAuth2 scope named `user_impersonation` with `admin_consent_required = false`. It MUST NOT have a client secret and MUST NOT have any app roles.
- **FR-012**: The MCP Server app registration MUST expose one delegated OAuth2 scope named `mcp.access`. It MUST declare `required_resource_access` for the Backend API `user_impersonation` scope. It MUST have one client secret; the secret value MUST be stored in Key Vault.
- **FR-013**: The Foundry OAuth Passthrough app registration MUST declare `required_resource_access` for the MCP Server `mcp.access` scope. It MUST have one client secret; the secret value MUST be stored in Key Vault. It MUST NOT expose custom scopes or app roles. Its Foundry redirect URI MUST be added manually after the Foundry project is created (not managed by Terraform).
- **FR-014**: No `app_role` blocks MUST appear on any of the three app registrations.
- **FR-015**: Remote Terraform state MUST be stored in Azure Blob Storage via a backend configuration block. The storage container is created manually before first apply; the Terraform configuration MUST NOT provision it.
- **FR-016**: Resource names MUST follow the `{resource_type}-{project}` convention (e.g., `app-mcp-server`, `app-backend-api`, `sql-backend-api`, `kv-mcp-server`, `appi-mcp-server`, `log-mcp-server`, `plan-mcp-server`).
- **FR-017**: The configuration MUST accept `tenant_id` and `subscription_id` as required input variables, plus `location`, `sql_admin_username`, and `sql_admin_password`.
- **FR-018**: The configuration MUST output: both App Service URLs, the Application Insights connection string, and the client IDs of all three app registrations. Client secret values MUST NOT appear in outputs.
- **FR-019**: All Terraform files MUST reside in `infra/terraform/` at the repository root as a flat directory (no child modules). Files MUST be split by concern: `main.tf` (provider config, backend, resource group), `variables.tf`, `outputs.tf`, `app-service.tf`, `sql.tf`, `keyvault.tf`, `entra-id.tf`. `terraform.tfvars.example` and `.gitignore` also live in this directory.
- **FR-020**: A `terraform.tfvars.example` file MUST be committed to version control. The actual `terraform.tfvars` and the `.terraform/` directory MUST be excluded by `.gitignore`.

### Key Entities

- **Resource Group (`mcp-server-baseline`)**: Single container for all provisioned Azure resources; simplifies RBAC assignment and lifecycle management.
- **App Service Plan**: Shared compute resource for both App Services; defines OS (Linux) and pricing tier (B1).
- **App Service (MCP Server)**: Hosts `McpServer.Presentation`; reads secrets from Key Vault via references; has a system-assigned managed identity.
- **App Service (Backend API)**: Hosts `McpServer.BackendApi`; reads secrets from Key Vault; has a system-assigned managed identity.
- **Azure SQL Server + Database**: Relational data store for the Backend API (EF Core); admin credentials are sensitive Terraform variables.
- **Application Insights**: Shared observability resource for both App Services; connection string stored in Key Vault.
- **Log Analytics Workspace**: Required backing store for Application Insights; created in the same resource group.
- **Azure Key Vault**: Secret store; holds client secrets and the Application Insights connection string; access is granted to App Service managed identities via RBAC.
- **Backend API App Registration**: Entra ID resource server; exposes `user_impersonation` delegated scope; no client secret.
- **MCP Server App Registration**: Confidential client in Entra ID; exposes `mcp.access`; requests Backend API `user_impersonation` scope for OBO; has one client secret.
- **Foundry OAuth Passthrough App Registration**: Confidential client for AI Foundry integration; requests MCP Server `mcp.access` scope; has one client secret; redirect URI added manually post-setup.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `terraform apply` completes successfully from a clean state with zero errors on first run.
- **SC-002**: A second `terraform apply` on an already-provisioned environment produces a plan with zero changes (full idempotency).
- **SC-003**: All three Entra ID app registrations are correctly configured (correct scopes, audience, cross-app permissions) with zero manual portal steps required before the first application deployment (the Foundry redirect URI addition is an explicitly documented post-provisioning step, not a gap).
- **SC-004**: Both App Service application settings contain zero plaintext secret values; all secrets resolve via Key Vault references.
- **SC-005**: `terraform validate` and `terraform fmt -check` both pass with no errors or formatting warnings.
- **SC-006**: Terraform state is persisted to Azure Blob Storage after apply; no local `terraform.tfstate` file is produced.

## Assumptions

- All resources are deployed into a single resource group named `mcp-server-baseline`; this simplifies RBAC scope and resource lifecycle for a workshop environment.
- The App Service Plan tier is B1 (Basic); this is appropriate for workshop and development use — production-grade scaling is out of scope.
- App Services connect to Azure SQL via the public endpoint using the "Allow Azure services" toggle (`0.0.0.0` firewall rule); explicit outbound IP rules, private endpoints, and VNet integration are out of scope.
- Key Vault access uses Azure RBAC (Key Vault Secrets User role assigned to managed identities), not the legacy access policy model.
- The storage account and container for Terraform remote state are created manually before the first `terraform init`; the Terraform configuration does not provision or depend on them as resources.
- The SQL admin username and password are provided as sensitive Terraform variables and are never stored in version control.
- The Foundry OAuth Passthrough app registration redirect URI (e.g., `https://<project>.services.ai.azure.com/...`) cannot be known at apply time and is explicitly documented as a manual post-provisioning step.
- All resources are deployed to a single Azure region, specified as a Terraform variable (default: `centralus`).
- The configuration targets a single environment (production); multi-environment support (dev/staging/prod) is out of scope.
- Provider versions: `azurerm` ≥ 4.0 and `azuread` ≥ 3.0 are required for compatibility with the APIs used.
- The `azurerm` provider uses the `azurerm` features block with `key_vault { purge_soft_deleted_on_destroy = true, recover_soft_deleted_key_vaults = true }` to ensure Key Vault is fully purged on destroy and recoverable on re-apply within the same session.
