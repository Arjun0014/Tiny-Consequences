// Shared helpers for the npm scripts. Node >= 20, no dependencies, cross-platform.
import { spawn, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
export const IS_WIN = process.platform === 'win32';

export const BOB_JAR = path.join(ROOT, 'tools', 'bob', 'bob.jar');
export const DIST_WEB = path.join(ROOT, 'dist', 'web');
export const APP_DIR = path.join(DIST_WEB, 'app');           // served folder (index.html lives here)
export const BUILD_INFO = path.join(DIST_WEB, 'build-info.json');

// Sources whose changes make the web bundle stale.
export const BUNDLE_INPUTS = ['game', 'sim', 'content', 'assets', 'game.project', '.defignore'];

export function rel(p) {
  return path.relative(ROOT, p).split(path.sep).join('/');
}

export function die(msg, code = 1) {
  console.error(`\n✗ ${msg}`);
  process.exit(code);
}

/** Splits process.argv after the script into { flags, rest } where rest follows a `--`. */
export function argvAfterDashDash(argv = process.argv.slice(2)) {
  const i = argv.indexOf('--');
  return i < 0 ? { own: argv, rest: [] } : { own: argv.slice(0, i), rest: argv.slice(i + 1) };
}

export function getFlag(args, name, fallback = undefined) {
  const i = args.indexOf(name);
  if (i >= 0 && i + 1 < args.length) return args[i + 1];
  const eq = args.find((a) => a.startsWith(name + '='));
  if (eq) return eq.slice(name.length + 1);
  return fallback;
}

export function hasFlag(args, name) {
  return args.includes(name);
}

/** Finds an executable on PATH (honours PATHEXT on Windows). */
export function which(name) {
  const exts = IS_WIN ? (process.env.PATHEXT || '.EXE;.CMD;.BAT').split(';').concat(['']) : [''];
  for (const dir of (process.env.PATH || '').split(path.delimiter)) {
    if (!dir) continue;
    for (const ext of exts) {
      const p = path.join(dir.replace(/^"|"$/g, ''), name + ext.toLowerCase());
      const P = path.join(dir.replace(/^"|"$/g, ''), name + ext);
      for (const cand of [p, P]) {
        try {
          if (fs.statSync(cand).isFile()) return cand;
        } catch { /* not here */ }
      }
    }
  }
  return null;
}

/** LuaJIT resolution: $LUAJIT, PATH, known install locations. */
export function resolveLuajit() {
  const env = process.env.LUAJIT;
  if (env && fs.existsSync(env)) return env;
  const onPath = which('luajit');
  if (onPath) return onPath;
  const home = process.env.USERPROFILE || process.env.HOME || '';
  const candidates = [
    process.env.LOCALAPPDATA && path.join(process.env.LOCALAPPDATA, 'Programs', 'LuaJIT', 'bin', 'luajit.exe'),
    home && path.join(home, 'AppData', 'Local', 'Programs', 'LuaJIT', 'bin', 'luajit.exe'),
    '/usr/bin/luajit',
    '/usr/local/bin/luajit',
    '/opt/homebrew/bin/luajit',
  ].filter(Boolean);
  for (const c of candidates) {
    if (fs.existsSync(c)) return c;
  }
  return null;
}

/** Returns the Java major version, or null if java is missing. */
export function javaMajor() {
  const r = spawnSync('java', ['-version'], { encoding: 'utf8', shell: false });
  if (r.error) return null;
  const out = `${r.stderr || ''}${r.stdout || ''}`;
  const m = out.match(/version "(\d+)(?:\.(\d+))?/);
  if (!m) return null;
  const major = Number(m[1]);
  return major === 1 ? Number(m[2]) : major;
}

/** Runs a command with inherited stdio; resolves to the exit code. */
export function run(cmd, args, opts = {}) {
  return new Promise((resolve) => {
    const child = spawn(cmd, args, { cwd: ROOT, stdio: 'inherit', ...opts });
    child.on('error', (err) => {
      console.error(`failed to start ${cmd}: ${err.message}`);
      resolve(127);
    });
    child.on('exit', (code, signal) => resolve(code ?? (signal ? 1 : 0)));
  });
}

/** npx invocation that works on Windows without shell quoting problems. */
export function npxCommand() {
  return IS_WIN ? { cmd: process.env.ComSpec || 'cmd.exe', pre: ['/d', '/s', '/c', 'npx'] } : { cmd: 'npx', pre: [] };
}

/** Newest mtime (ms) of any file under the given repo-relative paths. */
export function newestMtime(relPaths) {
  let newest = 0;
  let newestFile = null;
  const walk = (p) => {
    let st;
    try {
      st = fs.statSync(p);
    } catch {
      return;
    }
    if (st.isDirectory()) {
      for (const e of fs.readdirSync(p)) walk(path.join(p, e));
    } else if (st.mtimeMs > newest) {
      newest = st.mtimeMs;
      newestFile = p;
    }
  };
  for (const r of relPaths) walk(path.join(ROOT, r));
  return { newest, newestFile };
}

export function readBuildInfo() {
  try {
    return JSON.parse(fs.readFileSync(BUILD_INFO, 'utf8'));
  } catch {
    return null;
  }
}

/** Why the bundle needs rebuilding for `variant`, or null if it is fresh. */
export function bundleStaleReason(variant) {
  const info = readBuildInfo();
  if (!info || !fs.existsSync(path.join(APP_DIR, 'index.html'))) return 'no bundle';
  if (variant && info.variant !== variant) return `bundle is ${info.variant}, want ${variant}`;
  const { newest, newestFile } = newestMtime(BUNDLE_INPUTS);
  if (newest > info.builtAtMs) return `${rel(newestFile)} changed since last build`;
  return null;
}

/** Current chapter: --chapter N, env TC_CHAPTER, else "- Chapter: N" in docs/PROGRESS.md, else 1. */
export function currentChapter(args = []) {
  const fromFlag = getFlag(args, '--chapter');
  if (fromFlag) return Number(fromFlag);
  if (process.env.TC_CHAPTER) return Number(process.env.TC_CHAPTER);
  try {
    const txt = fs.readFileSync(path.join(ROOT, 'docs', 'PROGRESS.md'), 'utf8');
    const m = txt.match(/^-\s*Chapter:\s*(\d+)/m);
    if (m) return Number(m[1]);
  } catch { /* fall through */ }
  return 1;
}

export function gitHash() {
  const r = spawnSync('git', ['rev-parse', '--short', 'HEAD'], { cwd: ROOT, encoding: 'utf8' });
  const dirty = spawnSync('git', ['status', '--porcelain'], { cwd: ROOT, encoding: 'utf8' });
  const h = (r.stdout || '').trim() || 'nogit';
  return (dirty.stdout || '').trim() ? `${h}-dirty` : h;
}

export function dirSize(p) {
  let total = 0;
  const walk = (q) => {
    const st = fs.statSync(q);
    if (st.isDirectory()) for (const e of fs.readdirSync(q)) walk(path.join(q, e));
    else total += st.size;
  };
  if (fs.existsSync(p)) walk(p);
  return total;
}

export function fmtBytes(n) {
  if (n > 1024 * 1024) return `${(n / 1024 / 1024).toFixed(2)} MB`;
  if (n > 1024) return `${(n / 1024).toFixed(1)} KB`;
  return `${n} B`;
}

/** True when the module at `metaUrl` is the script node was started with. */
export function isMain(metaUrl) {
  if (!process.argv[1]) return false;
  try {
    return path.resolve(fileURLToPath(metaUrl)) === path.resolve(process.argv[1]);
  } catch {
    return false;
  }
}
