<template>
  <v-card elevation="2" class="mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon class="mr-2">mdi-chart-line</v-icon>
      Tuition Trend (5 Year)
    </v-card-title>

    <v-card-text>
      <div v-if="loading" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <v-alert v-else-if="error" type="error" variant="tonal" density="compact">
        {{ error }}
      </v-alert>

      <v-alert v-else-if="!hasData" type="info" variant="tonal" density="compact">
        No tuition trend data available for this school.
      </v-alert>

      <div v-else style="position: relative; height: 280px;">
        <Line :data="chartData" :options="chartOptions" />
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Line } from 'vue-chartjs'
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Title,
  Tooltip,
  Legend,
} from 'chart.js'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend)

interface TuitionTrendPoint {
  year: number
  inState: number | null
  outOfState: number | null
}

const props = defineProps<{
  schoolId: number
}>()

const loading = ref(false)
const error = ref<string | null>(null)
const trendData = ref<TuitionTrendPoint[]>([])

const hasData = computed(() => trendData.value.length > 0)

const chartData = computed(() => ({
  labels: trendData.value.map((p) => `${p.year}–${p.year + 1}`),
  datasets: [
    {
      label: 'In-State',
      data: trendData.value.map((p) => p.inState),
      borderColor: '#1565C0',
      backgroundColor: 'rgba(21, 101, 192, 0.1)',
      tension: 0.3,
      fill: false,
    },
    {
      label: 'Out-of-State',
      data: trendData.value.map((p) => p.outOfState),
      borderColor: '#E65100',
      backgroundColor: 'rgba(230, 81, 0, 0.1)',
      tension: 0.3,
      fill: false,
    },
  ],
}))

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'top' as const,
    },
    tooltip: {
      callbacks: {
        label: (context: any) => {
          const value = context.parsed.y
          if (value == null) return `${context.dataset.label}: N/A`
          return `${context.dataset.label}: ${new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            maximumFractionDigits: 0,
          }).format(value)}`
        },
      },
    },
  },
  scales: {
    y: {
      ticks: {
        callback: (value: any) =>
          new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            maximumFractionDigits: 0,
          }).format(value),
      },
    },
  },
}

async function fetchTrend() {
  loading.value = true
  error.value = null

  try {
    const response = await fetch(`/api/schools/${props.schoolId}/tuition-trend`)
    if (!response.ok) {
      const problem = await response.json().catch(() => null)
      throw new Error(problem?.detail || `Failed to load trend data (${response.status})`)
    }
    trendData.value = await response.json()
  } catch (e) {
    error.value = e instanceof Error ? e.message : 'An unexpected error occurred'
    trendData.value = []
  } finally {
    loading.value = false
  }
}

watch(
  () => props.schoolId,
  () => fetchTrend(),
  { immediate: true }
)
</script>
