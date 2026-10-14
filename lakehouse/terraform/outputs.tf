output "catalog_name" {
  description = "Unity Catalog name"
  value       = databricks_catalog.lakehouse.name
}

output "bronze_schema" {
  description = "Bronze schema full namespace"
  value       = "${databricks_catalog.lakehouse.name}.${databricks_schema.bronze.name}"
}

output "silver_schema" {
  description = "Silver schema full namespace"
  value       = "${databricks_catalog.lakehouse.name}.${databricks_schema.silver.name}"
}

output "gold_schema" {
  description = "Gold schema full namespace"
  value       = "${databricks_catalog.lakehouse.name}.${databricks_schema.gold.name}"
}

output "raw_landing_volume" {
  description = "Raw landing volume path"
  value       = "/Volumes/${databricks_catalog.lakehouse.name}/${databricks_schema.bronze.name}/${databricks_volume.raw_landing.name}"
}

output "quarantine_schema" {
  description = "Quarantine schema full namespace"
  value       = "${databricks_catalog.lakehouse.name}.${databricks_schema.quarantine.name}"
}
