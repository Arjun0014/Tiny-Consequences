// (session 25, fourth context) What the Web build keeps in its store as the Web test's reload goes: the stored save's header (world time, saved real time,
// offline mode, catch-up target - SaveFile.cs: world time i64 at byte 18, saved real time i64 at 26, offline byte at 34, catch-up target i64 at 35) read
// from IndexedDB after the start, after a fast run, after an explicit save and its answer, a moment later, and after a reload; with the game's [TC2] lines.
// Usage: node tc2/scripts/saveprobe.mjs [--burst 6] [--secs 3]
import { chromium } from '@playwright/test';
import { startServer } from './serve.mjs';

const a = process.argv.slice(2);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const burst = opt('--burst', '6'), secs = Number(opt('--secs', '3'));
const GPU = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8644;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, channel: 'chrome', args: GPU });
const page = await (await browser.newContext({ viewport: { width: 1280, height: 720 } })).newPage();
const lines = [];
page.on('console', (m) => { const t = m.text(); if (t.includes('[TC2]')) lines.push(t); });
const base = `http://127.0.0.1:${port}/index.html`;
const state = () => page.evaluate(() => window.__TC2 || null);
const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
const stored = () => page.evaluate(() => new Promise((res) => {
  try {
    const rq = indexedDB.open('tiny-consequences-2', 1);
    rq.onerror = () => res('open error');
    rq.onsuccess = () => {
      const g = rq.result.transaction('saves', 'readonly').objectStore('saves').get('world');
      g.onerror = () => { rq.result.close(); res('get error'); };
      g.onsuccess = () => {
        const v = g.result; rq.result.close();
        if (!v) { res('none'); return; }
        const u = v instanceof Uint8Array ? v : new Uint8Array(v);
        const dv = new DataView(u.buffer, u.byteOffset, u.byteLength);
        res(`${u.byteLength} B, world ${dv.getBigInt64(18, true)}, saved ${dv.getBigInt64(26, true)}, offline ${dv.getUint8(34)}, catch-up to ${dv.getBigInt64(35, true)}`);
      };
    };
  } catch (e) { res('error ' + e); }
}));
const log = async (what) => { const s = await state(); console.log(`${what}: now ${s && s.now} saveOk ${s && s.saveOk} catchingUp ${s && s.catchingUp} | stored ${await stored()}`); };

await page.goto(`${base}?fresh=1&seed=42&speed=3`);
await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0, null, { timeout: 300000, polling: 200 });
await page.waitForTimeout(2000);
await log('started');
if (a.includes('--cast')) {
  // (as the Web test casts: lightning aimed and a real click on the planet; then a save)
  await cmd('closepages'); await cmd('village 0 6000'); await page.waitForTimeout(1500);
  await cmd('aim 0'); await page.waitForTimeout(400);
  const cv = await page.locator('canvas').first().boundingBox();
  await page.mouse.move(cv.x + cv.width * 0.5, cv.y + cv.height * 0.55); await page.waitForTimeout(700);
  await page.mouse.down(); await page.waitForTimeout(150); await page.mouse.up(); await page.waitForTimeout(1500);
  const sc = await state(); console.log('cast: casts ' + (sc && sc.casts));
  await cmd('save'); await page.waitForTimeout(1500);
  await log('after a save after the cast');
}
await cmd('speed ' + burst); await page.waitForTimeout(secs * 1000); await cmd('speed 0'); await page.waitForTimeout(800);
await log('after the fast run');
const saves0 = (await state()).saves || 0;   // (as the Web test waits: one more save stored, not the stale saveOk of the save before)
await cmd('save');
await page.waitForFunction((n) => window.__TC2 && window.__TC2.saves > n && window.__TC2.saveOk === true, saves0, { timeout: 60000, polling: 100 }).catch(() => console.log('no answer in 60 s'));
await log('after the save and its answer');
await page.waitForTimeout(2000);
await log('two seconds later');
await page.goto(`${base}?speed=0`);
await page.waitForFunction(() => window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0, null, { timeout: 300000, polling: 200 });
await page.waitForTimeout(1500);
await log('after the reload');
console.log(lines.join('\n'));
await browser.close(); server.close();
