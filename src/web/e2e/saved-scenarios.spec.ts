import { test, expect } from '@playwright/test'
import { setupMockApi, mockSchoolDetail } from './fixtures/mockData'

test.describe('Saved Scenarios Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page)
    await page.goto('/')

    // Complete disclosure flow to reach simulation
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    await page.getByRole('option', { name: /The University of Texas at Austin/i }).click()

    // Select program
    await page.getByRole('button', { name: /Computer & Information Sciences/i }).click()
    await page.getByRole('row', { name: /Computer Science/i }).click()

    // Select destination
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')
    await page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i }).click()

    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
  })

  test('saves, reloads, and deletes a scenario', async ({ page }) => {
    // 1. Save scenario
    const scenarioNameInput = page.getByPlaceholder(/Harvard CS → Boston/i)
    await scenarioNameInput.fill('UT Austin CS - Austin Living Alone')
    await page.getByRole('button', { name: 'Save Scenario' }).click()

    // Verify confirmation and item in list
    await expect(page.getByText('Scenario saved!')).toBeVisible()
    await expect(page.getByText('UT Austin CS - Austin Living Alone')).toBeVisible()
    await expect(page.getByText('$4,150/mo disposable')).toBeVisible()

    // 2. Clear school to reset to empty state
    const clearSchoolBtn = page.locator('.v-autocomplete').first().locator('.v-field__clearable')
    await clearSchoolBtn.click()
    await expect(page.getByText('Search for a school to get started')).toBeVisible()

    // 3. Reload the saved scenario
    const reloadBtn = page.getByLabel(/Load scenario: UT Austin CS - Austin Living Alone/i)
    await reloadBtn.click()

    // Verify school, program, and simulation restored
    await expect(page.getByRole('heading', { name: mockSchoolDetail.name })).toBeVisible()
    await expect(page.getByText('$4,150', { exact: true })).toBeVisible()

    // 4. Delete the scenario
    const deleteBtn = page.getByLabel(/Delete scenario: UT Austin CS - Austin Living Alone/i)
    await deleteBtn.click()

    await expect(page.getByText('No saved scenarios yet')).toBeVisible()
  })

  test('clears all scenarios with confirmation dialog', async ({ page }) => {
    const scenarioNameInput = page.getByPlaceholder(/Harvard CS → Boston/i)
    
    // Save scenario 1
    await scenarioNameInput.fill('Scenario A')
    await page.getByRole('button', { name: 'Save Scenario' }).click()

    // Save scenario 2
    await scenarioNameInput.fill('Scenario B')
    await page.getByRole('button', { name: 'Save Scenario' }).click()

    await expect(page.getByText('Scenario A')).toBeVisible()
    await expect(page.getByText('Scenario B')).toBeVisible()

    // Click Clear All
    await page.getByRole('button', { name: 'Clear All' }).click()
    await expect(page.getByText('Clear all saved scenarios?')).toBeVisible()

    // Confirm deletion
    await page.getByRole('button', { name: 'Delete All' }).click()
    await expect(page.getByText('No saved scenarios yet')).toBeVisible()
  })
})

