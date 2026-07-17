import { ref, watch } from 'vue'
import { useAppStore } from '../stores/appStore'

export interface BudgetSimulation {
  grossAnnualSalary: number
  grossMonthly: number
  netMonthly: number
  effectiveTaxRate: number
  salarySource: string
  rentMonthly: number
  housingType: string
  loanPaymentMonthly: number
  loanPrincipal: number
  fixedCostsMonthly: number
  disposableMonthly: number
  incomeStatus: string
  locationName: string
  state: string
}

export function useBudgetSimulator() {
  const simulation = ref<BudgetSimulation | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const store = useAppStore()

  async function runSimulation(salaryOverride?: number) {
    if (!store.canSimulate) {
      simulation.value = null
      return
    }

    loading.value = true
    error.value = null

    try {
      const body = {
        schoolId: store.selectedSchoolId,
        cipCode: null as string | null,
        cbsaCode: store.selectedLocation!.cbsaCode,
        housingType: store.housingType,
        salaryOverride: salaryOverride ?? null,
      }

      const response = await fetch('/api/finance/simulator', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })

      if (!response.ok) {
        const problem = await response.json().catch(() => null)
        throw new Error(problem?.detail || `Simulation failed (${response.status})`)
      }

      simulation.value = await response.json()
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Simulation failed'
      simulation.value = null
    } finally {
      loading.value = false
    }
  }

  // Auto-run simulation when inputs change
  watch(
    () => [store.selectedSchoolId, store.selectedLocation, store.housingType],
    () => {
      if (store.canSimulate) {
        runSimulation()
      } else {
        simulation.value = null
      }
    },
    { deep: true }
  )

  return {
    simulation,
    loading,
    error,
    runSimulation,
  }
}
