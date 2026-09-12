/**
 * Format a number as a USD currency string with no decimal places.
 * Uses the browser's Intl.NumberFormat for locale-aware formatting.
 */
export function formatCurrency(value: number): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(value)
}

/**
 * Format a decimal as a percentage string with one decimal place.
 * E.g., 0.543 → "54.3%"
 */
export function formatPercent(value: number): string {
  return `${(value * 100).toFixed(1)}%`
}

/**
 * Format an ISO date string into a short, human-readable form.
 * E.g., "2026-09-12T..." → "Sep 12, 8:30 AM"
 */
export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  })
}
