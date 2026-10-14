"""
GradCast Lakehouse - Continuous Monitoring & Drift Detection.

Tracks:
1. Ingestion volume & quarantine rates.
2. Distribution drift in reported annual salary and monthly disposable.
3. Emits OpenTelemetry metrics conforming strictly to knowthankyew safe allowlist keys.
"""

from typing import Any, Dict
from pyspark.sql import DataFrame
from pyspark.sql import functions as F


# Telemetry safe allowlist keys - zero PII, zero raw query text, zero secrets
SAFE_TELEMETRY_ALLOWLIST = {
    "pipeline_name",
    "table_name",
    "batch_id",
    "total_records",
    "quarantined_records",
    "quarantine_rate",
    "median_salary",
    "median_disposable",
    "drift_detected",
    "duration_ms",
}


def compute_batch_health_metrics(
    valid_df: DataFrame,
    quarantine_df: DataFrame,
    pipeline_name: str = "gradcast_medallion",
    table_name: str = "silver.scenarios",
    batch_id: str = "batch-latest",
    baseline_median_salary: float = 65000.0,
    baseline_median_disposable: float = 1500.0,
) -> Dict[str, Any]:
    """
    Compute structured health metrics for an ingestion batch without persisting PII.
    Evaluates distribution drift against reference baselines independently of quarantine rate.
    """
    valid_count = valid_df.count()
    quarantine_count = quarantine_df.count()
    total_records = valid_count + quarantine_count

    quarantine_rate = float(quarantine_count) / float(total_records) if total_records > 0 else 0.0

    salary_stats = valid_df.select(
        F.percentile_approx("reported_annual_salary", 0.5).alias("median_salary"),
        F.percentile_approx("monthly_disposable", 0.5).alias("median_disposable"),
    ).first()

    median_salary = float(salary_stats["median_salary"]) if salary_stats and salary_stats["median_salary"] is not None else 0.0
    median_disposable = float(salary_stats["median_disposable"]) if salary_stats and salary_stats["median_disposable"] is not None else 0.0

    # Independent distribution drift: flag if median salary or disposable deviates > 25% from baseline distribution,
    # or if quarantine rate exceeds 5% quality threshold.
    salary_drift = False
    if baseline_median_salary > 0 and median_salary > 0:
        salary_drift = abs(median_salary - baseline_median_salary) / baseline_median_salary > 0.25

    disposable_drift = False
    if baseline_median_disposable != 0 and median_disposable != 0:
        disposable_drift = abs(median_disposable - baseline_median_disposable) / abs(baseline_median_disposable) > 0.25

    drift_detected = salary_drift or disposable_drift or (quarantine_rate > 0.05)

    raw_metrics = {
        "pipeline_name": pipeline_name,
        "table_name": table_name,
        "batch_id": batch_id,
        "total_records": total_records,
        "quarantined_records": quarantine_count,
        "quarantine_rate": round(quarantine_rate, 4),
        "median_salary": round(median_salary, 2),
        "median_disposable": round(median_disposable, 2),
        "drift_detected": drift_detected,
        "duration_ms": 0,
    }

    # Strict allowlist scrubbing
    return {k: v for k, v in raw_metrics.items() if k in SAFE_TELEMETRY_ALLOWLIST}
