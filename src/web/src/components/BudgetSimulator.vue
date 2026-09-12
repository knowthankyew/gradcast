<template>
  <v-card elevation="3" class="mb-6">
    <v-card-title class="d-flex align-center">
      <v-icon class="mr-2" color="accent">mdi-calculator-variant</v-icon>
      Post-Grad Monthly Budget Simulator
    </v-card-title>
    <v-card-subtitle class="d-flex align-center flex-wrap ga-2">
      <span v-if="store.selectedProgram">
        <v-icon size="x-small" color="accent" class="mr-1">mdi-school</v-icon>
        <strong>{{ store.selectedProgram.title }}</strong> ({{ store.selectedProgram.credentialName }}) &middot; {{ sim?.locationName ?? store.selectedLocation?.name }}
      </span>
      <span v-else-if="store.useSchoolAverage">
        <v-icon size="x-small" color="amber-darken-2" class="mr-1">mdi-chart-bell-curve-cumulative</v-icon>
        <strong>School-Wide Average</strong> (All Majors) &middot; {{ sim?.locationName ?? store.selectedLocation?.name }}
      </span>
      <span v-else>
        Based on your school, program earnings, and target destination
      </span>
      <v-chip
        v-if="isMissingEarnings"
        size="x-small"
        color="warning"
        variant="tonal"
        class="ml-1"
      >
        No reported earnings
      </v-chip>
    </v-card-subtitle>

    <v-card-text>
      <!-- Missing earnings data alert banner -->
      <v-alert
        v-if="isMissingEarnings"
        type="warning"
        variant="tonal"
        density="comfortable"
        class="mb-4"
      >
        <div class="d-flex align-center justify-space-between flex-wrap ga-2">
          <div>
            <div class="font-weight-medium d-flex align-center ga-1">
              <v-icon size="small" color="warning">mdi-alert-circle-outline</v-icon>
              No Median Earnings Reported for {{ store.selectedProgram ? store.selectedProgram.title : 'this School' }}
            </div>
            <div class="text-caption mt-1">
              The U.S. Department of Education suppresses earnings data for programs or schools with small graduating cohorts to protect student privacy.
              Known fixed costs (rent and student loans) are calculated below. Enter your expected starting salary or apply the $45k national baseline.
            </div>
          </div>
          <div class="d-flex align-center ga-2 flex-wrap">
            <v-btn
              size="small"
              variant="elevated"
              color="warning"
              prepend-icon="mdi-currency-usd"
              @click="applyNationalBaseline"
            >
              Use $45,000 Baseline
            </v-btn>
            <v-btn
              size="small"
              variant="tonal"
              color="warning"
              prepend-icon="mdi-arrow-down"
              @click="focusSalaryOverride"
            >
              Enter Custom Salary
            </v-btn>
            <v-btn
              v-if="!store.selectedProgram"
              size="small"
              variant="outlined"
              color="warning"
              prepend-icon="mdi-school"
              @click="scrollToProgramList"
            >
              Select a Specific Major
            </v-btn>
          </div>
        </div>
      </v-alert>

      <!-- School-wide average alert banner (only when school has reported data) -->
      <v-alert
        v-else-if="sim?.hasReportedEarnings && store.useSchoolAverage && !store.selectedProgram"
        type="warning"
        variant="tonal"
        density="comfortable"
        class="mb-4"
      >
        <div class="d-flex align-center justify-space-between flex-wrap ga-2">
          <div>
            <div class="font-weight-medium">Viewing School-Wide Average Earnings</div>
            <div class="text-caption">
              Salary is based on the average across all graduates at this school. To see your personalized budget, select your specific major.
            </div>
          </div>
          <v-btn
            size="small"
            variant="elevated"
            color="warning"
            prepend-icon="mdi-school"
            @click="scrollToProgramList"
          >
            Select a Specific Major
          </v-btn>
        </div>
      </v-alert>

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
            <div v-if="isMissingEarnings" class="text-h4 font-weight-light text-medium-emphasis">
              —
            </div>
            <div v-else class="text-h4 font-weight-light">
              {{ formatCurrency(sim.grossMonthly) }}
            </div>
            <v-chip
              size="x-small"
              :variant="isMissingEarnings ? 'elevated' : 'tonal'"
              :color="isMissingEarnings ? 'warning' : undefined"
              class="mt-1"
            >
              <template v-if="isMissingEarnings">
                No Earnings Data Reported
                <v-tooltip activator="parent" location="bottom" max-width="320">
                  The national median starting salary across all U.S. bachelor's graduates is $45,000/yr ($3,750/mo). Enter your custom salary below or click "Use $45,000 Baseline" to see your take-home pay.
                </v-tooltip>
              </template>
              <template v-else>
                {{ formatCurrency(sim.grossAnnualSalary) }}/yr
                <v-tooltip activator="parent" location="bottom">
                  Source: {{ salarySourceLabel }}
                </v-tooltip>
              </template>
            </v-chip>
          </v-col>
          <v-col cols="12" sm="6">
            <div class="text-caption text-medium-emphasis">Net Take-Home</div>
            <div v-if="isMissingEarnings" class="text-h4 font-weight-medium text-medium-emphasis">
              —
            </div>
            <div v-else class="text-h4 font-weight-medium text-primary">
              {{ formatCurrency(sim.netMonthly) }}
            </div>
            <v-chip size="x-small" variant="tonal" :color="isMissingEarnings ? 'grey' : 'secondary'" class="mt-1">
              {{ isMissingEarnings ? 'Awaiting salary input' : `${(sim.effectiveTaxRate * 100).toFixed(1)}% effective tax rate` }}
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
            <template v-if="isMissingEarnings">
              <div class="text-h4 font-weight-bold text-medium-emphasis">
                Pending Salary Input
              </div>
              <div class="text-caption text-medium-emphasis mt-1">
                Total monthly fixed costs: <strong>{{ formatCurrency(sim.fixedCostsMonthly) }}</strong>
              </div>
              <v-chip
                size="small"
                color="warning"
                variant="tonal"
                class="mt-2 cursor-pointer"
                @click="focusSalaryOverride"
              >
                <v-icon start size="small">mdi-arrow-down-circle</v-icon>
                Enter expected salary below to calculate
              </v-chip>
            </template>
            <template v-else>
              <div
                class="text-h4 font-weight-bold"
                :class="computedStatusTextColor"
              >
                {{ formatCurrency(sim.disposableMonthly) }}
              </div>
              <v-chip
                size="small"
                :color="computedStatusChipColor"
                variant="tonal"
                class="mt-2"
              >
                <v-icon start size="small">{{ computedStatusIcon }}</v-icon>
                {{ computedStatusLabel }}
              </v-chip>
            </template>
          </v-col>
          <v-col cols="12" sm="5">
            <!-- Simple visual breakdown -->
            <div class="text-caption text-medium-emphasis mb-2">Budget Split</div>
            <div v-if="isMissingEarnings" class="text-caption text-medium-emphasis pa-3 bg-surface-variant rounded">
              <v-icon size="small" class="mr-1" color="grey">mdi-chart-pie</v-icon>
              Budget split percentages will calculate once an expected salary is entered.
            </div>
            <div v-else class="d-flex flex-column ga-1">
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
        <div
          id="salary-override-section"
          class="salary-section-wrapper pa-4 rounded-lg"
          :class="{ 'missing-earnings-highlight-box': isMissingEarnings }"
        >
          <div v-if="isMissingEarnings" class="d-flex align-center flex-wrap ga-2 mb-3">
            <v-icon color="warning" size="small">mdi-lightbulb-on-outline</v-icon>
            <span class="text-subtitle-2 font-weight-bold">
              Enter Expected Starting Salary
            </span>
            <v-chip size="x-small" color="warning" variant="elevated" class="ml-1">
              Required for Take-Home Pay
            </v-chip>
          </div>
          <v-row align="center">
            <v-col cols="12" sm="6">
              <v-text-field
                id="salary-override-input"
                v-model.number="salaryOverrideInput"
                label="What-if salary override"
                prefix="$"
                type="number"
                variant="outlined"
                density="compact"
                :hint="isMissingEarnings ? 'Enter your expected salary to calculate take-home pay and budget split' : 'Enter a custom salary to explore different scenarios'"
                persistent-hint
                clearable
                :color="isMissingEarnings ? 'warning' : 'primary'"
                @keydown.enter="runWithOverride"
              />
            </v-col>
            <v-col cols="12" sm="6" class="d-flex align-center flex-wrap ga-2">
              <v-btn
                variant="elevated"
                :color="isMissingEarnings ? 'warning' : 'primary'"
                :disabled="!salaryOverrideInput"
                @click="runWithOverride"
              >
                <v-icon start>mdi-calculator</v-icon>
                Recalculate
              </v-btn>
              <v-btn
                v-if="isMissingEarnings"
                variant="tonal"
                color="secondary"
                prepend-icon="mdi-currency-usd"
                @click="applyNationalBaseline"
              >
                Use $45,000 Baseline
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
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useBudgetSimulator } from '../composables/useBudgetSimulator'
import { useAppStore } from '../stores/appStore'
import { formatCurrency } from '../utils/format'
import { statusColor, statusIcon, statusLabel, statusTextColor } from '../utils/budgetStatus'

const store = useAppStore()
const { simulation: sim, loading, error, runSimulation } = useBudgetSimulator()

const salaryOverrideInput = ref<number | null>(null)

const isMissingEarnings = computed(() => {
  return sim.value !== null && !sim.value.hasReportedEarnings && sim.value.salarySource !== 'user_override'
})

function applyNationalBaseline() {
  salaryOverrideInput.value = 45000
  runSimulation(45000)
}

function scrollToProgramList() {
  const el = document.getElementById('program-list-card')
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    el.classList.add('program-card-highlight')
    setTimeout(() => {
      el.classList.remove('program-card-highlight')
    }, 1800)
  }
}

function focusSalaryOverride() {
  const section = document.getElementById('salary-override-section')
  if (section) {
    section.scrollIntoView({ behavior: 'smooth', block: 'center' })
    section.classList.add('salary-override-highlight')
    setTimeout(() => {
      section.classList.remove('salary-override-highlight')
    }, 1800)
  }
  const input = document.querySelector('#salary-override-input input') as HTMLInputElement | null
  if (input) {
    setTimeout(() => {
      input.focus()
      input.select()
    }, 350)
  }
}

const salarySourceLabel = computed(() => {
  if (!sim.value) return ''
  switch (sim.value.salarySource) {
    case 'user_override': return 'Your custom salary'
    case 'program_median':
      return `${store.selectedProgram?.title ?? 'Program'} median earnings (Scorecard)`
    case 'school_median':
      return 'School-wide average earnings (Scorecard)'
    case 'scorecard_median':
      return store.selectedProgram
        ? `${store.selectedProgram.title} median earnings (Scorecard)`
        : 'School-wide average earnings (Scorecard)'
    case 'national_fallback': return 'National median baseline ($45,000/yr — no institutional data reported)'
    default: return sim.value.salarySource
  }
})

const computedStatusTextColor = computed(() => sim.value ? statusTextColor(sim.value.incomeStatus) : '')
const computedStatusChipColor = computed(() => sim.value ? statusColor(sim.value.incomeStatus) : 'grey')
const computedStatusIcon = computed(() => sim.value ? statusIcon(sim.value.incomeStatus) : 'mdi-help')
const computedStatusLabel = computed(() => sim.value ? statusLabel(sim.value.incomeStatus) : '')

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
</script>

<style scoped>
.budget-bar {
  height: 12px;
  border-radius: 6px;
  min-width: 4px;
  max-width: 60%;
  transition: width 0.3s ease;
}

.cursor-pointer {
  cursor: pointer;
}

.salary-section-wrapper {
  transition: all 0.3s ease;
}

.missing-earnings-highlight-box {
  background: rgba(var(--v-theme-warning), 0.07);
  border: 2px solid rgba(var(--v-theme-warning), 0.5) !important;
  border-radius: 12px !important;
}

.salary-override-highlight {
  animation: pulse-border 1.8s ease-in-out;
  border-color: rgb(var(--v-theme-warning)) !important;
}

@keyframes pulse-border {
  0% {
    box-shadow: 0 0 0 0 rgba(var(--v-theme-warning), 0.7);
    transform: scale(1);
  }
  50% {
    box-shadow: 0 0 0 10px rgba(var(--v-theme-warning), 0);
    transform: scale(1.01);
  }
  100% {
    box-shadow: 0 0 0 0 rgba(var(--v-theme-warning), 0);
    transform: scale(1);
  }
}
</style>
