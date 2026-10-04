// Session 23 UI: the news history (Your actions -> News) and the sound settings panel, opened as a player would
// (a held mouse press on the pills). Writes out/shots/ui23/*.png.
//   node scripts/shots_ui23.mjs [--no-build] [--port 8216]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8216));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'ui23');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let nextId = 1;
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  async function cmd(c, timeout = 300_000) {
    const id = `u${nextId++}`;
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
    const r = await h.jsonValue();
    if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
    return r.result;
  }
  async function shoot(name) {
    await page.waitForTimeout(400);
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(1500);
    const file = path.join(outDir, `${name}.png`);
    await page.screenshot({ path: file, timeout: 600_000 });
    console.log(`ui23: ${rel(file)}`);
  }
  const vp = page.viewportSize();
  const sx = vp.width / 1280, sy = vp.height / 720;
  // (a real press: down, held a few frames, up - a click within one frame is not seen as a touch)
  async function press(x, y) { await page.mouse.move(x * sx, y * sy); await page.waitForTimeout(150); await page.mouse.down(); await page.waitForTimeout(150); await page.mouse.up(); await page.waitForTimeout(400); }
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  // a few days lived and a cast, so the news has a history and the chain something to show
  await cmd({ cmd: 'v3_goto', lh: 11, days: 3 });
  const sq = await cmd({ cmd: 'v3_look', sett: 1, what: 'square', zoom: 1.35 });
  await cmd({ cmd: 'power', power: 'lightning', cell: sq.cell, sid: 1, x: sq.x + 0.2, y: sq.y + 0.15 });
  const t0 = await page.evaluate(() => window.__TC.hour);
  for (let h = 0.5; h <= 6; h += 0.5) await cmd({ cmd: 'v3_goto', hour: t0 + h });
  // the sound pill (bottom right: x 1160, 34 up from the bottom) opens the panel; + on "The world" once
  await press(1160, 720 - 34);
  await shoot('01_sound_panel');
  // the news history: Your actions opened in its News mode
  await press(1160, 720 - 34);
  const res = await cmd({ cmd: 'actions', open: true, news: true });
  console.log(`actions: ${res.list.length} casts`);
  await shoot('02_news_history');
  await cmd({ cmd: 'actions', open: false });
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
