// node scripts/shots_flood.mjs [--sett 5] [--at 20] [--premise flood5] [--zoom 1.35] [--after 6,10,30,60] [--what byre]
// (S21, R6.5e) A wet spell over village K's hills at absolute hour --at: screenshots as the river comes
// over its banks and goes down again (out/shots/flood), looking at --what (a building kind, "flood" for
// the water nearest the square, or "centre"), with the flooded tiles and who is up out of the water.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 5));
const at = Number(getFlag(args, '--at', 20));
const premise = getFlag(args, '--premise', 'flood5');
const zoom = Number(getFlag(args, '--zoom', 1.35));
const what = getFlag(args, '--what', 'byre');
const after = String(getFlag(args, '--after', '6,10,30,60')).split(',').map(Number);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'flood');
fs.mkdirSync(outDir, { recursive: true });
const port = 8196;
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
    let lk;
    try { lk = await cmd({ cmd: 'v3_look', sett, what, zoom }); } catch (e) { lk = await cmd({ cmd: 'v3_look', sett, what: 'centre', zoom }); }
    await page.waitForTimeout(400);
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(2500);
    const file = path.join(outDir, `${premise}_s${sett}_${i + 1}.png`);
    await page.screenshot({ path: file, timeout: 600_000 });   // (S23: the rain's cloud deck under SwiftShader)
    console.log(`shot ${rel(file)} (+${dt} h) at ${what}: ${JSON.stringify(lk.info || {})}`);
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
