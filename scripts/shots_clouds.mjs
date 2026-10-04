// Clouds seen from the planet down into a village (S23, clouds v5): the same world state shot at the planet
// view, mid-altitude, a descent through the cloud layer over a village (zooms 0.8 .. 1.55) and the village
// below; a rainy deck made by the god's rain cast a while before (or --natural: wait for a village's own
// rain). Also the frame time at each zoom with the clouds on and off (headless: SwiftShader - relative only).
// Writes out/shots/clouds[/<tag>]/*.png and clouds.json.
//   node scripts/shots_clouds.mjs [--no-build] [--tag before|after] [--port 8191] [--sett 1] [--after-h 20]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8191));
const tag = getFlag(args, '--tag', 'after');
const sett = Number(getFlag(args, '--sett', 1));
const afterH = Number(getFlag(args, '--after-h', 3));
const castLh = Number(getFlag(args, '--lh', 9.5));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'clouds', tag);
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { tag, shots: [], frames: [], console: [] };
let nextId = 1;
async function cmd(page, c, timeout = 180_000) {
  const id = `c${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
// mean frame time over n frames (requestAnimationFrame deltas)
async function frameMs(page, n = 4) {
  return page.evaluate((k) => new Promise((res) => {
    const ts = [];
    const f = (t) => { ts.push(t); if (ts.length > k) res((ts[ts.length - 1] - ts[0]) / k); else requestAnimationFrame(f); };
    requestAnimationFrame(f);
  }), n);
}
async function shoot(page, name, note) {
  await page.waitForTimeout(300);
  await cmd(page, { cmd: 'flush_view' });
  await page.waitForTimeout(1800);
  const file = path.join(outDir, `${name}.png`);
  await page.screenshot({ path: file, timeout: 600_000 });
  const tc = await page.evaluate(() => window.__TC);
  report.shots.push({ file: rel(file), note, hour: tc.hour, clouds: tc.clouds });
  console.log(`clouds ${tag}: ${rel(file)}  ${note || ''}`);
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', cloudq: '24' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  // the village, midmorning of day 2
  await cmd(page, { cmd: 'v3_goto', lh: castLh, days: 1 });
  const look = await cmd(page, { cmd: 'v3_look', sett, what: 'square' });
  const cam = await cmd(page, { cmd: 'camera' });
  const lat = cam.lat;
  const lon = cam.lon;
  if (hasFlag(args, '--natural')) {
    await cmd(page, { cmd: 'v3_goto', hour: (await page.evaluate(() => window.__TC.hour)) + afterH });
  } else {
    // the god's rain over the village (the cell at the middle of the view); live on so the deck stands over it
    await cmd(page, { cmd: 'power', power: 'rain', cell: look.cell || Number(getFlag(args, '--cell', 903)) });
    await cmd(page, { cmd: 'v3_goto', hour: (await page.evaluate(() => window.__TC.hour)) + afterH });
  }
  report.where = { lat, lon };
  await cmd(page, { cmd: 'flush_view' });
  try {
    report.probe = await cmd(page, { cmd: 'clouds' }, 20_000);
    console.log('clouds at the villages: ' + JSON.stringify(report.probe.villages.map((v) => [v.name, v.cover && v.cover.toFixed(2), v.dark && v.dark.toFixed(2), v.base.toFixed(2), v.god, v.rain_now])));
  } catch (e) { console.log('(no clouds probe in this build)'); }
  const views = [
    ['01_planet', 0.16, 'the planet'],
    ['02_mid', 0.55, 'mid-altitude'],
    ['03_approach', 0.85, 'over the layer'],
    ['04_descent_a', 1.0, 'entering'],
    ['05_descent_b', 1.12, 'in the layer'],
    ['06_descent_c', 1.22, 'in the layer, lower'],
    ['07_descent_d', 1.32, 'coming out under it'],
    ['08_below', 1.45, 'below the deck'],
    ['09_village', 1.6, 'the village'],
  ];
  const only = getFlag(args, '--only', null);
  for (const [name, zoom, note] of views) {
    if (only && !only.split(',').some((o) => name.startsWith(o))) continue;
    await cmd(page, { cmd: 'camera', lat, lon, zoom });
    await shoot(page, name, note);
    if (hasFlag(args, '--no-frames')) continue;
    const on = await frameMs(page);
    await cmd(page, { cmd: 'ui', clouds: false });
    const off = await frameMs(page);
    await cmd(page, { cmd: 'ui', clouds: true });
    report.frames.push({ name, zoom, ms_clouds: Math.round(on * 10) / 10, ms_none: Math.round(off * 10) / 10 });
    console.log(`  frame ms at zoom ${zoom}: clouds ${on.toFixed(1)}, none ${off.toFixed(1)}`);
  }
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'clouds.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
