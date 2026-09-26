// npm run serve — static server for the built bundle (dist/web/app) on http://localhost:8080.
// Flags: --port N (default 8080, env PORT), --dir <path> (default dist/web/app), --quiet
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { APP_DIR, ROOT, die, getFlag, hasFlag, isMain, rel } from './lib/util.mjs';

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.wasm': 'application/wasm',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
  '.txt': 'text/plain; charset=utf-8',
  '.ogg': 'audio/ogg',
  '.wav': 'audio/wav',
  '.mp3': 'audio/mpeg',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
};

export function startServer({ port = 8080, dir = APP_DIR, quiet = false } = {}) {
  const root = path.resolve(dir);
  if (!fs.existsSync(path.join(root, 'index.html'))) {
    die(`no index.html in ${rel(root)} — run \`npm run build\` first`);
  }
  const server = http.createServer((req, res) => {
    let urlPath;
    try {
      urlPath = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    } catch {
      res.writeHead(400).end('bad request');
      return;
    }
    let file = path.resolve(root, '.' + urlPath);
    if (file !== root && !file.startsWith(root + path.sep)) {
      res.writeHead(403).end('forbidden');
      return;
    }
    try {
      if (fs.statSync(file).isDirectory()) file = path.join(file, 'index.html');
    } catch { /* handled below */ }
    fs.stat(file, (err, st) => {
      if (err || !st.isFile()) {
        if (!quiet) console.log(`404 ${req.method} ${req.url}`);
        res.writeHead(404, { 'Content-Type': 'text/plain', 'Cache-Control': 'no-store' }).end('not found');
        return;
      }
      res.writeHead(200, {
        'Content-Type': MIME[path.extname(file).toLowerCase()] || 'application/octet-stream',
        'Content-Length': st.size,
        'Cache-Control': 'no-store, no-cache, must-revalidate, max-age=0',
        Pragma: 'no-cache',
        Expires: '0',
      });
      if (req.method === 'HEAD') {
        res.end();
        return;
      }
      fs.createReadStream(file).pipe(res);
    });
  });
  return new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(port, '127.0.0.1', () => {
      if (!quiet) console.log(`serve: http://localhost:${port}/  (${rel(root) || root})`);
      resolve(server);
    });
  });
}

if (isMain(import.meta.url)) {
  const args = process.argv.slice(2);
  const port = Number(getFlag(args, '--port', process.env.PORT || 8080));
  const dir = path.resolve(ROOT, getFlag(args, '--dir', APP_DIR));
  try {
    await startServer({ port, dir, quiet: hasFlag(args, '--quiet') });
  } catch (e) {
    die(e.code === 'EADDRINUSE' ? `port ${port} is already in use` : e.message);
  }
}
