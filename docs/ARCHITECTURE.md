<!-- diagram-meta
  last-validated: 2026-10-27
  validated-commit: 282c416
  covers: [src/**, lakehouse/**]
  generated-by: manual
-->

# Architecture & Invariants Specification: GradCast Transparency Engine

> Full-stack higher education return-on-investment (ROI), outcome transparency platform, and public data lakehouse.

---

## 1. Executive Summary

`gradcast` is a public-data reality engine in the `knowthankyew` portfolio built to eliminate the information asymmetry between prospective college students and higher education marketing claims. Leveraging multi-year federal datasets (U.S. Department of Education College Scorecard, IPEDS, and Treasury earnings data), it pairs a Medallion lakehouse pipeline with a high-performance .NET 10 Minimal API and a Vue 3 interactive dashboard—processing personal simulation inputs transiently without server persistence, keeping personal saved scenarios in browser `localStorage`, and persisting only voluntarily shared, anonymized scenarios (never raw IP addresses) under hard $k \ge 5$ cohort suppression.

---

## 2. System Architecture & Data Flow

### A. System Architecture & Lakehouse Pipeline

```mermaid
flowchart TD
    subgraph PublicDataSources["1. Federal & Institutional Public Data"]
        DeptEd["🏛️ US Department of Education<br/><i>(College Scorecard Multi-Year CSVs)</i>"]
        IPEDS["📊 NCES IPEDS Data<br/><i>(Completions, Tuitions, Retention)</i>"]
    end

    subgraph LakehousePipeline["2. Lakehouse Engineering (Medallion Pattern)"]
        Bronze["🥉 Bronze Layer<br/><code>lakehouse/src/pipelines/bronze_ingest.py</code>"]
        Silver["🥈 Silver Layer<br/><code>lakehouse/src/pipelines/silver_transform.py</code>"]
        Gold["🥇 Gold Layer &amp; k-Anonymity View (k &ge; 5)<br/><code>lakehouse/src/pipelines/views.py</code>"]
        ReverseETL["🔄 Reverse-ETL SQLite Compiler<br/><code>lakehouse/src/pipelines/export_sqlite.py</code>"]
    end

    subgraph BackendAPI["3. .NET 10 Application Tier (:5062)"]
        Importer["⚙️ Import Job Runner<br/><code>src/import</code>"]
        NetAPI[".NET 10 Minimal API<br/><code>src/api/Program.cs</code>"]
        DataLayer["💾 Dapper Repository &amp; SQLite Cache<br/><code>src/data/SqliteDatabaseInitializer.cs</code>"]
        PrivacyGuard["🛡️ Correlation &amp; Daily-Salted SHA-256 Guard<br/><code>src/api/Middleware/RequestCorrelationMiddleware.cs</code><br/><code>src/api/Endpoints/ScenarioEndpoints.cs</code>"]
    end

    subgraph FrontendSPA["4. Presentation Tier (:5173 / Production Web)"]
        VueApp["💻 Vue 3 + Vite SPA<br/><code>src/web</code>"]
        CompareArena["⚖️ Program &amp; Degree Comparison Arena"]
        ScorecardView["📈 Scorecard &amp; ROI Visualization"]
    end

    subgraph Consumer["5. End User"]
        Student["👤 Prospective Student / Parent"]
    end

    %% Pipeline flow
    DeptEd & IPEDS -->|"Fetch Raw Snapshots"| Importer
    Importer --> Bronze
    Bronze -->|"PySpark / Transform"| Silver
    Silver -->|"Aggregate &amp; k >= 5 Filter"| Gold
    Gold -->|"Compile Edge Benchmarks"| ReverseETL
    ReverseETL -->|"Atomic SQLite Swap"| DataLayer

    %% Runtime flow
    NetAPI <--> DataLayer
    NetAPI -.-> PrivacyGuard
    VueApp <-->|"JSON REST API"| NetAPI
    VueApp --> CompareArena & ScorecardView

    Student -->|"Browses &amp; Compares Degrees"| VueApp

    classDef src fill:#0f172a,stroke:#64748b,stroke-width:1px,color:#f8fafc;
    classDef lake fill:#14532d,stroke:#22c55e,stroke-width:2px,color:#f8fafc;
    classDef net fill:#1e293b,stroke:#38bdf8,stroke-width:2px,color:#f8fafc;
    classDef ui fill:#312e81,stroke:#818cf8,stroke-width:1px,color:#f8fafc;

    class DeptEd,IPEDS src;
    class Bronze,Silver,Gold,ReverseETL,Importer lake;
    class NetAPI,DataLayer,PrivacyGuard net;
    class VueApp,CompareArena,ScorecardView ui;
```

### B. Query & Transparency Lifecycle

```mermaid
sequenceDiagram
    autonumber
    actor User as Student / Parent
    participant Web as Vue 3 Dashboard
    participant API as .NET 10 Minimal API
    participant Guard as RequestCorrelationMiddleware / ScenarioEndpoints
    participant Cache as Local SQLite (gradcast.db)

    User->>Web: Select Institution, Program & Metro Area
    Web->>API: GET /api/schools/{id} & POST /api/finance/simulator
    API->>Guard: Attach X-Correlation-ID & W3C TraceId
    API->>Cache: Fetch Dapper-mapped median debt, earnings, HUD FMR & k >= 5 benchmarks
    Cache-->>API: Return audited cohort metrics (sub-millisecond local read)
    API->>Guard: Log structured request method, path, status & duration (Zero PII)
    API-->>Web: JSON Budget Simulation & Program Outcome Payload
    Web-->>User: Render net take-home pay, rent, loan payment & disposable ratio
```

---

## 3. Core Architectural Invariants

| # | Invariant | Enforcement & Guarantee |
|:--|:----------|:------------------------|
| 1 | **No Raw PII Persistence & Hard k-Anonymity (`k >= 5`)** | Personal simulation inputs are processed transiently without server persistence and personal saved scenarios remain in browser `localStorage`; when scenarios are voluntarily shared, raw IP addresses are never persisted—only a daily-rotating salted `SHA256` digest is stored at the edge for rate limiting, removed prior to `silver.scenarios` conformance, and `HAVING COUNT(*) >= 5` suppresses small cohorts in `v_public_positive_finds`. |
| 2 | **Dual-Mode Edge Read Isolation** | All production web reads are served from local SQLite (`gradcast.db`) via Dapper; direct Databricks cluster queries during live web traffic are prohibited. |
| 3 | **Central Package Management (CPM)** | 100% of .NET dependencies are pinned via root `Directory.Packages.props` with zero inline `.csproj` version overrides. |
| 4 | **Universal Multi-Stage Containerization** | Docker multi-stage Alpine build (`Dockerfile`) separating Node 22 Vite asset compilation and .NET 10 ASP.NET Core runtime executing under non-root `USER $APP_UID`. |
| 5 | **Fail-Closed Dependency Auditing** | MSBuild `NuGetAudit` enabled at build time (`Directory.Build.props`) with `NU1903`/`NU1904` treated as build errors. |

---

## 4. Directory Structure

```
gradcast/
├── Directory.Build.props        # NuGetAudit & fail-closed security properties
├── Directory.Packages.props     # Central Package Management (CPM) versions
├── GradCast.slnx                # Visual Studio solution file
├── Dockerfile                   # Multi-stage production container build
├── scripts/
│   └── audit-claims.py          # Portfolio-standard claims & invariant linter entrypoint
├── lakehouse/                   # Medallion lakehouse ETL pipelines
│   ├── src/                     # PySpark transformations (Bronze, Silver, Gold, Views, Reverse-ETL)
│   ├── terraform/               # Unity Catalog & RBAC infrastructure definitions
│   ├── scripts/                 # Lakehouse claims, DDL parity & privacy invariant implementation
│   └── tests/                   # Data quality, schema & claims validation suites
├── src/
│   ├── api/                     # .NET 10 Minimal API (Endpoints, Middleware)
│   ├── api.tests/               # xUnit integration & endpoint test suite
│   ├── data/                    # Dapper SQLite repository, schema DDL & reference seeders
│   ├── import/                  # Federal data ingestion utilities
│   └── web/                     # Vue 3 + Vite frontend application
└── docs/
    ├── ARCHITECTURE.md          # Architecture, data flow & invariant specification
    ├── PRIVACY_TELEMETRY_SCHEMA.md # Telemetry contract specification
    ├── QA_CHECKLIST.md          # Manual & automated QA verification runbook
    └── SCHEMA.md                # Unity Catalog & SQLite schema DDL specification
```
