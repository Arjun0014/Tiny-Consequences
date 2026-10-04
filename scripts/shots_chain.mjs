// "Your actions" played (S23, D-182; docs/v3/77_CHAIN_REACTION.md): a dry day at Tekheim, a bolt on the tree
// nearest the houses, the world lives on, then the chain of what the bolt caused opened in the panel; then rain
// on the parched ground and the list of both casts. Writes out/shots/chain/*.png and chain.json (the panel's
// rows as the bridge reads them: sim/chain.lua).
//   node scripts/shots_chain.mjs [--no-build] [--port 8197] [--hours 4]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8197));
const hours = Number(getFlag(args, '--hours', 4));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'chain');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { steps: [], console: [] };
let nextId = 1;
async function cmd(page, c, timeout = 300_000) {
  const id = `a${nextId++}`;
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
  console.log(`chain: ${rel(file)}  ${note || ''}`);
}
function printRows(res) {
  for (const I of res.list) console.log(`  [${I.power}] ${I.title}: ${I.n_total} (${I.n_direct} direct)${I.active ? ', unfolding' : ''}`);
  for (const r of res.rows || []) console.log(`    ${'  '.repeat(r.depth - 1)}- ${r.title}${r.line ? ' - ' + r.line : ''}${r.active ? '  *' : ''}`);
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', cloudq: '24' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  await cmd(page, { cmd: 'v3_goto', lh: 15, days: 3 });
  await cmd(page, { cmd: 'v3_premise', sett: 1, kind: 'named', name: 'dry_day' });
  const aim = await cmd(page, { cmd: 'v3_look', sett: 1, what: 'tree_near_house', zoom: 1.45 });
  await cmd(page, { cmd: 'power', power: 'lightning', cell: aim.cell, sid: 1, x: aim.x, y: aim.y });
  const t0 = (await page.evaluate(() => window.__TC.hour));
  for (let h = 0.5; h <= hours; h += 0.5) await cmd(page, { cmd: 'v3_goto', hour: t0 + h });
  await shoot(page, '01_after_the_bolt', 'the village some hours after the bolt');
  let res = await cmd(page, { cmd: 'actions', open: true, ev: 'latest' });
  printRows(res);
  report.bolt = res;
  await shoot(page, '02_chain_of_the_bolt', 'Your actions: the chain of the bolt');
  // rain on the parched hills: a second cast, then the list
  await cmd(page, { cmd: 'power', power: 'rain', cell: aim.cell });
  const t1 = (await page.evaluate(() => window.__TC.hour));
  for (let h = 1; h <= 12; h += 1) await cmd(page, { cmd: 'v3_goto', hour: t1 + h });
  res = await cmd(page, { cmd: 'actions', open: true });
  printRows(res);
  report.list = res;
  await shoot(page, '03_your_actions', 'Your actions: both casts');
  res = await cmd(page, { cmd: 'actions', open: true, ev: 'latest' });
  printRows(res);
  report.rain = res;
  await shoot(page, '04_chain_of_the_rain', 'Your actions: the chain of the rain');
  await cmd(page, { cmd: 'actions', open: false });
  await cmd(page, { cmd: 'camera', zoom: 0.6 });
  await shoot(page, '05_news_with_chain', 'the news: stories rooted at YOU with their chain');
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'chain.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
