// Screenshots of the Web build's HUD on a great world (session 25): the places named on the map at several views, and the History Book open at
// chosen parts and chapters - what camera shots cannot show (the HUD is drawn over the frame). A save (a headless run's, of this build's sim
// version) is put into the game's IndexedDB store as saved a moment ago with the offline mode paused, as realmsperf.mjs does, then the game loads
// it; each part the book opens is written then (its time is in the console: "[TC2] book: part ...").
// Usage: node tc2/scripts/bookshots.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,orbit:22000,village:1400]
//        [--book "-1,0:0,1:0,2:0,3:0,4:0,5:0,6:0,7:0"] [--tag NAME] [--webgl2]
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
  console.error('usage: node tc2/scripts/bookshots.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,...] [--book "-1,0:0,..."] [--tag NAME]');
  process.exit(2);
}
const views = opt('--views', 'region:6000,orbit:22000,village:1400').split(',').filter((v) => v).map((v) => { const [n, d] = v.split(':'); return { name: n, dist: Number(d) }; });
const pages = opt('--book', '-1,0:0,1:0,2:0,3:0,4:0,5:0,6:0,7:0').split(',').filter((v) => v).map((v) => { const [p, c] = v.split(':'); return { part: Number(p), chapter: Number(c || 0) }; });
const tag = opt('--tag', 'bookshots');
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });

const save = Buffer.from(fs.readFileSync(savePath));
if (save.readUInt32LE(0) !== 0x57324354) { console.error('not a TC2 world save: ' + savePath); process.exit(2); }
const simVersion = save.readInt32LE(6), seed = save.readBigUInt64LE(10);
save.writeBigInt64LE(BigInt(Date.now()), 26);
save.writeUInt8(0, 34);
save.writeBigInt64LE(0n, 35);
const webDir = path.join(root, 'tc2', 'web-build', 'web');
const servedSave = path.join(webDir, '_bookshots_world.tc2');
fs.writeFileSync(servedSave, save);

const GPU = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8644;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, channel: flag('--webgl2') ? undefined : 'chrome', args: GPU });
const context = await browser.newContext({ viewport: { width: 1600, height: 900 }, deviceScaleFactor: 1 });
const page = await context.newPage();
const consoleLines = [];
page.on('console', (m) => consoleLines.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => consoleLines.push(`[pageerror] ${e.message}`));
const origin = `http://127.0.0.1:${port}`;
const report = { tag, save: savePath, simVersion, seed: seed.toString(), shots: [] };
try {
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
  }, '/_bookshots_world.tc2');
  if (!put.startsWith('ok')) throw new Error('the save could not be put into the store: ' + put);
  fs.rmSync(servedSave, { force: true });
  await page.goto(`${origin}/index.html?speed=0${flag('--webgl2') ? '&gfx=webgl2' : ''}`);
  await page.waitForFunction(() => (window.__TC2 && window.__TC2.ready === true && window.__TC2.people > 0) || window.__TC2_FATAL, null, { timeout: 300000, polling: 200 });
  const s0 = await page.evaluate(() => window.__TC2);
  console.log(`loaded: seed ${s0.seed} (save ${seed}), year ${s0.year}, ${s0.people} people in ${s0.places} places, gfx ${s0.gfx}`);
  if (String(s0.seed) !== seed.toString() || s0.catchingUp) throw new Error('the game did not load the save as saved');
  const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
  await cmd('names on');
  for (const v of views) {
    await cmd(`look ${lat} ${lon} ${v.dist}`);
    await page.waitForTimeout(5000);
    const f = path.join(outDir, `${v.name}.png`);
    await page.screenshot({ path: f });
    report.shots.push(f);
    console.log('shot ' + v.name);
  }
  await cmd(`look ${lat} ${lon} 6000`);
  for (const pg of pages) {
    await cmd(`bookat ${pg.part} ${pg.chapter}`);
    await page.waitForTimeout(2500);
    const f = path.join(outDir, `book_${pg.part < 0 ? 'preface' : pg.part + '_' + pg.chapter}.png`);
    await page.screenshot({ path: f });
    report.shots.push(f);
    console.log('shot book ' + pg.part + ':' + pg.chapter);
  }
  await cmd('closebook');
  report.bookTimes = consoleLines.filter((l) => l.includes('[TC2] book:'));
  for (const l of report.bookTimes) console.log(l);
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
