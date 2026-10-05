// What a frame costs in the Web build, layer by layer: load the build (WebGPU in Chrome, or WebGL2 in Chromium), frame a
// view, read the HUD's frame rate, then turn each drawn layer off in turn (the bridge's `off NAME`) and read it again.
// Usage: node tc2/scripts/webperf.mjs [--webgl2] [--view village|close|low|orbit] [--layers a,b,c] [--tag NAME]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const flag = (f) => a.includes(f);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const webgl2 = flag('--webgl2');
const view = opt('--view', 'village');
const tag = opt('--tag', 'perf');
const layers = opt('--layers', 'tilt,shadows,terrain,clouds,sky,air,sea,planet,rivers,worldobjectsview,trees,people,buildings,wildlifeview,livestockview,sealifeview,puffrenderer,rainview,volcanoview').split(',');
const alt = { orbit: 22000, region: 6000, village: 1500, close: 450, low: 160 }[view] || 1500;
const outDir = path.join(root, 'tc2', 'out', 'webperf', tag);
fs.mkdirSync(outDir, { recursive: true });

const GPU = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8643;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, channel: webgl2 ? undefined : 'chrome', args: GPU });
const page = await (await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 })).newPage();
const logs = [];
page.on('console', (m) => { const t = m.text(); if (t.includes('[TC2]')) logs.push(t); });
const state = () => page.evaluate(() => window.__TC2 || null);
const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
async function fps(settle = 5000) { await page.waitForTimeout(settle); const s = await state(); return s ? s.fps : null; }

const rows = [];
try {
  await page.goto(`http://127.0.0.1:${port}/index.html?fresh=1&seed=42&speed=0${webgl2 ? '&gfx=webgl2' : ''}${flag('--nowarm') ? '&nowarm=1' : ''}`);
  await page.waitForFunction(() => (window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0) || window.__TC2_FATAL, null, { timeout: 300000, polling: 200 });
  const s0 = await state();
  for (const l of logs) console.log('  log: ' + l.slice(0, 160));
  await cmd(`village 0 ${alt}`);
  if (flag('--trace')) {
    // the frame rate second by second after the camera comes down (the HUD's value is smoothed over ~0.5 s)
    const seq = [];
    for (let i = 0; i < 16; i++) { await page.waitForTimeout(1000); const s = await state(); seq.push(s ? Math.round(s.fps) : null); }
    console.log('  fps each second after framing: ' + seq.join(' '));
  }
  const first = await fps(5000), later = await fps(10000), base = await fps(15000);
  console.log(`${s0.gfx} ${view}: all layers ${first} fps after 5 s, ${later} after 15 s, ${base} after 30 s (first frames compile shaders)`);
  rows.push({ layer: 'all@5s', fps: first }, { layer: 'all@15s', fps: later }, { layer: 'all', fps: base });
  // WebGL2 shadow settings to try: --glshadow "1:1.6,1:1.2,2:1.2"
  const tries = opt('--glshadow', '');
  for (const t of tries ? tries.split(',') : []) {
    const [c, r] = t.split(':');
    await cmd(`glshadow ${c} ${r}`);
    const f = await fps(8000);
    console.log(`  shadows ${c} cascade(s), reach ${r} x height: ${f} fps`);
    rows.push({ layer: `glshadow ${t}`, fps: f });
  }
  if (flag('--settle-only')) process.exit(0);
  for (const l of layers) {
    await cmd(`off ${l}`);
    const f = await fps();
    await cmd(`on ${l}`);
    await page.waitForTimeout(800);
    console.log(`  without ${l.padEnd(18)} ${String(f).padStart(6)} fps`);
    rows.push({ layer: l, fps: f });
  }
  for (const l of layers) await cmd(`off ${l}`);
  const none = await fps();
  console.log(`  without all of them      ${String(none).padStart(6)} fps`);
  rows.push({ layer: 'none', fps: none });
  await page.screenshot({ path: path.join(outDir, `${view}_bare.png`) });
} finally {
  fs.writeFileSync(path.join(outDir, `${view}${webgl2 ? '_webgl2' : ''}.json`), JSON.stringify(rows, null, 2));
  await browser.close();
  server.close();
}
