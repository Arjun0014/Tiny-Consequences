// Aim previews (S23, D-184): each v3 power armed from the bar (a held press on its button) and the pointer moved over
// Tekheim - the pill above the bar says what the cast would act on there. Writes out/shots/aim/*.png and aim.json.
//   node scripts/shots_aim.mjs [--no-build] [--port 8217] [--powers 1,2,8,10,11,14]
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8217));
const which = String(getFlag(args, '--powers', '1,2,6,8,10,11,12,14')).split(',').map(Number);
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'aim');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const report = [];
let nextId = 1;
let failed = null;
try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  async function cmd(c, timeout = 300_000) {
    const id = `a${nextId++}`;
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
    const r = await h.jsonValue();
    if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
    return r.result;
  }
  const vp = page.viewportSize();
  const sx = vp.width / 1280, sy = vp.height / 720;
  async function press(x, y) { await page.mouse.move(x * sx, y * sy); await page.waitForTimeout(150); await page.mouse.down(); await page.waitForTimeout(150); await page.mouse.up(); await page.waitForTimeout(300); }
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  await cmd({ cmd: 'v3_goto', lh: 11, days: 2 });
  await cmd({ cmd: 'v3_look', sett: 1, what: 'square', zoom: 1.35 });
  await page.waitForTimeout(1500);
  // the power bar: 14 buttons of 54 px, 10 apart, the bar centred; button i at x 224 + 64 (i - 1), 51 up from the bottom
  for (const i of which) {
    const bx = 224 + 64 * (i - 1);
    await press(bx, 720 - 51);
    // over the village (a little off the square), moved in steps so the hover is seen
    for (const [x, y] of [[600, 380], [620, 390], [640, 400]]) { await page.mouse.move(x * sx, y * sy); await page.waitForTimeout(250); }
    await page.waitForTimeout(900);
    const file = path.join(outDir, `p${String(i).padStart(2, '0')}.png`);
    await page.screenshot({ path: file, timeout: 600_000 });
    console.log(`aim: ${rel(file)}`);
    report.push({ power: i, file: rel(file) });
    await press(bx, 720 - 51);   // (disarmed again)
  }
} catch (e) {
  failed = e;
  console.error(e);
} finally {
  fs.writeFileSync(path.join(outDir, 'aim.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
if (failed) process.exit(1);
