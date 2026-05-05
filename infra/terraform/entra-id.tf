# Backend API app registration
# Exposes the user_impersonation delegated scope.
# No client secret and no app roles (FR-011, FR-014).
resource "azuread_application" "backend_api" {
  display_name     = "app-backend-api"
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

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

# MCP Server app registration
# Exposes the mcp.access delegated scope and requires user_impersonation on the Backend API (FR-012).
resource "azuread_application" "mcp_server" {
  display_name     = "app-mcp-server"
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

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

resource "azuread_application_password" "mcp_server" {
  application_id = azuread_application.mcp_server.id
  display_name   = "Terraform-managed secret"
}

# Foundry agent (OAuth passthrough) app registration
# Requires mcp.access on the MCP Server. No custom scopes or app roles (FR-013, FR-014).
# Redirect URI is left empty and must be added manually after the Foundry project is created.
resource "azuread_application" "agent" {
  display_name     = "app-foundry-agent"
  sign_in_audience = "AzureADandPersonalMicrosoftAccount"

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
