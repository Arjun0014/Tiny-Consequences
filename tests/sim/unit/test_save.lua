local T = require("tests.sim.lib")
local world = require("sim.world")
local save = require("sim.save")
local toy = require("tests.sim.support.toy")
local M = {}

function M.encode_decode_values()
  local snap = { seed = 5, level = 3, tick = 96, G = { co2 = 281.123456789012345, era = "garden" },
    static = { elev = { 1, -2.5, 3 } }, ent = { list = { { a = 1, b = "x", c = true }, { a = 2 } } },
    E = { rec = {}, next_id = 1 }, D = {}, S = { big = 3000000000, neg = -7, f = 1 / 3 },
    names = {}, pending = {}, I = {}, C = {},
    F = { grass = { 0, require("sim.fields").quantize_sqrt(0.25, 1), 1 }, biome = { 1, 2, 2 } }, P = { oil = { 0, 0.5, 1 } }, cmdlog_len = 0 }
  local s = save.encode(snap, {}, 100, { hello = "world" })
  local doc = assert(save.decode(s))
  T.eq(doc.G.co2, snap.G.co2, "double round trip")
  T.eq(doc.S.big, 3000000000)
  T.eq(doc.S.f, 1 / 3)
  T.eq(doc.ent.list[1].b, "x")
  T.eq(doc.ent.list[1].c, true)
  T.eq(doc.F.grass[2], snap.F.grass[2])
  T.eq(doc.F.biome[3], 2)
  T.eq(doc.P.oil[3], 1)
  T.eq(doc.current_tick, 100)
  T.eq(doc.extras.hello, "world")
end

function M.version_mismatch()
  local ok, err = save.decode("TCSAVE1|999|{}")
  T.eq(ok, nil); T.eq(err, "version")
  local ok2, err2 = save.decode("garbage")
  T.eq(ok2, nil); T.eq(err2, "not a save")
end

function M.world_round_trip_bit_identical()
  -- P02 on the toy world: save mid-year, continue; restore + continue; hashes equal
  local W = toy.make_world(11, 3)
  save.enable_capture(W)
  world.run_ticks(W, 48 * 2 + 10)
  world.command(W, "toy_spill", { cell = 17 })
  world.run_ticks(W, 5)
  local s = save.save_world(W, { cmdlog = W.cmdlog })
  world.run_ticks(W, 48 * 3)
  local h1 = world.state_hash(W)
  local doc = assert(save.decode(s))
  local W2 = save.restore(doc)
  T.eq(W2.tick, 48 * 2 + 15)
  world.run_ticks(W2, 48 * 3)
  local h2 = world.state_hash(W2)
  T.eq(h1, h2, "state hash after reload")
  return string.format("save size %d bytes", #s)
end

return M
