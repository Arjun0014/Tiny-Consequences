// npm run shots:voyage — a shipment overseas (D-059): the biggest village with boats sends porters
// with grain to a village on another landmass (bridge `voyage`); frames of the porters walking to the
// boat, the boat at sea, the landing, and the boat coming home with them. Writes
// out/shots/voyage/*.png and shots.json.
// Flags: --release, --no-build, --port N (default 8186), --world NAME (assets/worlds/NAME.tcsave),
//        --zoom Z (default 0.9)
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
const worldName = getFlag(args, '--world', null);
const zoom = Number(getFlag(args, '--zoom', 0.9));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });

const outDir = path.join(ROOT, 'out', 'shots', 'voyage');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));

const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { shots: [], console: [] };
let nextId = 1;
let failed = null;

async function cmd(page, c, timeout = 60_000) {
  const id = `y${nextId++}`;
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
  await page.waitForTimeout(1500);
}

try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  page.on('console', (m) => { if (m.type() === 'error') report.console.push(m.text()); });
  const q = { speed: '0', freeze: '1', world: 'v2' };   // the legacy v2 world (D-079)
  if (worldName) q.world = worldName;
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  // decisions are taken at the sender's local midnight (they set off at first light): go there
  const pr = await cmd(page, { cmd: 'voyage', probe: true });
  await cmd(page, { cmd: 'camera', lat: pr.lat, lon: pr.lon, zoom });
  await cmd(page, { cmd: 'run_hours', to_local: 0 });
  const v = await cmd(page, { cmd: 'voyage', from: pr.from, to: pr.to });
  report.voyage = v;
  const hours = v.arrive - v.hour;
  // frames: setting off (the walk to the boat), a third and two thirds of the way, the landing
  const at = [0.02, 0.35, 0.65, 0.97].map((f) => Math.max(1, Math.round((v.depart - v.hour) + f * (v.arrive - v.depart))));
  let ran = 0;
  for (let i = 0; i < at.length; i++) {
    await cmd(page, { cmd: 'run_hours', n: at[i] - ran });
    ran = at[i];
    const pos = await cmd(page, { cmd: 'voyage_pos', party: v.party, boat: v.boat });
    await cmd(page, { cmd: 'camera', lat: pos.lat, lon: pos.lon, zoom });
    await settle(page);
    const file = path.join(outDir, `voyage_${i + 1}.png`);
    await page.screenshot({ path: file });
    report.shots.push({ file: rel(file), hour: pos.hour, where: pos });
    console.log(`voyage ${i + 1}: ${rel(file)} ${JSON.stringify(pos)}`);
  }
  console.log(`voyage: ${hours} h, ${JSON.stringify(v)}`);
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
