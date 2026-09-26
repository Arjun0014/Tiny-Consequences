-- 3D value noise on the unit sphere (worldgen only; not a hot path). Hash-based lattice.
local rng = require("sim.rng")

local noise = {}
local floor = math.floor

local function lattice(seed, stream, ix, iy, iz)
  -- offset to keep keys non-negative
  return rng.float(seed, stream, ix + 4096, iy + 4096, iz + 4096)
end

local function smooth(t) return t * t * (3 - 2 * t) end

-- Single octave value noise at (x,y,z) in lattice units. Returns [0,1).
function noise.value(seed, stream, x, y, z)
  local ix, iy, iz = floor(x), floor(y), floor(z)
  local fx, fy, fz = smooth(x - ix), smooth(y - iy), smooth(z - iz)
  local c000 = lattice(seed, stream, ix, iy, iz)
  local c100 = lattice(seed, stream, ix + 1, iy, iz)
  local c010 = lattice(seed, stream, ix, iy + 1, iz)
  local c110 = lattice(seed, stream, ix + 1, iy + 1, iz)
  local c001 = lattice(seed, stream, ix, iy, iz + 1)
  local c101 = lattice(seed, stream, ix + 1, iy, iz + 1)
  local c011 = lattice(seed, stream, ix, iy + 1, iz + 1)
  local c111 = lattice(seed, stream, ix + 1, iy + 1, iz + 1)
  local x00 = c000 + (c100 - c000) * fx
  local x10 = c010 + (c110 - c010) * fx
  local x01 = c001 + (c101 - c001) * fx
  local x11 = c011 + (c111 - c011) * fx
  local y0 = x00 + (x10 - x00) * fy
  local y1 = x01 + (x11 - x01) * fy
  return y0 + (y1 - y0) * fz
end

-- Fractal noise, centred on 0 (≈ [-0.5, 0.5]). freq = lattice cells across the unit radius.
function noise.fbm(seed, stream, x, y, z, freq, octaves, gain)
  local sum, amp, norm = 0, 1, 0
  gain = gain or 0.5
  for o = 1, octaves do
    sum = sum + amp * (noise.value(seed, stream + o * 101, x * freq, y * freq, z * freq) - 0.5)
    norm = norm + amp
    amp = amp * gain
    freq = freq * 2
  end
  return sum / norm
end

return noise
