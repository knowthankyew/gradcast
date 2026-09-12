import { ref } from 'vue'
import type { BudgetSimulation } from '../types'

export interface SavedBudgetContext {
  schoolId: number
  schoolName: string
  programCipCode: string | null
  programTitle: string | null
  programCredentialLevel: number | null
  programCredentialName: string | null
  cbsaCode: string
  locationName: string
  locationState: string
  housingType: '1bed' | '2bed'
}

export interface SavedBudget {
  id: string
  name: string
  savedAt: string
  context: SavedBudgetContext
  simulation: BudgetSimulation
}

const STORAGE_KEY = 'gradcast_saved_budgets'

export function useSavedBudgets() {
  const savedBudgets = ref<SavedBudget[]>(loadFromStorage())

  function loadFromStorage(): SavedBudget[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      if (!raw) return []
      const parsed = JSON.parse(raw)
      // Handle legacy format (no context field)
      return parsed.filter((b: any) => b.context || b.schoolName)
    } catch {
      return []
    }
  }

  function persist() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(savedBudgets.value))
  }

  function saveBudget(context: SavedBudgetContext, name: string, simulation: BudgetSimulation) {
    const entry: SavedBudget = {
      id: crypto.randomUUID(),
      name,
      savedAt: new Date().toISOString(),
      context,
      simulation,
    }

    savedBudgets.value.unshift(entry)
    persist()
    return entry
  }

  function deleteBudget(id: string) {
    savedBudgets.value = savedBudgets.value.filter((b) => b.id !== id)
    persist()
  }

  function clearAll() {
    savedBudgets.value = []
    persist()
  }

  return {
    savedBudgets,
    saveBudget,
    deleteBudget,
    clearAll,
  }
}
