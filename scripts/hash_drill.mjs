// node scripts/hash_drill.mjs [--release] [--no-build] [--hours 0] [--depth 4] [--path S,v3]
// Where the browser's state first differs from LuaJIT's (S14): the world loaded in both, advanced
// `hours`, then the hashes of each child of a table compared, descending into the children that
// differ (breadth-limited) down to `depth` levels.
import { chromium } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { getFlag, hasFlag, resolveLuajit } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const hours = Number(getFlag(args, '--hours', 0));
const depth = Number(getFlag(args, '--depth', 4));
const start = (getFlag(args, '--path', '') || '').split(',').filter(Boolean);
const port = Number(getFlag(args, '--port', 8194));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let n = 0;
let absHour = 0;
async function cmd(page, c) {
  const id = `d${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout: 1_800_000, polling: 100 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
function lj(path) {
  const out = execFileSync(resolveLuajit(), ['tools/hash_parts.lua', String(absHour), ...path], { encoding: 'utf8', maxBuffer: 1 << 26 });
  return JSON.parse(out);
}
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  if (hours > 0) await cmd(page, { cmd: 'v3_bench', hours });
  // (the game opens the v3 world at its first dawn, D-079: LuaJIT is advanced to the same hour)
  const st = await cmd(page, { cmd: 'v3_hash', hours: 0 });
  absHour = st.start;
  console.log(`browser at hour ${absHour}`);
  const queue = [start];
  while (queue.length) {
    const path = queue.shift();
    const b = await cmd(page, { cmd: 'hash_parts', path });
    const l = lj(path);
    const lm = new Map(l.map((x) => [x.k, x]));
    const bm = new Map(b.map((x) => [x.k, x]));
    const diff = [];
    for (const x of b) { const y = lm.get(x.k); if (!y || y.h !== x.h) diff.push(x.k); }
    for (const y of l) if (!bm.has(y.k)) diff.push(y.k);
    console.log(`[${path.join('.') || 'W'}] ${b.length} keys (luajit ${l.length}), differ: ${diff.slice(0, 30).join(' ')}${diff.length > 30 ? ' ...' : ''}`);
    if (path.length < depth) for (const k of diff.slice(0, 3)) {
      const x = bm.get(k);
      if (x && x.t === 'table') queue.push([...path, k]);
      else if (x || lm.get(k)) console.log(`   ${[...path, k].join('.')}: browser ${x ? x.t : '-'} vs luajit ${lm.get(k) ? lm.get(k).t : '-'}`);
    }
  }
} finally {
  await browser.close();
  server.close();
}
