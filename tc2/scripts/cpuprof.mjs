// (session 27) A CPU profile of the game's page in ms a frame (realmsperf --profile): busy or idle, by kind, the wasm's functions by name (a build made
// with `unity.mjs build-web --symbols` has Build/web.symbols.json[.unityweb]), their own time and their time with all they call.
// Run alone it summarises profiles taken before: node tc2/scripts/cpuprof.mjs F.cpuprofile [--frames N] [--symbols DIR] [--top 60]
// (DIR defaults to tc2/web-build/web/Build; the frames counted while it was taken are in the run's report.json, `profiles`).
import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import { fileURLToPath } from 'node:url';

/**
 * The wasm's function names by index: JSON {index: name} or "index:name" lines, brotli or gzip when the build compressed it (Unity: .unityweb);
 * null when the build has none. IL2CPP's names end in a hash of the method's signature (TerrainLod_Update_m2E59...): dropped.
 * (Session 27, context 4: the plain-text reading is tried last and only when nearly every line is "index:name" - tried first, it had taken chance
 * "digits:" runs in the brotli bytes for a map of names, and every frame stayed numbered.)
 */
export function wasmNames(dir) {
  const f = ['web.symbols.json', 'web.symbols.json.unityweb', 'web.symbols.json.br', 'web.symbols.json.gz'].map((n) => path.join(dir, n)).find((p) => fs.existsSync(p));
  if (!f) { console.log('(no symbols beside the build: the wasm functions stay numbered - build with unity.mjs build-web --symbols)'); return null; }
  const buf = fs.readFileSync(f);
  const clean = (s) => String(s).replace(/_m[0-9A-F]{40}$/, '');
  for (const un of [zlib.brotliDecompressSync, zlib.gunzipSync, (b) => b]) {
    let s; try { s = un(buf).toString('utf8'); } catch { continue; }
    try { const o = JSON.parse(s); if (o && typeof o === 'object') { const m = new Map(); for (const k of Object.keys(o)) m.set(Number(k), clean(o[k])); if (m.size) return m; } } catch { /* lines? */ }
    const lines = s.split('\n').filter((l) => l.trim().length > 0);
    const m = new Map();
    for (const l of lines) { const i = l.indexOf(':'); if (i > 0 && /^\d+$/.test(l.slice(0, i))) m.set(Number(l.slice(0, i)), clean(l.slice(i + 1).trim())); }
    if (lines.length > 0 && m.size >= 0.9 * lines.length) return m;
  }
  console.log('(the symbols file could not be read: ' + f + ')');
  return null;
}

/** A CPU profile (Chrome's Profiler.stop) in ms a frame over `frames` ({n, t}: frames counted and ms) - each sample's time counted once a function. */
export function summarise(profile, frames, names, title, top = 60) {
  const parent = new Map(); for (const n of profile.nodes) for (const c of n.children || []) parent.set(c, n.id);
  let named = 0, numbered = 0;
  const nameOf = (cf) => {
    const fn = cf.functionName || '';
    const wasm = (cf.url || '').startsWith('wasm://') || /^\$?func\d+$|^wasm-function\[\d+\]$/.test(fn);
    if (wasm) {
      const m = /(\d+)\]?$/.exec(fn); const i = m ? Number(m[1]) : -1; const nm = names && names.get(i);
      if (nm) named++; else numbered++;
      return { name: nm || fn || '(wasm)', kind: 'wasm' };
    }
    if (fn === '(idle)' || fn === '(program)' || fn === '(garbage collector)' || fn === '(root)') return { name: fn, kind: fn };
    return { name: (fn || '(anonymous)') + (cf.url ? ' ' + path.basename(cf.url.split('?')[0]) + ':' + (cf.lineNumber + 1) : ''), kind: 'js' };
  };
  const label = new Map(); for (const n of profile.nodes) label.set(n.id, nameOf(n.callFrame));
  const samples = profile.samples || [], deltas = profile.timeDeltas || [];
  const self = new Map(), incl = new Map(), kinds = new Map();
  let total = 0;
  for (let s = 0; s < samples.length; s++) {
    const dt = (s + 1 < deltas.length ? deltas[s + 1] : (deltas[s] || 0)) / 1000;   // ms; the time until the next sample is this one's
    if (dt <= 0) continue;
    total += dt;
    const leaf = label.get(samples[s]);
    self.set(leaf.name, (self.get(leaf.name) || 0) + dt);
    kinds.set(leaf.kind, (kinds.get(leaf.kind) || 0) + dt);
    const seen = new Set();
    for (let id = samples[s]; id !== undefined; id = parent.get(id)) {
      const l = label.get(id); if (!l || l.kind === '(root)' || seen.has(l.name)) continue;
      seen.add(l.name); incl.set(l.name, (incl.get(l.name) || 0) + dt);
    }
  }
  const nF = Math.max(1, frames.n), perF = (ms) => (ms / nF).toFixed(2).padStart(7);
  const idle = kinds.get('(idle)') || 0;
  const out = [];
  out.push(`profile ${title}: ${(frames.t / 1000).toFixed(1)} s, ${frames.n} frames (${(frames.t / nF).toFixed(1)} ms a frame); sampled ${total.toFixed(0)} ms; wasm frames named ${named}, numbered ${numbered}`);
  out.push(`  busy ${(100 * (total - idle) / Math.max(1, total)).toFixed(1)} %, idle ${(100 * idle / Math.max(1, total)).toFixed(1)} % (idle: the main thread waiting - on the GPU, the next frame or the network)`);
  out.push('  by kind (ms a frame): ' + [...kinds.entries()].sort((x, y) => y[1] - x[1]).map(([k, v]) => `${k} ${(v / nF).toFixed(2)}`).join(', '));
  out.push('', 'own time (ms a frame)  - with what it calls - function');
  for (const [n, v] of [...self.entries()].sort((x, y) => y[1] - x[1]).slice(0, top)) out.push(`${perF(v)} ${perF(incl.get(n) || 0)}  ${n}`);
  out.push('', 'with what it calls (ms a frame)  - own - function');
  for (const [n, v] of [...incl.entries()].sort((x, y) => y[1] - x[1]).slice(0, top * 2)) out.push(`${perF(v)} ${perF(self.get(n) || 0)}  ${n}`);
  return out.join('\n') + '\n';
}

// run alone: summarise a profile taken before
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const a = process.argv.slice(2);
  const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
  const file = a.find((x) => x.endsWith('.cpuprofile'));
  if (!file || !fs.existsSync(file)) { console.error('usage: node tc2/scripts/cpuprof.mjs F.cpuprofile [--frames N] [--symbols DIR] [--top 60]'); process.exit(2); }
  const here = path.dirname(fileURLToPath(import.meta.url));
  const dir = opt('--symbols', path.join(here, '..', 'web-build', 'web', 'Build'));
  const profile = JSON.parse(fs.readFileSync(file, 'utf8'));
  const ms = (profile.endTime - profile.startTime) / 1000;
  const n = Number(opt('--frames', '0')) || 1;
  process.stdout.write(summarise(profile, { n, t: ms }, wasmNames(dir), path.basename(file), Number(opt('--top', '60'))));
}
