#!/usr/bin/env python3
"""
audit_claims.py - Automated Claims, Architectural Parity & Privacy Invariant Linter for GradCast.

Enforces:
1. Schema & Bidirectional DDL Parity:
   Validates that all tables, column sets, and clustering keys in docs/SCHEMA.md match PySpark/SQL DDL and SQLite migrations.
2. Invariant, Edge Security & Privacy Governance:
   - Verifies that the hard k-anonymity boundary (HAVING COUNT(*) >= 5) exists in lakehouse/src/pipelines/views.py.
   - Verifies that client identifiers are hashed via SHA256.HashData with daily rotation (yyyy-MM-dd), rate-limited,
     protected by trusted proxy checks, and fail-closed in production when Security:ClientHashSalt is unset.
   - Verifies that silver.scenarios strips client_hash and scenario_title, and Terraform RBAC isolates Bronze/Silver/Quarantine from analysts.
3. Supply Chain, CPM & Container Hardening:
   - Verifies Directory.Build.props fail-closed NuGetAudit (NU1903/NU1904) and Directory.Packages.props CPM with zero inline .csproj version overrides.
   - Verifies Dockerfile multi-stage Alpine build and non-root execution (USER $APP_UID).
4. Architecture & Legal Notice Parity:
   - Verifies docs/ARCHITECTURE.md and NOTICE.md accurately reflect GradCast's domain, Dapper data layer, and runtime invariants.
5. Vocabulary Guardrails:
   Prohibits unprovable marketing adjectives ('bulletproof', 'rock solid', 'conclusive', 'definitive', 'infinitely scalable', 'instant') across all markdown docs (unless explicitly exempted via an inline `<!-- audit-ignore -->` HTML comment).
6. README & Portfolio Master Brief Parity:
   Verifies root README.md architectural contracts and KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md test suite inventory scoped to gradcast.
"""

import ast
import argparse
import importlib.util
import os
from pathlib import Path
import re
import sys
from typing import Dict, List, Optional, Tuple
import xml.etree.ElementTree as ET

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
DEV_ROOT = REPO_ROOT.parent

PROHIBITED_WORDS = [
    re.compile(r"\bbullet-?proof\b", re.IGNORECASE),
    re.compile(r"\brock\s+solid\b", re.IGNORECASE),
    re.compile(r"\bconclusive\b", re.IGNORECASE),
    re.compile(r"\bdefinitive\b", re.IGNORECASE),
    re.compile(r"\binfinitely\s+scalable\b", re.IGNORECASE),
    re.compile(r"\binstant\b", re.IGNORECASE),
]

EXPECTED_SCHEMA_TABLES = [
    ("shared_scenarios", "src/data/SqliteDatabaseInitializer.cs"),
    ("bronze.shared_scenarios", "lakehouse/src/pipelines/bronze_ingest.py"),
    ("bronze.college_scorecard", "lakehouse/src/pipelines/bronze_ingest.py"),
    ("silver.scenarios", "lakehouse/src/pipelines/silver_transform.py"),
    ("quarantine.scenarios_rejected", "lakehouse/src/pipelines/silver_transform.py"),
    ("gold.dim_school", "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.dim_metro", "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.dim_program", "lakehouse/src/pipelines/gold_modeling.py"),
    ("gold.fact_scenario_outcomes", "lakehouse/src/pipelines/gold_modeling.py"),
    ("v_public_positive_finds", "lakehouse/src/pipelines/views.py"),
    ("community_scenario_benchmarks", "lakehouse/src/pipelines/export_sqlite.py"),
    ("community_scenario_benchmarks", "src/data/SqliteDatabaseInitializer.cs"),
]


def _extract_create_table_blocks(sql_text: str) -> List[Tuple[str, str, str]]:
    """
    Extract (table_name, columns_body, trailer) tuples from SQL text using
    balanced-parenthesis scanning so nested DECIMAL(10, 2) or CHECK(...)
    expressions do not truncate the column list.
    """
    clean_sql = re.sub(r"--.*$", "", sql_text, flags=re.MULTILINE)
    blocks: List[Tuple[str, str, str]] = []
    pattern = re.compile(r"CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([\"`\w\.\{\}]+)\s*\(", re.IGNORECASE)
    for match in pattern.finditer(clean_sql):
        table_name = match.group(1).replace('"', "").replace("`", "")
        start_idx = match.end()
        depth = 1
        i = start_idx
        active_quote: Optional[str] = None
        while i < len(clean_sql) and depth > 0:
            ch = clean_sql[i]
            if active_quote is not None:
                if ch == active_quote:
                    active_quote = None
            elif ch in ("'", '"', "`"):
                active_quote = ch
            elif ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
            i += 1
        if depth == 0:
            cols_body = clean_sql[start_idx : i - 1]
            trailer = re.split(
                r"""(?:;|\"\"\"|'''|\bCREATE\s+(?:OR\s+REPLACE\s+)?(?:TABLE|VIEW|INDEX)\b|\bALTER\s+TABLE\b)""",
                clean_sql[i : i + 450],
                maxsplit=1,
                flags=re.IGNORECASE,
            )[0]
            blocks.append((table_name, cols_body, trailer))
    return blocks


def _strip_csharp_comments(code: str) -> str:
    """
    Strip C# block (/* ... */) and line (// ...) comments while preserving
    string and char literals (e.g., 'https://dbc-123.cloud.databricks.com', '\"') intact.
    """
    return re.sub(
        r"""(@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')|/\*[\s\S]*?\*/|//[^\n]*""",
        lambda m: m.group(1) or "",
        code,
    )


def _parse_columns_from_body(cols_body: str) -> List[str]:
    cols: List[str] = []
    constraint_prefix = re.compile(
        r"^(?:CONSTRAINT\b|PRIMARY\s+KEY\b|FOREIGN\s+KEY\b|UNIQUE\b|CHECK\s*\()",
        re.IGNORECASE,
    )
    segments: List[str] = []
    buf: List[str] = []
    depth = 0
    active_quote: Optional[str] = None
    for ch in cols_body:
        if active_quote is not None:
            buf.append(ch)
            if ch == active_quote:
                active_quote = None
        elif ch in ("'", '"', "`"):
            active_quote = ch
            buf.append(ch)
        elif ch == "(":
            depth += 1
            buf.append(ch)
        elif ch == ")":
            depth = max(0, depth - 1)
            buf.append(ch)
        elif ch == "," and depth == 0:
            segments.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    if buf:
        segments.append("".join(buf))

    for seg in segments:
        norm = " ".join(seg.strip().split())
        if not norm or constraint_prefix.match(norm):
            continue
        col_m = re.match(r'["`]?([a-zA-Z0-9_]+)["`]?\s+', norm)
        if col_m:
            cols.append(col_m.group(1))
    return cols


def audit_schema_parity(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """
    Verify bidirectional schema and DDL parity between docs/SCHEMA.md and source code:
    1. Parses CREATE TABLE DDL blocks from docs/SCHEMA.md.
    2. Validates that every table, column set (both ways), and CLUSTER BY key matches source code DDLs.
    """
    schema_doc = repo_root / "docs" / "SCHEMA.md"
    if not schema_doc.is_file():
        errors.append(f"Missing schema document: {schema_doc}")
        return

    schema_content = schema_doc.read_text(encoding="utf-8")
    parsed_tables: Dict[str, Dict[str, object]] = {}
    for raw_tbl, cols_body, trailer in _extract_create_table_blocks(schema_content):
        parts = raw_tbl.split(".")
        norm_key = ".".join(parts[-2:]) if len(parts) >= 2 else parts[0]
        cols = _parse_columns_from_body(cols_body)
        cluster_m = re.search(r"CLUSTER BY\s*\(([^\)]+)\)", trailer, re.IGNORECASE)
        cluster_keys = [k.strip() for k in cluster_m.group(1).split(",")] if cluster_m else []

        parsed_tables[norm_key] = {
            "full_name": raw_tbl,
            "columns": cols,
            "cluster_keys": cluster_keys,
        }

    for table_spec, rel_source_path in EXPECTED_SCHEMA_TABLES:
        source_file = repo_root / rel_source_path
        if table_spec not in schema_content:
            errors.append(f"Table '{table_spec}' declared in expectations but missing in docs/SCHEMA.md")

        if not source_file.is_file():
            errors.append(f"Expected source file missing for table '{table_spec}': {rel_source_path}")
            continue

        source_content = source_file.read_text(encoding="utf-8")
        clean_table_name = table_spec.split(".")[-1]
        if clean_table_name not in source_content:
            errors.append(f"Table '{clean_table_name}' declared in docs/SCHEMA.md but missing in {rel_source_path}")

        source_blocks = _extract_create_table_blocks(source_content)
        matching_source = next(
            (
                b
                for b in source_blocks
                if b[0].split(".")[-1].strip('"`[]').lower() == clean_table_name.lower()
            ),
            None,
        )
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
                source_cols: List[str] = []
            else:
                errors.append(f"Table DDL block for '{clean_table_name}' missing in {rel_source_path}")
                continue
        else:
            table_block = matching_source[1]
            source_cols = _parse_columns_from_body(table_block)

        source_trailer = matching_source[2] if matching_source else ""
        src_cluster_m = re.search(r"CLUSTER BY\s*\(([^\)]+)\)", source_trailer, re.IGNORECASE)
        src_cluster_keys = [k.strip() for k in src_cluster_m.group(1).split(",") if k.strip()] if src_cluster_m else []

        spec = parsed_tables.get(table_spec)
        if spec:
            doc_cols: List[str] = list(spec["columns"])  # type: ignore[arg-type]
            if source_cols:
                source_col_set = set(source_cols)
                for col in doc_cols:
                    if col not in source_col_set:
                        errors.append(
                            f"Schema parity mismatch: Column '{col}' of table '{table_spec}' not found in DDL block of {rel_source_path}"
                        )
                doc_col_set = set(doc_cols)
                for src_col in source_cols:
                    if src_col not in doc_col_set:
                        errors.append(
                            f"Undocumented column in source DDL: Column '{src_col}' in {rel_source_path} for '{table_spec}' is missing from docs/SCHEMA.md"
                        )
            else:
                for col in doc_cols:
                    if not re.search(rf"\b{col}\b", table_block):
                        errors.append(
                            f"Schema parity mismatch: Column '{col}' of table '{table_spec}' not found in DDL block of {rel_source_path}"
                        )

            cluster_keys: List[str] = list(spec["cluster_keys"])  # type: ignore[arg-type]
            if cluster_keys != src_cluster_keys:
                errors.append(
                    f"Clustering key parity mismatch: Table '{table_spec}' expects CLUSTER BY {cluster_keys} in {rel_source_path}, found {src_cluster_keys}"
                )
        elif table_spec != "v_public_positive_finds":
            errors.append(f"Table DDL block for '{table_spec}' missing in docs/SCHEMA.md")

        if table_spec.startswith("bronze."):
            if not re.search(r"['\"]?delta\.appendOnly['\"]?\s*=\s*['\"]?true['\"]?", source_trailer, re.IGNORECASE):
                errors.append(
                    f"Medallion property violation: Bronze table '{table_spec}' must declare 'delta.appendOnly' = 'true' in {rel_source_path}"
                )
        if table_spec.startswith("silver."):
            if not re.search(r"['\"]?delta\.enableChangeDataFeed['\"]?\s*=\s*['\"]?true['\"]?", source_trailer, re.IGNORECASE):
                errors.append(
                    f"Medallion property violation: Silver table '{table_spec}' must declare 'delta.enableChangeDataFeed' = 'true' in {rel_source_path}"
                )
            if src_cluster_keys != ["cip_family", "cbsa_code"]:
                errors.append(
                    f"Medallion property violation: Silver table '{table_spec}' must cluster by ['cip_family', 'cbsa_code'] in {rel_source_path}"
                )
        if table_spec == "community_scenario_benchmarks":
            uncommented_src = re.sub(r"--.*$", "", _strip_csharp_comments(source_content), flags=re.MULTILINE)
            uncommented_src = re.sub(r"#.*$", "", uncommented_src, flags=re.MULTILINE)
            index_pattern = re.compile(
                r"CREATE\s+(?:UNIQUE\s+)?INDEX\s+(?:IF\s+NOT\s+EXISTS\s+)?[\"`]?\bIX_community_scenario_benchmarks_cbsa_cip\b[\"`]?\s+ON\s+[\"`]?\bcommunity_scenario_benchmarks\b",
                re.IGNORECASE,
            )
            if not index_pattern.search(uncommented_src):
                errors.append(
                    f"Index parity mismatch: Table '{table_spec}' expects covering index 'IX_community_scenario_benchmarks_cbsa_cip' in {rel_source_path}"
                )


def audit_privacy_invariants(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """
    Verify hard k-anonymity (k >= 5), SHA-256 daily-salted client hash, production fail-closed salt guard,
    Silver PII exclusion, and Terraform Unity Catalog RBAC isolation.
    """
    views_file = repo_root / "lakehouse/src/pipelines/views.py"
    if not views_file.is_file():
        errors.append("Missing views pipeline: lakehouse/src/pipelines/views.py")
    else:
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

        # Dynamic execution test of k-anonymity SQL generator loaded directly from repo_root/lakehouse/src/pipelines/views.py
        try:
            spec = importlib.util.spec_from_file_location("gradcast_views_audit", views_file)
            if spec is None or spec.loader is None:
                errors.append(f"Failed to create module spec for {views_file}")
            else:
                views_mod = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(views_mod)
                get_k_anonymity_view_sql = getattr(views_mod, "get_k_anonymity_view_sql", None)
                if not callable(get_k_anonymity_view_sql):
                    errors.append("Privacy violation: get_k_anonymity_view_sql is not callable in views.py")
                else:
                    generated_sql_clamped = get_k_anonymity_view_sql(catalog="audit_cat", min_cohort_size=1)
                    if "HAVING COUNT(*) >= 5" not in generated_sql_clamped:
                        errors.append("Privacy violation: get_k_anonymity_view_sql(min_cohort_size=1) failed to enforce k >= 5 clamp")
                    if "HAVING COUNT(*) >= 1" in generated_sql_clamped:
                        errors.append("Privacy violation: get_k_anonymity_view_sql(min_cohort_size=1) emitted unsuppressed threshold >= 1")
        except Exception as exc:
            errors.append(f"Failed to execute dynamic k-anonymity invariant test: {exc}")

    # Verify SHA-256 daily-salted hashing, trusted proxy guard, and fail-closed production salt in ScenarioEndpoints.cs & Program.cs
    endpoint_file = repo_root / "src/api/Endpoints/ScenarioEndpoints.cs"
    if not endpoint_file.is_file():
        errors.append("Missing endpoint file: src/api/Endpoints/ScenarioEndpoints.cs")
    else:
        ep_content = _strip_csharp_comments(endpoint_file.read_text(encoding="utf-8"))
        if not re.search(r'\$\"\{dateStr\}:\{clientIp\}:\{salt\}\"[\s\S]{0,120}SHA256\.HashData\(', ep_content):
            errors.append("Privacy violation: Client hash in ScenarioEndpoints.cs must combine dateStr, clientIp, and salt into SHA256.HashData(...)")
        if not re.search(r'UtcNow\.ToString\("yyyy-MM-dd"\)', ep_content):
            errors.append("Privacy violation: ScenarioEndpoints.cs must rotate client hash salt daily via yyyy-MM-dd UTC date prefix")
        if not re.search(r"if\s*\(\s*IsTrustedProxy\([^\)]+\)\s*\)[\s\S]{0,400}Fly-Client-IP[\s\S]{0,400}X-Forwarded-For", ep_content):
            errors.append("Security violation: ScenarioEndpoints.cs must verify IsTrustedProxy before trusting forwarded IP headers")
        if not re.search(r"TryInsertScenarioWithDailyLimitAsync\s*\([^;\)]*\bMaxDailySharesPerClient\b", ep_content):
            errors.append("Privacy violation: ScenarioEndpoints.cs must enforce per-client daily share rate limiting (MaxDailySharesPerClient)")
        if not re.search(r"ReportedAnnualSalary\s*<\s*0", ep_content) or not re.search(r"MonthlyDisposable\s+is\s*<\s*-20_?000", ep_content):
            errors.append("Validation violation: ScenarioEndpoints.cs must enforce non-negative salary and -$20,000 disposable-income bounds")
        for allowlist_name in ("AllowedHousingTypes", "AllowedSentimentTags", "AllowedSalaryModes"):
            if f"{allowlist_name}.Contains(" not in ep_content:
                errors.append(f"Validation violation: ScenarioEndpoints.cs must enforce input allowlist '{allowlist_name}'")
        if not re.search(r"Security:ClientHashSalt[\s\S]{0,250}IsProduction\(\)[\s\S]{0,150}throw\s+new\s+InvalidOperationException", ep_content):
            errors.append("Security violation: ScenarioEndpoints.cs must fail closed in Production when Security:ClientHashSalt is missing")

    # Verify Dual-Mode Edge Read Isolation: no direct Databricks queries or SDK references in src/api or src/data
    for tier_rel in ("src/api", "src/data"):
        tier_dir = repo_root / tier_rel
        if tier_dir.is_dir():
            for src_file in sorted(tier_dir.rglob("*")):
                if not src_file.is_file() or src_file.suffix.lower() not in (".cs", ".csproj", ".json", ".config"):
                    continue
                if {"bin", "obj"}.intersection(src_file.relative_to(repo_root).parts):
                    continue
                cleaned = _strip_csharp_comments(src_file.read_text(encoding="utf-8-sig"))
                if re.search(r"\bdatabricks\b|\.cloud\.databricks\.com", cleaned, re.IGNORECASE):
                    errors.append(
                        f"Dual-mode isolation violation: {src_file.relative_to(repo_root)} references Databricks in live web/data tier"
                    )

    program_file = repo_root / "src/api/Program.cs"
    if not program_file.is_file():
        errors.append("Missing API entrypoint: src/api/Program.cs")
    else:
        prog_content = _strip_csharp_comments(program_file.read_text(encoding="utf-8"))
        if not re.search(r"IsProduction\(\)[\s\S]{0,120}Security:ClientHashSalt[\s\S]{0,150}throw\s+new\s+InvalidOperationException", prog_content):
            errors.append("Security violation: Program.cs must fail closed at startup in Production when Security:ClientHashSalt is unset")

    # Verify Silver scenarios table excludes edge client_hash and scenario_title (docs/SCHEMA.md Section 6.2)
    silver_file = repo_root / "lakehouse/src/pipelines/silver_transform.py"
    if not silver_file.is_file():
        errors.append("Missing Silver pipeline: lakehouse/src/pipelines/silver_transform.py")
    else:
        silver_blocks = _extract_create_table_blocks(silver_file.read_text(encoding="utf-8"))
        silver_scenarios_block = next(
            (b for b in silver_blocks if "scenario_id" in b[1] and "quarantine_id" not in b[1]),
            None,
        )
        if not silver_scenarios_block:
            errors.append("Missing silver.scenarios DDL block in lakehouse/src/pipelines/silver_transform.py")
        else:
            silver_cols = set(_parse_columns_from_body(silver_scenarios_block[1]))
            for forbidden_col in ("client_hash", "scenario_title"):
                if forbidden_col in silver_cols:
                    errors.append(
                        f"Privacy boundary violation: '{forbidden_col}' must be excluded from silver.scenarios DDL in silver_transform.py"
                    )

    # Verify Terraform RBAC isolates Bronze, Silver, Quarantine, and Control schemas from analysts (docs/SCHEMA.md Section 6.1)
    rbac_file = repo_root / "lakehouse/terraform/access_control.tf"
    if not rbac_file.is_file():
        errors.append("Missing Terraform RBAC manifest: lakehouse/terraform/access_control.tf")
    else:
        rbac_content = rbac_file.read_text(encoding="utf-8")
        for restricted_resource in ("bronze_access", "silver_access", "quarantine_access", "control_access"):
            res_match = re.search(
                rf'resource\s+"databricks_grants"\s+"{restricted_resource}"\s*\{{(.*?)\n\}}',
                rbac_content,
                re.DOTALL,
            )
            if not res_match:
                errors.append(f"Missing databricks_grants resource '{restricted_resource}' in access_control.tf")
            else:
                clean_block = re.sub(r"#.*$", "", res_match.group(1), flags=re.MULTILINE)
                if re.search(
                    r"\bprincipal\s*=\s*[^\n]*(?:analyst|account\s+users|\ball[_\s-]*users\b|[\"']users[\"'])",
                    clean_block,
                    re.IGNORECASE,
                ):
                    errors.append(
                        f"RBAC privacy violation: analysts group must not be granted privileges in '{restricted_resource}' (access_control.tf)"
                    )


def audit_supply_chain_and_cpm(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """
    Verify Central Package Management (CPM), fail-closed MSBuild NuGetAudit (NU1903/NU1904),
    zero inline .csproj package version overrides, and multi-stage non-root Dockerfile invariants.
    """
    build_props = repo_root / "Directory.Build.props"
    if not build_props.is_file():
        errors.append("Missing Directory.Build.props at repository root.")
    else:
        try:
            bp_root = ET.fromstring(build_props.read_text(encoding="utf-8-sig"))
            nuget_audit_vals = [(el.text or "").strip().lower() for el in bp_root.findall(".//NuGetAudit")]
            if not nuget_audit_vals or nuget_audit_vals[-1] != "true":
                errors.append("Supply-chain violation: Directory.Build.props must enable <NuGetAudit>true</NuGetAudit>.")

            audit_level_vals = [(el.text or "").strip().lower() for el in bp_root.findall(".//NuGetAuditLevel")]
            if audit_level_vals and audit_level_vals[-1] not in ("low", "moderate", "high"):
                errors.append("Supply-chain violation: Directory.Build.props NuGetAuditLevel must not exceed 'high' (so NU1903 remains active).")

            warn_as_err_text = ";".join((el.text or "") for el in bp_root.findall(".//WarningsAsErrors"))
            if "NU1903" not in warn_as_err_text or "NU1904" not in warn_as_err_text:
                errors.append("Supply-chain violation: Directory.Build.props must treat NU1903 and NU1904 as build errors.")

            suppressed_text = ";".join(
                (el.text or "")
                for tag in ("NoWarn", "WarningsNotAsErrors")
                for el in bp_root.findall(f".//{tag}")
            )
            if "NU1903" in suppressed_text or "NU1904" in suppressed_text:
                errors.append("Supply-chain violation: Directory.Build.props must not suppress NU1903 or NU1904 in NoWarn/WarningsNotAsErrors.")
        except ET.ParseError as exc:
            errors.append(f"Supply-chain violation: Directory.Build.props is not valid XML: {exc}")

    pkg_props = repo_root / "Directory.Packages.props"
    if not pkg_props.is_file():
        errors.append("Missing Directory.Packages.props at repository root.")
    else:
        try:
            pp_root = ET.fromstring(pkg_props.read_text(encoding="utf-8-sig"))
            cpm_vals = [(el.text or "").strip().lower() for el in pp_root.findall(".//ManagePackageVersionsCentrally")]
            if not cpm_vals or cpm_vals[-1] != "true":
                errors.append("CPM violation: Directory.Packages.props must enable ManagePackageVersionsCentrally.")
        except ET.ParseError as exc:
            errors.append(f"CPM violation: Directory.Packages.props is not valid XML: {exc}")

    src_dir = repo_root / "src"
    if src_dir.is_dir():
        for csproj in sorted(src_dir.glob("*/*.csproj")):
            try:
                cs_root = ET.fromstring(csproj.read_text(encoding="utf-8-sig"))
                for pkg_ref in cs_root.findall(".//PackageReference"):
                    has_attr_ver = any(
                        k.lower() in ("version", "versionoverride") for k in pkg_ref.attrib
                    )
                    has_child_ver = any(
                        child.tag.lower() in ("version", "versionoverride") for child in pkg_ref
                    )
                    if has_attr_ver or has_child_ver:
                        errors.append(
                            f"CPM violation: {csproj.relative_to(repo_root)} contains inline PackageReference Version attribute (must be pinned in Directory.Packages.props)."
                        )
                        break
            except ET.ParseError as exc:
                errors.append(f"CPM violation: {csproj.relative_to(repo_root)} is not valid XML: {exc}")

    dockerfile = repo_root / "Dockerfile"
    if not dockerfile.is_file():
        errors.append("Missing Dockerfile at repository root.")
    else:
        df_text = dockerfile.read_text(encoding="utf-8")
        df_clean_lines = [
            re.sub(r"#.*$", "", line).strip()
            for line in df_text.splitlines()
            if re.sub(r"#.*$", "", line).strip()
        ]
        user_directives = [line for line in df_clean_lines if re.match(r"^USER\s+", line, re.IGNORECASE)]
        if not user_directives or not re.match(r"^USER\s+\$APP_UID$", user_directives[-1], re.IGNORECASE):
            errors.append("Container hardening violation: Dockerfile final USER instruction must be USER $APP_UID.")
        df_clean_text = "\n".join(df_clean_lines)
        if "FROM node:22-alpine" not in df_clean_text or "aspnet:10.0-alpine" not in df_clean_text:
            errors.append("Container parity violation: Dockerfile must use multi-stage node:22-alpine and aspnet:10.0-alpine images.")


def audit_architecture_and_notice_parity(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """
    Verify that NOTICE.md and docs/ARCHITECTURE.md accurately describe GradCast's domain,
    Dapper SQLite data layer, and runtime architecture without stale or hallucinated claims.
    """
    notice_file = repo_root / "NOTICE.md"
    if not notice_file.is_file():
        errors.append("Missing NOTICE.md at repository root.")
    else:
        notice_text = notice_file.read_text(encoding="utf-8")
        if "transcript analysis" in notice_text.lower():
            errors.append("NOTICE.md drift: Contains stale 'transcript analysis' description unrelated to GradCast.")
        for required_term in ("College Scorecard", "localStorage", "SHA-256", "k >= 5", "CycloneDX"):
            if required_term not in notice_text:
                errors.append(f"NOTICE.md drift: Missing required governance disclosure term '{required_term}'.")

    arch_file = repo_root / "docs" / "ARCHITECTURE.md"
    if not arch_file.is_file():
        errors.append("Missing docs/ARCHITECTURE.md.")
    else:
        arch_text = arch_file.read_text(encoding="utf-8")
        if "<!-- diagram-meta" not in arch_text or "```mermaid" not in arch_text:
            errors.append("docs/ARCHITECTURE.md missing diagram-meta header or mermaid architecture diagrams.")
        if "EF Core" in arch_text:
            errors.append("docs/ARCHITECTURE.md drift: Falsely claims 'EF Core' instead of Dapper.")
        if "AOT runtime" in arch_text:
            errors.append("docs/ARCHITECTURE.md drift: Falsely claims '.NET 10 AOT runtime' instead of managed ASP.NET Core Alpine runtime.")
        if "KnowThankYew.Privacy.Telemetry" in arch_text:
            errors.append("docs/ARCHITECTURE.md drift: References unlinked 'KnowThankYew.Privacy.Telemetry' package.")


def get_docs_to_scan(repo_root: Path = REPO_ROOT) -> List[Path]:
    """Return deduplicated list of all repository markdown files to scan for prohibited vocabulary."""
    excluded_dirs = {"node_modules", "bin", "obj", "dist", "coverage", "__pycache__", "venv", ".venv", ".git"}
    candidates: List[Path] = []
    for root, dirs, files in os.walk(repo_root):
        dirs[:] = sorted(d for d in dirs if d not in excluded_dirs and not d.startswith("."))
        root_path = Path(root)
        for fname in sorted(files):
            if fname.lower().endswith(".md"):
                candidates.append(root_path / fname)

    seen = set()
    unique_docs: List[Path] = []
    for doc in candidates:
        resolved = doc.resolve()
        if resolved not in seen and doc.is_file():
            seen.add(resolved)
            unique_docs.append(doc)
    return unique_docs


def audit_vocabulary(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """Single-pass scan across all repository markdown documentation for prohibited marketing terms."""
    rule_context_markers = (
        "prohibits unprovable",
        "prohibited word",
        "prohibited term",
        "prohibited marketing",
        "prohibited adjectives",
        "banned vocabulary",
    )
    for doc in get_docs_to_scan(repo_root):
        rel_path = doc.relative_to(repo_root)
        lines = doc.read_text(encoding="utf-8").splitlines()
        for idx, line in enumerate(lines, 1):
            lower = line.lower()
            if re.search(r"<!--\s*audit-ignore\s*-->", lower):
                continue
            scan_line = line
            if any(marker in lower for marker in rule_context_markers):
                # Strip only quoted/backticked rule examples so unquoted claims on the same line are still checked
                scan_line = re.sub(r"""(?:`[^`\n]+`|"[^"\n]+"|'[^'\n]+')""", "", line)
            for pattern in PROHIBITED_WORDS:
                if pattern.search(scan_line):
                    errors.append(
                        f"Vocabulary violation in {rel_path}:{idx}: "
                        f"Prohibited term matching '{pattern.pattern}' found: '{line.strip()}'"
                    )


def audit_readme_parity(errors: List[str], repo_root: Path = REPO_ROOT) -> None:
    """Verify that root README.md reflects Lakehouse, Databricks, and Dual-Mode architecture."""
    readme_file = repo_root / "README.md"
    if not readme_file.is_file():
        errors.append("Missing README.md file at repository root.")
        return

    content = readme_file.read_text(encoding="utf-8")

    # 1. Tech stack section check: specifically inside '## Tech Stack'
    tech_stack_match = re.search(r"## Tech Stack\s*\n(.*?)(?=\n## |\Z)", content, re.DOTALL)
    if not tech_stack_match:
        errors.append("README drift: '## Tech Stack' section not found in README.md")
    else:
        tech_stack_text = tech_stack_match.group(1)
        required_stack_keywords = [
            "SQLite",
            "Dapper",
            "Databricks",
            "Delta Lake",
            "PySpark",
            "Unity Catalog",
            "Terraform",
            "Dual-Mode Architecture",
        ]
        for kw in required_stack_keywords:
            if kw not in tech_stack_text:
                errors.append(f"README drift: '## Tech Stack' section is missing keyword '{kw}'")

    # 2. Directory structure check inside fenced block directly under '## Project Structure'
    proj_struct_match = re.search(r"## Project Structure\b.*?```[^\n]*\n(.*?)```", content, re.DOTALL)
    if not proj_struct_match or "lakehouse/" not in proj_struct_match.group(1):
        errors.append("README drift: Project structure tree in README.md does not document 'lakehouse/' directory.")

    # 3. Operations & verification commands check scoped to specific sections
    ops_match = re.search(r"## Databricks & Lakehouse Operations\s*\n(.*?)(?=\n## |\Z)", content, re.DOTALL)
    if not ops_match:
        errors.append("README drift: Missing '## Databricks & Lakehouse Operations' section in README.md")
    else:
        ops_text = ops_match.group(1)
        for cmd in ["databricks bundle validate", "databricks bundle deploy", "lakehouse/terraform"]:
            if cmd not in ops_text:
                errors.append(f"README drift: Missing operational command or path '{cmd}' in '## Databricks & Lakehouse Operations'")

    testing_match = re.search(r"## Testing\s*\n(.*?)(?=\n## |\Z)", content, re.DOTALL)
    if not testing_match:
        errors.append("README drift: Missing '## Testing' section in README.md")
    else:
        testing_text = testing_match.group(1)
        for cmd in ["lakehouse/tests", "scripts/audit-claims.py", "lakehouse/scripts/audit_claims.py"]:
            if cmd not in testing_text:
                errors.append(f"README drift: Missing test/audit command or path '{cmd}' in '## Testing'")

    # 4. Architectural doc links check (enforce valid markdown links ](target))
    for doc in ["docs/ARCHITECTURE.md", "docs/SCHEMA.md", "ROADMAP.md", "docs/QA_CHECKLIST.md"]:
        if f"]({doc})" not in content:
            errors.append(f"README drift: Missing Markdown link to '{doc}' in README.md")

    # 5. Dual-Mode edge-read storage contract check
    if "gradcast.db" not in content:
        errors.append("README drift: Missing SQLite database contract reference 'gradcast.db' in README.md")

    normalized = re.sub(r"\s+", " ", content.replace("*", "").replace("`", ""))
    normalized_lower = normalized.lower()
    if "all reads are served from local sqlite" not in normalized_lower:
        errors.append("README drift: Missing edge-read storage contract ('all reads are served from local SQLite') in README.md")

    if "direct databricks cluster queries during live web traffic are prohibited" not in normalized_lower:
        errors.append("README drift: Missing boundary invariant ('direct Databricks cluster queries during live web traffic are prohibited') in README.md")

    # Verify production data mode parity scoped directly to '### Data Modes'
    data_modes_match = re.search(r"### Data Modes\s*\n(.*?)(?=\n## |\Z)", content, re.DOTALL)
    if not data_modes_match:
        errors.append("README drift: Missing '### Data Modes' section in README.md")
    else:
        data_modes_text = data_modes_match.group(1)
        if not re.search(r"^\s*-\s*\*\*`local`\*\*\s*\([^)]*production[^)]*\)", data_modes_text, re.IGNORECASE | re.MULTILINE):
            errors.append("README drift: 'local' mode must be designated as the production/edge mode in '### Data Modes'")

        for mode in ["hybrid", "api"]:
            mode_line = re.search(rf"^\s*-\s*\*\*`{mode}`\*\*(.*)$", data_modes_text, re.IGNORECASE | re.MULTILINE)
            if mode_line:
                line_text = mode_line.group(1)
                if re.match(r"^\s*\([^)]*(?<!\bnon-)(?<!\bnot\s)(?<!\bnot for\s)\bproduction\b[^)]*\)", line_text, re.IGNORECASE) or \
                   re.search(r"\b(?:use in production|recommended for production|designated as production)\b", line_text, re.IGNORECASE):
                    errors.append(f"README drift: Incompatible data mode '{mode}' designated for production in '### Data Modes'")


def audit_portfolio_master_brief(
    errors: List[str],
    require_brief: bool = False,
    repo_root: Path = REPO_ROOT,
    brief_file_override: Optional[Path] = None,
) -> bool:
    """Verify that KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md gradcast rows match current test inventory."""
    if brief_file_override is not None:
        brief_file = brief_file_override if brief_file_override.is_file() else None
    else:
        dev_root = repo_root.parent
        brief_candidates = [
            dev_root / "KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md",
            repo_root / "KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md",
        ]
        brief_file = next((f for f in brief_candidates if f.is_file()), None)

    if not brief_file:
        if require_brief:
            errors.append("KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md not found but required via --require-brief.")
            return False
        print("[audit-claims] Note: KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md not found in parent directory (skipping brief check in isolated CI).")
        return False

    brief_content = brief_file.read_text(encoding="utf-8")

    # Count test methods in src/api.tests (excluding bin/ and obj/ build directories)
    test_files = [
        p
        for p in (repo_root / "src/api.tests").rglob("*.cs")
        if not {"bin", "obj"}.intersection(p.relative_to(repo_root).parts)
    ]
    fact_count = 0
    theory_count = 0
    for tf in test_files:
        code = tf.read_text(encoding="utf-8")
        fact_count += len(re.findall(r"\[Fact(?:\(.*\))?\]", code))
        inline_cases = len(re.findall(r"\[InlineData\(", code))
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

    # Scope regex specifically to lines mentioning `gradcast` in the Master Brief
    gradcast_lines = [line for line in brief_content.splitlines() if "`gradcast`" in line]
    if not gradcast_lines:
        errors.append(f"Portfolio Brief Drift: No `gradcast` entry found in {brief_file.name}.")
        return False

    ratio_lines = [line for line in gradcast_lines if re.search(r"\b\d+/\d+\b", line)]
    if not ratio_lines or not all(re.search(rf"\b{total_tests}/{total_tests}\b", line) for line in ratio_lines):
        errors.append(
            f"Portfolio Brief Drift: `gradcast` entry in {brief_file.name} does not reflect current GradCast test count ({expected_marker} Unit Tests)."
        )
    return True


def run_all_audits(
    require_brief: bool = False,
    repo_root: Path = REPO_ROOT,
    brief_file_override: Optional[Path] = None,
) -> Tuple[List[str], bool]:
    errors: List[str] = []
    audit_schema_parity(errors, repo_root=repo_root)
    audit_privacy_invariants(errors, repo_root=repo_root)
    audit_supply_chain_and_cpm(errors, repo_root=repo_root)
    audit_architecture_and_notice_parity(errors, repo_root=repo_root)
    audit_vocabulary(errors, repo_root=repo_root)
    audit_readme_parity(errors, repo_root=repo_root)
    brief_checked = audit_portfolio_master_brief(
        errors,
        require_brief=require_brief,
        repo_root=repo_root,
        brief_file_override=brief_file_override,
    )
    return errors, brief_checked


def main(argv: Optional[List[str]] = None) -> int:
    parser = argparse.ArgumentParser(description="GradCast Claims, Architecture & Privacy Invariant Audit")
    parser.add_argument("--require-brief", action="store_true", help="Require Portfolio Master Brief to be present")
    args = parser.parse_args(argv)

    print("=== GradCast Claims, Architecture & Privacy Invariant Audit ===")
    errors, brief_checked = run_all_audits(require_brief=args.require_brief, repo_root=REPO_ROOT)

    if errors:
        print(f"\n[FAIL] Found {len(errors)} documentation / architectural claim violation(s):")
        for err in errors:
            print(f"  ✗ {err}")
        return 1

    print("\n[PASS] Audit gates passed cleanly:")
    print(f"  ✓ Bidirectional Schema & DDL parity verified across {len(EXPECTED_SCHEMA_TABLES)} tables/views")
    print("  ✓ Hard k-anonymity (k >= 5), Silver PII scrubbing & Terraform RBAC isolation verified")
    print("  ✓ Daily-salted SHA-256 client hash, trusted proxy guard & fail-closed production salt verified")
    print("  ✓ Central Package Management (CPM), fail-closed NuGetAudit & non-root Alpine Dockerfile verified")
    print("  ✓ docs/ARCHITECTURE.md & NOTICE.md domain and runtime claims parity verified")
    print("  ✓ Zero prohibited marketing vocabulary words detected across all markdown documentation")
    print("  ✓ Root README.md architectural parity (Databricks, Lakehouse, IaC, Dual-Mode) verified")
    if brief_checked:
        print("  ✓ Portfolio Master Brief parity verified (scoped to gradcast test inventory)")
    else:
        print("  - Portfolio Master Brief check skipped (external file not present in isolated environment)")
    print("\nAll GradCast claims, architecture, and governance gates verified successfully.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
