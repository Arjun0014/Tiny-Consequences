// Runs the Unity 6.6 editor in batchmode for TC2 (one Unity process at a time: 11 GB RAM).
// Usage: node tc2/scripts/unity.mjs <configure|test|hashtest|build-web|build-win|method NAME> [--dev] [--threads] [--webgl2] [--out PATH]
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const project = path.join(root, 'unity', 'TinyConsequences2');
const unity = process.env.TC2_UNITY || 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe';
const outDir = path.join(root, 'tc2', 'out');
mkdirSync(path.join(outDir, 'logs'), { recursive: true });

const args = process.argv.slice(2);
const cmd = args[0];
const flag = (f) => args.includes(f);
const opt = (f, d) => { const i = args.indexOf(f); return i >= 0 && i + 1 < args.length ? args[i + 1] : d; };
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const log = path.join(outDir, 'logs', `unity-${cmd}-${stamp}.log`);

function run(extra, { quit = true } = {}) {
  const a = ['-batchmode', '-projectPath', project, '-logFile', log, ...(quit ? ['-quit'] : []), ...extra];
  const t0 = Date.now();
  console.log('unity ' + a.join(' '));
  // --jobs N also caps the build backend's threads (Bee compiles the generated C++ one job per thread otherwise)
  // (and emscripten's and binaryen's: wasm-opt otherwise takes every core at the end of a Web build)
  const env = opt('--jobs') ? { ...process.env, BEE_BUILD_THREADS: String(opt('--jobs')), EMCC_CORES: String(opt('--jobs')), BINARYEN_CORES: String(opt('--jobs')) } : process.env;
  const r = spawnSync(unity, a, { stdio: 'inherit', env });
  const secs = ((Date.now() - t0) / 1000).toFixed(0);
  let status = r.status ?? 1;
  // Unity can exit 0 when scripts fail to compile (the method never ran): treat compiler errors as failure
  if (existsSync(log) && /error CS\d+/.test(readFileSync(log, 'utf8'))) { console.log('compiler errors in the log'); status = status || 3; }
  console.log(`exit ${status} after ${secs} s; log ${log}`);
  return status;
}

function tail(file, pattern) {
  if (!existsSync(file)) return;
  const lines = readFileSync(file, 'utf8').split(/\r?\n/);
  const hits = lines.filter((l) => pattern.test(l));
  console.log(hits.slice(-60).join('\n'));
}

let status = 0;
switch (cmd) {
  case 'configure':
    status = run(['-executeMethod', 'TC2.EditorTools.ProjectSetup.Configure']);
    tail(log, /error CS|\[TC2\]|Exception|Error:/);
    break;
  case 'test': {
    const results = path.join(outDir, 'unity-tests.xml');
    status = run(['-runTests', '-testPlatform', opt('--platform', 'EditMode'), '-testResults', results, ...(opt('--filter') ? ['-testFilter', opt('--filter')] : [])], { quit: false });
    if (existsSync(results)) {
      const x = readFileSync(results, 'utf8');
      const m = x.match(/<test-run[^>]*>/);
      console.log(m ? m[0] : 'no test-run element');
      const fails = [...x.matchAll(/<test-case[^>]*name="([^"]+)"[^>]*result="Failed"/g)].map((f) => f[1]);
      if (fails.length) console.log('FAILED: ' + fails.join(', '));
    } else tail(log, /error CS|Exception|Error/);
    break;
  }
  case 'hashtest': {
    const out = path.join(outDir, 'hash-unity-editor.txt');
    status = run(['-executeMethod', 'TC2.EditorTools.BatchTools.HashTest', '-tc2Out', out]);
    if (existsSync(out)) console.log('editor (Mono): ' + readFileSync(out, 'utf8'));
    else tail(log, /error CS|Exception|\[TC2\]/);
    break;
  }
  case 'build-web': {
    const out = opt('--out', path.join(root, 'tc2', 'web-build', 'web'));
    const extra = ['-executeMethod', 'TC2.EditorTools.BuildScript.BuildWeb', '-buildTarget', 'WebGL', '-tc2Out', out];
    if (flag('--dev')) extra.push('-tc2Dev');
    if (flag('--threads')) extra.push('-tc2Threads');
    if (flag('--webgl2')) extra.push('-tc2WebGL2Only');
    if (opt('--jobs')) extra.push('-tc2Jobs', opt('--jobs'));   // (IL2CPP jobs: fewer on a shared machine)
    status = run(extra);
    tail(log, /error CS|\[TC2\]|Build Finished|Exception|Error building/);
    break;
  }
  case 'build-win': {
    status = run(['-executeMethod', 'TC2.EditorTools.BuildScript.BuildWindows', '-buildTarget', 'Win64']);
    tail(log, /error CS|\[TC2\]|Exception/);
    break;
  }
  case 'shots': {
    // the real game in Play Mode inside the batch editor: node tc2/scripts/unity.mjs shots --tag T [--seed N] [--hour H] [--days D] [--views a,b]
    const dir = path.join(outDir, 'shots', opt('--tag', 'latest'));
    const extra = ['-executeMethod', 'TC2.EditorTools.BatchTools.PlayShots', '-tc2shots', dir];
    for (const [f, a] of [['--seed', '-tc2seed'], ['--hour', '-tc2hour'], ['--days', '-tc2days'], ['--views', '-tc2views'], ['--size', '-tc2size'], ['--erupt', '-tc2erupt'], ['--quake', '-tc2quake'], ['--storm', '-tc2storm'], ['--abandon', '-tc2abandon'], ['--hungry', '-tc2hungry'], ['--wall', '-tc2wall'], ['--load', '-tc2load'], ['--radius', '-tc2radius'], ['--village', '-tc2village']]) if (opt(f)) extra.push(a, opt(f));
    if (args.includes('--ground')) extra.push('-tc2ground', '1');   // (C13.3: the simulation's ground under each home)
    status = run(extra, { quit: false });
    tail(log, /error CS|\[TC2\]|Exception/);
    console.log('shots in ' + dir);
    break;
  }
  case 'method':
    status = run(['-executeMethod', args[1]]);
    tail(log, /error CS|\[TC2\]|Exception/);
    break;
  default:
    console.log('usage: node tc2/scripts/unity.mjs configure|test|hashtest|build-web|build-win|method NAME');
    status = 2;
}
process.exit(status);
