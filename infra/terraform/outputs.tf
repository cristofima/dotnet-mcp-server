output "mcp_server_url" {
  description = "MCP Server public HTTPS URL."
  value       = "https://${azurerm_linux_web_app.mcp_server.default_hostname}"
}

output "backend_api_url" {
  description = "Backend API public HTTPS URL."
  value       = "https://${azurerm_linux_web_app.backend_api.default_hostname}"
}

output "app_insights_connection_string" {
  description = "Application Insights connection string."
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "backend_api_client_id" {
  description = "Backend API Entra ID application (client) ID."
  value       = azuread_application.backend_api.client_id
}

output "mcp_server_client_id" {
  description = "MCP Server Entra ID application (client) ID."
  value       = azuread_application.mcp_server.client_id
}

output "foundry_agent_client_id" {
  description = "Foundry agent Entra ID application (client) ID."
  value       = azuread_application.agent.client_id
}

output "key_vault_uri" {
  description = "Key Vault URI for manual secret retrieval."
  value       = azurerm_key_vault.main.vault_uri
}
