// A high-end phone in landscape (session 32, Arjun: "mobile high end only ... the touch responsiveness"): the Web build in a touch screen's browser -
// 852 x 393 CSS pixels at three device pixels each, touch only. Checks by real touch events (CDP): the world comes up; the canvas draws at most twice its
// CSS pixels; two fingers spread zoom in and pinch zoom out; one finger turns the world; a power chosen through the bridge is cast by a tap on the ground;
// in portrait the page asks to be turned. Pictures at each step. Run after a Web build: node tc2/scripts/mobiletest.mjs [--tag NAME]
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { guardBrowser } from './gpusafe.mjs';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const tag = opt('--tag', 'mobile');
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });
const checks = [];
const check = (name, ok, detail) => { checks.push({ name, ok, detail }); console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  ' + detail : '')); };
const GPU = ['--mute-audio', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8648;
const server = await startServer({ port, quiet: true });
guardBrowser('mobiletest');
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: GPU });
const W = 852, H = 393;
try {
  const context = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 3, isMobile: true, hasTouch: true });
  const page = await context.newPage();
  const lines = [];
  page.on('console', (m) => lines.push(`[${m.type()}] ${m.text()}`));
  page.on('pageerror', (e) => lines.push(`[pageerror] ${e.message}`));
  const wait = (ms) => page.waitForTimeout(ms);
  const st = () => page.evaluate(() => window.__TC2 || null);
  const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
  const snap = async (n) => { await page.screenshot({ path: path.join(outDir, n + '.png') }); console.log('shot ' + n); };
  const cdp = await context.newCDPSession(page);
  const touch = (type, pts) => cdp.send('Input.dispatchTouchEvent', { type, touchPoints: pts.map((p, i) => ({ x: p[0], y: p[1], id: i, radiusX: 4, radiusY: 4, force: 1 })) });
  async function gesture(from, to, steps = 14, ms = 18) {
    // fingers from their starting points to their ends (one or two), a frame or so a step
    await touch('touchStart', from); await wait(60);
    for (let k = 1; k <= steps; k++) { const t = k / steps; await touch('touchMove', from.map((p, i) => [p[0] + (to[i][0] - p[0]) * t, p[1] + (to[i][1] - p[1]) * t])); await wait(ms); }
    await wait(60); await touch('touchEnd', []); await wait(400);
  }
  await page.goto(`http://127.0.0.1:${port}/index.html?test=1`);
  const t0 = Date.now(); let s = null;
  while (Date.now() - t0 < 150000) { s = await st(); if (s && s.ready && s.people > 0) break; await wait(500); }
  check('the world comes up on a phone', !!(s && s.ready && s.people > 0), s ? `${s.people} people, gfx ${s.gfx}` : 'never ready');
  const canvas = await page.evaluate(() => { const c = document.querySelector('#tc2-canvas'); return { w: c.width, h: c.height, dpr: window.devicePixelRatio, cap: window.tc2Dpr, touch: window.tc2Touch }; });
  check('the canvas draws at most twice its CSS pixels', canvas.w <= W * 2 + 2 && canvas.touch === true, `canvas ${canvas.w}x${canvas.h}, device ratio ${canvas.dpr}, drawn at ${canvas.cap}`);
  await cmd('title off'); await cmd('guide -1'); await cmd('speed 1'); await cmd('home big 600'); await wait(5000);
  await snap('00_landscape');
  s = await st(); const alt0 = s.camAlt;
  await gesture([[W / 2 - 40, H / 2], [W / 2 + 40, H / 2]], [[W / 2 - 190, H / 2], [W / 2 + 190, H / 2]]);
  await wait(800); s = await st(); const alt1 = s.camAlt;
  check('two fingers spread zoom in', alt1 < alt0 * 0.8, `height ${alt0} -> ${alt1} m`);
  await snap('01_spread');
  await gesture([[W / 2 - 190, H / 2], [W / 2 + 190, H / 2]], [[W / 2 - 50, H / 2], [W / 2 + 50, H / 2]]);
  await wait(800); s = await st(); const alt2 = s.camAlt;
  check('two fingers pinched zoom out', alt2 > alt1 * 1.3, `height ${alt1} -> ${alt2} m`);
  const bx0 = s.bigX, by0 = s.bigY;
  await gesture([[W * 0.35, H * 0.45]], [[W * 0.62, H * 0.52]], 16, 22);
  await wait(900); s = await st();
  const moved = Math.hypot((s.bigX - bx0), (s.bigY - by0));
  check('one finger turns the world', moved > 20, `the greatest place moved ${moved.toFixed(0)} px on the screen`);
  await snap('02_dragged');
  const casts0 = s.casts;
  await cmd('aim 0'); await wait(600);
  await touch('touchStart', [[W / 2, H / 2 - 20]]); await wait(90); await touch('touchEnd', []); await wait(2500);
  s = await st();
  check('a tap on the ground casts the chosen power', s.casts === casts0 + 1, `casts ${casts0} -> ${s.casts}`);
  await snap('03_cast');
  const errors = lines.filter((l) => l.startsWith('[error]') || l.startsWith('[pageerror]'));
  check('no console errors', errors.length === 0, errors.slice(0, 3).join(' | '));
  await page.setViewportSize({ width: H, height: W }); await wait(1200);
  const rot = await page.evaluate(() => getComputedStyle(document.querySelector('#tc2-rotate')).display);
  check('in portrait the page asks to be turned', rot === 'flex', `#tc2-rotate ${rot}`);
  await snap('04_portrait');
  fs.writeFileSync(path.join(outDir, 'console.txt'), lines.join('\n'));
} finally {
  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify({ tag, checks }, null, 1));
  await browser.close(); server.close();
  const ok = checks.filter((c) => c.ok).length;
  console.log(`${ok}/${checks.length} checks passed`);
  process.exitCode = ok === checks.length ? 0 : 1;
}
