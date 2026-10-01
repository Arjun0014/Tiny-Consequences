// npm run shots:raid — a cattle raid (D-060): raiders set off (bridge `cattle_raid`), reach the
// pasture, and drive the beasts home. Writes out/shots/raid/*.png and shots.json.
// Flags: --release, --no-build, --port N (default 8187), --world NAME, --zoom Z (default 1.1)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8187));
const worldName = getFlag(args, '--world', null);
const zoom = Number(getFlag(args, '--zoom', 1.1));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'raid');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { shots: [], console: [] };
let nextId = 1;
let failed = null;
async function cmd(page, c, timeout = 60_000) {
  const id = `r${nextId++}`;
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
  await page.waitForTimeout(1800);
}
async function shot(page, name, where) {
  await cmd(page, { cmd: 'camera', lat: where.lat, lon: where.lon, zoom });
  await settle(page);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file });
  report.shots.push({ file: rel(file), where });
  console.log(`raid ${name}: ${rel(file)} ${JSON.stringify(where)}`);
}
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  page.on('console', (m) => { if (m.type() === 'error') report.console.push(m.text()); });
  const q = { speed: '0', freeze: '1', world: 'v2' };   // the legacy v2 world (D-079)
  if (worldName) q.world = worldName;
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  // raids set off at first light after the village's midnight council
  const pr = await cmd(page, { cmd: 'cattle_raid', probe: true });
  await cmd(page, { cmd: 'camera', lat: pr.lat, lon: pr.lon, zoom });
  await cmd(page, { cmd: 'run_hours', to_local: 0 });
  const r = await cmd(page, { cmd: 'cattle_raid', n: 8, from: pr.from, to: pr.to });
  report.raid = r;
  // on the way (midway), then after the fight: the drive home, midway
  await cmd(page, { cmd: 'run_hours', n: Math.max(1, Math.round((r.depart + r.arrive) / 2 - r.hour)) });
  await shot(page, 'going', await cmd(page, { cmd: 'party_pos', party: r.party }));
  // hour by hour until the raiders set off home with the beasts
  let back = null;
  for (let i = 0; i < 12 && !back; i++) {
    await cmd(page, { cmd: 'run_hours', n: 1 });
    try { back = await cmd(page, { cmd: 'party_pos', home: r.from }); } catch { back = null; }
  }
  if (!back) throw new Error('the raiders never set off home');
  await shot(page, 'driving_home', back);
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
