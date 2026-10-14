# GradCast Lakehouse & Dual-Mode Schema Specification

> **Status**: Approved / Draft for Implementation  
> **Target Systems**: SQLite 3 (Edge / Zero-Cost Serving) & Databricks Delta Lake (Unity Catalog Single Source of Truth)  
> **Privacy Governance**: `knowthankyew` Safe Allowlist Invariant & $k$-Anonymity ($k \ge 5$)

---

## 1. Architectural Strategy: The Dual-Mode Storage Model

GradCast operates under a **Dual-Mode Storage Architecture**:
1. **Edge / Local / Dirt-Cheap Tier (SQLite)**: Serves live web traffic on fly.io or local developer machines (`./start.sh`) with sub-millisecond read latency, zero cloud infrastructure cost, and embedded file-based persistence (`gradcast.db`).
2. **Lakehouse Analytical Tier (Databricks Delta Lake)**: The enterprise single source of truth. Ingests raw public datasets (multi-gigabyte multi-year U.S. College Scorecard archives, IPEDS microdata, HUD Fair Market Rents) and replicated anonymous user scenarios. Executes Medallion transformations, enforces data quality contracts, runs k-anonymity privacy transforms, and compiles analytical Gold marts.
3. **Reverse-ETL Bridge**: Databricks compiles Gold dimensional aggregates into an updated, compressed, read-optimized `gradcast.db` artifact distributed back to the web tier.

```
+-------------------------------------------------------------------------------+
|                             GRADCAST WEB APPLICATION                         |
|                       (ASP.NET Core 10 / Vue 3 Frontend)                      |
+-------------------------------------------------------------------------------+
       ▲                                                 │
       │ Reads pre-computed                              │ Writes anonymous
       │ benchmarks & lookups                            │ positive find scenarios
       │                                                 ▼
+-------------------------+                     +-------------------------------+
|  SQLite: Serving Edge   |                     |  SQLite: Staging Buffer /     |
|  (gradcast.db)          |                     |  Direct Web Ingestion API     |
+-------------------------+                     +-------------------------------+
       ▲                                                 │
       │ Reverse-ETL export                              │ Batch / CDC Ingestion
       │ (Compact SQLite compiler)                       │ (Auto Loader)
+───────────────────────────────────────────────────────────────────────────────+
|                  DATABRICKS LAKEHOUSE (SINGLE SOURCE OF TRUTH)                |
|                                                                               |
|  [BRONZE]  bronze_shared_scenarios   bronze_college_scorecard   bronze_hud_fmr |
|     │                                                                         |
|     ▼ Liquid Clustering CLUSTER BY (cip_family, cbsa_code) + Delta CDF           |
|  [SILVER]  silver_scenarios          silver_programs           silver_schools |
|     │                                                                         |
|     ▼ k-Anonymity Row Filter (k >= 5) + Star Schema Modeling                  |
|  [GOLD]    fact_scenario_outcomes    dim_program   dim_school   dim_metro     |
+───────────────────────────────────────────────────────────────────────────────+
```

---

## 2. Type & Optimization Mapping Matrix

| Logical Concept | SQLite (Edge / Local) | PostgreSQL (Reference OLTP) | Databricks Delta Lake (Lakehouse SSOT) | Optimization Strategy |
| :--- | :--- | :--- | :--- | :--- |
| **Identifier** | `INTEGER PRIMARY KEY AUTOINCREMENT` or `TEXT` (UUID) | `BIGSERIAL` / `UUID` | `BIGINT GENERATED ALWAYS AS IDENTITY` or `STRING` | Spark Identity column / UUID |
| **Monetary Values** | `INTEGER` (cents or whole dollars) | `NUMERIC(12, 2)` or `INTEGER` | `DECIMAL(12, 2)` | Exact arithmetic, no float rounding drift |
| **Ratios / Rates** | `REAL` (floating point) | `DOUBLE PRECISION` | `DOUBLE` | IEEE 754 precision |
| **Timestamps** | `TEXT` (ISO-8601 UTC) | `TIMESTAMPTZ` | `TIMESTAMP` | UTC normalized |
| **Indexing** | B-Tree (`CREATE INDEX`) | B-Tree, BRIN | **Liquid Clustering** (`CLUSTER BY`) + Parquet file statistics | File skipping without small-file partition explosions |
| **Deduplication** | `UNIQUE` constraints | `UNIQUE` constraints / B-Tree | `MERGE INTO` on primary business keys | Delta ACID transaction log |
| **Change Capture** | Application timestamps | Logical decoding / WAL | **Delta Change Data Feed (CDF)** | `delta.enableChangeDataFeed = true` |

---

## 3. SQLite Schema (Edge / Local Mode)

This schema extends the existing 6-table `gradcast.db` database to support the social sharing of positive financial finds while remaining 100% compatible with existing Dapper queries.

### 3.1 Existing Reference Tables (Preserved)
- `schools` (id, name, city, state, school_url, ownership)
- `school_year_data` (id, school_id, year, admission_rate, student_size, tuition_in_state, tuition_out_of_state, completion_rate)
- `programs` (id, school_id, year, cip_code, title, credential_level, completions, median_earnings)
- `cbsa_locations` (cbsa_code, name, state, type)
- `cbsa_counties` (id, cbsa_code, fips_state, fips_county, fips_code, county_name, state_abbr)
- `fair_market_rents` (id, cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom)

### 3.2 New Table: `shared_scenarios` (Edge Operational Ingest)
Stores user-shared scenarios that qualify as "positive finds" (disposable income surplus).

```sql
CREATE TABLE IF NOT EXISTS "shared_scenarios" (
    "id" TEXT NOT NULL CONSTRAINT "PK_shared_scenarios" PRIMARY KEY, -- ULID / UUID string
    "created_at" TEXT NOT NULL,                                      -- ISO-8601 UTC (e.g. 2026-10-14T14:30:00Z)
    "school_id" INTEGER NOT NULL,
    "cip_code" TEXT NOT NULL,
    "credential_level" INTEGER NOT NULL,                            -- 1=Cert, 2=Assoc, 3=Bach, 5=Mast, 6=Doct
    "cbsa_code" TEXT NOT NULL,
    "housing_type" TEXT NOT NULL,                                   -- 'alone' | 'roommate'
    "salary_mode" TEXT NOT NULL,                                    -- 'scorecard_median' | 'user_override'
    "reported_annual_salary" INTEGER NOT NULL,                      -- Annual gross USD
    "monthly_net_take_home" INTEGER NOT NULL,                       -- Net monthly after tax + FICA
    "monthly_rent" INTEGER NOT NULL,                                -- HUD FMR applied
    "monthly_loan_payment" INTEGER NOT NULL,                        -- 10-year standard amortization
    "monthly_disposable" INTEGER NOT NULL,                          -- Take-home minus rent and loan
    "disposable_ratio" REAL NOT NULL,                               -- monthly_disposable / monthly_net_take_home
    "sentiment_tag" TEXT NOT NULL,                                  -- 'comfortable' | 'surplus_plus' | 'hidden_gem'
    "scenario_title" TEXT NULL,                                     -- Sanitized user label (max 60 chars)
    "client_hash" TEXT NOT NULL,                                    -- SHA-256 of daily salt + IP/session (anti-spam, no PII)
    CONSTRAINT "FK_shared_scenarios_schools" FOREIGN KEY ("school_id") REFERENCES "schools" ("id") ON DELETE RESTRICT,
    CONSTRAINT "FK_shared_scenarios_cbsa" FOREIGN KEY ("cbsa_code") REFERENCES "cbsa_locations" ("cbsa_code") ON DELETE RESTRICT
);

-- Indexing for edge querying (top positive finds by metro and field of study)
CREATE INDEX "IX_shared_scenarios_cbsa_cip" ON "shared_scenarios" ("cbsa_code", "cip_code");
CREATE INDEX "IX_shared_scenarios_disposable" ON "shared_scenarios" ("monthly_disposable" DESC);
CREATE INDEX "IX_shared_scenarios_created_at" ON "shared_scenarios" ("created_at" DESC);
```

### 3.3 New Table: `community_scenario_benchmarks` (Reverse-ETL Cache)
Read-only cache compiled by Databricks Gold and synced to SQLite for edge display.

```sql
CREATE TABLE IF NOT EXISTS "community_scenario_benchmarks" (
    "cip_family" TEXT NOT NULL,                                     -- First 2 digits of CIP (e.g. '11' for CS)
    "cbsa_code" TEXT NOT NULL,
    "credential_level" INTEGER NOT NULL,
    "housing_type" TEXT NOT NULL,                                   -- 'alone' | 'roommate'
    "sample_count" INTEGER NOT NULL,                                -- Must be >= 5 (k-anonymity)
    "p25_monthly_disposable" INTEGER NOT NULL,
    "median_monthly_disposable" INTEGER NOT NULL,
    "p75_monthly_disposable" INTEGER NOT NULL,
    "top_contributing_school_id" INTEGER NULL,
    "last_updated_at" TEXT NOT NULL,
    CONSTRAINT "PK_community_scenario_benchmarks" PRIMARY KEY ("cip_family", "cbsa_code", "credential_level", "housing_type")
);
```

---

## 4. Databricks Lakehouse Medallion Schema (Delta Lake / Unity Catalog)

Catalog namespace: `gradcast_lakehouse` (or `lakehouse_dev` in development).

### 4.1 Bronze Layer (Raw Ingestion / Append-Only)
Raw landing tables with audit metadata. Data is never updated in Bronze; it is strictly appended.

#### Table: `gradcast_lakehouse.bronze.shared_scenarios`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.bronze.shared_scenarios (
    raw_payload STRING NOT NULL,                 -- Full unparsed JSON string from ingress
    ingest_timestamp TIMESTAMP NOT NULL,         -- UTC timestamp of Lakehouse landing
    source_system STRING NOT NULL,               -- 'edge_api_fly_io' | 'batch_sync'
    file_name STRING NOT NULL,                   -- Source batch file name
    file_offset BIGINT NOT NULL                  -- Ingestion offset
)
USING DELTA
TBLPROPERTIES (
    'delta.appendOnly' = 'true',
    'delta.autoOptimize.optimizeWrite' = 'true'
);
```

#### Table: `gradcast_lakehouse.bronze.college_scorecard`
Raw federal College Scorecard data ingested directly from US Department of Education annual CSV dumps.
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.bronze.college_scorecard (
    unitid STRING,                               -- Integrated Postsecondary Education Data System (IPEDS) ID
    opeid STRING,
    instnm STRING,
    cipcode STRING,
    credential_level STRING,
    earnings_10yr STRING,
    earnings_4yr STRING,
    debt_median STRING,
    report_year INT,
    _ingest_time TIMESTAMP
)
USING DELTA;
```

---

### 4.2 Silver Layer (Cleansed, Conformed, Type-Safe Truth)
Data in Silver is validated against schemas, deduplicated, and enriched with standard geographic codes.

#### Delta Table Features:
- **Liquid Clustering**: `CLUSTER BY (cip_family, cbsa_code)` optimizes query filtering across metro areas and programs without directory-partition skew.
- **Change Data Feed**: `delta.enableChangeDataFeed = true` allows downstream incremental streaming into Gold.
- **Constraints**: Enforces physical boundaries on valid salaries and disposable ratios.

#### Table: `gradcast_lakehouse.silver.scenarios`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.silver.scenarios (
    scenario_id STRING NOT NULL,
    created_at TIMESTAMP NOT NULL,
    school_id INT NOT NULL,
    cip_code STRING NOT NULL,
    cip_family STRING NOT NULL,                  -- Derived: SUBSTRING(cip_code, 1, 2)
    credential_level INT NOT NULL,
    cbsa_code STRING NOT NULL,
    housing_type STRING NOT NULL,
    salary_mode STRING NOT NULL,
    reported_annual_salary DECIMAL(12, 2) NOT NULL,
    monthly_net_take_home DECIMAL(10, 2) NOT NULL,
    monthly_rent DECIMAL(10, 2) NOT NULL,
    monthly_loan_payment DECIMAL(10, 2) NOT NULL,
    monthly_disposable DECIMAL(10, 2) NOT NULL,
    disposable_ratio DOUBLE NOT NULL,
    sentiment_tag STRING NOT NULL,
    is_valid_positive_find BOOLEAN NOT NULL,
    _updated_at TIMESTAMP NOT NULL,

    CONSTRAINT chk_positive_salary CHECK (reported_annual_salary >= 10000.00),
    CONSTRAINT chk_disposable_floor CHECK (monthly_disposable >= -20000.00)
)
USING DELTA
CLUSTER BY (cip_family, cbsa_code)
TBLPROPERTIES (
    'delta.enableChangeDataFeed' = 'true',
    'delta.minReaderVersion' = '2',
    'delta.minWriterVersion' = '5'
);
```

#### Table: `gradcast_lakehouse.silver.programs`
Conformed program master table integrating historic Scorecard median earnings.
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.silver.programs (
    program_surrogate_key BIGINT GENERATED ALWAYS AS IDENTITY,
    school_id INT NOT NULL,
    cip_code STRING NOT NULL,
    cip_family STRING NOT NULL,
    program_title STRING NOT NULL,
    credential_level INT NOT NULL,
    credential_desc STRING NOT NULL,             -- 'Bachelor's Degree', 'Master's Degree', etc.
    historic_median_earnings DECIMAL(10, 2),
    completions INT,
    data_year INT NOT NULL,
    is_current BOOLEAN NOT NULL,
    _effective_from TIMESTAMP NOT NULL,
    _effective_to TIMESTAMP
)
USING DELTA
CLUSTER BY (cip_code, school_id);
```

---

### 4.3 Gold Layer (Dimensional Star Schema & $k$-Anonymity Marts)
The Gold layer powers analytical dashboards, machine learning wage predictions, and the reverse-ETL pipeline back to SQLite.

```
       +-----------------------+               +-----------------------+
       |      dim_school       |               |      dim_metro        |
       +-----------------------+               +-----------------------+
       | PK: school_key        |               | PK: cbsa_code         |
       |     unitid            |               |     metro_name        |
       |     school_name       |               |     state_code        |
       |     state             |               |     cost_index        |
       +-----------------------+               +-----------------------+
                   ▲                                       ▲
                   │                                       │
                   │           +-----------------------+   │
                   └───────────┤ fact_scenario_outcomes├───┘
                               +-----------------------+
                               | PK: scenario_key      |
                               | FK: school_key        |
                               | FK: program_key       |
                               | FK: cbsa_code         |
                               | FK: date_key          |
                               |     salary_usd        |
                               |     monthly_net       |
                               |     monthly_rent      |
                               |     disposable_income |
                               +-----------------------+
                                           │
                                           ▼
                               +-----------------------+
                               |      dim_program      |
                               +-----------------------+
                               | PK: program_key       |
                               |     cip_code          |
                               |     cip_family        |
                               |     program_title     |
                               +-----------------------+
```

#### Table: `gradcast_lakehouse.gold.dim_school`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.gold.dim_school (
    school_key BIGINT GENERATED ALWAYS AS IDENTITY,
    school_id INT NOT NULL,
    school_name STRING NOT NULL,
    city STRING NOT NULL,
    state STRING NOT NULL,
    school_url STRING,
    ownership INT NOT NULL
)
USING DELTA
CLUSTER BY (school_id);
```

#### Table: `gradcast_lakehouse.gold.dim_metro`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.gold.dim_metro (
    cbsa_code STRING NOT NULL,
    metro_name STRING NOT NULL,
    state_code STRING NOT NULL,
    metro_type STRING NOT NULL
)
USING DELTA
CLUSTER BY (cbsa_code);
```

#### Table: `gradcast_lakehouse.gold.dim_program`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.gold.dim_program (
    program_key BIGINT GENERATED ALWAYS AS IDENTITY,
    cip_code STRING NOT NULL,
    cip_family STRING NOT NULL,
    program_title STRING
)
USING DELTA
CLUSTER BY (cip_code);
```

#### Table: `gradcast_lakehouse.gold.fact_scenario_outcomes`
```sql
CREATE TABLE IF NOT EXISTS gradcast_lakehouse.gold.fact_scenario_outcomes (
    scenario_key BIGINT GENERATED ALWAYS AS IDENTITY,
    scenario_id STRING NOT NULL,
    school_id INT NOT NULL,
    cip_code STRING NOT NULL,
    credential_level INT NOT NULL,
    cbsa_code STRING NOT NULL,
    date_key INT NOT NULL,                       -- Format: YYYYMMDD
    housing_type STRING NOT NULL,
    annual_salary DECIMAL(12, 2) NOT NULL,
    monthly_rent DECIMAL(10, 2) NOT NULL,
    monthly_disposable DECIMAL(10, 2) NOT NULL,
    disposable_ratio DOUBLE NOT NULL
)
USING DELTA
CLUSTER BY (cbsa_code, cip_code);
```

#### Secure View: `gradcast_lakehouse.gold.v_public_positive_finds` (Enforcing $k$-Anonymity)
To honor the **KnowThankYew** privacy invariants in the cloud, raw user scenarios are never exposed directly to public reporting or client APIs if the sample size is small. A strict $k$-anonymity filter ($k \ge 5$) prevents re-identification:

```sql
CREATE OR REPLACE VIEW gradcast_lakehouse.gold.v_public_positive_finds AS
SELECT 
    f.cbsa_code,
    m.metro_name,
    p.cip_family,
    f.credential_level,
    f.housing_type,
    COUNT(*) AS sample_count,
    ROUND(AVG(f.monthly_disposable), 2) AS mean_monthly_disposable,
    PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY f.monthly_disposable) AS median_disposable,
    PERCENTILE_CONT(0.25) WITHIN GROUP (ORDER BY f.monthly_disposable) AS p25_disposable,
    PERCENTILE_CONT(0.75) WITHIN GROUP (ORDER BY f.monthly_disposable) AS p75_disposable
FROM gradcast_lakehouse.gold.fact_scenario_outcomes f
JOIN gradcast_lakehouse.gold.dim_metro m ON f.cbsa_code = m.cbsa_code
JOIN gradcast_lakehouse.gold.dim_program p ON f.cip_code = p.cip_code
GROUP BY 
    f.cbsa_code, 
    m.metro_name, 
    p.cip_family, 
    f.credential_level,
    f.housing_type
HAVING COUNT(*) >= 5; -- Invariant: Hard k-anonymity boundary
```

---

## 5. Reverse-ETL: Databricks Gold to Edge SQLite Compiler

A daily scheduled PySpark job executes the following query and writes the output directly into a SQLite database file:

```python
# scripts/export_gold_to_sqlite.py
def export_gold_to_sqlite(spark, sqlite_db_path: str):
    df = spark.sql("""
        SELECT 
            cip_family,
            cbsa_code,
            credential_level,
            housing_type,
            sample_count,
            CAST(p25_disposable AS INT) AS p25_monthly_disposable,
            CAST(median_disposable AS INT) AS median_monthly_disposable,
            CAST(p75_disposable AS INT) AS p75_monthly_disposable,
            NULL AS top_contributing_school_id,
            CURRENT_TIMESTAMP() AS last_updated_at
        FROM gradcast_lakehouse.gold.v_public_positive_finds
    """)
    
    # Write to local SQLite staging file via pandas / sqlite3 connector
    pandas_df = df.toPandas()
    import sqlite3
    with sqlite3.connect(sqlite_db_path) as conn:
        pandas_df.to_sql(
            "community_scenario_benchmarks", 
            conn, 
            if_exists="replace", 
            index=False
        )
```

---

## 6. Security, Identity & Governance Controls

1. **Unity Catalog Privileges**:
   - `service_principal_etl`: `ALL PRIVILEGES` on `bronze`, `silver`, `gold`.
   - `web_api_ingest`: `INSERT` on `bronze.shared_scenarios`.
   - `analyst_role`: `SELECT` on `gold.*` (Denied access to raw bronze strings).
2. **Column Masking**:
   - `client_hash` is masked with a cryptographic salt in Silver; stripped entirely in Gold.
3. **Data Retention & VACUUM**:
   - Bronze tables retain 30 days of transaction history (`delta.deletedFileRetentionDuration = 'interval 30 days'`).
   - Silver/Gold retain 7 days (`VACUUM gradcast_lakehouse.silver.scenarios RETAIN 168 HOURS`).
