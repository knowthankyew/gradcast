import sys
from pathlib import Path
from typing import Dict, List
import unittest

REPO_ROOT = Path(__file__).resolve().parent.parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from lakehouse.src.pipelines.views import get_k_anonymity_view_sql


def filter_k_anonymity_cohorts(cohorts: List[Dict], min_k: int = 5) -> List[Dict]:
    """Pure-logic filter mirroring HAVING COUNT(*) >= 5 in v_public_positive_finds."""
    return [c for c in cohorts if c.get("sample_count", 0) >= min_k]


class TestKAnonymity(unittest.TestCase):
    def test_view_sql_enforces_hard_k_anonymity_boundary(self):
        # Default boundary
        sql = get_k_anonymity_view_sql(catalog="test_catalog", min_cohort_size=5)
        self.assertIn("HAVING COUNT(*) >= 5", sql)
        self.assertIn("JOIN test_catalog.gold.dim_metro", sql)
        self.assertIn("JOIN test_catalog.gold.dim_program", sql)

    def test_view_sql_clamps_sub_five_cohort_sizes_to_five(self):
        # Even if a caller attempts min_cohort_size=2, the effective clamp must enforce >= 5
        sql_clamped = get_k_anonymity_view_sql(catalog="test_catalog", min_cohort_size=2)
        self.assertIn("HAVING COUNT(*) >= 5", sql_clamped)
        self.assertNotIn("HAVING COUNT(*) >= 2", sql_clamped)

    def test_view_sql_permits_stricter_cohort_sizes(self):
        # Stricter privacy thresholds (e.g. k=10) should be honored
        sql_stricter = get_k_anonymity_view_sql(catalog="test_catalog", min_cohort_size=10)
        self.assertIn("HAVING COUNT(*) >= 10", sql_stricter)

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
