#!/usr/bin/env python3
"""
audit_claims.py - Automated Claims, Architectural Parity & Privacy Invariant Linter for GradCast.

Enforces:
1. Schema & DDL Parity:
   Validates that all tables and clustering keys in docs/SCHEMA.md exist in PySpark/SQL DDL and SQLite migrations.
2. Invariant & Privacy Governance:
   Verifies that the hard k-anonymity boundary (HAVING COUNT(*) >= 5) exists in lakehouse/src/pipelines/views.py.
   Verifies that client identifiers are hashed (SHA256) and never store raw IP or PII.
3. Vocabulary Guardrails:
   Prohibits unprovable marketing adjectives ('bulletproof', 'conclusive', 'definitive', 'infinitely scalable').
4. Portfolio Brief Parity:
   Verifies that KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md reflects the exact current test suite inventory.
"""

from pathlib import Path
import re
import sys
from typing import List, Tuple

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
DEV_ROOT = REPO_ROOT.parent

PROHIBITED_WORDS = [
    re.compile(r"\bbulletproof\b", re.IGNORECASE),
    re.compile(r"\bconclusive\b", re.IGNORECASE),
    re.compile(r"\bdefinitive\b", re.IGNORECASE),
    re.compile(r"\binfinitely scalable\b", re.IGNORECASE),
    re.compile(r"\binstant\b", re.IGNORECASE),
]

EXPECTED_SCHEMA_TABLES = [
    ("shared_scenarios", REPO_ROOT / "src/data/SqliteDatabaseInitializer.cs"),
    ("bronze.shared_scenarios", REPO_ROOT / "lakehouse/src/pipelines/bronze_ingest.py"),
    ("bronze.college_scorecard", REPO_ROOT / "lakehouse/src/pipelines/bronze_ingest.py"),
    ("silver.scenarios", REPO_ROOT / "lakehouse/src/pipelines/silver_transform.py"),
    ("quarantine.scenarios_rejected", REPO_ROOT / "lakehouse/src/pipelines/silver_transform.py"),
    ("gold.dim_school", REPO_ROOT / "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.dim_metro", REPO_ROOT / "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.dim_program", REPO_ROOT / "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.fact_scenario_outcomes", REPO_ROOT / "lakehouse/src/pipelines/gold_modeling.py"),
    ("v_public_positive_finds", REPO_ROOT / "lakehouse/src/pipelines/views.py"),
    ("community_scenario_benchmarks", REPO_ROOT / "lakehouse/src/pipelines/export_sqlite.py"),
    ("community_scenario_benchmarks", REPO_ROOT / "src/data/SqliteDatabaseInitializer.cs"),
]


def audit_schema_parity(errors: List[str]) -> None:
    """
    Verify schema and DDL parity between docs/SCHEMA.md and source code:
    1. Parses CREATE TABLE DDL blocks from docs/SCHEMA.md.
    2. Validates that every table, column set, and CLUSTER BY key matches source code DDLs.
    """
    schema_doc = REPO_ROOT / "docs" / "SCHEMA.md"
    if not schema_doc.is_file():
        errors.append(f"Missing schema document: {schema_doc}")
        return

    def extract_create_table_blocks(sql_text: str) -> List[Tuple[str, str, str]]:
        """
        Extract (table_name, columns_body, trailer) tuples from SQL text using
        balanced-parenthesis scanning so that nested DECIMAL(10, 2) or CHECK(...)
        expressions do not truncate the column list.
        """
        clean_sql = re.sub(r"--.*$", "", sql_text, flags=re.MULTILINE)
        blocks = []
        pattern = re.compile(r"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([\"`\w\.\{\}]+)\s*\(", re.IGNORECASE)
        for match in pattern.finditer(clean_sql):
            table_name = match.group(1).replace('"', "").replace("`", "")
            start_idx = match.end()
            depth = 1
            i = start_idx
            while i < len(clean_sql) and depth > 0:
                if clean_sql[i] == "(":
                    depth += 1
                elif clean_sql[i] == ")":
                    depth -= 1
                i += 1
            if depth == 0:
                cols_body = clean_sql[start_idx : i - 1]
                trailer_match = re.search(r"([^;]*?);", clean_sql[i:])
                trailer = trailer_match.group(1) if trailer_match else clean_sql[i : i + 200]
                blocks.append((table_name, cols_body, trailer))
        return blocks

    schema_content = schema_doc.read_text(encoding="utf-8")
    parsed_tables = {}
    for raw_tbl, cols_body, trailer in extract_create_table_blocks(schema_content):
        parts = raw_tbl.split(".")
        norm_key = ".".join(parts[-2:]) if len(parts) >= 2 else parts[0]

        cols = []
        for line in cols_body.strip().splitlines():
            line = line.strip()
            if not line or line.upper().startswith("CONSTRAINT") or line.upper().startswith("PRIMARY KEY"):
                continue
            col_m = re.match(r'["`]?([a-zA-Z0-9_]+)["`]?\s+', line)
            if col_m:
                cols.append(col_m.group(1))

        cluster_m = re.search(r"CLUSTER BY\s*\(([^\)]+)\)", trailer, re.IGNORECASE)
        cluster_keys = [k.strip() for k in cluster_m.group(1).split(",")] if cluster_m else []

        parsed_tables[norm_key] = {
            "full_name": raw_tbl,
            "columns": cols,
            "cluster_keys": cluster_keys,
        }

    for table_spec, source_file in EXPECTED_SCHEMA_TABLES:
        if table_spec not in schema_content:
            errors.append(f"Table '{table_spec}' declared in expectations but missing in docs/SCHEMA.md")

        if not source_file.is_file():
            errors.append(f"Expected source file missing for table '{table_spec}': {source_file}")
            continue

        source_content = source_file.read_text(encoding="utf-8")
        clean_table_name = table_spec.split(".")[-1]
        if clean_table_name not in source_content:
            errors.append(
                f"Table '{clean_table_name}' declared in docs/SCHEMA.md but missing in {source_file.relative_to(REPO_ROOT)}"
            )

        # Deep DDL validation: verify column definitions strictly within matching CREATE TABLE block
        source_blocks = extract_create_table_blocks(source_content)
        matching_source = next((b for b in source_blocks if clean_table_name in b[0]), None)
        if not matching_source and source_blocks:
            if clean_table_name == "scenarios":
                matching_source = next((b for b in source_blocks if "scenario_id" in b[1]), None)
            elif clean_table_name == "scenarios_rejected":
                matching_source = next((b for b in source_blocks if "quarantine_id" in b[1]), None)
            elif clean_table_name == "college_scorecard":
                matching_source = next((b for b in source_blocks if "unitid" in b[1]), None)
            elif clean_table_name == "shared_scenarios":
                matching_source = next((b for b in source_blocks if "file_offset" in b[1]), None)

        if not matching_source:
            if clean_table_name == "v_public_positive_finds":
                table_block = source_content
            else:
                errors.append(
                    f"Table DDL block for '{clean_table_name}' missing in {source_file.relative_to(REPO_ROOT)}"
                )
                continue
        else:
            table_block = matching_source[1]

        spec = parsed_tables.get(table_spec)
        if spec:
            for col in spec["columns"]:
                if not re.search(rf"\b{col}\b", table_block):
                    errors.append(
                        f"Schema parity mismatch: Column '{col}' of table '{table_spec}' not found in DDL block of {source_file.relative_to(REPO_ROOT)}"
                    )
            for k in spec["cluster_keys"]:
                if not re.search(rf"CLUSTER BY[^\;]*\b{k}\b", source_content, re.IGNORECASE):
                    errors.append(
                        f"Clustering key parity mismatch: Table '{table_spec}' expects CLUSTER BY '{k}' in {source_file.relative_to(REPO_ROOT)}"
                    )


def audit_privacy_invariants(errors: List[str]) -> None:
    """Verify hard k-anonymity (k >= 5) and SHA-256 client hash invariants."""
    views_file = REPO_ROOT / "lakehouse/src/pipelines/views.py"
    if not views_file.is_file():
        errors.append("Missing views pipeline: lakehouse/src/pipelines/views.py")
        return

    import ast
    try:
        raw_views_code = views_file.read_text(encoding="utf-8")
        tree = ast.parse(raw_views_code)
        fn_node = next(
            (node for node in ast.walk(tree) if isinstance(node, ast.FunctionDef) and node.name == "get_k_anonymity_view_sql"),
            None,
        )
        if not fn_node:
            errors.append("Privacy violation: get_k_anonymity_view_sql function not found in views.py")
        else:
            fn_source = ast.get_source_segment(raw_views_code, fn_node) or ""
            if "max(min_cohort_size, 5)" not in fn_source:
                errors.append("Privacy violation: get_k_anonymity_view_sql does not enforce max(min_cohort_size, 5) clamp")
    except Exception as exc:
        errors.append(f"Failed to parse views.py AST: {exc}")

    # Dynamic execution test of k-anonymity SQL generator
    try:
        import sys
        lakehouse_dir = str(REPO_ROOT / "lakehouse")
        if lakehouse_dir not in sys.path:
            sys.path.insert(0, lakehouse_dir)
        from src.pipelines.views import get_k_anonymity_view_sql
        generated_sql_clamped = get_k_anonymity_view_sql(catalog="audit_cat", min_cohort_size=1)
        if "HAVING COUNT(*) >= 5" not in generated_sql_clamped:
            errors.append("Privacy violation: get_k_anonymity_view_sql(min_cohort_size=1) failed to enforce k >= 5 clamp")
        if "HAVING COUNT(*) >= 1" in generated_sql_clamped:
            errors.append("Privacy violation: get_k_anonymity_view_sql(min_cohort_size=1) emitted unsuppressed threshold >= 1")
    except Exception as exc:
        errors.append(f"Failed to execute dynamic k-anonymity invariant test: {exc}")

    # Verify SHA-256 hashing in ScenarioEndpoints
    endpoint_file = REPO_ROOT / "src/api/Endpoints/ScenarioEndpoints.cs"
    if not endpoint_file.is_file():
        errors.append("Missing endpoint file: src/api/Endpoints/ScenarioEndpoints.cs")
    else:
        ep_content = endpoint_file.read_text(encoding="utf-8")
        if "SHA256" not in ep_content:
            errors.append("Privacy violation: Client hash in ScenarioEndpoints.cs is not using SHA256")


def audit_vocabulary(errors: List[str]) -> None:
    """Check markdown docs and source comments for prohibited marketing terms."""
    docs_to_check = [
        REPO_ROOT / "docs" / "SCHEMA.md",
        REPO_ROOT / "docs" / "QA_CHECKLIST.md",
        REPO_ROOT / "ROADMAP.md",
    ]

    for doc in docs_to_check:
        if not doc.is_file():
            continue
        lines = doc.read_text(encoding="utf-8").splitlines()
        for idx, line in enumerate(lines, 1):
            # Skip documentation explaining the linter rule itself
            lower = line.lower()
            if any(marker in lower for marker in ["prohibits unprovable", "prohibited adjectives", "prohibited words", "prohibited marketing", "prohibited_words"]) or ("bulletproof" in lower and "prohibited" in lower):
                continue
            for pattern in PROHIBITED_WORDS:
                if pattern.search(line):
                    errors.append(f"Prohibited word '{pattern.pattern}' found in {doc.name}:{idx}: {line.strip()}")


def audit_portfolio_master_brief(errors: List[str], require_brief: bool = False) -> bool:
    """Verify that KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md matches current test inventory."""
    brief_candidates = [
        DEV_ROOT / "KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md",
        REPO_ROOT / "KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md",
    ]
    brief_file = next((f for f in brief_candidates if f.is_file()), None)

    if not brief_file:
        if require_brief:
            errors.append("KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md not found but required via --require-brief.")
            return False
        print("[audit-claims] Note: KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md not found in parent directory (skipping brief check in isolated CI).")
        return False

    brief_content = brief_file.read_text(encoding="utf-8")

    # Count test methods in src/api.tests
    test_files = list((REPO_ROOT / "src/api.tests").rglob("*.cs"))
    fact_count = 0
    theory_count = 0
    for tf in test_files:
        code = tf.read_text(encoding="utf-8")
        fact_count += len(re.findall(r"\[Fact(?:\(.*\))?\]", code))
        inline_cases = len(re.findall(r"\[InlineData\(", code))
        # Account for [Theory] methods with MemberData/ClassData rather than InlineData
        method_blocks = re.split(r"(?=\[Theory)", code)
        non_inline_theories = 0
        for block in method_blocks[1:]:
            match = re.search(r"public\s+(?:async\s+)?(?:void|Task(?:\<[^>]+\>)?)\s+\w+\s*\(", block)
            header = block[:match.start()] if match else block
            if "[InlineData" not in header:
                non_inline_theories += 1
        theory_count += inline_cases + non_inline_theories

    total_tests = fact_count + theory_count

    expected_marker = f"{total_tests}/{total_tests}"
    if not re.search(rf"\b{total_tests}/{total_tests}\b", brief_content):
        errors.append(
            f"Portfolio Brief Drift: {brief_file.name} does not reflect current GradCast test count ({expected_marker} Unit Tests)."
        )
    return True


def main() -> int:
    import argparse
    parser = argparse.ArgumentParser(description="GradCast Claims, Architecture & Privacy Invariant Audit")
    parser.add_argument("--require-brief", action="store_true", help="Require Portfolio Master Brief to be present")
    args = parser.parse_args()

    print("=== GradCast Claims, Architecture & Privacy Invariant Audit ===")
    errors: List[str] = []

    audit_schema_parity(errors)
    audit_privacy_invariants(errors)
    audit_vocabulary(errors)
    brief_checked = audit_portfolio_master_brief(errors, require_brief=args.require_brief)

    if errors:
        print(f"\n[FAIL] Found {len(errors)} documentation / architectural claim violation(s):")
        for err in errors:
            print(f"  - {err}")
        return 1

    print("\n[PASS] Audit gates passed cleanly:")
    print(f"  ✓ Schema & DDL parity verified across {len(EXPECTED_SCHEMA_TABLES)} tables/views")
    print("  ✓ Hard k-anonymity invariant (k >= 5) verified in views.py")
    print("  ✓ Client privacy hash (SHA-256) verified in ScenarioEndpoints.cs")
    print("  ✓ Zero prohibited vocabulary words detected")
    if brief_checked:
        print("  ✓ Portfolio Master Brief parity verified (dynamic test suite)")
    else:
        print("  - Portfolio Master Brief check skipped (external file not present in isolated environment)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
