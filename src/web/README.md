# GradCast Web Client

The front-end user interface for GradCast, built with **Vue 3**, **Pinia**, **Vuetify 4**, and **TypeScript**.

## Architecture & Principles

- **Composition API (`<script setup>`)**: Modern Vue 3 script setup pattern with strict TypeScript typing.
- **State Management (Pinia)**:
  - Centralized store in `src/stores/appStore.ts`.
  - Single source of truth for selected school, historic year, program, destination metro area, and housing choice.
  - State restoration mutex (`isRestoring`) to prevent cascading watchers from firing during scenario hydration.
  - `updateSchoolDetail` action to refresh school data on year changes without destroying downstream user selections.
- **Composable Architecture**:
  - `useSchoolApi`: School search, detail lookup, and tuition trends.
  - `useBudgetSimulator`: Shared singleton composable for monthly budget calculations and what-if salary overrides.
  - `useJobPulse`: Live and baseline employment metrics via Adzuna and Scorecard data.
  - `useSavedBudgets`: Scenario persistence in `localStorage`.

## Getting Started

```bash
# Install dependencies
npm install

# Start Vite dev server with proxy to backend (http://localhost:5062)
npm run dev

# Typecheck and build for production
npm run build

# Preview production build locally
npm run preview
```

## End-to-End Testing (Playwright)

Playwright is configured in `playwright.config.ts` to automatically spin up the Vite server and execute tests in Chromium. Network requests to `/api/**` are mocked deterministically using `page.route()`, ensuring tests run fast and independently of backend services or database state.

```bash
# Run all E2E test suites headless
npm run test:e2e

# Run with interactive Playwright UI (time-travel debugging, DOM inspection)
npm run test:e2e:ui

# Run tests in a visible browser window
npm run test:e2e:headed
```

### Test Suites (`e2e/`)

1. **`gradcast-flow.spec.ts`**: Verifies the entire progressive disclosure user journey:
   - Initial empty state and disclaimer banner (persistence across reloads).
   - Searching for a school, viewing stats, and toggling tuition trends.
   - Expanding department categories and selecting a degree program.
   - Selecting a destination metro area and toggling roommate options.
   - Verifying Job Market Pulse and Budget Simulator output.

2. **`saved-scenarios.spec.ts`**: Verifies scenario persistence:
   - Saving scenarios to `localStorage`.
   - Clearing state back to home and rehydrating the full scenario into UI & Pinia.
   - Deleting scenarios and bulk-clearing with confirmation dialog.

3. **`what-if-scenarios.spec.ts`**: Verifies calculation overrides & reactivity resilience:
   - Entering custom salary overrides, recalculating take-home pay, and resetting back to scorecard median.
   - Changing historical scorecard data years while preserving destination city and budget calculations.
   - Clearing autocomplete fields and verifying state cleanup.
