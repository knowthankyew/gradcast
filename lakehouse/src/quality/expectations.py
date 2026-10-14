"""
GradCast Lakehouse - Data Quality Expectations & Quarantine Engine.

Defines declarative data quality contracts:
- expect_or_fail: Invariants that halt the pipeline on structural corruption.
- expect_or_drop: Record-level invariants that filter corrupt records into quarantine.
"""

from typing import Dict, List, Tuple
from pyspark.sql import DataFrame
from pyspark.sql import functions as F


# Declarative expectations for Delta Live Tables (DLT)
DLT_SCENARIO_EXPECTATIONS: Dict[str, str] = {
    "valid_scenario_id": "scenario_id IS NOT NULL AND length(scenario_id) > 0",
    "valid_school_id": "school_id > 0",
    "valid_cip_code": "cip_code IS NOT NULL AND length(cip_code) >= 2",
    "valid_cbsa_code": "cbsa_code IS NOT NULL AND length(cbsa_code) >= 4",
    "valid_salary_bounds": "reported_annual_salary >= 0.00",
    "valid_disposable_floor": "monthly_disposable >= -20000.00",
}


def apply_quality_expectations(df: DataFrame) -> Tuple[DataFrame, DataFrame]:
    """
    Apply expectations locally or in batch PySpark jobs.
    Splits input into passed records and dropped quarantine records.
    """
    conditions = [F.expr(expr) for expr in DLT_SCENARIO_EXPECTATIONS.values()]
    all_passed_cond = conditions[0]
    for c in conditions[1:]:
        all_passed_cond = all_passed_cond & c

    all_passed_cond = F.coalesce(all_passed_cond, F.lit(False))

    passed_df = df.filter(all_passed_cond)

    quarantined_df = (
        df
        .filter(~all_passed_cond)
        .withColumn(
            "quarantine_reason",
            F.concat_ws(
                "; ",
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_scenario_id"]), F.lit(False)), F.lit("invalid_scenario_id")),
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_school_id"]), F.lit(False)), F.lit("invalid_school_id")),
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_cip_code"]), F.lit(False)), F.lit("invalid_cip_code")),
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_cbsa_code"]), F.lit(False)), F.lit("invalid_cbsa_code")),
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_salary_bounds"]), F.lit(False)), F.lit("negative_salary")),
                F.when(~F.coalesce(F.expr(DLT_SCENARIO_EXPECTATIONS["valid_disposable_floor"]), F.lit(False)), F.lit("disposable_below_floor")),
            )
        )
        .withColumn("quarantined_at", F.current_timestamp())
    )

    return passed_df, quarantined_df
