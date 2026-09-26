// Runs a Lua script under LuaJIT from the repo root: `node scripts/lua.mjs <file.lua> [args...]`.
// Used by `npm run sim` (tools/run_sim.lua) and `npm run bench` (tools/bench.lua).
// LuaJIT is resolved from $LUAJIT, PATH, then known install locations (scripts/lib/util.mjs).
// Env passed to Lua: TC_CHAPTER (current chapter), LUA51 (path to lua5.1 if installed).
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, currentChapter, die, isMain, resolveLuajit, run, which } from './lib/util.mjs';

export function luaEnv() {
  const env = { ...process.env, TC_CHAPTER: String(currentChapter()) };
  const lua51 = which('lua5.1') || which('lua51');
  if (lua51) env.LUA51 = lua51;
  return env;
}

export async function runLua(script, args) {
  const luajit = resolveLuajit();
  if (!luajit) die('LuaJIT not found (set LUAJIT=<path>, or install it; see SETUP_HUMAN.md)');
  if (!fs.existsSync(path.join(ROOT, script))) die(`${script} does not exist yet`);
  return run(luajit, [script, ...args], { env: luaEnv() });
}

if (isMain(import.meta.url)) {
  const [script, ...rest] = process.argv.slice(2);
  if (!script) die('usage: node scripts/lua.mjs <file.lua> [args...]');
  process.exit(await runLua(script, rest[0] === '--' ? rest.slice(1) : rest));
}
