import { test, expect } from '@playwright/test'
import { setupMockApi, mockSchoolDetail } from './fixtures/mockData'

test.describe('Scenario Sharing and URL Sync', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page)
  })

  test('displays GitHub repo link in the footer', async ({ page }) => {
    await page.goto('/')

    const githubLink = page.locator('footer a[href="https://github.com/knowthankyew/gradcast"]')
    await expect(githubLink).toBeVisible()
    await expect(githubLink).toContainText('GitHub')
    await expect(githubLink).toHaveAttribute('target', '_blank')
  })

  test('dynamically updates the route as user makes selections and allows sharing', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await page.goto('/')

    // 1. Initial URL should be root
    expect(page.url()).toContain('127.0.0.1:5173')

    // 2. Select school -> URL updates to include school=1001
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    await page.getByRole('option', { name: /The University of Texas at Austin/i }).click()

    await expect(async () => {
      expect(page.url()).toContain('school=1001')
    }).toPass()

    // 3. Select program -> URL updates to include cip=11.0701
    await page.getByRole('button', { name: /Computer & Information Sciences/i }).click()
    await page.getByRole('row', { name: /Computer Science/i }).click()

    await expect(async () => {
      expect(page.url()).toContain('cip=11.0701')
    }).toPass()

    // 4. Select destination -> URL updates to include cbsa and housing
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')
    await page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i }).click()

    await expect(async () => {
      expect(page.url()).toContain('cbsa=12420')
      expect(page.url()).toContain('housing=1bed')
    }).toPass()

    // 5. Change housing to roommate (2bed)
    await page.getByRole('button', { name: /Roommate \(2-Bed\)/i }).click()

    await expect(async () => {
      expect(page.url()).toContain('housing=2bed')
    }).toPass()

    // 6. Click "Share Scenario" button in the simulator
    const shareBtn = page.getByRole('button', { name: /Share Scenario/i })
    await expect(shareBtn).toBeVisible()
    await shareBtn.click()

    // Verify confirmation snackbar
    await expect(page.getByText('Scenario link copied to clipboard!')).toBeVisible()
  })

  test('hydrates complete scenario directly from shared URL query params', async ({ page }) => {
    // Navigate directly with scenario parameters
    await page.goto('/scenario?school=1001&cip=11.0701&cred=3&cbsa=12420&housing=2bed')

    // School should be restored
    await expect(page.getByRole('heading', { name: mockSchoolDetail.name })).toBeVisible()

    // Location should be restored
    await expect(page.getByRole('combobox', { name: /Search metro areas/i })).toHaveValue('Austin-Round Rock-Georgetown')

    // Budget Simulator should automatically calculate and appear
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
    await expect(page.getByText('Rent (shared 2-bed)')).toBeVisible()
    await expect(page.getByText('$4,650', { exact: true })).toBeVisible()
  })

  test('shares scenario from saved scenarios list', async ({ page, context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write'])
    await page.goto('/scenario?school=1001&cip=11.0701&cred=3&cbsa=12420&housing=1bed')

    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()

    // Save scenario
    const scenarioNameInput = page.getByPlaceholder(/Harvard CS → Boston/i)
    await scenarioNameInput.fill('My Shared Scenario')
    await page.getByRole('button', { name: 'Save Scenario' }).click()

    await expect(page.getByText('Scenario saved!')).toBeVisible()
    await expect(page.getByText('My Shared Scenario')).toBeVisible()

    // Click share button on the saved scenario item
    const copyBtn = page.getByLabel(/Share scenario: My Shared Scenario/i)
    await expect(copyBtn).toBeVisible()
    await copyBtn.click()

    await expect(page.getByText('Scenario link copied to clipboard!')).toBeVisible()
  })
})
