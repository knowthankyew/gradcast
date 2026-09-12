<template>
  <v-card class="mb-6" elevation="2">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      <v-icon class="mr-2">mdi-map-marker-radius</v-icon>
      Target Destination
      <v-spacer />
      <div class="d-flex align-center">
        <v-switch
          v-model="store.hideMissingLocationData"
          label="Hide missing data"
          color="primary"
          density="compact"
          hide-details
          inset
          role="switch"
          :input-props="{ role: 'switch' }"
        />
        <v-tooltip location="top" text="Hide metro areas without Fair Market Rent data">
          <template #activator="{ props: tooltipProps }">
            <v-icon v-bind="tooltipProps" size="small" color="medium-emphasis" class="ml-1">
              mdi-information-outline
            </v-icon>
          </template>
        </v-tooltip>
      </div>
    </v-card-title>
    <v-card-subtitle class="mb-2">
      Where do you plan to live and work after graduation?
    </v-card-subtitle>
    <v-card-text>
      <v-row>
        <v-col cols="12" md="7">
          <v-autocomplete
            v-model="selectedItem"
            v-model:search="searchQuery"
            :items="items"
            :loading="loading"
            item-title="name"
            item-value="cbsaCode"
            return-object
            label="Search metro areas"
            placeholder="Start typing a city or metro area..."
            prepend-inner-icon="mdi-city"
            variant="outlined"
            clearable
            no-filter
            @update:model-value="onLocationSelected"
          >
            <template #selection="{ item }">
              <span>{{ (item as any)?.name || (item as any)?.raw?.name }}</span>
            </template>
            <template #item="{ props, item }">
              <v-list-item v-bind="props">
                <template #subtitle>
                  <div class="d-flex align-center flex-wrap ga-2 mt-1">
                    <span>{{ getLoc(item)?.state ?? '' }} Metro Area</span>
                    <span class="text-medium-emphasis">•</span>
                    <span v-if="getLoc(item)?.hasHousingData" class="font-weight-medium text-primary">
                      {{ formatRentPreview(getLoc(item)) }}
                    </span>
                    <span v-else class="text-medium-emphasis">
                      Rent data unavailable
                    </span>
                  </div>
                </template>
              </v-list-item>
            </template>
            <template #no-data>
              <div class="pa-4 text-caption text-medium-emphasis text-center">
                <div v-if="store.hideMissingLocationData">
                  No metro areas with available rent data found.
                  <div class="mt-1">
                    <v-btn
                      size="x-small"
                      variant="text"
                      color="primary"
                      @click="store.setHideMissingLocationData(false)"
                    >
                      Turn off "Hide missing data"
                    </v-btn>
                  </div>
                </div>
                <div v-else>
                  No matching metro areas found.
                </div>
              </div>
            </template>
          </v-autocomplete>
        </v-col>
        <v-col cols="12" md="5">
          <v-btn-toggle
            v-model="housingChoice"
            mandatory
            color="primary"
            variant="outlined"
            divided
            class="mt-1"
            @update:model-value="onHousingChanged"
          >
            <v-btn value="1bed">
              <v-icon start>mdi-home</v-icon>
              Live Alone (1-Bed)
            </v-btn>
            <v-btn value="2bed">
              <v-icon start>mdi-account-group</v-icon>
              Roommate (2-Bed)
            </v-btn>
          </v-btn-toggle>
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, watch, computed, onUnmounted } from 'vue'
import { useAppStore, type LocationSelection } from '../stores/appStore'
import { formatCurrency } from '../utils/format'

interface LocationResult {
  cbsaCode: string
  name: string
  state: string
  type: string
  oneBedRent?: number | null
  twoBedRent?: number | null
  hasHousingData?: boolean
}

const store = useAppStore()

const searchQuery = ref('')
const selectedItem = ref<LocationResult | null>(null)
const results = ref<LocationResult[]>([])
const loading = ref(false)
const housingChoice = ref<'1bed' | '2bed'>(store.housingType)

const items = computed(() => {
  if (selectedItem.value && !results.value.some((r) => r.cbsaCode === selectedItem.value?.cbsaCode)) {
    return [selectedItem.value, ...results.value]
  }
  return results.value
})

let debounceTimer: ReturnType<typeof setTimeout> | null = null
let abortController: AbortController | null = null

function fetchLocations(val: string) {
  if (debounceTimer) clearTimeout(debounceTimer)
  if (abortController) abortController.abort()

  if (!val || val.length < 2) {
    results.value = []
    return
  }

  debounceTimer = setTimeout(async () => {
    loading.value = true
    abortController = new AbortController()
    try {
      const requireHousingParam = store.hideMissingLocationData ? '&requireHousing=true' : ''
      const response = await fetch(`/api/locations/search?q=${encodeURIComponent(val)}${requireHousingParam}`, {
        signal: abortController.signal,
      })
      if (response.ok) {
        results.value = await response.json()
      }
    } catch (e: any) {
      if (e.name !== 'AbortError') {
        results.value = []
      }
    } finally {
      loading.value = false
    }
  }, 300)
}

watch(searchQuery, (val) => {
  fetchLocations(val)
})

watch(
  () => store.hideMissingLocationData,
  () => {
    if (searchQuery.value && searchQuery.value.length >= 2) {
      fetchLocations(searchQuery.value)
    }
  }
)

watch(
  () => store.selectedLocation,
  (newLoc) => {
    if (!newLoc) {
      selectedItem.value = null
      searchQuery.value = ''
    } else if (selectedItem.value?.cbsaCode !== newLoc.cbsaCode) {
      selectedItem.value = {
        cbsaCode: newLoc.cbsaCode,
        name: newLoc.name,
        state: newLoc.state,
        type: '',
      }
      searchQuery.value = newLoc.name
    }
  },
  { immediate: true }
)

watch(
  () => store.housingType,
  (newType) => {
    if (newType && housingChoice.value !== newType) {
      housingChoice.value = newType
    }
  }
)

onUnmounted(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  if (abortController) abortController.abort()
})

function getLoc(item: any): LocationResult {
  return item?.raw ?? item
}

function onLocationSelected(item: LocationResult | null) {
  if (item) {
    const location: LocationSelection = {
      cbsaCode: item.cbsaCode,
      name: item.name,
      state: item.state,
    }
    store.setLocation(location)
  } else {
    store.clearLocation()
  }
}

function onHousingChanged(value: '1bed' | '2bed') {
  store.setHousingType(value)
}

function formatRentPreview(loc: LocationResult): string {
  if (housingChoice.value === '2bed' && loc.twoBedRent != null) {
    const shared = Math.round(loc.twoBedRent / 2)
    return `${formatCurrency(shared)}/mo (shared 2-bed)`
  }
  if (loc.oneBedRent != null) {
    return `${formatCurrency(loc.oneBedRent)}/mo (1-bed)`
  }
  return 'Rent data unavailable'
}
</script>
