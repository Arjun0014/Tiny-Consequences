// node scripts/shots_geo.mjs — the v3 settlements' geography as the game draws it (D-085): each
// settlement from above, its dock, its nearest ford, and a fisher afloat. Writes out/shots/geo/.
// Flags: --no-build, --port N (default 8187), --zoom Z (overview), --hour H (local hour, default 10)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8187));
const zoom = Number(getFlag(args, '--zoom', 1.05));
const hour = Number(getFlag(args, '--hour', 10));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'geo');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { shots: [], console: [] };
let nextId = 1;
async function cmd(page, c, timeout = 120_000) {
  const id = `g${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
async function tryCmd(page, c) {
  try { return await cmd(page, c); } catch (e) { return null; }
}
async function shoot(page, name, note) {
  await page.waitForTimeout(400);
  await cmd(page, { cmd: 'flush_view' }, 120_000);
  await page.waitForTimeout(2500);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file });
  report.shots.push({ file: rel(file), note });
  console.log(`geo shots: ${rel(file)}  ${note || ''}`);
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  await cmd(page, { cmd: 'v3_goto', lh: hour });
  for (let s = 1; s <= 8; s++) {
    const c = await tryCmd(page, { cmd: 'v3_look', sett: s, what: 'centre', zoom });
    if (!c) break;
    await shoot(page, `s${s}_overview`, c.name);
    if (await tryCmd(page, { cmd: 'v3_look', sett: s, what: 'dock', zoom: 1.45 })) await shoot(page, `s${s}_dock`, `${c.name} dock`);
    const f = await tryCmd(page, { cmd: 'v3_look', sett: s, what: 'ford', zoom: 1.3 });
    if (f) await shoot(page, `s${s}_ford`, `${c.name} ford at ${f.x.toFixed(2)},${f.y.toFixed(2)}`);
  }
  // a fisher afloat: step on by quarter hours until someone rows or fishes
  for (let k = 0; k < 144; k++) {
    const p = await tryCmd(page, { cmd: 'v3_person', pid: 'fisher', look: true, zoom: 1.8 });
    if (p) { await shoot(page, `fisher_${k}`, `#${p.id} ${p.name}: ${p.objective} / ${p.action} | ${p.why}`); if (k > 0) break; }
    const now = await page.evaluate(() => window.__TC.hour);
    await cmd(page, { cmd: 'v3_goto', hour: now + 0.5 });
  }
} catch (e) { failed = e; }
report.console = report.console.filter((l) => !l.includes('INFO:'));
fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2));
await browser.close();
server.close();
if (failed) { console.error('geo shots FAILED: ' + failed.message); console.error(report.console.slice(-12).join('\n')); process.exit(1); }
