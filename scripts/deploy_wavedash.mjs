// npm run deploy:wavedash - (S26) build the Wavedash package, check it, and push it to Wavedash as a new build. It is NEVER
// published: a pushed build is there for the team to playtest; publishing is a human's decision on wavedash.com (or
// `wavedash publish <build id>` by hand).
// The API key comes from .env (WAVEDASH_TOKEN, else WAVEDASH_API_KEY / WAVEDASH_API / WAVEDASH_KEY), handed to the
// Wavedash CLI in its environment only (WAVEDASH_TOKEN); it is never printed, logged or written (the CLI's output is
// masked). The game id is wavedash.toml's game_id (or WAVEDASH_GAME_ID). The CLI: `wavedash` (cargo install; docs.wavedash.com/cli).
// Flags: --no-build (push the package already in dist/wavedash), --check (build and check only: no push),
//        -m "message" (the build's message; default: the git commit and date).
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { ROOT } from './lib/util.mjs';
import { readDotenv, pick, mask } from './lib/dotenv.mjs';

const args = process.argv.slice(2);
const has = (f) => args.includes(f);
const val = (f) => { const i = args.indexOf(f); return i >= 0 ? args[i + 1] : null; };
const DIR = path.join(ROOT, 'dist', 'wavedash');
const TOML = path.join(ROOT, 'wavedash.toml');
const die = (m) => { console.error(`deploy:wavedash: ${m}`); process.exit(1); };

// 1. the package
if (!has('--no-build')) {
  const r = spawnSync(process.execPath, [path.join(ROOT, 'scripts', 'package.mjs'), '--wavedash'], { stdio: 'inherit', cwd: ROOT });
  if (r.status !== 0) die('the package failed to build');
}

// 2. check it: what the page loads is all there, with forward-slash names, a release build, the Wavedash glue in the page
const problems = [];
const files = [];
(function walk(d, rel) {
  for (const e of fs.readdirSync(d, { withFileTypes: true })) {
    const r = rel ? `${rel}/${e.name}` : e.name;
    if (e.isDirectory()) walk(path.join(d, e.name), r);
    else files.push({ rel: r, size: fs.statSync(path.join(d, e.name)).size });
  }
})(DIR, '');
const names = new Set(files.map((f) => f.rel));
for (const f of ['index.html', 'dmloader.js', 'archive/archive_files.json']) if (!names.has(f)) problems.push(`missing ${f}`);
if (!files.some((f) => f.rel.endsWith('.wasm'))) problems.push('no .wasm');
if (!files.some((f) => /_wasm\.js$/.test(f.rel))) problems.push('no wasm loader script');
if (files.some((f) => f.rel.includes('\\'))) problems.push('a file name with a backslash');
try {
  const af = JSON.parse(fs.readFileSync(path.join(DIR, 'archive', 'archive_files.json'), 'utf8'));
  for (const c of af.content || []) for (const p of c.pieces || [{ name: c.name }]) if (!names.has(`archive/${p.name}`)) problems.push(`archive piece missing: ${p.name}`);
} catch (e) { problems.push('archive_files.json unreadable: ' + e.message); }
const html = names.has('index.html') ? fs.readFileSync(path.join(DIR, 'index.html'), 'utf8') : '';
for (const needle of ['window.tcWD', 'updateLoadProgressZeroToOne', "CUSTOM_PARAMETERS['update_progress']", 'window.tcLoad'])
  if (!html.includes(needle)) problems.push(`index.html lacks ${needle}`);
try {
  const bi = JSON.parse(fs.readFileSync(path.join(ROOT, 'dist', 'web', 'build-info.json'), 'utf8'));
  if (bi.variant !== 'release') problems.push(`the bundle is a ${bi.variant} build`);
} catch { problems.push('no dist/web/build-info.json'); }
const total = files.reduce((a, f) => a + f.size, 0);
console.log(`deploy:wavedash: ${path.relative(ROOT, DIR)} - ${files.length} files, ${(total / 1048576).toFixed(2)} MB`);
for (const f of files) console.log(`  ${f.rel.padEnd(32)} ${(f.size / 1024).toFixed(0).padStart(7)} KB`);
if (problems.length) die('the package is not right:\n  ' + problems.join('\n  '));
console.log('deploy:wavedash: the package checks out (index.html at its root, every archive piece present, the progress and SDK glue in the page)');
if (has('--check')) process.exit(0);

// 3. the game and the key
const toml = fs.readFileSync(TOML, 'utf8');
const gid = process.env.WAVEDASH_GAME_ID || (toml.match(/^\s*game_id\s*=\s*"([^"]*)"/m) || [])[1];
if (!gid || /YOUR_GAME_ID/.test(gid)) die('no game id: set game_id in wavedash.toml (wavedash project list --team-id <team>)');
const env = readDotenv();
const key = pick(env, ['WAVEDASH_TOKEN', 'WAVEDASH_API_KEY', 'WAVEDASH_API', 'WAVEDASH_KEY']);
if (!key) die('no API key: put WAVEDASH_TOKEN=<key> in .env (gitignored); create one at wavedash.com/dev-portal/keys');

// 4. push (never publish)
const git = spawnSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: ROOT, encoding: 'utf8' }).stdout.trim();
const msg = val('-m') || `Tiny Consequences ${git} (${new Date().toISOString().slice(0, 16).replace('T', ' ')} UTC) - playtest build, not published`;
console.log(`deploy:wavedash: pushing to game ${gid} (key from .env ${key.name}) - "${msg}"`);
const r = spawnSync('wavedash', ['build', 'push', '-c', TOML, '-m', msg], {
  cwd: ROOT, encoding: 'utf8', env: { ...process.env, WAVEDASH_TOKEN: key.value, WAVEDASH_GAME_ID: gid },
});
const out = mask((r.stdout || '') + (r.stderr || ''), [key.value]);
console.log(out.trim());
if (r.status !== 0) die(`wavedash build push failed (${r.status})`);
const id = (out.match(/build[_ ]?id[^A-Za-z0-9]*([A-Za-z0-9_-]{8,})/i) || out.match(/\b(j[0-9a-z]{31})\b/) || [])[1] || null;
const rec = { at: new Date().toISOString(), git, game_id: gid, build_id: id, message: msg, files: files.length, bytes: total, published: false };
fs.mkdirSync(path.join(ROOT, 'out'), { recursive: true });
fs.writeFileSync(path.join(ROOT, 'out', 'wavedash_last_push.json'), JSON.stringify(rec, null, 1));
console.log(`deploy:wavedash: pushed${id ? ` - build ${id}` : ''} (not published; out/wavedash_last_push.json)`);
