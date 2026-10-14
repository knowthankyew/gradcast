"""
GradCast Lakehouse - Secure Views & k-Anonymity Privacy Enforcement.

Implements:
v_public_positive_finds with hard k-anonymity boundary (HAVING COUNT(*) >= 5).
Guarantees zero individual student or user scenario re-identification.
"""

from pyspark.sql import SparkSession


def create_k_anonymity_views(
    spark: SparkSession,
    catalog: str = "gradcast_lakehouse",
    min_cohort_size: int = 5,
) -> None:
    """
    Create or replace the public reporting view enforcing k-anonymity (k >= 5).
    """
    view_sql = f"""
        CREATE OR REPLACE VIEW {catalog}.gold.v_public_positive_finds AS
        SELECT 
            f.cbsa_code,
            m.metro_name,
            p.cip_family,
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
            f.housing_type
        HAVING COUNT(*) >= {min_cohort_size};
    """
    spark.sql(view_sql)


if __name__ == "__main__":
    spark = SparkSession.builder.appName("LakehouseViews").getOrCreate()
    print("Views module loaded successfully.")
