"""
GradCast Lakehouse - Bronze Ingestion Pipeline (PySpark / Auto Loader).

Ingests:
1. Raw shared scenario JSON payloads from edge API landing into bronze.shared_scenarios.
2. Federal College Scorecard annual CSV dumps into bronze.college_scorecard.

Enforces:
- Append-only invariant ('delta.appendOnly' = 'true').
- System audit metadata (_ingest_time, file_name, source_system).
"""

from typing import Optional

try:
    from pyspark.sql import DataFrame, SparkSession
    from pyspark.sql import functions as F
    from pyspark.sql.types import (
        LongType,
        StringType,
        StructField,
        StructType,
        TimestampType,
    )
except ImportError:
    DataFrame = None  # type: ignore
    SparkSession = None  # type: ignore
    F = None  # type: ignore
    class _MockType:
        def __init__(self, *args, **kwargs): pass
    LongType = StringType = StructField = StructType = TimestampType = _MockType  # type: ignore


def get_shared_scenarios_bronze_schema() -> StructType:
    """
    Return explicit schema for bronze.shared_scenarios.
    file_offset stores the partition-unique row identifier generated at ingestion time.
    """
    return StructType([
        StructField("raw_payload", StringType(), False),
        StructField("ingest_timestamp", TimestampType(), False),
        StructField("source_system", StringType(), False),
        StructField("file_name", StringType(), False),
        StructField("file_offset", LongType(), False),
    ])


def ensure_bronze_shared_scenarios_table(
    spark: SparkSession,
    target_table: str = "gradcast_lakehouse.bronze.shared_scenarios",
) -> None:
    """Ensure target bronze table exists upfront with appendOnly property."""
    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {target_table} (
            raw_payload STRING NOT NULL,
            ingest_timestamp TIMESTAMP NOT NULL,
            source_system STRING NOT NULL,
            file_name STRING NOT NULL,
            file_offset BIGINT NOT NULL
        )
        USING DELTA
        TBLPROPERTIES (
            'delta.appendOnly' = 'true',
            'delta.autoOptimize.optimizeWrite' = 'true'
        )
    """)


def ingest_shared_scenarios_batch(
    spark: SparkSession,
    source_path: str,
    target_table: str = "gradcast_lakehouse.bronze.shared_scenarios",
    source_system: str = "edge_api_fly_io",
) -> DataFrame:
    """
    Read raw JSON scenario files and append to Bronze Delta table.
    Ensures delta.appendOnly is set to true.
    Idempotent: uses left_anti join against existing file_names in target table to prevent duplicate ingestion.
    """
    ensure_bronze_shared_scenarios_table(spark, target_table=target_table)

    raw_text_df = spark.read.text(source_path)

    # Filter out already-ingested files to prevent duplicate appends in append-only table
    try:
        existing_files = spark.table(target_table).select("file_name").distinct()
        raw_text_df = raw_text_df.join(
            existing_files,
            raw_text_df["_metadata.file_name"] == existing_files["file_name"],
            "left_anti",
        )
    except Exception:
        pass

    file_offset_col = (
        F.coalesce(F.col("_metadata.row_index"), F.monotonically_increasing_id())
        if hasattr(F, "col")
        else F.monotonically_increasing_id()
    )

    bronze_df = (
        raw_text_df
        .select(
            F.col("value").alias("raw_payload"),
            F.current_timestamp().alias("ingest_timestamp"),
            F.lit(source_system).alias("source_system"),
            F.col("_metadata.file_name").alias("file_name"),
            file_offset_col.alias("file_offset"),
        )
    )

    (
        bronze_df.write
        .format("delta")
        .mode("append")
        .option("mergeSchema", "false")
        .saveAsTable(target_table)
    )

    return bronze_df


def stream_shared_scenarios_autoloader(
    spark: SparkSession,
    source_volume_path: str,
    checkpoint_location: str,
    target_table: str = "gradcast_lakehouse.bronze.shared_scenarios",
    source_system: str = "edge_api_fly_io",
):
    """
    Stream incoming JSON files using Databricks Auto Loader (cloudFiles).
    Ensures delta.appendOnly is enabled upfront.
    """
    ensure_bronze_shared_scenarios_table(spark, target_table=target_table)

    stream_df = (
        spark.readStream
        .format("cloudFiles")
        .option("cloudFiles.format", "text")
        .option("cloudFiles.schemaLocation", f"{checkpoint_location}/schema")
        .load(source_volume_path)
        .select(
            F.col("value").alias("raw_payload"),
            F.current_timestamp().alias("ingest_timestamp"),
            F.lit(source_system).alias("source_system"),
            F.col("_metadata.file_name").alias("file_name"),
            F.coalesce(F.col("_metadata.row_index"), F.monotonically_increasing_id()).cast("bigint").alias("file_offset"),
        )
    )

    return (
        stream_df.writeStream
        .format("delta")
        .outputMode("append")
        .option("checkpointLocation", f"{checkpoint_location}/cp")
        .toTable(target_table)
    )


def ingest_college_scorecard_csv(
    spark: SparkSession,
    csv_path: str,
    report_year: int,
    target_table: str = "gradcast_lakehouse.bronze.college_scorecard",
) -> DataFrame:
    """
    Ingest federal College Scorecard CSV dump into Bronze.
    """
    raw_csv = (
        spark.read
        .option("header", "true")
        .option("inferSchema", "false")
        .csv(csv_path)
    )

    scorecard_df = (
        raw_csv
        .select(
            F.col("UNITID").alias("unitid"),
            F.col("OPEID").alias("opeid"),
            F.col("INSTNM").alias("instnm"),
            F.col("CIPCODE").alias("cipcode"),
            F.col("CREDLEV").alias("credential_level"),
            F.col("MD_EARN_WNE_P10").alias("earnings_10yr"),
            F.col("MD_EARN_WNE_P4").alias("earnings_4yr"),
            F.col("DEBT_MDN").alias("debt_median"),
            F.lit(report_year).cast("int").alias("report_year"),
            F.current_timestamp().alias("_ingest_time"),
        )
    )

    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {target_table} (
            unitid STRING,
            opeid STRING,
            instnm STRING,
            cipcode STRING,
            credential_level STRING,
            earnings_10yr STRING,
            earnings_4yr STRING,
            debt_median STRING,
            report_year INT,
            _ingest_time TIMESTAMP
        )
        USING DELTA
        TBLPROPERTIES ('delta.appendOnly' = 'true')
    """)

    (
        scorecard_df.write
        .format("delta")
        .mode("append")
        .saveAsTable(target_table)
    )

    return scorecard_df


def main() -> int:
    import argparse
    parser = argparse.ArgumentParser(description="Run Bronze Ingest Pipeline")
    parser.add_argument("--catalog", default="gradcast_lakehouse", help="Unity Catalog name")
    parser.add_argument("--source-path", default=None, help="Raw landing source path")
    args = parser.parse_args()

    spark = SparkSession.builder.appName("BronzeIngest").getOrCreate()
    target_table = f"{args.catalog}.bronze.shared_scenarios"
    ensure_bronze_shared_scenarios_table(spark, target_table=target_table)

    source_path = args.source_path or f"/Volumes/{args.catalog}/bronze/raw_landing"
    try:
        ingest_shared_scenarios_batch(spark, source_path=source_path, target_table=target_table)
        print(f"Bronze Ingest batch ingested from {source_path} into {target_table}.")
    except Exception as exc:
        err_msg = str(exc)
        # Only permit skipping if using default unconfigured volume path and the path does not exist yet
        is_missing_path = any(s in err_msg for s in ["Path does not exist", "TABLE_OR_VIEW_NOT_FOUND", "FileNotFoundException"])
        if not args.source_path and is_missing_path:
            print(f"Notice: Default landing volume '{source_path}' does not exist yet; skipping ingest.")
            return 0
        # Fail-closed: re-raise permissions, corrupted payload, or explicit source-path failures
        raise
    print(f"Bronze Ingest pipeline executed successfully for {target_table}.")
    return 0


if __name__ == "__main__":
    import sys
    sys.exit(main())
