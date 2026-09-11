import { test, expect } from '@playwright/test'
import { setupMockApi, mockSchoolDetail, mockFinanceSimulation } from './fixtures/mockData'

test.describe('GradCast Core User Flow', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockApi(page)
    await page.goto('/')
  })

  test('displays initial empty state and page title', async ({ page }) => {
    await expect(page).toHaveTitle(/GradCast/i)
    await expect(page.getByText('Search for a school to get started')).toBeVisible()
    await expect(page.getByText('Planning tool only')).toBeVisible()
  })

  test('dismisses disclaimer banner and keeps it dismissed on reload', async ({ page }) => {
    await expect(page.getByText('Planning tool only')).toBeVisible()
    await page.getByRole('button', { name: 'Got it' }).click()
    await expect(page.getByText('Planning tool only')).not.toBeVisible()

    // Reload page to verify persistence in localStorage
    await page.reload()
    await expect(page.getByText('Planning tool only')).not.toBeVisible()
  })

  test('completes full progressive disclosure flow: school -> program -> destination -> simulation', async ({ page }) => {
    // 1. Search for a school
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    
    // Select from autocomplete dropdown
    const option = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await option.click()

    // Verify SchoolDetail card
    await expect(page.getByRole('heading', { name: mockSchoolDetail.name })).toBeVisible()
    await expect(page.locator('.v-card-subtitle', { hasText: 'Austin, TX' })).toBeVisible()
    await expect(page.getByText('Public', { exact: true })).toBeVisible()
    await expect(page.getByText('31.0%')).toBeVisible() // Admission rate

    // 2. Toggle Tuition Trend
    const trendBtn = page.getByRole('button', { name: /Show Tuition Trend/i })
    await expect(trendBtn).toBeVisible()
    await trendBtn.click()
    await expect(page.getByText('Tuition Trend (5 Year)')).toBeVisible()

    // 3. Select Program
    await expect(page.getByText('Programs by Department')).toBeVisible()
    // Open Computer & Information Sciences panel
    const compSciPanel = page.getByRole('button', { name: /Computer & Information Sciences/i })
    await compSciPanel.click()

    // Click Computer Science program row
    const csRow = page.getByRole('row', { name: /Computer Science/i })
    await csRow.click()

    // Verify selected program chip
    await expect(page.locator('.v-chip', { hasText: 'Computer Science' })).toBeVisible()

    // 4. Select Target Destination
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')

    const metroOption = page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i })
    await metroOption.click()

    // 5. Verify Job Market Pulse Widget appears
    await expect(page.getByText('Job Market Pulse')).toBeVisible()
    await expect(page.getByText('1,420')).toBeVisible() // active openings
    await expect(page.getByText('$102,000', { exact: true })).toBeVisible() // local market salary

    // 6. Verify Budget Simulator appears
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
    await expect(page.getByText('$6,150', { exact: true })).toBeVisible() // Net take-home
    await expect(page.getByText('$1,550', { exact: true })).toBeVisible() // Rent monthly
    await expect(page.getByText('$4,150', { exact: true })).toBeVisible() // Disposable income
    await expect(page.getByText('Comfortable — solid financial cushion')).toBeVisible()

    // 7. Toggle housing choice to Roommate
    const roommateBtn = page.getByRole('button', { name: /Roommate \(2-Bed\)/i })
    await roommateBtn.click()
    await expect(roommateBtn).toHaveClass(/v-btn--active/)
  })

  test('toggles "Hide missing data" filter on programs list', async ({ page }) => {
    // 1. Search and select school
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    const option = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await option.click()

    await expect(page.getByText('Programs by Department')).toBeVisible()

    // 2. Verify filter is off by default
    await expect(page.getByText('Showing all 3 programs')).toBeVisible()
    const commPanel = page.getByRole('button', { name: /Communication & Journalism/i })
    await expect(commPanel).toBeVisible()

    // 3. Toggle switch ON to hide missing data
    const filterSwitch = page.getByRole('switch', { name: /Hide missing data/i })
    await filterSwitch.click()

    // 4. Verify filtered count and that program with missing earnings is hidden
    await expect(page.getByText('Showing 2 of 3 programs (missing earnings hidden)')).toBeVisible()
    await expect(commPanel).not.toBeVisible()

    // 5. Verify programs with earnings remain visible
    const csPanel = page.getByRole('button', { name: /Computer & Information Sciences/i })
    await expect(csPanel).toBeVisible()

    // 6. Toggle filter OFF and verify full list is restored
    await filterSwitch.click()
    await expect(page.getByText('Showing all 3 programs')).toBeVisible()
    await expect(commPanel).toBeVisible()
  })

  test('toggles "Hide missing data" filter on target destination selector', async ({ page }) => {
    // 1. Search and select school to display destination card
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    const schoolOption = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await schoolOption.click()

    await expect(page.getByText('Target Destination')).toBeVisible()

    // 2. Search metro area with filter off (default)
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Rural')

    // Rural location without rent data should appear
    const ruralOption = page.getByRole('option', { name: /Rural Outpost Without Housing Data/i })
    await expect(ruralOption).toBeVisible()
    await expect(page.getByText('Rent data unavailable')).toBeVisible()

    // 3. Toggle "Hide missing data" ON in Destination card
    const destinationCard = page.locator('.v-card', { hasText: 'Target Destination' })
    const filterSwitch = destinationCard.getByRole('switch', { name: /Hide missing data/i })
    await filterSwitch.click()

    // 4. Verify that location without housing data is now hidden
    await expect(ruralOption).not.toBeVisible()

    // 5. Search for Austin (which has housing data)
    await metroInput.fill('Austin')
    const austinOption = page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i })
    await expect(austinOption).toBeVisible()
    await expect(page.getByText('$1,550/mo (1-bed)')).toBeVisible()

    // 6. Switch housing to roommate (2-Bed) and verify rent preview updates
    const roommateBtn = page.getByRole('button', { name: /Roommate \(2-Bed\)/i })
    await roommateBtn.click()
    await expect(page.getByText('$975/mo (shared 2-bed)')).toBeVisible()
  })
})

