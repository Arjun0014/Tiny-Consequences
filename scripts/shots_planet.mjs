// node scripts/shots_planet.mjs --lat L --lon L [--zooms 0,0.4,0.8,1.2] [--name N] [--noon] [--no-build]
// The planet as the game draws it around a point (S14 geography pass: mountains, rivers), at several
// zooms; --noon first moves the world on to local noon there (the sun is real). Writes
// out/shots/planet/<name>_z<zoom>.png.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const lat = Number(getFlag(args, '--lat', 0));
const lon = Number(getFlag(args, '--lon', 0));
const zooms = String(getFlag(args, '--zooms', '0,0.4,0.8,1.2')).split(',').map(Number);
const name = getFlag(args, '--name', `p${lat}_${lon}`);
const gfx = getFlag(args, '--gfx', 'high');
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'planet');
fs.mkdirSync(outDir, { recursive: true });
const port = 8197;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 300)); });
let n = 1;
async function cmd(c, timeout = 300_000) {
  const id = `pl${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
try {
  const q = { speed: '0', gfx };
  if (getFlag(args, '--world', null)) q.world = getFlag(args, '--world', null);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  if (hasFlag(args, '--noon')) {
    const st = await cmd({ cmd: 'v3_hash', hours: 0 });
    const h0 = st.start;
    // local hour = (h + lon / 15) mod 24: the next local noon
    let h = 12 - lon / 15;
    while (h < h0) h += 24;
    await cmd({ cmd: 'v3_goto', hour: h });
  }
  for (const z of zooms) {
    await cmd({ cmd: 'camera', lat, lon, zoom: z });
    await page.waitForTimeout(600);
    await cmd({ cmd: 'flush_view' }).catch(() => {});
    await page.waitForTimeout(3000);
    const file = path.join(outDir, `${name}_z${z}.png`);
    await page.screenshot({ path: file });
    console.log(`planet: ${rel(file)}`);
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
