"""
Unit tests for Silver transformation logic and Liquid Clustering derivation.
Compatible with both pytest and python -m unittest.
"""

import json
import sys
from pathlib import Path
import unittest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from lakehouse.src.pipelines.silver_transform import (
    DataFrame,
    SCENARIO_JSON_SCHEMA,
    parse_and_cleanse_scenarios,
    persist_quarantine_scenarios,
    run_silver_pipeline,
)


def extract_cip_family(cip_code: str) -> str:
    """Helper mirroring PySpark derivation substring(regexp_replace(cip_code, '\\.', ''), 1, 2)."""
    cleaned = cip_code.replace(".", "").strip()
    return cleaned[:2]


def validate_scenario_record(record: dict) -> tuple[bool, str]:
    """Pure-logic validator mirroring Silver quarantine rules."""
    import re
    if not record.get("id"):
        return False, "Missing scenario ID"
    if not record.get("created_at"):
        return False, "Invalid timestamp"
    if record.get("school_id") is None or record.get("school_id", 0) <= 0:
        return False, "Invalid school ID"
    if not record.get("cip_code") or not re.match(r"^\d{2}(\.\d{2,4})?$", str(record.get("cip_code"))):
        return False, "Invalid or unparseable CIP code"
    if not record.get("cbsa_code"):
        return False, "Missing CBSA code"
    if record.get("reported_annual_salary") is None:
        return False, "Missing reported annual salary"
    if record.get("reported_annual_salary", 0) < 10000.00:
        return False, "Salary below $10,000 threshold"
    if record.get("monthly_disposable") is None:
        return False, "Missing monthly disposable"
    if record.get("monthly_disposable", 0) < -20000.00:
        return False, "Disposable below -$20,000 floor"
    return True, "Valid"


class TestSilverTransform(unittest.TestCase):
    def test_cip_family_derivation(self):
        self.assertEqual(extract_cip_family("11.0701"), "11")
        self.assertEqual(extract_cip_family("1107"), "11")
        self.assertEqual(extract_cip_family("14.0101"), "14")
        self.assertEqual(extract_cip_family("52.0201"), "52")

    def test_valid_scenario_passes_quarantine(self):
        record = {
            "id": "scen-test-01",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "11.0701",
            "cbsa_code": "41860",
            "reported_annual_salary": 95000.00,
            "monthly_disposable": 3500.00,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertTrue(is_valid)
        self.assertEqual(reason, "Valid")

    def test_low_salary_quarantined(self):
        record = {
            "id": "scen-test-02",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "11.0701",
            "cbsa_code": "41860",
            "reported_annual_salary": 5000.00,
            "monthly_disposable": 500.00,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertFalse(is_valid)
        self.assertEqual(reason, "Salary below $10,000 threshold")

    def test_severe_negative_disposable_quarantined(self):
        record = {
            "id": "scen-test-03",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "11.0701",
            "cbsa_code": "41860",
            "reported_annual_salary": 30000.00,
            "monthly_disposable": -25000.00,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertFalse(is_valid)
        self.assertEqual(reason, "Disposable below -$20,000 floor")

    def test_null_salary_quarantined(self):
        record = {
            "id": "scen-test-04",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "11.0701",
            "cbsa_code": "41860",
            "reported_annual_salary": None,
            "monthly_disposable": 1500.00,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertFalse(is_valid)
        self.assertEqual(reason, "Missing reported annual salary")

    def test_null_disposable_quarantined(self):
        record = {
            "id": "scen-test-05",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "11.0701",
            "cbsa_code": "41860",
            "reported_annual_salary": 50000.00,
            "monthly_disposable": None,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertFalse(is_valid)
        self.assertEqual(reason, "Missing monthly disposable")

    def test_malformed_cip_quarantined(self):
        record = {
            "id": "scen-test-06",
            "created_at": "2026-10-14T12:00:00Z",
            "school_id": 101,
            "cip_code": "x",
            "cbsa_code": "41860",
            "reported_annual_salary": 95000.00,
            "monthly_disposable": 3500.00,
        }
        is_valid, reason = validate_scenario_record(record)
        self.assertFalse(is_valid)
        self.assertEqual(reason, "Invalid or unparseable CIP code")

    def test_silver_pipeline_module_and_schema_integrity(self):
        self.assertIsNotNone(SCENARIO_JSON_SCHEMA)
        self.assertTrue(callable(run_silver_pipeline))
        self.assertTrue(callable(parse_and_cleanse_scenarios))
        self.assertTrue(callable(persist_quarantine_scenarios))

    def test_scenario_json_schema_fields(self):
        """Verify that SCENARIO_JSON_SCHEMA specifies all expected operational ingest fields."""
        expected_fields = {
            "id", "created_at", "school_id", "cip_code", "credential_level",
            "cbsa_code", "housing_type", "salary_mode", "reported_annual_salary",
            "monthly_net_take_home", "monthly_rent", "monthly_loan_payment",
            "monthly_disposable", "disposable_ratio", "sentiment_tag",
            "scenario_title", "client_hash",
        }
        actual_fields = {f.name for f in SCENARIO_JSON_SCHEMA.fields}
        self.assertEqual(actual_fields, expected_fields)

    @unittest.skipIf(DataFrame is None, "PySpark not installed in local environment")
    def test_parse_and_cleanse_scenarios_pyspark(self):
        """Verify parse_and_cleanse_scenarios with real SparkSession when PySpark is available."""
        from pyspark.sql import SparkSession
        spark = SparkSession.builder.master("local[1]").appName("SilverTransformTest").getOrCreate()
        try:
            test_data = [
                # 1. Valid record
                ('{"id": "valid-1", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "11.0701", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": 95000.00, "monthly_net_take_home": 5800.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": 3550.00, "disposable_ratio": 0.61, "sentiment_tag": "comfortable", "scenario_title": "CS Grad", "client_hash": "hash1"}',),
                # 2. Low salary record
                ('{"id": "invalid-low-salary", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "11.0701", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": 5000.00, "monthly_net_take_home": 500.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": -1750.00, "disposable_ratio": -3.5, "sentiment_tag": "stressed", "scenario_title": "Low Wage", "client_hash": "hash2"}',),
                # 3. Null salary record
                ('{"id": "invalid-null-salary", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "11.0701", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": null, "monthly_net_take_home": 5800.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": 3550.00, "disposable_ratio": 0.61, "sentiment_tag": "comfortable", "scenario_title": "No Salary", "client_hash": "hash3"}',),
                # 4. Null disposable record
                ('{"id": "invalid-null-disp", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "11.0701", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": 75000.00, "monthly_net_take_home": 4500.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": null, "disposable_ratio": null, "sentiment_tag": "comfortable", "scenario_title": "No Disp", "client_hash": "hash4"}',),
                # 5. Severe negative disposable floor record
                ('{"id": "invalid-neg-floor", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "11.0701", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": 30000.00, "monthly_net_take_home": 2000.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": -25000.00, "disposable_ratio": -12.5, "sentiment_tag": "stressed", "scenario_title": "Extreme Debt", "client_hash": "hash5"}',),
                # 6. Malformed CIP code
                ('{"id": "invalid-malformed-cip", "created_at": "2026-10-14T12:00:00Z", "school_id": 101, "cip_code": "x", "credential_level": 3, "cbsa_code": "41860", "housing_type": "alone", "salary_mode": "scorecard_median", "reported_annual_salary": 70000.00, "monthly_net_take_home": 4500.00, "monthly_rent": 1800.00, "monthly_loan_payment": 450.00, "monthly_disposable": 2250.00, "disposable_ratio": 0.5, "sentiment_tag": "comfortable", "scenario_title": "Bad CIP", "client_hash": "hash6"}',),
                # 7. Unparseable non-JSON record (null scenario_id)
                ('not valid json payload at all',)
            ]
            raw_df = spark.createDataFrame(test_data, ["raw_payload"])
            valid_df, quarantine_df = parse_and_cleanse_scenarios(raw_df)
            self.assertEqual(valid_df.count(), 1)
            self.assertEqual(quarantine_df.count(), 6)
            valid_row = valid_df.collect()[0]
            self.assertEqual(valid_row.scenario_id, "valid-1")
            self.assertEqual(valid_row.cip_family, "11")

            quarantined_rows = quarantine_df.collect()
            reasons = {r.scenario_id: r.rejection_reason for r in quarantined_rows if r.scenario_id is not None}
            self.assertIn("Salary below $10,000 threshold", reasons["invalid-low-salary"])
            self.assertIn("Missing reported annual salary", reasons["invalid-null-salary"])
            self.assertIn("Missing monthly disposable", reasons["invalid-null-disp"])
            self.assertIn("Disposable below -$20,000 floor", reasons["invalid-neg-floor"])
            self.assertIn("Invalid or unparseable CIP code", reasons["invalid-malformed-cip"])

            # Verify unparseable record has valid surrogate quarantine_id and preserved raw_payload
            corrupt_row = next(r for r in quarantined_rows if r.scenario_id is None)
            self.assertIsNotNone(corrupt_row.quarantine_id)
            self.assertEqual(len(corrupt_row.quarantine_id), 64)  # SHA-256 hash length
            self.assertEqual(corrupt_row.raw_payload, "not valid json payload at all")
            self.assertEqual(corrupt_row.rejection_reason, "Missing scenario ID")
        finally:
            spark.stop()


if __name__ == "__main__":
    unittest.main()
