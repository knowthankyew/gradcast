# GradCast Lakehouse — Strategic Architecture & Dual-Mode Roadmap

> **Portfolio Alignment**: Civic Reality Engine & Higher Education Return-on-Investment (ROI) Platform  
> **Governance Standard**: `knowthankyew` Safe Allowlist Invariant & $k$-Anonymity Cloud Boundary ($k \ge 5$)  
> **Technical Stack**: ASP.NET Core 10 / Dapper / SQLite 3 (Edge) ⇄ PySpark / Delta Lake / Unity Catalog / Terraform / DABs (Lakehouse)  
> **Schema Reference**: [`docs/SCHEMA.md`](docs/SCHEMA.md)

---

## 1. Architectural Strategy & Executive Summary

GradCast is designated in the `knowthankyew` Portfolio Master Brief as an **inherently public civic open-data engine**. It bridges the gap between college search tools and real post-graduation finances by modeling program-level median earnings against HUD Fair Market Rents, student loan amortization, and tax burdens.

To expand GradCast into an enterprise portfolio piece showcasing **Databricks as the Single Source of Truth**, the system adopts a **Dual-Mode Storage Architecture**:

1. **Edge / Local / Dirt-Cheap Tier (SQLite 3)**:
   - Preserves zero-dependency local execution (`./start.sh`) and minimal-cost cloud hosting (Fly.io single-container deployment).
   - Ingests and stores anonymous user-shared "positive finds" (budget scenarios with verified disposable surplus) via an operational table (`shared_scenarios`).
   - Serves pre-computed macro-benchmarks without requiring live cloud compute.
2. **Lakehouse Analytical Tier (Databricks / Unity Catalog)**:
   - Functions as the enterprise **Single Source of Truth (SSOT)**.
   - Ingests raw public datasets (multi-gigabyte historical U.S. College Scorecard archives, IPEDS microdata, HUD FMR data) and replicated operational scenarios.
   - Implements a modern **Medallion Architecture** (Bronze $\to$ Silver $\to$ Gold) using **Liquid Clustering**, **Delta Change Data Feed (CDF)**, and **$k$-Anonymity Row Filters**.
3. **The Reverse-ETL Bridge**:
   - A scheduled PySpark pipeline compiles Gold dimensional aggregates into an updated, compressed, read-optimized `gradcast.db` artifact, distributed directly to the edge.

---

## 2. Assessment: The Critical Value of Claims Auditing

In modern Data Platform, ML, and Lakehouse engineering, **Documentation Drift** is the single most pervasive operational failure:
- Documentation claims a pipeline clusters on `(cip_code, cbsa_code)`, but code was modified to cluster on `(year, state)`.
- Documentation promises $k$-anonymity suppression ($k \ge 5$) to protect students in small programs, but a query change leaks unaggregated records.
- Documentation advertises 100% test coverage or specific SLA query latencies that no longer hold.

### The Portfolio Pattern: `lakehouse/scripts/audit_claims.py`
Following the proven pattern from `mac-screen-record` and `event-driven-ftaas` (`ml/scripts/sync_docs.py`), GradCast Lakehouse includes an automated **Claims & Architecture Linter** (`lakehouse/scripts/audit_claims.py`). Executed in GitHub Actions CI before any merge, it enforces:

1. **Schema & DDL Parity**: Validates that all tables, columns, constraints, and Liquid Clustering keys declared in `docs/SCHEMA.md` and `ROADMAP.md` match the actual PySpark/SQL DDL and SQLite migrations.
2. **Invariant & Privacy Enforcement**: Statically scans data transformations for the $k \ge 5$ filter, verifies that client identifiers are hashed/salted, and guarantees that no raw PII fields enter Silver or Gold.
3. **Dual-Mode Contract Parity**: Confirms that the schema exported by the Reverse-ETL job matches the schema expected by the ASP.NET Core Dapper entities.
4. **Vocabulary & Assertion Lint**: Prohibits unprovable marketing adjectives (*"bulletproof"*, *"instant"*, *"infinitely scalable"*) while strictly guarding empirical terms (*"verified"*, *"ACID"*, *"100%"*).

---

## 3. Master Milestone Grid

| Phase | Milestone | Focus Area | Deliverables & Scope | Status |
| :--- | :--- | :--- | :--- | :---: |
| **Stage 1** | **Edge SQLite Social Sharing Layer** | Edge / OLTP | Operational `shared_scenarios` table, Dapper repo, ASP.NET Core Minimal API endpoint with strict input allowlist, Vuetify share button. | ✅ **Completed** |
| **Stage 2** | **Lakehouse Foundation & IaC** | Cloud / Governance | Terraform Unity Catalog provisioning (catalogs, schemas, service principals), Databricks Asset Bundle (`databricks.yml`), OAuth M2M authentication. | ✅ **Completed** |
| **Stage 3** | **Bronze & Silver Medallion Pipelines** | Data Engineering | PySpark Auto Loader raw ingestion, Silver cleansing/conformance, Delta constraints, Liquid Clustering (`CLUSTER BY`), Delta Change Data Feed (CDF). | ✅ **Completed** |
| **Stage 4** | **Gold Dimensional Modeling & Reverse-ETL** | Analytics / Serving | Star schema (`fact_scenario_outcomes`, `dim_program`, `dim_school`, `dim_metro`), $k$-anonymity row-level filter view ($k \ge 5$), PySpark-to-SQLite compiler. | ✅ **Completed** |
| **Stage 5** | **Observability & Lakehouse Monitoring** | SRE / Data Quality | Delta Live Tables expectations, schema enforcement contracts, data drift tracking, and OpenTelemetry pipeline metrics. | ✅ **Completed** |
| **Stage 6** | **Testing Harness & Verification** | Quality Engineering | Isolated unit testing across Silver transforms, $k$-anonymity view generation, Reverse-ETL SQLite compiler, and ASP.NET Core API suite. | ✅ **Completed** |
| **Stage 7** | **Claims Auditing & Supply Chain Gate** | Governance / CI | Automated `lakehouse/scripts/audit_claims.py` documentation linter and CycloneDX SBOM generation (`bom.json`) for multi-language supply chain security. | ✅ **Completed** |

---

## 4. Isolated Implementation Stories (Jira-Tailored for AI Coders)

```
================================================================================
STORY GC-LAKE-01: Edge SQLite Extension & Social Sharing Ingest Layer
================================================================================
```

### Context & Motivation
GradCast currently simulates post-grad budgets entirely client-side or reads historical Scorecard data from SQLite. To enable community insights without cloud costs, we need a lightweight, edge-native way for users to anonymously share "positive finds" (scenarios where monthly disposable income is positive).

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Add `shared_scenarios` table to `gradcast.db` via safe SQLite migration.
  - Implement C# Dapper entity `SharedScenario` and repository method `InsertScenarioAsync`.
  - Expose `POST /api/scenarios/share` Minimal API endpoint with strict model validation and daily client-hash rate limiting.
  - Expose `GET /api/scenarios/positive-finds` returning top shared scenarios filtered by metro and CIP category.
  - Unit tests verifying SQLite persistence and constraint enforcement.
- **OUT OF SCOPE**: Direct Databricks calls from the web app; user accounts/passwords (scenarios are 100% anonymous).

### Technical Specification
1. **Schema DDL**: Execute migration in SQLite (see [`docs/SCHEMA.md`](docs/SCHEMA.md#32-new-table-shared_scenarios-edge-operational-ingest)).
2. **Files to Modify/Create**:
   - `src/GradCast.Api/Models/SharedScenario.cs`: Record defining payload matching `SAFE_ALLOWLIST_KEYS`.
   - `src/GradCast.Api/Data/IScenarioRepository.cs` & `ScenarioRepository.cs`: Dapper implementation.
   - `src/GradCast.Api/Endpoints/ScenarioEndpoints.cs`: Route group `/api/scenarios`.
   - `tests/GradCast.Api.Tests/ScenarioEndpointTests.cs`: xUnit integration tests.

### Acceptance Criteria
- [x] `POST /api/scenarios/share` rejects payloads with negative salary or disposable income below $-\$20,000$.
- [x] Client identity is never recorded; `client_hash` is computed as `SHA256(Date.UtcNow.ToString("yyyy-MM-dd") + IP + Salt)`.
- [x] Anonymized scenarios persist into `gradcast.db` and query cleanly via Dapper in $< 5\text{ms}$.
- [x] Existing 78/78 xUnit backend tests continue to pass (now 112/112 total).

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-01 in /Users/cl0rkster/Dev/gradcast.
1. Add the "shared_scenarios" table definition to the SQLite database schema in accordance with docs/SCHEMA.md.
2. Create src/GradCast.Api/Models/SharedScenario.cs with required validation attributes.
3. Create src/GradCast.Api/Data/ScenarioRepository.cs using Dapper for inserting and querying scenarios.
4. Register the repository in Program.cs and create src/GradCast.Api/Endpoints/ScenarioEndpoints.cs with POST /api/scenarios/share and GET /api/scenarios/positive-finds.
5. Add unit tests in tests/GradCast.Api.Tests verifying valid insert, validation rejection, and sanitized query outputs.
Run 'dotnet test' to ensure all tests pass.
```

---

```
================================================================================
STORY GC-LAKE-02: Lakehouse Foundation & Declarative Infrastructure as Code (IaC)
================================================================================
```

### Context & Motivation
To establish Databricks as the enterprise Single Source of Truth, all Lakehouse resources (Unity Catalog metastore, catalogs, schemas, service principals, and compute) must be declared in code using Terraform and Databricks Asset Bundles (DABs), avoiding manual click-ops.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Terraform root module under `lakehouse/terraform/` defining `gradcast_lakehouse` catalog, 3 schemas (`bronze`, `silver`, `gold`), and volume storage credentials.
  - Service principal configuration with OAuth M2M authentication for CI/CD runners.
  - Databricks Asset Bundle definition (`lakehouse/databricks.yml`) declaring pipelines and environments (`dev`, `prod`).
  - GitHub Actions validation workflow running `databricks bundle validate` and `terraform fmt/validate`.
- **OUT OF SCOPE**: Live cloud billing provisioning in CI; production credential storage in Git.

### Technical Specification
1. **Directories**:
   - `lakehouse/terraform/main.tf`: Databricks provider configuration.
   - `lakehouse/terraform/unity_catalog.tf`: Catalogs (`gradcast_lakehouse`), schemas (`bronze`, `silver`, `gold`), external locations.
   - `lakehouse/terraform/access_control.tf`: Groups and privilege grants.
   - `lakehouse/databricks.yml`: DAB configuration referencing tasks and pipelines.
2. **Authentication Standards**:
   - Zero Personal Access Tokens (PATs). Use OAuth machine-to-machine (`ARM_CLIENT_ID` / `DATABRICKS_CLIENT_ID`).

### Acceptance Criteria
- [x] `terraform validate` passes with zero warnings.
- [x] `databricks bundle validate` succeeds against bundle schema specifications.
- [x] Unity Catalog 3-level namespace (`gradcast_lakehouse.<schema>.<table>`) strictly enforced.
- [x] Storage locations use cloud object storage abstractions with zero hardcoded access keys.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-02 under /Users/cl0rkster/Dev/gradcast/lakehouse.
1. Create directory structure: lakehouse/terraform/ and lakehouse/src/.
2. Write lakehouse/terraform/main.tf with provider "databricks" (>= 1.40.0).
3. Write lakehouse/terraform/unity_catalog.tf declaring catalog "gradcast_lakehouse" and schemas "bronze", "silver", "gold".
4. Write lakehouse/terraform/access_control.tf granting appropriate service principal roles.
5. Create lakehouse/databricks.yml configuring a Databricks Asset Bundle for "gradcast_lakehouse_bundle" with dev and prod targets.
6. Add a GitHub Actions check in .github/workflows/lakehouse-ci.yml validating Terraform and DAB configurations.
```

---

```
================================================================================
STORY GC-LAKE-03: Medallion Ingestion & Cleansing Pipelines (Bronze -> Silver)
================================================================================
```

### Context & Motivation
The Lakehouse ingests two streams: high-volume public reference data (College Scorecard historical archives) and operational scenario shares. The Silver layer must conform, deduplicate, and index this data for sub-second analytical queries.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Bronze Auto Loader ingestion script reading raw JSON scenario payloads and Scorecard CSVs into append-only Delta tables.
  - Silver transformation pipeline written in PySpark (Python 3.12).
  - Enable **Liquid Clustering** on `silver.scenarios` using `CLUSTER BY (cip_family, cbsa_code)`.
  - Enable **Delta Change Data Feed (CDF)** (`delta.enableChangeDataFeed = true`).
  - Implement Delta table constraints (`CHECK reported_annual_salary >= 10000.00`).
- **OUT OF SCOPE**: Training machine learning models (handled in Gold).

### Technical Specification
1. **Source Code**:
   - `lakehouse/src/pipelines/bronze_ingest.py`: Auto Loader streaming / batch ingestion.
   - `lakehouse/src/pipelines/silver_transform.py`: Cleansing, type coercion, deduplication with `MERGE INTO`.
2. **Schema Reference**: [`docs/SCHEMA.md` Section 4.1 & 4.2](docs/SCHEMA.md#4-databricks-lakehouse-medallion-schema-delta-lake--unity-catalog).

### Acceptance Criteria
- [x] Bronze tables configured with `delta.appendOnly = true`.
- [x] Silver tables utilize Liquid Clustering on `(cip_family, cbsa_code)` verified via `DESCRIBE DETAIL`.
- [x] Delta Change Data Feed emits `insert`, `update_postimage`, and `delete` events cleanly.
- [x] PySpark pipeline handles dirty data (missing rent, non-numeric earnings) by writing to an audit quarantine without terminating execution.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-03 in /Users/cl0rkster/Dev/gradcast/lakehouse/src/pipelines/.
1. Write bronze_ingest.py implementing PySpark batch/streaming ingestion for raw scenario JSON and Scorecard CSV into bronze.shared_scenarios and bronze.college_scorecard.
2. Write silver_transform.py performing:
   - Data validation and casting to decimal types.
   - Derivation of cip_family (first 2 digits of cip_code).
   - Deduplication on scenario_id using Delta MERGE.
   - Table creation with Liquid Clustering: CLUSTER BY (cip_family, cbsa_code).
   - Setting table property 'delta.enableChangeDataFeed' = 'true'.
3. Include data constraint declarations as specified in docs/SCHEMA.md.
```

---

```
================================================================================
STORY GC-LAKE-04: Gold Dimensional Modeling, $k$-Anonymity & Reverse-ETL SQLite Compiler
================================================================================
```

### Context & Motivation
To prevent user re-identification and supply GradCast's public edge tier with fresh benchmarks, the Gold layer must construct a star schema, enforce $k$-anonymity ($k \ge 5$) via a secure view, and compile Gold data into a deployable SQLite database file.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Star schema DDL & pipeline: `fact_scenario_outcomes`, `dim_program`, `dim_school`, `dim_metro`.
  - Secure View: `v_public_positive_finds` with strict `HAVING COUNT(*) >= 5` filter.
  - Reverse-ETL export script: `scripts/export_gold_to_sqlite.py` querying Gold and generating a compressed `gradcast_gold.db` SQLite artifact.
- **OUT OF SCOPE**: Exposing unaggregated student-level microdata.

### Technical Specification
1. **Source Code**:
   - `lakehouse/src/pipelines/gold_modeling.py`: Populates fact and dimension tables.
   - `lakehouse/src/pipelines/views.py`: Declares $k$-anonymity views.
   - `lakehouse/src/pipelines/export_sqlite.py`: Reverse-ETL SQLite compiler.
2. **Schema Reference**: [`docs/SCHEMA.md` Section 4.3 & 5](docs/SCHEMA.md#43-gold-layer-dimensional-star-schema--k-anonymity-marts).

### Acceptance Criteria
- [x] Queries on `v_public_positive_finds` return zero results for any `(cbsa_code, cip_family)` cohort with fewer than 5 scenarios.
- [x] Reverse-ETL compiler produces a valid, queryable SQLite file containing table `community_scenario_benchmarks`.
- [x] Export script executes deterministically and includes SQLite index creation for edge performance.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-04 in /Users/cl0rkster/Dev/gradcast/lakehouse/src/pipelines/.
1. Write gold_modeling.py to populate fact_scenario_outcomes and dimensions (dim_school, dim_program, dim_metro).
2. Create views.py implementing v_public_positive_finds with the strict HAVING COUNT(*) >= 5 filter from docs/SCHEMA.md.
3. Write export_sqlite.py to query v_public_positive_finds and write community_scenario_benchmarks into a standalone SQLite DB using Python's sqlite3 and pandas/DuckDB.
4. Add unit test verifying that cohorts with count < 5 are omitted from the export.
```

---

```
================================================================================
STORY GC-LAKE-05: Lakehouse Observability, Data Quality & Continuous Monitoring
================================================================================
```

### Context & Motivation
Enterprise single sources of truth require continuous data quality verification. If the College Scorecard changes reporting formats or invalid scenarios enter the system, alerts must trigger before corrupted data reaches Gold or SQLite.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Delta Live Tables (DLT) Expectations / Great Expectations suite validating schema and data boundaries.
  - Lakehouse Monitoring configuration tracking null rates, distribution shifts in reported salaries, and pipeline latency.
  - OpenTelemetry structured tracing events emitted during pipeline runs conforming to `knowthankyew` telemetry invariants.
- **OUT OF SCOPE**: Third-party commercial monitoring vendors (Datadog/PagerDuty).

### Technical Specification
1. **Source Code**:
   - `lakehouse/src/quality/expectations.py`: DLT expectation rules (`expect_valid_earnings`, `expect_valid_cbsa`).
   - `lakehouse/src/monitoring/lakehouse_monitor.py`: Automated profile and drift monitor setup.
2. **Core Contracts**:
   - `expect_or_drop`: Invalid records dropped to quarantine.
   - `expect_or_fail`: Schema corruption halts execution immediately.

### Acceptance Criteria
- [x] Data quality checks execute in $< 30\text{s}$ over 100,000 records.
- [x] Quarantined records write to `quarantine.scenarios_rejected` with failure reason string.
- [x] OpenTelemetry trace attributes adhere strictly to `SAFE_ALLOWLIST_KEYS`.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-05 in /Users/cl0rkster/Dev/gradcast/lakehouse/src/quality/.
1. Write expectations.py implementing data quality rules using PySpark / DLT expectations or Great Expectations.
2. Define expectations for salary boundaries, valid CIP codes, and non-empty metro mappings.
3. Write an quarantine router directing failed records to silver_quarantine with error classification.
4. Integrate OpenTelemetry span emitting using KnowThankYew.Privacy.Telemetry standards.
```

---

```
================================================================================
STORY GC-LAKE-06: Google-Grade Testing Harness (PySpark + Chispa)
================================================================================
```

### Context & Motivation
A core requirement for lead/architect roles at companies like Google is rigorous automated verification. Spark transformations must be verified with isolated unit tests on local compute without requiring active cloud clusters.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Fast, deterministic unit test harness using `unittest` and `pytest`.
  - Record-level validation fixtures testing null quarantine, boundaries, and mathematical precision.
  - Verification of strict $k$-anonymity failsafe clamp ($k \ge 5$) in `views.py`.
  - End-to-end integration test validating the Reverse-ETL SQLite compiler and schema in CI.
- **OUT OF SCOPE**: Slow tests requiring live cloud connectivity.

### Technical Specification
1. **Source Code**:
   - `lakehouse/tests/conftest.py`: Test environment configuration.
   - `lakehouse/tests/unit/test_silver_transform.py`: Silver validation, quarantine reasons, and boundary assertions.
   - `lakehouse/tests/unit/test_k_anonymity.py`: Strict validation of the $k \ge 5$ boundary and view DDL generation.
   - `lakehouse/tests/e2e/test_reverse_etl.py`: Exercises `export_gold_to_sqlite`, SQLite VACUUM, and covering indexes.

### Acceptance Criteria
- [x] All unit and integration tests execute locally via `python3 -m unittest discover -s lakehouse/tests` in $< 5\text{s}$.
- [x] Tests run cleanly in GitHub Actions CI with zero cloud credentials.
- [x] Reverse-ETL compiler verifies atomic replace and SQLite query performance.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-06 in /Users/cl0rkster/Dev/gradcast/lakehouse/tests/.
1. Setup pytest configuration with conftest.py providing a local SparkSession with Delta Lake packages configured.
2. Install chispa and write unit tests for:
   - Deduplication and Liquid Clustering key derivation in silver_transform.py.
   - Mathematical precision of monthly_disposable and disposable_ratio.
   - Strict k-anonymity suppression in v_public_positive_finds.
3. Add e2e test verifying that the export_sqlite script creates a readable SQLite database matching schema expectations.
Run 'pytest lakehouse/tests/' and ensure 100% pass rate.
```

---

```
================================================================================
STORY GC-LAKE-07: Automated Claims Auditing, Documentation Linter & CI Security Gate
================================================================================
```

### Context & Motivation
To ensure the codebase, documentation, schemas, and CI supply chain remain in absolute alignment, we implement an automated documentation claims linter (`lakehouse/scripts/audit_claims.py`) and a supply-chain attestation workflow.

### Scope & Explicit Boundaries
- **IN SCOPE**:
  - Python script `lakehouse/scripts/audit_claims.py` verifying:
    1. Schema table and column name parity between `docs/SCHEMA.md`, `ROADMAP.md`, and SQL/PySpark code.
    2. Presence of the $k \ge 5$ invariant in Gold view definitions.
    3. Absence of prohibited adjectives (*"bulletproof"*, *"instant"*, *"conclusive"*).
    4. Guarded word verification (*"verified"*, *"ACID"*, *"100%"*).
  - GitHub Actions CI workflow generating CycloneDX SBOM (`bom.json`) across Python, .NET, and frontend dependencies.
- **OUT OF SCOPE**: Manual audit reviews.

### Technical Specification
1. **Source Code**:
   - `lakehouse/scripts/audit_claims.py`: AST and regex-based claims linter.
   - `.github/workflows/lakehouse-ci.yml`: Unified CI gate.

### Acceptance Criteria
- [x] `python3 lakehouse/scripts/audit_claims.py` exits `0` on parity, exits `1` with clear diff on drift.
- [x] CycloneDX SBOM (`bom.json`) generated automatically for Python and .NET dependencies.
- [x] CI pipeline fails-closed if any claim, invariant, or test fails.

### AI Coder Execution Prompt
```text
Implement Story GC-LAKE-07 in /Users/cl0rkster/Dev/gradcast.
1. Create lakehouse/scripts/audit_claims.py adapting the portfolio pattern from mac-screen-record and ml/scripts/sync_docs.py:
   - Parse docs/SCHEMA.md and verify all tables/columns exist in pipeline DDL.
   - Check that the k >= 5 filter is present in views.py.
   - Scan markdown docs for prohibited words ('bulletproof', 'conclusive', 'definitive').
2. Update .github/workflows/lakehouse-ci.yml to run:
   - Ruff linting and mypy type checks.
   - Pytest unit and e2e tests.
   - audit_claims.py validation gate.
   - CycloneDX SBOM generation.
Verify by running 'python3 lakehouse/scripts/audit_claims.py'.
```

---

## 5. Summary & Context Resumption Guide

When returning to this project across context windows or assigning tasks to an AI coder:
1. **Pick the Active Story**: Follow the Story ID (`GC-LAKE-01` through `GC-LAKE-07`).
2. **Execute the Prompt**: Copy the isolated **AI Coder Execution Prompt** into the prompt window.
3. **Verify the Story**: Run the corresponding verification commands before declaring the stage complete.
4. **Run Claims Audit**: Always run `python3 lakehouse/scripts/audit_claims.py` to ensure docs and code remain synchronized.
