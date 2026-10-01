// npm run shots:v3 — the v3 agent village (D-078) through a day, headless: the village at local
// dawn, morning work, midday, evening; then one person followed close up. Writes
// out/shots/v3/*.png and shots.json (each shot with the followed person's objective and why).
// Flags: --no-build, --port N (default 8186), --hours a,b,c (local hours), --person ID, --zoom Z
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8186));
const zoom = Number(getFlag(args, '--zoom', 1.3));
const HOURS = getFlag(args, '--hours', '6.2,8.5,12.5,18.2').split(',').map(Number);
const person = getFlag(args, '--person', 'worker');
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'v3');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { shots: [], console: [] };
let nextId = 1;
async function cmd(page, c, timeout = 120_000) {
  const id = `v${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
async function shoot(page, name, who) {
  await page.waitForTimeout(400);
  await cmd(page, { cmd: 'flush_view' }, 120_000);
  await page.waitForTimeout(2500);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file });
  const tc = await page.evaluate(() => window.__TC);
  const st = who ? await cmd(page, { cmd: 'v3_person', pid: who }) : null;
  report.shots.push({ file: rel(file), hour: tc.hour, person: st });
  console.log(`v3 shots: ${rel(file)}${st ? `  #${st.id} ${st.name}: ${st.objective} / ${st.action} -> ${st.dest} | ${st.why}` : ''}`);
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  const q = { world: 'v3', speed: '0' };
  const vn = getFlag(args, '--villagers', null);
  if (vn !== null) q.villagers = vn;
  if (hasFlag(args, '--oldpeople')) q.oldpeople = '1';
  for (const f of ['vnoattr', 'vnoshadow', 'vnoanim', 'vnoenable', 'vlod0']) if (hasFlag(args, '--' + f)) q[f] = '1';
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  for (const h of HOURS) {
    await cmd(page, { cmd: 'v3_goto', lh: h });
    await cmd(page, { cmd: 'camera', preset: 'v3' });
    await cmd(page, { cmd: 'camera', lat: null, zoom });
    await shoot(page, `village_${String(h).replace('.', '_')}`, null);
  }
  // one person, close up, through part of a day
  await cmd(page, { cmd: 'v3_goto', lh: 8.0, days: 1 });
  const first = await cmd(page, { cmd: 'v3_person', pid: person });
  await cmd(page, { cmd: 'select', person: first.id });
  for (let k = 0; k < 4; k++) {
    await cmd(page, { cmd: 'v3_person', pid: first.id, look: true, zoom: 1.65 });
    await shoot(page, `follow_${k}`, first.id);
    await cmd(page, { cmd: 'v3_goto', hour: (await page.evaluate(() => window.__TC.hour)) + 0.0 });
    await cmd(page, { cmd: 'v3_goto', lh: [9.5, 12.2, 16.0, 19.0][k] });
  }
} catch (e) { failed = e; }
report.console = report.console.filter((l) => !l.includes('INFO:'));
fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2));
await browser.close();
server.close();
if (failed) { console.error('v3 shots FAILED: ' + failed.message); console.error(report.console.slice(-12).join('\n')); process.exit(1); }
