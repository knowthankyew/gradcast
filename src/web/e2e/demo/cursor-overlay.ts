import type { Page, Locator } from '@playwright/test'

export interface CursorState {
  x: number
  y: number
}

// Global cursor state per page
let currentPos: CursorState = { x: 640, y: 300 }

/**
 * Injects a stylish modern cursor overlay into the page.
 * Pure CSS/JS, easy to toggle, includes a subtle fade-in and click ripples.
 */
export async function installCursorOverlay(page: Page): Promise<void> {
  await page.evaluate(() => {
    // Remove existing cursor if re-installing
    const existing = document.getElementById('gradcast-demo-cursor')
    if (existing) existing.remove()

    const style = document.createElement('style')
    style.id = 'gradcast-demo-cursor-style'
    style.innerHTML = `
      #gradcast-demo-cursor {
        position: fixed;
        top: 0;
        left: 0;
        width: 24px;
        height: 24px;
        margin-top: -4px;
        margin-left: -4px;
        border-radius: 50%;
        background: rgba(79, 70, 229, 0.45);
        border: 2px solid #ffffff;
        box-shadow: 0 0 10px rgba(79, 70, 229, 0.7), 0 2px 6px rgba(0, 0, 0, 0.3);
        pointer-events: none;
        z-index: 99999999;
        transition: transform 0.05s linear, opacity 0.5s ease-in-out;
        opacity: 0;
        display: flex;
        align-items: center;
        justify-content: center;
      }
      #gradcast-demo-cursor::after {
        content: '';
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: #ffffff;
      }
      #gradcast-demo-cursor.visible {
        opacity: 1;
      }
      .gradcast-demo-ripple {
        position: fixed;
        border-radius: 50%;
        border: 2px solid rgba(79, 70, 229, 0.9);
        background: rgba(129, 140, 248, 0.35);
        pointer-events: none;
        z-index: 99999998;
        transform: translate(-50%, -50%) scale(0.2);
        animation: gradcast-ripple-anim 0.45s cubic-bezier(0.2, 0.8, 0.2, 1) forwards;
      }
      @keyframes gradcast-ripple-anim {
        0% {
          transform: translate(-50%, -50%) scale(0.2);
          opacity: 0.9;
        }
        100% {
          transform: translate(-50%, -50%) scale(2.2);
          opacity: 0;
        }
      }
    `
    document.head.appendChild(style)

    const cursorEl = document.createElement('div')
    cursorEl.id = 'gradcast-demo-cursor'
    document.body.appendChild(cursorEl)

    // Listen for mousemove and mousedown
    window.addEventListener('mousemove', (e) => {
      cursorEl.style.transform = `translate3d(${e.clientX}px, ${e.clientY}px, 0)`
      if (!cursorEl.classList.contains('visible')) {
        cursorEl.classList.add('visible')
      }
    })

    window.addEventListener('mousedown', (e) => {
      const ripple = document.createElement('div')
      ripple.className = 'gradcast-demo-ripple'
      ripple.style.left = `${e.clientX}px`
      ripple.style.top = `${e.clientY}px`
      ripple.style.width = '28px'
      ripple.style.height = '28px'
      document.body.appendChild(ripple)
      setTimeout(() => ripple.remove(), 500)
    })
  })

  // Initialize position and trigger gentle fade-in
  currentPos = { x: 640, y: 220 }
  await page.mouse.move(currentPos.x, currentPos.y)
  await sleep(200)
}

export function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

/**
 * Smoothly interpolates mouse position from currentPos to target (x, y)
 */
export async function smoothMoveTo(
  page: Page,
  targetX: number,
  targetY: number,
  steps = 22,
  stepDelayMs = 12
): Promise<void> {
  const startX = currentPos.x
  const startY = currentPos.y

  for (let i = 1; i <= steps; i++) {
    // Cubic ease-in-out curve
    const t = i / steps
    const ease = t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2
    const x = Math.round(startX + (targetX - startX) * ease)
    const y = Math.round(startY + (targetY - startY) * ease)

    await page.mouse.move(x, y)
    currentPos = { x, y }
    if (stepDelayMs > 0) {
      await sleep(stepDelayMs)
    }
  }

  currentPos = { x: targetX, y: targetY }
}

/**
 * Smoothly navigates cursor to the center of a locator
 */
export async function smoothMove(
  page: Page,
  locator: Locator,
  steps = 22,
  stepDelayMs = 12
): Promise<{ x: number; y: number }> {
  await locator.first().waitFor({ state: 'visible', timeout: 5000 })
  await locator.first().scrollIntoViewIfNeeded()

  const box = await locator.first().boundingBox()
  if (!box) {
    throw new Error('Element bounding box not found for smooth move')
  }

  const targetX = Math.round(box.x + box.width / 2)
  const targetY = Math.round(box.y + box.height / 2)

  await smoothMoveTo(page, targetX, targetY, steps, stepDelayMs)
  return { x: targetX, y: targetY }
}

/**
 * Performs a human-like smooth move, brief pause, click, and ripple hold.
 */
export async function smoothClick(
  page: Page,
  locator: Locator,
  options: {
    holdBeforeMs?: number
    holdAfterMs?: number
    steps?: number
  } = {}
): Promise<void> {
  const { holdBeforeMs = 120, holdAfterMs = 280, steps = 22 } = options

  await smoothMove(page, locator, steps)
  if (holdBeforeMs > 0) await sleep(holdBeforeMs)

  await page.mouse.down()
  await sleep(70)
  await page.mouse.up()

  if (holdAfterMs > 0) await sleep(holdAfterMs)
}

/**
 * Types text with organic human cadence (50ms - 85ms per character)
 */
export async function humanType(
  page: Page,
  locator: Locator,
  text: string,
  options: { baseDelayMs?: number; clickFirst?: boolean } = {}
): Promise<void> {
  const { baseDelayMs = 70, clickFirst = true } = options

  if (clickFirst) {
    await smoothClick(page, locator, { holdAfterMs: 150 })
  }

  for (const char of text) {
    await page.keyboard.type(char)
    const jitter = Math.floor(Math.random() * 25) - 10
    await sleep(Math.max(35, baseDelayMs + jitter))
  }

  await sleep(200)
}

/**
 * Smoothly scrolls an element into the center of the viewport
 */
export async function smoothScrollIntoCenter(
  locator: Locator,
  sleepAfterMs = 500
): Promise<void> {
  await locator.first().waitFor({ state: 'visible', timeout: 5000 })
  await locator.first().evaluate((el) => {
    el.scrollIntoView({ behavior: 'smooth', block: 'center' })
  })
  if (sleepAfterMs > 0) {
    await sleep(sleepAfterMs)
  }
}

/**
 * Performs a smooth page scroll
 */
export async function smoothScrollBy(
  page: Page,
  deltaY: number,
  steps = 14,
  stepDelayMs = 20
): Promise<void> {
  const perStep = deltaY / steps
  for (let i = 0; i < steps; i++) {
    await page.evaluate((dy) => window.scrollBy({ top: dy, behavior: 'instant' }), perStep)
    await sleep(stepDelayMs)
  }
}
