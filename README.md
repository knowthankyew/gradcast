# GradCast

An AI-generated proof-of-concept web application that helps students simulate their financial future after graduation — based on real college costs, program-level earnings data, local housing markets, and tax calculations. Built on the U.S. Department of Education's [College Scorecard API](https://collegescorecard.ed.gov/data/api/) and HUD Fair Market Rent data.

## Tech Stack

- **Backend**: C# / ASP.NET Core 10 Minimal API
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vuetify 4 + Pinia
- **Data Layer**: SQLite via EF Core (local/hybrid mode) or live API calls (remote mode)
- **Build**: .NET CLI + Vite

## Features

### Phase 1: College Data Explorer
- Type-ahead school search with optional state filtering
- School overview: admission rate, enrollment, tuition, completion rate
- Program-level data grouped by CIP category (department)
- Median earnings (1 year post-graduation) per program
- 5-year tuition trend line chart (in-state vs. out-of-state)
- Year selector for historic data with completeness tooltip

### Phase 2: Budget Simulator ("The Consequences Engine")
- **Progressive disclosure UX**: each step reveals the next, guiding the user through choices
- **Program selection**: click any program row to use its earnings data in the simulation
- **Target destination picker**: search 156 US metro areas with housing type toggle (live alone / roommate)
- **Job Market Pulse**: live job openings, local median salary, and Scorecard earnings comparison via Adzuna API (matched by CIP program category and target metro area)
- **Post-Grad Monthly Budget Simulator**:
  - Net take-home pay (federal + state taxes + FICA)
  - Rent from HUD Fair Market Rents (by metro + housing type)
  - Student loan payment (standard 10-year amortization from estimated debt)
  - Disposable income with status indicator (comfortable / manageable / tight / deficit)
  - Budget split percentage bars
  - Salary override for what-if scenarios
- **Saved Scenarios**: name and save budget simulations to localStorage for comparison

### Data Modes
- **`api`** (default): All Scorecard requests go to the live API
- **`local`**: All requests query local SQLite database
- **`hybrid`** (recommended): Local DB for imported data, API fallback for historic years

## User Flow

```
1. Search for a school          → Type-ahead autocomplete
2. View school details          → Tuition, admission rate, completion rate, trend chart
3. Browse/select a program      → Click to lock in earnings data (optional)
4. Pick target destination      → Metro area + housing preference
5. Budget simulator appears     → Real numbers, real consequences
6. Save scenarios               → Compare different school/city/program combinations
5. Job market pulse appears     → Active job openings & local salary vs. Scorecard earnings
6. Budget simulator appears     → Real numbers, real consequences
7. Save scenarios               → Compare different school/city/program combinations
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- A free API key from [api.data.gov](https://api.data.gov/signup/) (required for `api` and `hybrid` modes)
- (Optional) Free API credentials from [developer.adzuna.com](https://developer.adzuna.com/) (for live Job Market Pulse data)

### Quick Start (Hybrid Mode — Recommended)

1. Clone the repo:
   ```bash
   git clone https://github.com/yourusername/gradcast.git
   cd gradcast
   ```

2. Download College Scorecard data from [collegescorecard.ed.gov/data](https://collegescorecard.ed.gov/data) — click "All Data Files Download (.zip, 470 MB)"

3. Import the data:
   ```bash
   dotnet run --project src/import -- ~/Downloads/CollegeScorecard_Raw_Data.zip
   ```
   This creates `gradcast.db` with schools, programs, metro areas, and housing costs.

4. Configure your API key:
   ```bash
   cd src/api
   dotnet user-secrets init
   dotnet user-secrets set "CollegeScorecard:ApiKey" "YOUR_API_KEY"
   ```

5. Run the API in hybrid mode:
   ```bash
   DataSource=hybrid dotnet run --project src/api
   ```
   API starts on `http://localhost:5062`

6. Run the frontend (in a separate terminal):
   ```bash
   cd src/web
   npm install
   npm run dev
   ```
   App starts on `http://localhost:5173`

7. Open `http://localhost:5173` and explore.

### Quick Start (API-only Mode — No Import Needed)

If you just want to try it without downloading the bulk data:

```bash
# Set your API key in src/api/appsettings.Development.json, then:
dotnet run --project src/import -- --seed-only  # Creates empty DB with metro/FMR seed data
dotnet run --project src/api
cd src/web && npm install && npm run dev
```

Note: Budget simulations require the imported metro/housing data (seeded automatically), but school data will come from the live API.

## Import Tool

```bash
# Point at the downloaded Scorecard zip
dotnet run --project src/import -- ~/Downloads/CollegeScorecard_Raw_Data.zip

# Or an extracted directory
dotnet run --project src/import -- ~/Downloads/scorecard_data/

# Seed reference data only (no bulk data download required)
dotnet run --project src/import -- --seed-only

# Custom database path
dotnet run --project src/import -- ~/Downloads/CollegeScorecard_Raw_Data.zip ./custom.db
```

The import is idempotent and also seeds:
- **156 CBSA metro areas** (Census Bureau delineation data)
- **FY2025 Fair Market Rents** for all metros (HUD data)

## Project Structure

```
gradcast/
├── src/
│   ├── api/                        # ASP.NET Core 10 backend
│   │   ├── Configuration/          # Options classes
│   │   ├── Endpoints/              # Minimal API route handlers
│   │   │   ├── SchoolEndpoints.cs      # /api/schools/*
│   │   │   ├── LocationEndpoints.cs    # /api/locations/*
│   │   │   └── FinanceEndpoints.cs     # /api/finance/*
│   │   │   ├── FinanceEndpoints.cs     # /api/finance/*
│   │   │   └── JobEndpoints.cs         # /api/jobs/*
│   │   ├── Models/                 # DTOs
│   │   └── Services/               # Business logic
│   │       ├── CollegeScorecardService.cs      # Remote Scorecard API
│   │       ├── LocalCollegeScorecardService.cs # SQLite queries
│   │       ├── HybridCollegeScorecardService.cs
│   │       ├── LocationService.cs              # Metro area search
│   │       ├── HousingCostService.cs           # HUD FMR lookup
│   │       ├── TaxCalculationService.cs        # Federal + state tax math
│   │       ├── LoanAmortizationService.cs      # Student loan payments
│   │       ├── AdzunaJobPulseService.cs        # Live job openings via Adzuna
│   │       └── BudgetSimulatorService.cs       # Orchestrates the full sim
│   ├── data/                       # EF Core class library
│   │   ├── Entities/               # School, Program, CbsaLocation, FairMarketRent, etc.
│   │   ├── SeedData/               # Static seed (metros + FMR values)
│   │   └── GradCastDbContext.cs
│   ├── import/                     # CLI import tool
│   └── web/                        # Vue 3 + Vuetify 4 frontend
│       └── src/
│           ├── components/         # UI components
│           │   ├── SchoolSearch.vue
│           │   ├── SchoolDetail.vue
│           │   ├── ProgramList.vue         # Selectable program rows
│           │   ├── LocationSelector.vue    # Metro + housing type
│           │   ├── JobPulseWidget.vue      # Active openings & salary comparison
│           │   ├── BudgetSimulator.vue     # The "consequences engine"
│           │   └── SavedBudgets.vue        # localStorage scenarios
│           ├── composables/        # API + logic composables
│           ├── stores/             # Pinia state management
│           ├── data/               # Static lookup data (CIP categories)
│           └── types/              # TypeScript interfaces
├── GradCast.slnx                   # .NET solution
└── README.md
```

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/schools/search?q={name}&state={ST}` | Search schools |
| GET | `/api/schools/{id}?year={year}` | School detail with programs |
| GET | `/api/schools/{id}/tuition-trend` | 5-year tuition history |
| GET | `/api/locations/search?q={query}` | Search metro areas |
| GET | `/api/locations/{cbsa}/housing?type=1bed\|2bed` | Fair Market Rent |
| GET | `/api/finance/net-pay?grossSalary=&state=` | Net pay calculator |
| GET | `/api/finance/loan-payment?principal=&rate=&termYears=` | Loan amortization |
| POST | `/api/finance/simulator` | Full budget simulation |
| GET | `/api/jobs/pulse?cipCode={cip}&cbsa={cbsa}` | Live job openings & salary data |

## Phase 3 Vision

- Job market pulse (Adzuna API integration for live job openings by CIP + metro)
- Multi-year career trajectory modeling
- Side-by-side school/city comparison mode
- Savings rate projections and emergency fund timelines
- Geographic arbitrage suggestions (same degree, different cities)

## Data Sources

| Source | Data | Update Frequency |
|--------|------|-----------------|
| [College Scorecard](https://collegescorecard.ed.gov/) | Schools, programs, earnings, completion rates | Annual |
| [HUD Fair Market Rents](https://www.huduser.gov/portal/datasets/fmr.html) | Rent by metro area | Annual |
| Census Bureau | CBSA metro area delineations | Decennial+ |
| Federal tax brackets | Income tax calculations | Annual (hardcoded) |
| [Adzuna](https://developer.adzuna.com/) | Job openings & local salaries by CIP/metro | Real-time API |

## License

MIT
