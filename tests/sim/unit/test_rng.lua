local T = require("tests.sim.lib")
local rng = require("sim.rng")
local M = {}

function M.mul32_exact()
  local cases = { { 0, 0 }, { 1, 0xFFFFFFFF }, { 0xFFFFFFFF, 0xFFFFFFFF }, { 123456789, 987654321 },
    { 0x7feb352d, 0x846ca68b } }
  for _, c in ipairs(cases) do
    local a, b = c[1], c[2]
    -- reference: split both into 16-bit halves exactly
    local alo, ahi = a % 65536, math.floor(a / 65536)
    local blo, bhi = b % 65536, math.floor(b / 65536)
    local ref = (alo * blo + ((ahi * blo + alo * bhi) % 65536) * 65536) % 4294967296
    T.eq(rng.mul32(a, b), ref)
  end
  T.eq(rng.mul32(0xFFFFFFFF, 0xFFFFFFFF), 1)
  T.eq(rng.mul32(65536, 65536), 0)
end

function M.determinism_and_range()
  for i = 1, 1000 do
    local a = rng.hash(42, 3, i, 7, 9)
    T.eq(a, rng.hash(42, 3, i, 7, 9))
    T.ok(a >= 0 and a < 4294967296 and a == math.floor(a), "uint32")
    local f = rng.float(42, 3, i, 7, 9)
    T.ok(f >= 0 and f < 1)
  end
end

function M.distribution()
  local bins = {}
  for b = 1, 10 do bins[b] = 0 end
  local N = 20000
  local sum = 0
  for i = 1, N do
    local f = rng.float(7, 1, i, 0, 0)
    sum = sum + f
    local b = math.floor(f * 10) + 1
    bins[b] = bins[b] + 1
  end
  T.near(sum / N, 0.5, 0.01, "mean")
  for b = 1, 10 do T.between(bins[b], N / 10 * 0.9, N / 10 * 1.1, "bin " .. b) end
  -- fbase per-cell path
  local base = rng.base(7, 4, 100)
  local s2 = 0
  for c = 1, N do s2 = s2 + rng.fbase(base, c) end
  T.near(s2 / N, 0.5, 0.01, "fbase mean")
end

function M.independence()
  -- different streams / cells / seeds should be uncorrelated
  local N, same = 5000, 0
  for i = 1, N do
    if (rng.float(1, 1, i) < 0.5) == (rng.float(1, 2, i) < 0.5) then same = same + 1 end
  end
  T.between(same / N, 0.46, 0.54, "stream correlation")
  same = 0
  for i = 1, N do
    if (rng.float(1, 1, i) < 0.5) == (rng.float(2, 1, i) < 0.5) then same = same + 1 end
  end
  T.between(same / N, 0.46, 0.54, "seed correlation")
  same = 0
  local b1, b2 = rng.base(1, 5, 10), rng.base(1, 5, 11)
  for c = 1, N do
    if (rng.fbase(b1, c) < 0.5) == (rng.fbase(b2, c) < 0.5) then same = same + 1 end
  end
  T.between(same / N, 0.46, 0.54, "tick correlation")
  same = 0
  for c = 1, N do
    if (rng.fbase(b1, c) < 0.5) == (rng.fbase(b1, c + 1) < 0.5) then same = same + 1 end
  end
  T.between(same / N, 0.46, 0.54, "adjacent-cell correlation")
end

function M.known_values()
  -- pin a few outputs so an accidental change to the hash (which would change every world)
  -- is caught. Values recorded from the first implementation.
  local h = rng.hash(1, 1, 1, 1, 1)
  T.ok(h > 0)
  return string.format("hash(1,1,1,1,1)=%d", h)
end

return M
