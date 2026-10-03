// node scripts/shots_news.mjs [--premise flood5,fire] [--at 20] [--after 3,9]
// (S21, R7) The world news in the game: premises at hour --at, the feed after each step of hours, then a
// click on the top card (a real mouse click: the camera flies there and the inspector opens), then the
// "Why?" badge. Shots in out/shots/news.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const premises = String(getFlag(args, '--premise', 'flood5,fire')).split(',').filter(Boolean);
const at = Number(getFlag(args, '--at', 20));
const after = String(getFlag(args, '--after', '3,9')).split(',').map(Number);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'news');
fs.mkdirSync(outDir, { recursive: true });
const port = 8197;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback|NEWSDBG/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
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
  for (const pr of premises) {
    const r = await cmd({ cmd: 'v3_premise', sett: 1, kind: 'named', name: pr });
    console.log(`premise ${pr}: ${r.added ? 'made' : 'not made'}`);
  }
  let k = 0;
  for (const dt of after) {
    k += 1;
    await cmd({ cmd: 'v3_goto', hour: at + dt });
    await cmd({ cmd: 'camera', preset: 'v3' }).catch(() => {});
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(1800);
    const file = path.join(outDir, `feed_${k}.png`);
    await page.screenshot({ path: file });
    console.log(`shot ${rel(file)} (+${dt} h)`);
  }
  const feed = await cmd({ cmd: 'news' });
  for (const c of feed.cards) console.log(`card [${c.level}] ${c.title} | ${c.line} | root ${c.root} | why: ${c.why.join(' <- ')}`);
  if (process.argv.includes('--bridge-go')) {
    const g = await cmd({ cmd: 'news', go: 1 });
    console.log('bridge go: ' + JSON.stringify(g.went));
  }
  // click the top card (GUI 1280x720: the first card's middle at x 180, 609 from the bottom)
  const vp = page.viewportSize();
  const sx = vp.width / 1280, sy = vp.height / 720;
  // (a real press: down, held a few frames, up - a click within one frame is not seen as a touch)
  async function press(x, y) { await page.mouse.move(x, y); await page.waitForTimeout(120); await page.mouse.down(); await page.waitForTimeout(120); await page.mouse.up(); }
  await press(180 * sx, (720 - 609) * sy);
  await page.waitForTimeout(3500);
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(1500);
  let file = path.join(outDir, 'feed_clicked.png');
  await page.screenshot({ path: file });
  const cam = await cmd({ cmd: 'camera' });
  const ins = await cmd({ cmd: 'ui', inspector: true });
  console.log(`clicked the top card: camera at lat ${cam.lat.toFixed(1)} lon ${cam.lon.toFixed(1)} zoom ${cam.zoom.toFixed(2)}; inspector: ${ins.inspector ? ins.inspector.title : 'none'}`);
  // the "Why?" badge of the top card (its right end)
  await press((180 + 136) * sx, (720 - 598) * sy);
  await page.waitForTimeout(800);
  file = path.join(outDir, 'feed_why.png');
  await page.screenshot({ path: file });
  console.log(`shot ${rel(file)} (Why?)`);
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
