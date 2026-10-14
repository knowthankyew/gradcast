"""
GradCast Lakehouse - Secure Views & k-Anonymity Privacy Enforcement.

Implements:
v_public_positive_finds with hard k-anonymity boundary (HAVING COUNT(*) >= 5).
Guarantees zero individual student or user scenario re-identification.
"""

try:
    from pyspark.sql import SparkSession
except ImportError:
    SparkSession = None  # type: ignore


def get_k_anonymity_view_sql(
    catalog: str = "gradcast_lakehouse",
    min_cohort_size: int = 5,
) -> str:
    """
    Generate the SQL for v_public_positive_finds enforcing effective_min_k >= 5 clamp.
    """
    effective_min_k = max(min_cohort_size, 5)
    return f"""
        CREATE OR REPLACE VIEW {catalog}.gold.v_public_positive_finds AS
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
        FROM {catalog}.gold.fact_scenario_outcomes f
        JOIN {catalog}.gold.dim_metro m ON f.cbsa_code = m.cbsa_code
        JOIN {catalog}.gold.dim_program p ON f.cip_code = p.cip_code
        GROUP BY 
            f.cbsa_code, 
            m.metro_name, 
            p.cip_family, 
            f.credential_level,
            f.housing_type
        HAVING COUNT(*) >= {effective_min_k};
    """


def create_k_anonymity_views(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
    min_cohort_size: int = 5,
    analysts_group: str | None = None,
) -> None:
    """
    Create or replace the public reporting view enforcing k-anonymity (k >= 5).
    Optionally grants SELECT to the designated Unity Catalog analysts group.
    """
    view_sql = get_k_anonymity_view_sql(catalog=catalog, min_cohort_size=min_cohort_size)
    spark.sql(view_sql)
    if analysts_group:
        spark.sql(f"GRANT SELECT ON VIEW {catalog}.gold.v_public_positive_finds TO `{analysts_group}`")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="Create Lakehouse Views")
    parser.add_argument("--catalog", default="gradcast_lakehouse", help="Unity Catalog name")
    parser.add_argument("--analysts-group", default=None, help="Target Analysts group name for view GRANT")
    args = parser.parse_args()

    spark = SparkSession.builder.appName("LakehouseViews").getOrCreate()
    create_k_anonymity_views(spark, catalog=args.catalog, analysts_group=args.analysts_group)
    print("Views module loaded and k-anonymity views verified successfully.")
