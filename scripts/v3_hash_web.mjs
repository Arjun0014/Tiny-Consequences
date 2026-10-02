// node scripts/v3_hash_web.mjs [--release] [--no-build] [--hours 72] [--every 24] [--world NAME]
// The v3 world's full state hash in the browser's Lua 5.1 (bridge v3_hash) next to LuaJIT's
// (tools/v3_hash.lua): the two runtimes must live the same world (S14; W06 checks the v2 world).
import { chromium } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
import { resolveLuajit } from './lib/util.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const hours = Number(getFlag(args, '--hours', 72));
const every = Number(getFlag(args, '--every', 24));
const port = Number(getFlag(args, '--port', 8193));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
function luajitRef(from) {
  const lj = execFileSync(resolveLuajit(), ['tools/v3_hash.lua', '--hours', String(hours), '--every', String(every), '--from', String(from)], { encoding: 'utf8' });
  return [...lj.matchAll(/hour\s+(\d+)\s+hash\s+(\d+)/g)].map((m) => ({ hour: Number(m[1]), hash: m[2] }));
}
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let ok = true;
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  const q = { speed: '0', gfx: 'low' };
  if (getFlag(args, '--world', null)) q.world = getFlag(args, '--world', null);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { cmd: 'v3_hash', hours, every, id: 'h1' });
  const h = await page.waitForFunction(() => window.__TC.last_cmd && window.__TC.last_cmd.id === 'h1' && window.__TC.last_cmd, null, { timeout: 1_800_000, polling: 200 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(r.error);
  // (the game opens the v3 world at its first dawn, D-079: LuaJIT starts from the same hour)
  console.log(`browser start hour ${r.result.start}`);
  const ref = luajitRef(r.result.start);
  for (const x of r.result.hashes) {
    const y = ref.find((q) => q.hour === x.hour);
    const same = y && y.hash === x.hash;
    if (!same) ok = false;
    console.log(`hour ${String(x.hour).padStart(5)}  browser ${x.hash.padStart(10)}  luajit ${(y ? y.hash : '-').padStart(10)}  ${same ? 'same' : 'DIFFERENT'}`);
  }
} finally {
  await browser.close();
  server.close();
}
console.log(ok ? 'v3 world identical in both runtimes' : 'v3 worlds DIFFER between the runtimes');
process.exitCode = ok ? 0 : 1;
