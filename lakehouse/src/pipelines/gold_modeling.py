"""
GradCast Lakehouse - Gold Layer Dimensional Modeling.

Constructs:
1. Star schema fact: fact_scenario_outcomes
2. Conformed dimensions: dim_school, dim_metro, dim_program
"""

from pyspark.sql import DataFrame, SparkSession
from pyspark.sql import functions as F


def ensure_gold_tables(spark: SparkSession, catalog: str = "gradcast_lakehouse") -> None:
    """
    Declare Gold dimension and fact tables with Liquid Clustering.
    """
    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {catalog}.gold.dim_school (
            school_key BIGINT GENERATED ALWAYS AS IDENTITY,
            school_id INT NOT NULL,
            school_name STRING NOT NULL,
            city STRING NOT NULL,
            state STRING NOT NULL,
            school_url STRING,
            ownership INT NOT NULL
        )
        USING DELTA
        CLUSTER BY (school_id);
    """)

    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {catalog}.gold.dim_metro (
            cbsa_code STRING NOT NULL,
            metro_name STRING NOT NULL,
            state_code STRING NOT NULL,
            metro_type STRING NOT NULL
        )
        USING DELTA
        CLUSTER BY (cbsa_code);
    """)

    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {catalog}.gold.dim_program (
            program_key BIGINT GENERATED ALWAYS AS IDENTITY,
            cip_code STRING NOT NULL,
            cip_family STRING NOT NULL,
            program_title STRING
        )
        USING DELTA
        CLUSTER BY (cip_code);
    """)

    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {catalog}.gold.fact_scenario_outcomes (
            scenario_key BIGINT GENERATED ALWAYS AS IDENTITY,
            scenario_id STRING NOT NULL,
            school_id INT NOT NULL,
            cip_code STRING NOT NULL,
            cbsa_code STRING NOT NULL,
            date_key INT NOT NULL,
            housing_type STRING NOT NULL,
            annual_salary DECIMAL(12, 2) NOT NULL,
            monthly_rent DECIMAL(10, 2) NOT NULL,
            monthly_disposable DECIMAL(10, 2) NOT NULL,
            disposable_ratio DOUBLE NOT NULL
        )
        USING DELTA
        CLUSTER BY (cbsa_code, cip_code);
    """)


def populate_fact_scenario_outcomes(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
) -> None:
    """
    Populate fact_scenario_outcomes from silver.scenarios.
    """
    silver_df = spark.table(f"{catalog}.silver.scenarios")

    fact_df = (
        silver_df
        .filter(F.col("is_valid_positive_find") == True)
        .select(
            F.col("scenario_id"),
            F.col("school_id"),
            F.col("cip_code"),
            F.col("cbsa_code"),
            F.date_format(F.col("created_at"), "yyyyMMdd").cast("int").alias("date_key"),
            F.col("housing_type"),
            F.col("reported_annual_salary").alias("annual_salary"),
            F.col("monthly_rent"),
            F.col("monthly_disposable"),
            F.col("disposable_ratio"),
        )
    )

    (
        fact_df.write
        .format("delta")
        .mode("append")
        .saveAsTable(f"{catalog}.gold.fact_scenario_outcomes")
    )


if __name__ == "__main__":
    spark = SparkSession.builder.appName("GoldModeling").getOrCreate()
    print("Gold Modeling module loaded successfully.")
