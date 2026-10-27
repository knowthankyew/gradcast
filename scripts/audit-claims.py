#!/usr/bin/env python3
"""
audit-claims.py - Portfolio-standard CLI entrypoint for GradCast Claims, Architecture & Privacy Invariant Linter.

Delegates directly in-process to lakehouse.scripts.audit_claims with full CLI flag forwarding
(--require-brief) and zero duplicate subprocess execution.
"""

from pathlib import Path
import sys

REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from lakehouse.scripts.audit_claims import (  # noqa: E402
    EXPECTED_SCHEMA_TABLES,
    PROHIBITED_WORDS,
    audit_architecture_and_notice_parity,
    audit_portfolio_master_brief,
    audit_privacy_invariants,
    audit_readme_parity,
    audit_schema_parity,
    audit_supply_chain_and_cpm,
    audit_vocabulary,
    get_docs_to_scan,
    main,
    run_all_audits,
)

__all__ = [
    "EXPECTED_SCHEMA_TABLES",
    "PROHIBITED_WORDS",
    "audit_architecture_and_notice_parity",
    "audit_portfolio_master_brief",
    "audit_privacy_invariants",
    "audit_readme_parity",
    "audit_schema_parity",
    "audit_supply_chain_and_cpm",
    "audit_vocabulary",
    "get_docs_to_scan",
    "main",
    "run_all_audits",
]

if __name__ == "__main__":
    sys.exit(main())
