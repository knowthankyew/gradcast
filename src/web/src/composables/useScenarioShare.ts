import { ref, watch, onMounted, onUnmounted } from 'vue'
import { useAppStore } from '../stores/appStore'
import { useSchoolApi } from './useSchoolApi'
import type { SavedBudget } from './useSavedBudgets'

export interface ScenarioShareContext {
  schoolId: number
  year?: number | null
  cipCode?: string | null
  credentialLevel?: number | null
  useAverage?: boolean
  cbsaCode?: string | null
  housingType?: '1bed' | '2bed'
  salaryOverride?: number | null
}

export function useScenarioShare() {
  const store = useAppStore()
  const { getSchoolDetail } = useSchoolApi()

  const isHydrating = ref(false)
  const shareCopied = ref(false)
  let copyTimeout: ReturnType<typeof setTimeout> | null = null

  /**
   * Generates a full shareable scenario URL for either the current state
   * or a specific saved budget.
   */
  function generateScenarioUrl(context?: ScenarioShareContext | SavedBudget['context']): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : ''
    const params = new URLSearchParams()

    if (context) {
      // From saved budget context or custom context
      if ('schoolId' in context) {
        params.set('school', context.schoolId.toString())
      }
      if ('programCipCode' in context && context.programCipCode) {
        params.set('cip', context.programCipCode)
        if (context.programCredentialLevel != null) {
          params.set('cred', context.programCredentialLevel.toString())
        }
      } else if ('cipCode' in context && context.cipCode) {
        params.set('cip', context.cipCode)
        if (context.credentialLevel != null) {
          params.set('cred', context.credentialLevel.toString())
        }
      } else if ('useAverage' in context && context.useAverage) {
        params.set('cip', 'avg')
      } else if (!('programCipCode' in context) || !context.programCipCode) {
        params.set('cip', 'avg')
      }

      if ('cbsaCode' in context && context.cbsaCode) {
        params.set('cbsa', context.cbsaCode)
      }
      if ('housingType' in context && context.housingType) {
        params.set('housing', context.housingType)
      }
      if ('year' in context && context.year) {
        params.set('year', context.year.toString())
      }
      if ('salaryOverride' in context && context.salaryOverride != null) {
        params.set('salary', context.salaryOverride.toString())
      }
    } else {
      // From active store state
      if (!store.selectedSchoolId) return `${origin}/`

      params.set('school', store.selectedSchoolId.toString())

      if (store.selectedYear) {
        params.set('year', store.selectedYear.toString())
      }

      if (store.selectedProgram) {
        params.set('cip', store.selectedProgram.cipCode)
        if (store.selectedProgram.credentialLevel != null) {
          params.set('cred', store.selectedProgram.credentialLevel.toString())
        }
      } else if (store.useSchoolAverage) {
        params.set('cip', 'avg')
      }

      if (store.selectedLocation) {
        params.set('cbsa', store.selectedLocation.cbsaCode)
        params.set('housing', store.housingType)
      }

      if (store.salaryOverride != null) {
        params.set('salary', store.salaryOverride.toString())
      }
    }

    const queryString = params.toString()
    return queryString ? `${origin}/scenario?${queryString}` : `${origin}/`
  }

  /**
   * Copies the scenario link to clipboard with clipboard fallback
   */
  async function copyScenarioLink(context?: ScenarioShareContext | SavedBudget['context']): Promise<boolean> {
    const url = generateScenarioUrl(context)

    try {
      if (navigator?.clipboard?.writeText) {
        await navigator.clipboard.writeText(url)
      } else {
        // Fallback for older browsers / iframe contexts
        const textArea = document.createElement('textarea')
        textArea.value = url
        textArea.style.position = 'fixed'
        textArea.style.opacity = '0'
        document.body.appendChild(textArea)
        textArea.focus()
        textArea.select()
        document.execCommand('copy')
        document.body.removeChild(textArea)
      }

      shareCopied.value = true
      if (copyTimeout) clearTimeout(copyTimeout)
      copyTimeout = setTimeout(() => {
        shareCopied.value = false
      }, 3000)
      return true
    } catch {
      return false
    }
  }

  /**
   * Reads URL query parameters and restores application state
   */
  async function hydrateFromUrl(): Promise<boolean> {
    if (typeof window === 'undefined') return false

    const search = window.location.search
    if (!search) return false

    const params = new URLSearchParams(search)
    const schoolParam = params.get('school')
    if (!schoolParam) return false

    const schoolId = parseInt(schoolParam, 10)
    if (isNaN(schoolId)) return false

    const yearParam = params.get('year')
    const year = yearParam ? parseInt(yearParam, 10) : null

    const cipParam = params.get('cip')
    const credParam = params.get('cred')
    const credLevel = credParam ? parseInt(credParam, 10) : null

    const cbsaParam = params.get('cbsa')
    const housingParam = params.get('housing')
    const salaryParam = params.get('salary')
    const salaryOverride = salaryParam ? parseFloat(salaryParam) : null

    isHydrating.value = true
    store.isRestoring = true

    try {
      // 1. Fetch school details
      const school = await getSchoolDetail(schoolId, year)
      if (!school) {
        return false
      }
      store.restoreSchool(school)
      if (year) {
        store.setYear(year)
      }

      // 2. Restore program or average
      if (cipParam === 'avg') {
        store.clearProgram()
        store.setUseSchoolAverage(true)
      } else if (cipParam) {
        const matchingProg = school.programs.find(p => {
          if (p.code !== cipParam) return false
          if (credLevel != null) return p.credentialLevel === credLevel
          return true
        })

        if (matchingProg) {
          store.setProgram({
            cipCode: matchingProg.code,
            title: matchingProg.title,
            credentialLevel: matchingProg.credentialLevel,
            credentialName: matchingProg.credentialName,
          })
        } else {
          // Fall back to school average if specified program not found
          store.clearProgram()
          store.setUseSchoolAverage(true)
        }
      } else {
        store.clearProgram()
        store.setUseSchoolAverage(school.programs.length === 0)
      }

      // 3. Restore location if CBSA provided
      if (cbsaParam) {
        try {
          const locRes = await fetch(`/api/locations/${encodeURIComponent(cbsaParam)}`)
          if (locRes.ok) {
            const locData = await locRes.json()
            store.setLocation({
              cbsaCode: locData.cbsaCode,
              name: locData.name,
              state: locData.state,
            })
          }
        } catch {
          // Ignore location fetch error on hydration
        }
      }

      // 4. Restore housing choice
      if (housingParam === '1bed' || housingParam === '2bed') {
        store.setHousingType(housingParam)
      }

      // 5. Restore custom salary override
      if (salaryOverride != null && !isNaN(salaryOverride)) {
        store.setSalaryOverride(salaryOverride)
      }

      // 6. Scroll into view if simulator is ready
      if (store.canSimulate) {
        setTimeout(() => {
          document.querySelector('.v-card[class*="elevation-3"]')?.scrollIntoView({
            behavior: 'smooth',
            block: 'start',
          })
        }, 500)
      }

      return true
    } catch {
      return false
    } finally {
      store.isRestoring = false
      isHydrating.value = false
    }
  }

  /**
   * Initializes two-way dynamic URL sync:
   * 1. Hydrates state from URL parameters on first load.
   * 2. Watches store changes and updates window.history without page reloads.
   * 3. Listens to popstate for browser back/forward button clicks.
   */
  function initDynamicUrlSync() {
    let unwatch: (() => void) | null = null

    const handlePopState = () => {
      hydrateFromUrl()
    }

    onMounted(async () => {
      // Step 1: Initial hydration from URL
      await hydrateFromUrl()

      // Step 2: Listen for browser back/forward
      window.addEventListener('popstate', handlePopState)

      // Step 3: Watch for reactive state changes and update URL
      unwatch = watch(
        [
          () => store.selectedSchoolId,
          () => store.selectedYear,
          () => store.selectedProgram,
          () => store.useSchoolAverage,
          () => store.selectedLocation,
          () => store.housingType,
          () => store.salaryOverride,
          () => store.isRestoring,
        ],
        () => {
          if (store.isRestoring || isHydrating.value) return

          const url = generateScenarioUrl()
          const currentUrl = window.location.pathname + window.location.search

          const targetUrlObj = new URL(url)
          const targetUrl = targetUrlObj.pathname + targetUrlObj.search

          if (currentUrl !== targetUrl) {
            window.history.replaceState({}, '', targetUrl)
          }
        }
      )
    })

    onUnmounted(() => {
      if (unwatch) unwatch()
      window.removeEventListener('popstate', handlePopState)
      if (copyTimeout) clearTimeout(copyTimeout)
    })
  }

  return {
    isHydrating,
    shareCopied,
    generateScenarioUrl,
    copyScenarioLink,
    hydrateFromUrl,
    initDynamicUrlSync,
  }
}
