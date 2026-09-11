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
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            clearable
            no-filter
            hide-no-data
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
          </v-autocomplete>
        </v-col>
        <v-col cols="12" md="4">
          <v-select
            v-model="selectedState"
            :items="states"
            label="Filter by state"
            variant="outlined"
            clearable
            prepend-inner-icon="mdi-map-marker"
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

const emit = defineEmits<{
  schoolSelected: [id: number]
}>()

const store = useAppStore()
const { searchSchools, searchLoading } = useSchoolApi()

const searchQuery = ref('')
const selectedSchool = ref<any>(null)
const selectedState = ref<string | null>(null)
const results = ref<SchoolSearchResult[]>([])
const loading = computed(() => searchLoading.value)

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

  if (!val || val.length < 2) {
    results.value = []
    return
  }

  debounceTimer = setTimeout(async () => {
    const reqId = ++currentRequestId
    const data = await searchSchools(val, selectedState.value ?? undefined)
    if (reqId === currentRequestId) {
      results.value = data
    }
  }, 300)
})

watch(selectedState, () => {
  if (searchQuery.value && searchQuery.value.length >= 2) {
    const reqId = ++currentRequestId
    searchSchools(searchQuery.value, selectedState.value ?? undefined).then((data) => {
      if (reqId === currentRequestId) {
        results.value = data
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

function onSchoolSelected(school: any) {
  if (school) {
    emit('schoolSelected', school.id)
  } else {
    store.clearSchool()
  }
}

const states = [
  'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
  'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
  'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
  'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
  'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
  'DC', 'PR', 'VI', 'GU', 'AS', 'MP',
]
</script>
