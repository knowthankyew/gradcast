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

from typing import Optional, Tuple

try:
    from pyspark.sql import DataFrame, SparkSession
    from pyspark.sql import functions as F
    from pyspark.sql.window import Window
    from pyspark.sql.types import (
        DoubleType,
        IntegerType,
        StringType,
        StructField,
        StructType,
        TimestampType,
    )
except ImportError:
    DataFrame = None  # type: ignore
    SparkSession = None  # type: ignore
    F = None  # type: ignore
    Window = None  # type: ignore
    class _MockType:
        def __init__(self, *args, **kwargs):
            if args and isinstance(args[0], list):
                self.fields = args[0]
            elif args and isinstance(args[0], str):
                self.name = args[0]
            else:
                self.fields = []
                self.name = None
    DoubleType = IntegerType = StringType = StructField = StructType = TimestampType = _MockType  # type: ignore

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
            _updated_at TIMESTAMP NOT NULL,
            CONSTRAINT chk_positive_salary CHECK (reported_annual_salary >= 10000.00),
            CONSTRAINT chk_disposable_floor CHECK (monthly_disposable >= -20000.00)
        )
        USING DELTA
        CLUSTER BY (cip_family, cbsa_code)
        TBLPROPERTIES (
            'delta.enableChangeDataFeed' = 'true'
        )
    """)


def parse_and_cleanse_scenarios(raw_df: DataFrame) -> tuple[DataFrame, DataFrame]:
    """
    Parse raw bronze payload, classify records into valid vs quarantined.
    Returns: (valid_df, quarantine_df)
    """
    ingest_ts_col = (
        F.col("ingest_timestamp")
        if hasattr(raw_df, "columns") and "ingest_timestamp" in raw_df.columns
        else F.current_timestamp()
    )

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
            ingest_ts_col.alias("ingest_timestamp"),
            F.current_timestamp().alias("_updated_at"),
            F.col("raw_payload"),
        )
    )

    # Comprehensive validation rules checking every NOT NULL column in target schema
    is_valid_cond = (
        F.col("scenario_id").isNotNull()
        & F.col("created_at").isNotNull()
        & F.col("school_id").isNotNull()
        & (F.col("school_id") > 0)
        & F.col("cip_code").isNotNull()
        & F.col("credential_level").isNotNull()
        & F.col("cbsa_code").isNotNull()
        & F.col("housing_type").isNotNull()
        & F.col("salary_mode").isNotNull()
        & F.col("reported_annual_salary").isNotNull()
        & (F.col("reported_annual_salary") >= 10000.00)
        & F.col("monthly_net_take_home").isNotNull()
        & F.col("monthly_rent").isNotNull()
        & F.col("monthly_loan_payment").isNotNull()
        & F.col("monthly_disposable").isNotNull()
        & (F.col("monthly_disposable") >= -20000.00)
        & F.col("disposable_ratio").isNotNull()
        & F.col("sentiment_tag").isNotNull()
    )
    is_valid_cond = F.coalesce(is_valid_cond, F.lit(False))

    valid_df = parsed_df.filter(is_valid_cond).drop("raw_payload")

    quarantine_df = (
        parsed_df
        .filter(~is_valid_cond)
        .drop("raw_payload")
        .withColumn(
            "rejection_reason",
            F.when(F.col("scenario_id").isNull(), "Missing scenario ID")
            .when(F.col("created_at").isNull(), "Invalid timestamp")
            .when(F.col("school_id").isNull() | (F.col("school_id") <= 0), "Invalid school ID")
            .when(F.col("cip_code").isNull(), "Missing CIP code")
            .when(F.col("credential_level").isNull(), "Missing credential level")
            .when(F.col("cbsa_code").isNull(), "Missing CBSA code")
            .when(F.col("housing_type").isNull(), "Missing housing type")
            .when(F.col("salary_mode").isNull(), "Missing salary mode")
            .when(F.col("reported_annual_salary").isNull(), "Missing reported annual salary")
            .when(F.col("reported_annual_salary") < 10000.00, "Salary below $10,000 threshold")
            .when(F.col("monthly_net_take_home").isNull(), "Missing monthly net take-home")
            .when(F.col("monthly_rent").isNull(), "Missing monthly rent")
            .when(F.col("monthly_loan_payment").isNull(), "Missing monthly loan payment")
            .when(F.col("monthly_disposable").isNull(), "Missing monthly disposable")
            .when(F.col("monthly_disposable") < -20000.00, "Disposable below -$20,000 floor")
            .when(F.col("disposable_ratio").isNull(), "Missing disposable ratio")
            .when(F.col("sentiment_tag").isNull(), "Missing sentiment tag")
            .otherwise("Unknown validation error")
        )
        .withColumn("quarantined_at", F.current_timestamp())
    )

    return valid_df, quarantine_df


def persist_quarantine_scenarios(
    spark: SparkSession,
    quarantine_df: DataFrame,
    target_table: str = "gradcast_lakehouse.quarantine.scenarios_rejected",
) -> None:
    """
    Persist quarantined scenarios into a dedicated audit table without failing the pipeline.
    Strips raw payload and client identifiers to preserve zero-PII cloud invariants.
    """
    spark.sql(f"""
        CREATE TABLE IF NOT EXISTS {target_table} (
            scenario_id STRING,
            created_at TIMESTAMP,
            school_id INT,
            cip_code STRING,
            cip_family STRING,
            credential_level INT,
            cbsa_code STRING,
            housing_type STRING,
            salary_mode STRING,
            reported_annual_salary DECIMAL(12, 2),
            monthly_net_take_home DECIMAL(10, 2),
            monthly_rent DECIMAL(10, 2),
            monthly_loan_payment DECIMAL(10, 2),
            monthly_disposable DECIMAL(10, 2),
            disposable_ratio DOUBLE,
            sentiment_tag STRING,
            rejection_reason STRING NOT NULL,
            quarantined_at TIMESTAMP NOT NULL
        )
        USING DELTA
        TBLPROPERTIES ('delta.appendOnly' = 'true')
    """)

    clean_cols = [
        "scenario_id", "created_at", "school_id", "cip_code", "cip_family",
        "credential_level", "cbsa_code", "housing_type", "salary_mode",
        "reported_annual_salary", "monthly_net_take_home", "monthly_rent",
        "monthly_loan_payment", "monthly_disposable", "disposable_ratio",
        "sentiment_tag", "rejection_reason", "quarantined_at"
    ]
    to_persist = quarantine_df.select(*[F.col(c) for c in clean_cols if c in quarantine_df.columns])

    (
        to_persist.write
        .format("delta")
        .mode("append")
        .saveAsTable(target_table)
    )


def merge_scenarios_into_silver(
    spark: SparkSession,
    valid_df: DataFrame,
    target_table: str = "gradcast_lakehouse.silver.scenarios",
) -> None:
    """
    Idempotent Delta MERGE INTO on primary business key (scenario_id).
    Enforces deterministic deduplication windowing on newest ingest_timestamp and _updated_at.
    """
    try:
        if Window is not None:
            order_cols = []
            if hasattr(valid_df, "columns") and "ingest_timestamp" in valid_df.columns:
                order_cols.append(F.col("ingest_timestamp").desc())
            order_cols.append(F.col("_updated_at").desc())
            window_spec = Window.partitionBy("scenario_id").orderBy(*order_cols)
            deduped_df = (
                valid_df
                .withColumn("_row_num", F.row_number().over(window_spec))
                .filter(F.col("_row_num") == 1)
                .drop("_row_num")
            )
        else:
            deduped_df = valid_df.dropDuplicates(["scenario_id"])
    except Exception:
        deduped_df = valid_df.dropDuplicates(["scenario_id"])

    deduped_df.createOrReplaceTempView("stage_valid_scenarios")

    spark.sql(f"""
        MERGE INTO {target_table} AS target
        USING stage_valid_scenarios AS source
        ON target.scenario_id = source.scenario_id
        WHEN MATCHED AND source._updated_at >= target._updated_at THEN
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


def run_silver_pipeline(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
    source_table: Optional[str] = None,
    target_table: Optional[str] = None,
    quarantine_table: Optional[str] = None,
) -> None:
    """Execute complete Silver Medallion ingestion, quarantine, and conformed MERGE."""
    src = source_table or f"{catalog}.bronze.shared_scenarios"
    tgt = target_table or f"{catalog}.silver.scenarios"
    quar = quarantine_table or f"{catalog}.quarantine.scenarios_rejected"

    ensure_silver_scenarios_table(spark, table_name=tgt)
    raw_df = spark.table(src)
    valid_df, quarantine_df = parse_and_cleanse_scenarios(raw_df)
    persist_quarantine_scenarios(spark, quarantine_df, target_table=quar)
    merge_scenarios_into_silver(spark, valid_df, target_table=tgt)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Run Silver Transform Pipeline")
    parser.add_argument("--catalog", default="gradcast_lakehouse", help="Unity Catalog name")
    args = parser.parse_args()

    spark = SparkSession.builder.appName("SilverTransform").getOrCreate()
    run_silver_pipeline(spark, catalog=args.catalog)
    print("Silver Transform pipeline executed successfully.")
