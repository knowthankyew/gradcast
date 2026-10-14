resource "databricks_catalog" "lakehouse" {
  name    = var.catalog_name
  comment = "GradCast enterprise Single Source of Truth (SSOT) Lakehouse catalog"

  properties = {
    "governance" = "knowthankyew-safe-allowlist"
    "mode"       = "dual-mode-storage"
    "env"        = var.environment
  }
}

resource "databricks_schema" "bronze" {
  catalog_name = databricks_catalog.lakehouse.name
  name         = "bronze"
  comment      = "Append-only raw landing zone for Scorecard archives and shared scenarios"

  properties = {
    "tier"       = "bronze"
    "retention"  = "permanent"
    "appendOnly" = "true"
  }
}

resource "databricks_schema" "silver" {
  catalog_name = databricks_catalog.lakehouse.name
  name         = "silver"
  comment      = "Cleansed, conformed data with Liquid Clustering and Delta Change Data Feed"

  properties = {
    "tier"       = "silver"
    "clustering" = "liquid"
    "cdf"        = "enabled"
  }
}

resource "databricks_schema" "gold" {
  catalog_name = databricks_catalog.lakehouse.name
  name         = "gold"
  comment      = "Dimensional star schema and k-anonymity (k >= 5) views compiled for Reverse-ETL"

  properties = {
    "tier"        = "gold"
    "privacy"     = "k-anonymity-5"
    "reverse_etl" = "sqlite3-export"
  }
}

resource "databricks_volume" "raw_landing" {
  name         = "raw_landing"
  catalog_name = databricks_catalog.lakehouse.name
  schema_name  = databricks_schema.bronze.name
  volume_type  = "MANAGED"
  comment      = "Managed volume for raw College Scorecard CSV dumps and incoming scenario JSON files"
}

resource "databricks_volume" "gold_exports" {
  name         = "exports"
  catalog_name = databricks_catalog.lakehouse.name
  schema_name  = databricks_schema.gold.name
  volume_type  = "MANAGED"
  comment      = "Managed volume for compiled, k-anonymized Reverse-ETL SQLite artifacts"
}

resource "databricks_schema" "quarantine" {
  catalog_name = databricks_catalog.lakehouse.name
  name         = "quarantine"
  comment      = "Data quality quarantine layer for rejected scenarios and ingestion failures"
}
