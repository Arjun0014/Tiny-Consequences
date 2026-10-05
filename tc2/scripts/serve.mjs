// Static server for the TC2 Web build. Sends COOP/COEP/CORP so threaded builds get SharedArrayBuffer, the right
// MIME types, and Content-Encoding for .br/.gz files (the build also carries a decompression fallback).
// Usage: node tc2/scripts/serve.mjs [--dir tc2/web-build/web] [--port 8642]
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream',
  '.json': 'application/json', '.css': 'text/css', '.png': 'image/png', '.jpg': 'image/jpeg', '.ico': 'image/x-icon', '.svg': 'image/svg+xml',
  '.unityweb': 'application/octet-stream', '.mem': 'application/octet-stream', '.symbols.json': 'application/json',
};

export function startServer({ dir = path.join(root, 'tc2', 'web-build', 'web'), port = 8642, isolate = true, quiet = false } = {}) {
  const base = path.resolve(dir);
  const server = http.createServer((req, res) => {
    const url = decodeURIComponent((req.url || '/').split('?')[0]);
    let file = path.join(base, url === '/' ? 'index.html' : url);
    if (!file.startsWith(base)) { res.writeHead(403); res.end(); return; }
    fs.stat(file, (err, st) => {
      if (err || !st.isFile()) { res.writeHead(404); res.end('not found'); if (!quiet) console.log('404 ' + url); return; }
      const headers = { 'Cache-Control': 'no-store' };
      if (isolate) {
        headers['Cross-Origin-Opener-Policy'] = 'same-origin';
        headers['Cross-Origin-Embedder-Policy'] = 'require-corp';
        headers['Cross-Origin-Resource-Policy'] = 'cross-origin';
      }
      let name = file;
      if (name.endsWith('.br')) { headers['Content-Encoding'] = 'br'; name = name.slice(0, -3); }
      else if (name.endsWith('.gz')) { headers['Content-Encoding'] = 'gzip'; name = name.slice(0, -3); }
      const ext = name.endsWith('.symbols.json') ? '.symbols.json' : path.extname(name);
      headers['Content-Type'] = TYPES[ext] || 'application/octet-stream';
      headers['Content-Length'] = st.size;
      res.writeHead(200, headers);
      fs.createReadStream(file).pipe(res);
    });
  });
  return new Promise((resolve) => server.listen(port, '127.0.0.1', () => { if (!quiet) console.log(`serving ${base} on http://127.0.0.1:${port}`); resolve(server); }));
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const a = process.argv.slice(2);
  const opt = (f, d) => { const i = a.indexOf(f); return i >= 0 ? a[i + 1] : d; };
  startServer({ dir: opt('--dir', undefined), port: Number(opt('--port', 8642)), isolate: !a.includes('--no-isolate') });
}
