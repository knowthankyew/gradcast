# GradCast - Requirements

## Overview

GradCast is a local-first single-page application (C# backend / Vue.js frontend) that simulates a student's post-graduation financial reality. It combines federal education data (College Scorecard), housing market data (HUD Fair Market Rents), job market signals (Adzuna), and tax/loan calculations into a "consequences engine" that answers: *"If I study X at School Y and move to City Z, what does my monthly budget actually look like?"*

**Core philosophy**: Runs locally or not at all. No cloud hosting dependencies. All reference data is either seeded statically or imported from downloadable government datasets. External APIs (Scorecard, Adzuna) are optional enhancements, not hard dependencies.

## Current State (Built)

### Phase 1: College Data Explorer (Complete)

#### FR-1: School Search
- Type-ahead autocomplete with debounced input (300ms, min 2 chars)
- Optional state filter
- Results: school name, city, state (top 10)

#### FR-2: School Overview
- School name, city, state, URL, institution type (public/private/for-profit)
- Admission rate, undergraduate enrollment, in-state/out-of-state tuition
- Completion rate with tooltip explaining "150% time"

#### FR-3: Program Data
- Field-of-study data grouped by CIP 2-digit category
- Selectable program rows (clicked → feeds earnings data into budget simulator)
- Per-program: title, credential level, completions, median earnings (1 yr post-grad)
- Expand/collapse by department, sorted by completions descending

#### FR-4: Historical Data
- Year selector (Latest + past 10 years)
- 5-year tuition trend line chart (in-state vs. out-of-state)
- Tooltip explaining data completeness lag for recent years

#### FR-5: Data Modes
- **API mode**: Live College Scorecard API calls (needs API key)
- **Local mode**: All queries from local SQLite (needs import)
- **Hybrid mode** (recommended): Local for imported data, API fallback for historic years

### Phase 2: The Consequences Engine (Complete)

#### FR-6: Target Location Selector
- Type-ahead search across 156 seeded US metro areas (CBSA)
- Housing type toggle: "Live Alone (1-Bed)" / "Roommate (Shared 2-Bed)"
- Progressive disclosure: appears after school is selected

#### FR-7: Job Market Pulse
- Appears when both a program AND location are selected
- Displays: active openings, local salary, Scorecard national median
- CIP-to-keyword mapping (37 categories → job search terms)
- Data source badge: "Live data" (Adzuna) vs. "National baseline" (Scorecard only)
- Gracefully degrades when Adzuna isn't configured

#### FR-8: Budget Simulator
- Net take-home pay: federal marginal brackets + FICA + state tax
- Housing cost: HUD FMR by CBSA + housing type (1-bed full, 2-bed split)
- Student loan payment: standard 10-year amortization from estimated debt
- Disposable income with status: comfortable / manageable / tight / deficit
- Budget split percentage bars
- Salary override for what-if scenarios

#### FR-9: Saved Scenarios
- Name and save budget simulations to localStorage
- Load-back: restores school/program/location/housing into app state
- Delete individual or clear all
- Persists across page reloads

---

## Non-Functional Requirements

### NFR-1: Technology Stack
- **Backend**: C# / ASP.NET Core 10 Minimal API
- **Frontend**: Vue 3 (Composition API) + TypeScript + Vite + Vuetify 4
- **State**: Pinia (cross-component shared state)
- **Data**: SQLite via EF Core (local/hybrid modes)
- **Target**: Local development only. GitHub portfolio piece. No cloud deployment planned.

### NFR-2: Data Externalization (No Hardcoded Reference Data)
All mutable reference data lives in external configuration files or SQLite, not in compiled code:
- **Tax brackets**: Versioned JSON files (`Configuration/TaxData/tax_config_YYYY.json`). Update annually by adding a new file — no recompile.
- **CBSA metro areas**: Seeded from static class, expandable via Census Bureau import.
- **HUD Fair Market Rents**: Seeded from static class, expandable via HUD CSV import.
- **CIP-to-keyword mapping**: Static dictionary in code (acceptable — changes infrequently). Future: move to JSON or DB table.
- **Loan rates**: Configurable per-request (default 5.5%, overridable).

### NFR-3: Graceful Degradation
- Every external API call has a fallback path (local DB or static data).
- Adzuna unconfigured → Scorecard-only earnings displayed.
- Scorecard API down → local SQLite data served.
- Missing data fields → "N/A" shown, never errors to the user.

### NFR-4: Progressive Disclosure UX
- Each step in the flow only appears after the previous completes.
- Flow: Search → School Detail → Programs → Location → Job Pulse → Budget
- Prevents cognitive overload; guides the user toward the "aha" moment.

### NFR-5: State Race Condition Prevention
- `isRestoring` mutex in Pinia store prevents watchers from firing duplicate API calls during saved scenario load-back.
- Budget simulator watcher skips execution while `isRestoring` is true.

---

## In Progress

### Feedback Integration (Current Sprint)
- [x] Tax brackets externalized to versioned JSON config (2026 values live)
- [x] `isRestoring` mutex in Pinia store (prevents watcher race conditions on load-back)
- [x] CIP keyword map enhanced to 4-digit granularity with 2-digit fallback
- [x] Legal/data disclaimer banner in UI
- [x] Spec docs brought current (this document)

---

## Future Consideration (Phase 3)

### Comparison & Planning
- Side-by-side comparison mode (2-3 saved scenarios as columns)
- Multi-year career trajectory modeling (5yr/10yr salary growth curves)
- Savings rate projections and emergency fund timelines
- Geographic arbitrage suggestions (same degree, different city = better budget)

### Data Enrichment
- Import per-year historic Scorecard CSVs (full tuition trend without API)
- Import full Census Bureau CBSA delineation (~930 metros + FIPS mapping)
- Import HUD FMR by county FIPS (non-metro coverage)
- BLS Occupational Employment data (static wage baselines, no API key)
- 4-digit CIP keyword mapping moved to JSON or DB for easier maintenance

### UX Polish
- Mobile responsive testing (progressive disclosure card stacking)
- Loading skeleton states
- Export budget to PDF (for parents/counselors)
- Screenshot tour in README

### Structural
- Integration tests for tax + loan + rent math (deterministic, high-value)
- Disclaimer/legal text (data lag, not financial advice, individual results vary)
- Error boundary component for graceful failure recovery

---

## Decisions Log
1. **Runs locally only** — no cloud deployment, no hosting cost concerns.
2. **Hybrid mode recommended** — local DB for speed, API fallback for completeness.
3. **Tax config externalized** — JSON files versioned by year, loaded at startup.
4. **Seed data in code** — CBSAs + FMR are small, stable datasets. Import tool expands if needed.
5. **Adzuna optional** — graceful fallback means the app is fully functional without it.
6. **No BLS import yet** — noted as future enhancement, Scorecard median is sufficient baseline.
7. **localStorage for saves** — no user accounts, no backend persistence for user data.
8. **Progressive disclosure** — guided flow, not a dashboard dump.
9. **Budget endpoint decoupled from Adzuna** — only uses fast local data. Job pulse is async/independent.
