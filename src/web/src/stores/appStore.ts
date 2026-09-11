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

  // Location state
  const selectedLocation = ref<LocationSelection | null>(null)
  const housingType = ref<'1bed' | '2bed'>('1bed')

  // Restoration mutex — prevents watchers from firing during scenario load-back
  const isRestoring = ref(false)

  // Computed — progressive disclosure gates
  const hasSchool = computed(() => selectedSchool.value !== null)
  const hasProgram = computed(() => selectedProgram.value !== null)
  const hasLocation = computed(() => selectedLocation.value !== null)
  const canSimulate = computed(() => hasSchool.value && hasLocation.value && !isRestoring.value)

  // Actions
  function setSchool(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
    // Clear downstream selections when school changes
    selectedProgram.value = null
    selectedLocation.value = null
  }

  /** Restore school without clearing downstream state (used by load-back) */
  function restoreSchool(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
  }

  function setYear(year: number | null) {
    selectedYear.value = year
  }

  function setProgram(program: ProgramSelection | null) {
    selectedProgram.value = program
  }

  function setLocation(location: LocationSelection) {
    selectedLocation.value = location
  }

  function setHousingType(type: '1bed' | '2bed') {
    housingType.value = type
  }

  function clearSchool() {
    selectedSchoolId.value = null
    selectedSchool.value = null
    selectedProgram.value = null
    selectedLocation.value = null
  }

  function clearProgram() {
    selectedProgram.value = null
  }

  function clearLocation() {
    selectedLocation.value = null
  }

  return {
    selectedSchoolId,
    selectedSchool,
    selectedYear,
    selectedProgram,
    selectedLocation,
    housingType,
    isRestoring,
    hasSchool,
    hasProgram,
    hasLocation,
    canSimulate,
    setSchool,
    restoreSchool,
    setYear,
    setProgram,
    setLocation,
    setHousingType,
    clearSchool,
    clearProgram,
    clearLocation,
  }
})
