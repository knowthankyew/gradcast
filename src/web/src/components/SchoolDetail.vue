<template>
  <v-card v-if="school" elevation="2" class="mb-6">
    <v-card-title class="d-flex align-center" role="heading" aria-level="2">
      <v-icon class="mr-2">mdi-school</v-icon>
      {{ school.name }}
      <v-chip class="ml-3" size="small" :color="ownershipColor">
        {{ school.ownershipName }}
      </v-chip>
    </v-card-title>

    <v-card-subtitle>
      <v-icon size="small" class="mr-1">mdi-map-marker</v-icon>
      {{ school.city }}, {{ school.state }}
      <a
        v-if="school.schoolUrl"
        :href="formattedUrl"
        target="_blank"
        rel="noopener"
        class="ml-3"
      >
        <v-icon size="small" class="mr-1">mdi-open-in-new</v-icon>
        Visit Website
      </a>
    </v-card-subtitle>

    <v-card-text>
      <v-row>
        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis">Admission Rate</div>
          <div class="text-h6">
            {{ school.admissionRate != null ? formatPercent(school.admissionRate) : 'N/A' }}
          </div>
        </v-col>

        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis">Undergraduate Enrollment</div>
          <div class="text-h6">
            {{ school.studentSize != null ? school.studentSize.toLocaleString() : 'N/A' }}
          </div>
        </v-col>

        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis d-inline-flex align-center">
            Completion Rate
            <v-tooltip location="bottom" max-width="320">
              <template #activator="{ props: tooltipProps }">
                <v-icon v-bind="tooltipProps" size="x-small" class="ml-1" color="grey">mdi-information-outline</v-icon>
              </template>
              <span>
                Measured at "150% time" — meaning students who finished within 1.5x the
                expected program length (e.g., 6 years for a 4-year degree). This is the
                federal standard for measuring on-time graduation.
              </span>
            </v-tooltip>
          </div>
          <div class="text-h6">
            {{ school.completionRate != null ? formatPercent(school.completionRate) : 'N/A' }}
          </div>
        </v-col>

        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis">Tuition (In-State)</div>
          <div class="text-h6">
            {{ school.tuitionInState != null ? formatCurrency(school.tuitionInState) : 'N/A' }}
          </div>
        </v-col>

        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis">Tuition (Out-of-State)</div>
          <div class="text-h6">
            {{ school.tuitionOutOfState != null ? formatCurrency(school.tuitionOutOfState) : 'N/A' }}
          </div>
        </v-col>

        <v-col cols="12" sm="6" md="4">
          <div class="text-caption text-medium-emphasis">Programs Offered</div>
          <div class="text-h6">
            {{ school.programs.length }}
          </div>
        </v-col>
      </v-row>

      <v-btn
        class="mt-4"
        variant="tonal"
        color="primary"
        size="small"
        :prepend-icon="showTrend ? 'mdi-chart-line-variant' : 'mdi-chart-line'"
        @click="showTrend = !showTrend"
      >
        {{ showTrend ? 'Hide' : 'Show' }} Tuition Trend
      </v-btn>

      <div v-if="showTrend" class="mt-4">
        <TuitionTrend :school-id="school.id" />
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { SchoolDetail } from '../types'
import TuitionTrend from './TuitionTrend.vue'

const props = defineProps<{
  school: SchoolDetail
}>()

const showTrend = ref(false)

// Reset trend visibility when a different school is selected
watch(() => props.school.id, () => {
  showTrend.value = false
})
const ownershipColor = computed(() => {
  switch (props.school.ownership) {
    case 1: return 'blue'
    case 2: return 'green'
    case 3: return 'orange'
    default: return 'grey'
  }
})

const formattedUrl = computed(() => {
  const url = props.school.schoolUrl
  if (!url) return ''
  return url.startsWith('http') ? url : `https://${url}`
})

function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}
</script>
