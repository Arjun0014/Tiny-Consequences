// npm run shots — Stage 1.1 placeholder: one screenshot of the running game.
// Builds if stale, serves the bundle on a private port, loads it in headless Chromium
// (SwiftShader WebGL) and writes out/shots/latest/w01.png + w01.json (__TC + console).
// The full screenshot protocol (07_TESTING.md §7) replaces this later.
// Flags: --release, --no-build, --port N (default 8182)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, die, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8182));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });

const outDir = path.join(ROOT, 'out', 'shots', 'latest');
fs.mkdirSync(outDir, { recursive: true });

const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  const consoleLog = [];
  page.on('console', (m) => consoleLog.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => consoleLog.push(`[pageerror] ${e.message}`));
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { freeze: '1' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 20_000 });
  await page.waitForTimeout(1000);
  const tc = await page.evaluate(() => window.__TC);
  const shot = path.join(outDir, 'w01.png');
  await page.screenshot({ path: shot });
  fs.writeFileSync(path.join(outDir, 'w01.json'), JSON.stringify({ tc, console: consoleLog }, null, 2) + '\n');
  console.log(`shots: ${rel(shot)} (fps ${tc.fps}, errors ${tc.errors.length})`);
} catch (e) {
  failed = e;
} finally {
  await browser.close();
  server.close();
}
if (failed) die(`shots failed: ${failed.message}`);
