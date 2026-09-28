-- Counter-based deterministic RNG (05_ARCHITECTURE.md §5).
-- Pure Lua 5.1 + `bit` (LuaBitOp semantics: available in Defold on all platforms and in LuaJIT).
-- Every intermediate product stays below 2^53 so results are exact in doubles.
--
-- Usage:
--   rng.hash(seed, stream, a, b, c)  -> uint32        (general, 4 mixing rounds)
--   rng.float(seed, stream, a, b, c) -> [0,1)
--   Hot paths: precompute a per-tick base once, then one mixing round per cell:
--   local base = rng.base(seed, stream, tick)
--   local u = rng.fbase(base, cell)          -> [0,1)
--
-- Keys must come from *where and when* (cell, tick, entity creation key), never from
-- global counters, so that an action in one place doesn't reshuffle rolls elsewhere.

local bit = bit or require("bit")
local bxor, rshift = bit.bxor, bit.rshift

local M32 = 4294967296
local INV32 = 1 / 4294967296

local rng = {}

-- 32-bit multiply mod 2^32 with 16-bit splits (exact in doubles).
local function mul32(a, b)
  local alo = a % 65536
  local ahi = (a - alo) / 65536
  local blo = b % 65536
  local bhi = (b - blo) / 65536
  local mid = (ahi * blo + alo * bhi) % 65536
  return (alo * blo + mid * 65536) % M32
end
rng.mul32 = mul32

-- lowbias32 finaliser (good avalanche). x must be an integer in [0, 2^32).
local function mix(x)
  x = bxor(x, rshift(x, 16)) % M32
  x = mul32(x, 0x7feb352d)
  x = bxor(x, rshift(x, 15)) % M32
  x = mul32(x, 0x846ca68b)
  x = bxor(x, rshift(x, 16)) % M32
  return x
end
rng.mix = mix

-- Fold an arbitrary non-negative integer (< 2^52) into the running hash.
local function fold(h, v)
  v = v % M32
  return mix(bxor(h, mul32(v, 0x9E3779B1)) % M32)
end

function rng.hash(seed, stream, a, b, c)
  local h = mix((seed % M32 + stream * 0x632BE5AB) % M32)
  h = fold(h, a or 0)
  h = fold(h, b or 0)
  h = fold(h, c or 0)
  return h
end

function rng.float(seed, stream, a, b, c)
  return rng.hash(seed, stream, a, b, c) * INV32
end

-- Per-tick base for hot loops.
function rng.base(seed, stream, t)
  return rng.hash(seed, stream, t, 0, 0)
end

-- One mixing round: uint32 from base + small integer key (cell, entity index...).
function rng.ibase(base, k)
  return mix(bxor(base, mul32(k % M32, 0x85EBCA6B)) % M32)
end

function rng.fbase(base, k)
  return mix(bxor(base, mul32(k % M32, 0x85EBCA6B)) % M32) * INV32
end

-- Integer in [lo, hi] from a float u in [0,1).
function rng.range(u, lo, hi)
  return lo + math.floor(u * (hi - lo + 1))
end

-- Stream ids. Never renumber (would change every world).
rng.S = {
  WORLDGEN = 1, NAMES = 2, WEATHER_WOBBLE = 3, FIRE_IGNITE = 4, FIRE_SPREAD = 5,
  BIRTH = 6, DEATH = 7, PAIRING = 8, MIGRATION = 9, GEOLOGY = 10, FAUNA = 11,
  VILLAGE = 12, PERSON = 13, SHIPS = 14, OCEAN = 15, FLOOD = 16, CULL = 17,
  TRAITS = 18, TEST = 19, PLATES = 20, TERRAIN = 21, SETTLE = 22, BLIGHT = 23,
  LIGHTNING = 24, HUNT = 25, JOBS = 26, MEMORY = 27, POWERS = 28, METEOR = 29,
  WEATHER_DAY = 30, HOUSEHOLD = 31, BUILD = 32, BOAT = 33, LABOUR = 34, COURT = 35, TRADE = 36,
  PARTY = 37, HERD = 38, CONFLICT = 39, STATUS = 40,
}

return rng
