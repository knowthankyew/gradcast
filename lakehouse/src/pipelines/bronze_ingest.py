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
from pyspark.sql import DataFrame, SparkSession
from pyspark.sql import functions as F
from pyspark.sql.types import (
    BigIntegerType,
    StringType,
    StructField,
    StructType,
    TimestampType,
)


def get_shared_scenarios_bronze_schema() -> StructType:
    """Return explicit schema for bronze.shared_scenarios."""
    return StructType([
        StructField("raw_payload", StringType(), False),
        StructField("ingest_timestamp", TimestampType(), False),
        StructField("source_system", StringType(), False),
        StructField("file_name", StringType(), False),
        StructField("file_offset", BigIntegerType(), False),
    ])


def ingest_shared_scenarios_batch(
    spark: SparkSession,
    source_path: str,
    target_table: str = "gradcast_lakehouse.bronze.shared_scenarios",
    source_system: str = "edge_api_fly_io",
) -> DataFrame:
    """
    Read raw JSON scenario files and append to Bronze Delta table.
    Ensures delta.appendOnly is set to true.
    """
    raw_text_df = spark.read.text(source_path)

    bronze_df = (
        raw_text_df
        .select(
            F.col("value").alias("raw_payload"),
            F.current_timestamp().alias("ingest_timestamp"),
            F.lit(source_system).alias("source_system"),
            F.input_file_name().alias("file_name"),
            F.monotonically_increasing_id().alias("file_offset"),
        )
    )

    (
        bronze_df.write
        .format("delta")
        .mode("append")
        .option("mergeSchema", "false")
        .saveAsTable(target_table)
    )

    # Enforce appendOnly on the table properties
    spark.sql(f"""
        ALTER TABLE {target_table}
        SET TBLPROPERTIES (
            'delta.appendOnly' = 'true',
            'delta.autoOptimize.optimizeWrite' = 'true'
        )
    """)

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
    """
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
            F.input_file_name().alias("file_name"),
            F.monotonically_increasing_id().alias("file_offset"),
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
            F.col("MD_EARN_WNE_P10").alias("earnings_1yr"),
            F.col("MD_EARN_WNE_P4").alias("earnings_4yr"),
            F.col("DEBT_MDN").alias("debt_median"),
            F.lit(report_year).cast("int").alias("report_year"),
            F.current_timestamp().alias("_ingest_time"),
        )
    )

    (
        scorecard_df.write
        .format("delta")
        .mode("append")
        .saveAsTable(target_table)
    )

    return scorecard_df


if __name__ == "__main__":
    spark = SparkSession.builder.appName("BronzeIngest").getOrCreate()
    print("Bronze Ingest module loaded successfully.")
