// A session in the real game for plays and acceptance checks (S24): the release or debug bundle served, a
// headless Chromium drawing with the machine's GPU (ANGLE D3D11) - or SwiftShader with { gpu: false } -
// and the test bridge: cmd(), lua() (read the sim's state), shoot() (flush the view, screenshot).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './browser.mjs';
import { startServer } from '../serve.mjs';
import { rel } from './util.mjs';

export const GPU_ARGS = ['--use-angle=d3d11', '--ignore-gpu-blocklist', '--enable-gpu-rasterization', '--enable-webgl',
  '--autoplay-policy=no-user-gesture-required'];

export async function openGame({ port = 8240, gpu = true, params = {}, viewport = VIEWPORT, log = null } = {}) {
  const server = await startServer({ port, quiet: true });
  const browser = await chromium.launch({ headless: true, args: gpu ? GPU_ARGS : CHROMIUM_ARGS });
  const page = await browser.newPage({ viewport, deviceScaleFactor: 1 });
  const consoleLines = [];
  page.on('console', (m) => { const t = `[${m.type()}] ${m.text()}`; consoleLines.push(t); if (log && m.type() === 'error') log(t); });
  page.on('pageerror', (e) => consoleLines.push(`[pageerror] ${e.message}`));
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0', cloudq: '24', ...params }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  let nextId = 1;
  async function cmd(c, timeout = 300_000) {
    const id = `a${nextId++}`;
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 30 });
    const r = await h.jsonValue();
    if (!r.ok) throw new Error(`${c.cmd} failed: ${r.error}`);
    return r.result;
  }
  async function lua(code) { return (await cmd({ cmd: 'lua', code })).v; }
  async function hour() { return page.evaluate(() => window.__TC.hour); }
  async function advance(h) { const t = await hour(); return cmd({ cmd: 'v3_goto', hour: t + h }); }
  async function shoot(dir, name, settle = 900, flush = true) {
    fs.mkdirSync(dir, { recursive: true });
    if (flush) await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(settle);
    const file = path.join(dir, `${name}.png`);
    await page.screenshot({ path: file, timeout: 600_000 });
    return rel(file);
  }
  async function close() { await browser.close(); server.close(); }
  return { page, browser, server, cmd, lua, hour, advance, shoot, close, consoleLines };
}

// A Lua table literal from a JS value (numbers, strings, booleans, arrays and plain objects).
export function luaValue(v) {
  if (v === null || v === undefined) return 'nil';
  if (typeof v === 'number') return Number.isFinite(v) ? String(v) : '0';
  if (typeof v === 'boolean') return v ? 'true' : 'false';
  if (typeof v === 'string') return JSON.stringify(v);
  if (Array.isArray(v)) return `{${v.map(luaValue).join(',')}}`;
  return `{${Object.entries(v).map(([k, x]) => `[${JSON.stringify(k)}]=${luaValue(x)}`).join(',')}}`;
}
