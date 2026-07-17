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

## Phase 2A: Location & Career Destination

### Task 15: CBSA Location Data & Search Endpoint
- [ ] Add `CbsaLocation` entity to GradCast.Data (code, name, state, FIPS counties)
- [ ] Source CBSA delineation data (Census Bureau CSV) and add to import tool
- [ ] Create `LocationService` that queries SQLite for CBSA autocomplete
- [ ] Create `GET /api/locations/search?q={query}` endpoint (top 10 matches)
- [ ] Seed import tool with CBSA data (run alongside existing Scorecard import)

### Task 16: Frontend Location Selector Component
- [ ] Create `LocationSelector.vue` with Vuetify `v-autocomplete` for metro areas
- [ ] Add housing type toggle: "Live alone (1-Bed)" vs. "Roommate (Shared 2-Bed)"
- [ ] Add Pinia store (`useAppStore`) for shared state: selectedLocation, housingType
- [ ] Place component in App.vue between SchoolSearch and SchoolDetail
- [ ] Only show when a school has been selected

### Task 17: HUD Fair Market Rent Data & Housing Endpoint
- [ ] Add `FairMarketRent` entity to GradCast.Data (CBSA code, year, 1-4 bed rents)
- [ ] Download and import HUD FMR CSV data in import tool
- [ ] Create `HousingCostService` that looks up rent by CBSA + bedroom count
- [ ] Create `GET /api/locations/{cbsaCode}/housing?type={1bed|2bed}` endpoint
- [ ] Return monthly rent amount for the selected housing type

### Task 18: Job Pulse Service & Endpoint
- [ ] Define `IJobPulseService` interface and `JobPulseResult` model
- [ ] Create CIP-to-keyword mapping (static data file or DB table, ~47 categories)
- [ ] Implement `AdzunaJobPulseService` (Adzuna API v1, free tier)
- [ ] Add Adzuna API key to configuration (appsettings)
- [ ] Create `GET /api/jobs/pulse?cipCode={cip}&cbsa={code}` endpoint
- [ ] Graceful fallback: return Scorecard earnings only if Adzuna unavailable

### Task 19: Frontend Job Pulse Widget
- [ ] Create `JobPulseWidget.vue` component (shown inside expanded program row)
- [ ] Create `useJobPulse` composable (fetches pulse data for CIP + location)
- [ ] Modify `ProgramList.vue` to support row expansion with inline widget
- [ ] Display: active openings count, local salary vs. Scorecard earnings comparison
- [ ] Show data source badge ("Live data" vs. "National baseline")

---

## Phase 2B: Budget Simulator

### Task 20: Tax Calculation Service
- [ ] Create `TaxCalculationService.cs` (zero external dependencies)
- [ ] Implement 2024 federal marginal tax bracket calculation
- [ ] Implement state income tax lookup (flat rates + simplified progressive for CA/NY)
- [ ] Create `GET /api/finance/net-pay?grossSalary={salary}&state={ST}` endpoint
- [ ] Return: gross monthly, federal tax, state tax, net monthly

### Task 21: Loan Amortization Service
- [ ] Create `LoanAmortizationService.cs`
- [ ] Standard amortization formula: P * [r(1+r)^n] / [(1+r)^n - 1]
- [ ] Default inputs: median debt from Scorecard, 5.5% rate, 10-year term
- [ ] Allow override of debt amount and rate
- [ ] Return monthly payment amount

### Task 22: Budget Simulator Endpoint
- [ ] Create `POST /api/finance/simulator-baseline` endpoint
- [ ] Accept: schoolId, cipCode, cbsaCode, housingType, salaryOverride (optional)
- [ ] Orchestrate: pull median earnings → calculate net pay → get rent → get loan payment
- [ ] Return full `BudgetSimulation` result with disposable income and status

### Task 23: Frontend Budget Simulator Dashboard
- [ ] Create `BudgetSimulator.vue` parent component (full-width card)
- [ ] Create `IncomeHeader.vue` — gross vs. net monthly headline numbers
- [ ] Create `ExpenseBreakdown.vue` — line items with amounts
- [ ] Create `DisposableResult.vue` — final number with color coding + donut chart
- [ ] Create `useBudgetSimulator` composable (calls simulator endpoint)
- [ ] Wire into App.vue — show only when school + location + program are all selected

### Task 24: Integration & Reactivity
- [ ] Connect Pinia store state changes to automatic budget recalculation
- [ ] Add salary override input (user can type a custom salary to explore scenarios)
- [ ] Ensure budget updates when user changes school, location, year, or program
- [ ] Loading states and error handling for the full simulation pipeline

### Task 25: Polish & Demo Readiness
- [ ] Update README with Phase 2 features, new data imports, and Adzuna setup
- [ ] Add import instructions for HUD FMR + CBSA data
- [ ] Verify end-to-end flow: search → select school → select location → see budget
- [ ] Responsive layout testing for budget simulator card
- [ ] Final commit and demo walkthrough notes
