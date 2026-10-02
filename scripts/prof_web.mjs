// node scripts/prof_web.mjs [--release] [--no-build] [--world NAME] [--warm 0] [--hours 48]
//        [--runs 3] [--idle 0] [--settle 3] [--render] [--fresh [--from H]] [--collect] [--nogc] [--time mod:fn,mod:fn] [--prof [--every 500] [--top 40]]
// Where the v3 sim's time goes IN THE BROWSER (D-090: the HTML5 Lua 5.1 prices string.format, bit
// and C calls very differently from LuaJIT). This laptop is shared (other heavy workloads), so a
// single wall-clock run can be off 2-3x: by default the world is loaded `runs` times and each game
// hour's cost is the MINIMUM over the runs (the lower envelope); the sum is the robust figure.
//   --time: the named module functions wrapped in wall timers (calls, ms/h, us/call), per run.
//   --prof: the wall-time hook sampler (game/platform/luaprof.lua; biased toward functions that
//           run long stretches, use --time to confirm).
// Output to the console and out/prof_web.json.
import { chromium } from '@playwright/test';
import fs from 'node:fs';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8191));
const warm = Number(getFlag(args, '--warm', 0));
const hours = Number(getFlag(args, '--hours', 48));
const runs = Number(getFlag(args, '--runs', 3));
const idle = Number(getFlag(args, '--idle', 0)); // seconds the page idles after loading, before the probe
const fns = getFlag(args, '--time', null);
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let n = 0;
async function cmd(page, c, timeout = 1_800_000) {
  const id = `p${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
    id, { timeout, polling: 100 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
async function load() {
  const page = await browser.newPage({ viewport: VIEWPORT });
  const q = { speed: '0', gfx: 'low' };
  if (getFlag(args, '--world', null)) q.world = getFlag(args, '--world', null);
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, q));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  if (!hasFlag(args, '--render')) {
    // headless Chromium rasterises on the CPU (SwiftShader): frames drawn and queued just before a
    // probe compete with the sim for the processor (and its turbo budget) for a second or more
    // (S15: a bench right after load or after idling cost 1.5-3x the same hours on a fresh copy).
    // Draw nothing and let the rasteriser drain first; --render keeps drawing.
    await cmd(page, { cmd: 'render_off', on: true });
    await page.waitForTimeout(Number(getFlag(args, '--settle', 3)) * 1000);
  }
  if (idle > 0) await page.waitForTimeout(idle * 1000);
  if (warm > 0) await cmd(page, { cmd: 'v3_bench', hours: warm });
  return page;
}
const out = { variant, warm, hours, runs: [] };
try {
  if (hasFlag(args, '--prof')) {
    const page = await load();
    const from = getFlag(args, '--from', null) ? Number(getFlag(args, '--from', null)) : undefined;
    const r = await cmd(page, { cmd: 'v3_prof', hours, fresh: hasFlag(args, '--fresh'), from, every: Number(getFlag(args, '--every', 500)), top: Number(getFlag(args, '--top', 40)), depth: 16, slow_us: Number(getFlag(args, '--slow-us', 40)) });
    console.log(`[${variant}] profiled ${hours} h: wall ${r.wall_ms.toFixed(0)} ms, sampled ${r.sampled_ms.toFixed(0)} ms (${r.ms_per_hour.toFixed(1)} ms/h without the hook), ${r.hooks} samples`);
    const show = (title, rows) => {
      console.log(title);
      for (const x of rows) console.log(`  ${x.pct.toFixed(1).padStart(5)}%  ${(x.ms / hours).toFixed(2).padStart(6)} ms/h  ${x.k}${x.name ? '  ' + x.name : ''}`);
    };
    show('self (functions):', r.self);
    show('inclusive:', r.incl);
    show('lines:', r.lines);
    show('slow windows (C-side work, by line):', r.slow);
    out.prof = r;
    await page.close();
  } else {
    let env = null;
    const timed = {};
    for (let k = 0; k < runs; k++) {
      const page = await load();
      if (fns) {
        const r = await cmd(page, { cmd: 'v3_time', hours, fns: fns.split(',') });
        console.log(`[${variant}] run ${k + 1}: ${r.ms_per_hour.toFixed(1)} ms/h (timers on)`);
        for (const f of r.fns) {
          const t = timed[f.fn] || (timed[f.fn] = { calls: f.calls, ms: [] });
          t.ms.push(f.ms_per_hour);
        }
      } else {
        if (hasFlag(args, '--fresh')) {
          // the same first hours twice in one page: right after load, then on a fresh copy of the world
          const from = getFlag(args, '--from', null) ? Number(getFlag(args, '--from', null)) : undefined;
          const b1 = await cmd(page, { cmd: 'v3_bench', hours, fresh: true, from, hash: true });
          const b2 = await cmd(page, { cmd: 'v3_bench', hours, fresh: true, from, hash: true});
          console.log(`  hashes fresh ${b1.hash} ${b2.hash}`);
          console.log(`  t0 fresh ${b1.t0}`);
          console.log(`  fresh copy 1: ${b1.per.map((x) => x.toFixed(0)).join(' ')}`);
          console.log(`  fresh copy 2: ${b2.per.map((x) => x.toFixed(0)).join(' ')}`);
        }
        const b = await cmd(page, { cmd: 'v3_bench', hours, collect: hasFlag(args, '--collect'), nogc: hasFlag(args, '--nogc'), hash: hasFlag(args, '--fresh') });
        if (b.hash) console.log(`  hash game world ${b.hash}`);
        console.log(`[${variant}] run ${k + 1}: ${b.ms_per_hour.toFixed(1)} ms of sim per game hour, ${b.people} people (from hour ${b.t0})`);
        env = env ? env.map((v, i) => Math.min(v, b.per[i])) : b.per.slice();
        out.runs.push(b.per);
      }
      await page.close();
    }
    if (fns) {
      for (const [fn, t] of Object.entries(timed)) {
        const m = Math.min(...t.ms);
        console.log(`  ${fn.padEnd(36)} ${String(t.calls).padStart(8)} calls  min ${m.toFixed(2).padStart(7)} ms/h  (${t.ms.map((x) => x.toFixed(1)).join(' / ')})  ${(1000 * m * hours / Math.max(1, t.calls)).toFixed(1)} us/call`);
      }
      out.timed = timed;
    } else {
      const sum = env.reduce((a, b) => a + b, 0);
      const win = 6;
      const ws = [];
      for (let i = 0; i < env.length; i += win) ws.push((env.slice(i, i + win).reduce((a, b) => a + b, 0) / Math.min(win, env.length - i)).toFixed(0));
      console.log(`[${variant}] lower envelope over ${runs} runs: ${(sum / hours).toFixed(1)} ms of sim per game hour (a 12-day season ${(sum / hours * 288 / 1000).toFixed(1)} s)`);
      console.log(`  by ${win}-hour window (ms/h): ${ws.join(' ')}`);
      out.envelope = env;
      out.ms_per_hour = sum / hours;
    }
  }
  fs.mkdirSync('out', { recursive: true });
  fs.writeFileSync('out/prof_web.json', JSON.stringify(out, null, 1));
} catch (e) {
  console.error(e);
  process.exitCode = 1;
} finally {
  await browser.close();
  server.close();
}
