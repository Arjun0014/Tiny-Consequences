// npm run test:sim — lint:lua, then the headless sim suite under LuaJIT:
//   luajit tests/sim/run.lua <args>   (e.g. `npm run test:sim -- --full`)
import { lintPaths } from './lint_lua.mjs';
import { runLua } from './lua.mjs';

const { files, violations } = lintPaths();
for (const v of violations) console.log(`${v.file}:${v.line}: ${v.rule}: ${v.msg}    | ${v.text}`);
console.log(`lint:lua: ${files} file(s), ${violations.length} violation(s)`);
if (violations.length) process.exit(1);

const args = process.argv.slice(2);
process.exit(await runLua('tests/sim/run.lua', args[0] === '--' ? args.slice(1) : args));
