<template>
  <v-card class="mb-6" elevation="2">
    <v-card-title class="text-h6">Find a School</v-card-title>
    <v-card-text>
      <v-row>
        <v-col cols="12" md="8">
          <v-autocomplete
            v-model="selectedSchool"
            v-model:search="searchQuery"
            :items="items"
            :loading="loading"
            :item-title="getSchoolDisplayName"
            item-value="id"
            return-object
            label="Search by school name"
            placeholder="Start typing a school name..."
            hint="Matches anywhere in name or city • Use &quot;quotes&quot; for exact match"
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            clearable
            no-filter
            :hide-no-data="!hasCompletedSearch || loading || effectiveSearchLength < 2"
            maxlength="200"
            autocorrect="off"
            autocapitalize="off"
            spellcheck="false"
            @update:model-value="onSchoolSelected"
          >
            <template #selection="{ item }">
              <span>{{ getSchoolDisplayName((item as any)?.raw || item) }}</span>
            </template>
            <template #item="{ props, item }">
              <v-list-item v-bind="props">
                <template #subtitle>
                  {{ (item as any).raw?.city }}, {{ (item as any).raw?.state }}
                </template>
              </v-list-item>
            </template>
            <template #no-data>
              <div class="pa-4 text-caption text-medium-emphasis text-center">
                No matching schools found for "{{ searchQuery.trim() }}".
              </div>
            </template>
          </v-autocomplete>
        </v-col>
        <v-col cols="12" md="4">
          <v-autocomplete
            v-model="selectedState"
            :items="stateOptions"
            item-title="label"
            item-value="code"
            :custom-filter="filterStateOption"
            label="Filter by state"
            variant="outlined"
            clearable
            prepend-inner-icon="mdi-map-marker"
            autocorrect="off"
            autocapitalize="off"
            spellcheck="false"
          />
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, watch, computed, onUnmounted } from 'vue'
import type { SchoolSearchResult } from '../types'
import { useSchoolApi } from '../composables/useSchoolApi'
import { useAppStore } from '../stores/appStore'
import { getEffectiveSearchLength, matchesSearchQuery } from '../utils/searchQuery'

const emit = defineEmits<{
  schoolSelected: [id: number]
}>()

const store = useAppStore()
const { searchSchools, searchLoading } = useSchoolApi()

interface SchoolSearchItem extends SchoolSearchResult {
  displayName: string
}

interface StateOption {
  code: string
  name: string
  label: string
}

const searchQuery = ref('')
const selectedSchool = ref<SchoolSearchItem | null>(null)
const selectedState = ref<string | null>(null)
const results = ref<SchoolSearchResult[]>([])
const hasCompletedSearch = ref(false)
const loading = computed(() => searchLoading.value)
const effectiveSearchLength = computed(() => getEffectiveSearchLength(searchQuery.value))

function getSchoolDisplayName(item: any): string {
  if (!item) return ''
  if (typeof item === 'string') return item
  if (item.displayName) return item.displayName
  if (item.name) {
    return item.city && item.state ? `${item.name} — ${item.city}, ${item.state}` : item.name
  }
  return ''
}

const items = computed(() => {
  const list = results.value.map((s) => ({
    ...s,
    displayName: getSchoolDisplayName(s),
  }))
  if (selectedSchool.value && !list.some((s) => s.id === selectedSchool.value?.id)) {
    return [
      {
        ...selectedSchool.value,
        displayName: getSchoolDisplayName(selectedSchool.value),
      },
      ...list,
    ]
  }
  return list
})

let debounceTimer: ReturnType<typeof setTimeout> | null = null
let currentRequestId = 0

watch(searchQuery, (val) => {
  if (debounceTimer) clearTimeout(debounceTimer)
  currentRequestId++

  if (selectedSchool.value && val === getSchoolDisplayName(selectedSchool.value)) {
    hasCompletedSearch.value = false
    return
  }

  if (!val || getEffectiveSearchLength(val) < 2) {
    results.value = []
    hasCompletedSearch.value = false
    return
  }

  hasCompletedSearch.value = false
  debounceTimer = setTimeout(async () => {
    const reqId = ++currentRequestId
    const data = await searchSchools(val, selectedState.value ?? undefined)
    if (reqId === currentRequestId) {
      results.value = data
      hasCompletedSearch.value = true
    }
  }, 300)
})

watch(selectedState, () => {
  if (
    searchQuery.value &&
    getEffectiveSearchLength(searchQuery.value) >= 2 &&
    (!selectedSchool.value || searchQuery.value !== getSchoolDisplayName(selectedSchool.value))
  ) {
    if (debounceTimer) clearTimeout(debounceTimer)
    hasCompletedSearch.value = false
    const reqId = ++currentRequestId
    searchSchools(searchQuery.value, selectedState.value ?? undefined).then((data) => {
      if (reqId === currentRequestId) {
        results.value = data
        hasCompletedSearch.value = true
      }
    })
  }
})

watch(
  () => store.selectedSchool,
  (school) => {
    if (!school) {
      selectedSchool.value = null
      searchQuery.value = ''
    } else if (selectedSchool.value?.id !== school.id) {
      const displayName = getSchoolDisplayName(school)
      selectedSchool.value = {
        id: school.id,
        name: school.name,
        city: school.city,
        state: school.state,
        displayName,
      }
      searchQuery.value = displayName
    }
  },
  { immediate: true }
)

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})

function onSchoolSelected(school: SchoolSearchItem | null) {
  if (school) {
    emit('schoolSelected', school.id)
  } else {
    store.clearSchool()
  }
}

function filterStateOption(_value: string, query: string, item?: { raw?: StateOption }): boolean {
  const opt = item?.raw
  if (!opt) {
    return matchesSearchQuery(query, [_value])
  }
  return matchesSearchQuery(query, [opt.code, opt.name, opt.label])
}

const rawStates: ReadonlyArray<readonly [string, string]> = [
  ['AL', 'Alabama'], ['AK', 'Alaska'], ['AZ', 'Arizona'], ['AR', 'Arkansas'],
  ['CA', 'California'], ['CO', 'Colorado'], ['CT', 'Connecticut'], ['DE', 'Delaware'],
  ['FL', 'Florida'], ['GA', 'Georgia'], ['HI', 'Hawaii'], ['ID', 'Idaho'],
  ['IL', 'Illinois'], ['IN', 'Indiana'], ['IA', 'Iowa'], ['KS', 'Kansas'],
  ['KY', 'Kentucky'], ['LA', 'Louisiana'], ['ME', 'Maine'], ['MD', 'Maryland'],
  ['MA', 'Massachusetts'], ['MI', 'Michigan'], ['MN', 'Minnesota'], ['MS', 'Mississippi'],
  ['MO', 'Missouri'], ['MT', 'Montana'], ['NE', 'Nebraska'], ['NV', 'Nevada'],
  ['NH', 'New Hampshire'], ['NJ', 'New Jersey'], ['NM', 'New Mexico'], ['NY', 'New York'],
  ['NC', 'North Carolina'], ['ND', 'North Dakota'], ['OH', 'Ohio'], ['OK', 'Oklahoma'],
  ['OR', 'Oregon'], ['PA', 'Pennsylvania'], ['RI', 'Rhode Island'], ['SC', 'South Carolina'],
  ['SD', 'South Dakota'], ['TN', 'Tennessee'], ['TX', 'Texas'], ['UT', 'Utah'],
  ['VT', 'Vermont'], ['VA', 'Virginia'], ['WA', 'Washington'], ['WV', 'West Virginia'],
  ['WI', 'Wisconsin'], ['WY', 'Wyoming'], ['DC', 'District of Columbia'], ['PR', 'Puerto Rico'],
  ['VI', 'U.S. Virgin Islands'], ['GU', 'Guam'], ['AS', 'American Samoa'], ['MP', 'Northern Mariana Islands'],
  ['FM', 'Federated States of Micronesia'], ['MH', 'Marshall Islands'], ['PW', 'Palau'],
]

const stateOptions: StateOption[] = rawStates.map(([code, name]) => ({
  code,
  name,
  label: `${code} — ${name}`,
}))
</script>
