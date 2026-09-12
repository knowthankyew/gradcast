import { ref, watch } from 'vue'
import { useAppStore } from '../stores/appStore'

export interface BudgetSimulation {
  grossAnnualSalary: number
  grossMonthly: number
  netMonthly: number
  effectiveTaxRate: number
  salarySource: string
  hasReportedEarnings: boolean
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

const simulation = ref<BudgetSimulation | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

export function useBudgetSimulator() {

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
        cipCode: store.selectedProgram?.cipCode ?? null,
        credentialLevel: store.selectedProgram?.credentialLevel ?? null,
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

  // Auto-run simulation when any relevant input changes
  // immediate: true ensures it fires on first render if canSimulate is already true
  watch(
    () => [
      store.selectedSchoolId,
      store.selectedLocation?.cbsaCode,
      store.housingType,
      store.selectedProgram?.cipCode,
      store.useSchoolAverage,
    ],
    () => {
      if (store.canSimulate) {
        runSimulation()
      } else {
        simulation.value = null
      }
    },
    { immediate: true }
  )

  return {
    simulation,
    loading,
    error,
    runSimulation,
  }
}
