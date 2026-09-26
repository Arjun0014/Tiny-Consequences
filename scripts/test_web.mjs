// npm run test:web — build the bundle if stale, then run the Playwright suite.
// Flags (before `--`): --chapter N (default: PROGRESS.md "Chapter: N"), --release (test the
// release bundle), --no-build, --headed. Everything else is passed to `playwright test`
// (e.g. `npm run test:web -- -g W01`).
import path from 'node:path';
import { ROOT, currentChapter, hasFlag, run } from './lib/util.mjs';
import { build } from './build.mjs';

const argv = process.argv.slice(2);
const own = new Set(['--release', '--no-build', '--headed']);
const passthrough = [];
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a === '--') continue;
  if (own.has(a)) continue;
  if (a === '--chapter') { i++; continue; }
  if (a.startsWith('--chapter=')) continue;
  passthrough.push(a);
}

const chapter = currentChapter(argv);
const variant = hasFlag(argv, '--release') ? 'release' : 'debug';
if (!hasFlag(argv, '--no-build')) {
  await build({ variant, ifStale: true, quiet: true });
}

console.log(`test:web: chapter ≤ ${chapter}, ${variant} bundle`);
const env = { ...process.env, TC_CHAPTER: String(chapter) };
if (hasFlag(argv, '--headed')) env.TC_HEADED = '1';
const cli = path.join(ROOT, 'node_modules', '@playwright', 'test', 'cli.js');
const code = await run(process.execPath, [cli, 'test', '--config', 'playwright.config.mjs', ...passthrough], { env });
process.exit(code);
