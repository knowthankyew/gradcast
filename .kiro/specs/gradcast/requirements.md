# GradCast - Requirements

## Overview
GradCast is a single-page application (C# backend / Vue.js frontend) that allows students to explore college data from the U.S. Department of Education's College Scorecard API. Phase 1 focuses on school search and program-level graduation/completion data. Phase 2 (future) will layer in local job boards, housing costs, and budget projections.

## Phase 1 Scope

### Functional Requirements

#### FR-1: School Search (Type-Ahead Autocomplete)
- **FR-1.1**: User can type a partial school name into an input field and receive matching suggestions in real-time (debounced, ~300ms).
- **FR-1.2**: Suggestions display school name, city, and state.
- **FR-1.3**: Minimum 2 characters before triggering search.
- **FR-1.4**: Results are limited to 10 suggestions per query.
- **FR-1.5**: User selects a school from the dropdown to load its detail view.

#### FR-2: School Overview (Summary Card)
- **FR-2.1**: Display school name, city, state, and school URL.
- **FR-2.2**: Display institution type (public/private/for-profit).
- **FR-2.3**: Display overall admission rate (if available).
- **FR-2.4**: Display total undergraduate enrollment.
- **FR-2.5**: Display in-state and out-of-state tuition costs.
- **FR-2.6**: Display overall completion/graduation rate (150% time).

#### FR-3: Program-Level Graduation/Completion Data
- **FR-3.1**: Retrieve field-of-study (program) data for the selected school, using CIP 4-digit codes.
- **FR-3.2**: Group programs by CIP 2-digit category (e.g., "Engineering," "Business," "Health Professions").
- **FR-3.3**: For each program, display: program title, credential level (certificate, associate, bachelor's, etc.), and number of completions.
- **FR-3.4**: Allow expanding/collapsing program categories.
- **FR-3.5**: Sort programs within a category by number of completions (descending).

#### FR-4: API Integration
- **FR-4.1**: Backend proxies all calls to College Scorecard API (API key not exposed to frontend).
- **FR-4.2**: API key stored in configuration (appsettings.json / environment variable), never in source control.
- **FR-4.3**: Backend implements basic in-memory caching for school detail responses (5 minute TTL).
- **FR-4.4**: Handle API rate limits gracefully (1000 req/hour) — return appropriate error to frontend.

### Non-Functional Requirements

#### NFR-1: Performance
- Autocomplete suggestions return within 500ms perceived latency.
- School detail view loads within 2 seconds.

#### NFR-2: Technology Stack
- **Backend**: C# / ASP.NET Core 8 Minimal API.
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vite.
- **UI Framework**: Vuetify 3 (Material Design components for Vue 3).
- **HTTP Client**: Backend uses `HttpClient` with typed responses.
- **Target Hosting**: GitHub portfolio project; designed to be deployable but runs locally for PoC.

#### NFR-3: Developer Experience
- Solution structured as a monorepo with `/src/api` (C#) and `/src/web` (Vue).
- `dotnet run` starts the API; `npm run dev` starts the Vue dev server with proxy to API.
- README with setup instructions including API key registration link.

#### NFR-4: Error Handling
- Frontend displays user-friendly messages when API is unreachable or returns errors.
- Backend returns structured error responses (ProblemDetails).

---

## Phase 2 (Future — Out of Scope for Now)
- Integration with local job board APIs (Indeed, BLS, etc.).
- Housing cost data (HUD Fair Market Rents API or similar).
- Budget simulator: fixed costs (rent, student loan payments) vs. expected starting salary.
- Career path modeling based on program of study.

---

## Decisions
1. **State/city filter**: Yes — include optional state filter alongside name search.
2. **Median earnings**: Include in Phase 1 — per-program median earnings 1 year after graduation.
3. **UI Framework**: Vuetify 3 (Material Design, Bootstrap-familiar grid system).
4. **Component library**: Vuetify 3 provides autocomplete, expansion panels, cards, data tables.
5. **Portfolio goal**: This is a GitHub portfolio piece to demonstrate C#/Vue.js skills and show investors a tangible PoC of the GradCast vision.
