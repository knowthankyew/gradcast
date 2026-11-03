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
    const programsCard = page.locator('.v-card', { hasText: 'Programs by Department' })
    const filterSwitch = programsCard.getByRole('switch', { name: /Hide missing data/i })
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
    await metroInput.fill('Austin')
    await expect(page.getByText('$975/mo (shared 2-bed)')).toBeVisible()
  })

  test('prompts user to select program when destination is picked first, and allows school-wide average fallback', async ({ page }) => {
    // 1. Search and select school
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    const schoolOption = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await schoolOption.click()

    // 2. Select destination BEFORE picking a program
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')
    const metroOption = page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i })
    await metroOption.click()

    // 3. Verify ProgramRequiredPrompt card appears and BudgetSimulator is gated
    await expect(page.getByText('Select a Program to Generate Your Budget')).toBeVisible()
    await expect(page.getByText('Step 3 Needed')).toBeVisible()
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).not.toBeVisible()

    // 4. Click Proceed with School-Wide Average fallback
    const schoolAvgBtn = page.getByRole('button', { name: /Proceed with School-Wide Average/i })
    await schoolAvgBtn.click()

    // 5. Verify simulator is unlocked and displays school-wide average banner
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
    await expect(page.getByText('Viewing School-Wide Average Earnings')).toBeVisible()
    await expect(page.getByText('School-Wide Average Active')).toBeVisible()

    // 6. Select a specific program (Computer Science)
    const compSciPanel = page.getByRole('button', { name: /Computer & Information Sciences/i })
    await compSciPanel.click()
    const csRow = page.getByRole('row', { name: /Computer Science/i })
    await csRow.click()

    // 7. Verify Job Market Pulse is now active and school-wide warning is gone
    await expect(page.getByText('Job Market Pulse')).toBeVisible()
    await expect(page.getByText('Viewing School-Wide Average Earnings')).not.toBeVisible()
    await expect(page.getByText(/Computer Science \(Bachelor's Degree\)/)).toBeVisible()
  })

  test('gracefully handles programs with missing median earnings and allows national baseline application', async ({ page }) => {
    // 1. Search and select school
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await schoolInput.fill('Texas')
    const schoolOption = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await schoolOption.click()

    // 2. Select a location
    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await metroInput.fill('Austin')
    const metroOption = page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i })
    await metroOption.click()

    // 3. Select program with missing earnings (Communication, General)
    const commPanel = page.getByRole('button', { name: /Communication & Journalism/i })
    await commPanel.click()
    const commRow = page.getByRole('row', { name: /Communication, General/i })
    await commRow.click()

    // 4. Verify simulator displays missing earnings notice
    await expect(page.getByText('Post-Grad Monthly Budget Simulator')).toBeVisible()
    await expect(page.getByText(/No Median Earnings Reported/i)).toBeVisible()
    await expect(page.getByText('No Earnings Data Reported')).toBeVisible()
    await expect(page.getByText('Pending Salary Input')).toBeVisible()

    // 5. Fixed costs (rent + loan) are still calculated and visible
    await expect(page.getByText('Total Fixed Costs')).toBeVisible()

    // 6. Click Use $45,000 Baseline
    const baselineBtn = page.getByRole('button', { name: /Use \$45,000 Baseline/i }).first()
    await baselineBtn.click()

    // 7. Verify simulation recalculates with user override
    await expect(page.getByText('Pending Salary Input')).not.toBeVisible()
    await expect(page.getByText('Reset')).toBeVisible()
  })

  test('supports unanchored search, double-quoted exact matching, and mid-word state filtering across comboboxes', async ({ page }) => {
    const capturedSchoolQueries: Array<{ q: string | null; state: string | null }> = []
    await page.route('**/api/schools/search*', async (route) => {
      const url = new URL(route.request().url())
      capturedSchoolQueries.push({
        q: url.searchParams.get('q'),
        state: url.searchParams.get('state'),
      })
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 1001,
            name: 'The University of Texas at Austin',
            city: 'Austin',
            state: 'TX',
          },
        ]),
      })
    })

    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await expect(schoolInput).toHaveAttribute('autocorrect', 'off')
    await expect(schoolInput).toHaveAttribute('autocapitalize', 'off')
    await expect(schoolInput).toHaveAttribute('spellcheck', 'false')

    // 1. Typing just quotes or 1 effective char inside quotes should NOT trigger API search
    await schoolInput.fill('"T')
    await page.waitForTimeout(400)
    expect(capturedSchoolQueries).toHaveLength(0)

    // 2. Filter by state autocomplete supports mid-word unanchored search ("exas" -> "TX — Texas")
    const stateInput = page.getByRole('combobox', { name: 'Filter by state' })
    await stateInput.click()
    await stateInput.fill('exas')
    await page.getByRole('option', { name: /TX — Texas/i }).click()

    // 3. Double-quoted exact phrase + unanchored token triggers search with state=TX
    await schoolInput.fill('"University of Texas" Austin')
    const option = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await expect(option).toBeVisible()
    expect(capturedSchoolQueries.some((c) => c.q === '"University of Texas" Austin' && c.state === 'TX')).toBe(true)
    await option.click()

    // 4. Program search supports unanchored mid-word tokens AND double-quoted exact word boundaries
    const programSearch = page.getByLabel('Search programs by name', { exact: true })
    await expect(programSearch).toHaveAttribute('autocorrect', 'off')
    await expect(programSearch).toHaveAttribute('spellcheck', 'false')

    // Mid-word "eer" matches "Engineering, General" when unquoted
    await programSearch.fill('eer')
    await expect(page.getByText('Showing 1 of 3 programs matching "eer"')).toBeVisible()
    await expect(page.getByRole('row', { name: /Engineering, General/i })).toBeVisible()

    // Quoted '"eer"' enforces word boundaries and excludes mid-word substring matches
    await programSearch.fill('"eer"')
    await expect(page.getByText('No matching programs found')).toBeVisible()

    // Multi-token unanchored "science bachelor" matches across program title and credential name
    await programSearch.fill('science bachelor')
    await expect(page.getByRole('row', { name: /Computer Science/i })).toBeVisible()

    // Quoted '"Computer Science"' matches exact phrase
    await programSearch.fill('"Computer Science"')
    await expect(page.getByRole('row', { name: /Computer Science/i })).toBeVisible()
  })
})

