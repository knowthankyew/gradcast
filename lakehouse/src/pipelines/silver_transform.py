"""
GradCast Lakehouse - Silver Cleansing & Conformance Pipeline.

Performs:
1. JSON schema parsing of raw bronze payloads.
2. Invariant verification & quarantine routing for corrupt/dirty records.
3. Derivation of clustering keys (cip_family).
4. Type coercion to precise DECIMAL and DOUBLE types.
5. Deduplication via Delta MERGE INTO.
6. Table creation with Liquid Clustering: CLUSTER BY (cip_family, cbsa_code).
7. Delta Change Data Feed enablement ('delta.enableChangeDataFeed' = 'true').
"""

from pyspark.sql import DataFrame, SparkSession
from pyspark.sql import functions as F
from pyspark.sql.types import (
    DoubleType,
    IntegerType,
    StringType,
    StructField,
    StructType,
    TimestampType,
)

# Raw JSON schema emitted by the ASP.NET Core edge ingest layer
SCENARIO_JSON_SCHEMA = StructType([
    StructField("id", StringType(), False),
    StructField("created_at", StringType(), False),
    StructField("school_id", IntegerType(), False),
    StructField("cip_code", StringType(), False),
    StructField("credential_level", IntegerType(), False),
    StructField("cbsa_code", StringType(), False),
    StructField("housing_type", StringType(), False),
    StructField("salary_mode", StringType(), False),
    StructField("reported_annual_salary", DoubleType(), False),
    StructField("monthly_net_take_home", DoubleType(), False),
    StructField("monthly_rent", DoubleType(), False),
    StructField("monthly_loan_payment", DoubleType(), False),
    StructField("monthly_disposable", DoubleType(), False),
    StructField("disposable_ratio", DoubleType(), False),
    StructField("sentiment_tag", StringType(), False),
    StructField("scenario_title", StringType(), True),
    StructField("client_hash", StringType(), True),
])


def ensure_silver_scenarios_table(
    spark: SparkSession,
    table_name: str = "gradcast_lakehouse.silver.scenarios",
) -> None:
    """
    Declare the Silver scenarios table with Liquid Clustering, Change Data Feed,
    and boundary constraints if it does not exist.
    """
    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {table_name} (
            scenario_id STRING NOT NULL,
            created_at TIMESTAMP NOT NULL,
            school_id INT NOT NULL,
            cip_code STRING NOT NULL,
            cip_family STRING NOT NULL,
            credential_level INT NOT NULL,
            cbsa_code STRING NOT NULL,
            housing_type STRING NOT NULL,
            salary_mode STRING NOT NULL,
            reported_annual_salary DECIMAL(12, 2) NOT NULL,
            monthly_net_take_home DECIMAL(10, 2) NOT NULL,
            monthly_rent DECIMAL(10, 2) NOT NULL,
            monthly_loan_payment DECIMAL(10, 2) NOT NULL,
            monthly_disposable DECIMAL(10, 2) NOT NULL,
            disposable_ratio DOUBLE NOT NULL,
            sentiment_tag STRING NOT NULL,
            is_valid_positive_find BOOLEAN NOT NULL,
            _updated_at TIMESTAMP NOT NULL
        )
        USING DELTA
        CLUSTER BY (cip_family, cbsa_code)
        TBLPROPERTIES (
            'delta.enableChangeDataFeed' = 'true',
            'delta.minReaderVersion' = '2',
            'delta.minWriterVersion' = '5'
        )
    """)


def parse_and_cleanse_scenarios(raw_df: DataFrame) -> tuple[DataFrame, DataFrame]:
    """
    Parse raw bronze payload, classify records into valid vs quarantined.
    Returns: (valid_df, quarantine_df)
    """
    parsed_df = (
        raw_df
        .withColumn("parsed", F.from_json(F.col("raw_payload"), SCENARIO_JSON_SCHEMA))
        .select(
            F.col("parsed.id").alias("scenario_id"),
            F.to_timestamp(F.col("parsed.created_at")).alias("created_at"),
            F.col("parsed.school_id").cast("int").alias("school_id"),
            F.col("parsed.cip_code").alias("cip_code"),
            F.substring(F.regexp_replace(F.col("parsed.cip_code"), "\\.", ""), 1, 2).alias("cip_family"),
            F.col("parsed.credential_level").cast("int").alias("credential_level"),
            F.col("parsed.cbsa_code").alias("cbsa_code"),
            F.lower(F.col("parsed.housing_type")).alias("housing_type"),
            F.lower(F.col("parsed.salary_mode")).alias("salary_mode"),
            F.col("parsed.reported_annual_salary").cast("decimal(12,2)").alias("reported_annual_salary"),
            F.col("parsed.monthly_net_take_home").cast("decimal(10,2)").alias("monthly_net_take_home"),
            F.col("parsed.monthly_rent").cast("decimal(10,2)").alias("monthly_rent"),
            F.col("parsed.monthly_loan_payment").cast("decimal(10,2)").alias("monthly_loan_payment"),
            F.col("parsed.monthly_disposable").cast("decimal(10,2)").alias("monthly_disposable"),
            F.col("parsed.disposable_ratio").cast("double").alias("disposable_ratio"),
            F.lower(F.col("parsed.sentiment_tag")).alias("sentiment_tag"),
            (F.col("parsed.monthly_disposable") > 0).alias("is_valid_positive_find"),
            F.current_timestamp().alias("_updated_at"),
            F.col("raw_payload"),
        )
    )

    # Validation rules
    is_valid_cond = (
        F.col("scenario_id").isNotNull()
        & F.col("created_at").isNotNull()
        & (F.col("school_id") > 0)
        & F.col("cip_code").isNotNull()
        & F.col("cbsa_code").isNotNull()
        & (F.col("reported_annual_salary") >= 10000.00)
        & (F.col("monthly_disposable") >= -20000.00)
    )
    is_valid_cond = F.coalesce(is_valid_cond, F.lit(False))

    valid_df = parsed_df.filter(is_valid_cond).drop("raw_payload")

    quarantine_df = (
        parsed_df
        .filter(~is_valid_cond)
        .withColumn(
            "rejection_reason",
            F.when(F.col("scenario_id").isNull(), "Missing scenario ID")
            .when(F.col("created_at").isNull(), "Invalid timestamp")
            .when(F.col("school_id") <= 0, "Invalid school ID")
            .when(F.col("cip_code").isNull(), "Missing CIP code")
            .when(F.col("cbsa_code").isNull(), "Missing CBSA code")
            .when(F.col("reported_annual_salary") < 10000.00, "Salary below $10,000 threshold")
            .when(F.col("monthly_disposable") < -20000.00, "Disposable below -$20,000 floor")
            .otherwise("Unknown validation error")
        )
        .withColumn("quarantined_at", F.current_timestamp())
    )

    return valid_df, quarantine_df


def merge_scenarios_into_silver(
    spark: SparkSession,
    valid_df: DataFrame,
    target_table: str = "gradcast_lakehouse.silver.scenarios",
) -> None:
    """
    Idempotent Delta MERGE INTO on primary business key (scenario_id).
    """
    deduped_df = valid_df.dropDuplicates(["scenario_id"])
    deduped_df.createOrReplaceTempView("stage_valid_scenarios")

    spark.sql(f"""
        MERGE INTO {target_table} AS target
        USING stage_valid_scenarios AS source
        ON target.scenario_id = source.scenario_id
        WHEN MATCHED THEN
            UPDATE SET
                target.created_at = source.created_at,
                target.school_id = source.school_id,
                target.cip_code = source.cip_code,
                target.cip_family = source.cip_family,
                target.credential_level = source.credential_level,
                target.cbsa_code = source.cbsa_code,
                target.housing_type = source.housing_type,
                target.salary_mode = source.salary_mode,
                target.reported_annual_salary = source.reported_annual_salary,
                target.monthly_net_take_home = source.monthly_net_take_home,
                target.monthly_rent = source.monthly_rent,
                target.monthly_loan_payment = source.monthly_loan_payment,
                target.monthly_disposable = source.monthly_disposable,
                target.disposable_ratio = source.disposable_ratio,
                target.sentiment_tag = source.sentiment_tag,
                target.is_valid_positive_find = source.is_valid_positive_find,
                target._updated_at = source._updated_at
        WHEN NOT MATCHED THEN
            INSERT (
                scenario_id, created_at, school_id, cip_code, cip_family,
                credential_level, cbsa_code, housing_type, salary_mode,
                reported_annual_salary, monthly_net_take_home, monthly_rent,
                monthly_loan_payment, monthly_disposable, disposable_ratio,
                sentiment_tag, is_valid_positive_find, _updated_at
            )
            VALUES (
                source.scenario_id, source.created_at, source.school_id, source.cip_code, source.cip_family,
                source.credential_level, source.cbsa_code, source.housing_type, source.salary_mode,
                source.reported_annual_salary, source.monthly_net_take_home, source.monthly_rent,
                source.monthly_loan_payment, source.monthly_disposable, source.disposable_ratio,
                source.sentiment_tag, source.is_valid_positive_find, source._updated_at
            )
    """)


if __name__ == "__main__":
    spark = SparkSession.builder.appName("SilverTransform").getOrCreate()
    print("Silver Transform module loaded successfully.")
