// R7 acceptance played in the real game (S24). Each case sets up a state in a fresh world, shoots BEFORE, casts the
// power through the game's own path (the bar armed and a click where the player would click, or the bridge's
// `power` - the same powers.cast), shoots IMMEDIATELY and AFTER, and reads the sim (game/platform/accept.lua), the
// news and "Your actions". Drawn with the machine's GPU (ANGLE D3D11); --swiftshader for the software rasteriser.
// Writes out/accept/<tag>/<case>/*.png and out/accept/<tag>/report.json.
//   node scripts/play_accept.mjs --case clouds,meteor_village,lightning_herd [--tag before] [--no-build] [--port 8240]
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, getFlag, hasFlag } from './lib/util.mjs';
import { build } from './build.mjs';
import { openGame } from './lib/play.mjs';
import { decodePng, diffShare } from './lib/png.mjs';

const args = process.argv.slice(2);
const tag = getFlag(args, '--tag', 'now');
const port = Number(getFlag(args, '--port', 8240));
const only = (getFlag(args, '--case', '') || '').split(',').filter(Boolean);
if (!hasFlag(args, '--no-build')) await build({ variant: 'debug', ifStale: true, quiet: true });
const base = path.join(ROOT, 'out', 'accept', tag);
fs.mkdirSync(base, { recursive: true });
const reportFile = path.join(base, 'report.json');
const report = fs.existsSync(reportFile) ? JSON.parse(fs.readFileSync(reportFile, 'utf8')) : {};

const STATS = (sid, x, y, r, since) => `return require('game.platform.accept').village(W, ${sid}, ${x}, ${y}, ${r}, now, ${since})`;
const fmt = (o) => JSON.stringify(o);

async function newsAndChain(g) {
  const feed = await g.cmd({ cmd: 'news', n: 6 });
  const act = await g.cmd({ cmd: 'actions', ev: 'latest' });
  return {
    news: feed.cards.map((c) => `[${c.level}] ${c.title} | ${c.line || ''} | root ${c.root} | why: ${(c.why || []).join(' <- ')}`),
    actions: (act.list || []).map((I) => `${I.title}: ${I.n_total} (${I.n_direct} direct)${I.active ? ' unfolding' : ''}`),
    chain: (act.rows || []).map((r) => `${'  '.repeat(r.depth - 1)}- ${r.title}${r.line ? ' - ' + r.line : ''}${r.active ? ' *' : ''}`),
  };
}

// A click at the window pixel (x, y from the top left), held a few frames so Defold sees it.
async function press(g, x, y) {
  await g.page.mouse.move(x, y); await g.page.waitForTimeout(120);
  await g.page.mouse.down(); await g.page.waitForTimeout(150);
  await g.page.mouse.up(); await g.page.waitForTimeout(250);
}

const CASES = {
  // The descent from the planet into a village under the god's rain: what share of the frame the cloud hides at
  // each zoom (the same frame drawn with and without the cloud layer).
  async clouds(g, dir, out) {
    await g.cmd({ cmd: 'v3_goto', lh: 9.5, days: 1 });
    const look = await g.cmd({ cmd: 'v3_look', sett: 1, what: 'square' });
    const cam = await g.cmd({ cmd: 'camera' });
    await g.cmd({ cmd: 'power', power: 'rain', cell: look.cell });
    await g.advance(3);
    out.probe = (await g.cmd({ cmd: 'clouds' })).villages.map((v) => `${v.name} cover ${(v.cover || 0).toFixed(2)} dark ${(v.dark || 0).toFixed(2)} base ${v.base.toFixed(2)}`);
    out.zooms = [];
    for (const z of [0.16, 0.55, 0.7, 0.8, 0.9, 0.95, 1.0, 1.05, 1.1, 1.15, 1.2, 1.25, 1.3, 1.35, 1.45, 1.6]) {
      await g.cmd({ cmd: 'camera', lat: cam.lat, lon: cam.lon, zoom: z });
      const fa = await g.shoot(dir, `z${z.toFixed(2)}`);
      await g.cmd({ cmd: 'ui', clouds: false });
      const fb = await g.shoot(dir, `z${z.toFixed(2)}_none`);
      await g.cmd({ cmd: 'ui', clouds: true });
      const a = decodePng(fs.readFileSync(path.join(ROOT, fa)));
      const b = decodePng(fs.readFileSync(path.join(ROOT, fb)));
      const d = diffShare(a, b, 24, [Math.floor(a.w * 0.2), Math.floor(a.h * 0.16), Math.floor(a.w * 0.8), Math.floor(a.h * 0.84)]);
      const eye = await g.lua('local s = require("game.view.state"); local e = s.eye; return math.sqrt(e.x*e.x + e.y*e.y + e.z*e.z) - s.R');
      out.zooms.push({ zoom: z, eye_alt: Math.round(eye * 1000) / 1000, hidden: Math.round(d.share * 1000) / 1000, mean: Math.round(d.mean * 10) / 10 });
      console.log(`  zoom ${z.toFixed(2)}  eye ${eye.toFixed(2)} above the sea  cloud hides ${(d.share * 100).toFixed(0)}% of the middle of the frame (mean change ${d.mean.toFixed(0)})`);
    }
  },

  // A meteor straight onto a village's square.
  async meteor_village(g, dir, out) {
    const sid = Number(getFlag(args, '--sett', 1));
    await g.cmd({ cmd: 'v3_goto', lh: 11, days: 1 });
    const sq = await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 1.35 });
    const t0 = await g.hour();
    out.before = await g.lua(STATS(sid, sq.x, sq.y, 0.5, t0));
    out.shots = [await g.shoot(dir, '0_before_close')];
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 0.85 });
    out.shots.push(await g.shoot(dir, '0_before_mid'));
    await g.cmd({ cmd: 'power', power: 'meteor', cell: sq.cell, sid, x: sq.x, y: sq.y });
    // (the rock falling and striking, in real time on the paused world - the view's own effect)
    out.shots.push(await g.shoot(dir, '1a_falling', 250, false));
    out.shots.push(await g.shoot(dir, '1b_strike', 300, false));
    out.shots.push(await g.shoot(dir, '1c_after_flash', 300, false));
    await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.05 });
    out.immediate = await g.lua(STATS(sid, sq.x, sq.y, 0.5, t0));
    out.shots.push(await g.shoot(dir, '1_immediate_mid'));
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 1.35 });
    out.shots.push(await g.shoot(dir, '1_immediate_close'));
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 0.55 });
    out.shots.push(await g.shoot(dir, '1_immediate_planet'));
    for (const h of [0.5, 1, 2, 3]) await g.cmd({ cmd: 'v3_goto', hour: t0 + h });
    out.after3h = await g.lua(STATS(sid, sq.x, sq.y, 0.5, t0));
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 1.35 });
    out.shots.push(await g.shoot(dir, '2_after3h_close'));
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 0.85 });
    out.shots.push(await g.shoot(dir, '2_after3h_mid'));
    for (let h = 6; h <= 48; h += 6) await g.cmd({ cmd: 'v3_goto', hour: t0 + h });
    out.after2d = await g.lua(STATS(sid, sq.x, sq.y, 0.5, t0));
    await g.cmd({ cmd: 'v3_look', sett: sid, what: 'square', zoom: 1.35 });
    out.shots.push(await g.shoot(dir, '3_after2d_close'));
    Object.assign(out, await newsAndChain(g));
    await g.cmd({ cmd: 'actions', open: true, ev: 'latest' });
    out.shots.push(await g.shoot(dir, '4_your_actions'));
  },

  // Lightning on a herd at pasture, aimed by a click on a beast in the middle of it (the bar armed as a player does).
  async lightning_herd(g, dir, out) {
    const sid = Number(getFlag(args, '--sett', 1));
    const herd = getFlag(args, '--herd', 'flock');
    await g.cmd({ cmd: 'v3_goto', lh: 10, days: 1 });
    let t0 = await g.hour();
    let H = await g.lua(`return require('game.platform.accept').herd(W, ${sid}, "${herd}", now)`);
    // (out at pasture: wait for it)
    for (let k = 0; k < 12 && !(H.acts.graze || H.acts.walk || H.acts.rest); k++) {
      await g.advance(1);
      H = await g.lua(`return require('game.platform.accept').herd(W, ${sid}, "${herd}", now)`);
    }
    t0 = await g.hour();
    // a beast in the open (nothing taller near it) with the most of its herd about it - as a player picks one
    const bz = await g.cmd({ cmd: 'v3_beasts', sett: sid });
    const grp = herd;
    let best = null, bn = -1;
    for (const b of bz.beasts) {
      if (b.grp !== grp || !b.exposed) continue;
      let n = 0;
      for (const e of H.list) if ((e.x - b.x) ** 2 + (e.y - b.y) ** 2 < 0.0036) n++;
      if (n > bn) { bn = n; best = { k: b.k, x: b.x, y: b.y }; }
    }
    if (!best) { let bd = 1e9; for (const e of H.list) { const d = (e.x - H.x) ** 2 + (e.y - H.y) ** 2; if (d < bd) { best = e; bd = d; } } }
    out.neighbours = bn;
    out.herd_before = { n: H.n, acts: H.acts, spread: H.spread };
    out.aim = best;
    await g.cmd({ cmd: 'v3_look', sett: sid, x: best.x, y: best.y, zoom: Number(getFlag(args, '--zoom', 1.42)) });
    out.before = await g.lua(STATS(sid, best.x, best.y, 0.15, t0));
    out.shots = [await g.shoot(dir, '0_before', 1500)];
    const vp = g.page.viewportSize();
    const sc = await g.cmd({ cmd: 'v3_screen', sett: sid, grp: herd, k: best.k });
    const px = sc.sx * vp.width / sc.w, py = (sc.h - sc.sy) * vp.height / sc.h;
    await g.cmd({ cmd: 'arm', index: 1 });
    await press(g, px, py);
    await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.02 });
    out.immediate = await g.lua(STATS(sid, best.x, best.y, 0.15, t0));
    out.shots.push(await g.shoot(dir, '1_immediate'));
    await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.25 });
    const H2 = await g.lua(`return require('game.platform.accept').herd(W, ${sid}, "${herd}", now)`);
    out.herd_15min = { n: H2.n, acts: H2.acts, spread: H2.spread, moved: Math.hypot(H2.x - H.x, H2.y - H.y) };
    out.shots.push(await g.shoot(dir, '2_15min'));
    await g.cmd({ cmd: 'v3_goto', hour: t0 + 1 });
    out.after1h = await g.lua(STATS(sid, best.x, best.y, 0.15, t0));
    out.shots.push(await g.shoot(dir, '3_1h'));
    Object.assign(out, await newsAndChain(g));
  },
};

// (S24) fire cases ------------------------------------------------------------------------------------------
const HERD_FIRE = (sid, kind) => `return require('game.platform.accept').herd_fire(W, ${sid}, "${kind}", now)`;
const FIRE_SCALES = (sid) => `return require('game.platform.accept').fire_scales(W, ${sid}, now)`;
const whys = (o) => Object.entries(o.whys || {}).sort((a, b) => b[1] - a[1]).slice(0, 4).map(([k, v]) => `${v} ${k}`).join('; ');

// A stand of trees alight beside a herd at pasture on a dry morning: the beasts notice, run each its own way and
// keep away while it burns.
CASES.fire_herd = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  const herd = getFlag(args, '--herd', 'flock');
  await g.cmd({ cmd: 'v3_goto', lh: 9, days: 1 });
  const t0 = await g.hour();
  const pr = await g.cmd({ cmd: 'v3_premise', sett: sid, kind: 'pasture_fire', herd, dry: 0.15, off: 0.15, n: 6 });
  out.premise = pr;
  const lx = (pr.x + pr.fx) / 2, ly = (pr.y + pr.fy) / 2;
  out.rows = [];
  out.shots = [];
  for (const [i, dt] of [[0, 0.01], [1, 0.1], [2, 0.3], [3, 1.0], [4, 3.0]]) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + dt });
    await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: 1.3 });
    out.shots.push(await g.shoot(dir, `${i}_+${dt}h`, 1200));
    const hf = await g.lua(HERD_FIRE(sid, herd));
    const row = `+${dt}h burning ${hf.burning} (trees ${hf.trees}, fields ${hf.plots}, roofs ${hf.blds}) | herd ${hf.n}: mean ${hf.mean.toFixed(2)} from the fire, nearest ${hf.min.toFixed(2)}, ${hf.near} within 0.2 | ${whys(hf)}`;
    out.rows.push(row);
    console.log('  ' + row);
  }
  Object.assign(out, await newsAndChain(g));
};

// Rain on a fire burning in dry woods by a village, and the same fire left alone (a twin world): the god's rain
// puts it out sooner and it burns less.
CASES.rain_fire = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  const rain = !hasFlag(args, '--no-rain');
  await g.cmd({ cmd: 'v3_goto', lh: 10, days: 1 });
  const t0 = await g.hour();
  const pr = await g.cmd({ cmd: 'v3_premise', sett: sid, kind: 'pasture_fire', herd: 'flock', dry: 0.12, off: 0.3, n: 14 });
  const lx = pr.fx, ly = pr.fy;
  await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.75 });
  await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: 1.25 });
  out.shots = [await g.shoot(dir, '0_burning', 1200)];
  out.before = await g.lua(FIRE_SCALES(sid));
  const look = await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: 1.25 });
  if (rain) await g.cmd({ cmd: 'power', power: 'rain', cell: look.cell });
  out.rows = [];
  for (const dt of [1, 2, 4, 8, 16]) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.75 + dt });
    const fs_ = await g.lua(FIRE_SCALES(sid));
    const st = await g.lua(STATS(sid, lx, ly, 0.6, t0));
    const row = `+${dt}h after ${rain ? 'the rain' : '(no rain)'}: burning ${fs_.burning}, fires so far ${st.fires}, trees standing near ${st.trees_near}, burnt houses ${st.burnt} | planet ${fs_.cells.join(' ')}`;
    out.rows.push(row);
    console.log('  ' + row);
    if (dt === 2) { await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: 1.25 }); out.shots.push(await g.shoot(dir, `1_+${dt}h`, 1200)); }
  }
  await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: 1.25 });
  out.shots.push(await g.shoot(dir, '2_after', 1200));
  Object.assign(out, await newsAndChain(g));
};

// A big fire in the woods by a village, seen at three scales: what the map burns and what the planet shows on the
// cells the map owns must tell the same story.
CASES.fire_scales = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  await g.cmd({ cmd: 'v3_goto', lh: 12, days: 1 });
  const t0 = await g.hour();
  const pr = await g.cmd({ cmd: 'v3_premise', sett: sid, kind: 'pasture_fire', herd: 'flock', dry: 0.1, off: 0.5, n: 30 });
  const lx = pr.fx, ly = pr.fy;
  out.rows = [];
  out.shots = [];
  for (const dt of [0.5, 3, 8]) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + dt });
    const fs_ = await g.lua(FIRE_SCALES(sid));
    const row = `+${dt}h: the map burns ${fs_.burning} things | the planet's fire on its cells (* the map's own): ${fs_.cells.join(' ') || 'none'}`;
    out.rows.push(row);
    console.log('  ' + row);
    for (const z of [1.25, 0.85, 0.55]) {
      await g.cmd({ cmd: 'v3_look', sett: sid, x: lx, y: ly, zoom: z });
      out.shots.push(await g.shoot(dir, `${dt}h_z${z}`, 1000));
    }
  }
  Object.assign(out, await newsAndChain(g));
};

// (S24) A herd watched as the eye sees it: every ten game minutes for some hours its shape (nearest neighbours, spread,
// clumps, walkers and how alike their headings are, its middle against the herder) and a shot from above every half hour.
const SHAPE = (sid, kind) => `return require('game.platform.accept').herd_shape(W, ${sid}, "${kind}", now)`;
CASES.herd_watch = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  const herd = getFlag(args, '--herd', 'flock');
  const hours = Number(getFlag(args, '--hours', 3));
  await g.cmd({ cmd: 'v3_goto', lh: Number(getFlag(args, '--lh', 9)), days: 1, sett: sid });
  let sh = await g.lua(SHAPE(sid, herd));
  for (let k = 0; k < 10 && !(sh.n > 5 && sh.herder_d != null); k++) { await g.advance(0.5); sh = await g.lua(SHAPE(sid, herd)); }
  const t0 = await g.hour();
  out.rows = [];
  out.shots = [];
  const f3 = (v) => (v == null ? '-' : v.toFixed(3));
  for (let m = 0; m <= hours * 60; m += 10) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + m / 60 });
    sh = await g.lua(SHAPE(sid, herd));
    const row = `+${m}min n ${sh.n} nn med ${f3(sh.nn_med)} p10 ${f3(sh.nn_p10)} (sep ${f3(sh.sep)}) rg ${f3(sh.rg)} clumps ${sh.clumps} walking ${(100 * (sh.walking || 0)).toFixed(0)}% align ${f3(sh.align)} herder ${f3(sh.herder_d)}${sh.herder_walking ? ' (walking)' : ''}`;
    out.rows.push(row);
    console.log('  ' + row);
    if (m % 30 === 0 && sh.cx != null) {
      await g.cmd({ cmd: 'v3_look', sett: sid, x: sh.cx, y: sh.cy, zoom: Number(getFlag(args, '--zoom', 1.2)) });
      out.shots.push(await g.shoot(dir, `m${String(m).padStart(3, '0')}`, 900));
    }
  }
};

// (S24) A herd on the move: from just before its pen opens in the morning, a reading and a shot every few game
// minutes as it goes out with its herder (does it go as a ball, or string out behind its leaders?).
CASES.herd_move = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  const herd = getFlag(args, '--herd', 'flock');
  await g.cmd({ cmd: 'v3_goto', lh: Number(getFlag(args, '--lh', 6.5)), days: 1, sett: sid });
  let sh = await g.lua(SHAPE(sid, herd));
  // (wait for the first walkers: the gate opened)
  for (let k = 0; k < 60 && !(sh.walking > 0.2); k++) { await g.advance(0.05); sh = await g.lua(SHAPE(sid, herd)); }
  const t0 = await g.hour();
  out.rows = [];
  out.shots = [];
  const f3 = (v) => (v == null ? '-' : v.toFixed(3));
  const step = Number(getFlag(args, '--step', 3));
  for (let m = 0; m <= Number(getFlag(args, '--minutes', 36)); m += step) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + m / 60 });
    sh = await g.lua(SHAPE(sid, herd));
    const row = `+${m}min n ${sh.n} nn med ${f3(sh.nn_med)} rg ${f3(sh.rg)} long ${f3(sh.elong)} clumps ${sh.clumps} walking ${(100 * (sh.walking || 0)).toFixed(0)}% align ${f3(sh.align)} herder ${f3(sh.herder_d)}${sh.herder_walking ? ' (walking)' : ''}`;
    out.rows.push(row);
    console.log('  ' + row);
    if (sh.cx != null) {
      await g.cmd({ cmd: 'v3_look', sett: sid, x: sh.cx, y: sh.cy, zoom: Number(getFlag(args, '--zoom', 1.4)) });
      out.shots.push(await g.shoot(dir, `m${String(m).padStart(3, '0')}`, 700));
    }
  }
};

// (S24) A herd seen close, as a player looking at it would: its middle from near (two zooms), every ten game minutes.
CASES.herd_close = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  const herd = getFlag(args, '--herd', 'flock');
  await g.cmd({ cmd: 'v3_goto', lh: Number(getFlag(args, '--lh', 10)), days: 1, sett: sid });
  out.rows = [];
  out.shots = [];
  const f3 = (v) => (v == null ? '-' : v.toFixed(3));
  const t0 = await g.hour();
  for (let m = 0; m <= Number(getFlag(args, '--minutes', 40)); m += 10) {
    await g.cmd({ cmd: 'v3_goto', hour: t0 + m / 60 });
    const sh = await g.lua(SHAPE(sid, herd));
    const row = `+${m}min n ${sh.n} nn med ${f3(sh.nn_med)} p10 ${f3(sh.nn_p10)} rg ${f3(sh.rg)} long ${f3(sh.elong)} clumps ${sh.clumps} walking ${(100 * (sh.walking || 0)).toFixed(0)}% align ${f3(sh.align)}`;
    out.rows.push(row);
    console.log('  ' + row);
    if (sh.cx != null) {
      for (const z of [1.6, 1.85]) {
        await g.cmd({ cmd: 'v3_look', sett: sid, x: sh.cx, y: sh.cy, zoom: z });
        out.shots.push(await g.shoot(dir, `m${String(m).padStart(3, '0')}_z${z}`, 700));
      }
    }
  }
};

// (S24) The sea's fish as the player sees them: the fish field on the cells off a fishing village and the schools drawn
// for it, at a few zooms.
CASES.fish_look = async (g, dir, out) => {
  const sid = Number(getFlag(args, '--sett', 1));
  await g.cmd({ cmd: 'v3_goto', lh: 11, days: 1, sett: sid });
  const look = await g.cmd({ cmd: 'v3_look', sett: sid, what: 'sea', zoom: 1.0 });
  const x = look.x + Number(getFlag(args, '--dx', 0)), y = look.y + Number(getFlag(args, '--dy', 0));
  out.fish = await g.lua(`local s = W.S.v3.settlements[${sid}]; local o = {}; for _, c in ipairs(require('sim.society.sites').rings(W, s.cell, 2)) do if W.geo.ocean[c] == 1 then o[#o + 1] = string.format('%d:%.2f', c, W.F.fish[c]) end end; return table.concat(o, ' ')`);
  console.log('  fish field: ' + out.fish);
  out.shots = [];
  for (const z of [0.85, 1.0, 1.15, 1.3]) {
    await g.cmd({ cmd: 'v3_look', sett: sid, x, y, zoom: z });
    out.shots.push(await g.shoot(dir, `z${z}`, 1200));
    const st = await g.lua(`local f = require('game.view.fish'); return f.stats`);
    console.log(`  zoom ${z}: ${st.schools} schools drawn over ${st.cells} cells`);
  }
};

// (S24) Every power in its contexts: a state the world could reach (set as a premise), the cast through the game's
// own power path, the world read before, at once and after, the scene shot close and from afar, the news and the
// chain. Each spec: sid, power (+ mode / species), lh, setup (Lua run first: a state), premise (named), what/x/y (where
// the camera looks and the cast lands), offset (from that point), zoom, far (a far zoom), after (hours), read (extra Lua).
const ENV = (sid) => `return require('game.platform.accept').env(W, ${sid}, now)`;
const envline = (e) => {
  const f = Object.entries(e.fields || {}).map(([k, v]) => `${k} ${v}`).join(', ');
  return `soil ${e.soil.toFixed(2)} temp ${e.temp.toFixed(1)} (anom ${e.anom.toFixed(1)}) rain ${e.rain_now} river x${e.river.toFixed(1)} flood ${e.flood} snow ${e.snow.toFixed(3)}`
    + ` | fields: ${f}; blight ${e.blighted} worst ${e.blight.toFixed(2)} | beasts ${e.beasts} sick ${e.sick}+${e.sick_inc} | wild ${e.wild} wolves ${e.wolves}`
    + ` | sea ${e.sea_t == null ? '-' : e.sea_t.toFixed(1)}C fish ${e.fish == null ? '-' : e.fish.toFixed(2)} oil ${e.oil.toFixed(2)} shore ${e.shore == null ? '-' : e.shore.toFixed(1)} gulls ${e.gulls} (oiled ${e.gulls_oiled})`
    + ` | aerosol ${e.aerosol.toFixed(2)}`;
};
const SETUP = {
  drought: (sid) => `local s = W.S.v3.settlements[${sid}]; for _, c in ipairs(require('sim.powers').area(W, s.cell, 2)) do W.F.soil_water[c] = 0.08 end; for _, q in ipairs(W.S.v3.settlements) do q.wx = nil end; return 1`,
  soaked: (sid) => `local s = W.S.v3.settlements[${sid}]; for _, c in ipairs(require('sim.powers').area(W, s.cell, 3)) do W.F.soil_water[c] = 0.66 end; return 1`,
  dry: (sid) => `local s = W.S.v3.settlements[${sid}]; W.F.soil_water[s.cell] = 0.12; return 1`,
};
const PSPECS = {
  lightning_tree_dry: { sid: 1, power: 'lightning', lh: 13, setup: SETUP.dry(1), what: 'tree_near_house', zoom: 1.3, after: [1, 4] },
  lightning_house: { sid: 1, power: 'lightning', lh: 14, setup: SETUP.dry(1), what: 'house', zoom: 1.35, after: [1, 6] },
  lightning_cattle: { sid: 1, power: 'lightning', lh: 15, herd: 'cattle', after: [0.25, 1] },
  rain_drought: { sid: 4, power: 'rain', lh: 10, setup: SETUP.drought(4), zoom: 1.25, far: 0.8, after: [3, 24, 72] },
  rain_soaked: { sid: 5, power: 'rain', lh: 10, setup: SETUP.soaked(5), what: 'river', zoom: 1.1, far: 0.8, after: [6, 24, 48] },
  dry_spell: { sid: 5, power: 'dry', lh: 10, zoom: 1.25, after: [24, 96, 144] },
  cold_crops: { sid: 6, power: 'cold', lh: 10, zoom: 1.25, far: 0.8, after: [12, 48, 96] },
  warm_snow: { sid: 6, power: 'warm', lh: 10, premise: 'cold_snap', prelive: 48, zoom: 1.25, far: 0.8, after: [12, 48] },
  stir_mountain: { sid: 4, power: 'stir', lh: 10, what: 'volcano', zoom: 0.95, far: 0.6, after: [24, 96],
    read: `local o = {} for i, p in ipairs(require('game.view.fx').plume_steam) do if p.on then o[#o + 1] = i end end return #o` },
  strain_coast: { sid: 1, power: 'strain', lh: 10, zoom: 1.2, far: 0.6, after: [24, 72] },
  // (S24) the god's bolt on each kind of target (Arjun's list): a wild deer, a wolf (a pack brought first), a dog, a person
  // out of doors, a dry field, wet ground
  lightning_deer: { sid: 4, power: 'lightning', lh: 10, aim: { what: 'wild' }, zoom: 1.4, after: [0.25, 1] },
  lightning_wolf: { sid: 1, power: 'lightning', lh: 21, precast: { power: 'bring', species: 'wolves' }, prelive: 2, aim: { what: 'wild', sp: 'wolf', wait: 30 }, zoom: 1.4, after: [0.25, 1] },
  lightning_dog: { sid: 1, power: 'lightning', lh: 10, aim: { what: 'dog' }, zoom: 1.45, after: [0.25, 1] },
  lightning_person: { sid: 1, power: 'lightning', lh: 11, aim: { what: 'person' }, zoom: 1.45, after: [0.25, 2] },
  lightning_field_dry: { sid: 1, power: 'lightning', lh: 13, setup: SETUP.dry(1), field: true, zoom: 1.35, after: [1, 4] },
  lightning_field_ripe: { sid: 6, power: 'lightning', lh: 14, days: 0, setup: SETUP.dry(6), field: 'ripe', zoom: 1.35, after: [1, 4],
    read: `local s = W.S.v3.settlements[6]; local d; for _, q in ipairs(W.S.v3.deeds) do if q.kind == "strike" and q.at == 6 then d = q end end; local b = require('sim.agents.blaze'); local m = require('sim.agents.map'); local n = 0; for _, f in ipairs(s.burn or {}) do n = n + 1 end; local i = d and m.tile_of(s, d.x, d.y) or 0; local pk = i > 0 and s.plot[i] or 0; local pl = pk > 0 and s.plots[pk]; return string.format("what %s at %.3f,%.3f plot %d fuel %.2f stage %s dry %.2f rain %s burning %d now %.3f t %.3f", d and d.what or "-", d and d.x or 0, d and d.y or 0, pk, pl and b.plot_fuel(pl) or -1, pl and pl.stage or "-", b.dryness(W, s), tostring(s.rain_now), n, now, d and d.t or 0)` },
  lightning_wet: { sid: 1, power: 'lightning', lh: 13, setup: SETUP.soaked(1), what: 'tree_near_house', zoom: 1.35, after: [1, 4] },
  // (S24) second contexts for the powers seen once
  dry_tekheim: { sid: 1, power: 'dry', lh: 10, zoom: 1.2, far: 0.8, after: [24, 96, 144] },
  cold_mountain: { sid: 4, power: 'cold', lh: 22, zoom: 1.2, far: 0.8, after: [6, 24, 72] },
  warm_on_snow: { sid: 1, power: 'warm', lh: 11, premise: 'cold_snap', prelive: 48, zoom: 1.2, far: 0.8, after: [12, 48] },
  sickness_cattle: { sid: 1, power: 'sickness', lh: 10, what: 'byre', zoom: 1.4, after: [24, 72, 144] },
  blight_krikeivorn: { sid: 6, power: 'blight', lh: 10, field: 'ripe', zoom: 1.35, after: [48, 144] },
  sea_warm_tekheim: { sid: 1, power: 'sea', mode: 'warm', lh: 10, what: 'sea', zoom: 1.0, far: 0.6, after: [48, 192] },
  oil_tekheim: { sid: 1, power: 'oil', lh: 10, what: 'sea', zoom: 1.0, far: 0.6, after: [24, 96, 192] },
  // (S24, matrix I) a mountain stirred twice - past what makes it erupt - and watched through its eruption
  volcano_erupt: { sid: 4, power: 'stir', casts: 2, lh: 10, what: 'volcano', zoom: 0.95, far: 0.6, after: [24, 48, 96] },
  strain_inland: { sid: 4, power: 'strain', lh: 10, zoom: 1.2, far: 0.6, after: [24, 72] },
  cull_wolves: { sid: 4, power: 'cull', species: 'wolves', lh: 22, zoom: 1.1, far: 0.7, after: [24, 96] },
  cull_deer: { sid: 4, power: 'cull', species: 'deer', lh: 10, what: 'wild', zoom: 1.2, far: 0.7, after: [24, 96] },
  bring_wolves: { sid: 1, power: 'bring', species: 'wolves', lh: 18, zoom: 1.1, far: 0.7, after: [24, 72] },
  bring_fish: { sid: 1, power: 'bring', species: 'fish', lh: 10, what: 'dock', offset: [0.3, 0], sea: true, zoom: 1.0, far: 0.6, after: [24, 96] },
  sickness_flock: { sid: 1, power: 'sickness', lh: 10, what: 'flock', zoom: 1.4, after: [24, 72, 144] },
  blight_field: { sid: 1, power: 'blight', lh: 10, field: true, zoom: 1.35, after: [48, 144, 240] },
  sea_warm: { sid: 2, power: 'sea', mode: 'warm', lh: 10, what: 'dock', offset: [0.4, 0], sea: true, zoom: 1.0, far: 0.55, after: [48, 192, 384] },
  oil_shore: { sid: 2, power: 'oil', lh: 10, what: 'dock', offset: [0.6, 0], sea: true, zoom: 1.0, far: 0.55, after: [24, 96, 192] },
  meteor_land: { sid: 4, power: 'meteor', lh: 11, offset: [2.2, 0.4], zoom: 1.0, far: 0.6, after: [3, 48] },
  meteor_forest: { sid: 4, power: 'meteor', lh: 11, offset: [0.9, 0.0], zoom: 1.2, far: 0.7, after: [3, 48] },
  meteor_ocean: { sid: 1, power: 'meteor', lh: 11, what: 'dock', offset: [0.9, 0], sea: true, zoom: 1.0, far: 0.6, after: [3, 24] },
};

async function pcase(g, dir, out, sp) {
  const sid = sp.sid;
  await g.cmd({ cmd: 'v3_goto', lh: sp.lh ?? 10, days: sp.days ?? 1, sett: sid });
  if (sp.setup) await g.lua(sp.setup);
  if (sp.premise) await g.cmd({ cmd: 'v3_premise', sett: sid, kind: 'named', name: sp.premise });
  if (sp.precast) await g.cmd({ cmd: 'power', ...sp.precast, cell: await g.lua(`return W.S.v3.settlements[${sid}].cell`) });
  if (sp.prelive) await g.advance(sp.prelive);
  let t0 = await g.hour();
  out.rows = [];
  out.shots = [];
  // where: a point the spec names (a herd's beast in the open, a growing field, the square, a building...)
  let look;
  let ax, ay;
  if (sp.aim) {
    // (S24) a target the aimed bolt takes itself: a person out of doors, a wild deer, a wolf, a dog (waiting for one)
    const q = `return require('game.platform.accept').aim_target(W, ${sid}, "${sp.aim.what}", ${sp.aim.sp ? `"${sp.aim.sp}"` : 'nil'}, now)`;
    let f = await g.lua(q);
    for (let k = 0; k < (sp.aim.wait || 12) && !f; k++) { await g.advance(1); f = await g.lua(q); }
    if (!f) throw new Error(`no ${sp.aim.sp || sp.aim.what} the bolt would take`);
    out.target = f.what;
    ax = f.x; ay = f.y;
    look = await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.4 });
  } else if (sp.herd) {
    const bz = await g.cmd({ cmd: 'v3_beasts', sett: sid });
    let best = null, bn = -1;
    for (const b of bz.beasts) {
      if (b.grp !== sp.herd || !b.exposed) continue;
      let n = 0;
      for (const o of bz.beasts) if (o.grp === sp.herd && (o.x - b.x) ** 2 + (o.y - b.y) ** 2 < 0.01) n++;
      if (n > bn) { bn = n; best = b; }
    }
    if (!best) throw new Error(`no ${sp.herd} out in the open`);
    ax, ay = 0, 0;
    ax = best.x; ay = best.y;
    look = await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.4 });
  } else if (sp.field) {
    const stage = sp.field === 'ripe' ? 'ripe' : 'growing';
    const f = await g.lua(`local s = W.S.v3.settlements[${sid}]; for _, pl in ipairs(s.plots) do if pl.alive and pl.stage == "${stage}" and ("${stage}" ~= "ripe" or require('sim.agents.blaze').plot_fuel(pl) > 0.3) then return { x = (pl.x0 + pl.x1) / 2, y = (pl.y0 + pl.y1) / 2 } end end; return nil`);
    if (!f) throw new Error(`no ${stage} field`);
    ax = f.x; ay = f.y;
    look = await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.35 });
  } else {
    look = await g.cmd({ cmd: 'v3_look', sett: sid, what: sp.what || 'square', zoom: sp.zoom || 1.3 });
    ax = look.x + (sp.offset ? sp.offset[0] : 0);
    ay = look.y + (sp.offset ? sp.offset[1] : 0);
    if (sp.offset) look = await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.3 });
  }
  // the cell of the point (the cast's cell: from the map's tile, or off the map the nearest cell)
  const cell = await g.lua(`local s = W.S.v3.settlements[${sid}]; local m = require('sim.agents.map'); local i = m.tile_of(s, ${ax}, ${ay}); if i > 0 then return s.cell_of[i] end; local ux, uy, uz = m.unit(s, ${ax}, ${ay}); local g = W.grid; local best, bd = s.cell, -2; for c = 1, W.n do local d = g.X[c] * ux + g.Y[c] * uy + g.Z[c] * uz; if d > bd then best, bd = c, d end end; return best`);
  out.aim = { x: ax, y: ay, cell };
  const env0 = await g.lua(ENV(sid));
  out.before = envline(env0);
  out.stats0 = await g.lua(STATS(sid, ax, ay, 0.5, t0));
  console.log(`  before: ${out.before}`);
  out.shots.push(await g.shoot(dir, '0_before', 1000));
  if (sp.far) { await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.far }); out.shots.push(await g.shoot(dir, '0_before_far', 900)); }
  const prev = await g.cmd({ cmd: 'lua', code: `return require('sim.powers3').preview(W, "${sp.power}", ${cell}, { sid = ${sid}, x = ${ax}, y = ${ay}, mode = ${sp.mode ? `"${sp.mode}"` : 'nil'}, species = ${sp.species ? `"${sp.species}"` : 'nil'} })` });
  out.preview = prev.v;
  console.log(`  aim preview: ${out.preview}`);
  // the cast: the same path as the bar (bridge power = powers.cast)
  const onmap = await g.lua(`local s = W.S.v3.settlements[${sid}]; return require('sim.agents.map').tile_of(s, ${ax}, ${ay}) > 0`);
  for (let q = 0; q < (sp.casts || 1); q++) {
    await g.cmd({ cmd: 'power', power: sp.power, cell, sid: onmap ? sid : undefined, x: onmap ? ax : undefined, y: onmap ? ay : undefined, mode: sp.mode, species: sp.species });
  }
  t0 = await g.hour();
  await g.cmd({ cmd: 'v3_goto', hour: t0 + 0.05 });
  const env1 = await g.lua(ENV(sid));
  out.immediate = envline(env1);
  out.stats1 = await g.lua(STATS(sid, ax, ay, 0.5, t0));
  console.log(`  at once: ${out.immediate}`);
  await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.3 });
  out.shots.push(await g.shoot(dir, '1_at_once', 1000));
  if (sp.read) { out.read1 = await g.lua(sp.read); console.log(`  read at once: ${JSON.stringify(out.read1)}`); }
  if (sp.far) { await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.far }); out.shots.push(await g.shoot(dir, '1_at_once_far', 900)); }
  let last = 0;
  for (const h of sp.after || []) {
    for (let k = last + 6; k < h; k += 6) await g.cmd({ cmd: 'v3_goto', hour: t0 + k });
    await g.cmd({ cmd: 'v3_goto', hour: t0 + h });
    last = h;
    const e = await g.lua(ENV(sid));
    const st = await g.lua(STATS(sid, ax, ay, 0.5, t0));
    const row = `+${h}h: ${envline(e)} || people ${st.people} dead ${st.dead} hurt ${st.hurt} | standing ${st.standing} burnt ${st.burnt} fell ${st.fallen} | beasts ${st.beasts} dead ${st.beasts_dead} | burning ${st.burning}`;
    out.rows.push(row);
    console.log('  ' + row);
    await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.zoom || 1.3 });
    out.shots.push(await g.shoot(dir, `2_+${h}h`, 1000));
    if (sp.far) { await g.cmd({ cmd: 'v3_look', sett: sid, x: ax, y: ay, zoom: sp.far }); out.shots.push(await g.shoot(dir, `2_+${h}h_far`, 900)); }
  }
  Object.assign(out, await newsAndChain(g));
}
for (const [name, sp] of Object.entries(PSPECS)) CASES[name] = (g, dir, out) => pcase(g, dir, out, sp);

const names = only.length ? only : Object.keys(CASES);
for (const name of names) {
  const fn = CASES[name];
  if (!fn) { console.log(`no case ${name}`); continue; }
  const dir = path.join(base, name);
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
  const out = { case: name, tag, at: new Date().toISOString() };
  const g = await openGame({ port, gpu: !hasFlag(args, '--swiftshader'), log: (t) => console.log('[page] ' + t.slice(0, 300)) });
  try {
    await g.cmd({ cmd: 'ui', debug: false });
    console.log(`== ${name}`);
    await fn(g, dir, out);
  } catch (e) {
    out.error = String(e && e.stack || e);
    console.log(`== ${name} FAILED: ${e.message}`);
  } finally {
    out.console = g.consoleLines.filter((l) => /error|attempt to|traceback/i.test(l)).slice(0, 20);
    await g.close();
  }
  report[name] = out;
  for (const k of ['before', 'immediate', 'after3h', 'after1h', 'after2d', 'herd_before', 'herd_15min']) if (out[k]) console.log(`  ${k}: ${fmt(out[k])}`);
  for (const k of ['news', 'actions', 'chain']) if (out[k]) { console.log(`  ${k}:`); for (const l of out[k]) console.log(`    ${l}`); }
  fs.writeFileSync(reportFile, JSON.stringify(report, null, 1));
}
