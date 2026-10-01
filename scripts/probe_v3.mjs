// node scripts/probe_v3.mjs [--hours 24] [--step 0.5] — step a v3 world in the browser by bridge
// commands, timing each and capturing console errors (finding hangs in the HTML5 runtime).
import { chromium } from '@playwright/test';
import { getFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const hours = Number(getFlag(args, '--hours', 24));
const step = Number(getFlag(args, '--step', 0.5));
const port = 8191;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
const log = [];
page.on('console', (m) => { const t = m.text(); if (!t.includes('INFO:')) log.push(`[${m.type()}] ${t}`); });
page.on('pageerror', (e) => log.push(`[pageerror] ${e.message}`));
let n = 1;
async function cmd(c, timeout = 60_000) {
  const id = `p${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  return h.jsonValue();
}
try {
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  let t = await page.evaluate(() => window.__TC.hour);
  for (let k = 0; k < hours / step; k++) {
    const a = Date.now();
    const r = await cmd({ cmd: 'v3_goto', hour: t + step });
    t += step;
    const f = await cmd({ cmd: 'v3_person', pid: 'fisher', look: true, zoom: 1.8 });
    const ms = Date.now() - a;
    if (!r.ok || ms > 3000 || k % 8 === 0) console.log(`h+${(k + 1) * step}: ${ms} ms ${r.ok ? '' : r.error} fisher ${f.ok ? f.result.name : '-'}`);
    if (log.length) { console.log(log.splice(0).slice(-5).join('\n')); }
  }
} catch (e) { console.log('FAILED ' + e.message); console.log(log.slice(-10).join('\n')); }
await browser.close();
server.close();
