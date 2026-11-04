// A scripted play session in the Web build (session 32): the game opened as a player opens it (or a save put into its store), then steps read from a
// file, one a line - the pointer and keys as a player uses them, bridge commands, waits and pictures. For judging the game by playing it.
// Usage: node tc2/scripts/play.mjs --steps FILE [--save F.tc2] [--tag NAME] [--url "index.html?..."] [--w 1600 --h 900] [--webgl2]
// Steps: cmd TEXT | wait MS | shot NAME | move X Y | click X Y | rclick X Y | dclick X Y | drag X1 Y1 X2 Y2 [MS] | wheel X Y DY [N] | key KEY |
//        hold KEY MS | state | eval JS | until JS MS   (# comments; coordinates in CSS pixels)
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
const stepsFile = opt('--steps');
if (!stepsFile || !fs.existsSync(stepsFile)) { console.error('usage: node tc2/scripts/play.mjs --steps FILE [--save F.tc2] [--tag NAME]'); process.exit(2); }
const steps = fs.readFileSync(stepsFile, 'utf8').split(/\r?\n/).map((l) => l.trim()).filter((l) => l && !l.startsWith('#'));
const tag = opt('--tag', 'play');
const W = Number(opt('--w', '1600')), H = Number(opt('--h', '900'));
const outDir = path.join(root, 'tc2', 'out', 'webtest', tag);
fs.mkdirSync(outDir, { recursive: true });
const webDir = path.join(root, 'tc2', 'web-build', 'web');
const savePath = opt('--save');
const servedSave = path.join(webDir, '_play_world.tc2');
if (savePath) {
  const save = Buffer.from(fs.readFileSync(savePath));
  if (save.readUInt32LE(0) !== 0x57324354) { console.error('not a TC2 world save: ' + savePath); process.exit(2); }
  save.writeBigInt64LE(BigInt(Date.now()), 26);   // (saved just now: no catch-up of an absence)
  save.writeUInt8(0, 34);
  save.writeBigInt64LE(0n, 35);
  fs.writeFileSync(servedSave, save);
}

const GPU = ['--mute-audio', '--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl', '--enable-unsafe-webgpu', '--enable-features=Vulkan,WebGPU'];
const port = Number(opt('--port', '8647'));
const server = await startServer({ port, quiet: true });
guardBrowser('play');   // (the integrated GPU only - never the RX 5600M)
const browser = await chromium.launch({ headless: true, channel: flag('--webgl2') ? undefined : 'chrome', args: GPU });
const context = await browser.newContext({ viewport: { width: W, height: H }, deviceScaleFactor: 1 });
const page = await context.newPage();
const lines = [];
page.on('console', (m) => lines.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => lines.push(`[pageerror] ${e.message}`));
const origin = `http://127.0.0.1:${port}`;
const wait = (ms) => page.waitForTimeout(ms);
const report = { tag, shots: [], states: [] };
let failed = false;
try {
  if (savePath) {
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
    }, '/_play_world.tc2');
    if (!put.startsWith('ok')) throw new Error('the save could not be put into the store: ' + put);
    fs.rmSync(servedSave, { force: true });
  }
  const t0 = Date.now();
  await page.goto(`${origin}/${opt('--url', 'index.html')}${flag('--webgl2') ? (opt('--url', 'index.html').includes('?') ? '&' : '?') + 'gfx=webgl2' : ''}`);
  console.log('opened ' + page.url());
  const cmd = (c) => page.evaluate((x) => { (window.__TC2_CMDS = window.__TC2_CMDS || []).push(x); }, c);
  for (const s of steps) {
    const [op, ...rest] = s.split(/\s+/);
    const n = rest.map(Number);
    switch (op) {
      case 'cmd': await cmd(rest.join(' ')); break;
      case 'wait': await wait(n[0]); break;
      case 'shot': { const f = path.join(outDir, `${rest[0]}.png`); await page.screenshot({ path: f }); report.shots.push(f); console.log(`shot ${rest[0]} (+${((Date.now() - t0) / 1000).toFixed(1)} s)`); break; }
      case 'move': await page.mouse.move(n[0], n[1], { steps: 4 }); break;
      case 'click': await page.mouse.move(n[0], n[1], { steps: 3 }); await wait(120); await page.mouse.down(); await wait(90); await page.mouse.up(); await wait(100); break;
      case 'rclick': await page.mouse.move(n[0], n[1], { steps: 3 }); await wait(100); await page.mouse.down({ button: 'right' }); await wait(90); await page.mouse.up({ button: 'right' }); break;
      case 'dclick': await page.mouse.move(n[0], n[1], { steps: 3 }); await wait(100); await page.mouse.down(); await wait(60); await page.mouse.up(); await wait(90); await page.mouse.down(); await wait(60); await page.mouse.up(); break;
      case 'drag': {
        const ms = n[4] || 600, k = Math.max(4, Math.round(ms / 30));
        await page.mouse.move(n[0], n[1]); await wait(80); await page.mouse.down(); await wait(80);
        for (let i = 1; i <= k; i++) { await page.mouse.move(n[0] + (n[2] - n[0]) * i / k, n[1] + (n[3] - n[1]) * i / k); await wait(ms / k); }
        await wait(60); await page.mouse.up(); break;
      }
      case 'wheel': { await page.mouse.move(n[0], n[1]); const times = n[3] || 1; for (let i = 0; i < times; i++) { await page.mouse.wheel(0, n[2]); await wait(70); } break; }
      case 'key': await page.keyboard.press(rest[0]); break;
      case 'hold': await page.keyboard.down(rest[0]); await wait(n[1]); await page.keyboard.up(rest[0]); break;
      case 'state': { const st = await page.evaluate(() => window.__TC2 || null); report.states.push(st); console.log('state ' + JSON.stringify(st).slice(0, 900)); break; }
      case 'eval': { const r = await page.evaluate(rest.join(' ')); console.log('eval ' + JSON.stringify(r).slice(0, 900)); break; }
      case 'until': {
        const js = rest.slice(0, -1).join(' '), ms = Number(rest[rest.length - 1]);
        try { await page.waitForFunction(js, null, { timeout: ms, polling: 250 }); console.log('until ok: ' + js); }
        catch { console.log('until TIMEOUT: ' + js); }
        break;
      }
      default: console.log('unknown step: ' + s);
    }
  }
} catch (e) {
  failed = true;
  console.log('FAILED: ' + e.message);
}
const errs = lines.filter((l) => /\[error\]|\[pageerror\]|Exception/.test(l));
report.errors = errs;
fs.writeFileSync(path.join(outDir, 'console.txt'), lines.join('\n'));
fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify(report, null, 1));
console.log(`console: ${lines.length} lines, ${errs.length} errors` + (errs.length ? '\n' + errs.slice(0, 12).join('\n') : ''));
await browser.close();
server.close();
process.exit(failed ? 1 : 0);
