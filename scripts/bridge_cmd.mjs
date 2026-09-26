// Dev helper: run bridge commands in the HTML5 build and print the results as JSON.
// node scripts/bridge_cmd.mjs [--release] [--no-build] [--params '{"seed":"3"}'] '{"cmd":"ping"}' ...
import { chromium } from '@playwright/test';
import { getFlag, hasFlag } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const variant = hasFlag(args, '--release') ? 'release' : 'debug';
const port = Number(getFlag(args, '--port', 8184));
const params = JSON.parse(getFlag(args, '--params', '{}'));
const cmds = args.filter((a) => a.startsWith('{') && a !== getFlag(args, '--params'));
if (!hasFlag(args, '--no-build')) await build({ variant, ifStale: true, quiet: true });
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
let code = 0;
try {
  const page = await browser.newPage({ viewport: VIEWPORT });
  page.on('console', (m) => { if (m.type() === 'error' || hasFlag(args, '--verbose')) console.log(`[console] ${m.text()}`); });
  const t0 = Date.now();
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, params));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 120_000 });
  console.log(`ready after ${Date.now() - t0} ms`);
  let i = 0;
  for (const raw of cmds) {
    const id = `dev${i++}`;
    await page.evaluate((c) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(c); }, { ...JSON.parse(raw), id });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd,
      id, { timeout: 600_000, polling: 100 });
    console.log(JSON.stringify(await h.jsonValue(), null, 1));
  }
  const tc = await page.evaluate(() => window.__TC);
  delete tc.last_cmd;
  console.log('__TC', JSON.stringify(tc));
} catch (e) {
  console.error(e); code = 1;
} finally {
  await browser.close();
  server.close();
}
process.exit(code);
