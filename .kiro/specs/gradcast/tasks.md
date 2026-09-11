# GradCast - Implementation Tasks

## Phase 1: College Data Explorer (Complete — 14 Tasks)

- [x] Task 1: Project scaffolding (.NET 10 + Vue 3 + Vuetify 4 + TypeScript)
- [x] Task 2: Backend configuration & service foundation
- [x] Task 3: Backend school search endpoint (with state filter)
- [x] Task 4: Backend school detail endpoint (with year parameter)
- [x] Task 5: Frontend types and API composable
- [x] Task 6: Frontend SchoolSearch component (v-autocomplete)
- [x] Task 7: Frontend SchoolDetail component (stats card)
- [x] Task 8: Frontend ProgramList component (grouped by CIP category)
- [x] Task 9: App assembly & integration
- [x] Task 10: Polish (.gitignore, README, MIT license)
- [x] Task 11: Year selector for historic data + info tooltip
- [x] Task 12: Tuition trend 5-year line chart (vue-chartjs)
- [x] Task 13: SQLite data layer + CSV import tool
- [x] Task 14: Hybrid data mode (local + API fallback)

---

## Phase 2: The Consequences Engine (Complete — 15 Tasks)

- [x] Task 15: CBSA location entities, seed data (156 metros), search endpoint
- [x] Task 16: Pinia store + LocationSelector + housing type toggle
- [x] Task 17: HUD FMR entity, FY2025 seed data, housing cost endpoint
- [x] Task 18: Job Pulse backend (IJobPulseService, Adzuna client, CIP keyword map, endpoint)
- [x] Task 19: Job Pulse frontend widget (composable + component)
- [x] Task 20: Tax calculation service (externalized to JSON config, 2026 brackets)
- [x] Task 21: Loan amortization service (standard formula, configurable rate/term)
- [x] Task 22: Budget simulator orchestration endpoint (POST /api/finance/simulator)
- [x] Task 23: Budget Simulator frontend dashboard + salary override
- [x] Task 24: Selectable program rows (CIP code → store → simulation)
- [x] Task 25: Saved budget scenarios (name + save to localStorage)
- [x] Task 26: Progressive disclosure UX refactor
- [x] Task 27: Load-back saved scenarios (restore state from localStorage)
- [x] Task 28: Tax brackets externalized to versioned JSON (no-recompile updates)
- [x] Task 29: README comprehensive rewrite (features, flow, setup, all endpoints)

---

## In Progress: Structural Hardening

### Task 30: isRestoring Mutex (Pinia) ✅ Complete
- [x] Add `isRestoring` ref to useAppStore
- [x] `canSimulate` returns false while isRestoring is true
- [x] SavedBudgets.onLoad sets isRestoring=true before state changes, false after
- [x] Prevents budget simulator watcher from firing duplicate calls during hydration

### Task 31: 4-Digit CIP Keyword Granularity ✅ Complete
- [x] Extend CipJobKeywordMap to accept 4-digit codes (e.g., "1107" → "software engineer")
- [x] Maintain 2-digit fallback for codes without specific 4-digit mappings
- [x] More precise job search results for specific programs

### Task 32: Legal/Data Disclaimer ✅ Complete
- [x] Add dismissible v-banner at top of App.vue (DisclaimerBanner.vue)
- [x] "Planning tool only. Not financial advice. Data may lag 1-2 years."
- [x] Dismissal persisted to localStorage (key: gradcast_disclaimer_dismissed)

---

## One Bug Remaining

### Task 33: Fix Adzuna UTF-8 Encoding Bug
- [x] Adzuna API returns `Content-Type: charset=utf8` (missing dash)
- [x] .NET's `ReadAsStringAsync` throws: `'utf8' is not a supported encoding name`
- [x] Fix: read response as bytes, decode with `System.Text.Encoding.UTF8` explicitly
- [x] After fix: Job Pulse widget will show live openings count + local salary data

---

## Backend Review Follow-Up (Planned)

### Task 34: Normalize CIP Codes for Budget Salary Resolution ✅ Complete
- [x] Establish one canonical stored/query format for CIP codes (digits only, 4-digit category level).
- [x] Normalize imported `CIPCODE` values before persisting program rows.
- [x] Normalize incoming simulator CIP codes before querying program earnings.
- [x] Replace the current prefix/max-earnings lookup with the selected program's matching earnings value; define and document a deterministic fallback when multiple credential rows match.
- [x] Add regression coverage proving a selected local program resolves its own median earnings rather than the school-wide fallback.

### Task 35: Validate and Harden Finance Inputs ✅ Complete
- [x] Validate loan `termYears` as a positive, bounded integer before amortization (1–50 years).
- [x] Validate simulator `housingType` against the supported values: `studio`, `1bed`, and `2bed`.
- [x] Validate `salaryOverride` as a positive, bounded annual salary when provided.
- [x] Return consistent RFC 7807 problem responses for invalid finance input; do not allow invalid input to produce a 500.
- [x] Add endpoint-level tests for zero/negative loan terms, unsupported housing types, and invalid salary overrides.

### Task 36: Treat Missing Housing Data as Unavailable, Not Free ✅ Complete
- [x] Make budget simulation return an explicit unavailable result when no FMR record exists for the selected CBSA.
- [x] Map that result to a clear client error/status rather than returning a successful simulation with `$0` rent.
- [x] Preserve the existing successful response shape for simulations with valid housing data.
- [x] Add regression coverage for a valid location with missing FMR data.

### Task 37: Refresh Import Dependencies and Remove Vulnerable SQLite Native Package
- [x] Replace the preview EF Core SQLite package in `src/import` with the same supported stable version used by API/data projects.
- [x] Restore packages and verify `dotnet list GradCast.slnx package --include-transitive --vulnerable` reports no known vulnerabilities.
- [x] Run the import tool against a representative Scorecard extract to confirm compatibility.

### Task 38: Make Reference-Data Seeding Updatable
- [x] Change CBSA and FMR seeding from "skip when table has rows" to idempotent upsert behavior.
- [x] Key FMR updates by `(CbsaCode, Year)` so a newly added annual seed is inserted without deleting prior-year history.
- [x] Update existing rows when corrected seed values are re-imported.
- [x] Add an import integration test covering a second run with a new FMR year.

### Task 39: Restore Local-First Default Behavior
- [ ] Change the default data mode to `hybrid` after a local import, or document and implement an explicit startup mode selection.
- [ ] When API credentials are absent, serve local data when available and return an actionable configuration error only when remote fallback is actually required.
- [ ] Document the intended first-run workflow: import data, optional API credentials, then run the app.
- [ ] Add startup/service-selection coverage for unconfigured API credentials and each data mode.

### Task 40: Secure Development Credentials
- [ ] Rotate any currently active College Scorecard and Adzuna credentials.
- [ ] Store credentials in .NET user secrets or environment variables; keep only empty/example values in config files.
- [ ] Add `appsettings.Development.example.json` (or equivalent setup documentation) without secrets.
- [ ] Verify ignored local config is not tracked and no credentials appear in application logs.

### Task 41: Strengthen Backend Test Coverage and Spec Compliance
- [ ] Replace permissive assertions with deterministic expected values for tax, loan, rent, and simulation calculations.
- [ ] Add integration tests for finance endpoint status codes and problem-response bodies.
- [ ] Add tests for malformed/missing tax-config fields and ensure provider exceptions follow the documented contract.
- [ ] Dispose parsed JSON documents and use read-only EF queries where appropriate.

---

## Future: Phase 3 (Planned, Not Started)

### Comparison & What-If
- [ ] Side-by-side comparison columns (2-3 saved scenarios)
- [ ] Geographic arbitrage view (same degree across different cities)
- [ ] Multi-year trajectory modeling (salary growth 5yr/10yr)
- [ ] Savings rate projection (emergency fund timeline)

### Data Enrichment
- [ ] Per-year historic Scorecard CSV import (full local tuition trend)
- [ ] Full Census CBSA delineation (~930 metros + constituent FIPS)
- [ ] HUD FMR by county FIPS (non-metro coverage)
- [ ] BLS OEWS import (static occupational wage data, no API key)
- [ ] CIP keyword map → JSON or SQLite table (easier annual maintenance)
- [ ] Median debt field from Scorecard (replace tuition-based estimate)

### UX Polish
- [ ] Mobile responsive testing (card stacking on small screens)
- [ ] Loading skeleton states for all cards
- [ ] Export budget to PDF (parents/counselors use case)
- [ ] Screenshot tour in README
- [ ] Error boundary component

### Structural
- [ ] Integration tests for deterministic math (tax + loan + rent)
- [ ] DB schema versioning tied to import year
- [ ] `gradcast.db` bundled as read-only asset for containerized demos (if ever needed)

---

## Architecture Notes

### Data Externalization Pattern
All data that changes annually or could become stale is stored externally:

| Data | Location | Update Frequency | Update Method |
|------|----------|-----------------|---------------|
| Tax brackets | `Configuration/TaxData/tax_config_YYYY.json` | Annual | Add new JSON file |
| CBSA metros | `SeedData/CbsaSeed.cs` → SQLite | Decennial | Expand seed or Census import |
| HUD FMR rents | `SeedData/FmrSeed.cs` → SQLite | Annual | Expand seed or HUD CSV import |
| College data | SQLite via import tool | Annual | Re-run import with new CSV |
| CIP keywords | `CipJobKeywordMap.cs` | Infrequent | Edit code (future: JSON) |
| Loan rate | Request parameter (default 5.5%) | As needed | User override or config |

### Hosting Constraint
This application runs locally. Period. Design decisions should never assume cloud infrastructure:
- SQLite is the database (not Postgres, not managed SQL)
- localStorage is user persistence (not accounts/auth/backend sessions)
- Import tool runs manually (not a cron job or scheduled function)
- API keys are optional enhancements, never hard requirements
