// The TC2 Web test (15 §1): load the build in headless Chromium on the real GPU (ANGLE D3D11; WebGPU enabled),
// check it starts without errors, compare the in-browser determinism probe with the headless one, take
// screenshots from orbit to village, save to IndexedDB, reload, come back "a day later" and watch the catch-up.
// Usage: node tc2/scripts/webtest.mjs [--webgl2] [--no-gpu] [--tag NAME] [--skip-away]
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const flag = (f) => a.includes(f);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const tag = opt('--tag', 'web');
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });
const report = { tag, started: new Date().toISOString(), checks: [], shots: [] };
const check = (name, ok, detail) => { report.checks.push({ name, ok, detail }); console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  ' + detail : '')); };

const GPU = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const SOFT = ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--enable-webgl'];

// reference probe from the headless runtime
const dotnet = 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/DotNetSdk/dotnet.exe';
const ref = spawnSync(dotnet, [path.join(root, 'tc2/headless/app/bin/Release/net8.0/tc2.dll'), 'hashtest'], { encoding: 'utf8' }).stdout.trim().split(/\r?\n/).pop();
report.reference = ref;

const port = 8642;
const server = await startServer({ port, quiet: true });
// WebGPU needs a browser with Dawn's shader compilers (installed Chrome); Playwright's Chromium lacks dxil.dll.
const useChrome = !flag('--webgl2') && !flag('--no-gpu') && !flag('--chromium');
const browser = await chromium.launch({ headless: true, channel: useChrome ? 'chrome' : undefined, args: flag('--no-gpu') ? SOFT : GPU });
report.browser = useChrome ? 'chrome' : 'chromium';
const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const consoleLines = [];
page.on('console', (m) => consoleLines.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => consoleLines.push(`[pageerror] ${e.message}`));
page.on('response', (r) => { if (r.status() >= 400) consoleLines.push(`[http ${r.status()}] ${r.url()}`); });

async function state() { return page.evaluate(() => window.__TC2 || null); }
async function cmd(c) { await page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c); }
async function waitFor(fn, arg, timeout = 180000) { return page.waitForFunction(fn, arg, { timeout, polling: 200 }); }
async function shot(name, settle = 1500) {
  await page.waitForTimeout(settle);
  const file = path.join(outDir, name + '.png');
  await page.screenshot({ path: file });
  report.shots.push(file);
  const st = await state();
  // frame rate and simulation cost at this view (the HUD's smoothed values)
  (report.perf = report.perf || []).push({ view: name, fps: st && st.fps, simMs: st && st.simMs });
  console.log('shot ' + file + (st ? `  fps ${st.fps} sim ${st.simMs} ms` : ''));
}

const base = `http://127.0.0.1:${port}/index.html`;
const gfxParam = flag('--webgl2') ? '&gfx=webgl2' : '';
const t0 = Date.now();
try {
  await page.goto(`${base}?fresh=1&seed=42&speed=3${gfxParam}`);
  await waitFor(() => (window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0) || window.__TC2_FATAL, null, 300000);
  if (await page.evaluate(() => window.__TC2_FATAL)) throw new Error('fatal: ' + await page.evaluate(() => window.__TC2_FATAL));
  const s0 = await state();
  report.loadSeconds = (Date.now() - t0) / 1000;
  const probe = await page.evaluate(() => window.__TC2_GFX || '');
  check('starts', true, `${report.loadSeconds.toFixed(1)} s, gfx ${s0.gfx} (probe: ${probe}), ${s0.people} people in ${s0.places} places`);
  report.gfxProbe = probe;
  report.gfx = s0.gfx;

  // determinism probe in the browser (IL2CPP -> wasm) vs headless (RyuJIT)
  await cmd('hashtest');
  await waitFor(() => window.__TC2 && window.__TC2.hashtest && window.__TC2.hashtest.length > 10, null, 300000);
  const web = (await state()).hashtest;
  report.webProbe = web;
  check('browser = headless (bit-identical probe)', web === ref, web === ref ? web : `web ${web} | ref ${ref}`);

  // views: orbit, region, village, close
  await cmd('village 0 22000'); await shot('1_orbit', 2500);
  await cmd('village 0 6000'); await shot('2_region', 2500);
  await cmd('village 0 1400'); await shot('3_village', 4500);   // (past the first second of a new view: its meshes and shaders come up)
  await cmd('village 0 380'); await shot('4_close', 4500);
  await cmd('village 0 95'); await shot('4b_low', 4500);
  await cmd('speed 5'); await page.waitForTimeout(4000);
  const s1 = await state();
  check('time runs at 1 h/s', s1.now > s0.now, `now ${s0.now} -> ${s1.now}, achieved ${s1.achieved}`);
  await shot('5_close_running', 500);

  // save to IndexedDB, reload, the same world continues
  await cmd('speed 0'); await page.waitForTimeout(800);
  await cmd('save');
  await waitFor(() => window.__TC2 && window.__TC2.saveOk === true, null, 60000);
  const before = await state();
  await page.goto(`${base}?speed=0${gfxParam}`);
  await waitFor(() => window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0, null, 300000);
  await page.waitForTimeout(1500);
  const after = await state();
  const sameWorld = after.seed === before.seed && after.now >= before.now && after.now - before.now < 4147200;
  check('reload continues the saved world', sameWorld, `seed ${before.seed}/${after.seed} now ${before.now} -> ${after.now} (catching up: ${after.catchingUp})`);

  if (!flag('--skip-away')) {
    // come back a day later (Living: ~96 world years), watch the catch-up
    await cmd('save');
    await page.waitForTimeout(1500);
    const tAway = Date.now();
    await page.goto(`${base}?speed=0&absence=24h${gfxParam}`);
    await waitFor(() => window.__TC2 && window.__TC2.ready === true, null, 300000);
    let sawCatchUp = false;
    for (let i = 0; i < 1200; i++) {
      const s = await state();
      if (s && s.catchingUp) sawCatchUp = true;
      if (i === 8) await shot('6_catchup', 0);
      if (s && sawCatchUp && !s.catchingUp) break;
      await page.waitForTimeout(500);
    }
    const back = await state();
    const secs = (Date.now() - tAway) / 1000;
    report.catchUpSeconds = secs;
    check('a day away catches up ~96 years', sawCatchUp && back.year >= 90, `year ${back.year}, ${back.people} people in ${back.places} places, ${secs.toFixed(0)} s`);
    await cmd('village 0 1400'); await shot('7_after_a_century', 2500);
    // the History Book of the century lived while away (D2-077)
    await cmd('book'); await shot('8_book', 3000);
    report.afterAway = back;
  }
} catch (e) {
  check('no exception in the test', false, String(e));
}
const errors = consoleLines.filter((l) => l.startsWith('[error]') || l.startsWith('[pageerror]'));
check('no console errors', errors.length === 0, errors.slice(0, 5).join(' | '));
fs.writeFileSync(path.join(outDir, 'console.txt'), consoleLines.join('\n'));
fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 2));
await browser.close();
server.close();
const failed = report.checks.filter((c) => !c.ok).length;
console.log(`${report.checks.length - failed}/${report.checks.length} checks passed; report ${path.join(outDir, 'report.json')}`);
process.exit(failed ? 1 : 0);
