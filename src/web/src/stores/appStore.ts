import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { SchoolDetail } from '../types'

export interface LocationSelection {
  cbsaCode: string
  name: string
  state: string
}

export const useAppStore = defineStore('app', () => {
  // School state
  const selectedSchoolId = ref<number | null>(null)
  const selectedSchool = ref<SchoolDetail | null>(null)
  const selectedYear = ref<number | null>(null)

  // Location state
  const selectedLocation = ref<LocationSelection | null>(null)
  const housingType = ref<'1bed' | '2bed'>('1bed')

  // Computed
  const hasSchool = computed(() => selectedSchool.value !== null)
  const hasLocation = computed(() => selectedLocation.value !== null)
  const canSimulate = computed(() => hasSchool.value && hasLocation.value)

  // Actions
  function setSchool(school: SchoolDetail) {
    selectedSchoolId.value = school.id
    selectedSchool.value = school
  }

  function setYear(year: number | null) {
    selectedYear.value = year
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
  }

  function clearLocation() {
    selectedLocation.value = null
  }

  return {
    selectedSchoolId,
    selectedSchool,
    selectedYear,
    selectedLocation,
    housingType,
    hasSchool,
    hasLocation,
    canSimulate,
    setSchool,
    setYear,
    setLocation,
    setHousingType,
    clearSchool,
    clearLocation,
  }
})
