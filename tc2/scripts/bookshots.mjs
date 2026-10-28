// Screenshots of the Web build's HUD on a great world (session 25): the places named on the map at several views, and the History Book open at
// chosen parts and chapters - what camera shots cannot show (the HUD is drawn over the frame). A save (a headless run's, of this build's sim
// version) is put into the game's IndexedDB store as saved a moment ago with the offline mode paused, as realmsperf.mjs does, then the game loads
// it; each part the book opens is written then (its time is in the console: "[TC2] book: part ...").
// Usage: node tc2/scripts/bookshots.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,orbit:22000,village:1400]
//        [--book "-1,0:0,1:0,2:0,3:0,4:0,5:0,6:0,7:0"] [--tag NAME] [--webgl2] [--play] [--pages "person:0,place:0"]
// --play (session 27): the pointer over a home, a lightning aimed, cast and its "Your hand", a storm aimed from afar (play_*.png);
// with --meteor-play also a meteor thrown near that home: its fall, its landing and what it left (play_meteor_*.png).
// --lapse (session 29): the timelapse - the oldest person followed at the village view, 1 day/s (the camera rises, watches from orbit), 1 min/s (it
// comes back to them), then a skip of --skip YEARS (default 2) watched from orbit to its end and the leaf of what the years did (lapse_*.png, the
// camera's state at each in report.json `lapse`); --flash N: N frames 120 ms apart at 10 days/s from orbit (lapse_flash_*.png) to measure flicker.
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
const savePath = opt('--save');
const lat = Number(opt('--lat')), lon = Number(opt('--lon'));
if (!savePath || !fs.existsSync(savePath) || !Number.isFinite(lat) || !Number.isFinite(lon)) {
  console.error('usage: node tc2/scripts/bookshots.mjs --save F.tc2 --lat DEG --lon DEG [--views region:6000,...] [--book "-1,0:0,..."] [--tag NAME]');
  process.exit(2);
}
const views = opt('--views', 'region:6000,orbit:22000,village:1400,close:500,street:110').split(',').filter((v) => v).map((v) => { const [n, d] = v.split(':'); return { name: n, dist: Number(d) }; });
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

const GPU = ['--mute-audio', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = 8644;
const server = await startServer({ port, quiet: true });
guardBrowser('bookshots');   // (session 27: the integrated GPU only - never the RX 5600M)
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
  if (flag('--play')) {
    // (session 27) the hand on the world: the pointer over the greatest place's first household's home (its house marked, its family ringed, the
    // tag), a lightning aimed there (its reach on the ground and the places it would touch), cast (the burst), then "Your hand" a few seconds on;
    // a storm aimed from afar
    const snap = async (name) => { const f = path.join(outDir, `${name}.png`); await page.screenshot({ path: f }); report.shots.push(f); console.log('shot ' + name); };
    await cmd('home big 140');
    await page.waitForTimeout(5000);
    await page.mouse.move(800, 450);
    await page.waitForTimeout(1500);
    await snap('play_hover');
    await cmd('aim 0');
    await page.mouse.move(830, 470);
    await page.waitForTimeout(1500);
    await snap('play_aim');
    await cmd('speed 4');   // (10 min/s: the cast applied and its first consequences lived)
    await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up();   // (held a few frames to reach the game)
    await page.waitForTimeout(250);
    await snap('play_cast');
    await page.waitForTimeout(4000);
    await snap('play_hand');
    await cmd('speed 0');
    await cmd(`look ${lat} ${lon} 6000`);
    await page.waitForTimeout(3000);
    await cmd('aim 1');
    await page.mouse.move(760, 430);
    await page.waitForTimeout(1500);
    await snap('play_aimfar');
    await cmd('aim -1');
    if (flag('--meteor-play')) {
      // (session 27) a meteor thrown at the fields 260 m off that home: the stone falling, its landing (the burst), then its fires and scar
      await cmd('home big 700');
      await page.waitForTimeout(4000);
      await cmd('aim 8');
      await page.mouse.move(800 + 170, 450 + 60);
      await page.waitForTimeout(1200);
      await cmd('speed 3');
      await page.mouse.down(); await page.waitForTimeout(80); await page.mouse.up();
      await page.waitForTimeout(350);
      await snap('play_meteor_fall');
      await page.waitForTimeout(700);
      await snap('play_meteor_land');
      await page.waitForTimeout(6000);
      await snap('play_meteor_after');
      await cmd('speed 0');
    }
  }
  if (flag('--lapse')) {
    const snap = async (name) => { const f = path.join(outDir, `${name}.png`); await page.screenshot({ path: f }); report.shots.push(f); const st = await page.evaluate(() => window.__TC2); const row = { name, lapse: st.lapse, light: st.lapseLight, alt: st.camAlt, year: st.year, date: st.date, speed: st.speed, catchingUp: st.catchingUp, skipping: st.skipping }; (report.lapse = report.lapse || []).push(row); console.log('shot ' + name + ' ' + JSON.stringify(row)); };
    await cmd('follow oldest');
    await page.waitForTimeout(3500);
    await snap('lapse_0_ground');
    await cmd('speed 6');   // (1 day/s: a day a second - too fast to watch from the ground)
    await page.waitForTimeout(1600);
    await snap('lapse_1_rising');
    await page.waitForTimeout(3500);
    await snap('lapse_2_orbit');
    await page.waitForTimeout(6000);
    await snap('lapse_3_orbit_later');
    const flashes = Number(opt('--flash', '0'));
    if (flashes > 0) {
      await cmd('speed 7');   // (10 days/s: ten days a second - the strobe a steady light must not show)
      await page.waitForTimeout(2500);
      for (let i = 0; i < flashes; i++) { const f = path.join(outDir, `lapse_flash_${String(i).padStart(2, '0')}.png`); await page.screenshot({ path: f }); await page.waitForTimeout(120); }
      await cmd('speed 6');
      await page.waitForTimeout(1500);
    }
    await cmd('speed 3');   // (1 min/s: the camera comes back to the person followed)
    await page.waitForTimeout(1800);
    await snap('lapse_4_returning');
    await page.waitForTimeout(4500);
    await snap('lapse_5_back');
    await cmd(`skip ${Number(opt('--skip', '2'))}`);
    await page.waitForTimeout(2500);
    await snap('lapse_6_skip_rising');
    await page.waitForTimeout(5000);
    await snap('lapse_7_skip_orbit');
    const t0 = Date.now(); let k = 0;
    while (Date.now() - t0 < Number(opt('--skipwait', '600')) * 1000) {
      const st = await page.evaluate(() => window.__TC2);
      if (!st.catchingUp && st.lapse === 'Off') break;
      await page.waitForTimeout(1000);
      if ((Date.now() - t0) / 1000 > 20 * (k + 1)) { k++; await snap(`lapse_8_skip_${k}`); }
    }
    await page.waitForTimeout(1500);
    await snap('lapse_9_skip_back');
  }
  // (session 27) the person and place pages as leaves of the book: --pages "person:0,person:5,place:0,place:2" - the oldest living person's page and
  // the greatest place's at those sections (page_person_0.png ...)
  for (const pp of opt('--pages', '').split(',').filter((v) => v)) {
    const [kind, sec, who] = pp.split(':');
    // (session 29) "card:0" the oldest person's card as a click opens it; "person:N:head" the first of the greatest place's page at section N
    if (kind === 'card') await cmd(`select ${who || 'oldest'}`);
    else await cmd(`${kind} ${who || (kind === 'person' ? 'oldest' : 'big')} ${Number(sec || 0)}`);
    await page.waitForTimeout(2500);
    const f = path.join(outDir, `page_${kind}_${Number(sec || 0)}${who ? '_' + who : ''}.png`);
    await page.screenshot({ path: f });
    report.shots.push(f);
    console.log('shot ' + path.basename(f));
  }
  await cmd('closepages');
  await cmd(`look ${lat} ${lon} 6000`);
  for (const pg of pages) {
    await cmd(`bookat ${pg.part} ${pg.chapter}`);
    await page.waitForTimeout(2500);
    const f = path.join(outDir, `book_${pg.part === -1 ? 'preface' : pg.part === -2 ? 'timeline' : pg.part + '_' + pg.chapter}.png`);   // (-2: the timeline - it overwrote the preface's picture)
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
