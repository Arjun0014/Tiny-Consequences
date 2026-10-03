// node scripts/shots_fire.mjs [--sett 1] [--days 3] [--hour 20] [--dry 0.15] [--kind fire|strike] [--zoom 1.45]
// (S20, R6.5b) A roof set alight (or lightning at a house) at village K on day D at local hour H, with
// the ground at soil water --dry: screenshots as the fire grows and the village answers it (out/shots/fire).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const days = Number(getFlag(args, '--days', 3));
const hour = Number(getFlag(args, '--hour', 20));
const dry = Number(getFlag(args, '--dry', 0.15));
const kind = getFlag(args, '--kind', 'fire');
const zoom = Number(getFlag(args, '--zoom', 1.45));
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'fire');
fs.mkdirSync(outDir, { recursive: true });
const port = 8194;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `f${n++}`;
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
  const pr = await cmd({ cmd: 'v3_premise', sett, kind, dry });
  console.log(`premise ${kind} at ${pr.name}: building ${pr.added}`);
  for (const [i, dt] of [[1, 0.12], [2, 0.25], [3, 0.6]]) {
    await cmd({ cmd: 'v3_goto', hour: g.hour + dt });
    await cmd({ cmd: 'v3_look', sett, what: 'centre', zoom });
    await page.waitForTimeout(400);
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(2500);
    const file = path.join(outDir, `${kind}_s${sett}_h${hour}_${i}.png`);
    await page.screenshot({ path: file });
    const b = await cmd({ cmd: 'v3_premise', sett, kind: 'burning' });
    console.log(`shot ${rel(file)} (+${dt} h): ${b.burning || 'nothing burning'} | drawn ${b.drawn} | ${b.acc}`);
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
