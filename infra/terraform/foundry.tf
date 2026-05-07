# Azure AI Foundry account
# kind = "AIServices" + allowProjectManagement = true is what makes it a Foundry resource.
# customSubDomainName is a globally unique DNS label — must contain var.prefix for uniqueness.
# Uses azapi provider (2025-06-01) for full Foundry API surface and preview feature access.
#
# NOTE: var.foundry_location defaults to "eastus" which has the broadest GPT-4.1 availability.
# If you change it, verify the model is available: https://learn.microsoft.com/azure/ai-services/openai/concepts/models
resource "azapi_resource" "ai_foundry" {
  type                      = "Microsoft.CognitiveServices/accounts@2025-06-01"
  name                      = "aif-${var.prefix}"
  parent_id                 = azurerm_resource_group.main.id
  location                  = var.foundry_location
  schema_validation_enabled = false

  identity {
    type = "SystemAssigned"
  }

  body = {
    kind = "AIServices"
    sku = {
      name = "S0"
    }
    properties = {
      # Support both Entra ID and API key auth
      disableLocalAuth = false

      # Required to make this a Foundry resource (enables project management, agents, etc.)
      allowProjectManagement = true

      # Globally unique DNS subdomain: https://<customSubDomainName>.openai.azure.com
      # Must be lowercase alphanumeric only (no hyphens)
      customSubDomainName = "aif${var.prefix}"
    }
  }

  response_export_values = ["properties.endpoint"]
}

# AI Foundry project
# A project is required for the "new Foundry" experience (agents, traces, evaluations, etc.).
# Without it the portal shows the account as classic / no-project.
resource "azapi_resource" "ai_foundry_project" {
  type                      = "Microsoft.CognitiveServices/accounts/projects@2025-06-01"
  name                      = "proj-mcp-server"
  parent_id                 = azapi_resource.ai_foundry.id
  location                  = var.foundry_location
  schema_validation_enabled = false

  depends_on = [azapi_resource.ai_foundry]

  identity {
    type = "SystemAssigned"
  }

  body = {
    sku = {
      name = "S0"
    }
    properties = {
      displayName = "MCP Server Project"
      description = "Project for the dotnet-mcp-server workshop scenario."
    }
  }
}

# GPT-4.1 model deployment
# Version 2025-04-14 is the GA release of gpt-4.1.
# capacity = 1 = 1K tokens per minute (TPM) — increase for production workloads.
resource "azapi_resource" "gpt41_deployment" {
  type      = "Microsoft.CognitiveServices/accounts/deployments@2023-05-01"
  name      = "gpt-4.1"
  parent_id = azapi_resource.ai_foundry.id

  depends_on = [azapi_resource.ai_foundry]

  body = {
    sku = {
      name     = "Standard"
      capacity = 1
    }
    properties = {
      model = {
        format  = "OpenAI"
        name    = "gpt-4.1"
        version = "2025-04-14"
      }
    }
  }
}
