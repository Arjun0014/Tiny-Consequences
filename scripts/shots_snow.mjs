// node scripts/shots_snow.mjs [--sett 1] [--at 72.02] [--premise cold_snap] [--zoom 1.45] [--after 11,20,30,44]
// (S20, R6.5d) A cold air mass over village K at absolute hour --at (the shipped world's day 3 is wet at
// Tekheim): screenshots of the village as the snow falls and lies (out/shots/snow), with the depth,
// the herds out and the hay.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const at = Number(getFlag(args, '--at', 72.02));
const premise = getFlag(args, '--premise', 'cold_snap');
const zoom = Number(getFlag(args, '--zoom', 1.45));
const after = String(getFlag(args, '--after', '11,20,30,44')).split(',').map(Number);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'snow');
fs.mkdirSync(outDir, { recursive: true });
const port = 8195;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `s${n++}`;
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
  await cmd({ cmd: 'v3_goto', hour: at });
  const pr = await cmd({ cmd: 'v3_premise', sett, kind: 'named', name: premise });
  console.log(`premise ${premise} at hour ${at}: ${pr.added ? 'made' : 'not made'}`);
  for (const [i, dt] of after.entries()) {
    await cmd({ cmd: 'v3_goto', hour: at + dt });
    await cmd({ cmd: 'v3_look', sett, what: 'centre', zoom });
    await page.waitForTimeout(400);
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(2500);
    const file = path.join(outDir, `${premise}_s${sett}_${i + 1}.png`);
    await page.screenshot({ path: file });
    const st = await cmd({ cmd: 'v3_premise', sett, kind: 'snow' });
    console.log(`shot ${rel(file)} (+${dt} h): snow ${st.snow.toFixed(3)} m${st.snowing ? ' falling' : ''}, ${st.temp.toFixed(1)} C, cover ${st.cover.toFixed(2)}, herds out ${st.herds_out}, hay ${st.hay.toFixed(0)}`);
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
