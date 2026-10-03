// node scripts/gen_audio.mjs [--only id,id] [--force] [--dry] [--defold-only]
// (S21, docs/v3/80_WORLD_AUDIO.md) The world's sounds from assets/audio/manifest.json:
//   1. each sound (and each take of it) generated with the ElevenLabs sound effects API as raw PCM
//      (44.1 kHz mono: no MP3 padding to break a seamless loop), kept in out/audio_cache;
//   2. levelled with ffmpeg to its group's loudness (static gain, peaks under -1 dB; one-shots trimmed of
//      silence at both ends, loops never trimmed) and encoded Ogg Vorbis to assets/audio/<group>/;
//   3. the Defold side generated: a .sound component per file (game/audio/snd/), the game object holding
//      them all (game/audio/sounds.go) and a Lua catalog (game/audio/catalog.lua: ids, takes, loops).
// The key is read from .env (ELEVENLABS_API_KEY) and never printed or written anywhere. A sound already
// encoded is skipped (--force to make it again). Idempotent: rerun after adding to the manifest.
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync, spawnSync } from 'node:child_process';
import { ROOT, getFlag, hasFlag } from './lib/util.mjs';

const args = process.argv.slice(2);
const only = getFlag(args, '--only', null);
const onlySet = only ? new Set(String(only).split(',')) : null;
const force = hasFlag(args, '--force');
const dry = hasFlag(args, '--dry');
const defoldOnly = hasFlag(args, '--defold-only');

const manifest = JSON.parse(fs.readFileSync(path.join(ROOT, 'assets/audio/manifest.json'), 'utf8'));
const cacheDir = path.join(ROOT, 'out', 'audio_cache');
fs.mkdirSync(cacheDir, { recursive: true });

function readKey() {
  let txt = '';
  try { txt = fs.readFileSync(path.join(ROOT, '.env'), 'utf8'); } catch { return null; }
  for (const line of txt.split(/\r?\n/)) {
    const m = line.match(/^\s*ELEVENLABS_API_KEY\s*=\s*(.*)\s*$/);
    if (m) return m[1].replace(/^['"]|['"]$/g, '').trim();
  }
  return null;
}

const TARGET = { ambient: -23, sfx: -19, ui: -20 };   // mean loudness (dB) by group
const outOf = (s, k) => path.join(ROOT, 'assets', 'audio', s.group, (s.variants && s.variants > 1) ? `${s.id}_${k}.ogg` : `${s.id}.ogg`);

function ffmpeg(argv) {
  const r = spawnSync('ffmpeg', ['-hide_banner', '-nostats', ...argv], { encoding: 'utf8' });
  if (r.status !== 0) throw new Error('ffmpeg failed: ' + (r.stderr || '').split('\n').slice(-4).join(' | '));
  return r.stderr || '';
}

function levels(pcm, pre) {
  const err = ffmpeg(['-f', 's16le', '-ar', '44100', '-ac', '1', '-i', pcm, '-af', (pre ? pre + ',' : '') + 'volumedetect', '-f', 'null', '-']);
  const mean = Number((err.match(/mean_volume:\s*(-?[\d.]+)/) || [])[1]);
  const max = Number((err.match(/max_volume:\s*(-?[\d.]+)/) || [])[1]);
  return { mean, max };
}

async function generate(key, s, k) {
  const url = `https://api.elevenlabs.io/v1/sound-generation?output_format=pcm_44100`;
  const body = { text: s.prompt, duration_seconds: s.dur, prompt_influence: s.pi ?? 0.4, loop: !!s.loop, model_id: manifest.model };
  for (let attempt = 1; attempt <= 3; attempt++) {
    const res = await fetch(url, { method: 'POST', headers: { 'xi-api-key': key, 'Content-Type': 'application/json', Accept: 'audio/*' }, body: JSON.stringify(body) });
    if (res.ok) return Buffer.from(await res.arrayBuffer());
    const msg = (await res.text()).slice(0, 240).replace(/\s+/g, ' ');
    console.log(`  ${s.id}#${k}: HTTP ${res.status} (try ${attempt}): ${msg}`);
    if (res.status === 422 && body.loop) { delete body.loop; continue; }   // (a model without seamless loops)
    if (res.status === 401 || res.status === 403) throw new Error('the API refused the key (401/403): check .env');
    await new Promise((r) => setTimeout(r, 2500 * attempt));
  }
  throw new Error(`${s.id}#${k}: no audio after 3 tries`);
}

function encode(s, pcm, out) {
  fs.mkdirSync(path.dirname(out), { recursive: true });
  // one-shots: silence trimmed at both ends; loops: untouched (the seam is the model's)
  const trim = s.loop ? '' : 'silenceremove=start_periods=1:start_threshold=-55dB,areverse,silenceremove=start_periods=1:start_threshold=-55dB,areverse';
  const lv = levels(pcm, trim);
  const target = TARGET[s.group] ?? -20;
  let gain = Number.isFinite(lv.mean) ? target - lv.mean : 0;
  if (Number.isFinite(lv.max)) gain = Math.min(gain, -1.0 - lv.max);
  gain = Math.max(-30, Math.min(24, gain));
  const af = (trim ? trim + ',' : '') + `volume=${gain.toFixed(2)}dB`;
  // (the beds are wind, water and leaves: 24 kHz at the lowest quality is plenty, half the size; the
  // actions keep 44.1 kHz)
  const enc = s.group === 'ambient' ? ['-ar', '24000', '-q:a', '0'] : ['-ar', '44100', '-q:a', s.group === 'ui' ? '3' : '2'];
  ffmpeg(['-y', '-f', 's16le', '-ar', '44100', '-ac', '1', '-i', pcm, '-af', af, '-c:a', 'libvorbis', ...enc, out]);
  return gain;
}

// The Defold side: a .sound per file, the game object, the catalog.
function defold() {
  const sndDir = path.join(ROOT, 'game', 'audio', 'snd');
  fs.mkdirSync(sndDir, { recursive: true });
  const comps = [];
  const takes = {}, loops = {}, groups = {};
  for (const s of manifest.sounds) {
    const n = s.variants && s.variants > 1 ? s.variants : 1;
    let have = 0;
    for (let k = 1; k <= n; k++) {
      const ogg = outOf(s, k);
      if (!fs.existsSync(ogg)) continue;
      have++;
      const name = path.basename(ogg, '.ogg');
      const rel = '/' + path.relative(ROOT, ogg).split(path.sep).join('/');
      fs.writeFileSync(path.join(sndDir, `${name}.sound`),
        `sound: "${rel}"\nlooping: ${s.loop ? 1 : 0}\ngroup: "${s.group}"\ngain: 1.0\npan: 0.0\nspeed: 1.0\nloopcount: 0\n`);
      comps.push(`components {\n  id: "${name}"\n  component: "/game/audio/snd/${name}.sound"\n}\n`);
    }
    if (have > 0) { takes[s.id] = have; groups[s.id] = s.group; if (s.loop) loops[s.id] = true; }
  }
  fs.writeFileSync(path.join(ROOT, 'game', 'audio', 'sounds.go'), comps.join(''));
  const lua = ['-- Generated by scripts/gen_audio.mjs from assets/audio/manifest.json: do not edit.',
    '-- takes[id] = how many takes (files id_1 .. id_n, or id when one); loops[id] = true for seamless loops.',
    'local M = { takes = {}, loops = {}, groups = {} }'];
  for (const id of Object.keys(takes).sort()) {
    lua.push(`M.takes.${id} = ${takes[id]}`);
    lua.push(`M.groups.${id} = "${groups[id]}"`);
    if (loops[id]) lua.push(`M.loops.${id} = true`);
  }
  lua.push('return M', '');
  fs.writeFileSync(path.join(ROOT, 'game', 'audio', 'catalog.lua'), lua.join('\n'));
  console.log(`defold: ${comps.length} sound components, ${Object.keys(takes).length} sounds in the catalog`);
}

if (!defoldOnly) {
  const key = readKey();
  if (!key && !dry) { console.log('No ELEVENLABS_API_KEY in .env: nothing generated (the Defold side is still written from what exists).'); }
  let made = 0, skipped = 0, failed = 0;
  for (const s of manifest.sounds) {
    if (onlySet && !onlySet.has(s.id)) continue;
    const n = s.variants && s.variants > 1 ? s.variants : 1;
    for (let k = 1; k <= n; k++) {
      const out = outOf(s, k);
      if (fs.existsSync(out) && !force) { skipped++; continue; }
      const pcm = path.join(cacheDir, `${s.id}_${k}.pcm`);
      if (dry) { console.log(`would make ${path.relative(ROOT, out)} (${s.dur} s${s.loop ? ', loop' : ''})`); continue; }
      try {
        if (!fs.existsSync(pcm) || force) {
          if (!key) { failed++; continue; }
          const buf = await generate(key, s, k);
          fs.writeFileSync(pcm, buf);
        }
        const g = encode(s, pcm, out);
        made++;
        console.log(`made ${path.relative(ROOT, out)} (${s.dur} s${s.loop ? ', loop' : ''}, gain ${g.toFixed(1)} dB, ${(fs.statSync(out).size / 1024).toFixed(0)} KB)`);
      } catch (e) {
        failed++;
        console.log(`FAILED ${s.id}#${k}: ${e.message}`);
      }
    }
  }
  console.log(`audio: ${made} made, ${skipped} already there, ${failed} failed`);
}
defold();
