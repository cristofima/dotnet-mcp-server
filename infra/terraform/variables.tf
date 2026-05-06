variable "prefix" {
  type        = string
  description = "Short identifier (3-8 lowercase alphanumeric chars) prepended to globally unique resource names: Key Vault, SQL Server, App Services. Use your initials or a short project code."

  validation {
    condition     = can(regex("^[a-z0-9]{3,8}$", var.prefix))
    error_message = "prefix must be 3-8 lowercase alphanumeric characters (no hyphens or spaces)."
  }
}

variable "tenant_id" {
  type        = string
  description = "Entra ID tenant ID (GUID)."
}

variable "subscription_id" {
  type        = string
  description = "Azure subscription ID (GUID)."
}

variable "location" {
  type        = string
  description = "Azure region for SQL Server, Key Vault, Log Analytics, and App Insights."
  default     = "centralus"
}

variable "app_service_location" {
  type        = string
  description = "Azure region for the App Service Plan and Web Apps. May differ from 'location' when regional quota restrictions apply."
  default     = "centralus"
}

variable "sql_admin_username" {
  type        = string
  sensitive   = true
  description = "SQL Server administrator login. Must not be a reserved word (admin, sa, root, etc.)."
}

variable "sql_admin_password" {
  type        = string
  sensitive   = true
  description = "SQL Server administrator password. Must meet Azure SQL complexity requirements: >=8 characters, uppercase, lowercase, digit, and special character."
}

variable "dotnet_version" {
  type        = string
  description = ".NET application stack version for App Services (e.g., \"9.0\", \"10.0\")."
  default     = "10.0"
}
