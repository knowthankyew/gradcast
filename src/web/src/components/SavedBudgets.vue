<template>
  <v-card v-if="savedBudgets.length > 0 || canSave" elevation="2" class="mb-6">
    <v-card-title class="d-flex align-center" role="heading" aria-level="2">
      <v-icon class="mr-2">mdi-content-save-all</v-icon>
      Saved Scenarios
      <v-chip v-if="savedBudgets.length > 0" class="ml-3" size="small" variant="tonal">
        {{ savedBudgets.length }}
      </v-chip>
    </v-card-title>

    <v-card-text>
      <!-- Save current -->
      <v-row v-if="canSave" align="center" class="mb-4">
        <v-col cols="12" sm="8">
          <v-text-field
            v-model="newName"
            label="Name this scenario"
            placeholder="e.g., Harvard CS → Boston, live alone"
            variant="outlined"
            density="compact"
            prepend-inner-icon="mdi-label"
            hide-details
            @keyup.enter="onSave"
          />
        </v-col>
        <v-col cols="12" sm="4">
          <v-btn
            color="accent"
            variant="elevated"
            :disabled="!newName.trim()"
            block
            @click="onSave"
          >
            <v-icon start>mdi-content-save</v-icon>
            Save Scenario
          </v-btn>
        </v-col>
      </v-row>

      <v-alert v-if="justSaved" type="success" variant="tonal" density="compact" class="mb-4" closable>
        Scenario saved!
      </v-alert>

      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4" closable @click:close="errorMessage = null">
        {{ errorMessage }}
      </v-alert>

      <!-- Saved list -->
      <div v-if="savedBudgets.length === 0" class="text-center text-medium-emphasis py-4">
        <v-icon size="32" class="mb-2">mdi-bookmark-outline</v-icon>
        <div>No saved scenarios yet. Run a simulation and save it to compare later.</div>
      </div>

      <v-list v-else density="compact" lines="three">
        <v-list-item
          v-for="budget in savedBudgets"
          :key="budget.id"
          class="px-0"
        >
          <template #prepend>
            <v-icon :color="statusColor(budget.simulation.incomeStatus)">
              {{ statusIcon(budget.simulation.incomeStatus) }}
            </v-icon>
          </template>

          <v-list-item-title class="font-weight-medium">
            {{ budget.name }}
          </v-list-item-title>
          <v-list-item-subtitle>
            {{ budget.context.schoolName }}
            <span v-if="budget.context.programTitle"> &middot; {{ budget.context.programTitle }}</span>
            <span v-else> &middot; School-Wide Average</span>
            &middot; {{ budget.context.locationName }}
            &middot; {{ budget.context.housingType === '2bed' ? 'Roommate' : 'Alone' }}
          </v-list-item-subtitle>
          <v-list-item-subtitle>
            <span class="font-weight-medium" :class="'text-' + statusColor(budget.simulation.incomeStatus)">
              {{ formatCurrency(budget.simulation.disposableMonthly) }}/mo disposable
            </span>
            &middot; {{ formatCurrency(budget.simulation.grossAnnualSalary) }}/yr gross
            &middot; Saved {{ formatDate(budget.savedAt) }}
          </v-list-item-subtitle>

          <template #append>
            <v-btn
              icon="mdi-reload"
              variant="text"
              size="small"
              color="primary"
              :aria-label="`Load scenario: ${budget.name}`"
              title="Load this scenario"
              @click="onLoad(budget)"
            />
            <v-btn
              icon="mdi-delete"
              variant="text"
              size="small"
              color="grey"
              :aria-label="`Delete scenario: ${budget.name}`"
              title="Delete"
              @click="onDelete(budget.id)"
            />
          </template>
        </v-list-item>
      </v-list>

      <v-btn
        v-if="savedBudgets.length > 1"
        variant="text"
        color="error"
        size="small"
        class="mt-2"
        @click="showClearConfirm = true"
      >
        <v-icon start size="small">mdi-delete-sweep</v-icon>
        Clear All
      </v-btn>

      <v-dialog v-model="showClearConfirm" max-width="400">
        <v-card>
          <v-card-title>Clear all saved scenarios?</v-card-title>
          <v-card-text>This will permanently delete all {{ savedBudgets.length }} saved scenarios.</v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="showClearConfirm = false">Cancel</v-btn>
            <v-btn color="error" variant="elevated" @click="onClearAll">Delete All</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBudgetSimulator } from '../composables/useBudgetSimulator'
import { useSavedBudgets, type SavedBudget } from '../composables/useSavedBudgets'
import { useAppStore } from '../stores/appStore'
import { useSchoolApi } from '../composables/useSchoolApi'

const { simulation } = useBudgetSimulator()
const store = useAppStore()
const { savedBudgets, saveBudget, deleteBudget, clearAll } = useSavedBudgets()
const { getSchoolDetail } = useSchoolApi()

const newName = ref('')
const justSaved = ref(false)
const showClearConfirm = ref(false)
const errorMessage = ref<string | null>(null)

const canSave = computed(() => simulation.value !== null)

function onSave() {
  if (!newName.value.trim() || !simulation.value) return

  saveBudget(
    {
      schoolId: store.selectedSchoolId!,
      schoolName: store.selectedSchool?.name ?? 'Unknown School',
      programCipCode: store.selectedProgram?.cipCode ?? null,
      programTitle: store.selectedProgram?.title ?? null,
      programCredentialLevel: store.selectedProgram?.credentialLevel ?? null,
      programCredentialName: store.selectedProgram?.credentialName ?? null,
      cbsaCode: store.selectedLocation!.cbsaCode,
      locationName: store.selectedLocation!.name,
      locationState: store.selectedLocation!.state,
      housingType: store.housingType,
    },
    newName.value.trim(),
    simulation.value
  )

  newName.value = ''
  justSaved.value = true
  setTimeout(() => { justSaved.value = false }, 3000)
}

async function onLoad(budget: SavedBudget) {
  const ctx = budget.context
  errorMessage.value = null

  // Activate mutex — prevents watchers from firing during state hydration
  store.isRestoring = true

  try {
    // Restore school (without clearing downstream state)
    const school = await getSchoolDetail(ctx.schoolId)
    if (!school) {
      store.isRestoring = false
      errorMessage.value = `School (ID: ${ctx.schoolId}) could not be loaded. It may no longer exist in the database.`
      return
    }
    store.restoreSchool(school)

    // Restore program (if one was selected)
    if (ctx.programCipCode && ctx.programTitle) {
      store.setProgram({
        cipCode: ctx.programCipCode,
        title: ctx.programTitle,
        credentialLevel: ctx.programCredentialLevel ?? 0,
        credentialName: ctx.programCredentialName ?? '',
      })
    } else {
      store.clearProgram()
      store.setUseSchoolAverage(true)
    }

    // Restore location + housing type
    store.setLocation({
      cbsaCode: ctx.cbsaCode,
      name: ctx.locationName,
      state: ctx.locationState,
    })
    store.setHousingType(ctx.housingType)
  } finally {
    // Release mutex — canSimulate becomes true, watcher fires once
    store.isRestoring = false
  }

  // Scroll to simulator
  setTimeout(() => {
    document.querySelector('.v-card[class*="elevation-3"]')?.scrollIntoView({
      behavior: 'smooth',
      block: 'start',
    })
  }, 500)
}

function onDelete(id: string) {
  deleteBudget(id)
}

function onClearAll() {
  clearAll()
  showClearConfirm.value = false
}

function statusColor(status: string): string {
  switch (status) {
    case 'comfortable': return 'green'
    case 'manageable': return 'light-green'
    case 'tight': return 'orange'
    case 'deficit': return 'red'
    default: return 'grey'
  }
}

function statusIcon(status: string): string {
  switch (status) {
    case 'comfortable': return 'mdi-check-circle'
    case 'manageable': return 'mdi-check'
    case 'tight': return 'mdi-alert'
    case 'deficit': return 'mdi-alert-circle'
    default: return 'mdi-help'
  }
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}
</script>
