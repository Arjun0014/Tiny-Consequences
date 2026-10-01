// npm run bench:web [-- --release --level 3,4 --ticks 96 --default]
// Runs sim/bench.lua inside the HTML5 build (the real Lua 5.1 runtime) via the test bridge and
// prints per-tick timings per system. Used for the SIM_LEVEL decision and W08.
import { chromium } from '@playwright/test';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8183));
const levels = String(getFlag(args, '--level', '3,4')).split(',').map(Number);
const ticks = Number(getFlag(args, '--ticks', 96));
const useDefault = hasFlag(args, '--default');   // measure the pre-spun default world instead
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let code = 0;
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  page.on('console', (m) => { if (m.type() === 'error') console.log(`[console] ${m.text()}`); });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v2' }));   // the legacy world (D-079)
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 30_000 });
  for (const level of levels) {
    const id = `bench${level}`;
    await page.evaluate((c) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(c); },
      { cmd: 'bench', id, level, ticks, default: useDefault, seed: Number(getFlag(args, '--seed', 42)), spinup_years: Number(getFlag(args, '--spinup', 0)) });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
      id, { timeout: 600_000, polling: 250 });
    const r = await h.jsonValue();
    if (!r.ok) { console.log(`level ${level}: ERROR ${r.error}`); code = 1; continue; }
    const res = r.result;
    console.log(`[${variant}] level ${res.level} (${res.cells} cells): median ${res.median_ms.toFixed(2)} ms, p95 ${res.p95_ms.toFixed(2)} ms, max ${res.max_ms.toFixed(2)} ms, worldgen ${res.gen_ms.toFixed(0)} ms`);
    const sys = Object.entries(res.systems).sort((a, b) => b[1] - a[1]);
    console.log('   ' + sys.map(([k, v]) => `${k} ${v.toFixed(2)}`).join(' | '));
  }
} catch (e) {
  console.error(e); code = 1;
} finally {
  await browser.close();
  server.close();
}
process.exit(code);
