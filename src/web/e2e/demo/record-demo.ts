import { chromium, type Browser, type BrowserContext, type Page } from '@playwright/test'
import * as path from 'path'
import * as fs from 'fs'
import { setupMockApi } from '../fixtures/mockData'
import {
  installCursorOverlay,
  smoothMove,
  smoothClick,
  humanType,
  smoothScrollIntoCenter,
  smoothScrollBy,
  sleep,
} from './cursor-overlay'

export interface RecordDemoOptions {
  outputDir: string
  baseURL?: string
  headless?: boolean
}

/**
 * Executes the scripted narrative demo flow and returns the path to the recorded video file.
 */
export async function recordGradCastDemo(options: RecordDemoOptions): Promise<string> {
  const { outputDir, baseURL = 'http://127.0.0.1:5173', headless = true } = options

  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true })
  }

  const browser: Browser = await chromium.launch({
    headless,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-web-security',
      '--hide-scrollbars',
    ],
  })

  const context: BrowserContext = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    deviceScaleFactor: 1,
    recordVideo: {
      dir: outputDir,
      size: { width: 1280, height: 720 },
    },
  })

  // Pre-seed localStorage to dismiss disclaimer cleanly
  await context.addInitScript(() => {
    window.localStorage.setItem('gradcast_disclaimer_dismissed', 'true')
  })

  const page: Page = await context.newPage()
  await setupMockApi(page)

  try {
    // 1. Initial Page Load
    await page.goto(baseURL, { waitUntil: 'networkidle' })
    await installCursorOverlay(page)
    await sleep(350)

    // 2. Search and Select School
    const schoolInput = page.getByPlaceholder('Start typing a school name...')
    await humanType(page, schoolInput, 'Texas', { baseDelayMs: 65 })
    await sleep(200)

    const schoolOption = page.getByRole('option', { name: /The University of Texas at Austin/i })
    await smoothClick(page, schoolOption, { holdBeforeMs: 100, holdAfterMs: 600 })

    // 3. Reveal 5-Year Tuition Trend Chart
    const trendBtn = page.getByRole('button', { name: /Show Tuition Trend/i })
    await smoothClick(page, trendBtn, { holdBeforeMs: 80, holdAfterMs: 800 })

    // 4. Select Program (Computer Science)
    const programsCard = page.locator('.v-card', { hasText: 'Programs by Department' })
    await smoothScrollIntoCenter(programsCard, 350)

    const compSciPanel = page.getByRole('button', { name: /Computer & Information Sciences/i })
    await smoothClick(page, compSciPanel, { holdBeforeMs: 80, holdAfterMs: 300 })

    const csRow = page.getByRole('row', { name: /Computer Science/i })
    await smoothClick(page, csRow, { holdBeforeMs: 80, holdAfterMs: 600 })

    // 5. Select Destination City (Austin) and Toggle Roommate Housing
    const destinationCard = page.locator('.v-card', { hasText: 'Target Destination' })
    await smoothScrollIntoCenter(destinationCard, 350)

    const metroInput = page.getByPlaceholder('Start typing a city or metro area...')
    await humanType(page, metroInput, 'Austin', { baseDelayMs: 60 })
    await sleep(200)

    const metroOption = page.getByRole('option', { name: /Austin-Round Rock-Georgetown/i })
    await smoothClick(page, metroOption, { holdBeforeMs: 90, holdAfterMs: 600 })

    // Interactive Housing Toggle (Roommate 2-Bed)
    const roommateBtn = page.getByRole('button', { name: /Roommate \(2-Bed\)/i })
    await smoothClick(page, roommateBtn, { holdBeforeMs: 100, holdAfterMs: 700 })

    // 6. Highlight Budget Simulator Results
    const budgetCard = page.locator('.v-card', { hasText: 'Post-Grad Monthly Budget Simulator' })
    await smoothScrollIntoCenter(budgetCard, 350)
    // Hold to allow viewer to clearly read income, rent, loans, and disposable cushion
    await sleep(1400)

    // 7. Save Scenario to LocalStorage
    const savedCard = page.locator('.v-card', { hasText: 'Saved Scenarios' })
    await smoothScrollIntoCenter(savedCard, 350)

    const scenarioInput = page.getByPlaceholder(/Harvard CS → Boston/i)
    await humanType(page, scenarioInput, 'UT Austin CS → Austin', { baseDelayMs: 40 })
    await sleep(150)

    const saveBtn = page.getByRole('button', { name: 'Save Scenario' })
    await smoothClick(page, saveBtn, { holdBeforeMs: 100, holdAfterMs: 1500 })

    // Brief final pause before loop
    await sleep(400)
  } finally {
    // Await closing page so video stream finishes flushing
    await page.close()
    await context.close()
    await browser.close()
  }

  // Retrieve the generated video
  const files = fs.readdirSync(outputDir)
  const videoFile = files.find((f) => f.endsWith('.webm'))
  if (!videoFile) {
    throw new Error(`No .webm video file found in ${outputDir}`)
  }

  return path.join(outputDir, videoFile)
}
