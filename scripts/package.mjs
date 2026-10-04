// npm run package:itch - the submission bundle (S25): the release HTML5 build (scripts/build.mjs --variant release), zipped
// as dist/tiny-consequences-web.zip with index.html at its root (itch.io / jam pages take this zip as an HTML game).
// Flags: --no-build (zip the release bundle already in dist/web/app).
// (S25d) --wavedash (npm run build:wavedash): the same release bundle as dist/wavedash/ (wavedash.toml's upload_dir) and
// dist/tiny-consequences-wavedash.zip. The game finds the Wavedash SDK on the page at run time (game/html/engine_template.html,
// window.tcWD): it calls Wavedash.init() when ready, shows the player's name, keeps progress and settings in the cloud.
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const APP = path.join(ROOT, 'dist', 'web', 'app');
const args = process.argv.slice(2);
const WD = args.includes('--wavedash');
const ZIP = path.join(ROOT, 'dist', WD ? 'tiny-consequences-wavedash.zip' : 'tiny-consequences-web.zip');

function run(cmd, a, opts = {}) {
  const r = spawnSync(cmd, a, { stdio: 'inherit', cwd: ROOT, shell: false, ...opts });
  if (r.status !== 0) { console.error(`package: ${cmd} failed (${r.status})`); process.exit(1); }
}

if (!args.includes('--no-build')) run(process.execPath, [path.join(ROOT, 'scripts', 'build.mjs'), '--variant', 'release']);
const info = path.join(ROOT, 'dist', 'web', 'build-info.json');
if (fs.existsSync(info)) {
  const bi = JSON.parse(fs.readFileSync(info, 'utf8'));
  if (bi.variant && bi.variant !== 'release') { console.error(`package: dist/web/app is a ${bi.variant} build - run without --no-build`); process.exit(1); }
}
if (!fs.existsSync(path.join(APP, 'index.html'))) { console.error('package: no dist/web/app/index.html'); process.exit(1); }
if (fs.existsSync(ZIP)) fs.rmSync(ZIP);
let SRC = APP;
if (WD) {
  SRC = path.join(ROOT, 'dist', 'wavedash');
  fs.rmSync(SRC, { recursive: true, force: true });
  fs.cpSync(APP, SRC, { recursive: true });
}
if (process.platform === 'win32') {
  run('powershell', ['-NoProfile', '-Command', `Compress-Archive -Path '${SRC}\\*' -DestinationPath '${ZIP}' -Force`]);
} else {
  run('zip', ['-qr', ZIP, '.'], { cwd: SRC });
}
const mb = (fs.statSync(ZIP).size / 1048576).toFixed(2);
console.log(`package: ${path.relative(ROOT, ZIP)} (${mb} MB) - upload as an HTML game; index.html at the zip's root`
  + (WD ? ` (Wavedash: ${path.relative(ROOT, SRC)} is wavedash.toml's upload_dir)` : ''));
