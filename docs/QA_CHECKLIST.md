# GradCast Lakehouse — QA & Verification Checklist

> **Purpose**: Step-by-step hands-on testing guide for evaluating the Dual-Mode GradCast architecture. Feel free to wander off-script at any point!

---

## 📋 Section 1: Quick Health & Automated Test Suite Verification

Run these baseline automated checks to verify the entire system is healthy before manual probing:

- [ ] **1.1 .NET 10 Backend Test Suite (107 Tests)**
  ```bash
  cd /Users/cl0rkster/Dev/gradcast
  dotnet test
  ```
  *Expected Result*: `Passed! - Failed: 0, Passed: 107, Total: 107`.

- [ ] **1.2 Lakehouse Python & SQLite Compiler Unit Tests (6 Tests)**
  ```bash
  python3 -m unittest discover -s lakehouse/tests -v
  ```
  *Expected Result*: All 6 tests pass (`OK`), confirming Silver validation, $k$-anonymity suppression, and SQLite benchmark table creation.

- [ ] **1.3 Architectural & Claims Auditing Linter**
  ```bash
  python3 lakehouse/scripts/audit_claims.py
  ```
  *Expected Result*: `[PASS] Audit gates passed cleanly` (Schema parity, $k \ge 5$ invariant, SHA-256 client hash, zero prohibited vocabulary, and Master Brief parity).

- [ ] **1.4 Terraform Configuration & Syntax Validation**
  ```bash
  terraform -chdir=lakehouse/terraform init -backend=false
  terraform -chdir=lakehouse/terraform validate
  ```
  *Expected Result*: `Success! The configuration is valid.`

- [ ] **1.5 Portfolio Meta-Audit**
  ```bash
  cd /Users/cl0rkster/Dev && ./audit-portfolio.sh -r gradcast
  ```
  *Expected Result*: Green status across CPM, FailClosed audit, Policy Pack, and Section 8 Master Brief verification (12/12 repos).

---

## 🌐 Section 2: Edge API & SQLite Persistence Probing

Start the ASP.NET Core API server locally:
```bash
cd /Users/cl0rkster/Dev/gradcast
dotnet run --project src/api
# Or run ./start.sh
```
*(By default, the server listens at `http://localhost:5000` or `http://localhost:5167` — check console output for the active port).*

- [ ] **2.1 Health Check Probe**
  ```bash
  curl -i http://localhost:5000/api/health
  ```
  *Verify*: HTTP `200 OK` with JSON payload `{"status":"Healthy","database":"Connected","cbsaLocations":...}`.

- [ ] **2.2 Positive Scenario Submission (`POST /api/scenarios/share`)**
  Submit an authentic positive financial find:
  ```bash
  curl -i -X POST http://localhost:5000/api/scenarios/share \
    -H "Content-Type: application/json" \
    -d '{
      "schoolId": 110635,
      "cipCode": "11.0701",
      "credentialLevel": 3,
      "cbsaCode": "41860",
      "housingType": "alone",
      "salaryMode": "scorecard_median",
      "reportedAnnualSalary": 115000,
      "monthlyNetTakeHome": 7100,
      "monthlyRent": 2300,
      "monthlyLoanPayment": 320,
      "monthlyDisposable": 4480,
      "disposableRatio": 0.63,
      "sentimentTag": "comfortable",
      "scenarioTitle": "UC Berkeley CS in SF Bay Area"
    }'
  ```
  *Verify*: HTTP `201 Created` with a response payload containing an `id` string (e.g., `{"id":"...","message":"Scenario shared successfully."}`). Note this ID for the next check.

- [ ] **2.3 Retrieve Specific Scenario (`GET /api/scenarios/{id}`)**
  ```bash
  curl -i http://localhost:5000/api/scenarios/<PASTE_SCENARIO_ID_HERE>
  ```
  *Verify*: HTTP `200 OK` returning the scenario joined with school name (`"University of California-Berkeley"`) and location name (`"San Francisco-Oakland-Berkeley, CA"`). Notice internal `client_hash` is stripped from public output.

- [ ] **2.4 Query Top Positive Finds (`GET /api/scenarios/positive-finds`)**
  ```bash
  curl -i "http://localhost:5000/api/scenarios/positive-finds?cbsaCode=41860&limit=10"
  ```
  *Verify*: HTTP `200 OK` returning a list of scenarios sorted by `monthlyDisposable` in descending order.

- [ ] **2.5 Inspect Edge SQLite Database Directly**
  In a separate terminal, query the SQLite database file:
  ```bash
  sqlite3 gradcast.db "SELECT id, cip_code, reported_annual_salary, monthly_disposable, scenario_title FROM shared_scenarios ORDER BY created_at DESC LIMIT 3;"
  ```
  *Verify*: The row you just submitted via `curl` is persisted cleanly.

---

## 🛡️ Section 3: Input Validation & Privacy Boundary Probing

- [ ] **3.1 Negative Salary Rejection**
  Submit a scenario with a negative salary:
  ```bash
  curl -i -X POST http://localhost:5000/api/scenarios/share \
    -H "Content-Type: application/json" \
    -d '{
      "schoolId": 110635,
      "cipCode": "11.0701",
      "credentialLevel": 3,
      "cbsaCode": "41860",
      "housingType": "alone",
      "salaryMode": "scorecard_median",
      "reportedAnnualSalary": -50000,
      "monthlyNetTakeHome": 0,
      "monthlyRent": 2000,
      "monthlyLoanPayment": 0,
      "monthlyDisposable": -2000,
      "disposableRatio": 0.0,
      "sentimentTag": "tight"
    }'
  ```
  *Verify*: HTTP `400 Bad Request` with problem details `"Invalid salary"`: `"Reported annual salary cannot be negative."`

- [ ] **3.2 Disposable Income Floor Rejection (< -$20,000)**
  Submit a scenario claiming an impossible monthly deficit of -$30,000:
  ```bash
  curl -i -X POST http://localhost:5000/api/scenarios/share \
    -H "Content-Type: application/json" \
    -d '{
      "schoolId": 110635,
      "cipCode": "11.0701",
      "credentialLevel": 3,
      "cbsaCode": "41860",
      "housingType": "alone",
      "salaryMode": "scorecard_median",
      "reportedAnnualSalary": 50000,
      "monthlyNetTakeHome": 3000,
      "monthlyRent": 25000,
      "monthlyLoanPayment": 10000,
      "monthlyDisposable": -32000,
      "disposableRatio": -10.6,
      "sentimentTag": "tight"
    }'
  ```
  *Verify*: HTTP `400 Bad Request` with problem details `"Invalid disposable income"`.

- [ ] **3.3 Anti-XSS Sanitization Check**
  Submit a title containing HTML/Script injection:
  ```bash
  curl -i -X POST http://localhost:5000/api/scenarios/share \
    -H "Content-Type: application/json" \
    -d '{
      "schoolId": 110635,
      "cipCode": "11.0701",
      "credentialLevel": 3,
      "cbsaCode": "41860",
      "housingType": "alone",
      "salaryMode": "scorecard_median",
      "reportedAnnualSalary": 80000,
      "monthlyNetTakeHome": 5000,
      "monthlyRent": 1500,
      "monthlyLoanPayment": 200,
      "monthlyDisposable": 3300,
      "disposableRatio": 0.66,
      "sentimentTag": "comfortable",
      "scenarioTitle": "<p>Safe Title Here</p>"
    }'
  ```
  *Verify*: The scenario succeeds with HTTP `201`, but when retrieved via `GET /api/scenarios/{id}`, the HTML `<p>` tags have been stripped, leaving only `"Safe Title Here"`.

---

## 🥾 Section 4: "Off-Script" Exploratory Prompts

Here are interesting avenues to explore if you want to push boundaries:

1. **Anti-Abuse Rate Limiting Stress Test**:
   * Loop `curl` 55 times to `POST /api/scenarios/share`.
   * Verify that starting around submission #51, the API begins rejecting requests with `HTTP 429 Too Many Requests` (`"Daily scenario share limit reached for this client"`).
2. **Reverse-ETL SQLite Compilation**:
   * Run `python3 lakehouse/src/pipelines/export_sqlite.py --help`.
   * Inspect [`lakehouse/tests/e2e/test_reverse_etl.py`](../lakehouse/tests/e2e/test_reverse_etl.py) to see how SQLite tables are built and indexed directly from aggregated DataFrames.
3. **Intentional Documentation Drift Test**:
   * Add a prohibited word like `"bulletproof"` into `ROADMAP.md` or change a table name in `docs/SCHEMA.md`.
   * Run `python3 lakehouse/scripts/audit_claims.py`.
   * Watch the linter catch the drift and fail with exit code `1`, then revert to restore green status.
