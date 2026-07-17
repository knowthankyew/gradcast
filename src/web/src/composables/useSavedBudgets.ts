import { ref } from 'vue'
import type { BudgetSimulation } from './useBudgetSimulator'

export interface SavedBudget {
  id: string
  name: string
  savedAt: string
  schoolName: string
  programTitle: string | null
  locationName: string
  simulation: BudgetSimulation
}

const STORAGE_KEY = 'gradcast_saved_budgets'

export function useSavedBudgets() {
  const savedBudgets = ref<SavedBudget[]>(loadFromStorage())

  function loadFromStorage(): SavedBudget[] {
    try {
      const raw = localStorage.getItem(STORAGE_KEY)
      return raw ? JSON.parse(raw) : []
    } catch {
      return []
    }
  }

  function persist() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(savedBudgets.value))
  }

  function saveBudget(
    name: string,
    schoolName: string,
    programTitle: string | null,
    locationName: string,
    simulation: BudgetSimulation
  ) {
    const entry: SavedBudget = {
      id: crypto.randomUUID(),
      name,
      savedAt: new Date().toISOString(),
      schoolName,
      programTitle,
      locationName,
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
