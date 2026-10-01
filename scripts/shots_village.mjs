// npm run shots:village — a village through its day (W2c): the biggest village close up at local
// dawn, the walk to work, midday, the walk home and evening, plus the village and person inspectors.
// The view draws the sim's own buildings, schedule and boat trips, so these frames show what the
// simulation is doing at those hours. Writes out/shots/village/*.png and shots.json.
// Flags: --release, --no-build, --port N (default 8183), --seed N, --years N / --weeks N (run first),
//        --zoom Z (default 1.0), --hours a,b,c (local hours, default 5.5,7.3,12,17.8,20.5)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8183));
const seedFlag = getFlag(args, '--seed', null);
const years = Number(getFlag(args, '--years', 0));
const weeks = Number(getFlag(args, '--weeks', 0)) + years * 48;
const zoom = Number(getFlag(args, '--zoom', 1.0));
const HOURS = getFlag(args, '--hours', '5.5,7.3,12,17.8,20.5').split(',').map(Number);
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });

const outDir = path.join(ROOT, 'out', 'shots', 'village');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));

const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { shots: [], console: [] };
let nextId = 1;
let failed = null;

async function cmd(page, c, timeout = 60_000) {
  const id = `v${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}

async function settle(page) {
  await page.waitForTimeout(300);
  await cmd(page, { cmd: 'flush_view' }, 120_000);
  await page.waitForFunction(() => window.__TC && window.__TC.chunks_dirty === 0, null, { timeout: 120_000, polling: 200 });
  await page.waitForTimeout(1500);   // puppets re-assign (0.6 s) and settle
}

async function shoot(page, name) {
  await settle(page);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file });
  const tc = await page.evaluate(() => window.__TC);
  report.shots.push({ file: rel(file), hour: tc.hour, people: tc.people_puppets, ships: tc.ships, view_ms: tc.view_ms });
  console.log(`village shots: ${rel(file)}  hour ${tc.hour}  people ${tc.people_puppets}  ships ${tc.ships}  view ${Number(tc.view_ms).toFixed(2)} ms`);
}

try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  const q = { freeze: '1', speed: '0', world: 'v2' };   // the legacy v2 world (D-079); --world overrides
  if (seedFlag !== null) q.seed = String(seedFlag);
  const worldFlag = getFlag(args, '--world', null);
  if (worldFlag !== null) q.world = String(worldFlag);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 180_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  await cmd(page, { cmd: 'select', none: true });
  if (weeks > 0) {
    await cmd(page, { cmd: 'run_ticks', n: weeks });
    await page.waitForFunction(() => window.__TC && window.__TC.ff_remaining === 0, null, { timeout: 900_000, polling: 500 });
  }
  await cmd(page, { cmd: 'camera', preset: 'village' });
  await cmd(page, { cmd: 'select', village: 'biggest', fly: true, zoom });
  await cmd(page, { cmd: 'select', none: true });
  for (const h of HOURS) {
    await cmd(page, { cmd: 'run_hours', to_local: h }, 300_000);
    await shoot(page, `h${String(h).replace('.', '_')}`);
  }
  await cmd(page, { cmd: 'select', village: 'biggest' });
  await shoot(page, 'inspector_village');
  // the rest of the village inspector: the mouse wheel over the panel scrolls it
  const vp = page.viewportSize();
  await page.mouse.move(vp.width - 170, vp.height / 2);
  for (let k = 0; k < 4; k++) { await page.mouse.wheel(0, 120); await page.waitForTimeout(150); }
  await page.waitForTimeout(600);
  await shoot(page, 'inspector_village_2');
  for (let k = 0; k < 6; k++) { await page.mouse.wheel(0, 120); await page.waitForTimeout(150); }
  await page.waitForTimeout(600);
  await shoot(page, 'inspector_village_3');
  await cmd(page, { cmd: 'select', person: 'worker' });
  await shoot(page, 'inspector_person');
  await cmd(page, { cmd: 'select', none: true });
  await cmd(page, { cmd: 'camera', preset: 'village' });
  await shoot(page, 'mid');
} catch (e) {
  failed = e;
} finally {
  fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2));
  await browser.close();
  server.close();
}
const errs = report.console.filter((l) => l.startsWith('[error]') || l.startsWith('[pageerror]'));
if (errs.length) console.log(errs.slice(0, 20).join('\n'));
if (failed) { console.error(failed); process.exit(1); }
