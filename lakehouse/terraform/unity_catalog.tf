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

resource "databricks_sql_table" "v_public_positive_finds" {
  catalog_name = databricks_catalog.lakehouse.name
  schema_name  = databricks_schema.gold.name
  name         = "v_public_positive_finds"
  table_type   = "VIEW"
  view_definition = <<-EOT
    SELECT 
        f.cbsa_code,
        m.metro_name,
        p.cip_family,
        f.credential_level,
        f.housing_type,
        COUNT(*) AS sample_count,
        ROUND(AVG(f.monthly_disposable), 2) AS mean_monthly_disposable,
        PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY f.monthly_disposable) AS median_disposable,
        PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY f.monthly_disposable) AS p25_disposable,
        PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY f.monthly_disposable) AS p75_disposable
    FROM ${databricks_catalog.lakehouse.name}.gold.fact_scenario_outcomes f
    JOIN ${databricks_catalog.lakehouse.name}.gold.dim_metro m ON f.cbsa_code = m.cbsa_code
    JOIN ${databricks_catalog.lakehouse.name}.gold.dim_program p ON f.cip_code = p.cip_code
    GROUP BY 
        f.cbsa_code, 
        m.metro_name, 
        p.cip_family, 
        f.credential_level,
        f.housing_type
    HAVING COUNT(*) >= 5
  EOT
  comment = "Public k-anonymized (k >= 5) reporting view enforcing zero individual student re-identification"
}
