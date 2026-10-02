// node scripts/mathcheck.mjs [--release] [--no-build] [--n 20000]
// How often each math function gives different bits in the browser's Lua and LuaJIT (S14).
import { chromium } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { getFlag, hasFlag, resolveLuajit } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const n = Number(getFlag(args, '--n', 20000));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const ljs = execFileSync(resolveLuajit(), ['tools/mathcheck.lua', String(n)], { encoding: 'utf8', maxBuffer: 1 << 28 });
const lj = new Map(ljs.trim().split('\n').map((l) => { const [name, ...v] = l.split(' '); return [name, v]; }));
const port = 8196;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { cmd: 'mathcheck', n, id: 'm1' });
  const h = await page.waitForFunction(() => window.__TC.last_cmd && window.__TC.last_cmd.id === 'm1' && window.__TC.last_cmd, null, { timeout: 600_000, polling: 200 });
  const r = (await h.jsonValue()).result;
  for (const f of r) {
    const ref = lj.get(f.name);
    let d = 0, ex = '';
    for (let i = 0; i < n; i++) if (f.vals[i] !== ref[i]) { d++; if (!ex) ex = `${f.vals[i]} vs ${ref[i]}`; }
    console.log(`${f.name.padEnd(6)} ${String(d).padStart(6)} of ${n} differ  ${ex}`);
  }
} finally {
  await browser.close();
  server.close();
}
