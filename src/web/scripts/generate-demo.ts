import { spawn, execSync, type ChildProcess } from 'child_process'
import * as path from 'path'
import * as fs from 'fs'
import { fileURLToPath } from 'url'
import ffmpegStatic from 'ffmpeg-static'
import { recordGradCastDemo } from '../e2e/demo/record-demo'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const REPO_ROOT = path.resolve(__dirname, '../../..')
const ASSETS_DIR = path.join(REPO_ROOT, 'docs', 'assets')
const TEMP_RECORDINGS_DIR = path.join(__dirname, '../test-results/demo-recording')

const DEV_SERVER_URL = 'http://127.0.0.1:5173'

async function isServerRunning(url: string): Promise<boolean> {
  try {
    const res = await fetch(url)
    return res.ok || res.status < 500
  } catch {
    return false
  }
}

async function waitForServer(url: string, timeoutMs = 30000): Promise<void> {
  const startTime = Date.now()
  while (Date.now() - startTime < timeoutMs) {
    if (await isServerRunning(url)) return
    await new Promise((r) => setTimeout(r, 400))
  }
  throw new Error(`Dev server did not start within ${timeoutMs}ms at ${url}`)
}

function resolveFfmpeg(): string {
  try {
    const sysFfmpeg = execSync('which ffmpeg', { encoding: 'utf-8' }).trim()
    if (sysFfmpeg && fs.existsSync(sysFfmpeg)) {
      return sysFfmpeg
    }
  } catch {
    // Fall back to ffmpeg-static
  }

  if (ffmpegStatic && fs.existsSync(ffmpegStatic)) {
    return ffmpegStatic
  }

  throw new Error('Unable to find a valid ffmpeg binary. Please check ffmpeg-static installation.')
}

async function main() {
  const isHeaded = process.argv.includes('--headed')

  console.log('🎬 Starting GradCast Demo Recording Pipeline...')

  // 1. Ensure docs/assets directory exists
  if (!fs.existsSync(ASSETS_DIR)) {
    fs.mkdirSync(ASSETS_DIR, { recursive: true })
  }

  // Clean temp recordings directory
  if (fs.existsSync(TEMP_RECORDINGS_DIR)) {
    fs.rmSync(TEMP_RECORDINGS_DIR, { recursive: true, force: true })
  }
  fs.mkdirSync(TEMP_RECORDINGS_DIR, { recursive: true })

  // 2. Manage Vite dev server
  let viteProcess: ChildProcess | null = null
  const alreadyRunning = await isServerRunning(DEV_SERVER_URL)

  if (!alreadyRunning) {
    console.log('⚡ Starting Vite development server on port 5173...')
    viteProcess = spawn('npm', ['run', 'dev', '--', '--host', '127.0.0.1', '--port', '5173'], {
      cwd: path.resolve(__dirname, '..'),
      stdio: 'pipe',
      shell: true,
    })

    viteProcess.stderr?.on('data', (d) => {
      // Print any unexpected errors
      const str = d.toString()
      if (str.includes('error') || str.includes('Error')) {
        process.stderr.write(str)
      }
    })

    await waitForServer(DEV_SERVER_URL)
    console.log('✓ Dev server is up and responsive.')
  } else {
    console.log('✓ Reusing existing dev server at ' + DEV_SERVER_URL)
  }

  let webmPath: string
  try {
    // 3. Record the scripted demo
    console.log('📹 Recording demo flow (1280x720) with synthetic cursor...')
    webmPath = await recordGradCastDemo({
      outputDir: TEMP_RECORDINGS_DIR,
      baseURL: DEV_SERVER_URL,
      headless: !isHeaded,
    })
    console.log(`✓ Recording complete: ${path.basename(webmPath)}`)
  } finally {
    if (viteProcess) {
      console.log('🛑 Stopping temporary Vite server...')
      viteProcess.kill('SIGTERM')
    }
  }

  // 4. Resolve FFmpeg
  const ffmpegBin = resolveFfmpeg()
  console.log(`🔧 Using FFmpeg: ${ffmpegBin}`)

  const gifOutput = path.join(ASSETS_DIR, 'gradcast-demo.gif')
  const mp4Output = path.join(ASSETS_DIR, 'gradcast-walkthrough.mp4')

  // 5. Convert to High-Quality Optimized Looping GIF (Target ≤ 4 MB)
  console.log('🎞️  Converting WebM to optimized looping GIF (960px, 12.5 fps, Bayer dither)...')
  const gifFilter =
    'fps=12.5,scale=960:-1:flags=lanczos,split[s0][s1];[s0]palettegen=max_colors=128:stats_mode=diff[p];[s1][p]paletteuse=dither=bayer:bayer_scale=3'
  const gifCmd = `"${ffmpegBin}" -y -i "${webmPath}" -vf "${gifFilter}" "${gifOutput}"`

  try {
    execSync(gifCmd, { stdio: 'inherit' })
  } catch (err) {
    console.error('Error generating GIF:', err)
    process.exit(1)
  }

  // 6. Convert to Crisp Web-Friendly MP4
  console.log('🎥 Converting WebM to H.264 MP4 (1280x720)...')
  const mp4Cmd = `"${ffmpegBin}" -y -i "${webmPath}" -c:v libx264 -pix_fmt yuv420p -profile:v high -level 4.0 -crf 22 -movflags +faststart "${mp4Output}"`

  try {
    execSync(mp4Cmd, { stdio: 'inherit' })
  } catch (err) {
    console.error('Error generating MP4:', err)
    process.exit(1)
  }

  // 7. Check file sizes
  const gifSize = (fs.statSync(gifOutput).size / (1024 * 1024)).toFixed(2)
  const mp4Size = (fs.statSync(mp4Output).size / (1024 * 1024)).toFixed(2)

  console.log('\n=============================================')
  console.log('🎉 Demo Generation Complete!')
  console.log(`🖼️  GIF: ${gifOutput} (${gifSize} MB)`)
  console.log(`🎥 MP4: ${mp4Output} (${mp4Size} MB)`)
  console.log('=============================================\n')

  // Clean intermediate webm files
  if (fs.existsSync(TEMP_RECORDINGS_DIR)) {
    fs.rmSync(TEMP_RECORDINGS_DIR, { recursive: true, force: true })
  }
}

main().catch((err) => {
  console.error('Fatal demo generation error:', err)
  process.exit(1)
})
