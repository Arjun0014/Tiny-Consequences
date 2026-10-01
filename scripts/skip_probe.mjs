// node scripts/skip_probe.mjs [--release] [--no-build] [--hours 48] [--skips day,season]
// v3 catch-up (D-079): the sim's own cost in the browser (bridge v3_bench: ms per game hour with
// no view work), then real skips through the UI path (people hidden, the view still drawing the
// planet) with their wall time and game hours per real second.
import { chromium } from '@playwright/test';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8189));
const hours = Number(getFlag(args, '--hours', 48));
const skips = getFlag(args, '--skips', 'day,season').split(',').filter(Boolean);
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let n = 0;
async function cmd(page, c, timeout = 600_000) {
  const id = `s${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  const q = { speed: '0', gfx: 'low' };
  if (hasFlag(args, '--freeze')) q.freeze = '1';
  if (getFlag(args, '--world', null)) q.world = getFlag(args, '--world', null);   // assets/worlds/NAME.tcsave
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  const b = await cmd(page, { cmd: 'v3_bench', hours });
  console.log(`[${variant}] sim only: ${hours} h in ${b.ms.toFixed(0)} ms = ${b.ms_per_hour.toFixed(1)} ms per game hour (${(1000 / b.ms_per_hour).toFixed(1)} h/s; a season of 12 days ${(b.ms_per_hour * 288 / 1000).toFixed(1)} s), ${b.people} people`);
  if (hasFlag(args, '--nogc')) await cmd(page, { cmd: 'gc', op: 'stop' });
  for (const k of skips) {
    const kb0 = (await cmd(page, { cmd: 'gc' })).kb;
    const t0 = Date.now();
    const r = await cmd(page, { cmd: 'v3_skip', kind: k });
    const samples = [];
    for (;;) {
      const t = await page.evaluate(() => window.__TC);
      if (!t.skipping) break;
      samples.push(t);
      if (samples.length === 2) {
        const vs = await cmd(page, { cmd: 'view_stats' });
        console.log('   view parts during the skip (EMA ms): ' + Object.entries(vs.view_parts || {}).map(([k2, v]) => k2 + ' ' + Number(v).toFixed(1)).join(' | ') + ` | chunks rebuilt ${vs.chunks_rebuilt} rebuild max ${Number(vs.rebuild_ms_max || 0).toFixed(1)}`);
      }
      await page.waitForTimeout(1500);
    }
    for (const t of samples.slice(0, 6)) console.log(`   frame: gap ${t.frame_gap_ms.toFixed(0)} ms, main update ${t.frame_update_ms.toFixed(0)} ms, bridge ${t.frame_bridge_ms.toFixed(0)} ms, progress ${t.skip_progress}`);
    for (const t of samples.slice(1, 4)) console.log(`   during: fps ${t.fps.toFixed(1)} sim/frame avg ${t.sim_ms_avg.toFixed(1)} max ${t.sim_ms_max.toFixed(1)} view avg ${t.view_ms_avg.toFixed(1)} p90 ${t.view_ms_p90.toFixed(1)} progress ${t.skip_progress}`);
    const s = (Date.now() - t0) / 1000;
    const tc = await page.evaluate(() => window.__TC);
    await page.waitForTimeout(600);
    const sd = (await page.evaluate(() => window.__TC)).skip_done;
    if (sd) console.log(`   skip internals: ${sd.frames} frames, sim ${sd.sim_ms.toFixed(0)} ms of ${sd.ms.toFixed(0)} ms (${(sd.sim_ms / sd.frames).toFixed(1)} ms sim a frame, ${(sd.ms / sd.frames).toFixed(1)} ms a frame)`);
    const kb1 = (await cmd(page, { cmd: 'gc' })).kb;
    console.log(`   Lua heap ${kb0.toFixed(0)} -> ${kb1.toFixed(0)} KB`);
    console.log(`[${variant}] skip ${k}: ${(r.to - r.from).toFixed(1)} game h in ${s.toFixed(1)} s = ${((r.to - r.from) / s).toFixed(1)} h/s (fps during ${tc.fps.toFixed(1)})`);
  }
} catch (e) {
  console.error(e);
  process.exitCode = 1;
} finally {
  await browser.close();
  server.close();
}
