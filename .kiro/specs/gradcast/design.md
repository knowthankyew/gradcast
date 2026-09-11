# GradCast - Design

## Architecture (As Built)

```
┌──────────────────────────────────────────────────────────────────────────┐
│                    Vue 3 SPA (Vite + Vuetify 4 + Pinia)                   │
│                                                                           │
│  Components (Progressive Disclosure Flow):                                │
│  SchoolSearch → SchoolDetail → ProgramList → LocationSelector             │
│                                    ↓                                      │
│                             JobPulseWidget → BudgetSimulator               │
│                                                    ↓                      │
│                                             SavedBudgets                  │
│                                                                           │
│  Composables: useSchoolApi, useJobPulse, useBudgetSimulator,              │
│               useSavedBudgets                                             │
│  State: Pinia useAppStore (selectedSchool, selectedProgram,               │
│          selectedLocation, housingType, isRestoring)                      │
└──────────────────────────────┬────────────────────────────────────────────┘
                               │ HTTP (JSON) — proxied via Vite dev server
                               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      ASP.NET Core 10 Minimal API                          │
│                                                                           │
│  Endpoints:                                                               │
│  ├── /api/schools/search?q=&state=       → CollegeScorecardService        │
│  ├── /api/schools/{id}?year=             → CollegeScorecardService        │
│  ├── /api/schools/{id}/tuition-trend     → CollegeScorecardService        │
│  ├── /api/locations/search?q=            → LocationService (SQLite)       │
│  ├── /api/locations/{cbsa}/housing?type= → HousingCostService (SQLite)    │
│  ├── /api/jobs/pulse?cipCode=&cbsa=      → AdzunaJobPulseService          │
│  ├── /api/finance/net-pay?...            → TaxCalculationService (JSON)   │
│  ├── /api/finance/loan-payment?...       → LoanAmortizationService        │
│  └── POST /api/finance/simulator         → BudgetSimulatorService         │
│                                                                           │
│  Data Mode (configurable):                                                │
│  ├── "api"    → Live Scorecard API calls                                  │
│  ├── "local"  → SQLite only                                               │
│  └── "hybrid" → Local first, API fallback for historic years              │
│                                                                           │
│  Services:                                                                │
│  ├── CollegeScorecardService (remote HTTP)                                │
│  ├── LocalCollegeScorecardService (SQLite queries)                        │
│  ├── HybridCollegeScorecardService (local + fallback)                     │
│  ├── LocationService (SQLite LIKE search on cbsa_locations)               │
│  ├── HousingCostService (SQLite lookup on fair_market_rents)              │
│  ├── TaxCalculationService (reads JSON config at startup)                 │
│  ├── LoanAmortizationService (pure math, zero deps)                       │
│  ├── BudgetSimulatorService (orchestrator — local data only, no APIs)     │
│  └── AdzunaJobPulseService (optional external, graceful fallback)         │
└──────────────────────────────┬────────────────────────────────────────────┘
                               │
                               ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                           Data Layer                                       │
│                                                                           │
│  SQLite (gradcast.db) — created by import tool:                           │
│  ├── schools (6,273 institutions)                                         │
│  ├── school_year_data (tuition, admission, enrollment, completion)         │
│  ├── programs (216,684 field-of-study records)                            │
│  ├── cbsa_locations (156 metro areas, seeded)                             │
│  └── fair_market_rents (156 FY2025 records, seeded)                       │
│                                                                           │
│  Static Config (loaded at startup):                                       │
│  └── Configuration/TaxData/tax_config_2026.json                           │
│                                                                           │
│  External APIs (optional, graceful fallback):                             │
│  ├── College Scorecard (api.data.gov, 1000 req/hr, free key)              │
│  └── Adzuna Jobs (api.adzuna.com, 250 req/day, free key)                  │
└──────────────────────────────────────────────────────────────────────────┘
```

## Project Structure (Actual)

```
gradcast/
├── src/
│   ├── api/                              # ASP.NET Core 10 Minimal API
│   │   ├── Program.cs                    # DI, middleware, endpoint mapping
│   │   ├── Configuration/
│   │   │   ├── CollegeScorecardOptions.cs
│   │   │   ├── AdzunaOptions.cs
│   │   │   └── TaxData/
│   │   │       └── tax_config_2026.json  # Versioned, no-recompile updates
│   │   ├── Endpoints/
│   │   │   ├── SchoolEndpoints.cs
│   │   │   ├── LocationEndpoints.cs
│   │   │   ├── FinanceEndpoints.cs
│   │   │   └── JobEndpoints.cs
│   │   ├── Models/                       # DTOs (records)
│   │   └── Services/
│   │       ├── ICollegeScorecardService.cs
│   │       ├── CollegeScorecardService.cs
│   │       ├── LocalCollegeScorecardService.cs
│   │       ├── HybridCollegeScorecardService.cs
│   │       ├── LocationService.cs
│   │       ├── HousingCostService.cs
│   │       ├── TaxCalculationService.cs
│   │       ├── LoanAmortizationService.cs
│   │       ├── BudgetSimulatorService.cs
│   │       ├── IJobPulseService.cs
│   │       ├── AdzunaJobPulseService.cs
│   │       └── CipJobKeywordMap.cs
│   ├── data/                             # EF Core class library
│   │   ├── Entities/
│   │   │   ├── School.cs
│   │   │   ├── SchoolYearData.cs
│   │   │   ├── Program.cs
│   │   │   ├── CbsaLocation.cs
│   │   │   ├── CbsaCounty.cs
│   │   │   └── FairMarketRent.cs
│   │   ├── SeedData/
│   │   │   ├── CbsaSeed.cs              # 156 metros
│   │   │   └── FmrSeed.cs               # FY2025 rents
│   │   └── GradCastDbContext.cs
│   ├── import/                           # CLI import tool
│   │   └── Program.cs                    # Downloads scorecard CSV, seeds CBSA/FMR
│   └── web/                              # Vue 3 + Vuetify 4 + Pinia
│       └── src/
│           ├── components/
│           │   ├── DisclaimerBanner.vue
│           │   ├── SchoolSearch.vue
│           │   ├── SchoolDetail.vue
│           │   ├── ProgramList.vue       # Selectable rows
│           │   ├── YearSelector.vue
│           │   ├── TuitionTrend.vue
│           │   ├── LocationSelector.vue
│           │   ├── JobPulseWidget.vue
│           │   ├── BudgetSimulator.vue
│           │   └── SavedBudgets.vue
│           ├── composables/
│           │   ├── useSchoolApi.ts
│           │   ├── useBudgetSimulator.ts
│           │   ├── useJobPulse.ts
│           │   └── useSavedBudgets.ts
│           ├── stores/
│           │   └── appStore.ts           # Pinia (shared state + isRestoring mutex)
│           ├── data/
│           │   └── cipCategories.ts      # CIP 2-digit → human-readable names
│           └── types/
│               └── index.ts
├── GradCast.slnx
├── README.md
└── LICENSE (MIT)
```

## Key Design Decisions

### Data Externalization Pattern

**Problem**: Hardcoded reference data (tax brackets, FMR values, metro lists) becomes stale and requires recompilation to update.

**Solution**: All annually-changing data lives outside compiled code:

| Data | Storage | Update Path |
|------|---------|-------------|
| Federal/state tax brackets | `tax_config_YYYY.json` | Drop new file, restart |
| CBSA metro areas | SQLite (seeded from `CbsaSeed.cs`) | Edit seed + re-import |
| HUD Fair Market Rents | SQLite (seeded from `FmrSeed.cs`) | Edit seed + re-import |
| College Scorecard data | SQLite (from CSV import) | Re-download + re-import |
| Student loan rate | Request parameter (default 5.5%) | User override in UI |
| CIP → job keywords | Static dictionary | Edit code (future: JSON) |

### Budget Simulator: Decoupled from External APIs

The `POST /api/finance/simulator` endpoint uses **only local/fast data**:
- Salary → Scorecard median from SQLite (or user override)
- Taxes → JSON config file (loaded once at startup)
- Housing → HUD FMR from SQLite
- Loan → Pure math (amortization formula)

The Job Pulse widget runs **independently and asynchronously** on its own endpoint. If Adzuna is slow or down, the budget still loads instantly.

### Progressive Disclosure (Frontend)

Components render sequentially, each gated by the previous step:
```
hasSchool?         → SchoolDetail + YearSelector + ProgramList
hasSchool?         → LocationSelector
hasProgram && hasLocation? → JobPulseWidget
canSimulate?       → BudgetSimulator + SavedBudgets
```

`canSimulate = hasSchool && hasLocation && !isRestoring`

### State Management (Pinia)

Single store (`useAppStore`) with:
- **State**: selectedSchoolId, selectedSchool, selectedYear, selectedProgram, selectedLocation, housingType, isRestoring
- **Computed gates**: hasSchool, hasProgram, hasLocation, canSimulate
- **Actions**: setSchool (clears downstream), restoreSchool (preserves downstream), setProgram, setLocation, setHousingType, clearSchool/Program/Location
- **Mutex**: `isRestoring` flag prevents reactive watchers from triggering during load-back

### Saved Scenarios (localStorage)

Each saved budget stores minimal stable keys for re-hydration:
```typescript
interface SavedBudgetContext {
  schoolId: number
  programCipCode: string | null
  programTitle: string | null
  programCredentialName: string | null
  cbsaCode: string
  locationName: string
  locationState: string
  housingType: '1bed' | '2bed'
}
```

On load-back: `isRestoring=true` → restoreSchool → setProgram → setLocation → setHousingType → `isRestoring=false` → watcher fires once → budget simulates.

## API Endpoints (Complete)

| Method | Path | Source | Description |
|--------|------|--------|-------------|
| GET | `/api/schools/search?q=&state=` | Scorecard/SQLite | School autocomplete |
| GET | `/api/schools/{id}?year=` | Scorecard/SQLite | School detail + programs |
| GET | `/api/schools/{id}/tuition-trend` | Scorecard/SQLite | 5-year tuition history |
| GET | `/api/locations/search?q=` | SQLite | Metro area autocomplete |
| GET | `/api/locations/{cbsa}/housing?type=` | SQLite | HUD FMR rent lookup |
| GET | `/api/jobs/pulse?cipCode=&cbsa=` | Adzuna/SQLite | Job openings + salary |
| GET | `/api/finance/net-pay?grossSalary=&state=` | JSON config | Net pay after taxes |
| GET | `/api/finance/loan-payment?principal=&rate=&termYears=` | Math | Loan amortization |
| POST | `/api/finance/simulator` | Orchestrator | Full budget simulation |
