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
- **Backend**: C# / ASP.NET Core 10 Minimal API.
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vite.
- **UI Framework**: Vuetify 4 (Material Design components for Vue 3).
- **HTTP Client**: Backend uses `HttpClient` with typed responses.
- **State Management**: Pinia (introduced in Phase 2 for cross-component shared state).
- **Target Hosting**: GitHub portfolio project; designed to be deployable but runs locally for PoC.

#### NFR-3: Developer Experience
- Solution structured as a monorepo with `/src/api` (C#) and `/src/web` (Vue).
- `dotnet run` starts the API; `npm run dev` starts the Vue dev server with proxy to API.
- README with setup instructions including API key registration link.

#### NFR-4: Error Handling
- Frontend displays user-friendly messages when API is unreachable or returns errors.
- Backend returns structured error responses (ProblemDetails).

---

## Phase 2 Scope

### Phase 2A: Location & Career Destination

#### FR-5: Post-Graduation Target Location Selector
- **FR-5.1**: User can search and select a target metro area (CBSA) where they plan to live after graduation.
- **FR-5.2**: Location search uses type-ahead autocomplete against a local dataset of 156 major US metro areas (expandable to full ~930 CBSAs via Census import).
- **FR-5.3**: User selects a housing preference: "Live alone (1-Bed)" or "Have a roommate (Shared 2-Bed)."
- **FR-5.4**: Selected location persists across views and feeds into budget calculations.
- **FR-5.5**: UI placed as a "Target Destination" card between school search and school detail.

#### FR-6: Local Job Market Pulse
- **FR-6.1**: When a program row is expanded, show a "Local Job Pulse" widget for that field of study in the target metro.
- **FR-6.2**: Display number of active job openings matching the CIP category in the target location.
- **FR-6.3**: Show comparison: College Scorecard 1-year median earnings vs. local market salary data.
- **FR-6.4**: Job data sourced from Adzuna API (free tier, 250 req/day) with BLS OEWS as fallback/static baseline.
- **FR-6.5**: CIP-to-keyword mapping service translates academic program codes to job search terms.
- **FR-6.6**: Graceful degradation: if job API is unavailable, display Scorecard earnings only with a note.

### Phase 2B: Budget Simulator (The "Consequences Engine")

#### FR-7: Net Take-Home Pay Calculator
- **FR-7.1**: Given a gross annual salary (from program earnings data or user override), calculate estimated monthly net pay.
- **FR-7.2**: Apply federal tax brackets (2024 standard deduction, marginal rates).
- **FR-7.3**: Apply state income tax approximation based on target destination state.
- **FR-7.4**: Display gross monthly vs. net monthly side-by-side as headline figures.
- **FR-7.5**: Allow user to override the salary input manually for what-if scenarios.

#### FR-8: Housing & Debt Baseline
- **FR-8.1**: Auto-populate monthly rent from HUD Fair Market Rents for the selected CBSA and housing type.
- **FR-8.2**: Calculate estimated monthly student loan payment using the school's median debt at graduation (from Scorecard data) and standard 10-year federal loan amortization at current rates.
- **FR-8.3**: Display as a line-item breakdown: Rent, Loan Payment, and resulting Net Disposable Income.
- **FR-8.4**: Support a simple donut/bar chart showing the monthly budget split.

#### FR-9: Budget Simulator Dashboard
- **FR-9.1**: Aggregate all calculations into a "Post-Grad Monthly Budget Simulator" card.
- **FR-9.2**: Show: Gross Income → Net Pay → Fixed Costs (Rent + Loans) → Disposable Income.
- **FR-9.3**: Highlight disposable income with color coding (green if positive, red if negative/tight).
- **FR-9.4**: Update reactively as user changes school, program, location, or housing type.

### Phase 2 Non-Functional Requirements

#### NFR-5: Data Sources (Phase 2)
- **HUD FMR**: Import Fair Market Rent data by CBSA into local SQLite (annual CSV download from huduser.gov).
- **CBSA Lookup**: Static dataset of US metro areas with CBSA codes, names, states, and FIPS codes.
- **Adzuna API**: Free tier (250 req/day), API key in configuration. Stubbed interface for swap-ability.
- **Tax Brackets**: Hardcoded in-service, no external dependency. Updated annually as needed.

#### NFR-6: Architecture (Phase 2)
- All new services follow the same interface pattern (injectable, testable, swappable).
- Budget calculations are backend-only (keeps financial logic server-side and testable).
- Frontend components are composable and independently loadable (no budget card shown until location is selected).

---

## Phase 3 (Future — Out of Scope)
- Multi-year career trajectory modeling (5-year, 10-year salary growth curves)
- Side-by-side school comparison mode
- Savings rate projections and emergency fund timelines
- Geographic arbitrage suggestions (same degree, different cities)

---

## Decisions
1. **State/city filter**: Yes — include optional state filter alongside name search.
2. **Median earnings**: Include in Phase 1 — per-program median earnings 1 year after graduation.
3. **UI Framework**: Vuetify 4 (Material Design, Bootstrap-familiar grid system).
4. **Component library**: Vuetify 4 provides autocomplete, expansion panels, cards, data tables.
5. **Portfolio goal**: This is a GitHub portfolio piece to demonstrate C#/Vue.js skills and show investors a tangible PoC of the GradCast vision.
