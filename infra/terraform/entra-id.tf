# Backend API app registration
# Exposes the user_impersonation delegated scope.
# No client secret and no app roles (FR-011, FR-014).
resource "azuread_application" "backend_api" {
  display_name     = "app-backend-api"
  sign_in_audience = "AzureADMultipleOrgs"

  api {
    requested_access_token_version = 2

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
}

resource "azuread_service_principal" "backend_api" {
  client_id = azuread_application.backend_api.client_id
}

# Application ID URI for backend_api: api://<client_id>
# Set separately to avoid the self-reference circular dependency inside azuread_application.
resource "azuread_application_identifier_uri" "backend_api" {
  application_id = azuread_application.backend_api.id
  identifier_uri = "api://${azuread_application.backend_api.client_id}"
}

# MCP Server app registration
# Exposes the mcp.access delegated scope and requires user_impersonation on the Backend API (FR-012).
resource "azuread_application" "mcp_server" {
  display_name     = "app-mcp-server"
  sign_in_audience = "AzureADMultipleOrgs"

  api {
    requested_access_token_version = 2

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
    resource_app_id = azuread_application.backend_api.client_id

    resource_access {
      id   = local.backend_api_user_impersonation_scope_id
      type = "Scope"
    }
  }
}

resource "azuread_service_principal" "mcp_server" {
  client_id = azuread_application.mcp_server.client_id
}

# Application ID URI for mcp_server: api://<client_id>
# This is what consumers reference in scopes: api://<client_id>/mcp.access
resource "azuread_application_identifier_uri" "mcp_server" {
  application_id = azuread_application.mcp_server.id
  identifier_uri = "api://${azuread_application.mcp_server.client_id}"
}

resource "azuread_application_password" "mcp_server" {
  application_id = azuread_application.mcp_server.id
  display_name   = "Terraform-managed secret"
}

# Foundry agent (OAuth passthrough) app registration
# Requires mcp.access on the MCP Server. No custom scopes or app roles (FR-013, FR-014).
# Redirect URI is left empty and must be added manually after the Foundry project is created.
resource "azuread_application" "agent" {
  display_name     = "app-foundry-agent"
  sign_in_audience = "AzureADMultipleOrgs"

  api {
    requested_access_token_version = 2
  }

  required_resource_access {
    resource_app_id = azuread_application.mcp_server.client_id

    resource_access {
      id   = local.mcp_server_access_scope_id
      type = "Scope"
    }
  }

  web {
    redirect_uris = []
  }
}

resource "azuread_service_principal" "agent" {
  client_id = azuread_application.agent.client_id
}

resource "azuread_application_password" "agent" {
  application_id = azuread_application.agent.id
  display_name   = "Terraform-managed secret"
}

# azuread v2 and azapi v2 do not expose Microsoft Graph tokenLifetimePolicies natively.
# We use terraform_data + local-exec (PowerShell + az CLI) to manage the policy via Graph API.
# The provisioner is idempotent: it checks for an existing assignment before creating.
# Triggers re-run whenever the MCP Server service principal is recreated.
resource "terraform_data" "mcp_token_lifetime_policy" {
  triggers_replace = [azuread_service_principal.mcp_server.object_id]

  provisioner "local-exec" {
    interpreter = ["pwsh", "-Command"]
    command     = <<-EOT
      $spId  = "${azuread_service_principal.mcp_server.object_id}"
      $token = az account get-access-token --resource https://graph.microsoft.com/ --query accessToken -o tsv
      $hdrs  = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

      # Skip if a lifetime policy is already assigned to this SP
      $cur = Invoke-RestMethod -Method GET `
               -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$spId/tokenLifetimePolicies" `
               -Headers $hdrs
      if ($cur.value.Count -gt 0) { Write-Host "Token lifetime policy already assigned, skipping."; exit 0 }

      # Create the 8-hour access token policy
      $policyJson = '{"definition":["{\"TokenLifetimePolicy\":{\"Version\":1,\"AccessTokenLifetime\":\"08:00:00\"}}"],"displayName":"MCP Server - 8h access token","isOrganizationDefault":false}'
      $policy = Invoke-RestMethod -Method POST `
                  -Uri "https://graph.microsoft.com/v1.0/policies/tokenLifetimePolicies" `
                  -Headers $hdrs -Body $policyJson
      Write-Host "Token lifetime policy created: $($policy.id)"

      # Assign the policy to the MCP Server service principal
      $refJson = '{"@odata.id":"https://graph.microsoft.com/v1.0/policies/tokenLifetimePolicies/' + $policy.id + '"}'
      Invoke-RestMethod -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$spId/tokenLifetimePolicies/`$ref" `
        -Headers $hdrs -Body $refJson
      Write-Host "Policy assigned to SP $spId"
    EOT
  }
}
