// node scripts/microbench_web.mjs [--release] [--no-build] [--n 200000]
// What common Lua operations cost in the HTML5 runtime (bridge microbench): ns each, loop overhead
// included. Guides browser optimisation (D-090: LuaJIT counts mislead about C calls).
import { chromium } from '@playwright/test';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8192));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  const best = {};
  for (let k = 0; k < 5; k++) {
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { cmd: 'microbench', n: Number(getFlag(args, '--n', 200000)), id: `m${k}` });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, `m${k}`, { timeout: 600_000, polling: 100 });
    const r = (await h.jsonValue()).result;
    for (const [n, v] of Object.entries(r)) best[n] = Math.min(best[n] ?? 1e9, v);
  }
  console.log(`[${variant}] ns per operation (best of 5, loop included):`);
  for (const [n, v] of Object.entries(best)) console.log(`  ${n.padEnd(12)} ${v.toFixed(1)}`);
} catch (e) {
  console.error(e);
  process.exitCode = 1;
} finally {
  await browser.close();
  server.close();
}
