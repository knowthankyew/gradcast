# GradCast - Implementation Tasks

## Task 1: Project Scaffolding
- [ ] Create solution file `GradCast.sln`
- [ ] Create ASP.NET Core 8 Minimal API project at `src/api/`
- [ ] Create Vue 3 + Vite + TypeScript project at `src/web/`
- [ ] Add Tailwind CSS to the Vue project
- [ ] Create README.md with setup instructions

## Task 2: Backend Configuration & Service Foundation
- [ ] Add `CollegeScorecardOptions` configuration class
- [ ] Configure `appsettings.json` with base URL and placeholder
- [ ] Set up `HttpClient` with typed client registration in DI
- [ ] Add `IMemoryCache` registration
- [ ] Create `ICollegeScorecardService` interface
- [ ] Implement `CollegeScorecardService` with HttpClient injection

## Task 3: Backend - School Search Endpoint
- [ ] Create `SchoolSearchResult` model (Id, Name, City, State)
- [ ] Implement `SearchSchoolsAsync` in service — calls Scorecard API with `school.name` filter
- [ ] Map Scorecard API JSON response to typed DTOs
- [ ] Create `GET /api/schools/search` endpoint with query parameter validation (min 2 chars)
- [ ] Return 400 ProblemDetails for invalid input

## Task 4: Backend - School Detail Endpoint
- [ ] Create `SchoolDetail` model (full school info + nested programs)
- [ ] Create `ProgramData` model (code, title, credential level, completions count)
- [ ] Implement `GetSchoolDetailAsync` in service — calls Scorecard API with `id` filter and full field list
- [ ] Add in-memory caching (5 min TTL) for detail responses
- [ ] Create `GET /api/schools/{id:int}` endpoint
- [ ] Return 404 ProblemDetails when school not found
- [ ] Handle upstream 429 → return 503 with Retry-After

## Task 5: Frontend - Project Setup & API Layer
- [ ] Define TypeScript types matching backend DTOs (`SchoolSearchResult`, `SchoolDetail`, `ProgramData`)
- [ ] Create `useSchoolApi` composable with `searchSchools(query)` and `getSchoolDetail(id)` functions
- [ ] Include loading state, error state, and data refs
- [ ] Configure Vite proxy to forward `/api` requests to backend

## Task 6: Frontend - SchoolSearch Component (Type-Ahead)
- [ ] Build `SchoolSearch.vue` with text input and results dropdown
- [ ] Implement debounced input (300ms) triggering API call
- [ ] Display results with school name, city, state
- [ ] Keyboard navigation: arrow keys, Enter to select, Esc to close
- [ ] ARIA attributes: combobox role, listbox, option, aria-activedescendant
- [ ] Emit `school-selected` event with school ID
- [ ] Show loading spinner during search

## Task 7: Frontend - SchoolDetail Component
- [ ] Build `SchoolDetail.vue` displaying the overview summary card
- [ ] Show: name, location, school URL (link), institution type, admission rate, enrollment, tuition (in-state/out-of-state), overall completion rate
- [ ] Handle missing data gracefully (show "N/A" or hide field)
- [ ] Loading skeleton while data fetches

## Task 8: Frontend - ProgramList Component (Grouped by Category)
- [ ] Build `ProgramList.vue` with collapsible category sections
- [ ] Create CIP 2-digit category lookup map (static data)
- [ ] Group programs by 2-digit CIP prefix, map to category name
- [ ] Sort categories alphabetically; sort programs within by completions descending
- [ ] Display program title, credential level (human-readable), and completion count
- [ ] Expand/collapse with smooth transition
- [ ] Show total programs count per category in header

## Task 9: App Assembly & Integration
- [ ] Wire components together in `App.vue`
- [ ] SchoolSearch at top → on select, fetch detail → render SchoolDetail + ProgramList
- [ ] Global error display (toast or banner) for API failures
- [ ] Responsive layout: centered card, max-width container
- [ ] Basic page header/branding ("GradCast")

## Task 10: Polish & Developer Experience
- [ ] Add `.gitignore` for .NET and Node artifacts
- [ ] Add `launch.json` / `tasks.json` for VS Code (optional)
- [ ] Verify end-to-end flow with a real API key
- [ ] Document any known limitations in README
