// npm run shots:biomes — close-up review shots of each landscape of the world (the bridge picks
// a representative cell per landscape: temperate/mixed/boreal forest, jungle, savanna, desert,
// tundra, marsh, grassland, mountain), at local noon. Writes out/shots/biomes/<name>.png.
// --wild: instead, the biggest herd and pack of each kind (bridge wild_spots, W4) -> out/shots/wild/.
// Flags: --no-build, --release, --zoom Z (default 1.0), --hour H (default 13), --seed N,
//        --only a,b, --years N (run the world N years first)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8186));
const zoom = Number(getFlag(args, '--zoom', 1.0));
const hour = Number(getFlag(args, '--hour', 13));
const seedFlag = getFlag(args, '--seed', null);
const only = getFlag(args, '--only', null);
const years = Number(getFlag(args, '--years', 0));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const wild = hasFlag(args, '--wild');
const outDir = path.join(ROOT, 'out', 'shots', wild ? 'wild' : 'biomes');
fs.mkdirSync(outDir, { recursive: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let nextId = 1;
async function cmd(page, c, timeout = 120_000) {
  const id = `b${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  const q = { freeze: '1', speed: '0' };
  if (seedFlag !== null) q.seed = String(seedFlag);
  const worldFlag = getFlag(args, '--world', null);
  if (worldFlag !== null) q.world = String(worldFlag);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  if (years > 0) {
    await cmd(page, { cmd: 'run_ticks', n: years * 48 });
    await page.waitForFunction((n) => window.__TC && window.__TC.ff_remaining === 0 && window.__TC.tick >= n, years * 48, { timeout: 600_000 });
  }
  const spots = await cmd(page, { cmd: wild ? 'wild_spots' : 'biome_spots' });
  for (const [name, s] of Object.entries(spots)) {
    if (only && !only.split(',').includes(name)) continue;
    await cmd(page, { cmd: 'camera', lat: s.lat, lon: s.lon, zoom });
    await cmd(page, { cmd: 'run_hours', to_local: hour }, 300_000);
    await page.waitForTimeout(300);
    await cmd(page, { cmd: 'flush_view' }, 120_000);
    await page.waitForFunction(() => window.__TC && window.__TC.chunks_dirty === 0, null, { timeout: 120_000, polling: 200 });
    await page.waitForTimeout(800);
    const file = path.join(outDir, `${name}.png`);
    await page.screenshot({ path: file });
    console.log(`${wild ? 'wild' : 'biomes'}: ${rel(file)}  cell ${s.cell} lat ${s.lat.toFixed(1)} lon ${s.lon.toFixed(1)}${s.n ? ' n ' + s.n.toFixed(0) : ''}`);
  }
} finally {
  await browser.close();
  server.close();
}
