# Implementation Plan: Terraform Azure Workshop Infrastructure

**Branch**: `feature/001-terraform-azure-workshop-infra` | **Date**: 2026-05-04 | **Spec**: [spec.md](spec.md)  
**Input**: Feature specification from `specs/001-terraform-azure-workshop-infra/spec.md`

## Summary

Provision the complete Azure infrastructure for the MCP Server OAuth2 Security Baseline workshop using a flat Terraform root module. The configuration creates two Linux App Services (MCP Server and Backend API) on a shared B1 plan, an Azure SQL Server/Database, a Key Vault (RBAC model, purge protection disabled), workspace-based Application Insights, and three Entra ID app registrations representing the full OAuth2/OBO scope chain. All secrets are stored in Key Vault and consumed by App Services via Key Vault references — no plaintext secrets in App Service configuration. Remote state is backed by Azure Blob Storage. Providers: `azurerm ~> 3.0` and `azuread ~> 2.0`.

## Technical Context

**Language/Version**: HCL2 / Terraform ~1.9  
**Primary Dependencies**: `azurerm ~> 3.0` (Azure infrastructure), `azuread ~> 2.0` (Entra ID / identity)  
**Storage**: Azure Blob Storage (remote state only; pre-created manually before `terraform init`)  
**Testing**: `terraform validate`, `terraform fmt -check`, manual `terraform plan` review before apply  
**Target Platform**: Azure cloud — single region (`centralus` default), single resource group (`mcp-server-baseline`)  
**Project Type**: Infrastructure as Code (IaC) — Terraform root module, flat file layout, no child modules  
**Performance Goals**: N/A — infrastructure provisioning is one-time; no runtime throughput requirements  
**Constraints**: B1 App Service Plan tier; no child modules; `purge_protection_enabled = false` on Key Vault; azuread ~2.x attribute naming (see [research.md](research.md))  
**Scale/Scope**: Single workshop/development environment; one resource group; ~30 Terraform-managed resources

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Justification for all N/A**: This feature is pure Terraform IaC. It does not add, modify, or delete any .NET source files, MCP tools, use cases, or test projects. All five constitution principles govern C# application code and have no applicable surface in HCL configuration files.

| Principle | Gate Question | Status |
|-----------|--------------|--------|
| I. Security-First (OAuth2 + OBO) | Does every new tool/prompt carry `[Authorize]` at class AND method level? Does the feature use OBO for all downstream calls — no token passthrough? | N/A — no .NET tools or prompts added |
| II. Clean Architecture | Do new types respect Domain → Application → Infrastructure → Presentation? Does no use case reference Infrastructure or Presentation? | N/A — no .NET types added |
| III. Single Responsibility in Tools | Does each tool method delegate to exactly one use case? Are `McpToolResult.Ok/Fail` calls absent from tool classes? | N/A — no MCP tool classes added |
| IV. Observability by Default | Is all telemetry centralized in `McpTelemetryFilter`? Are `Stopwatch`, `McpActivitySource`, and `McpMetrics.*` absent from tool methods? | N/A — no .NET telemetry code added |
| V. Test-First Discipline | Is there a test in the correct layer project for each new use case? Do tests use xUnit v3 + plain `Assert.*` with no banned frameworks? | N/A — no use cases or xUnit tests added; validation is `terraform validate` + `terraform fmt` |

> IaC security is addressed through: RBAC Key Vault authorization, Key Vault references (no plaintext secrets in App Service), `purge_protection_enabled = false` for safe destroy/recreate cycles, and `minimum_tls_version = "1.2"` on SQL Server.

## Project Structure

### Documentation (this feature)

```text
specs/001-terraform-azure-workshop-infra/
├── plan.md                          # This file (/speckit.plan output)
├── research.md                      # Phase 0: provider version decisions, API research
├── data-model.md                    # Phase 1: all resources, variables, outputs, dependency graph
├── quickstart.md                    # Phase 1: step-by-step provisioning guide
├── contracts/
│   └── terraform-interface.md       # Phase 1: variable/output contract + permission requirements
└── tasks.md                         # Phase 2 (/speckit.tasks — not created by /speckit.plan)
```

### Source Code (repository root)

```text
infra/
└── terraform/                       # Flat root module — all files here, no subdirectories
    ├── main.tf                      # terraform block, providers, locals, resource_group, log analytics, app insights
    ├── variables.tf                 # tenant_id, subscription_id, location, sql_admin_username/password, dotnet_version
    ├── outputs.tf                   # App Service URLs, client IDs, key_vault_uri, app_insights_conn_str (sensitive)
    ├── app-service.tf               # service_plan, two linux_web_app, two kv role_assignment
    ├── sql.tf                       # mssql_server, mssql_database, mssql_firewall_rule
    ├── keyvault.tf                  # key_vault, four key_vault_secret resources
    ├── entra-id.tf                  # three azuread_application, three service_principal, two application_password
    ├── terraform.tfvars.example     # Placeholder values — committed to version control
    └── .gitignore                   # .terraform/, *.tfvars (except *.tfvars.example), state files
```

**Structure Decision**: Flat root module in `infra/terraform/`. Chosen because FR-019 explicitly requires flat layout with no child modules, and the resource count (~30 resources across 7 concerns) is manageable in a single directory split by concern files. No module abstraction is needed for a single-environment workshop configuration.

## Complexity Tracking

> No constitution violations — all principles are N/A for this Terraform IaC feature.
