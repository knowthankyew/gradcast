# GradCast

A proof-of-concept web application that helps students explore college data — graduation rates, program offerings, tuition costs, and median earnings by field of study. Built on the U.S. Department of Education's [College Scorecard API](https://collegescorecard.ed.gov/data/api/).

## Tech Stack

- **Backend**: C# / ASP.NET Core 10 Minimal API
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vuetify 4
- **Build**: .NET CLI + Vite

## Features (Phase 1)

- Type-ahead school search with optional state filtering
- School overview: admission rate, enrollment, tuition, completion rate
- Program-level data grouped by CIP category (department)
- Median earnings (1 year post-graduation) per program
- Credential level breakdown (certificate through doctoral)

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- A free API key from [api.data.gov](https://api.data.gov/signup/)

### Setup

1. Clone the repo:
   ```bash
   git clone https://github.com/yourusername/gradcast.git
   cd gradcast
   ```

2. Configure your API key (the development config already has one, but for your own usage):
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

## Project Structure

```
gradcast/
├── src/
│   ├── api/                        # ASP.NET Core backend
│   │   ├── Configuration/          # Options classes
│   │   ├── Endpoints/              # Minimal API route handlers
│   │   ├── Models/                 # DTOs
│   │   └── Services/               # College Scorecard API client
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
| GET | `/api/schools/{id}` | School detail with program-level data |

## Phase 2 Vision

- Local job board integration (BLS, Indeed) for career placement data
- Housing cost data (HUD Fair Market Rents) by target metro area
- Budget simulator: student loan payments + living costs vs. expected starting salary
- Career path modeling based on chosen program of study

## Data Source

All institution and program data is sourced from the [College Scorecard API](https://collegescorecard.ed.gov/data/api/) maintained by the U.S. Department of Education. Rate limit: 1,000 requests/hour.

## License

MIT
