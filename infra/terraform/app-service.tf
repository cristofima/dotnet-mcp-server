resource "azurerm_service_plan" "main" {
  name                = "plan-${var.prefix}"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.app_service_location
  os_type             = "Linux"
  sku_name            = "B1"
}

resource "azurerm_linux_web_app" "mcp_server" {
  name                = "app-${var.prefix}-mcp"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.app_service_location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = var.dotnet_version
    }
  }

  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.main.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})"
    "EntraId__ClientSecret"                 = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.mcp_client_secret.versionless_id})"
  }
}

resource "azurerm_linux_web_app" "backend_api" {
  name                = "app-${var.prefix}-api"
  resource_group_name = azurerm_resource_group.main.name
  location            = var.app_service_location
  service_plan_id     = azurerm_service_plan.main.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version = var.dotnet_version
    }
  }

  app_settings = {
    "APPINSIGHTS_INSTRUMENTATIONKEY"        = azurerm_application_insights.main.instrumentation_key
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.app_insights_conn_str.versionless_id})"
    "ConnectionStrings__Default"            = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault_secret.backend_api_sql_conn_str.versionless_id})"
  }
}

resource "azurerm_role_assignment" "mcp_server_kv" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.mcp_server.identity[0].principal_id
}

resource "azurerm_role_assignment" "backend_api_kv" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_web_app.backend_api.identity[0].principal_id
}
