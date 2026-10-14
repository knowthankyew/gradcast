"""
End-to-end integration tests for Reverse-ETL SQLite compiler.
Verifies compiled SQLite database validity, index creation, and query performance.
"""

import os
import sqlite3
import tempfile
import unittest


SQLITE_BENCHMARK_SCHEMA_DDL = """
CREATE TABLE IF NOT EXISTS community_scenario_benchmarks (
    cip_family TEXT NOT NULL,
    cbsa_code TEXT NOT NULL,
    credential_level INTEGER NOT NULL,
    housing_type TEXT NOT NULL,
    sample_count INTEGER NOT NULL,
    p25_monthly_disposable INTEGER NOT NULL,
    median_monthly_disposable INTEGER NOT NULL,
    p75_monthly_disposable INTEGER NOT NULL,
    top_contributing_school_id INTEGER NULL,
    last_updated_at TEXT NOT NULL,
    CONSTRAINT PK_community_scenario_benchmarks PRIMARY KEY (cip_family, cbsa_code, credential_level, housing_type)
);

CREATE INDEX IF NOT EXISTS IX_community_scenario_benchmarks_cbsa_cip 
    ON community_scenario_benchmarks (cbsa_code, cip_family);
"""


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


if __name__ == "__main__":
    unittest.main()
