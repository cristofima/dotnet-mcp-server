output "mcp_server_url" {
  description = "MCP Server public HTTPS URL."
  value       = "https://${azapi_resource.mcp_server.output.properties.defaultHostName}"
}

output "backend_api_url" {
  description = "Backend API public HTTPS URL."
  value       = "https://${azapi_resource.backend_api.output.properties.defaultHostName}"
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

output "foundry_endpoint" {
  description = "Azure AI Foundry account endpoint (OpenAI-compatible base URL)."
  value       = azapi_resource.ai_foundry.output.properties.endpoint
}

output "key_vault_uri" {
  description = "Key Vault URI for manual secret retrieval."
  value       = azurerm_key_vault.main.vault_uri
}
