// node scripts/probe_cmd.mjs [--release] [--no-build] '<json cmd>' ['<json cmd>' ...]
// Load the game (speed 0) and run bridge commands in order, printing each result (probes).
import { chromium } from '@playwright/test';
import { hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const cmds = args.filter((a) => a.startsWith('{')).map((a) => JSON.parse(a));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const port = 8195;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { speed: '0', gfx: 'low' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  let n = 0;
  for (const c of cmds) {
    const id = `q${n++}`;
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
    const h = await page.waitForFunction((w) => window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout: 1_800_000, polling: 100 });
    console.log(JSON.stringify(await h.jsonValue()));
  }
} finally {
  await browser.close();
  server.close();
}
