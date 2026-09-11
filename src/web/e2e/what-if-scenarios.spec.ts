import { test, expect } from '@playwright/test'
import { setupMockApi } from './fixtures/mockData'

test.describe('What-If Scenarios & State Integrity', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page)
    await page.goto('/')

    // Complete flow to reach simulator
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    await page.getByRole('option', { name: /The University of Texas at Austin/i }).click()

    await page.getByRole('button', { name: /Computer & Information Sciences/i }).click()
    await page.getByRole('row', { name: /Computer Science/i }).click()

    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')
    await page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i }).click()

    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
  })

  test('applies salary override and allows resetting back to scorecard median', async ({ page }) => {
    // Initial state: $95,000 gross scorecard median
    await expect(page.getByText('$95,000/yr')).toBeVisible()

    // Enter custom salary override: 120,000
    const overrideInput = page.getByRole('spinbutton', { name: 'What-if salary override' })
    await overrideInput.fill('120000')

    const recalcBtn = page.getByRole('button', { name: 'Recalculate' })
    await recalcBtn.click()

    // Verify recalculation applied
    await expect(page.getByText('$120,000/yr')).toBeVisible()
    const resetBtn = page.getByRole('button', { name: 'Reset' })
    await expect(resetBtn).toBeVisible()

    // Click Reset
    await resetBtn.click()

    // Verify reverted to original $95,000
    await expect(page.getByText('$95,000/yr')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Reset' })).not.toBeVisible()
  })

  test('changing data year preserves target destination and simulation', async ({ page }) => {
    // Destination city is currently Austin
    await expect(page.locator('.v-autocomplete__selection-text', { hasText: 'Austin-Round Rock-Georgetown' })).toBeVisible()
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()

    // Change Data Year dropdown
    const yearSelect = page.getByLabel('Data Year')
    await yearSelect.click({ force: true })

    // Select previous year option
    const yearOption = page.getByRole('option').nth(1)
    await yearOption.click()

    // Verify that the destination and budget simulator are STILL visible and NOT wiped out
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
    await expect(page.locator('.v-autocomplete__selection-text', { hasText: 'Austin-Round Rock-Georgetown' })).toBeVisible()
  })

  test('clearing school search resets downstream state to empty display', async ({ page }) => {
    // Locate clear button on school search autocomplete
    const clearButton = page.locator('.v-autocomplete').first().locator('.v-field__clearable')
    await clearButton.click()

    // Verify empty state is restored
    await expect(page.getByText('Search for a school to get started')).toBeVisible()
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).not.toBeVisible()
  })
})

