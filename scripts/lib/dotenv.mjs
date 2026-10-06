// .env at the repo root (gitignored): KEY=value lines (# comments, optional quotes, optional `export `). The values are
// secrets: they are returned, never printed or written anywhere (S26: the Wavedash key; S21's ElevenLabs key reads the
// same file its own way).
import fs from 'node:fs';
import path from 'node:path';
import { ROOT } from './util.mjs';

export function readDotenv(file = path.join(ROOT, '.env')) {
  let txt;
  try { txt = fs.readFileSync(file, 'utf8'); } catch { return {}; }
  const out = {};
  for (const raw of txt.split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const m = line.match(/^(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*)$/);
    if (!m) continue;
    let v = m[2].trim();
    if ((v.startsWith('"') && v.endsWith('"')) || (v.startsWith("'") && v.endsWith("'"))) v = v.slice(1, -1);
    else v = v.replace(/\s+#.*$/, '');
    out[m[1]] = v;
  }
  return out;
}

// The first of `names` that .env (or the process environment) sets, as { name, value } - or null.
export function pick(env, names) {
  for (const n of names) {
    const v = env[n] || process.env[n];
    if (v) return { name: n, value: v };
  }
  return null;
}

// text with every given secret masked (for logs that might echo them)
export function mask(text, secrets) {
  let t = String(text);
  for (const s of secrets) if (s && s.length >= 6) t = t.split(s).join(s.slice(0, 4) + '…' + '[masked]');
  return t;
}
