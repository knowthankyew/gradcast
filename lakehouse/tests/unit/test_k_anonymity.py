"""
Unit tests for k-anonymity privacy boundary (k >= 5) in Gold reporting.
"""

import unittest
from typing import Dict, List


def filter_k_anonymity_cohorts(cohorts: List[Dict], min_k: int = 5) -> List[Dict]:
    """Pure-logic filter mirroring HAVING COUNT(*) >= 5 in v_public_positive_finds."""
    return [c for c in cohorts if c.get("sample_count", 0) >= min_k]


class TestKAnonymity(unittest.TestCase):
    def test_cohorts_under_five_are_suppressed(self):
        cohorts = [
            {"cbsa_code": "41860", "cip_family": "11", "sample_count": 12},  # Pass
            {"cbsa_code": "41860", "cip_family": "14", "sample_count": 5},   # Boundary pass
            {"cbsa_code": "31080", "cip_family": "11", "sample_count": 4},   # Suppressed
            {"cbsa_code": "12060", "cip_family": "52", "sample_count": 1},   # Suppressed
        ]

        result = filter_k_anonymity_cohorts(cohorts, min_k=5)
        self.assertEqual(len(result), 2)
        self.assertEqual(result[0]["sample_count"], 12)
        self.assertEqual(result[1]["sample_count"], 5)

        # Verify no suppressed cohorts leak through
        cbsas_leaked = [c["cbsa_code"] for c in result]
        self.assertNotIn("31080", cbsas_leaked)
        self.assertNotIn("12060", cbsas_leaked)


if __name__ == "__main__":
    unittest.main()
