-- Every famine of a run, with the village's state (session 8, D-074 diagnosis):
--   luajit tools/wip/famine_trace.lua <seed> [years=150] [--nodread] [--cap N]
-- For each famine: year, village (age, people), its dread, its store and fields a year before and
-- at the famine, last year's harvest against need, its boats' catch share, and the famine's chain.
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local pos, nodread, cap = {}, false, 2400
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--nodread" then nodread = true
  elseif a == "--cap" then cap = tonumber(arg[i + 1]); i = i + 1
  else pos[#pos + 1] = a end
  i = i + 1
end
local seed, years = tonumber(pos[1]) or 22, tonumber(pos[2]) or 150
local config = require("sim.config")
config.SOCIETY.soft_cap = cap
if nodread then
  for k, v in pairs(config.SOCIETY) do if k:match("^dread") and type(v) == "number" then config.SOCIETY[k] = 0 end end
end
local world = require("sim.world")
local events = require("sim.causality.events")
local W = require("sim.worldgen.init").generate(seed)
local prev = {}   -- village id -> state a year ago
local function snap(v)
  local ny = math.max(1, (v.need_day or 0) * 336)
  local a = v.acc_last or {}
  return { store = (v.stock.grain + v.stock.fresh) / ny, fields = #v.farms, pop = v.pop, dread = v.dread or 0,
    harv = (a.harvest or 0) / math.max(1, a.need or 1), catch = (a.catch or 0) / math.max(1, a.need or 1) }
end
local e0 = W.E.next_id
local n = 0
for y = 1, years do
  for t = 1, 48 do
    world.tick(W)
    for id = e0, W.E.next_id - 1 do
      local r = W.E.rec[id]
      if r and r.kind == "village_split" and type(r.data) == "table" and os.getenv("SPLITS") then
        local v = W.ent.villages[r.data.village]
        if v then
          local ny = math.max(1, v.need_day * 336)
          print(string.format("  split y%d from %s (pop %d, phase %s): parent store %.2fy after, %d leave (%s)", y, v.name, v.pop,
            v.season and v.season.phase or "?", (v.stock.grain + v.stock.fresh) / ny, r.mag or 0, tostring(r.data.name)))
        end
      end
      if r and r.kind == "famine" and type(r.data) == "table" then
        local v = W.ent.villages[r.data.village]
        if v then
          n = n + 1
          local p = prev[v.id]
          local now = snap(v)
          local ch = {}
          for _, e in ipairs(events.chain(W, id)) do ch[#ch + 1] = e.kind end
          print(string.format("y%3d %-16s age %3.0f pop %4d dread %.2f | a year ago: store %.2fy fields %d harvest %.2f catch %.2f | now store %.2fy fields %d | %s",
            y, v.name, (W.hour - v.founded) / 8064, v.pop, now.dread, p and p.store or -1, p and p.fields or -1, p and p.harv or -1,
            p and p.catch or -1, now.store, now.fields, table.concat(ch, ">")))
        end
      end
    end
    e0 = W.E.next_id
  end
  for _, v in ipairs(W.ent.villages) do if v.alive and v.pop > 0 then prev[v.id] = snap(v) end end
end
print(string.format("seed %d %s: %d famines", seed, nodread and "off" or "on", n))
