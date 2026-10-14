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
            credential_level INT NOT NULL,
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
    Populate fact_scenario_outcomes idempotently from silver.scenarios using Delta MERGE.
    """
    silver_df = spark.table(f"{catalog}.silver.scenarios")

    fact_df = (
        silver_df
        .filter(F.col("is_valid_positive_find") == True)
        .select(
            F.col("scenario_id"),
            F.col("school_id"),
            F.col("cip_code"),
            F.col("credential_level"),
            F.col("cbsa_code"),
            F.date_format(F.col("created_at"), "yyyyMMdd").cast("int").alias("date_key"),
            F.col("housing_type"),
            F.col("reported_annual_salary").alias("annual_salary"),
            F.col("monthly_rent"),
            F.col("monthly_disposable"),
            F.col("disposable_ratio"),
        )
        .dropDuplicates(["scenario_id"])
    )

    fact_df.createOrReplaceTempView("stage_fact_scenarios")

    spark.sql(f"""
        MERGE INTO {catalog}.gold.fact_scenario_outcomes AS target
        USING stage_fact_scenarios AS source
        ON target.scenario_id = source.scenario_id
        WHEN MATCHED THEN
            UPDATE SET
                target.school_id = source.school_id,
                target.cip_code = source.cip_code,
                target.credential_level = source.credential_level,
                target.cbsa_code = source.cbsa_code,
                target.date_key = source.date_key,
                target.housing_type = source.housing_type,
                target.annual_salary = source.annual_salary,
                target.monthly_rent = source.monthly_rent,
                target.monthly_disposable = source.monthly_disposable,
                target.disposable_ratio = source.disposable_ratio
        WHEN NOT MATCHED THEN
            INSERT (
                scenario_id, school_id, cip_code, credential_level, cbsa_code, date_key,
                housing_type, annual_salary, monthly_rent, monthly_disposable, disposable_ratio
            )
            VALUES (
                source.scenario_id, source.school_id, source.cip_code, source.credential_level, source.cbsa_code, source.date_key,
                source.housing_type, source.annual_salary, source.monthly_rent, source.monthly_disposable, source.disposable_ratio
            )
    """)


def populate_dimensions(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
) -> None:
    """
    Populate dim_school, dim_metro, and dim_program from Silver conformed tables.
    Deduplicates strictly by natural keys to prevent join fan-out.
    """
    silver_scenarios = spark.table(f"{catalog}.silver.scenarios")

    # 1. dim_program (unique per cip_code)
    programs_df = (
        silver_scenarios
        .select(
            F.col("cip_code"),
            F.col("cip_family"),
            F.concat(F.lit("Field of Study: "), F.col("cip_family")).alias("program_title"),
        )
        .dropDuplicates(["cip_code"])
    )
    programs_df.createOrReplaceTempView("stage_dim_programs")
    spark.sql(f"""
        MERGE INTO {catalog}.gold.dim_program AS target
        USING stage_dim_programs AS source
        ON target.cip_code = source.cip_code
        WHEN MATCHED THEN
            UPDATE SET
                target.cip_family = source.cip_family,
                target.program_title = source.program_title
        WHEN NOT MATCHED THEN
            INSERT (cip_code, cip_family, program_title)
            VALUES (source.cip_code, source.cip_family, source.program_title)
    """)

    # 2. dim_metro (unique per cbsa_code)
    metro_df = (
        silver_scenarios
        .select(
            F.col("cbsa_code"),
            F.concat(F.lit("Metro Area "), F.col("cbsa_code")).alias("metro_name"),
            F.lit("US").alias("state_code"),
            F.lit("Metropolitan").alias("metro_type"),
        )
        .dropDuplicates(["cbsa_code"])
    )
    metro_df.createOrReplaceTempView("stage_dim_metro")
    spark.sql(f"""
        MERGE INTO {catalog}.gold.dim_metro AS target
        USING stage_dim_metro AS source
        ON target.cbsa_code = source.cbsa_code
        WHEN MATCHED THEN
            UPDATE SET
                target.metro_name = source.metro_name,
                target.state_code = source.state_code,
                target.metro_type = source.metro_type
        WHEN NOT MATCHED THEN
            INSERT (cbsa_code, metro_name, state_code, metro_type)
            VALUES (source.cbsa_code, source.metro_name, source.state_code, source.metro_type)
    """)

    # 3. dim_school (unique per school_id)
    school_df = (
        silver_scenarios
        .select(
            F.col("school_id"),
            F.concat(F.lit("Institution #"), F.col("school_id")).alias("school_name"),
            F.lit("Unknown").alias("city"),
            F.lit("US").alias("state"),
            F.lit(None).cast("string").alias("school_url"),
            F.lit(1).cast("int").alias("ownership"),
        )
        .dropDuplicates(["school_id"])
    )
    school_df.createOrReplaceTempView("stage_dim_school")
    spark.sql(f"""
        MERGE INTO {catalog}.gold.dim_school AS target
        USING stage_dim_school AS source
        ON target.school_id = source.school_id
        WHEN MATCHED THEN
            UPDATE SET
                target.school_name = source.school_name
        WHEN NOT MATCHED THEN
            INSERT (school_id, school_name, city, state, school_url, ownership)
            VALUES (source.school_id, source.school_name, source.city, source.state, source.school_url, source.ownership)
    """)


def run_gold_pipeline(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
) -> None:
    """Execute complete Gold dimensional pipeline."""
    ensure_gold_tables(spark, catalog)
    populate_dimensions(spark, catalog)
    populate_fact_scenario_outcomes(spark, catalog)


if __name__ == "__main__":
    spark = SparkSession.builder.appName("GoldModeling").getOrCreate()
    print("Gold Modeling module loaded successfully.")
