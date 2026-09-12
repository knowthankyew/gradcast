<template>
  <v-card elevation="2" class="mb-6 program-prompt-card" variant="outlined">
    <v-card-text class="pa-6">
      <div class="d-flex align-start flex-column flex-sm-row ga-4">
        <div class="prompt-icon-wrapper">
          <v-icon size="36" color="primary">mdi-school-outline</v-icon>
        </div>
        <div class="flex-grow-1">
          <div class="d-flex align-center flex-wrap ga-2 mb-1">
            <span class="text-h6 font-weight-bold">Select a Program to Generate Your Budget</span>
            <v-chip size="x-small" color="primary" variant="tonal" class="font-weight-medium">
              Step 3 Needed
            </v-chip>
          </div>
          <p class="text-body-2 text-medium-emphasis mb-4">
            Starting salaries differ significantly by major and degree level. Choose your program above to calculate your personalized net take-home pay, loan payments, and unlock live local job market demand.
          </p>

          <div class="d-flex align-center flex-wrap ga-3">
            <v-btn
              color="primary"
              variant="elevated"
              prepend-icon="mdi-arrow-up"
              @click="scrollToProgramList"
            >
              Choose a Program
            </v-btn>

            <v-btn
              variant="tonal"
              color="secondary"
              :prepend-icon="hasSchoolEarnings ? 'mdi-chart-bell-curve-cumulative' : 'mdi-calculator-variant'"
              @click="proceedWithSchoolAverage"
            >
              {{ hasSchoolEarnings ? 'Proceed with School-Wide Average' : 'Proceed without Major' }}
            </v-btn>
          </div>
          <div class="text-caption text-medium-emphasis mt-2">
            {{ hasSchoolEarnings
              ? 'Undecided? You can continue with the school-wide average now and pick a major at any time.'
              : 'Undecided? You can continue now to view fixed costs and enter a custom salary or use the national baseline.' }}
          </div>
        </div>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useAppStore } from '../stores/appStore'

const store = useAppStore()

const hasSchoolEarnings = computed(() => {
  return store.selectedSchool?.programs.some((p) => p.medianEarnings != null) ?? false
})

function scrollToProgramList() {
  const el = document.getElementById('program-list-card')
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    // Add brief highlight animation
    el.classList.add('program-card-highlight')
    setTimeout(() => {
      el.classList.remove('program-card-highlight')
    }, 1800)

    // Also focus the search input if present
    const searchInput = el.querySelector('input')
    if (searchInput) {
      searchInput.focus()
    }
  }
}

function proceedWithSchoolAverage() {
  store.setUseSchoolAverage(true)
}
</script>

<style scoped>
.program-prompt-card {
  border-color: rgba(var(--v-theme-primary), 0.3) !important;
  background: linear-gradient(
    to bottom right,
    rgba(var(--v-theme-primary), 0.03),
    rgba(var(--v-theme-surface), 1)
  );
}

.prompt-icon-wrapper {
  background: rgba(var(--v-theme-primary), 0.1);
  border-radius: 12px;
  padding: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
