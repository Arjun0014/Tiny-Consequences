// In-browser simulation benchmark (14 §2-3): loads the Web build in Chrome, runs benchmark worlds inside the page
// (BenchRunner: its own world, frame slices of 45 ms, no rendering of that world) and records pure simulation
// time per world year. Compare with: node tc2/scripts/headless.mjs bench --name poc --settlements S --founders F --years Y [--coarse]
// Usage: node tc2/scripts/webbench.mjs [--sets "6x24x20:activity,6x24x60:coarse"] [--webgl2]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const sets = opt('--sets', '6x24x20:activity,6x24x60:coarse,20x50x10:activity,20x50x30:coarse,40x125x3:activity,40x125x10:coarse').split(',');
const outDir = path.join(root, 'tc2', 'out', 'webbench');
fs.mkdirSync(outDir, { recursive: true });

const server = await startServer({ port: 8643, quiet: true });
const browser = await chromium.launch({ headless: true, channel: a.includes('--chromium') ? undefined : 'chrome', args: ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-unsafe-webgpu'] });
const page = await browser.newPage({ viewport: { width: 960, height: 540 } });
const lines = [];
page.on('console', (m) => lines.push(m.text()));
await page.goto(`http://127.0.0.1:8643/index.html?fresh=1&speed=0${a.includes('--webgl2') ? '&gfx=webgl2' : ''}`);
await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true, null, { timeout: 300000 });
const results = [];
for (const set of sets) {
  const [dims, mode] = set.split(':');
  const [s, f, y] = dims.split('x');
  await page.evaluate((c) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(c); }, `bench ${s} ${f} ${y} ${mode}`);
  await page.waitForFunction(() => window.__TC2 && window.__TC2.bench && window.__TC2.bench.startsWith('running'), null, { timeout: 120000 });
  const h = await page.waitForFunction(() => window.__TC2 && window.__TC2.bench && window.__TC2.bench.startsWith('done') && window.__TC2.bench, null, { timeout: 3600000, polling: 1000 });
  const r = await h.jsonValue();
  console.log(r);
  results.push(r);
}
fs.writeFileSync(path.join(outDir, 'results.txt'), results.join('\n') + '\n');
await browser.close();
server.close();
