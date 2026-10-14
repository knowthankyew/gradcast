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

# Gold schema grants (Service principal manages; Analysts have USE_SCHEMA)
resource "databricks_grants" "gold_schema_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.gold.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }

  grant {
    principal  = databricks_group.analysts.display_name
    privileges = ["USE_SCHEMA"]
  }
}

# Quarantine schema grants (Service principal pipeline quarantine only)
resource "databricks_grants" "quarantine_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.quarantine.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }
}

# Control schema grants (Service principal watermark metadata only)
resource "databricks_grants" "control_access" {
  schema = "${databricks_catalog.lakehouse.name}.${databricks_schema.control.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["USE_SCHEMA", "SELECT", "MODIFY", "CREATE_TABLE"]
  }
}

# Bronze raw landing volume grants
resource "databricks_grants" "raw_landing_volume_access" {
  volume = "${databricks_catalog.lakehouse.name}.${databricks_schema.bronze.name}.${databricks_volume.raw_landing.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["READ_VOLUME", "WRITE_VOLUME"]
  }
}

# Gold exports volume grants (Reverse-ETL SQLite compiler destination)
resource "databricks_grants" "gold_exports_volume_access" {
  volume = "${databricks_catalog.lakehouse.name}.${databricks_schema.gold.name}.${databricks_volume.gold_exports.name}"

  grant {
    principal  = databricks_service_principal.pipeline_runner.application_id
    privileges = ["READ_VOLUME", "WRITE_VOLUME"]
  }
}

