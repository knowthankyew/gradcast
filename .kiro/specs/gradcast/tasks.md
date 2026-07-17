# GradCast - Implementation Tasks

## Phase 1 (Complete)

- [x] Task 1: Project scaffolding
- [x] Task 2: Backend configuration & service foundation
- [x] Task 3: Backend school search endpoint
- [x] Task 4: Backend school detail endpoint
- [x] Task 5: Frontend types and API composable
- [x] Task 6: Frontend SchoolSearch component
- [x] Task 7: Frontend SchoolDetail component
- [x] Task 8: Frontend ProgramList component
- [x] Task 9: App assembly & integration
- [x] Task 10: Polish & developer experience
- [x] Task 11: Year selector for historic data
- [x] Task 12: Tuition trend line chart
- [x] Task 13: SQLite data layer + CSV import tool
- [x] Task 14: Hybrid data mode (local + API fallback)

---

## Phase 2A: Location & Budget Engine (Complete)

- [x] Task 15: CBSA location data, search endpoint, and seed import
- [x] Task 16: Pinia store + LocationSelector + progressive disclosure UX
- [x] Task 17: HUD Fair Market Rent data, housing endpoint, seed import
- [x] Task 20: Tax calculation service (federal brackets + state rates + FICA)
- [x] Task 21: Loan amortization service (standard 10-year federal)
- [x] Task 22: Budget simulator orchestration endpoint (POST /api/finance/simulator)
- [x] Task 23: Budget Simulator frontend dashboard + salary override
- [x] Task 24: Selectable program rows feeding CIP code into simulation
- [x] Task 25: Saved budget scenarios (name + localStorage + list/delete)
- [x] Task 26: Progressive disclosure UX refactor + README update

---

## Remaining Work

### Task 27: Saved Budget Scenario — Load Back
- [ ] Add "Load" button to each saved scenario in SavedBudgets.vue
- [ ] On load: restore school, program, location, and housing type into the Pinia store
- [ ] Re-fetch school detail if needed (schoolId stored in saved scenario)
- [ ] Scroll to budget simulator after restoration
- [ ] Handle case where school/location data may no longer exist

### Task 18: Job Pulse Service & Endpoint
- [ ] Define `IJobPulseService` interface and `JobPulseResult` model
- [ ] Create CIP-to-keyword mapping (static data, ~47 CIP 2-digit categories → job search terms)
- [ ] Implement `AdzunaJobPulseService` (Adzuna API v1, free tier, 250 req/day)
- [ ] Add Adzuna API key to configuration (appsettings)
- [ ] Create `GET /api/jobs/pulse?cipCode={cip}&cbsa={code}` endpoint
- [ ] Graceful fallback: return Scorecard earnings only if Adzuna is unavailable
- [ ] Consider BLS OEWS data as static fallback (no API key needed)

### Task 19: Frontend Job Pulse Widget
- [ ] Create `JobPulseWidget.vue` component
- [ ] Show when a program is selected AND a location is chosen
- [ ] Display: active job openings, local salary range vs. Scorecard median earnings
- [ ] Create `useJobPulse` composable (async, independent from budget simulator)
- [ ] Data source badge ("Live data" vs. "National baseline")
- [ ] This is the connection between selected degree → job market reality

---

## Future (Phase 3 — Not Started)

### Comparison & Planning
- [ ] Side-by-side comparison mode (2-3 saved scenarios shown as columns)
- [ ] Multi-year career trajectory modeling (5yr/10yr salary growth)
- [ ] Savings rate projections and emergency fund timeline
- [ ] Geographic arbitrage suggestions (same degree, better budget in different city)

### Data Enrichment
- [ ] Import per-year historic Scorecard CSVs for full tuition trend local data
- [ ] Import full Census Bureau CBSA delineation (all ~930 metros + FIPS mapping)
- [ ] Import HUD FMR by county FIPS for non-metro area coverage
- [ ] BLS Occupational Employment data import for static wage baselines

### Polish & Deployment
- [ ] Responsive layout testing across devices
- [ ] Loading skeleton states for all cards
- [ ] Error boundary component for graceful failure recovery
- [ ] Consider Firebase/Vercel deployment for public demo
- [ ] Screenshot tour for README
