-- A toy system for testing the world/save/determinism machinery before real systems exist.
-- Writes a few fields with neighbour reads (prev buffers), rng, events and blame.
local rng = require("sim.rng")
local blame = require("sim.causality.blame")
local events = require("sim.causality.events")
local fields = require("sim.fields")

local toy = {}

function toy.update(W)
  local n, g = W.n, W.grid
  local F, P, C, I = W.F, W.P, W.C, W.I
  local grass, oil, poil = F.grass, F.oil, P.oil
  fields.snapshot(W, "oil")
  local base = rng.base(W.seed, rng.S.TEST, W.tick)
  local nbr, cnt = g.nbr, g.nbr_count
  local ctag, cidle = C.oil, I.oil
  for c = 1, n do
    local b = c * 6 - 6
    local s = 0
    for k = 1, cnt[c] do s = s + poil[nbr[b + k]] end
    local old = oil[c]
    local new = 0.5 * poil[c] + 0.5 * s / cnt[c]
    new = new * 0.99
    oil[c] = new
    local t = ctag[c]
    local it = 0
    for k = 1, cnt[c] do local tj = ctag[nbr[b + k]]; if tj ~= 0 then it = tj end end
    if t ~= 0 or it ~= 0 then
      blame.step(ctag, cidle, c, new - old, 1e-6, 48, new - old, it, 0, 0, 0, 0, 0, 0)
    end
    grass[c] = grass[c] + (rng.fbase(base, c) - 0.5) * 0.01
  end
  if W.tick % 48 == 7 then
    events.emit(W, "biome_shift", 1 + W.tick % n, 0, 1)
  end
end

-- a test command: spill oil at a cell
local world = require("sim.world")
world.COMMANDS.toy_spill = function(W, p)
  local id = events.emit(W, "test_oil", p.cell, 0, 1)
  W.F.oil[p.cell] = 1
  blame.set(W.C.oil, W.I.oil, p.cell, id)
end

function toy.make_world(seed, level)
  world.set_systems({ toy })
  local W = world.new(seed, { level = level or 3 })
  for c = 1, W.n do W.F.grass[c] = 0.5 end
  return W
end

return toy
