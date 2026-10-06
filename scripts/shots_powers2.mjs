// The R7 v2 powers played (S23, D-183): a meteor by Tekheim and what it caused; sickness sent into its flock and
// what it did two days on - the scene and "Your actions" for each. Writes out/shots/powers2/*.png and powers2.json.
//   node scripts/shots_powers2.mjs [--no-build] [--port 8215]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8215));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'powers2');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { steps: [] };
let nextId = 1;
async function cmd(page, c, timeout = 300_000) {
  const id = `p${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
async function shoot(page, name, note) {
  await page.waitForTimeout(300);
  await cmd(page, { cmd: 'flush_view' });
  await page.waitForTimeout(2500);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file, timeout: 600_000 });
  report.steps.push({ file: rel(file), note });
  console.log(`powers2: ${rel(file)}  ${note || ''}`);
}
function printRows(res) {
  for (const r of res.rows || []) console.log(`    ${'  '.repeat(r.depth - 1)}- ${r.title}${r.line ? ' - ' + r.line : ''}${r.active ? '  *' : ''}`);
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', cloudq: '24' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  await cmd(page, { cmd: 'v3_goto', lh: 11, days: 2 });
  const sq = await cmd(page, { cmd: 'v3_look', sett: 1, what: 'square', zoom: 1.35 });
  await shoot(page, '01_the_bar', 'the village and the fourteen powers');
  // a meteor a little off the square
  await cmd(page, { cmd: 'power', power: 'meteor', cell: sq.cell, sid: 1, x: sq.x + 0.35, y: sq.y + 0.2, instant: true });
  let t0 = await page.evaluate(() => window.__TC.hour);
  for (let h = 0.25; h <= 1.5; h += 0.25) await cmd(page, { cmd: 'v3_goto', hour: t0 + h });
  await shoot(page, '02_meteor', 'an hour and a half after the meteor');
  let res = await cmd(page, { cmd: 'actions', open: true, ev: 'latest' });
  printRows(res);
  report.meteor = res;
  await shoot(page, '03_meteor_chain', 'Your actions: the meteor');
  await cmd(page, { cmd: 'actions', open: false });
  // sickness into the flock
  const fl = await cmd(page, { cmd: 'v3_look', sett: 1, what: 'flock', zoom: 1.5 });
  await cmd(page, { cmd: 'power', power: 'sickness', cell: fl.cell, sid: 1, x: fl.x, y: fl.y });
  t0 = await page.evaluate(() => window.__TC.hour);
  for (let h = 2; h <= 60; h += 2) await cmd(page, { cmd: 'v3_goto', hour: t0 + h });
  await cmd(page, { cmd: 'v3_look', sett: 1, what: 'flock', zoom: 1.6 });
  await shoot(page, '04_sick_flock', 'the flock two and a half days after the sickness');
  res = await cmd(page, { cmd: 'actions', open: true, ev: 'latest' });
  printRows(res);
  report.sickness = res;
  await shoot(page, '05_sickness_chain', 'Your actions: the sickness');
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'powers2.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
