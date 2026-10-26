// How long the Web build takes to catch up a day away, and what the views cost it: load a fresh world, save it, come back
// "a day later" (Living: ~96 world years) with the camera at a chosen height and chosen layers off (the bridge's `off NAME`),
// and time the catch-up from the page's load to the world arriving.
// Usage: node tc2/scripts/webcatchup.mjs [--alt 1400|22000] [--off a,b,c] [--away 24h] [--tag NAME] [--webgl2]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { guardBrowser } from './gpusafe.mjs';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const flag = (f) => a.includes(f);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const webgl2 = flag('--webgl2');
const alt = opt('--alt', '1400');
const off = opt('--off', '').split(',').filter((s) => s);
const away = opt('--away', '24h');
const tag = opt('--tag', 'catchup');
const outDir = path.join(root, 'tc2', 'out', 'webcatchup', tag);
fs.mkdirSync(outDir, { recursive: true });

const GPU = ['--mute-audio', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8644;
const server = await startServer({ port, quiet: true });
guardBrowser('webcatchup');   // (session 27: the integrated GPU only - never the RX 5600M)
const browser = await chromium.launch({ headless: true, channel: webgl2 ? undefined : 'chrome', args: GPU });
const page = await (await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 })).newPage();
const state = () => page.evaluate(() => window.__TC2 || null);
const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
const base = `http://127.0.0.1:${port}/index.html`;
const gfx = webgl2 ? '&gfx=webgl2' : '';
const result = { alt, off, away };
try {
  await page.goto(`${base}?fresh=1&seed=42&speed=0${gfx}`);
  await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0, null, { timeout: 300000, polling: 200 });
  await cmd(`village 0 ${alt}`);
  await page.waitForTimeout(1500);
  await cmd('save');
  await page.waitForFunction(() => window.__TC2 && window.__TC2.saveOk === true, null, { timeout: 60000, polling: 200 });
  const t0 = Date.now();
  await page.goto(`${base}?speed=0&absence=${away}${gfx}`);
  await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true, null, { timeout: 300000, polling: 100 });
  for (const l of off) await cmd(`off ${l}`);
  await cmd(`catchupalt ${alt}`);   // (builds that raise the camera while catching up: hold it at this height instead)
  await cmd(`village 0 ${alt}`);
  let saw = false, fpsSum = 0, fpsN = 0;
  for (let i = 0; i < 2400; i++) {
    const s = await state();
    if (s && s.catchingUp) { saw = true; if (s.fps) { fpsSum += s.fps; fpsN++; } }
    if (s && saw && !s.catchingUp) break;
    await page.waitForTimeout(250);
  }
  const s = await state();
  result.seconds = (Date.now() - t0) / 1000;
  result.year = s.year; result.people = s.people; result.places = s.places;
  result.fpsDuring = fpsN ? Math.round(fpsSum / fpsN) : null;
  console.log(`catch-up (camera at ${alt} m${off.length ? ', off ' + off.join(' ') : ''}): ${result.seconds.toFixed(1)} s to year ${s.year}, ${s.people} people in ${s.places} places, ~${result.fpsDuring} fps during`);
} catch (e) {
  result.error = String(e);
  console.log('error: ' + e);
} finally {
  fs.writeFileSync(path.join(outDir, `catchup_${alt}${off.length ? '_' + off.join('-') : ''}.json`), JSON.stringify(result, null, 2));
  await browser.close();
  server.close();
}
