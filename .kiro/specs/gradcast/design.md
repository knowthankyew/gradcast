# GradCast - Design

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                     Vue 3 SPA (Vite)                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ SchoolSearch │  │ SchoolDetail │  │ ProgramList  │  │
│  │  (typeahead) │  │  (overview)  │  │  (grouped)   │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │
│         │                  │                  │          │
│         └──────────────────┼──────────────────┘          │
│                            │                             │
│                   Composables / API Layer                 │
└────────────────────────────┼─────────────────────────────┘
                             │ HTTP (JSON)
                             ▼
┌─────────────────────────────────────────────────────────┐
│              ASP.NET Core 10 Minimal API                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │  /api/schools│  │/api/schools/ │  │  Middleware   │  │
│  │   /search    │  │  {id}/detail │  │  (caching,   │  │
│  │              │  │              │  │   errors)    │  │
│  └──────┬───────┘  └──────┬───────┘  └──────────────┘  │
│         │                  │                             │
│         └──────────────────┘                             │
│                    │                                     │
│         CollegeScorecardService                          │
│         (HttpClient + typed DTOs)                        │
└────────────────────────────┼─────────────────────────────┘
                             │ HTTPS
                             ▼
              ┌──────────────────────────┐
              │  College Scorecard API    │
              │  api.data.gov/ed/...     │
              └──────────────────────────┘
```

## Project Structure

```
gradcast/
├── src/
│   ├── api/                          # ASP.NET Core project
│   │   ├── GradCast.Api.csproj
│   │   ├── Program.cs                # Host builder, DI, middleware, route mapping
│   │   ├── appsettings.json          # Config (API key placeholder)
│   │   ├── appsettings.Development.json
│   │   ├── Endpoints/
│   │   │   └── SchoolEndpoints.cs    # Minimal API route definitions
│   │   ├── Services/
│   │   │   ├── ICollegeScorecardService.cs
│   │   │   └── CollegeScorecardService.cs
│   │   ├── Models/
│   │   │   ├── SchoolSearchResult.cs
│   │   │   ├── SchoolDetail.cs
│   │   │   └── ProgramData.cs
│   │   └── Configuration/
│   │       └── CollegeScorecardOptions.cs
│   │
│   └── web/                          # Vue 3 + Vite project
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       ├── index.html
│       ├── src/
│       │   ├── main.ts
│       │   ├── App.vue
│       │   ├── components/
│       │   │   ├── SchoolSearch.vue
│       │   │   ├── SchoolDetail.vue
│       │   │   └── ProgramList.vue
│       │   ├── composables/
│       │   │   └── useSchoolApi.ts
│       │   ├── types/
│       │   │   └── index.ts
│       │   └── assets/
│       │       └── main.css
│       └── tailwind.config.js
│
├── GradCast.sln
└── README.md
```

## Backend Design

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/schools/search?q={query}` | Autocomplete search, returns top 10 matches |
| GET | `/api/schools/{id}` | Full school detail + program data |

### College Scorecard API Mapping

**Search endpoint** maps to:
```
GET https://api.data.gov/ed/collegescorecard/v1/schools
  ?api_key={key}
  &school.name={query}
  &fields=id,school.name,school.city,school.state
  &per_page=10
```

**Detail endpoint** maps to:
```
GET https://api.data.gov/ed/collegescorecard/v1/schools
  ?api_key={key}
  &id={schoolId}
  &fields=id,school.name,school.city,school.state,school.school_url,
          school.ownership,latest.admissions.admission_rate.overall,
          latest.student.size,latest.cost.tuition.in_state,
          latest.cost.tuition.out_of_state,latest.completion.rate_suppressed.overall,
          latest.programs.cip_4_digit
```

The `latest.programs.cip_4_digit` field returns an array of program objects with:
- `code` — 4-digit CIP code (first 2 digits = category)
- `title` — Program name
- `credential.level` — Credential type (1=cert, 2=assoc, 3=bach, 5=masters, 6=doctoral)
- `counts.ipeds_awards1` — Number of completions (awards)

### Service Layer

```csharp
public interface ICollegeScorecardService
{
    Task<IReadOnlyList<SchoolSearchResult>> SearchSchoolsAsync(string query, CancellationToken ct);
    Task<SchoolDetail?> GetSchoolDetailAsync(int schoolId, CancellationToken ct);
}
```

### Caching Strategy
- Use `IMemoryCache` with 5-minute absolute expiration.
- Cache key: `school_detail_{id}` for detail responses.
- Search results are NOT cached (dynamic, short-lived queries).

### Error Handling
- All Scorecard API errors are caught and wrapped in ProblemDetails (RFC 7807).
- Rate limiting (429) from upstream → 503 to frontend with Retry-After header.

## Frontend Design

### State Management
- No Pinia/Vuex needed for Phase 1. Use component-local state + composables.
- `useSchoolApi()` composable encapsulates all HTTP calls with loading/error state.

### Component Hierarchy

```
App.vue
├── SchoolSearch.vue        ← Input + dropdown overlay
│   └── (emits: @school-selected)
├── SchoolDetail.vue        ← Summary card (conditionally rendered)
│   └── ProgramList.vue     ← Grouped, collapsible program display
```

### Type-Ahead Behavior
1. User types ≥ 2 characters.
2. Debounce 300ms.
3. Call `GET /api/schools/search?q=...`.
4. Display results in a positioned dropdown.
5. Keyboard navigation (↑/↓/Enter/Esc).
6. On select → emit school ID → parent fetches detail.

### Program Grouping Logic (Frontend)
- Extract 2-digit CIP prefix from `code` field.
- Map 2-digit prefix to human-readable category name (static lookup table of ~47 categories from NCES CIP taxonomy).
- Group programs under their category, sort categories alphabetically, sort programs within by completion count descending.

### UI Framework
- Vuetify 4 with default Material Design theme (customized brand colors).
- Key Vuetify components used:
  - `v-autocomplete` — type-ahead school search with built-in debounce, keyboard nav, ARIA.
  - `v-card` — school detail overview.
  - `v-expansion-panels` — collapsible program category groups.
  - `v-data-table` — program details within each category (title, credential, completions, earnings).
  - `v-select` — state filter dropdown.
  - `v-chip` — credential level badges.
  - `v-progress-circular` / `v-skeleton-loader` — loading states.
- Responsive: Vuetify grid system (`v-container`, `v-row`, `v-col`).

## Configuration & Secrets

| Setting | Location | Example |
|---------|----------|---------|
| `CollegeScorecard:ApiKey` | User Secrets (dev) / Env var (prod) | `abcdef123456` |
| `CollegeScorecard:BaseUrl` | appsettings.json | `https://api.data.gov/ed/collegescorecard/v1` |

## Development Workflow

1. Register for API key at https://api.data.gov/signup/
2. `dotnet user-secrets set "CollegeScorecard:ApiKey" "YOUR_KEY"` in `src/api/`
3. `dotnet run --project src/api` → API on `https://localhost:5001`
4. `cd src/web && npm install && npm run dev` → Vite on `http://localhost:5173` with proxy to API

---

## Phase 2 Design

### Architecture (Extended)

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Vue 3 SPA (Vite + Vuetify)                    │
│  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌──────────────────┐ │
│  │SchoolSearch│ │ Location   │ │SchoolDetail│ │ BudgetSimulator  │ │
│  │            │ │ Selector   │ │+ Programs  │ │  (dashboard)     │ │
│  └─────┬──────┘ └─────┬──────┘ └─────┬──────┘ └────────┬─────────┘ │
│        │               │              │                  │           │
│        └───────────────┼──────────────┼──────────────────┘           │
│                        │              │                              │
│              Composables: useSchoolApi, useLocationApi,               │
│                          useJobPulse, useBudgetSimulator              │
└────────────────────────┼──────────────┼──────────────────────────────┘
                         │              │  HTTP (JSON)
                         ▼              ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   ASP.NET Core 10 Minimal API                        │
│                                                                      │
│  Endpoints:                                                          │
│  ┌────────────────┐ ┌───────────────┐ ┌──────────────────────────┐  │
│  │ /api/schools/* │ │/api/locations/│ │ /api/finance/*           │  │
│  │ (existing)     │ │  search       │ │  /net-pay               │  │
│  │                │ │               │ │  /simulator-baseline    │  │
│  └────────┬───────┘ └───────┬───────┘ └────────────┬─────────────┘  │
│           │                  │                      │                │
│  ┌────────┴───────┐ ┌───────┴──────┐ ┌─────────────┴─────────────┐ │
│  │ College        │ │ Location     │ │ FinanceService             │ │
│  │ Scorecard Svc  │ │ Service      │ │  - TaxCalculationService  │ │
│  │ (existing)     │ │ (SQLite)     │ │  - LoanAmortizationSvc    │ │
│  └────────┬───────┘ └──────────────┘ │  - HousingCostService     │ │
│           │                           └───────────────────────────┘  │
│  ┌────────┴───────┐                  ┌──────────────────────────┐   │
│  │ /api/jobs/     │                  │ /api/jobs/pulse           │   │
│  │  pulse         │                  │  → Adzuna / BLS fallback  │   │
│  └────────────────┘                  └──────────────────────────┘   │
│                                                                      │
│  SQLite: schools, programs, cbsa_locations, hud_fmr, bls_wages       │
└──────────────────────────────────────────────────────────────────────┘
```

### Phase 2 API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/locations/search?q={query}` | Search metro areas (CBSA) by name |
| GET | `/api/locations/{cbsaCode}/housing?type={1bed\|2bed}` | HUD FMR rent for location |
| GET | `/api/jobs/pulse?cipCode={cip}&cbsa={code}` | Job openings + salary data for field in area |
| GET | `/api/finance/net-pay?grossSalary={salary}&state={ST}` | Monthly net pay after taxes |
| POST | `/api/finance/simulator-baseline` | Full budget simulation (accepts school, program, location) |

### Phase 2 Data Models

#### Location / CBSA
```csharp
public record CbsaLocation(
    string CbsaCode,        // "12060" (Atlanta)
    string Name,            // "Atlanta-Sandy Springs-Alpharetta, GA"
    string State,           // Primary state
    string[] FipsCodes      // Constituent county FIPS codes (for HUD FMR mapping)
);
```

#### HUD Fair Market Rent
```csharp
public record FairMarketRent(
    string FipsCode,        // County-level FIPS (HUD's native key)
    string? CbsaCode,       // Linked CBSA (null for non-metro counties)
    int Year,
    int Efficiency,         // Studio
    int OneBedroom,
    int TwoBedroom,
    int ThreeBedroom,
    int FourBedroom
);
```

#### Budget Simulation Result
```csharp
public record BudgetSimulation(
    decimal GrossAnnualSalary,
    decimal GrossMonthly,
    decimal FederalTaxMonthly,
    decimal StateTaxMonthly,
    decimal NetMonthly,
    decimal RentMonthly,
    decimal LoanPaymentMonthly,
    decimal DisposableMonthly,
    string IncomeStatus       // "comfortable" | "tight" | "deficit"
);
```

#### Job Pulse Result
```csharp
public record JobPulseResult(
    string CipCode,
    string CbsaCode,
    int ActiveOpenings,
    decimal? LocalMedianSalary,
    decimal? ScorecardMedianEarnings,
    string DataSource           // "adzuna" | "bls" | "scorecard_only"
);
```

### Phase 2 Services

#### LocationService
- Queries local SQLite table of 156 major US metro areas (top CBSAs by population)
- Pre-seeded during import from static seed data; full Census delineation (~930 CBSAs) available as future enhancement

#### HousingCostService
- Queries HUD FMR data by CBSA code and bedroom count
- Data imported from annual HUD CSV download (same pattern as Scorecard import)

#### TaxCalculationService
- Zero external dependencies
- 2024 federal marginal brackets + standard deduction ($14,600 single)
- State tax: flat lookup table (0% for TX/FL/WA, flat % for most others, simplified progressive for CA/NY)
- Returns monthly breakdown

#### LoanAmortizationService
- Input: total debt at graduation (from Scorecard `median_debt` field), interest rate (default 5.5% federal), term (10 years)
- Output: monthly payment using standard amortization formula
- No external calls

#### JobPulseService (Interface + Implementations)
```csharp
public interface IJobPulseService
{
    Task<JobPulseResult?> GetPulseAsync(string cipCode, string cbsaCode, CancellationToken ct);
}
```
- `AdzunaJobPulseService` — live API calls (free tier, key required)
- `BlsJobPulseService` — static BLS Occupational Employment data (fallback, no key needed)
- Configurable via DI, same pattern as CollegeScorecardService local/remote/hybrid

### Phase 2 Frontend Components

```
App.vue
├── SchoolSearch.vue              (existing)
├── LocationSelector.vue          ← NEW: CBSA autocomplete + housing type toggle
├── YearSelector.vue              (existing)
├── SchoolDetail.vue              (existing, with TuitionTrend)
├── ProgramList.vue               (existing, MODIFIED: expandable rows)
│   └── JobPulseWidget.vue        ← NEW: inline job market data per program
└── BudgetSimulator.vue           ← NEW: the "consequences engine" dashboard
    ├── IncomeHeader.vue          ← Gross vs. Net headline numbers
    ├── ExpenseBreakdown.vue      ← Line items: rent, loan, etc.
    └── DisposableResult.vue      ← Final number + donut chart
```

### Phase 2 State Management
- Phase 2 introduces cross-component shared state (selected location affects budget, job pulse, etc.)
- Introduce Pinia store at Task 16 (Location Selector): `useAppStore` with `selectedSchool`, `selectedLocation`, `housingType`, `selectedYear`
- Composables consume store state reactively
- Budget simulator endpoint uses ONLY local/fast data (Scorecard earnings, HUD rents, tax math) — never calls Adzuna
- Job pulse widget fires independently on its own async endpoint to keep the dashboard snappy

### Phase 2 Import Tool Updates
- Add HUD FMR CSV import (download from huduser.gov/portal/datasets/fmr.html)
- Add CBSA lookup table import (from Census Bureau delineation files)
- Optional: BLS OEWS data import for static wage baselines

### CIP-to-Keyword Mapping Strategy
- Maintain a static mapping file (`cipJobKeywords.ts` or DB table) that maps 2-digit CIP categories to job search keywords
- Example: `"11"` (Computer Science) → `["software engineer", "developer", "data analyst", "IT"]`
- Example: `"52"` (Business) → `["business analyst", "marketing", "finance", "accounting"]`
- Used by JobPulseService to translate academic codes into job board search queries
