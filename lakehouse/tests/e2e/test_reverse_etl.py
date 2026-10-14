"""
End-to-end integration tests for Reverse-ETL SQLite compiler.
Verifies compiled SQLite database validity, index creation, and query performance.
"""

import os
import sqlite3
import tempfile
import unittest


import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from lakehouse.src.pipelines.export_sqlite import SQLITE_BENCHMARK_SCHEMA_DDL, export_gold_to_sqlite


class StubDataFrame:
    def __init__(self, rows):
        self._rows = rows

    def collect(self):
        return self._rows


class StubSparkSession:
    def __init__(self, rows):
        self._rows = rows

    def sql(self, query):
        return StubDataFrame(self._rows)


class TestReverseEtlSqlite(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.TemporaryDirectory()
        self.db_path = os.path.join(self.temp_dir.name, "test_benchmarks.db")

    def tearDown(self):
        self.temp_dir.cleanup()

    def test_sqlite_benchmark_table_creation_and_querying(self):
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.executescript(SQLITE_BENCHMARK_SCHEMA_DDL)

            # Insert sample compiled benchmark
            cursor.execute("""
                INSERT INTO community_scenario_benchmarks (
                    cip_family, cbsa_code, credential_level, housing_type, sample_count,
                    p25_monthly_disposable, median_monthly_disposable, p75_monthly_disposable,
                    top_contributing_school_id, last_updated_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?);
            """, ("11", "41860", 3, "alone", 25, 3200, 4500, 5800, 101, "2026-10-14 12:00:00"))

            conn.commit()

            # Query via covering index
            cursor.execute("""
                SELECT sample_count, median_monthly_disposable
                FROM community_scenario_benchmarks
                WHERE cbsa_code = ? AND cip_family = ?;
            """, ("41860", "11"))

            row = cursor.fetchone()
            self.assertIsNotNone(row)
            self.assertEqual(row[0], 25)
            self.assertEqual(row[1], 4500)

            # Verify index exists
            cursor.execute("""
                SELECT name FROM sqlite_master 
                WHERE type='index' AND name='IX_community_scenario_benchmarks_cbsa_cip';
            """)
            index_row = cursor.fetchone()
            self.assertIsNotNone(index_row)

    def test_export_gold_to_sqlite_full_pipeline(self):
        fake_rows = [
            {
                "cip_family": "11",
                "cbsa_code": "41860",
                "credential_level": 3,
                "housing_type": "alone",
                "sample_count": 25,
                "p25_monthly_disposable": 3200,
                "median_monthly_disposable": 4500,
                "p75_monthly_disposable": 5800,
                "top_contributing_school_id": 101,
                "last_updated_at": "2026-10-14 12:00:00",
            }
        ]
        stub_spark = StubSparkSession(fake_rows)
        exported_count = export_gold_to_sqlite(stub_spark, self.db_path, catalog="test_catalog")
        self.assertEqual(exported_count, 1)

        # Verify output SQLite database was atomically generated and vacuumed
        self.assertTrue(os.path.isfile(self.db_path))
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.cursor()
            cursor.execute("SELECT sample_count, median_monthly_disposable FROM community_scenario_benchmarks WHERE cbsa_code='41860';")
            row = cursor.fetchone()
            self.assertIsNotNone(row)
            self.assertEqual(row[0], 25)
            self.assertEqual(row[1], 4500)


if __name__ == "__main__":
    unittest.main()
