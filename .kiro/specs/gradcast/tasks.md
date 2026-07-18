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

### Task 30: isRestoring Mutex (Pinia)
- [ ] Add `isRestoring` ref to useAppStore
- [ ] `canSimulate` returns false while isRestoring is true
- [ ] SavedBudgets.onLoad sets isRestoring=true before state changes, false after
- [ ] Prevents budget simulator watcher from firing duplicate calls during hydration

### Task 31: 4-Digit CIP Keyword Granularity
- [ ] Extend CipJobKeywordMap to accept 4-digit codes (e.g., "1107" → "computer science", "1101" → "information systems")
- [ ] Maintain 2-digit fallback for codes without specific 4-digit mappings
- [ ] Update AdzunaJobPulseService to pass full CIP code to the map
- [ ] More precise job search results for specific programs

### Task 32: Legal/Data Disclaimer
- [ ] Add a dismissible v-alert or v-banner at top of App.vue
- [ ] Text: "This tool provides estimates for planning purposes only. It is not financial advice. Data may lag 1-2 years. Individual outcomes vary."
- [ ] Store dismissal in localStorage so it doesn't reappear every session
- [ ] Add brief disclaimer text to the budget simulator card subtitle

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
