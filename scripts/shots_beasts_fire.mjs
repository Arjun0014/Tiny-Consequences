// node scripts/shots_beasts_fire.mjs [--sett 1] [--herd flock] [--days 1] [--hour 9] [--dry 0.15] [--off 0.15] [--n 6] [--zoom 1.3]
// (S22, D-177) A stand of trees set alight beside a herd at pasture on a dry morning: screenshots as the
// beasts notice the fire, run each its own way and keep away while it burns (out/shots/beasts_fire).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const herd = getFlag(args, '--herd', 'flock');
const days = Number(getFlag(args, '--days', 1));
const hour = Number(getFlag(args, '--hour', 9));
const dry = Number(getFlag(args, '--dry', 0.15));
const off = Number(getFlag(args, '--off', 0.15));
const ntrees = Number(getFlag(args, '--n', 6));
const zoom = Number(getFlag(args, '--zoom', 1.3));
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'beasts_fire');
fs.mkdirSync(outDir, { recursive: true });
const port = 8196;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `b${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
try {
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  const g = await cmd({ cmd: 'v3_goto', lh: hour, days });
  const pr = await cmd({ cmd: 'v3_premise', sett, kind: 'pasture_fire', herd, dry, off, n: ntrees });
  console.log(`${pr.name}: ${pr.added} trees alight ${off} from the ${herd} (${pr.beasts} out) at (${pr.x.toFixed(2)}, ${pr.y.toFixed(2)})`);
  const lx = (pr.x + pr.fx) / 2, ly = (pr.y + pr.fy) / 2;
  for (const [i, dt] of [[0, 0.01], [1, 0.08], [2, 0.2], [3, 0.4], [4, 0.8], [5, 2.5]]) {
    await cmd({ cmd: 'v3_goto', hour: g.hour + dt });
    await cmd({ cmd: 'v3_look', sett, x: lx, y: ly, zoom });
    await page.waitForTimeout(400);
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(2500);
    const file = path.join(outDir, `${herd}_s${sett}_${i}.png`);
    await page.screenshot({ path: file });
    const b = await cmd({ cmd: 'v3_premise', sett, kind: 'burning' });
    console.log(`shot ${rel(file)} (+${dt} h): ${b.burning ? b.burning.split(';').length + ' burning' : 'nothing burning'} | drawn ${b.drawn}`);
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
