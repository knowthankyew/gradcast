"""
Unit and regression tests for the GradCast Claims, Architecture & Privacy Invariant Linter.
Verifies both positive baseline pass and negative detection of synthetic invariant regressions.
"""

from pathlib import Path
import shutil
import sys
import tempfile
import unittest
import unittest.mock

REPO_ROOT = Path(__file__).resolve().parent.parent.parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from lakehouse.scripts.audit_claims import (  # noqa: E402
    audit_architecture_and_notice_parity,
    audit_portfolio_master_brief,
    audit_privacy_invariants,
    audit_readme_parity,
    audit_schema_parity,
    audit_supply_chain_and_cpm,
    audit_vocabulary,
    main as audit_main,
    run_all_audits,
)


class TestGradCastAuditClaims(unittest.TestCase):
    def test_repository_baseline_passes_cleanly(self):
        errors, _ = run_all_audits(
            require_brief=False,
            repo_root=REPO_ROOT,
            brief_file_override=REPO_ROOT / "__nonexistent_brief__.md",
        )
        self.assertEqual(errors, [], f"Expected 0 errors on repository baseline, got: {errors}")

    def test_vocabulary_scanner_detects_prohibited_terms_and_respects_guardrail_lines(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir(parents=True)
            (root / "README.md").write_text(
                "# Clean Title\n"
                "Rule: prohibits unprovable \"bulletproof\" marketing buzzwords in documentation.\n"
                "This architecture is 100% bulletproof and infinitely scalable.\n"
                "Results are instant and conclusive even when direct queries are prohibited.\n",
                encoding="utf-8",
            )
            errors: list[str] = []
            audit_vocabulary(errors, repo_root=root)
            # Line 2 has 'prohibit' so it is skipped; lines 3 and 4 trigger 4 violations
            self.assertEqual(len(errors), 4)
            joined = "\n".join(errors)
            self.assertIn("bullet-?proof", joined)
            self.assertIn("infinitely\\s+scalable", joined)
            self.assertIn("instant", joined)
            self.assertIn("conclusive", joined)

    def test_supply_chain_and_cpm_detects_inline_csproj_version_and_missing_nuget_audit(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "Directory.Build.props").write_text("<Project></Project>", encoding="utf-8")
            (root / "Directory.Packages.props").write_text("<Project></Project>", encoding="utf-8")
            (root / "Dockerfile").write_text("FROM mcr.microsoft.com/dotnet/aspnet:10.0\n", encoding="utf-8")
            api_dir = root / "src" / "api"
            api_dir.mkdir(parents=True)
            (api_dir / "GradCast.Api.csproj").write_text(
                '<Project><ItemGroup><PackageReference Include="Dapper" Version="2.1.89" /></ItemGroup></Project>',
                encoding="utf-8",
            )

            errors: list[str] = []
            audit_supply_chain_and_cpm(errors, repo_root=root)
            joined = "\n".join(errors)
            self.assertIn("NuGetAudit", joined)
            self.assertIn("ManagePackageVersionsCentrally", joined)
            self.assertIn("inline PackageReference Version attribute", joined)
            self.assertIn("USER $APP_UID", joined)

    def test_architecture_and_notice_parity_detects_hallucinated_claims(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "docs").mkdir(parents=True)
            (root / "NOTICE.md").write_text(
                "GradCast is an academic transcript analysis and degree progress audit engine.",
                encoding="utf-8",
            )
            (root / "docs" / "ARCHITECTURE.md").write_text(
                "<!-- diagram-meta -->\n```mermaid\ngraph TD\nA-->B\n```\n"
                "Uses EF Core, .NET 10 AOT runtime, and KnowThankYew.Privacy.Telemetry.",
                encoding="utf-8",
            )

            errors: list[str] = []
            audit_architecture_and_notice_parity(errors, repo_root=root)
            joined = "\n".join(errors)
            self.assertIn("transcript analysis", joined)
            self.assertIn("EF Core", joined)
            self.assertIn("AOT runtime", joined)
            self.assertIn("KnowThankYew.Privacy.Telemetry", joined)

    def test_privacy_invariants_detects_silver_pii_leak_and_rbac_violation(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            # Copy views, endpoints, program, silver_transform, and access_control into temp tree
            for rel in (
                "lakehouse/src/pipelines/views.py",
                "lakehouse/src/pipelines/silver_transform.py",
                "lakehouse/terraform/access_control.tf",
                "src/api/Endpoints/ScenarioEndpoints.cs",
                "src/api/Program.cs",
            ):
                dst = root / rel
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(REPO_ROOT / rel, dst)

            # Inject forbidden client_hash column into silver.scenarios DDL
            silver_path = root / "lakehouse/src/pipelines/silver_transform.py"
            orig_silver = silver_path.read_text(encoding="utf-8")
            silver_text = orig_silver.replace(
                "scenario_id STRING NOT NULL,",
                "scenario_id STRING NOT NULL,\n            client_hash STRING,",
                1,
            )
            self.assertNotEqual(orig_silver, silver_text)
            silver_path.write_text(silver_text, encoding="utf-8")

            # Grant analysts access to bronze_access in access_control.tf
            rbac_path = root / "lakehouse/terraform/access_control.tf"
            orig_rbac = rbac_path.read_text(encoding="utf-8")
            rbac_text = orig_rbac.replace(
                'resource "databricks_grants" "bronze_access" {',
                'resource "databricks_grants" "bronze_access" {\n  grant {\n    principal = databricks_group.analysts.display_name\n    privileges = ["USE_SCHEMA"]\n  }',
            )
            self.assertNotEqual(orig_rbac, rbac_text)
            rbac_path.write_text(rbac_text, encoding="utf-8")

            errors: list[str] = []
            audit_privacy_invariants(errors, repo_root=root)
            joined = "\n".join(errors)
            self.assertIn("'client_hash' must be excluded from silver.scenarios DDL", joined)
            self.assertIn("analysts group must not be granted privileges in 'bronze_access'", joined)

    def test_bidirectional_schema_parity_detects_undocumented_source_column(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel in (
                "docs/SCHEMA.md",
                "src/data/SqliteDatabaseInitializer.cs",
                "lakehouse/src/pipelines/bronze_ingest.py",
                "lakehouse/src/pipelines/silver_transform.py",
                "lakehouse/src/pipelines/gold_modeling.py",
                "lakehouse/src/pipelines/views.py",
                "lakehouse/src/pipelines/export_sqlite.py",
            ):
                dst = root / rel
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(REPO_ROOT / rel, dst)

            # Add an undocumented column to gold.dim_metro in gold_modeling.py
            gold_path = root / "lakehouse/src/pipelines/gold_modeling.py"
            orig_gold = gold_path.read_text(encoding="utf-8")
            gold_text = orig_gold.replace(
                "metro_type STRING NOT NULL",
                "metro_type STRING NOT NULL,\n            undocumented_secret_col STRING",
            )
            self.assertNotEqual(orig_gold, gold_text)
            gold_path.write_text(gold_text, encoding="utf-8")

            errors: list[str] = []
            audit_schema_parity(errors, repo_root=root)
            joined = "\n".join(errors)
            self.assertIn("Undocumented column in source DDL: Column 'undocumented_secret_col'", joined)

    def test_portfolio_master_brief_scopes_match_to_gradcast_row(self):
        with tempfile.TemporaryDirectory() as tmp:
            brief_path = Path(tmp) / "KNOWTHANKYEW_PORTFOLIO_MASTER_BRIEF.md"
            # Another repo has 116/116, but gradcast has a deliberately impossible 999999/999999 count -> must fail!
            brief_path.write_text(
                "| `other-repo` | 116/116 Unit Tests |\n"
                "| `gradcast` | 999999/999999 Unit Tests |\n",
                encoding="utf-8",
            )
            errors: list[str] = []
            audit_portfolio_master_brief(
                errors,
                require_brief=True,
                repo_root=REPO_ROOT,
                brief_file_override=brief_path,
            )
            self.assertEqual(len(errors), 1)
            self.assertIn("Portfolio Brief Drift: `gradcast` entry", errors[0])

    def test_privacy_invariants_detects_databricks_url_in_live_api_tier(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for rel in (
                "lakehouse/src/pipelines/views.py",
                "lakehouse/src/pipelines/silver_transform.py",
                "lakehouse/terraform/access_control.tf",
                "src/api/Endpoints/ScenarioEndpoints.cs",
                "src/api/Program.cs",
            ):
                dst = root / rel
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(REPO_ROOT / rel, dst)

            leaked_cs = root / "src/api/LeakedDatabricksClient.cs"
            leaked_cs.write_text(
                'namespace GradCast.Api;\n'
                'public static class LeakedDatabricksClient {\n'
                '    // Even with https:// containing //, string literal must be preserved!\n'
                '    public const string Host = "https://dbc-123456.cloud.databricks.com";\n'
                '}\n',
                encoding="utf-8",
            )
            errors: list[str] = []
            audit_privacy_invariants(errors, repo_root=root)
            joined = "\n".join(errors)
            self.assertIn("Dual-mode isolation violation", joined)
            self.assertIn("LeakedDatabricksClient.cs", joined)

    def test_readme_parity_and_cli_main_exit_codes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            orig_readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
            bad_readme = orig_readme.replace(
                "- **`hybrid`** (Default Development Mode):",
                "- **`hybrid`** (Production Mode):",
            )
            self.assertNotEqual(orig_readme, bad_readme)
            (root / "README.md").write_text(bad_readme, encoding="utf-8")
            errors: list[str] = []
            audit_readme_parity(errors, repo_root=root)
            self.assertTrue(any("Incompatible data mode 'hybrid' designated for production" in e for e in errors))

        with unittest.mock.patch(
            "lakehouse.scripts.audit_claims.run_all_audits",
            side_effect=lambda **kwargs: run_all_audits(
                **kwargs, brief_file_override=REPO_ROOT / "__nonexistent_brief__.md"
            ),
        ):
            self.assertEqual(audit_main([]), 0)
        with unittest.mock.patch(
            "lakehouse.scripts.audit_claims.run_all_audits",
            return_value=(["synthetic violation"], False),
        ):
            self.assertEqual(audit_main([]), 1)


if __name__ == "__main__":
    unittest.main()
