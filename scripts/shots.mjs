// npm run shots — screenshot protocol (07_TESTING.md §7), Chapter-1 items:
//   2. full planet at year 0 from 3 presets (continent A, continent B, a pole)
//   3. mid zoom on a village (a coastal lowland cell until villages exist), close zoom
//   4. the same after 50 untouched years (run_ticks 2400)
// Builds if stale, serves the bundle on a private port, drives the game through the test
// bridge in headless Chromium (SwiftShader WebGL) with ?freeze=1, and writes
// out/shots/latest/<year>_<preset>.png plus shots.json (bridge numbers per shot + console).
// Flags: --release, --no-build, --port N (default 8182), --seed N (default: the pre-spun default
//        world the player sees), --years N (default 50), --only a,b (preset subset),
//        --no-later (skip the year-N set), --preview (the pre-1.7 view-only village art preview),
//        --hour H (local hour at the focus for each shot, default 13; 'none' = as is)
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, die, getFlag, hasFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8182));
const seedFlag = getFlag(args, '--seed', null);
const seed = seedFlag === null ? 'default world' : String(seedFlag);
const years = Number(getFlag(args, '--years', 50));
const PRESETS = (getFlag(args, '--only', 'continent_a,continent_b,pole,village,close')).split(',');
const UI_SHOTS = !hasFlag(args, '--no-ui');
// --hour H: before each shot, run the sim to local solar hour H at the view focus (default 13,
// daylight for review; --hour none keeps the world's clock)
const HOUR_FLAG = getFlag(args, '--hour', '13');
const HOUR = HOUR_FLAG === 'none' ? null : Number(HOUR_FLAG);
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });

const outDir = path.join(ROOT, 'out', 'shots', 'latest');
fs.mkdirSync(outDir, { recursive: true });
for (const f of fs.readdirSync(outDir)) if (f.endsWith('.png')) fs.rmSync(path.join(outDir, f));

const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let failed = null;
const report = { seed, variant, shots: [], console: [] };
let nextId = 1;

async function cmd(page, c, timeout = 30_000) {
  const id = `s${nextId++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
  return r.result;
}

async function settle(page) {
  // finish LOD/dirty rebuilds synchronously, then let a few frames and a publish happen
  await page.waitForTimeout(300);
  await cmd(page, { cmd: 'flush_view' }, 120_000);
  await page.waitForFunction(() => window.__TC && window.__TC.chunks_dirty === 0, null, { timeout: 120_000, polling: 200 });
  await page.waitForTimeout(900);
}

async function shoot(page, label, preset, name = null) {
  const cam = preset ? await cmd(page, { cmd: 'camera', preset }) : null;
  if (HOUR !== null && preset) await cmd(page, { cmd: 'run_hours', to_local: HOUR }, 300_000);
  await settle(page);
  const file = path.join(outDir, name ? `${name}.png` : `${label}_${preset}.png`);
  await page.screenshot({ path: file });
  const tc = await page.evaluate(() => window.__TC);
  const gl = await page.evaluate(() => (window.__TC_GL ? window.__TC_GL.samples.slice(-10) : []));
  const glDraws = gl.length ? Math.max(...gl) : null;
  const entry = {
    file: rel(file), preset, label, cam, tick: tc.tick, year: tc.year,
    view_ms: tc.view_ms, view_ms_p90: tc.view_ms_p90, view_ms_max: tc.view_ms_max, view_parts: tc.view_parts, lua_kb: tc.lua_kb, prop_count: tc.prop_count, prop_total: tc.prop_total, prop_verts: tc.prop_verts,
    draw_calls: tc.draw_calls, gl_draw_calls: glDraws, chunks_visible: tc.chunks_visible, chunks_rebuilt: tc.chunks_rebuilt,
    clouds: tc.clouds, cloud_cover: tc.cloud_cover, fires: tc.fires, errors: tc.errors.length,
  };
  report.shots.push(entry);
  console.log(`shots: ${entry.file}  year ${tc.year}  props ${tc.prop_count}/${tc.prop_total}  draws ~${tc.draw_calls} (gl ${glDraws})  view med ${Number(tc.view_ms).toFixed(2)} p90 ${Number(tc.view_ms_p90).toFixed(2)} max ${Number(tc.view_ms_max).toFixed(1)} ms (avg ${Object.entries(tc.view_parts || {}).map(([k, v]) => `${k} ${Number(v).toFixed(2)}`).join(', ')})`);
}

try {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  // Count real WebGL draw calls per animation frame (the bridge's draw_calls is an estimate).
  await page.addInitScript(() => {
    let cur = 0;
    const samples = [];
    window.__TC_GL = { samples };
    const names = ['drawArrays', 'drawElements', 'drawArraysInstanced', 'drawElementsInstanced'];
    for (const P of [window.WebGL2RenderingContext, window.WebGLRenderingContext]) {
      if (!P) continue;
      for (const n of names) {
        const f = P.prototype[n];
        if (f) P.prototype[n] = function (...a) { cur++; return f.apply(this, a); };
      }
    }
    const tick = () => { if (cur > 0) { samples.push(cur); if (samples.length > 30) samples.shift(); } cur = 0; requestAnimationFrame(tick); };
    requestAnimationFrame(tick);
  });
  page.on('console', (m) => report.console.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => report.console.push(`[pageerror] ${e.message}`));
  const t0 = Date.now();
  const q = { freeze: '1', speed: '0', world: 'v2' };   // the legacy v2 world (D-079); --world overrides
  if (seedFlag !== null) q.seed = String(seedFlag);
  const worldFlag = getFlag(args, '--world', null);
  if (worldFlag !== null) q.world = String(worldFlag);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.loading === true && window.__TC.loading_frac > 0.2, null, { timeout: 60_000 }).catch(() => {});
  await page.screenshot({ path: path.join(outDir, 'loading.png') });
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  report.ready_ms = Date.now() - t0;
  console.log(`shots: world ready in ${report.ready_ms} ms`);
  await cmd(page, { cmd: 'ui', debug: false });
  await cmd(page, { cmd: 'select', none: true });
  for (const p of PRESETS) await shoot(page, 'y000', p);
  if (UI_SHOTS) {
    // 07 §7 item 8: HUD at 1280x720 and 1920x1080 (planet inspector open), item 7: inspector
    // on a person; plus village and cell inspectors and the debug overlay.
    await cmd(page, { cmd: 'select', planet: true });
    await page.mouse.move(520, 41);   // hover a HUD item: shows its tooltip + hover state
    await shoot(page, 'ui', 'default', 'hud_1280x720');
    await page.mouse.move(640, 700);
    await page.setViewportSize({ width: 1920, height: 1080 });
    await page.waitForTimeout(1500);
    await shoot(page, 'ui', 'default', 'hud_1920x1080');
    await page.setViewportSize(VIEWPORT);
    await page.waitForTimeout(1500);
    const tc0 = await page.evaluate(() => window.__TC);
    if (tc0.villages > 0) {
      await cmd(page, { cmd: 'select', village: 'first' });
      await cmd(page, { cmd: 'camera', preset: 'village' });
      await cmd(page, { cmd: 'select', village: 'first', fly: true, zoom: 0.62 });
      await shoot(page, 'ui', null, 'inspector_village');
      await cmd(page, { cmd: 'select', person: 'first', fly: true, zoom: 0.95 });
      await page.waitForTimeout(2500);
      await shoot(page, 'ui', null, 'inspector_person');
    }
    await cmd(page, { cmd: 'camera', preset: 'default' });
    await cmd(page, { cmd: 'pick', x: 560, y: 330 });
    await shoot(page, 'ui', null, 'inspector_cell');
    await cmd(page, { cmd: 'select', none: true });
    await cmd(page, { cmd: 'ui', debug: true });
    await shoot(page, 'ui', 'default', 'debug_overlay');
    await cmd(page, { cmd: 'ui', debug: false });
  }
  if (!hasFlag(args, '--no-later') && years > 0) {
    const t1 = Date.now();
    await cmd(page, { cmd: 'run_ticks', n: years * 48 });
    await page.waitForFunction(() => window.__TC && window.__TC.ff_remaining === 0, null, { timeout: 900_000, polling: 500 });
    report.run_ticks_ms = Date.now() - t1;
    console.log(`shots: ran ${years} years in ${report.run_ticks_ms} ms`);
    await page.waitForTimeout(1500);
    const label = `y${String(years).padStart(3, '0')}`;
    for (const p of PRESETS) await shoot(page, label, p);
  }
  if (hasFlag(args, '--preview')) {
    // Art preview only: a view-side village + farms (no sim villages exist before Stage 1.7).
    await cmd(page, { cmd: 'preview_village' });
    await page.waitForTimeout(1200);
    await shoot(page, 'preview', 'village');
    await shoot(page, 'preview', 'close');
    await cmd(page, { cmd: 'preview_village', off: true });
  }
  const tc = await page.evaluate(() => window.__TC);
  report.final = tc;
  if (tc.errors.length) failed = new Error(`bridge errors: ${tc.errors.join(' | ')}`);
} catch (e) {
  failed = e;
} finally {
  fs.writeFileSync(path.join(outDir, 'shots.json'), JSON.stringify(report, null, 2) + '\n');
  await browser.close();
  server.close();
}
const errs = report.console.filter((l) => l.startsWith('[error]') || l.startsWith('[pageerror]'));
if (errs.length) console.log(`console errors:\n  ${errs.join('\n  ')}`);
if (failed) die(`shots failed: ${failed.message}`);
