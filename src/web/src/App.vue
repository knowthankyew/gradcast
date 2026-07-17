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
        <SchoolSearch @school-selected="onSchoolSelected" />

        <LocationSelector v-if="store.hasSchool" />

        <YearSelector v-if="store.hasSchool" @year-changed="onYearChanged" />

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

        <div v-if="detailLoading" class="d-flex justify-center my-8">
          <v-progress-circular indeterminate color="primary" size="48" />
        </div>

        <template v-if="store.selectedSchool && !detailLoading">
          <SchoolDetail :school="store.selectedSchool" />
          <ProgramList :programs="store.selectedSchool.programs" />
          <BudgetSimulator v-if="store.canSimulate" />
        </template>

        <v-card v-if="!store.selectedSchool && !detailLoading && !error" class="text-center pa-8" variant="tonal">
          <v-icon size="64" color="primary" class="mb-4">mdi-magnify</v-icon>
          <div class="text-h6 mb-2">Search for a school to get started</div>
          <div class="text-body-2 text-medium-emphasis">
            Explore graduation rates, program data, and median earnings across U.S. colleges and universities.
          </div>
        </v-card>
      </v-container>
    </v-main>

    <v-footer app class="text-center text-caption text-medium-emphasis pa-4">
      Data provided by the U.S. Department of Education
      <a href="https://collegescorecard.ed.gov/" target="_blank" rel="noopener" class="ml-1">
        College Scorecard
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
