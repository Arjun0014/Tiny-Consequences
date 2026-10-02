// node scripts/shots_herd.mjs --sett K --at ABS_HOUR [--what flock|herd|wild|wolves] [--herd cattle|swine]
//        [--frames 6] [--dt 0.05] [--zoom 1.6] [--name N] [--follow]
// A herd, band or pack watched over time (S15 animal audit): the camera framed on it at the first
// hour, then `frames` shots `dt` game hours apart (the camera stays unless --follow re-frames each
// shot). Writes out/shots/herd/<name>_<i>.png and a contact sheet out/shots/herd/<name>.png.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const at = Number(getFlag(args, '--at', 30));
const what = getFlag(args, '--what', 'flock');
const herd = getFlag(args, '--herd', 'cattle');
const frames = Number(getFlag(args, '--frames', 6));
const dt = Number(getFlag(args, '--dt', 0.05));
const zoom = Number(getFlag(args, '--zoom', 1.6));
const name = getFlag(args, '--name', `s${sett}_${what}_${at}`);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'herd');
fs.mkdirSync(outDir, { recursive: true });
const port = 8193;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 240_000) {
  const id = `h${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
const files = [];
try {
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: getFlag(args, '--world', 'v3'), speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  for (let i = 0; i < frames; i++) {
    await cmd({ cmd: 'v3_goto', hour: at + i * dt });
    if (i === 0 || args.includes('--follow')) {
      const r = await cmd({ cmd: 'v3_look', sett, what, zoom, herd });
      if (i === 0) console.log(`herd: ${r.name || ''} ${r.info ? JSON.stringify(r.info) : ''}`);
    }
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(i === 0 ? 2500 : 900);
    const file = path.join(outDir, `${name}_${i}.png`);
    await page.screenshot({ path: file });
    files.push(file);
  }
  // contact sheet: the frames side by side, two rows
  const sheet = await browser.newPage({ viewport: { width: 1600, height: 900 } });
  const cols = Math.ceil(frames / 2);
  const imgs = files.map((f, i) => `<div style="position:relative"><img src="data:image/png;base64,${fs.readFileSync(f).toString('base64')}" style="width:100%"><span style="position:absolute;left:6px;top:4px;color:#fff;font:bold 16px sans-serif;text-shadow:0 0 3px #000">+${(i * dt * 60).toFixed(0)} min</span></div>`).join('');
  await sheet.setContent(`<body style="margin:0;background:#111"><div style="display:grid;grid-template-columns:repeat(${cols},1fr);gap:4px">${imgs}</div></body>`);
  const out = path.join(outDir, `${name}.png`);
  await sheet.screenshot({ path: out, fullPage: true });
  console.log(`sheet: ${rel(out)}`);
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
