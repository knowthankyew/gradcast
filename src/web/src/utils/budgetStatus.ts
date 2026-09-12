/**
 * Shared budget income status helpers.
 * Used by BudgetSimulator.vue and SavedBudgets.vue to map the
 * backend IncomeStatus string to display properties.
 */

export type IncomeStatus = 'comfortable' | 'manageable' | 'tight' | 'deficit'

export function statusColor(status: string): string {
  switch (status) {
    case 'comfortable': return 'green'
    case 'manageable': return 'light-green'
    case 'tight': return 'orange'
    case 'deficit': return 'red'
    default: return 'grey'
  }
}

export function statusIcon(status: string): string {
  switch (status) {
    case 'comfortable': return 'mdi-check-circle'
    case 'manageable': return 'mdi-check'
    case 'tight': return 'mdi-alert'
    case 'deficit': return 'mdi-alert-circle'
    default: return 'mdi-help'
  }
}

export function statusLabel(status: string): string {
  switch (status) {
    case 'comfortable': return 'Comfortable — solid financial cushion'
    case 'manageable': return 'Manageable — budget works but limited flexibility'
    case 'tight': return 'Tight — very little room for extras'
    case 'deficit': return 'Deficit — expenses exceed income'
    default: return ''
  }
}

/**
 * Map an income status to a Vuetify text color class (for inline text styling).
 * Distinct from statusColor() which returns a Vuetify color name for chips/icons.
 */
export function statusTextColor(status: string): string {
  switch (status) {
    case 'comfortable': return 'text-green-darken-2'
    case 'manageable': return 'text-green-darken-1'
    case 'tight': return 'text-orange-darken-2'
    case 'deficit': return 'text-red-darken-2'
    default: return ''
  }
}
