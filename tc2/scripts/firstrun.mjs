// TC2's first minutes played as a new player plays them (session 31): a clean browser profile at the game's plain address - the title, Begin clicked with the pointer, the guide
// walked by real pointing and clicking (a real person pointed at and clicked, their card and life, Why?, the book, a year skipped from orbit, a power cast at the ground), a picture
// at every step. Run after a Web build: node tc2/scripts/firstrun.mjs [--tag NAME] [--webgl2] [--seed N]. Checks: the guide reaches its end by the player's own hand; no console errors.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { guardBrowser } from './gpusafe.mjs';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const flag = (f) => a.includes(f);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const tag = opt('--tag', 'firstrun');
const W = Number(opt('--w', '1600')), H = Number(opt('--h', '900'));
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });
const report = { tag, steps: [], checks: [] };
const check = (name, ok, detail) => { report.checks.push({ name, ok, detail }); console.log((ok ? 'PASS ' : 'FAIL ') + name + (detail ? '  ' + detail : '')); };

const GPU = ['--mute-audio', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8646;
const server = await startServer({ port, quiet: true });
guardBrowser('firstrun');
const browser = await chromium.launch({ headless: true, channel: flag('--webgl2') ? undefined : 'chrome', args: GPU });
const context = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
const page = await context.newPage();
const lines = [];
page.on('console', (m) => lines.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => lines.push(`[pageerror] ${e.message}`));
const st = () => page.evaluate(() => window.__TC2 || null);
const wait = (ms) => page.waitForTimeout(ms);
const snap = async (name) => { const f = path.join(outDir, `${name}.png`); await page.screenshot({ path: f }); console.log('shot ' + name); };
const click = async (x, y) => { await page.mouse.move(x, y); await wait(140); await page.mouse.down(); await wait(90); await page.mouse.up(); await wait(120); };
const clickRect = async (r) => { if (r && r[2] > 0) await click(r[0] + r[2] / 2, r[1] + r[3] / 2); };
const until = async (fn, ms) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { const s = await st(); if (s && fn(s)) return s; await wait(250); } return null; };

try {
  await page.goto(`http://127.0.0.1:${port}/index.html${flag('--webgl2') ? '?gfx=webgl2' : ''}${opt('--seed') ? (flag('--webgl2') ? '&' : '?') + 'menu=0' : ''}`);
  await page.waitForFunction(() => (window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0) || window.__TC2_FATAL, null, { timeout: 300000, polling: 300 });
  await wait(6000);
  let s = await st();
  check('a plain address opens on the title', s.title === true, `title ${s.title}, speed ${s.speed}`);
  await snap('00_title');
  await page.mouse.move(s.begin[0] + 40, s.begin[1] + 20); await wait(800);
  await snap('01_title_hover');
  await clickRect(s.begin);
  await wait(1200);
  await snap('02_entering');
  s = await st();
  check('Begin enters the world and starts the guide', s.title === false && s.guide === 0, `title ${s.title}, guide ${s.guide}`);
  let last = -1, guard = 0;
  const seen = new Set();
  const t0 = Date.now();
  while (Date.now() - t0 < 420000 && guard++ < 400) {
    s = await st();
    if (!s || s.guide < 0) break;
    if (s.guide !== last) {
      last = s.guide;
      seen.add(last);
      await wait(last <= 3 ? 4500 : 1800);   // (the camera's flights)
      s = await st();
      if (s.guide !== last) continue;
      await snap(`step_${String(last).padStart(2, '0')}`);
      report.steps.push({ step: last, year: s.year, date: s.date, camAlt: s.camAlt, speed: s.speed });
    }
    switch (s.guide) {
      case 2:   // point at the person ringed
        if (s.gt[2] > 0) { await page.mouse.move(s.gt[0] + s.gt[2] / 2, s.gt[1] + s.gt[3] * 0.72); }
        await wait(400);
        break;
      case 3:   // click them, where they are now
        if (s.gt[2] > 0) await click(s.gt[0] + s.gt[2] / 2, s.gt[1] + s.gt[3] * 0.72);
        await wait(500);
        break;
      case 4:   // their card: their life
        await snap('step_04_card');
        await clickRect(s.gt);
        await wait(1500);
        break;
      case 7:   // the book's seal
        await clickRect(s.gt);
        await wait(1200);
        break;
      case 10: {   // a year skipped, from the card's own button; wait for it to come back
        await clickRect(s.gskip);
        await wait(3000);
        await snap('step_10_rising');
        await wait(6000);
        await snap('step_10_orbit');
        const back = await until((x) => x.guide !== 10, 240000);
        check('a year skipped from orbit brings the guide on', !!back, back ? `year ${back.year}` : 'timed out');
        await wait(1200);
        await snap('step_10_back');
        break;
      }
      case 11: {   // a power: the first seal, then the ground
        const b = s.power0;
        await clickRect(b);
        await wait(600);
        await page.mouse.move(W * 0.62, H * 0.45);
        await wait(900);
        await snap('step_11_aimed');
        await page.mouse.down(); await wait(90); await page.mouse.up();
        await wait(1200);
        await snap('step_11_cast');
        const on = await until((x) => x.guide !== 11, 8000);
        if (!on) await clickRect((await st()).gn);
        break;
      }
      default:
        await clickRect(s.gn);
        await wait(900);
        break;
    }
  }
  s = await st();
  check('the guide reaches its end by the player\'s own hand', s.guide < 0 || s.guide === 12, `last step ${last}, seen ${[...seen].join(',')}`);
  if (s.guide >= 0) { await clickRect(s.gn); await wait(1000); }
  await snap('99_after');
  s = await st();
  check('the guide has ended', s.guide < 0, `guide ${s.guide}`);
  // a reload continues the world and does not start the guide again
  await page.waitForTimeout(800);
  await page.goto(`http://127.0.0.1:${port}/index.html${flag('--webgl2') ? '?gfx=webgl2' : ''}`);
  await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0, null, { timeout: 300000, polling: 300 });
  await wait(5000);
  s = await st();
  await snap('99_continue');
  check('coming back: the title says Continue', s.title === true && s.continued === true, `title ${s.title}, continued ${s.continued}, year ${s.year}`);
  await clickRect(s.begin);
  await wait(1500);
  s = await st();
  check('the guide is not walked twice', s.title === false && s.guide < 0, `title ${s.title}, guide ${s.guide}`);
} catch (e) {
  check('no exception', false, String(e));
}
const errors = lines.filter((l) => l.startsWith('[error]') || l.startsWith('[pageerror]'));
check('no console errors', errors.length === 0, errors.slice(0, 4).join(' | '));
fs.writeFileSync(path.join(outDir, 'console.txt'), lines.join('\n'));
fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 1));
await browser.close();
server.close();
const failed = report.checks.filter((c) => !c.ok).length;
console.log(`${report.checks.length - failed}/${report.checks.length} checks passed`);
process.exit(failed ? 1 : 0);
