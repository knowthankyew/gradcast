# GradCast

A proof-of-concept web application that helps students explore college data — graduation rates, program offerings, tuition costs, and median earnings by field of study. Built on the U.S. Department of Education's [College Scorecard API](https://collegescorecard.ed.gov/data/api/).

## Tech Stack

- **Backend**: C# / ASP.NET Core 10 Minimal API
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vuetify 4
- **Data Layer**: SQLite via EF Core (local mode) or live API calls (remote mode)
- **Build**: .NET CLI + Vite

## Features

- Type-ahead school search with optional state filtering
- School overview: admission rate, enrollment, tuition, completion rate
- Program-level data grouped by CIP category (department)
- Median earnings (1 year post-graduation) per program
- Credential level breakdown (certificate through doctoral)
- 5-year tuition trend line chart (in-state vs. out-of-state)
- Year selector for historic data with completeness tooltip
- Three data modes: remote API, local SQLite, or hybrid (local + API fallback)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- A free API key from [api.data.gov](https://api.data.gov/signup/) (required for `api` and `hybrid` modes)

### Quick Start (API Mode)

1. Clone the repo:
   ```bash
   git clone https://github.com/yourusername/gradcast.git
   cd gradcast
   ```

2. Configure your API key:
   ```bash
   cd src/api
   dotnet user-secrets init
   dotnet user-secrets set "CollegeScorecard:ApiKey" "YOUR_API_KEY"
   ```

3. Run the API:
   ```bash
   dotnet run --project src/api
   ```
   API starts on `http://localhost:5062`

4. Run the frontend (in a separate terminal):
   ```bash
   cd src/web
   npm install
   npm run dev
   ```
   App starts on `http://localhost:5173`

5. Open `http://localhost:5173` and start searching for schools.

## Data Modes

GradCast supports three data source configurations, controlled by the `DataSource` setting in `appsettings.json`:

### `api` (default)
All requests go to the College Scorecard API. Simple, always up-to-date, but subject to rate limits (1,000 req/hour).

### `local`
All requests query a local SQLite database. Requires running the import tool first. Zero external calls, instant responses, works offline. Best for demos and development.

### `hybrid` (recommended for demos)
Searches and current-year data are served from the local SQLite database (instant). When the user selects a historic year that isn't in the local DB, the service transparently falls back to the College Scorecard API. Best of both worlds — fast for common operations, complete for exploratory use.

```json
// appsettings.json
{
  "DataSource": "hybrid",
  "DatabasePath": "gradcast.db"
}
```

## Import Tool

The import tool downloads the full College Scorecard dataset (~150MB of CSVs) and loads it into a local SQLite database.

### Usage

```bash
# Import to default location (./gradcast.db)
dotnet run --project src/import

# Import to a specific path
dotnet run --project src/import -- /path/to/gradcast.db
```

### What it imports

- **Institution data**: ~6,500 schools with name, location, ownership, tuition, admission rate, enrollment, and completion rate
- **Field of study data**: ~100,000+ program records with CIP codes, credential levels, completion counts, and median earnings

The import is idempotent — running it again updates existing records. The downloaded CSVs represent the "Most Recent Cohorts" release from the Department of Education.

### Pointing the API at the database

After importing, set your data source mode:

```bash
# Via environment variable
DataSource=hybrid dotnet run --project src/api

# Or edit src/api/appsettings.json / appsettings.Development.json
```

## Project Structure

```
gradcast/
├── src/
│   ├── api/                        # ASP.NET Core backend
│   │   ├── Configuration/          # Options classes
│   │   ├── Endpoints/              # Minimal API route handlers
│   │   ├── Models/                 # DTOs
│   │   └── Services/               # Data service implementations
│   │       ├── CollegeScorecardService.cs      # Remote API client
│   │       ├── LocalCollegeScorecardService.cs # SQLite queries
│   │       └── HybridCollegeScorecardService.cs # Local + API fallback
│   ├── data/                       # EF Core class library
│   │   ├── Entities/               # School, SchoolYearData, Program
│   │   └── GradCastDbContext.cs
│   ├── import/                     # CLI tool for CSV import
│   │   └── Program.cs
│   └── web/                        # Vue 3 frontend
│       └── src/
│           ├── components/         # Vue components
│           ├── composables/        # API composable (useSchoolApi)
│           ├── data/               # Static lookup data (CIP categories)
│           └── types/              # TypeScript interfaces
├── GradCast.slnx                   # .NET solution
└── README.md
```

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/schools/search?q={name}&state={ST}` | Search schools (min 2 chars, optional state filter) |
| GET | `/api/schools/{id}?year={year}` | School detail with programs (optional year) |
| GET | `/api/schools/{id}/tuition-trend` | 5-year tuition history |

## Phase 2 Vision

- Local job board integration (BLS, Indeed) for career placement data
- Housing cost data (HUD Fair Market Rents) by target metro area
- Budget simulator: student loan payments + living costs vs. expected starting salary
- Career path modeling based on chosen program of study

## Data Source

All institution and program data is sourced from the [College Scorecard](https://collegescorecard.ed.gov/) maintained by the U.S. Department of Education. The bulk CSV data is freely downloadable and explicitly intended for reuse.

## License

MIT
