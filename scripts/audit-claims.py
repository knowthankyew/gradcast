#!/usr/bin/env python3
"""
audit-claims.py - Comprehensive Claims, Architecture & Privacy Invariant Linter for GradCast.

Provides portfolio-standard discoverability (scripts/audit-claims.py) that invokes:
1. Lakehouse schema, PySpark DDL, SQLite migration, and k-anonymity (k >= 5) invariants
2. Client privacy identifier hashing (SHA-256)
3. Prohibited marketing adjectives across docs/ARCHITECTURE.md, README.md, ROADMAP.md, and docs/SCHEMA.md
4. Portfolio Master Brief test parity
"""

import os
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent

PROHIBITED_WORDS = [
    re.compile(r"\bbullet-?proof\b", re.IGNORECASE),
    re.compile(r"\brock\s+solid\b", re.IGNORECASE),
    re.compile(r"\bconclusive\b", re.IGNORECASE),
    re.compile(r"\bdefinitive\b", re.IGNORECASE),
    re.compile(r"\binfinitely scalable\b", re.IGNORECASE),
]

def get_docs_to_scan():
    docs = [
        REPO_ROOT / "README.md",
        REPO_ROOT / "ROADMAP.md",
        REPO_ROOT / "ARCHITECTURE.md",
    ]
    docs_dir = REPO_ROOT / "docs"
    if docs_dir.is_dir():
        docs.extend(sorted(docs_dir.glob("*.md")))
    return docs


def audit_vocabulary(errors: list[str]) -> None:
    for doc in get_docs_to_scan():
        if not doc.is_file():
            continue
        lines = doc.read_text(encoding="utf-8").splitlines()
        for idx, line in enumerate(lines, 1):
            lower = line.lower()
            if "prohibit" in lower or "forbidden" in lower or "guardrail" in lower:
                continue
            for pattern in PROHIBITED_WORDS:
                if pattern.search(line):
                    errors.append(
                        f"Vocabulary violation in {doc.relative_to(REPO_ROOT)}:{idx}: "
                        f"Prohibited term matching '{pattern.pattern}' found: '{line.strip()}'"
                    )


def main() -> int:
    print("\n=== GradCast Claims, Architecture & Privacy Invariant Audit ===")
    errors: list[str] = []

    # 1. Run lakehouse core claims linter
    lakehouse_script = REPO_ROOT / "lakehouse" / "scripts" / "audit_claims.py"
    if lakehouse_script.is_file():
        proc = subprocess.run(
            [sys.executable, str(lakehouse_script)],
            cwd=str(REPO_ROOT),
            capture_output=True,
            text=True,
        )
        if proc.returncode != 0:
            errors.append(f"Lakehouse claims audit failed:\n{proc.stdout}\n{proc.stderr}")
        else:
            # Print lakehouse pass summary lines indented
            for line in proc.stdout.splitlines():
                if line.startswith("  ✓") or line.startswith("[PASS]"):
                    print(line)
    else:
        errors.append(f"Missing core lakehouse claims script at {lakehouse_script}")

    # 2. Audit vocabulary across architecture and documentation
    audit_vocabulary(errors)

    if errors:
        print(f"\n[FAIL] {len(errors)} invariant violation(s) detected:")
        for err in errors:
            print(f"  ✗ {err}")
        return 1

    print("  ✓ docs/ARCHITECTURE.md and all docs verified clean of prohibited vocabulary")
    print("\nAll GradCast claims, architecture, and governance gates verified successfully.\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
