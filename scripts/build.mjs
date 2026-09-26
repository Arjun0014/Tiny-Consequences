// npm run build / build:release — Defold HTML5 bundle via bob.jar.
// Output: dist/web/app/index.html (+ dmloader.js, *.wasm, archive/), dist/web/build-info.json.
// Flags: --variant debug|release (default debug), --if-stale (skip when bundle is fresh), --quiet
import fs from 'node:fs';
import path from 'node:path';
import { spawn } from 'node:child_process';
import {
  ROOT, BOB_JAR, DIST_WEB, APP_DIR, BUILD_INFO, die, getFlag, hasFlag, javaMajor, rel,
  bundleStaleReason, gitHash, dirSize, fmtBytes, isMain,
} from './lib/util.mjs';

const NOISE = [
  /^WARNING: A restricted method/,
  /^WARNING: java\.lang\.System::load/,
  /^WARNING: Use --enable-native-access/,
  /^WARNING: Restricted methods will be blocked/,
  /^WARNING: Vulnerable protobuf generated type/,
  /GeneratedMessage warnPre22Gencode/,
  /^As of 2022\/09\/29/,
  /^Warning: Failed to clean up temp directory/,
  /^\s*\d+% /,
  /\bINFO\b/,
  /sun\.misc\.Unsafe/,
  /^WARNING: A terminally deprecated method/,
  /^WARNING: Please consider reporting/,
  /^Time profiler report/,
  /SEVERE\s+(com|java|jdk|sun|org)\.[\w.$]+\(.*\)\s*$/, // Java stack frames
];
// bob prints "ERROR: <file>:<line>: <msg>" for compile errors and "<date> SEVERE <msg>" when fatal.
const PROBLEM = /^(ERROR|FATAL)\b|\bSEVERE\b|^Exception|^Error:/;

function findIndexDir(dir) {
  if (!fs.existsSync(dir)) return null;
  if (fs.existsSync(path.join(dir, 'index.html'))) return dir;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.isDirectory()) {
      const found = findIndexDir(path.join(dir, e.name));
      if (found) return found;
    }
  }
  return null;
}

export async function build({ variant = 'debug', ifStale = false, quiet = false } = {}) {
  if (!['debug', 'release'].includes(variant)) die(`unknown variant "${variant}" (debug|release)`);
  if (ifStale) {
    const why = bundleStaleReason(variant);
    if (!why) {
      if (!quiet) console.log(`build: ${variant} bundle is up to date (${rel(APP_DIR)}/index.html)`);
      return { skipped: true };
    }
    console.log(`build: rebuilding (${why})`);
  }
  if (!fs.existsSync(BOB_JAR)) die(`missing ${rel(BOB_JAR)} — run \`npm run setup\``);
  const jv = javaMajor();
  if (!jv) die('java not found on PATH (need JDK 21+)');
  if (jv < 21) die(`java ${jv} found, need 21+`);

  const startedAt = Date.now();
  const bundleTmp = path.join(ROOT, 'dist', '.bob-bundle');
  fs.rmSync(bundleTmp, { recursive: true, force: true });
  fs.mkdirSync(path.join(ROOT, 'out', 'build'), { recursive: true });
  const logPath = path.join(ROOT, 'out', 'build', `bob-${variant}.log`);

  const args = [
    '--enable-native-access=ALL-UNNAMED',
    '-Dcom.google.protobuf.use_unsafe_pre22_gencode=true',
    '-jar', BOB_JAR,
    '--root', ROOT,
    '--platform', 'wasm-web',
    '--architectures', 'wasm-web',
    '--archive',
    '--variant', variant,
    '--bundle-output', bundleTmp,
    '--build-report-json', path.join(ROOT, 'out', 'build', `report-${variant}.json`),
    'resolve', 'build', 'bundle',
  ];
  console.log(`build: bob ${variant} (wasm-web) …`);
  const log = fs.createWriteStream(logPath);
  const problems = [];
  const code = await new Promise((resolve) => {
    const child = spawn('java', args, { cwd: ROOT, stdio: ['ignore', 'pipe', 'pipe'] });
    let buf = '';
    const onData = (chunk) => {
      const s = chunk.toString();
      log.write(s);
      buf += s;
      let nl;
      while ((nl = buf.search(/\r?\n/)) >= 0) {
        const line = buf.slice(0, nl);
        buf = buf.slice(nl + (buf[nl] === '\r' ? 2 : 1));
        if (!line.trim() || NOISE.some((re) => re.test(line))) continue;
        const isProblem = PROBLEM.test(line);
        if (isProblem) problems.push(line);
        else if (!quiet) console.log(`  ${line}`);
      }
    };
    child.stdout.on('data', onData);
    child.stderr.on('data', onData);
    child.on('error', (e) => { console.error(e.message); resolve(127); });
    child.on('exit', (c) => resolve(c ?? 1));
  });
  log.end();

  const indexDir = findIndexDir(bundleTmp);
  if (code !== 0 || problems.length || !indexDir) {
    const unique = [...new Set(problems.map((p) => p.replace(/^.*?\bSEVERE\s+/, 'SEVERE ').trim()))];
    console.error(`\nbob exit code ${code}; full log: ${rel(logPath)}`);
    for (const p of unique.slice(0, 20)) console.error(`  ${p}`);
    if (!indexDir) console.error('  (no index.html produced)');
    die('build FAILED');
  }

  fs.mkdirSync(DIST_WEB, { recursive: true });
  fs.rmSync(APP_DIR, { recursive: true, force: true });
  try {
    fs.renameSync(indexDir, APP_DIR);
  } catch {
    fs.cpSync(indexDir, APP_DIR, { recursive: true });
  }
  fs.rmSync(bundleTmp, { recursive: true, force: true });

  const size = dirSize(APP_DIR);
  const info = {
    variant,
    builtAtMs: startedAt,
    builtAt: new Date(startedAt).toISOString(),
    git: gitHash(),
    defold: JSON.parse(fs.readFileSync(path.join(ROOT, 'tools', 'defold-version.json'), 'utf8')),
    bytes: size,
    seconds: Math.round((Date.now() - startedAt) / 100) / 10,
  };
  fs.writeFileSync(BUILD_INFO, JSON.stringify(info, null, 2) + '\n');
  console.log(`build: OK ${variant} in ${info.seconds}s → ${rel(APP_DIR)}/index.html (${fmtBytes(size)})`);
  return { skipped: false, info };
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  await build({
    variant: getFlag(args, '--variant', 'debug'),
    ifStale: hasFlag(args, '--if-stale'),
    quiet: hasFlag(args, '--quiet'),
  });
}
