"""
GradCast Reverse-ETL Bridge: Databricks Gold to Edge SQLite Compiler.

Exports aggregated, k-anonymized (k >= 5) benchmarks from Gold views
into a compressed, edge-serving SQLite 3 database file containing
table 'community_scenario_benchmarks'.
"""

import argparse
import os
import sqlite3
from typing import Optional
from pyspark.sql import DataFrame, SparkSession


SQLITE_BENCHMARK_SCHEMA_DDL = """
CREATE TABLE IF NOT EXISTS community_scenario_benchmarks (
    cip_family TEXT NOT NULL,
    cbsa_code TEXT NOT NULL,
    credential_level INTEGER NOT NULL,
    sample_count INTEGER NOT NULL,
    p25_monthly_disposable INTEGER NOT NULL,
    median_monthly_disposable INTEGER NOT NULL,
    p75_monthly_disposable INTEGER NOT NULL,
    top_contributing_school_id INTEGER NULL,
    last_updated_at TEXT NOT NULL,
    CONSTRAINT PK_community_scenario_benchmarks PRIMARY KEY (cip_family, cbsa_code, credential_level)
);

CREATE INDEX IF NOT EXISTS IX_community_scenario_benchmarks_cbsa_cip 
    ON community_scenario_benchmarks (cbsa_code, cip_family);
"""


def export_gold_to_sqlite(
    spark: SparkSession,
    sqlite_db_path: str,
    catalog: str = "gradcast_lakehouse",
) -> int:
    """
    Read k-anonymized positive finds from Gold view and compile into SQLite edge database.
    Returns the number of rows exported.
    """
    query = f"""
        SELECT 
            cip_family,
            cbsa_code,
            3 AS credential_level,
            sample_count,
            CAST(p25_disposable AS INT) AS p25_monthly_disposable,
            CAST(median_disposable AS INT) AS median_monthly_disposable,
            CAST(p75_disposable AS INT) AS p75_monthly_disposable,
            CAST(NULL AS INT) AS top_contributing_school_id,
            DATE_FORMAT(CURRENT_TIMESTAMP(), 'yyyy-MM-dd HH:mm:ss') AS last_updated_at
        FROM {catalog}.gold.v_public_positive_finds
    """
    df: DataFrame = spark.sql(query)
    rows = df.collect()

    os.makedirs(os.path.dirname(os.path.abspath(sqlite_db_path)), exist_ok=True)

    with sqlite3.connect(sqlite_db_path) as conn:
        cursor = conn.cursor()
        cursor.executescript(SQLITE_BENCHMARK_SCHEMA_DDL)

        cursor.execute("DELETE FROM community_scenario_benchmarks;")

        insert_sql = """
            INSERT INTO community_scenario_benchmarks (
                cip_family, cbsa_code, credential_level, sample_count,
                p25_monthly_disposable, median_monthly_disposable, p75_monthly_disposable,
                top_contributing_school_id, last_updated_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?);
        """

        records = [
            (
                r["cip_family"],
                r["cbsa_code"],
                r["credential_level"],
                r["sample_count"],
                r["p25_monthly_disposable"],
                r["median_monthly_disposable"],
                r["p75_monthly_disposable"],
                r["top_contributing_school_id"],
                r["last_updated_at"],
            )
            for r in rows
        ]

        cursor.executemany(insert_sql, records)
        conn.commit()

        # Run vacuum for maximum edge read performance and minimum file size
        cursor.execute("VACUUM;")

    return len(records)


def main():
    parser = argparse.ArgumentParser(description="GradCast Reverse-ETL SQLite Compiler")
    parser.add_argument("--catalog", default="gradcast_lakehouse", help="Unity Catalog namespace")
    parser.add_argument("--output-path", required=True, help="Destination SQLite .db file path")
    args = parser.parse_args()

    spark = SparkSession.builder.appName("GradCastReverseETL").getOrCreate()
    count = export_gold_to_sqlite(spark, args.output_path, args.catalog)
    print(f"Successfully compiled {count} benchmark records into {args.output_path}")


if __name__ == "__main__":
    main()
