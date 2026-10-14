resource "databricks_service_principal" "pipeline_runner" {
  display_name = "gradcast-lakehouse-pipeline-runner-${var.environment}"
  active       = true
}

resource "databricks_group" "analysts" {
  display_name = "gradcast-analysts-${var.environment}"
}

# Catalog access grants
resource "databricks_grants" "catalog_access" {
  catalog = databricks_catalog.lakehouse.name

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_CATALOG"]
  }

  grant {
    principal  = databricks_group.analysts.display_name
    privileges = ["USE_CATALOG"]
  }
}

# Bronze schema grants (Service principal pipeline ingest only)
resource "databricks_grants" "bronze_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.bronze.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }
}

# Silver schema grants (Pipeline transformation only)
resource "databricks_grants" "silver_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.silver.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }
}

# Gold schema grants (Service principal manages; Analysts read verified k-anonymity marts)
resource "databricks_grants" "gold_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.gold.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }

  grant {
    principal  = databricks_group.analysts.display_name
    privileges = ["USE_SCHEMA", "SELECT"]
  }
}
