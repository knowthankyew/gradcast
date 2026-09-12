import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { SchoolDetail } from '../types'

export interface LocationSelection {
  cbsaCode: string
  name: string
  state: string
}

export interface ProgramSelection {
  cipCode: string
  title: string
  credentialLevel: number
  credentialName: string
}

export const useAppStore = defineStore('app', () => {
  // School state
  const selectedSchoolId = ref<number | null>(null)
  const selectedSchool = ref<SchoolDetail | null>(null)
  const selectedYear = ref<number | null>(null)

  // Program state (optional — refines salary estimate)
  const selectedProgram = ref<ProgramSelection | null>(null)
  const useSchoolAverage = ref(false)
  const hideMissingProgramData = ref(false)

  // Location state
  const selectedLocation = ref<LocationSelection | null>(null)
  const hideMissingLocationData = ref(false)
  // Intentionally '1bed' | '2bed' (no 'studio') because the UI only exposes two housing modes (1-bed and shared 2-bed), even though the backend supports 'studio'.
  const housingType = ref<'1bed' | '2bed'>('1bed')

  // Restoration mutex — prevents watchers from firing during scenario load-back
  const isRestoring = ref(false)

  // Computed — progressive disclosure gates
  const hasSchool = computed(() => selectedSchool.value !== null)
  const hasProgram = computed(() => selectedProgram.value !== null)
  const hasProgramSelection = computed(() => selectedProgram.value !== null || useSchoolAverage.value)
  const hasLocation = computed(() => selectedLocation.value !== null)
  const canSimulate = computed(() => hasSchool.value && hasLocation.value && hasProgramSelection.value && !isRestoring.value)

  // Actions
  function setSchool(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
    // Clear downstream selections when school changes
    selectedProgram.value = null
    selectedLocation.value = null
    // If school has no programs listed, default to school average
    useSchoolAverage.value = school.programs.length === 0
  }

  /** Restore school without clearing downstream state (used by load-back) */
  function restoreSchool(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
  }

  /** Update school detail (e.g. year change) without clearing destination.
   * Keeps selected program if it exists in the updated school's programs.
   */
  function updateSchoolDetail(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
    if (selectedProgram.value) {
      const stillExists = school.programs.some(
        (p) =>
          p.code === selectedProgram.value?.cipCode &&
          p.credentialName === selectedProgram.value?.credentialName
      )
      if (!stillExists) {
        selectedProgram.value = null
      }
    }
  }

  function setYear(year: number | null) {
    selectedYear.value = year
  }

  function setProgram(program: ProgramSelection | null) {
    selectedProgram.value = program
    if (program !== null) {
      useSchoolAverage.value = false
    }
  }

  function setUseSchoolAverage(useAverage: boolean) {
    useSchoolAverage.value = useAverage
    if (useAverage) {
      selectedProgram.value = null
    }
  }

  function setHideMissingProgramData(hide: boolean) {
    hideMissingProgramData.value = hide
  }

  function setLocation(location: LocationSelection) {
    selectedLocation.value = location
  }

  function setHideMissingLocationData(hide: boolean) {
    hideMissingLocationData.value = hide
  }

  function setHousingType(type: '1bed' | '2bed') {
    housingType.value = type
  }

  function clearSchool() {
    selectedSchoolId.value = null
    selectedSchool.value = null
    selectedProgram.value = null
    useSchoolAverage.value = false
    selectedLocation.value = null
  }

  function clearProgram() {
    selectedProgram.value = null
    useSchoolAverage.value = false
  }

  function clearLocation() {
    selectedLocation.value = null
  }

  return {
    selectedSchoolId,
    selectedSchool,
    selectedYear,
    selectedProgram,
    useSchoolAverage,
    hideMissingProgramData,
    selectedLocation,
    hideMissingLocationData,
    housingType,
    isRestoring,
    hasSchool,
    hasProgram,
    hasProgramSelection,
    hasLocation,
    canSimulate,
    setSchool,
    restoreSchool,
    updateSchoolDetail,
    setYear,
    setProgram,
    setUseSchoolAverage,
    setHideMissingProgramData,
    setLocation,
    setHideMissingLocationData,
    setHousingType,
    clearSchool,
    clearProgram,
    clearLocation,
  }
})
