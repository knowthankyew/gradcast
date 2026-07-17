<template>
  <v-card elevation="2" class="mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon class="mr-2" color="deep-purple">mdi-briefcase-search</v-icon>
      Job Market Pulse
      <v-chip class="ml-3" size="small" :color="dataSourceColor" variant="tonal">
        <v-icon start size="x-small">{{ dataSourceIcon }}</v-icon>
        {{ dataSourceLabel }}
      </v-chip>
    </v-card-title>
    <v-card-subtitle>
      {{ store.selectedProgram?.title }} in {{ pulse?.locationName ?? store.selectedLocation?.name }}
    </v-card-subtitle>

    <v-card-text>
      <!-- Loading -->
      <div v-if="loading" class="d-flex justify-center py-6">
        <v-progress-circular indeterminate color="deep-purple" size="36" />
      </div>

      <!-- Error -->
      <v-alert v-else-if="error" type="warning" variant="tonal" density="compact">
        {{ error }}
      </v-alert>

      <!-- Results -->
      <template v-else-if="pulse">
        <v-row>
          <!-- Active Openings -->
          <v-col cols="12" sm="4">
            <div class="text-caption text-medium-emphasis">Active Openings</div>
            <div class="text-h5 font-weight-medium" :class="pulse.activeOpenings > 0 ? 'text-deep-purple' : 'text-grey'">
              {{ pulse.activeOpenings > 0 ? pulse.activeOpenings.toLocaleString() : 'N/A' }}
            </div>
            <div v-if="pulse.activeOpenings === 0" class="text-caption text-medium-emphasis">
              Live data requires Adzuna API key
            </div>
          </v-col>

          <!-- Local Median Salary -->
          <v-col cols="12" sm="4">
            <div class="text-caption text-medium-emphasis">Local Market Salary</div>
            <div class="text-h5 font-weight-medium" :class="pulse.localMedianSalary ? 'text-green-darken-2' : 'text-grey'">
              {{ pulse.localMedianSalary ? formatCurrency(pulse.localMedianSalary) : 'N/A' }}
            </div>
            <div v-if="pulse.localMedianSalary" class="text-caption text-medium-emphasis">
              Local average from job listings
            </div>
          </v-col>

          <!-- Scorecard Earnings -->
          <v-col cols="12" sm="4">
            <div class="text-caption text-medium-emphasis">National Median (Scorecard)</div>
            <div class="text-h5 font-weight-medium" :class="pulse.scorecardMedianEarnings ? 'text-blue' : 'text-grey'">
              {{ pulse.scorecardMedianEarnings ? formatCurrency(pulse.scorecardMedianEarnings) : 'N/A' }}
            </div>
            <div v-if="pulse.scorecardMedianEarnings" class="text-caption text-medium-emphasis">
              1 year post-graduation, all schools
            </div>
          </v-col>
        </v-row>

        <!-- Earnings comparison bar -->
        <div v-if="pulse.scorecardMedianEarnings" class="mt-4">
          <div class="text-caption text-medium-emphasis mb-1">Earnings Context</div>
          <div class="d-flex align-center ga-2">
            <v-chip size="x-small" color="blue" variant="tonal">
              National: {{ formatCurrency(pulse.scorecardMedianEarnings) }}
            </v-chip>
            <v-icon size="x-small" v-if="pulse.localMedianSalary">mdi-arrow-right</v-icon>
            <v-chip v-if="pulse.localMedianSalary" size="x-small" color="green" variant="tonal">
              Local: {{ formatCurrency(pulse.localMedianSalary) }}
              <span class="ml-1">({{ salaryDiffLabel }})</span>
            </v-chip>
          </div>
        </div>

        <!-- Search terms transparency -->
        <div class="mt-4">
          <div class="text-caption text-medium-emphasis">
            <v-icon size="x-small" class="mr-1">mdi-magnify</v-icon>
            Search terms: {{ pulse.searchKeywords }}
          </div>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useJobPulse } from '../composables/useJobPulse'
import { useAppStore } from '../stores/appStore'

const store = useAppStore()
const { pulse, loading, error } = useJobPulse()

const dataSourceColor = computed(() => {
  if (!pulse.value) return 'grey'
  return pulse.value.dataSource === 'adzuna' ? 'green' : 'blue-grey'
})

const dataSourceIcon = computed(() => {
  if (!pulse.value) return 'mdi-help'
  return pulse.value.dataSource === 'adzuna' ? 'mdi-lightning-bolt' : 'mdi-database'
})

const dataSourceLabel = computed(() => {
  if (!pulse.value) return ''
  switch (pulse.value.dataSource) {
    case 'adzuna': return 'Live data'
    case 'scorecard_only': return 'National baseline'
    default: return pulse.value.dataSource
  }
})

const salaryDiffLabel = computed(() => {
  if (!pulse.value?.localMedianSalary || !pulse.value?.scorecardMedianEarnings) return ''
  const diff = pulse.value.localMedianSalary - pulse.value.scorecardMedianEarnings
  const pct = (diff / pulse.value.scorecardMedianEarnings) * 100
  if (pct > 0) return `+${pct.toFixed(0)}%`
  return `${pct.toFixed(0)}%`
})

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}
</script>
