// node scripts/shots_look.mjs --sett K --days D --hour H [--zoom Z] [--what centre|dock|ford] [--name N]
// One screenshot of settlement K, D days on, at hour H of settlement 1's local clock (D-088
// visual checks). Writes out/shots/look/<name>.png and prints who is doing what nearby.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const days = Number(getFlag(args, '--days', 0));
const hour = Number(getFlag(args, '--hour', 10));
const zoom = Number(getFlag(args, '--zoom', 1.4));
const what = getFlag(args, '--what', 'centre');
const herd = getFlag(args, '--herd', 'cattle');
const pid = Number(getFlag(args, '--pid', 0));
const name = getFlag(args, '--name', `s${sett}_d${days}_h${hour}`);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'look');
fs.mkdirSync(outDir, { recursive: true });
const port = 8192;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `l${n++}`;
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
  await cmd({ cmd: 'v3_goto', lh: hour, days });
  const at = pid ? await cmd({ cmd: 'v3_person', pid, look: true, zoom }) : await cmd({ cmd: 'v3_look', sett, what, zoom, herd });
  if (pid) at.name = `#${pid} ${at.name || ''} ${at.obj || ''} | ${at.why || ''}`;
  await page.waitForTimeout(400);
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(2500);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file });
  console.log(`look: ${rel(file)} (${at.name})` + (at.info ? ' ' + JSON.stringify(at.info) : ''));
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
