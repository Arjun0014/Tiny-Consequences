// npm run play — release build (if stale) + static server on :8080 + open the default browser.
// Ctrl+C to stop. Flags: --debug (play the debug bundle), --port N
import { spawn } from 'node:child_process';
import { IS_WIN, die, getFlag, hasFlag } from './lib/util.mjs';
import { build } from './build.mjs';
import { startServer } from './serve.mjs';

const args = process.argv.slice(2);
const port = Number(getFlag(args, '--port', 8080));
await build({ variant: hasFlag(args, '--debug') ? 'debug' : 'release', ifStale: true });
try {
  await startServer({ port, quiet: true });
} catch (e) {
  die(e.code === 'EADDRINUSE' ? `port ${port} is already in use (another \`npm run serve\`/\`play\`?)` : e.message);
}
const url = `http://localhost:${port}/`;
console.log(`play: ${url}  (Ctrl+C to stop)`);
const opener = IS_WIN
  ? ['cmd.exe', ['/d', '/c', 'start', '""', url]]
  : process.platform === 'darwin' ? ['open', [url]] : ['xdg-open', [url]];
spawn(opener[0], opener[1], { stdio: 'ignore', detached: true }).on('error', () => {
  console.log(`(could not open a browser automatically; open ${url} yourself)`);
}).unref();
