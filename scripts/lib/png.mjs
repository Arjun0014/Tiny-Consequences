// A minimal PNG reader (8-bit RGB/RGBA, not interlaced - what Chromium's screenshots are) for measuring frames:
// decodePng(buffer) -> { w, h, px: Uint8Array RGBA }.
import zlib from 'node:zlib';

export function decodePng(buf) {
  if (buf.readUInt32BE(0) !== 0x89504e47) throw new Error('not a PNG');
  let pos = 8;
  let w = 0, h = 0, depth = 0, ctype = 0, inter = 0;
  const idat = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    const data = buf.subarray(pos + 8, pos + 8 + len);
    if (type === 'IHDR') {
      w = data.readUInt32BE(0); h = data.readUInt32BE(4); depth = data[8]; ctype = data[9]; inter = data[12];
    } else if (type === 'IDAT') idat.push(data);
    else if (type === 'IEND') break;
    pos += 12 + len;
  }
  if (depth !== 8 || inter !== 0 || (ctype !== 2 && ctype !== 6)) throw new Error(`unsupported PNG (depth ${depth}, colour ${ctype}, interlace ${inter})`);
  const bpp = ctype === 6 ? 4 : 3;
  const raw = zlib.inflateSync(Buffer.concat(idat));
  const stride = w * bpp;
  const out = new Uint8Array(w * h * 4);
  const prev = new Uint8Array(stride);
  const line = new Uint8Array(stride);
  let r = 0;
  for (let y = 0; y < h; y++) {
    const f = raw[r++];
    for (let i = 0; i < stride; i++) {
      const x = raw[r++];
      const a = i >= bpp ? line[i - bpp] : 0;
      const b = prev[i];
      const c = i >= bpp ? prev[i - bpp] : 0;
      let v;
      if (f === 0) v = x;
      else if (f === 1) v = x + a;
      else if (f === 2) v = x + b;
      else if (f === 3) v = x + ((a + b) >> 1);
      else {
        const p = a + b - c;
        const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
        v = x + (pa <= pb && pa <= pc ? a : (pb <= pc ? b : c));
      }
      line[i] = v & 255;
    }
    for (let i = 0, o = y * w * 4; i < w; i++, o += 4) {
      out[o] = line[i * bpp]; out[o + 1] = line[i * bpp + 1]; out[o + 2] = line[i * bpp + 2]; out[o + 3] = bpp === 4 ? line[i * bpp + 3] : 255;
    }
    prev.set(line);
  }
  return { w, h, px: out };
}

// The share of the frame where two images of the same view differ by more than `thr` (mean of |dR|,|dG|,|dB|),
// within the rectangle [x0, x1) x [y0, y1) (pixels; default the whole frame).
export function diffShare(a, b, thr = 24, rect = null) {
  const x0 = rect ? rect[0] : 0, y0 = rect ? rect[1] : 0;
  const x1 = rect ? rect[2] : a.w, y1 = rect ? rect[3] : a.h;
  let n = 0, k = 0, sum = 0;
  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const o = (y * a.w + x) * 4;
      const d = (Math.abs(a.px[o] - b.px[o]) + Math.abs(a.px[o + 1] - b.px[o + 1]) + Math.abs(a.px[o + 2] - b.px[o + 2])) / 3;
      sum += d;
      if (d > thr) k++;
      n++;
    }
  }
  return { share: k / n, mean: sum / n };
}
