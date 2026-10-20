// What the realms map layer (D2-167) costs a frame in the Web build, on a world that has realms: a save (a headless run's, of this build's sim
// version) is put into the game's IndexedDB store as saved a moment ago with the offline mode paused - so the game loads it as it was, owing no
// absence - from a page of the same origin before the game starts (the game saves on pagehide and on a timer: injected while it runs, its own save
// could win); then at each view the frames the browser draws are counted (requestAnimationFrame over a window) with the layer off and on, in
// alternating pairs, so the machine's load drifts across both alike. The paired ratio is the measure; the absolute rate is the machine's.
// Usage: node tc2/scripts/realmsperf.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,orbit:22000,village:1400] [--pairs 5]
//        [--settle 2500] [--window 2500] [--toggles N] [--tag NAME] [--webgl2]
// The wasm heap (MB; it only grows) is traced after the load, at each view and pair: --pairs 0 takes the views with the layer off only (what the
// world and its views hold); --toggles N first turns the layer on and off N times at the first view (does turning it leak?).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from '@playwright/test';
import { startServer } from './serve.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const a = process.argv.slice(2);
const flag = (f) => a.includes(f);
const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
const savePath = opt('--save');
const lat = Number(opt('--lat')), lon = Number(opt('--lon'));
if (!savePath || !fs.existsSync(savePath) || !Number.isFinite(lat) || !Number.isFinite(lon)) {
  console.error('usage: node tc2/scripts/realmsperf.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,...] [--pairs 5] [--tag NAME]');
  process.exit(2);
}
const views = opt('--views', 'region:6000,orbit:22000,village:1400').split(',').map((v) => { const [n, d] = v.split(':'); return { name: n, dist: Number(d) }; });
const pairs = Number(opt('--pairs', '5'));
const toggles = Number(opt('--toggles', '0'));
const settle = Number(opt('--settle', '2500'));
const windowMs = Number(opt('--window', '2500'));
const tag = opt('--tag', 'realmsperf');
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });

// the save as saved a moment ago, paused: header (SaveFile.cs) - magic u32, format u16, sim i32, seed u64, world time i64, saved real time i64 at
// byte 26, offline mode byte at 34 (0 paused), catch-up target i64 at 35 (0 none); the payload's hash does not cover the header
const save = Buffer.from(fs.readFileSync(savePath));
if (save.readUInt32LE(0) !== 0x57324354) { console.error('not a TC2 world save: ' + savePath); process.exit(2); }
const simVersion = save.readInt32LE(6), seed = save.readBigUInt64LE(10);
save.writeBigInt64LE(BigInt(Date.now()), 26);
save.writeUInt8(0, 34);
save.writeBigInt64LE(0n, 35);

// (served from the build's folder for the moment it takes the page to fetch it: the store is the origin's)
const webDir = path.join(root, 'tc2', 'web-build', 'web');
const servedSave = path.join(webDir, '_realmsperf_world.tc2');
fs.writeFileSync(servedSave, save);

const GPU = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8643;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, channel: flag('--webgl2') ? undefined : 'chrome', args: GPU });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const consoleLines = [];
page.on('console', (m) => consoleLines.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => consoleLines.push(`[pageerror] ${e.message}`));
const origin = `http://127.0.0.1:${port}`;
const report = { tag, save: savePath, simVersion, seed: seed.toString(), lat, lon, pairs, settle, windowMs, views: [] };
const median = (v) => { const s = [...v].sort((x, y) => x - y); const n = s.length; return n ? (n % 2 ? s[(n - 1) / 2] : (s[n / 2 - 1] + s[n / 2]) / 2) : NaN; };
try {
  // the save into the store, from a page of the same origin with no game running
  await page.goto(`${origin}/inject-a-save`);
  const put = await page.evaluate(async (url) => {
    const buf = await (await fetch(url)).arrayBuffer();
    return await new Promise((res) => {
      const rq = indexedDB.open('tiny-consequences-2', 1);
      rq.onupgradeneeded = () => rq.result.createObjectStore('saves');
      rq.onerror = () => res('open failed');
      rq.onsuccess = () => {
        const tx = rq.result.transaction('saves', 'readwrite');
        tx.objectStore('saves').put(new Uint8Array(buf), 'world');
        tx.oncomplete = () => { rq.result.close(); res('ok ' + buf.byteLength); };
        tx.onerror = () => { rq.result.close(); res('put failed'); };
      };
    });
  }, '/_realmsperf_world.tc2');
  if (!put.startsWith('ok')) throw new Error('the save could not be put into the store: ' + put);
  console.log(`the save put into the store (${put.slice(3)} bytes; sim ${simVersion})`);
  fs.rmSync(servedSave, { force: true });
  await page.goto(`${origin}/index.html?speed=0${flag('--webgl2') ? '&gfx=webgl2' : ''}`);
  await page.waitForFunction(() => (window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0) || window.__TC2_FATAL, null, { timeout: 300000, polling: 200 });
  const s0 = await page.evaluate(() => window.__TC2);
  console.log(`loaded: seed ${s0.seed} (save ${seed}), year ${s0.year}, ${s0.people} people in ${s0.places} places, gfx ${s0.gfx}; catching up ${s0.catchingUp}`);
  if (String(s0.seed) !== seed.toString() || s0.catchingUp) throw new Error('the game did not load the save as saved');
  report.loaded = { year: s0.year, people: s0.people, places: s0.places, gfx: s0.gfx };
  const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
  // the wasm heap in MB (Unity's instance; it grows and never shrinks), and whether the page has failed
  const heapMb = () => page.evaluate(() => { try { const M = window.__TC2_INSTANCE && window.__TC2_INSTANCE.Module; const b = M && (M.HEAPU8 || M.HEAP8 || M.HEAPU32 || M.HEAP32 || M.HEAPF32); return b ? Math.round(b.buffer.byteLength / 1048576) : -1; } catch (e) { return -1; } });
  const failed = () => consoleLines.some((l) => l.startsWith('[pageerror]') || l.includes('abort("OOM")'));
  report.heap = [{ at: 'loaded', mb: await heapMb() }];
  const traceHeap = async (at) => { const mb = await heapMb(); report.heap.push({ at, mb }); console.log(`heap ${mb} MB (${at})${failed() ? ' - THE PAGE HAS FAILED' : ''}`); };
  await traceHeap('loaded');
  if (toggles > 0 && views.length > 0) {
    // the layer turned on and off at the first view, as a player pressing R
    await cmd(`look ${lat} ${lon} ${views[0].dist}`);
    await page.waitForTimeout(settle * 2);
    await traceHeap(`${views[0].name}, before turning`);
    for (let i = 1; i <= toggles; i++) {
      await cmd('on realms'); await page.waitForTimeout(1000);
      await cmd('off realms'); await page.waitForTimeout(1000);
      if (i % 5 === 0 || i === toggles) await traceHeap(`${views[0].name}, turned on and off ${i} times`);
    }
  }
  // frames the browser draws in a window (the game renders on the animation frame)
  const fps = () => page.evaluate((ms) => new Promise((res) => {
    let n = 0; const t0 = performance.now();
    const f = () => { n++; const t = performance.now() - t0; if (t < ms) requestAnimationFrame(f); else res(n / (t / 1000)); };
    requestAnimationFrame(f);
  }), windowMs);
  for (const v of views) {
    await cmd(`look ${lat} ${lon} ${v.dist}`);
    await cmd('off realms');
    await page.waitForTimeout(settle * 2);   // (the view's patches and meshes come up)
    await traceHeap(`${v.name}, the layer off`);
    if (pairs <= 0) {
      report.views.push({ view: v.name, dist: v.dist, fpsOff: await fps() });
      await page.screenshot({ path: path.join(outDir, `${v.name}_plain.png`) });
      continue;
    }
    const off = [], on = [], ratio = [];
    for (let i = 0; i < pairs; i++) {
      // alternate which goes first, so a drift in the machine's load falls on both alike
      const order = i % 2 === 0 ? ['off', 'on'] : ['on', 'off'];
      const got = {};
      for (const o of order) {
        await cmd(`${o} realms`);
        await page.waitForTimeout(settle);
        got[o] = await fps();
      }
      off.push(got.off); on.push(got.on); ratio.push(got.on / got.off);
      console.log(`${v.name} pair ${i + 1}: off ${got.off.toFixed(1)} fps, on ${got.on.toFixed(1)} fps (x${(got.on / got.off).toFixed(3)})`);
      await traceHeap(`${v.name}, pair ${i + 1}`);
      if (failed()) throw new Error('the page failed (see console.txt) - the frame counts after it are not the game\'s');
    }
    await cmd('on realms'); await page.waitForTimeout(settle);
    await page.screenshot({ path: path.join(outDir, `${v.name}_realms.png`) });
    await cmd('off realms'); await page.waitForTimeout(settle);
    await page.screenshot({ path: path.join(outDir, `${v.name}_plain.png`) });
    const r = { view: v.name, dist: v.dist, off, on, medianOff: median(off), medianOn: median(on), medianRatio: median(ratio),
      msOff: 1000 / median(off), msOn: 1000 / median(on) };
    report.views.push(r);
    console.log(`== ${v.name}: off ${r.medianOff.toFixed(1)} fps (${r.msOff.toFixed(2)} ms), on ${r.medianOn.toFixed(1)} fps (${r.msOn.toFixed(2)} ms); paired ratio x${r.medianRatio.toFixed(3)}`);
  }
} catch (e) {
  report.error = String(e && e.message || e);
  console.error('FAILED: ' + report.error);
  process.exitCode = 1;
} finally {
  fs.rmSync(servedSave, { force: true });
  fs.writeFileSync(path.join(outDir, 'console.txt'), consoleLines.join('\n'));
  fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 1));
  await browser.close();
  server.close();
}
