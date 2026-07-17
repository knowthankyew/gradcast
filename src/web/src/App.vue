<template>
  <v-app>
    <v-app-bar color="primary" density="comfortable">
      <v-app-bar-title>
        <v-icon class="mr-2">mdi-school-outline</v-icon>
        GradCast
      </v-app-bar-title>
      <template #append>
        <span class="text-caption text-medium-emphasis mr-4">College Data Explorer</span>
      </template>
    </v-app-bar>

    <v-main>
      <v-container class="py-6" style="max-width: 1100px;">

        <!-- Step 1: Search for a school -->
        <SchoolSearch @school-selected="onSchoolSelected" />

        <!-- Loading -->
        <div v-if="detailLoading" class="d-flex justify-center my-8">
          <v-progress-circular indeterminate color="primary" size="48" />
        </div>

        <!-- Error -->
        <v-alert
          v-if="error"
          type="error"
          variant="tonal"
          closable
          class="mb-6"
          @click:close="error = null"
        >
          {{ error }}
        </v-alert>

        <!-- Step 2: School detail + year selector (after school selected) -->
        <template v-if="store.hasSchool && !detailLoading">
          <SchoolDetail :school="store.selectedSchool!" />
          <YearSelector @year-changed="onYearChanged" />

          <!-- Step 3: Program selection (after school loads) -->
          <ProgramList :programs="store.selectedSchool!.programs" />

          <!-- Step 4: Target destination (after school loads — program optional) -->
          <LocationSelector />

          <!-- Step 5: Job market pulse (when program AND location are selected) -->
          <JobPulseWidget v-if="store.hasProgram && store.hasLocation" />

          <!-- Step 6: Budget simulation (after location selected) -->
          <BudgetSimulator v-if="store.canSimulate" />
        </template>

        <!-- Empty state -->
        <v-card v-if="!store.hasSchool && !detailLoading && !error" class="text-center pa-8" variant="tonal">
          <v-icon size="64" color="primary" class="mb-4">mdi-magnify</v-icon>
          <div class="text-h6 mb-2">Search for a school to get started</div>
          <div class="text-body-2 text-medium-emphasis">
            Explore graduation rates, program data, and median earnings across U.S. colleges and universities.
            Then pick a target city to see your post-graduation budget.
          </div>
        </v-card>
      </v-container>
    </v-main>

    <v-footer app class="text-center text-caption text-medium-emphasis pa-4">
      Data provided by the
      <a href="https://collegescorecard.ed.gov/" target="_blank" rel="noopener" class="ml-1">
        U.S. Dept. of Education
      </a>
      &amp;
      <a href="https://www.huduser.gov/portal/datasets/fmr.html" target="_blank" rel="noopener" class="ml-1">
        HUD Fair Market Rents
      </a>
    </v-footer>
  </v-app>
</template>

<script setup lang="ts">
import SchoolSearch from './components/SchoolSearch.vue'
import SchoolDetail from './components/SchoolDetail.vue'
import ProgramList from './components/ProgramList.vue'
import YearSelector from './components/YearSelector.vue'
import LocationSelector from './components/LocationSelector.vue'
import JobPulseWidget from './components/JobPulseWidget.vue'
import BudgetSimulator from './components/BudgetSimulator.vue'
import { useSchoolApi } from './composables/useSchoolApi'
import { useAppStore } from './stores/appStore'

const store = useAppStore()
const { detailLoading, error, getSchoolDetail } = useSchoolApi()

async function onSchoolSelected(id: number) {
  const detail = await getSchoolDetail(id, store.selectedYear)
  if (detail) {
    store.setSchool(detail)
  }
}

async function onYearChanged(year: number | null) {
  store.setYear(year)
  if (store.selectedSchoolId) {
    const detail = await getSchoolDetail(store.selectedSchoolId, year)
    if (detail) {
      store.setSchool(detail)
    }
  }
}
</script>
