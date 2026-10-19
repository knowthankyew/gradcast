<!-- diagram-meta
  last-validated: 2026-10-19
  covers: [src/**, lakehouse/**]
  generated-by: manual
-->

# Architecture & Invariants Specification: GradCast Transparency Engine

> Full-stack higher education return-on-investment (ROI), outcome transparency platform, and public data lakehouse.

---

## 1. Executive Summary

`gradcast` is a public-data reality engine in the `knowthankyew` portfolio built to eliminate the information asymmetry between prospective college students and higher education marketing claims. Leveraging multi-year federal datasets (U.S. Department of Education College Scorecard, IPEDS, and Treasury earnings data), it pairs a Medallion lakehouse pipeline with a high-performance .NET 10 Minimal API and a Vue 3 interactive dashboard, delivering honest program-level debt-to-earnings comparisons with zero consumer data collection.

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
        Bronze["🥉 Bronze Layer<br/><i>Raw Ingestion & Parquet Snapshots</i>"]
        Silver["🥈 Silver Layer<br/><i>Cleaned, Deduplicated & Standardized CIP/OPEID</i>"]
        Gold["🥇 Gold Layer<br/><i>Pre-aggregated Debt-to-Earnings & ROI Matrices</i>"]
    end

    subgraph BackendAPI["3. .NET 10 Application Tier (:5000)"]
        Importer["⚙️ Import Job Runner<br/><code>src/import</code>"]
        NetAPI[".NET 10 Minimal API<br/><code>src/api/Program.cs</code>"]
        DataLayer["💾 Data Repository & SQLite Cache<br/><code>src/data/gradcast.db</code>"]
        Telemetry["🛡️ KnowThankYew.Privacy.Telemetry<br/><i>(ActivitySource + OTel SDK)</i>"]
    end

    subgraph FrontendSPA["4. Presentation Tier (:5173 / Production Web)"]
        VueApp["💻 Vue 3 + Vite SPA<br/><code>src/web</code>"]
        CompareArena["⚖️ Program & Degree Comparison Arena"]
        ScorecardView["📈 Scorecard & ROI Visualization"]
    end

    subgraph Consumer["5. End User"]
        Student["👤 Prospective Student / Parent"]
    end

    %% Pipeline flow
    DeptEd & IPEDS -->|"Fetch Raw Snapshots"| Importer
    Importer --> Bronze
    Bronze -->|"PySpark / Transform"| Silver
    Silver -->|"Aggregate & Index"| Gold
    Gold -->|"Load Production Tables"| DataLayer

    %% Runtime flow
    NetAPI <--> DataLayer
    NetAPI -.-> Telemetry
    VueApp <-->|"JSON REST API"| NetAPI
    VueApp --> CompareArena & ScorecardView

    Student -->|"Browses & Compares Degrees"| VueApp

    classDef src fill:#0f172a,stroke:#64748b,stroke-width:1px,color:#f8fafc;
    classDef lake fill:#14532d,stroke:#22c55e,stroke-width:2px,color:#f8fafc;
    classDef net fill:#1e293b,stroke:#38bdf8,stroke-width:2px,color:#f8fafc;
    classDef ui fill:#312e81,stroke:#818cf8,stroke-width:1px,color:#f8fafc;

    class DeptEd,IPEDS src;
    class Bronze,Silver,Gold,Importer lake;
    class NetAPI,DataLayer,Telemetry net;
    class VueApp,CompareArena,ScorecardView ui;
```

### B. Query & Transparency Lifecycle

```mermaid
sequenceDiagram
    autonumber
    actor User as Student / Parent
    participant Web as Vue 3 Dashboard
    participant API as .NET 10 Minimal API
    participant Cache as SQLite / Gold Cache
    participant Telem as KnowThankYew.Privacy.Telemetry

    User->>Web: Select Institution & Program (e.g. Nursing B.S.)
    Web->>API: GET /api/v1/programs?institutionId=...&cip=513801
    API->>Telem: StartActivity("QueryProgramRoi")
    API->>Cache: Fetch verified median debt, completion rate & 4-yr earnings
    Cache-->>API: Return audited cohort metrics
    API->>Telem: Record allowlisted duration_ms & row_count (Zero PII)
    API-->>Web: JSON Program Outcome Payload
    Web-->>User: Render debt-to-earnings ratio, net monthly cost & peer comparison
```

---

## 3. Core Architectural Invariants

| # | Invariant | Enforcement & Guarantee |
|:--|:----------|:------------------------|
| 1 | **Zero Consumer PII Collection** | Queries are anonymous; no accounts, session tracking, credit card data, or student identifiers are ingested. |
| 2 | **Public Data Integrity** | Every institution statistic is traced directly to federal OPEID / IPEDS UNITID source records. |
| 3 | **Central Package Management (CPM)** | 100% of .NET dependencies are pinned via root `Directory.Packages.props` with fail-closed vulnerability gating. |
| 4 | **Universal Multi-Stage Containerization** | Docker multi-stage Alpine build separating Node Vite assets and .NET 10 AOT runtime for minimal attack surface. |
| 5 | **Fail-Closed Dependency Auditing** | MSBuild `NuGetAudit` enabled at build time with `NU1903`/`NU1904` treated as build errors. |

---

## 4. Directory Structure

```
gradcast/
├── Directory.Build.props        # NuGetAudit & fail-closed security properties
├── Directory.Packages.props     # Central Package Management (CPM) versions
├── GradCast.slnx                # Visual Studio solution file
├── Dockerfile                   # Multi-stage production container build
├── lakehouse/                   # Medallion lakehouse ETL pipelines
│   ├── src/                     # PySpark transformations (Bronze, Silver, Gold)
│   ├── terraform/               # Cloud infrastructure definitions
│   └── tests/                   # Data quality & schema validation suites
├── src/
│   ├── api/                     # .NET 10 Minimal API (Endpoints, Middleware)
│   ├── api.tests/               # xUnit integration & endpoint test suite
│   ├── data/                    # EF Core SQLite repository & migrations
│   ├── import/                  # Federal data ingestion utilities
│   └── web/                     # Vue 3 + Vite frontend application
└── docs/
    ├── ARCHITECTURE.md          # Architecture, data flow & invariant specification
    ├── PRIVACY_TELEMETRY_SCHEMA.md # Telemetry contract specification
    └── SCHEMA.md                # Federal dataset schema mapping guide
```
