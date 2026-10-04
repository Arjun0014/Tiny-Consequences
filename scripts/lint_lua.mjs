// npm run lint:lua — rejects Lua constructs that are not Lua 5.1 (HTML5 runs Lua 5.1.4;
// LuaJIT accepts more), and non-deterministic / engine calls inside the pure-Lua sim.
// See 05_ARCHITECTURE.md §4. Comments and string literals are stripped before matching.
//
// Usage: node scripts/lint_lua.mjs [files or dirs...]   (default: sim content game tools tests/sim)
//        node scripts/lint_lua.mjs --selftest
// Suppress a finding on one line with a trailing comment: `-- lint:allow <rule-id>`.
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, isMain } from './lib/util.mjs';

const DEFAULT_DIRS = ['sim', 'content', 'game', 'tools', 'tests/sim'];
const SKIP_DIRS = new Set(['node_modules', 'bob', '.git', 'build', 'dist', 'out']);
const EXTENSIONS = new Set(['.lua', '.script', '.gui_script', '.render_script']);
const PURE_DIRS = ['sim/', 'content/'];

// ---------------------------------------------------------------------------------------
// Lexer: returns code with comments blanked and every string literal replaced by "@N"
// (newlines preserved so offsets map to the same line numbers), plus string records.

function longBracketLevel(src, i) {
  // src[i] === '['; returns level (number of '=') if a long bracket opens here, else -1.
  let j = i + 1;
  while (src[j] === '=') j++;
  return src[j] === '[' ? j - i - 1 : -1;
}

export function lex(src) {
  const out = [];
  const strings = [];
  let line = 1;
  let i = 0;
  const n = src.length;
  const blank = (s) => s.replace(/[^\n]/g, ' ');
  while (i < n) {
    const c = src[i];
    if (c === '-' && src[i + 1] === '-') {
      let end;
      const lvl = src[i + 2] === '[' ? longBracketLevel(src, i + 2) : -1;
      if (lvl >= 0) {
        const close = ']' + '='.repeat(lvl) + ']';
        const k = src.indexOf(close, i + 4 + lvl);
        end = k < 0 ? n : k + close.length;
      } else {
        const k = src.indexOf('\n', i);
        end = k < 0 ? n : k;
      }
      const chunk = src.slice(i, end);
      out.push(blank(chunk));
      line += (chunk.match(/\n/g) || []).length;
      i = end;
      continue;
    }
    if (c === '[') {
      const lvl = longBracketLevel(src, i);
      if (lvl >= 0) {
        const close = ']' + '='.repeat(lvl) + ']';
        const start = i + lvl + 2;
        const k = src.indexOf(close, start);
        const end = k < 0 ? n : k + close.length;
        const value = src.slice(start, k < 0 ? n : k);
        const nl = (src.slice(i, end).match(/\n/g) || []).length;
        strings.push({ value, line, kind: 'long', badEscapes: [] });
        out.push(`"@${strings.length - 1}"` + '\n'.repeat(nl));
        line += nl;
        i = end;
        continue;
      }
    }
    if (c === '"' || c === "'") {
      const startLine = line;
      let j = i + 1;
      let value = '';
      const badEscapes = [];
      let nl = 0;
      while (j < n && src[j] !== c && src[j] !== '\n') {
        if (src[j] === '\\') {
          const e = src[j + 1];
          if (e === 'x') badEscapes.push({ esc: '\\x', line: line + nl });
          else if (e === 'z') badEscapes.push({ esc: '\\z', line: line + nl });
          else if (e === 'u' && src[j + 2] === '{') badEscapes.push({ esc: '\\u{', line: line + nl });
          if (e === '\n') nl++;
          else if (e === '\r' && src[j + 2] === '\n') { nl++; j++; }
          value += src[j] + (e ?? '');
          j += 2;
          continue;
        }
        value += src[j];
        j++;
      }
      const end = j < n && src[j] === c ? j + 1 : j;
      strings.push({ value, line: startLine, kind: 'short', badEscapes });
      out.push(`"@${strings.length - 1}"` + '\n'.repeat(nl));
      line += nl;
      i = end;
      continue;
    }
    if (c === '\n') line++;
    out.push(c);
    i++;
  }
  return { code: out.join(''), strings };
}

// ---------------------------------------------------------------------------------------
// Rules

const NB = '(?<![.:\\w])'; // not preceded by '.', ':' or an identifier char

const COMMON_RULES = [
  { id: 'goto', re: /\bgoto\b/g, msg: "'goto' is Lua 5.2+" },
  { id: 'label', re: /::\s*[A-Za-z_]\w*\s*::/g, msg: '::label:: is Lua 5.2+' },
  { id: 'int-div', re: /\/\//g, msg: "'//' (integer division) is Lua 5.3+" },
  { id: 'bitop', re: /<<|>>|&|\||~(?!=)/g, msg: 'bitwise operators are Lua 5.3+ (use the `bit` module in game/, arithmetic in sim/)' },
  { id: 'ffi', re: /\bffi\s*\./g, msg: 'ffi is LuaJIT-only' },
  { id: 'load-string', re: new RegExp(`${NB}load\\s*(\\(\\s*)?"@\\d+"`, 'g'), msg: 'load(<string>) is Lua 5.2+ (use loadstring)' },
  { id: 'table-unpack', re: /\btable\s*\.\s*unpack\b/g, msg: 'table.unpack is Lua 5.2+ (use unpack)' },
  { id: 'luajit-only', re: /\bjit\s*\.|\btable\s*\.\s*(new|clear|move|pack)\b|\bbit32\s*\.|\butf8\s*\.|\brawlen\b|\bstring\s*\.\s*(pack|unpack|packsize)\b/g, msg: 'not available in Lua 5.1' },
  { id: 'attrib', re: /<\s*(const|close)\s*>/g, msg: '<const>/<close> are Lua 5.4' },
  { id: 'env', re: /\b_ENV\b/g, msg: '_ENV is Lua 5.2+' },
  { id: 'number-literal', re: /\b0[bB][01]+\b|\b\d+[uU]?[lL][lL]\b|\b0[xX][0-9a-fA-F]*\.?[0-9a-fA-F]*[pP][+-]?\d+|\b\d+(\.\d*)?i\b/g, msg: 'LuaJIT/5.2+ numeric literal' },
];

const PURE_RULES = [
  { id: 'nondeterministic', re: /\bmath\s*\.\s*random|\bos\s*\.\s*(time|clock|date)\b/g, msg: 'non-deterministic call in sim (use sim/rng.lua / ticks)' },
  { id: 'io', re: new RegExp(`${NB}io\\s*[.:]|\\bos\\s*\\.\\s*(getenv|execute|remove|rename|tmpname|exit)\\b`, 'g'), msg: 'io/os access in pure sim/content' },
  // Only globals count: a file that binds the name itself (`local sys = {}`, a parameter) is fine.
  { id: 'defold-api', re: new RegExp(`${NB}(go|msg|gui|vmath|sys|resource|html5|render|buffer|timer|socket|particlefx|profiler|collectionfactory)\\s*\\.|${NB}(pprint)\\s*\\(`, 'g'), msg: 'Defold API in pure sim/content', globalsOnly: true },
  // (S23, D-110) the browser's Lua 5.1 computes a ^ b with the C library's pow(); LuaJIT folds x ^ 2 to x * x: the
  // two worlds may differ in the last bit. Squares are x * x; anything else goes through sim/dmath.lua.
  { id: 'pow', re: /\^/g, msg: "'^' is pow() in the browser and folded by LuaJIT (use x * x, or sim/dmath.lua)" },
];

const LUAJIT_MODULES = new Set(['ffi', 'jit', 'table.new', 'table.clear', 'string.buffer', 'bit32', 'utf8']);

/** Counts commas at nesting depth 1 of the call whose '(' is at code[open]. */
function topLevelCommas(code, open) {
  let depth = 0;
  let commas = 0;
  for (let k = open; k < code.length; k++) {
    const ch = code[k];
    if (ch === '(' || ch === '{' || ch === '[') depth++;
    else if (ch === ')' || ch === '}' || ch === ']') {
      depth--;
      if (depth === 0) return commas;
    } else if (ch === ',' && depth === 1) commas++;
  }
  return commas;
}

const ARITY_RULES = [
  { id: 'math-log-base', re: /\bmath\s*\.\s*log\s*\(/g, maxCommas: 0, msg: 'math.log(x, base) is Lua 5.2+ (use math.log(x) / math.log(base))' },
  { id: 'xpcall-args', re: new RegExp(`${NB}xpcall\\s*\\(`, 'g'), maxCommas: 1, msg: 'xpcall(f, handler, args...) is Lua 5.2+ (wrap f in a closure)' },
  { id: 'string-rep-sep', re: /\bstring\s*\.\s*rep\s*\(/g, maxCommas: 1, msg: 'string.rep(s, n, sep) is Lua 5.2+' },
];

export function lintSource(src, relPath) {
  const { code, strings } = lex(src);
  const lineStarts = [0];
  for (let k = 0; k < code.length; k++) if (code[k] === '\n') lineStarts.push(k + 1);
  const lineOf = (off) => {
    let lo = 0, hi = lineStarts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (lineStarts[mid] <= off) lo = mid; else hi = mid - 1;
    }
    return lo + 1;
  };
  const rawLines = src.split('\n');
  const found = [];
  const add = (line, id, msg) => {
    const raw = rawLines[line - 1] || '';
    const allow = raw.match(/--.*lint:allow\s*([\w-]*)/);
    if (allow && (!allow[1] || allow[1] === id)) return;
    found.push({ file: relPath, line, rule: id, msg, text: raw.trim().slice(0, 100) });
  };
  const norm = relPath.split(path.sep).join('/');
  const pure = PURE_DIRS.some((d) => norm.startsWith(d));
  const rules = pure ? COMMON_RULES.concat(PURE_RULES) : COMMON_RULES;
  const bound = new Map();
  const isBound = (name) => {
    if (!bound.has(name)) {
      const local = new RegExp(String.raw`\blocal\s+(?:function\s+)?(?:[\w\s,]*,\s*)?${name}\b`);
      const param = new RegExp(String.raw`\bfunction\b[\w\s.:]*\([^)]*\b${name}\b[^)]*\)`);
      const loopVar = new RegExp(String.raw`\bfor\s+(?:[\w\s,]*,\s*)?${name}\b[\w\s,]*(=|\bin\b)`);
      bound.set(name, local.test(code) || param.test(code) || loopVar.test(code));
    }
    return bound.get(name);
  };
  for (const r of rules) {
    r.re.lastIndex = 0;
    let m;
    while ((m = r.re.exec(code))) {
      if (r.globalsOnly && isBound(m[1] || m[2])) continue;
      add(lineOf(m.index), r.id, r.msg);
    }
  }
  for (const r of ARITY_RULES) {
    r.re.lastIndex = 0;
    let m;
    while ((m = r.re.exec(code))) {
      const open = m.index + m[0].length - 1;
      if (topLevelCommas(code, open) > r.maxCommas) add(lineOf(m.index), r.id, r.msg);
    }
  }
  const reqRe = new RegExp(`${NB}require\\s*\\(?\\s*"@(\\d+)"`, 'g');
  let m;
  while ((m = reqRe.exec(code))) {
    const name = strings[Number(m[1])].value;
    if (LUAJIT_MODULES.has(name)) add(lineOf(m.index), 'require-luajit', `require("${name}") is not Lua 5.1`);
  }
  for (const s of strings) {
    for (const e of s.badEscapes) add(e.line, 'string-escape', `'${e.esc}' escape is Lua 5.2+`);
  }
  found.sort((a, b) => a.line - b.line);
  return found;
}

function collectFiles(targets) {
  const files = [];
  const walk = (p) => {
    let st;
    try { st = fs.statSync(p); } catch { return; }
    if (st.isDirectory()) {
      if (SKIP_DIRS.has(path.basename(p))) return;
      for (const e of fs.readdirSync(p).sort()) walk(path.join(p, e));
    } else if (EXTENSIONS.has(path.extname(p))) {
      files.push(p);
    }
  };
  for (const t of targets) walk(path.resolve(ROOT, t));
  return files;
}

export function lintPaths(targets = DEFAULT_DIRS) {
  const files = collectFiles(targets);
  const all = [];
  for (const f of files) {
    const relPath = path.relative(ROOT, f).split(path.sep).join('/');
    all.push(...lintSource(fs.readFileSync(f, 'utf8'), relPath));
  }
  return { files: files.length, violations: all };
}

// ---------------------------------------------------------------------------------------
// Self-test

const CASES = [
  // [scope-file, source, expected rule ids (sorted, may repeat)]
  ['sim/a.lua', 'goto continue', ['goto']],
  ['sim/a.lua', '::continue::', ['label']],
  ['game/a.lua', 'local q = a // b', ['int-div']],
  ['game/a.lua', 'local s = "\\x41"', ['string-escape']],
  ['game/a.lua', "local s = 'a\\z\n   b'", ['string-escape']],
  ['game/a.lua', 'local s = "\\u{48}"', ['string-escape']],
  ['game/a.lua', 'local ffi = require("ffi")\nffi.cdef[[int x;]]', ['ffi', 'require-luajit']],
  ['tools/a.lua', "local f = load('return 1')", ['load-string']],
  ['tools/a.lua', 'local f = load [[return 1]]', ['load-string']],
  ['sim/a.lua', 'local l = math.log(x, 2)', ['math-log-base']],
  ['sim/a.lua', 'local l = math.log(\n  f(a, b),\n  2)', ['math-log-base']],
  ['sim/a.lua', 'local a, b = table.unpack(t)', ['table-unpack']],
  ['sim/a.lua', 'local x = a & b', ['bitop']],
  ['sim/a.lua', 'local x = a | b', ['bitop']],
  ['sim/a.lua', 'local x = ~a', ['bitop']],
  ['sim/a.lua', 'local x = a ~ b', ['bitop']],
  ['sim/a.lua', 'local x = a << 2', ['bitop']],
  ['sim/a.lua', 'local x = a >> 2', ['bitop']],
  ['sim/a.lua', 'local r = math.random(1, 6)', ['nondeterministic']],
  ['sim/a.lua', 'local t = os.time() + os.clock()', ['nondeterministic', 'nondeterministic']],
  ['content/a.lua', 'local d = os.date("%Y")', ['nondeterministic']],
  ['sim/a.lua', 'io.write("x")', ['io']],
  ['sim/a.lua', 'local p = go.get_position()', ['defold-api']],
  ['sim/a.lua', 'local v = vmath.vector3(1, 2, 3)', ['defold-api']],
  ['sim/a.lua', 'timer.delay(1, false, f)', ['defold-api']],
  ['sim/a.lua', 'pprint(t)', ['defold-api']],
  ['sim/a.lua', 'local x <const> = 5', ['attrib']],
  ['sim/a.lua', 'local m = 0b101 + 1LL', ['number-literal', 'number-literal']],
  ['sim/a.lua', 'xpcall(f, handler, 1, 2)', ['xpcall-args']],
  ['sim/a.lua', 'local t = table.new(10, 0)', ['luajit-only']],
  ['sim/a.lua', 'local d = (x - a) ^ 2 + (y - b) ^ 2', ['pow', 'pow']],
  ['game/a.lua', 'local d = (x - a) ^ 2', []],
  ['sim/a.lua', 'local p = s:match("^%s*(.-)%s*$")', []],
  // Clean snippets (no findings expected)
  ['sim/a.lua', 'local url = "http://x.y//z" -- a // b goto x', []],
  ['sim/a.lua', 'if a ~= b then return end', []],
  ['sim/a.lua', 'local s = [[ a // b & c | d ~ e goto f ]]', []],
  ['sim/a.lua', 'local s = [==[ ]] still // inside ]==] local y = 1', []],
  ['sim/a.lua', '--[==[ multi\n line // comment ]] still ]==]\nlocal z = 1', []],
  ['sim/a.lua', 'local s = "\\\\x41"', []],
  ['sim/a.lua', 'local l = math.log(f(a, b)) + math.log10(x)', []],
  ['sim/a.lua', 'local f = loadstring("return 1")', []],
  ['sim/a.lua', 'local goto_count, x = 1, t[a[1]]', []],
  ['sim/a.lua', 'local u = unpack(t) xpcall(f, handler)', []],
  ['sim/a.lua', 'local v = "go.get_position() math.random()"', []],
  ['sim/a.lua', 'local rng = require("sim.rng") local hex = 0xFF + 1e10 + 3.5e-2', []],
  ['game/a.lua', 'local data = sys.load("f") local v = vmath.vector3() local n = bit.band(a, b)', []],
  ['tools/a.lua', 'local t0 = os.clock() io.write("ok")', []],
  ['sim/a.lua', 'local sys = {}\nfunction sys.update(W) end\nreturn sys', []],
  ['sim/a.lua', 'local function f(render, x) return render.y end', []],
  ['sim/a.lua', 'local M = {}\nfunction M.f(W) return sys.now() end', ['defold-api']],
  ['sim/a.lua', 'local x = a & b -- lint:allow bitop', []],
  ['sim/a.lua', "local s = 'it''s' .. \"q\\\"uote\" .. 'a//b'", []],
];

function selftest() {
  let fails = 0;
  for (const [file, src, expected] of CASES) {
    const got = lintSource(src, file).map((v) => v.rule).sort();
    const exp = [...expected].sort();
    const ok = got.length === exp.length && got.every((g, k) => g === exp[k]);
    if (!ok) {
      fails++;
      console.log(`FAIL ${file}: ${JSON.stringify(src)}\n     expected [${exp}] got [${got}]`);
    }
  }
  console.log(`lint:lua selftest: ${CASES.length - fails}/${CASES.length} passed`);
  return fails === 0;
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2).filter((a) => a !== '--');
  if (args.includes('--selftest')) {
    process.exit(selftest() ? 0 : 1);
  }
  const { files, violations } = lintPaths(args.length ? args : DEFAULT_DIRS);
  for (const v of violations) console.log(`${v.file}:${v.line}: ${v.rule}: ${v.msg}    | ${v.text}`);
  console.log(`lint:lua: ${files} file(s), ${violations.length} violation(s)`);
  process.exit(violations.length ? 1 : 0);
}
