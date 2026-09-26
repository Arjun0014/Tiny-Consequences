-- Fast seeded 3D value noise for the view (visual only; never used by sim/).
-- A permutation table keyed by seed keeps visuals stable per world. Pure Lua 5.1.
local M = {}

local floor = math.floor

--- Creates a noise instance for `seed` (any integer).
function M.new(seed)
  local s = (math.abs(math.floor(seed or 1)) * 2654435761 + 12345) % 4294967296
  local function lcg()
    s = (s * 1664525 + 1013904223) % 4294967296
    return s
  end
  local p = {}
  for i = 0, 255 do p[i] = i end
  for i = 255, 1, -1 do
    local j = lcg() % (i + 1)
    p[i], p[j] = p[j], p[i]
  end
  local perm, vals = {}, {}
  for i = 0, 511 do perm[i] = p[i % 256] end
  for i = 0, 255 do vals[i] = lcg() / 4294967296 end
  return { perm = perm, vals = vals }
end

--- Single octave value noise in [0,1) at lattice coordinates (x, y, z).
function M.value(N, x, y, z)
  local perm, vals = N.perm, N.vals
  local ix, iy, iz = floor(x), floor(y), floor(z)
  local fx, fy, fz = x - ix, y - iy, z - iz
  fx = fx * fx * (3 - 2 * fx)
  fy = fy * fy * (3 - 2 * fy)
  fz = fz * fz * (3 - 2 * fz)
  ix, iy, iz = ix % 256, iy % 256, iz % 256
  local ix1, iy1, iz1 = (ix + 1) % 256, (iy + 1) % 256, (iz + 1) % 256
  local a0, a1 = perm[ix], perm[ix1]
  local b00, b01 = perm[a0 + iy], perm[a0 + iy1]
  local b10, b11 = perm[a1 + iy], perm[a1 + iy1]
  local c000, c001 = vals[perm[b00 + iz]], vals[perm[b00 + iz1]]
  local c010, c011 = vals[perm[b01 + iz]], vals[perm[b01 + iz1]]
  local c100, c101 = vals[perm[b10 + iz]], vals[perm[b10 + iz1]]
  local c110, c111 = vals[perm[b11 + iz]], vals[perm[b11 + iz1]]
  local x00 = c000 + (c100 - c000) * fx
  local x10 = c010 + (c110 - c010) * fx
  local x01 = c001 + (c101 - c001) * fx
  local x11 = c011 + (c111 - c011) * fx
  local y0 = x00 + (x10 - x00) * fy
  local y1 = x01 + (x11 - x01) * fy
  return y0 + (y1 - y0) * fz
end

--- Fractal noise centred on 0 (about [-0.5, 0.5]); freq = lattice cells per unit length.
function M.fbm(N, x, y, z, freq, octaves, gain)
  local value = M.value
  local sum, amp, norm = 0, 1, 0
  gain = gain or 0.5
  for o = 1, octaves do
    local off = o * 17.31
    sum = sum + amp * (value(N, x * freq + off, y * freq - off, z * freq + off * 0.5) - 0.5)
    norm = norm + amp
    amp = amp * gain
    freq = freq * 2
  end
  return sum / norm
end

--- Ridged noise in [0,1] (sharp crests), for mountain relief.
function M.ridged(N, x, y, z, freq, octaves)
  local value = M.value
  local sum, amp, norm = 0, 1, 0
  for o = 1, octaves do
    local off = o * 9.73
    local v = 1 - math.abs(value(N, x * freq + off, y * freq + off, z * freq - off) * 2 - 1)
    sum = sum + amp * v * v
    norm = norm + amp
    amp = amp * 0.5
    freq = freq * 2
  end
  return sum / norm
end

return M
