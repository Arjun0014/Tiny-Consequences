// The village's sound measured (S23, D-181; docs/v3/80_WORLD_AUDIO.md §9): the mixer (game/audio/audio.lua)
// runs as in play while the world lives at the watch speed; at each listening place it reports what it
// played - the one-shots' energy by category (sum of gain^2: hazard, work, village, beasts, dogs, wild) and
// the beds' gains. The hierarchy wanted close in: danger > work, water, weather > the village's hum > the
// herds as texture. Headless: nothing is heard, everything the mixer decides is counted.
//   node scripts/play_audio.mjs [--no-build] [--port 8199] [--secs 45]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8199));
const secs = Number(getFlag(args, '--secs', 45));
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'audio');
fs.mkdirSync(outDir, { recursive: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = { places: [] };
let nextId = 1;
async function cmd(page, c, timeout = 300_000) {
  const id = `u${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  // (gfx=low: the mixer does not depend on what is drawn; the CPU rasteriser would slow the clock)
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd(page, { cmd: 'ui', debug: false });
  const places = [
    { name: 'the flock, close in (1.5)', look: { sett: 1, what: 'flock', zoom: 1.5 }, lh: 10 },
    { name: 'the flock, right among it (1.75)', look: { sett: 1, what: 'flock', zoom: 1.75 }, lh: 10.5 },
    { name: 'the village square (1.45)', look: { sett: 1, what: 'square', zoom: 1.45 }, lh: 11 },
    { name: 'the village, mid zoom (1.1)', look: { sett: 1, what: 'square', zoom: 1.1 }, lh: 11.5 },
  ];
  for (const pl of places) {
    await cmd(page, { cmd: 'set_speed', n: 0 });
    await cmd(page, { cmd: 'v3_goto', lh: pl.lh, days: 1 });
    await cmd(page, { cmd: 'v3_look', ...pl.look });
    await page.waitForTimeout(1500);
    await cmd(page, { cmd: 'audio', reset: true });
    await cmd(page, { cmd: 'set_speed', n: 1 });
    await page.waitForTimeout(secs * 1000);
    const a = await cmd(page, { cmd: 'audio' });
    await cmd(page, { cmd: 'set_speed', n: 0 });
    const E = a.energy || {};
    const tot = Object.values(E).reduce((x, y) => x + y, 0) || 1;
    const share = Object.fromEntries(Object.entries(E).map(([k, v]) => [k, Math.round((100 * v) / tot)]));
    report.places.push({ place: pl.name, shots: a.shots, energy: E, share, beds: a.beds });
    console.log(`${pl.name}: ${a.shots} one-shots in ${secs}s; energy share % ${JSON.stringify(share)}`);
    console.log(`  beds ${JSON.stringify(a.beds)}`);
  }
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'play_audio.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
