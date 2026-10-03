// node scripts/play_powers.mjs [--sett 1] [--days 1] [--hour 10] [--zoom 1.55]
// (S22) Lightning played as a player plays it: close in on a village, the bolt armed in the power bar, a click
// on what is under the pointer - an exposed sheep, a sheep under a tree, a person out in the open, a house -
// each in a fresh world; then the screen and the news (out/shots/play_powers). The click goes through the
// game's own picking (the ground under the pointer, or the beast or person drawn there).
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = Number(getFlag(args, '--sett', 1));
const days = Number(getFlag(args, '--days', 1));
const hour = Number(getFlag(args, '--hour', 10));
const zoom = Number(getFlag(args, '--zoom', 1.55));
const only = getFlag(args, '--only', null);
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'play_powers');
fs.mkdirSync(outDir, { recursive: true });
const port = 8197;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });

async function scenario(name, pick) {
  const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
  page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
  let n = 1;
  async function cmd(c, timeout = 240_000) {
    const id = `q${n++}`;
    await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
    const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
    const r = await h.jsonValue();
    if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
    return r.result;
  }
  async function press(x, y) { await page.mouse.move(x, y); await page.waitForTimeout(150); await page.mouse.down(); await page.waitForTimeout(150); await page.mouse.up(); await page.waitForTimeout(250); }
  try {
    await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
    await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
    await cmd({ cmd: 'ui', debug: false });
    const g = await cmd({ cmd: 'v3_goto', lh: hour, days });
    const t = await pick(cmd);
    if (!t) { console.log(`== ${name}: nothing to aim at`); await page.close(); return; }
    await cmd({ cmd: 'v3_look', sett, x: t.x, y: t.y, zoom });
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(1800);
    const vp = page.viewportSize();
    let px = vp.width / 2, py = vp.height / 2;
    if (t.grp || t.pid) {
      const sc = await cmd(t.pid ? { cmd: 'v3_screen', pid: t.pid } : { cmd: 'v3_screen', sett, grp: t.grp, k: t.k });
      px = sc.sx * vp.width / sc.w; py = (sc.h - sc.sy) * vp.height / sc.h;
    }
    let file = path.join(outDir, `${name}_0_aim.png`);
    await page.screenshot({ path: file });
    // the bar: 9 buttons of 54 with gaps of 10, centred, 51 px from the bottom; lightning is the first
    const sx = vp.width / 1280, sy = vp.height / 720;
    const w = 9 * 54 + 8 * 10 + 28;
    await press((640 - w / 2 + 14 + 27) * sx, (720 - 51) * sy);
    await press(px, py);
    await page.waitForTimeout(300);
    await cmd({ cmd: 'v3_goto', hour: g.hour + 0.05 });
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(1500);
    file = path.join(outDir, `${name}_1_struck.png`);
    await page.screenshot({ path: file });
    await cmd({ cmd: 'v3_goto', hour: g.hour + 1.0 });
    await cmd({ cmd: 'flush_view' });
    await page.waitForTimeout(1500);
    file = path.join(outDir, `${name}_2_after.png`);
    await page.screenshot({ path: file });
    const feed = await cmd({ cmd: 'news' });
    console.log(`== ${name}: aimed at ${t.what} (${t.x.toFixed(3)}, ${t.y.toFixed(3)}) clicked (${px.toFixed(0)}, ${py.toFixed(0)})`);
    for (const c of feed.cards) console.log(`   card [${c.level}] ${c.title} | ${c.line} | root ${c.root} | why: ${c.why.join(' <- ')}`);
    if (t.after) console.log('   ' + await t.after(cmd));
  } catch (e) { console.log(`== ${name}: FAILED ${e.message}`); }
  await page.close();
}

const scenarios = {
  sheep_open: async (cmd) => {
    const b = await cmd({ cmd: 'v3_beasts', sett });
    const c = b.beasts.filter((q) => q.exposed && (q.sp === 'sheep' || q.sp === 'goat')).sort((p, q) => q.margin - p.margin)[0];
    if (!c) return null;
    return { ...c, what: `a ${c.sp} in the open (nothing taller within reach)`,
      after: async (cmd2) => { const b2 = await cmd2({ cmd: 'v3_beasts', sett }); const still = b2.beasts.find((q) => q.grp === c.grp && q.k === c.k); return still ? `the ${c.sp} is still out of doors` : `the ${c.sp} is no longer standing (dead)`; } };
  },
  sheep_under_tree: async (cmd) => {
    const b = await cmd({ cmd: 'v3_beasts', sett });
    const c = b.beasts.filter((q) => !q.exposed && q.taller === 'a tree' && (q.sp === 'sheep' || q.sp === 'goat' || q.sp === 'cattle')).sort((p, q) => p.margin - q.margin)[0];
    if (!c) return null;
    return { ...c, what: `a ${c.sp} under a tree (the tree is taller)`,
      after: async (cmd2) => { const b2 = await cmd2({ cmd: 'v3_beasts', sett }); const still = b2.beasts.find((q) => q.grp === c.grp && q.k === c.k); return still ? `the ${c.sp} is still out of doors` : `the ${c.sp} is no longer standing`; } };
  },
  cow_open: async (cmd) => {
    const b = await cmd({ cmd: 'v3_beasts', sett });
    const c = b.beasts.filter((q) => q.exposed && q.sp === 'cattle').sort((p, q) => q.margin - p.margin)[0];
    if (!c) return null;
    return { ...c, what: 'a cow in the open' };
  },
  wild_open: async (cmd) => {
    const b = await cmd({ cmd: 'v3_beasts', sett });
    const c = b.beasts.filter((q) => q.exposed && q.grp === 'wild').sort((p, q) => q.margin - p.margin)[0];
    if (!c) return null;
    return { ...c, what: `a wild ${c.sp} in the open` };
  },
  house: async (cmd) => {
    const r = await cmd({ cmd: 'v3_look', sett, what: 'house', zoom }).catch(() => null);
    return r ? { x: r.x ?? 0, y: r.y ?? 0, what: 'a house' } : null;
  },
};
for (const [name, pick] of Object.entries(scenarios)) {
  if (!only || only.split(',').includes(name)) await scenario(name, pick);
}
await browser.close();
server.close();
