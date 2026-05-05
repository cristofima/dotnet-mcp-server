# Quickstart: Terraform Azure Workshop Infrastructure

**Feature**: `001-terraform-azure-workshop-infra`  
**Branch**: `feature/001-terraform-azure-workshop-infra`

This guide walks a workshop operator from a clean Azure subscription to a fully provisioned MCP Server environment in six steps.

---

## Prerequisites

Verify the following before starting.

### Tools

| Tool | Version | Install |
|---|---|---|
| Terraform | `~> 1.9` | [developer.hashicorp.com/terraform/install](https://developer.hashicorp.com/terraform/install) |
| Azure CLI | `>= 2.55` | [learn.microsoft.com/cli/azure/install-azure-cli](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) |

### Azure Permissions

The account running `terraform apply` needs all three of:

- **Contributor** on the target subscription (create all resources)
- **User Access Administrator** on the target subscription (create RBAC role assignments for managed identities)
- **Application Administrator** or **Cloud Application Administrator** in the Entra ID tenant (create app registrations)

`Owner` at the subscription scope satisfies all three.

### Remote State Storage (one-time setup)

The Terraform backend stores state in Azure Blob Storage. Create the following before running `terraform init` — the Terraform configuration does not provision these:

```bash
# Log in
az login

# Create a resource group for the Terraform state storage
az group create \
  --name rg-tf-state \
  --location westeurope

# Create a storage account (name must be globally unique)
az storage account create \
  --name stworkshoptfstate \
  --resource-group rg-tf-state \
  --location westeurope \
  --sku Standard_LRS

# Create the state container
az storage container create \
  --name tfstate \
  --account-name stworkshoptfstate
```

Adjust `rg-tf-state`, `stworkshoptfstate`, and `westeurope` to match your environment.

---

## Step 1: Clone and Navigate

```bash
git clone https://github.com/your-org/dotnet-mcp-server.git
cd dotnet-mcp-server/infra/terraform
```

---

## Step 2: Create Backend Configuration

Create `backend.conf` in `infra/terraform/`. This file is not committed to version control (listed in `.gitignore`).

```hcl
# infra/terraform/backend.conf
resource_group_name  = "rg-tf-state"
storage_account_name = "stworkshoptfstate"
container_name       = "tfstate"
key                  = "mcp-server.tfstate"
```

Update the values to match the storage account you created in the prerequisite step.

---

## Step 3: Create terraform.tfvars

Copy the example file and fill in your values:

```bash
cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars`:

```hcl
tenant_id        = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"   # Your Entra ID tenant ID
subscription_id  = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"   # Your Azure subscription ID
location         = "westeurope"                              # Azure region

sql_admin_username = "sqladmin"
sql_admin_password = "YourSecure@Password1"                  # Min 8 chars; uppercase, lowercase, digit, special char
```

To find your tenant and subscription IDs:

```bash
az account show --query "{tenantId:tenantId, subscriptionId:id}" -o json
```

---

## Step 4: Initialize Terraform

```bash
terraform init -backend-config=backend.conf
```

Expected output: `Terraform has been successfully initialized!`

Verify the backend is configured:

```bash
terraform show
# Should return: No state.  (empty state on first init)
```

---

## Step 5: Plan

```bash
terraform plan -var-file=terraform.tfvars
```

Review the plan output. You should see approximately **28–32 resources** to be created. Confirm:

- 3 Entra ID app registrations + 3 service principals + 2 passwords
- 1 resource group, 1 App Service Plan, 2 App Services
- 1 SQL Server, 1 SQL Database, 1 firewall rule
- 1 Key Vault, 4 Key Vault secrets, 2 role assignments
- 1 Log Analytics Workspace, 1 Application Insights

---

## Step 6: Apply

```bash
terraform apply -var-file=terraform.tfvars
```

Type `yes` when prompted. Provisioning takes approximately 5–10 minutes.

When complete, retrieve the outputs:

```bash
terraform output
```

Example output:

```
backend_api_client_id     = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
backend_api_url           = "https://app-backend-api.azurewebsites.net"
foundry_agent_client_id   = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
key_vault_uri             = "https://kv-mcp-server.vault.azure.net/"
mcp_server_client_id      = "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
mcp_server_url            = "https://app-mcp-server.azurewebsites.net"
app_insights_connection_string = <sensitive>
```

---

## Post-Apply Manual Steps

Two steps require manual action after `terraform apply` completes:

### 1. Grant Admin Consent for API Permissions

The `required_resource_access` blocks in Entra ID declare the permissions but do not grant admin consent automatically. In the Azure Portal:

1. Go to **Entra ID → App registrations → app-mcp-server → API permissions**
2. Click **Grant admin consent for {your tenant}**
3. Repeat for **app-foundry-agent**

### 2. Add Foundry Redirect URI

The Foundry agent app registration (`app-foundry-agent`) has no redirect URI set. After creating your Foundry project:

1. Go to **Entra ID → App registrations → app-foundry-agent → Authentication**
2. Under **Web → Redirect URIs**, add your Foundry project URL
3. Save

---

## Retrieve Secrets from Key Vault

To retrieve the client secrets for local development or CI/CD:

```bash
# MCP Server client secret
az keyvault secret show \
  --vault-name kv-mcp-server \
  --name mcp-server-client-secret \
  --query value -o tsv

# Foundry agent client secret
az keyvault secret show \
  --vault-name kv-mcp-server \
  --name foundry-agent-client-secret \
  --query value -o tsv
```

---

## Destroy (Cleanup)

```bash
terraform destroy -var-file=terraform.tfvars
```

Because `purge_protection_enabled = false` and the provider's `features.key_vault.purge_soft_delete_on_destroy = true` setting is active, the Key Vault is fully purged (not just soft-deleted). The name `kv-mcp-server` is available immediately after destroy completes.

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| `Error: A resource with the ID "..." already exists` on Key Vault | A soft-deleted vault with the same name exists | Run `az keyvault purge --name kv-mcp-server` to purge it before re-applying |
| `Error: insufficient privileges to complete the operation` on app registrations | Missing Application Administrator role in Entra ID | Request the role from your tenant administrator |
| App Service returns 403 on Key Vault reference | Role assignment not yet propagated | Wait 1–2 minutes and restart the App Service |
| `terraform init` fails with backend authentication error | Azure CLI session expired | Run `az login` and retry |
| SQL password rejected by Azure | Password doesn't meet complexity rules | Use ≥ 8 chars with uppercase, lowercase, digit, and special character |
| `Error: An argument named "client_id" is not expected` on azuread resources | Wrong provider version selected | Confirm `~> 2.0` constraint in `required_providers` and run `terraform init -upgrade` |
