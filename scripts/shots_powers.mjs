// node scripts/shots_powers.mjs [--sett 1] [--at 20] [--power 1] [--wait 4]
// (S21, R7) A god power cast the way a player does it: close in on a village, the power's button pressed in
// the bar, a click on the village; then what the world did, and the news (out/shots/powers).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const at = Number(getFlag(args, '--at', 20));
const power = Number(getFlag(args, '--power', 1));
const waitH = Number(getFlag(args, '--wait', 4));
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'powers');
fs.mkdirSync(outDir, { recursive: true });
const port = 8199;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `p${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
async function press(x, y) { await page.mouse.move(x, y); await page.waitForTimeout(150); await page.mouse.down(); await page.waitForTimeout(150); await page.mouse.up(); await page.waitForTimeout(200); }
try {
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  await cmd({ cmd: 'v3_goto', hour: at });
  await cmd({ cmd: 'v3_look', sett, what: 'house', zoom: 1.4 }).catch(async () => cmd({ cmd: 'v3_look', sett, what: 'centre', zoom: 1.4 }));
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(1500);
  const vp = page.viewportSize();
  const sx = vp.width / 1280, sy = vp.height / 720;
  // the bar: 9 buttons of 54 with gaps of 10, centred, 51 px from the bottom
  const nb = 9, w = nb * 54 + (nb - 1) * 10 + 28;
  const bx = 640 - w / 2 + 14 + 27 + (power - 1) * 64;
  await press(bx * sx, (720 - 51) * sy);
  let file = path.join(outDir, 'armed.png');
  await page.screenshot({ path: file });
  console.log(`shot ${rel(file)} (power ${power} armed)`);
  await press(640 * sx, 360 * sy);
  await page.waitForTimeout(500);
  await cmd({ cmd: 'v3_goto', hour: at + 0.3 });
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(1500);
  file = path.join(outDir, 'cast.png');
  await page.screenshot({ path: file });
  console.log(`shot ${rel(file)} (cast)`);
  await cmd({ cmd: 'v3_goto', hour: at + waitH });
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(1500);
  file = path.join(outDir, 'after.png');
  await page.screenshot({ path: file });
  const feed = await cmd({ cmd: 'news' });
  for (const c of feed.cards) console.log(`card [${c.level}] ${c.title} | ${c.line} | root ${c.root} | why: ${c.why.join(' <- ')}`);
  console.log(`shot ${rel(file)} (+${waitH} h)`);
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
