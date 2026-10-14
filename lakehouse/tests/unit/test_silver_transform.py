"""
Unit tests for Silver transformation logic and Liquid Clustering derivation.
Compatible with both pytest and python -m unittest.
"""

import json
import unittest


def extract_cip_family(cip_code: str) -> str:
    """Helper mirroring PySpark derivation substring(regexp_replace(cip_code, '\\.', ''), 1, 2)."""
    cleaned = cip_code.replace(".", "").strip()
    return cleaned[:2]


def validate_scenario_record(record: dict) -> tuple[bool, str]:
    """Pure-logic validator mirroring Silver quarantine rules."""
    if not record.get("id"):
        return False, "Missing scenario ID"
    if not record.get("created_at"):
        return False, "Invalid timestamp"
    if record.get("school_id") is None or record.get("school_id", 0) <= 0:
        return False, "Invalid school ID"
    if not record.get("cip_code"):
        return False, "Missing CIP code"
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


if __name__ == "__main__":
    unittest.main()
