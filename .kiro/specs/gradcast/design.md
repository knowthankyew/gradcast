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
│              ASP.NET Core 8 Minimal API                  │
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
- Vuetify 3 with default Material Design theme (customized brand colors).
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
