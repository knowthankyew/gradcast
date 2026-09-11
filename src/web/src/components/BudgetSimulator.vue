<template>
  <v-card elevation="3" class="mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon class="mr-2" color="accent">mdi-calculator-variant</v-icon>
      Post-Grad Monthly Budget Simulator
    </v-card-title>
    <v-card-subtitle>
      Based on your school, program earnings, and target destination
    </v-card-subtitle>

    <v-card-text>
      <!-- Loading -->
      <div v-if="loading" class="d-flex justify-center py-8">
        <v-progress-circular indeterminate color="primary" size="48" />
      </div>

      <!-- Error -->
      <v-alert v-else-if="error" type="error" variant="tonal" density="compact" class="mb-4">
        {{ error }}
      </v-alert>

      <!-- Results -->
      <template v-else-if="sim">
        <!-- Income Header -->
        <v-row class="mb-4">
          <v-col cols="12" sm="6">
            <div class="text-caption text-medium-emphasis">Gross Monthly</div>
            <div class="text-h4 font-weight-light">
              {{ formatCurrency(sim.grossMonthly) }}
            </div>
            <v-chip size="x-small" variant="tonal" class="mt-1">
              {{ formatCurrency(sim.grossAnnualSalary) }}/yr
              <v-tooltip activator="parent" location="bottom">
                Source: {{ salarySourceLabel }}
              </v-tooltip>
            </v-chip>
          </v-col>
          <v-col cols="12" sm="6">
            <div class="text-caption text-medium-emphasis">Net Take-Home</div>
            <div class="text-h4 font-weight-medium text-primary">
              {{ formatCurrency(sim.netMonthly) }}
            </div>
            <v-chip size="x-small" variant="tonal" color="secondary" class="mt-1">
              {{ (sim.effectiveTaxRate * 100).toFixed(1) }}% effective tax rate
            </v-chip>
          </v-col>
        </v-row>

        <v-divider class="mb-4" />

        <!-- Expense Breakdown -->
        <div class="text-subtitle-2 mb-3">Monthly Fixed Costs</div>
        <v-row density="comfortable">
          <v-col cols="12">
            <div class="d-flex align-center justify-space-between py-2">
              <div class="d-flex align-center">
                <v-icon size="small" class="mr-2" color="orange">mdi-home-city</v-icon>
                <span>Rent ({{ sim.housingType === '2bed' ? 'shared 2-bed' : '1-bedroom' }})</span>
              </div>
              <span class="font-weight-medium">{{ formatCurrency(sim.rentMonthly) }}</span>
            </div>
          </v-col>
          <v-col cols="12">
            <div class="d-flex align-center justify-space-between py-2">
              <div class="d-flex align-center">
                <v-icon size="small" class="mr-2" color="deep-purple">mdi-school</v-icon>
                <span>Student Loan Payment</span>
                <v-tooltip location="bottom" max-width="280">
                  <template #activator="{ props: tp }">
                    <v-icon v-bind="tp" size="x-small" class="ml-1" color="grey">mdi-information-outline</v-icon>
                  </template>
                  <span>
                    Estimated from {{ formatCurrency(sim.loanPrincipal) }} total debt
                    (based on school tuition) at 5.5% over 10 years.
                  </span>
                </v-tooltip>
              </div>
              <span class="font-weight-medium">{{ formatCurrency(sim.loanPaymentMonthly) }}</span>
            </div>
          </v-col>
          <v-col cols="12">
            <v-divider class="my-1" />
            <div class="d-flex align-center justify-space-between py-2">
              <span class="font-weight-medium">Total Fixed Costs</span>
              <span class="font-weight-bold">{{ formatCurrency(sim.fixedCostsMonthly) }}</span>
            </div>
          </v-col>
        </v-row>

        <v-divider class="my-4" />

        <!-- Disposable Income Result -->
        <v-row>
          <v-col cols="12" sm="7">
            <div class="text-subtitle-2 mb-1">Monthly Disposable Income</div>
            <div
              class="text-h4 font-weight-bold"
              :class="statusColor"
            >
              {{ formatCurrency(sim.disposableMonthly) }}
            </div>
            <v-chip
              size="small"
              :color="statusChipColor"
              variant="tonal"
              class="mt-2"
            >
              <v-icon start size="small">{{ statusIcon }}</v-icon>
              {{ statusLabel }}
            </v-chip>
          </v-col>
          <v-col cols="12" sm="5">
            <!-- Simple visual breakdown -->
            <div class="text-caption text-medium-emphasis mb-2">Budget Split</div>
            <div class="d-flex flex-column ga-1">
              <div class="d-flex align-center">
                <div
                  class="budget-bar bg-orange-lighten-1"
                  :style="{ width: rentPct + '%' }"
                  role="progressbar"
                  :aria-valuenow="rentPct"
                  aria-valuemin="0"
                  aria-valuemax="100"
                  :aria-label="`Rent takes ${rentPct}% of net pay`"
                />
                <span class="text-caption ml-2">Rent {{ rentPct }}%</span>
              </div>
              <div class="d-flex align-center">
                <div
                  class="budget-bar bg-deep-purple-lighten-2"
                  :style="{ width: loanPct + '%' }"
                  role="progressbar"
                  :aria-valuenow="loanPct"
                  aria-valuemin="0"
                  aria-valuemax="100"
                  :aria-label="`Student loans take ${loanPct}% of net pay`"
                />
                <span class="text-caption ml-2">Loans {{ loanPct }}%</span>
              </div>
              <div class="d-flex align-center">
                <div
                  class="budget-bar"
                  :class="disposablePct > 0 ? 'bg-green-lighten-1' : 'bg-red-lighten-1'"
                  :style="{ width: Math.abs(disposablePct) + '%' }"
                  role="progressbar"
                  :aria-valuenow="disposablePct"
                  aria-valuemin="0"
                  aria-valuemax="100"
                  :aria-label="`Disposable income is ${disposablePct}% of net pay`"
                />
                <span class="text-caption ml-2">Disposable {{ disposablePct }}%</span>
              </div>
            </div>
          </v-col>
        </v-row>

        <!-- Salary override -->
        <v-divider class="my-4" />
        <v-row align="center">
          <v-col cols="12" sm="7">
            <v-text-field
              v-model.number="salaryOverrideInput"
              label="What-if salary override"
              prefix="$"
              type="number"
              variant="outlined"
              density="compact"
              hint="Enter a custom salary to explore different scenarios"
              persistent-hint
              clearable
            />
          </v-col>
          <v-col cols="12" sm="5" class="d-flex align-center ga-2">
            <v-btn
              variant="tonal"
              color="primary"
              :disabled="!salaryOverrideInput"
              @click="runWithOverride"
            >
              <v-icon start>mdi-refresh</v-icon>
              Recalculate
            </v-btn>
            <v-btn
              v-if="sim.salarySource === 'user_override'"
              variant="text"
              color="secondary"
              @click="resetOverride"
            >
              <v-icon start>mdi-undo</v-icon>
              Reset
            </v-btn>
          </v-col>
        </v-row>
      </template>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBudgetSimulator } from '../composables/useBudgetSimulator'

const { simulation: sim, loading, error, runSimulation } = useBudgetSimulator()

const salaryOverrideInput = ref<number | null>(null)

const salarySourceLabel = computed(() => {
  if (!sim.value) return ''
  switch (sim.value.salarySource) {
    case 'user_override': return 'Your custom salary'
    case 'scorecard_median': return 'College Scorecard median earnings'
    case 'national_fallback': return 'National median (no program data available)'
    default: return sim.value.salarySource
  }
})

const statusColor = computed(() => {
  if (!sim.value) return ''
  switch (sim.value.incomeStatus) {
    case 'comfortable': return 'text-green-darken-2'
    case 'manageable': return 'text-green-darken-1'
    case 'tight': return 'text-orange-darken-2'
    case 'deficit': return 'text-red-darken-2'
    default: return ''
  }
})

const statusChipColor = computed(() => {
  if (!sim.value) return 'grey'
  switch (sim.value.incomeStatus) {
    case 'comfortable': return 'green'
    case 'manageable': return 'light-green'
    case 'tight': return 'orange'
    case 'deficit': return 'red'
    default: return 'grey'
  }
})

const statusIcon = computed(() => {
  if (!sim.value) return 'mdi-help'
  switch (sim.value.incomeStatus) {
    case 'comfortable': return 'mdi-check-circle'
    case 'manageable': return 'mdi-check'
    case 'tight': return 'mdi-alert'
    case 'deficit': return 'mdi-alert-circle'
    default: return 'mdi-help'
  }
})

const statusLabel = computed(() => {
  if (!sim.value) return ''
  switch (sim.value.incomeStatus) {
    case 'comfortable': return 'Comfortable — solid financial cushion'
    case 'manageable': return 'Manageable — budget works but limited flexibility'
    case 'tight': return 'Tight — very little room for extras'
    case 'deficit': return 'Deficit — expenses exceed income'
    default: return ''
  }
})

const rentPct = computed(() => {
  if (!sim.value || sim.value.netMonthly <= 0) return 0
  return Math.round((sim.value.rentMonthly / sim.value.netMonthly) * 100)
})

const loanPct = computed(() => {
  if (!sim.value || sim.value.netMonthly <= 0) return 0
  return Math.round((sim.value.loanPaymentMonthly / sim.value.netMonthly) * 100)
})

const disposablePct = computed(() => {
  if (!sim.value || sim.value.netMonthly <= 0) return 0
  return Math.round((sim.value.disposableMonthly / sim.value.netMonthly) * 100)
})

function runWithOverride() {
  if (salaryOverrideInput.value && salaryOverrideInput.value > 0) {
    runSimulation(salaryOverrideInput.value)
  }
}

function resetOverride() {
  salaryOverrideInput.value = null
  runSimulation()
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
.budget-bar {
  height: 12px;
  border-radius: 6px;
  min-width: 4px;
  max-width: 60%;
  transition: width 0.3s ease;
}
</style>
