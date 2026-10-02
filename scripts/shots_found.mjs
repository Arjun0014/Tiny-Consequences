// node scripts/shots_found.mjs [--sett Zurug] [--days 22] [--name found]
// (S17, R5) Founding watched in the game: the crowded premise at a village (sim/agents/premise.lua:
// its ground built over), then the world runs; shots of the party on the road (the planet), the place
// when they arrive (camps being made), and later (huts, stores), each with the inspector on the place
// or a founder. Writes out/shots/found/<name>_*.png and prints what the inspector says.
import fs from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { ROOT, getFlag, rel } from './lib/util.mjs';
import { CHROMIUM_ARGS, VIEWPORT, gameUrl } from './lib/browser.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';
const args = process.argv.slice(2);
const sett = getFlag(args, '--sett', 'Zurug');
const days = Number(getFlag(args, '--days', 22));
const name = getFlag(args, '--name', 'found');
await build({ variant: 'debug', ifStale: true, quiet: true });
const outDir = path.join(ROOT, 'out', 'shots', 'found');
fs.mkdirSync(outDir, { recursive: true });
const port = 8193;
const server = await startServer({ port, quiet: true });
const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
const page = await browser.newPage({ viewport: VIEWPORT, deviceScaleFactor: 1 });
page.on('console', (m) => { const t = m.text(); if (m.type() === 'error' || /ERROR|error:|attempt to|stack traceback/.test(t)) console.log('[page] ' + t.slice(0, 400)); });
let n = 1;
async function cmd(c, timeout = 600_000) {
  const id = `f${n++}`;
  await page.evaluate((x) => { (window.__TC_CMDS = window.__TC_CMDS || []).push(x); }, { ...c, id });
  const h = await page.waitForFunction((w) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === w && window.__TC.last_cmd, id, { timeout, polling: 50 });
  const r = await h.jsonValue();
  if (!r.ok) throw new Error(`${c.cmd}: ${r.error}`);
  return r.result;
}
async function shot(tag, info) {
  await page.waitForTimeout(300);
  await cmd({ cmd: 'flush_view' });
  await page.waitForTimeout(2000);
  const file = path.join(outDir, `${name}_${tag}.png`);
  await page.screenshot({ path: file });
  console.log(`shot: ${rel(file)}`);
  if (info) console.log('   ' + JSON.stringify(info).slice(0, 1500));
}
try {
  await page.goto(gameUrl(`http://127.0.0.1:${port}`, { world: 'v3', speed: '0' }));
  await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, { timeout: 240_000 });
  await cmd({ cmd: 'ui', debug: false });
  await cmd({ cmd: 'render_off', on: true }).catch(() => {});
  const pr = await cmd({ cmd: 'v3_premise', kind: 'crowd', sett });
  console.log('premise:', JSON.stringify(pr));
  let t = (await cmd({ cmd: 'v3_found_info' })).hour || 0;
  let party = null, onRoad = false, arrived = false, later = false;
  for (let h = 1; h <= days * 24; h += 1) {
    await cmd({ cmd: 'v3_goto', hour: t + h });
    if (h % 6 !== 0 && party && onRoad && arrived) continue;
    const fi = await cmd({ cmd: 'v3_found_info' });
    if (!party && fi.parties.length > 0) { party = fi.parties[0]; console.log(`day ${(h / 24).toFixed(1)} decided: ${party.why}`); }
    if (h % 24 === 0) console.log(`day ${h / 24}: parties ${fi.parties.length} places ${fi.places.length}`);
    if (party && !onRoad) {
      const p = await cmd({ cmd: 'v3_person', pid: party.members[0] });
      if (p.action === 'travel') {
        onRoad = true;
        await cmd({ cmd: 'render_off', on: false }).catch(() => {});
        await cmd({ cmd: 'v3_person', pid: party.members[0], look: true, zoom: 0.95 });
        const sel = await cmd({ cmd: 'select', person: party.members[0] });
        await shot('1_road', { day: (h / 24).toFixed(1), who: p.name, why: p.why, inspector: sel.rows });
        await cmd({ cmd: 'render_off', on: true }).catch(() => {});
      }
    }
    if (party && onRoad && !arrived && fi.places.length > 0) {
      arrived = true;
      await cmd({ cmd: 'v3_goto', hour: t + h + 3 });
      await cmd({ cmd: 'render_off', on: false }).catch(() => {});
      const place = fi.places[0];
      await cmd({ cmd: 'v3_look', sett: place.id, what: 'square', zoom: 1.55 });
      const sel = await cmd({ cmd: 'select', sett: place.id });
      await shot('2_arrival', { day: (h / 24).toFixed(1), place, inspector: sel.rows });
      const sp = await cmd({ cmd: 'select', person: party.members[0] });
      await cmd({ cmd: 'v3_person', pid: party.members[0], look: true, zoom: 1.75 });
      await shot('3_founder', { inspector: sp.rows });
      await cmd({ cmd: 'render_off', on: true }).catch(() => {});
    }
    if (arrived && !later && h >= days * 24 - 1) later = true;
  }
  await cmd({ cmd: 'render_off', on: false }).catch(() => {});
  const fi = await cmd({ cmd: 'v3_found_info' });
  if (fi.places.length > 0) {
    const place = fi.places[0];
    await cmd({ cmd: 'v3_goto', lh: 11, days: 0 });
    await cmd({ cmd: 'v3_look', sett: place.id, what: 'square', zoom: 1.5 });
    const sel = await cmd({ cmd: 'select', sett: place.id });
    await shot('4_later', { place, inspector: sel.rows });
    await cmd({ cmd: 'v3_look', sett: place.id, what: 'square', zoom: 1.1 });
    await shot('5_wide', null);
    if (place.first) {
      await cmd({ cmd: 'v3_look', sett: place.id, x: place.first.x, y: place.first.y, zoom: 1.85 });
      await shot('6_close', { first: place.first });
    }
  } else {
    console.log('no place founded in ' + days + ' days');
  }
} catch (e) { console.log('FAILED ' + e.message); }
await browser.close();
server.close();
