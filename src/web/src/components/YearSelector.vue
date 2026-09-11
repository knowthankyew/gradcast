<template>
  <v-card class="mb-6" elevation="2">
    <v-card-text>
      <v-row align="center">
        <v-col cols="12" sm="6" md="4">
          <v-select
            v-model="selectedYear"
            :items="yearOptions"
            item-title="label"
            item-value="value"
            label="Data Year"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-calendar"
            @update:model-value="onYearChanged"
          />
        </v-col>
        <v-col cols="12" sm="6" md="8">
          <v-tooltip location="bottom" max-width="360">
            <template #activator="{ props }">
              <v-chip
                v-bind="props"
                prepend-icon="mdi-information-outline"
                variant="tonal"
                color="info"
                size="small"
              >
                Why select a past year?
              </v-chip>
            </template>
            <span>
              College Scorecard data is released on a rolling basis. The most recent years
              may have incomplete data as metrics like earnings and completion rates require
              time to collect after students graduate. Selecting an earlier year (2–4 years back)
              often provides the most complete picture across all programs and data points.
            </span>
          </v-tooltip>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useAppStore } from '../stores/appStore'

const emit = defineEmits<{
  yearChanged: [year: number | null]
}>()

const store = useAppStore()

const currentYear = new Date().getFullYear()

const yearOptions = [
  { label: 'Latest Available', value: null },
  ...Array.from({ length: 10 }, (_, i) => {
    const year = currentYear - 1 - i
    return { label: `${year}–${year + 1}`, value: year }
  }),
]

const selectedYear = ref<number | null>(store.selectedYear)

watch(
  () => store.selectedYear,
  (newYear) => {
    if (selectedYear.value !== newYear) {
      selectedYear.value = newYear
    }
  }
)

function onYearChanged(value: number | null) {
  emit('yearChanged', value)
}
</script>
