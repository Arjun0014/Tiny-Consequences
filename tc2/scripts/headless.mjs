// Builds and runs the TC2 headless host (tc2/headless/app) with the .NET SDK bundled in the Unity editor.
// Usage: node tc2/scripts/headless.mjs <test|run|bench|hash|inspect|follow|timeuse|floatcheck> [args...]   (--nobuild to skip the build)
// test runs the determinism lint first (tc2/tools/floatcheck, D2-057).
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const app = path.join(root, 'tc2', 'headless', 'app');
const dotnet = process.env.TC2_DOTNET || 'C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/DotNetSdk/dotnet.exe';
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1' };
const args = process.argv.slice(2);
const noBuild = args.includes('--nobuild');
const pass = args.filter(a => a !== '--nobuild');

if (!existsSync(dotnet)) { console.error('dotnet not found at ' + dotnet + ' (set TC2_DOTNET)'); process.exit(2); }
if (!noBuild) {
  const b = spawnSync(dotnet, ['build', '-c', 'Release', '-nologo', '-v', 'q', '-clp:ErrorsOnly'], { cwd: app, env, stdio: 'inherit' });
  if (b.status !== 0) { console.error('build failed'); process.exit(b.status || 1); }
}
// determinism lint before the tests (D2-057): no arithmetic typed float in TC2.Core (Mono would carry it at double precision)
if (pass[0] === 'test' || pass[0] === 'floatcheck') {
  const tool = path.join(root, 'tc2', 'tools', 'floatcheck');
  const tb = spawnSync(dotnet, ['build', '-c', 'Release', '-nologo', '-v', 'q', '-clp:ErrorsOnly'], { cwd: tool, env, stdio: 'inherit' });
  if (tb.status !== 0) { console.error('floatcheck build failed'); process.exit(tb.status || 1); }
  const fc = spawnSync(dotnet, [path.join(tool, 'bin', 'Release', 'net8.0', 'floatcheck.dll'), path.join(root, 'unity', 'TinyConsequences2', 'Assets', 'TC2', 'Core'), '--list'],
                       { cwd: root, env, stdio: 'inherit' });
  if (fc.status !== 0) { console.error('floatcheck: float arithmetic in TC2.Core (do it in double, cast to float when stored)'); process.exit(1); }
  if (pass[0] === 'floatcheck') process.exit(0);
}
const dll = path.join(app, 'bin', 'Release', 'net8.0', 'tc2.dll');
const r = spawnSync(dotnet, [dll, ...pass], { cwd: root, env, stdio: 'inherit' });
process.exit(r.status ?? 1);
