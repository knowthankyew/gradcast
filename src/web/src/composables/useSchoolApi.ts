import { ref } from 'vue'
import type { SchoolSearchResult, SchoolDetail } from '../types'

export function useSchoolApi() {
  const searchResults = ref<SchoolSearchResult[]>([])
  const schoolDetail = ref<SchoolDetail | null>(null)
  const searchLoading = ref(false)
  const detailLoading = ref(false)
  const error = ref<string | null>(null)

  async function searchSchools(query: string, state?: string): Promise<SchoolSearchResult[]> {
    if (!query || query.length < 2) {
      searchResults.value = []
      return []
    }

    searchLoading.value = true
    error.value = null

    try {
      const params = new URLSearchParams({ q: query })
      if (state) {
        params.append('state', state)
      }

      const response = await fetch(`/api/schools/search?${params}`)

      if (!response.ok) {
        const problem = await response.json().catch(() => null)
        throw new Error(problem?.detail || `Search failed (${response.status})`)
      }

      const data: SchoolSearchResult[] = await response.json()
      searchResults.value = data
      return data
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'An unexpected error occurred'
      searchResults.value = []
      return []
    } finally {
      searchLoading.value = false
    }
  }

  async function getSchoolDetail(id: number): Promise<SchoolDetail | null> {
    detailLoading.value = true
    error.value = null

    try {
      const response = await fetch(`/api/schools/${id}`)

      if (!response.ok) {
        const problem = await response.json().catch(() => null)
        throw new Error(problem?.detail || `Failed to load school details (${response.status})`)
      }

      const data: SchoolDetail = await response.json()
      schoolDetail.value = data
      return data
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'An unexpected error occurred'
      schoolDetail.value = null
      return null
    } finally {
      detailLoading.value = false
    }
  }

  function clearDetail() {
    schoolDetail.value = null
    error.value = null
  }

  return {
    searchResults,
    schoolDetail,
    searchLoading,
    detailLoading,
    error,
    searchSchools,
    getSchoolDetail,
    clearDetail,
  }
}
