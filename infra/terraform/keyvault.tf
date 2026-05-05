data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "main" {
  name                      = "kv-${var.prefix}"
  resource_group_name       = azurerm_resource_group.main.name
  location                  = azurerm_resource_group.main.location
  tenant_id                 = var.tenant_id
  sku_name                  = "standard"
  purge_protection_enabled  = false
  enable_rbac_authorization = true
}

# Grant the Terraform operator temporary access so secrets can be written during apply.
# Operators can remove this role assignment after the initial provisioning if desired.
resource "azurerm_role_assignment" "terraform_operator_kv" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = data.azurerm_client_config.current.object_id
}

resource "azurerm_key_vault_secret" "mcp_client_secret" {
  name         = "mcp-server-client-secret"
  value        = azuread_application_password.mcp_server.value
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_kv]
}

resource "azurerm_key_vault_secret" "foundry_client_secret" {
  name         = "foundry-agent-client-secret"
  value        = azuread_application_password.agent.value
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_kv]
}

resource "azurerm_key_vault_secret" "app_insights_conn_str" {
  name         = "app-insights-connection-string"
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_kv]
}

resource "azurerm_key_vault_secret" "backend_api_sql_conn_str" {
  name         = "backend-api-sql-connection-string"
  value        = "Server=tcp:${azurerm_mssql_server.main.fully_qualified_domain_name},1433;Initial Catalog=${azurerm_mssql_database.main.name};Persist Security Info=False;User ID=${var.sql_admin_username};Password=${var.sql_admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.terraform_operator_kv]
}
