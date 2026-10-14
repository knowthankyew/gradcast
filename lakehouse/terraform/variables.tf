variable "databricks_host" {
  type        = string
  description = "Databricks workspace URL (e.g. https://adb-123456789.azuredatabricks.net or https://dbc-1234.cloud.databricks.com)"
  default     = "https://adb-mock-workspace.azuredatabricks.net"
}

variable "databricks_client_id" {
  type        = string
  description = "OAuth M2M service principal client application ID"
  default     = ""
  sensitive   = true
}

variable "databricks_client_secret" {
  type        = string
  description = "OAuth M2M service principal client secret"
  default     = ""
  sensitive   = true
}

variable "environment" {
  type        = string
  description = "Deployment target environment (dev, staging, prod)"
  default     = "dev"
}

variable "catalog_name" {
  type        = string
  description = "Unity Catalog name for the GradCast enterprise single source of truth"
  default     = "gradcast_lakehouse"
}
