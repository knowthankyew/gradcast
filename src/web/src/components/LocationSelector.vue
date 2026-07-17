<template>
  <v-card class="mb-6" elevation="2">
    <v-card-title class="text-h6">
      <v-icon class="mr-2">mdi-map-marker-radius</v-icon>
      Target Destination
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
            hide-no-data
            @update:model-value="onLocationSelected"
          >
            <template #item="{ props, item }">
              <v-list-item v-bind="props">
                <template #subtitle>
                  {{ (item as any).raw?.state ?? '' }} Metro Area
                </template>
              </v-list-item>
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
import { ref, watch, computed } from 'vue'
import { useAppStore, type LocationSelection } from '../stores/appStore'

interface LocationResult {
  cbsaCode: string
  name: string
  state: string
  type: string
}

const store = useAppStore()

const searchQuery = ref('')
const selectedItem = ref<LocationResult | null>(null)
const results = ref<LocationResult[]>([])
const loading = ref(false)
const housingChoice = ref<'1bed' | '2bed'>(store.housingType)

const items = computed(() => results.value)

let debounceTimer: ReturnType<typeof setTimeout> | null = null

watch(searchQuery, (val) => {
  if (debounceTimer) clearTimeout(debounceTimer)

  if (!val || val.length < 2) {
    results.value = []
    return
  }

  debounceTimer = setTimeout(async () => {
    loading.value = true
    try {
      const response = await fetch(`/api/locations/search?q=${encodeURIComponent(val)}`)
      if (response.ok) {
        results.value = await response.json()
      }
    } catch {
      results.value = []
    } finally {
      loading.value = false
    }
  }, 300)
})

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
</script>
