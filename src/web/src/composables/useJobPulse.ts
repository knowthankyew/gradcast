import { ref, watch } from 'vue'
import { useAppStore } from '../stores/appStore'

export interface JobPulseData {
  cipCode: string
  cbsaCode: string
  searchKeywords: string
  activeOpenings: number
  localMedianSalary: number | null
  scorecardMedianEarnings: number | null
  dataSource: string
  locationName: string
}

export function useJobPulse() {
  const pulse = ref<JobPulseData | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const store = useAppStore()

  async function fetchPulse() {
    if (!store.selectedProgram || !store.selectedLocation) {
      pulse.value = null
      return
    }

    loading.value = true
    error.value = null

    try {
      const params = new URLSearchParams({
        cipCode: store.selectedProgram.cipCode,
        cbsa: store.selectedLocation.cbsaCode,
      })

      const response = await fetch(`/api/jobs/pulse?${params}`)

      if (!response.ok) {
        const problem = await response.json().catch(() => null)
        throw new Error(problem?.detail || `Job pulse failed (${response.status})`)
      }

      pulse.value = await response.json()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load job data'
      pulse.value = null
    } finally {
      loading.value = false
    }
  }

  // Auto-fetch when program or location changes
  watch(
    () => [store.selectedProgram?.cipCode, store.selectedLocation?.cbsaCode],
    () => {
      if (store.selectedProgram && store.selectedLocation) {
        fetchPulse()
      } else {
        pulse.value = null
      }
    },
    { immediate: true }
  )

  return {
    pulse,
    loading,
    error,
    fetchPulse,
  }
}
