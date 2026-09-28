// npm run perf:probe [-- --release --no-build --seconds 10 --speed 4 --viewport 640x360 --all --world NAME --gfx low]
// Plays the default world at a speed and reports the in-game perf window (bridge `perf`) under
// three conditions: normal, Lua GC stopped for the window, and visuals frozen. Separates sim,
// view and GC costs when W08 fails (headless WebGL is CPU-rendered: judge ms, not fps).
import { chromium } from '@playwright/test';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8184));
const seconds = Number(getFlag(args, '--seconds', 10));
const speed = Number(getFlag(args, '--speed', 4));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let n = 0;
async function cmd(page, c) {
  const id = `p${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout: 120_000, polling: 50 });
  return (await h.jsonValue()).result;
}
const fmt = (r) => `tick med ${r.tick_median.toFixed(2)} p95 ${r.tick_p95.toFixed(1)} max ${r.tick_max.toFixed(1)} | sim/frame ${r.sim_ms_avg.toFixed(1)} | view avg ${r.view_ms_avg.toFixed(1)} p90 ${r.view_ms_p90.toFixed(1)} max ${r.view_ms_max.toFixed(1)} | ${r.ticks_per_s.toFixed(1)} ticks/s, ${r.frames} frames, chunks ${r.chunks_rebuilt}`;
try {
  const vw = getFlag(args, '--viewport', null);
  const page = await browser.newPage({ viewport: vw ? { width: Number(vw.split('x')[0]), height: Number(vw.split('x')[1]) } : VIEWPORT });
  const q = { speed: '0' };
  const wname = getFlag(args, '--world', null);
  if (wname) q.world = wname;
  const gfx = getFlag(args, '--gfx', null);
  if (gfx) q.gfx = gfx;
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 60_000 });
  const modes = hasFlag(args, '--all') ? ['normal', 'gc-stopped', 'frozen-view'] : ['normal'];
  for (const mode of modes) {
    if (mode === 'gc-stopped') await cmd(page, { cmd: 'gc', op: 'stop' });
    if (mode === 'frozen-view') { await cmd(page, { cmd: 'gc', op: 'restart' }); await cmd(page, { cmd: 'freeze_visuals', on: true }); }
    const kb0 = (await cmd(page, { cmd: 'gc' })).kb;
    await cmd(page, { cmd: 'set_speed', n: speed });
    await page.waitForTimeout(1000);
    await cmd(page, { cmd: 'perf', reset: true });
    await page.waitForTimeout(seconds * 1000);
    const r = await cmd(page, { cmd: 'perf' });
    await cmd(page, { cmd: 'set_speed', n: 0 });
    const kb1 = (await cmd(page, { cmd: 'gc' })).kb;
    const vs = await cmd(page, { cmd: 'view_stats' });
    const tc = await page.evaluate(() => window.__TC);
    const vp = vs.view_parts || {};
    console.log('   view parts (EMA ms): ' + Object.entries(vp).map(([k, v]) => k + ' ' + Number(v).toFixed(2)).join(' | ') + ' | rebuild max ' + Number(vs.rebuild_ms_max || 0).toFixed(1) + ' | phases ' + JSON.stringify(vs.props_phase_max) + ' | recolours ' + vs.recolours + ' | puppets ' + vs.puppets + ' clouds ' + vs.clouds);
    console.log(`[${variant}] ${mode.padEnd(11)} ${fmt(r)} | heap ${kb0.toFixed(0)} -> ${kb1.toFixed(0)} KB | fps ${tc.fps} year ${tc.year}`);
  }
} catch (e) {
  console.error(e);
  process.exitCode = 1;
} finally {
  await browser.close();
  server.close();
}
