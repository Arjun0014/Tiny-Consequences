// npm run setup — checks Node/Java/LuaJIT, downloads the pinned bob.jar if missing,
// installs npm deps if needed and Playwright's Chromium.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { BOB_JAR, ROOT, die, javaMajor, rel, resolveLuajit, run } from './lib/util.mjs';

let problems = 0;
const ok = (m) => console.log(`  ok   ${m}`);
const bad = (m) => { problems++; console.log(`  FAIL ${m}`); };

console.log('setup: checking tools');
const nodeMajor = Number(process.versions.node.split('.')[0]);
nodeMajor >= 20 ? ok(`node ${process.versions.node}`) : bad(`node ${process.versions.node} (need >= 20)`);

const jv = javaMajor();
if (!jv) bad('java not found on PATH (install JDK 21+, e.g. Temurin)');
else if (jv < 21) bad(`java ${jv} (need >= 21)`);
else ok(`java ${jv}`);

const luajit = resolveLuajit();
if (!luajit) bad('luajit not found (set LUAJIT=<path> or install LuaJIT 2.1; see SETUP_HUMAN.md)');
else {
  const v = spawnSync(luajit, ['-v'], { encoding: 'utf8' });
  ok(`${(v.stdout || v.stderr || '').trim().split('\n')[0]}  (${luajit})`);
}

const pin = JSON.parse(fs.readFileSync(path.join(ROOT, 'tools', 'defold-version.json'), 'utf8'));
if (!fs.existsSync(BOB_JAR)) {
  const url = `https://d.defold.com/archive/stable/${pin.sha1}/bob/bob.jar`;
  console.log(`setup: downloading bob.jar ${pin.version} from ${url}`);
  fs.mkdirSync(path.dirname(BOB_JAR), { recursive: true });
  const res = await fetch(url);
  if (!res.ok) die(`download failed: HTTP ${res.status}`);
  const tmp = BOB_JAR + '.part';
  fs.writeFileSync(tmp, Buffer.from(await res.arrayBuffer()));
  fs.renameSync(tmp, BOB_JAR);
}
if (jv) {
  const v = spawnSync('java', ['-jar', BOB_JAR, '--version'], { encoding: 'utf8' });
  const out = `${v.stdout || ''}${v.stderr || ''}`;
  if (out.includes(pin.sha1)) ok(`bob.jar ${pin.version} (${pin.sha1.slice(0, 7)}) at ${rel(BOB_JAR)}`);
  else bad(`bob.jar version mismatch: ${out.trim().split('\n')[0]} (want ${pin.version} ${pin.sha1})`);
}

if (!fs.existsSync(path.join(ROOT, 'node_modules', '@playwright', 'test'))) {
  console.log('setup: npm install');
  const npmCli = process.env.npm_execpath;
  const code = npmCli
    ? await run(process.execPath, [npmCli, 'install'])
    : await run(process.platform === 'win32' ? 'npm.cmd' : 'npm', ['install'], { shell: process.platform === 'win32' });
  if (code !== 0) bad('npm install failed');
}
console.log('setup: playwright install chromium');
const pwCode = await run(process.execPath, [path.join(ROOT, 'node_modules', '@playwright', 'test', 'cli.js'), 'install', 'chromium']);
pwCode === 0 ? ok('playwright chromium') : bad('playwright install chromium failed');

if (problems) die(`setup: ${problems} problem(s) — see above`);
console.log('setup: all good');
