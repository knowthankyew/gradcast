<template>
  <v-card v-if="props.programs.length > 0" elevation="2" class="mb-6">
    <v-card-title class="d-flex align-center flex-wrap ga-2">
      <v-icon class="mr-2">mdi-book-open-variant</v-icon>
      Programs by Department
      <v-chip class="ml-1" size="small" color="primary" variant="tonal">
        {{ categories.length }} {{ categories.length === 1 ? 'category' : 'categories' }}
      </v-chip>
      <v-spacer />
      <v-chip
        v-if="store.selectedProgram"
        color="accent"
        variant="elevated"
        size="small"
        closable
        @click:close="store.clearProgram()"
      >
        <v-icon start size="small">mdi-check-circle</v-icon>
        {{ store.selectedProgram.title }}
      </v-chip>
      <v-chip v-else size="small" variant="tonal" color="grey">
        <v-icon start size="small">mdi-cursor-default-click</v-icon>
        Click a program to use its earnings data
      </v-chip>
    </v-card-title>

    <v-card-text>
      <!-- Filter controls -->
      <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-4">
        <div class="text-caption text-medium-emphasis">
          <span v-if="store.hideMissingProgramData">
            Showing {{ totalFilteredPrograms }} of {{ props.programs.length }} programs (missing earnings hidden)
          </span>
          <span v-else>
            Showing all {{ props.programs.length }} programs
          </span>
        </div>

        <div class="d-flex align-center">
          <v-switch
            v-model="store.hideMissingProgramData"
            label="Hide missing data"
            color="primary"
            density="compact"
            hide-details
            inset
          />
          <v-tooltip location="top" text="Hide programs without reported median earnings">
            <template #activator="{ props: tooltipProps }">
              <v-icon v-bind="tooltipProps" size="small" color="medium-emphasis" class="ml-1">
                mdi-information-outline
              </v-icon>
            </template>
          </v-tooltip>
        </div>
      </div>

      <!-- Department panels -->
      <v-expansion-panels v-if="categories.length > 0" variant="accordion">
        <v-expansion-panel
          v-for="category in categories"
          :key="category.code"
        >
          <v-expansion-panel-title>
            <div class="d-flex align-center flex-grow-1">
              <span class="font-weight-medium">{{ category.name }}</span>
              <v-spacer />
              <v-chip size="x-small" class="mr-2" variant="tonal">
                {{ category.programs.length }} program{{ category.programs.length !== 1 ? 's' : '' }}
              </v-chip>
              <v-chip size="x-small" color="primary" variant="tonal">
                {{ category.totalCompletions.toLocaleString() }} completions
              </v-chip>
            </div>
          </v-expansion-panel-title>

          <v-expansion-panel-text>
            <v-table density="compact" hover>
              <thead>
                <tr>
                  <th>Program</th>
                  <th>Credential</th>
                  <th class="text-right">Completions</th>
                  <th class="text-right">Median Earnings (1 yr)</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="program in category.programs"
                  :key="`${program.code}-${program.credentialLevel}`"
                  :class="{ 'program-selected': isSelected(program) }"
                  class="program-row"
                  @click="selectProgram(program)"
                >
                  <td>
                    <v-icon
                      v-if="isSelected(program)"
                      size="x-small"
                      color="accent"
                      class="mr-1"
                    >mdi-check-circle</v-icon>
                    {{ program.title }}
                  </td>
                  <td>
                    <v-chip size="x-small" :color="credentialColor(program.credentialLevel)">
                      {{ program.credentialName }}
                    </v-chip>
                  </td>
                  <td class="text-right">
                    {{ program.completions != null ? program.completions.toLocaleString() : '—' }}
                  </td>
                  <td class="text-right">
                    <span v-if="program.medianEarnings != null" class="font-weight-medium">
                      {{ formatCurrency(program.medianEarnings) }}
                    </span>
                    <span v-else class="text-medium-emphasis">—</span>
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <!-- Empty state when all programs are filtered out -->
      <div v-else class="text-center py-8 text-medium-emphasis">
        <v-icon size="48" class="mb-2" color="grey">mdi-filter-off-outline</v-icon>
        <div class="text-subtitle-1 font-weight-medium mb-1">
          No programs with reported earnings data
        </div>
        <div class="text-body-2 mb-4">
          All {{ props.programs.length }} programs at this school have privacy-suppressed or unreported earnings.
        </div>
        <v-btn
          size="small"
          variant="tonal"
          color="primary"
          prepend-icon="mdi-filter-remove"
          @click="store.setHideMissingProgramData(false)"
        >
          Show All Programs
        </v-btn>
      </div>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ProgramData, ProgramCategory } from '../types'
import { cipCategories } from '../data/cipCategories'
import { useAppStore } from '../stores/appStore'

const props = defineProps<{
  programs: ProgramData[]
}>()

const store = useAppStore()

const filteredPrograms = computed(() => {
  if (!store.hideMissingProgramData) {
    return props.programs
  }
  return props.programs.filter((p) => p.medianEarnings != null)
})

const totalFilteredPrograms = computed(() => filteredPrograms.value.length)

const categories = computed<ProgramCategory[]>(() => {
  const grouped = new Map<string, ProgramData[]>()

  for (const program of filteredPrograms.value) {
    const prefix = program.code.substring(0, 2)
    if (!grouped.has(prefix)) {
      grouped.set(prefix, [])
    }
    grouped.get(prefix)!.push(program)
  }

  const result: ProgramCategory[] = []
  for (const [code, programs] of grouped) {
    if (programs.length === 0) continue
    const sorted = [...programs].sort(
      (a, b) => (b.completions ?? 0) - (a.completions ?? 0)
    )
    result.push({
      code,
      name: cipCategories[code] || `Category ${code}`,
      programs: sorted,
      totalCompletions: sorted.reduce((sum, p) => sum + (p.completions ?? 0), 0),
    })
  }

  return result.sort((a, b) => a.name.localeCompare(b.name))
})

function isSelected(program: ProgramData): boolean {
  return store.selectedProgram?.cipCode === program.code &&
         store.selectedProgram?.credentialName === program.credentialName
}

function selectProgram(program: ProgramData) {
  if (isSelected(program)) {
    store.clearProgram()
  } else {
    store.setProgram({
      cipCode: program.code,
      title: program.title,
      credentialLevel: program.credentialLevel,
      credentialName: program.credentialName,
    })
  }
}

function credentialColor(level: number): string {
  switch (level) {
    case 1: return 'grey'
    case 2: return 'teal'
    case 3: return 'blue'
    case 5: return 'purple'
    case 6: return 'deep-purple'
    case 7: return 'indigo'
    case 8: return 'brown'
    default: return 'grey'
  }
}

function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}
</script>

<style scoped>
.program-row {
  cursor: pointer;
  transition: background-color 0.15s;
}
.program-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.04);
}
.program-selected {
  background-color: rgba(var(--v-theme-accent), 0.08) !important;
}
</style>
