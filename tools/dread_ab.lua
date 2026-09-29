-- Does the memory of hunger (dread, D-064) change what villages do and what happens to them?
-- One run per call; compare arms over several seeds (tools/dread_ab.py summarises the CSVs).
--   luajit tools/dread_ab.lua <seed> [years=150] [--nodread] [--cap N] [--out file.csv]
-- --nodread switches the behaviour off (dread is still measured, so the arms are comparable):
-- the margin and store terms of farming.field_want and anything else keyed on SOCIETY.dread_*.
-- One row per village-year: population, dread, grain in store (years of need; monthly mean and
-- at the harvest), storage capacity and how often the store was at it, fields, sown, harvest,
-- need, spoilage, deaths (starvation, all), emigrants, events (famine, rationing, seed eaten,
-- crop failure).
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local seed, years = 22, 150
local nodread, cap, outp = false, nil, nil
local pos = {}
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--nodread" then nodread = true
  elseif a == "--cap" then cap = tonumber(arg[i + 1]); i = i + 1
  elseif a == "--out" then outp = arg[i + 1]; i = i + 1
  else pos[#pos + 1] = a end
  i = i + 1
end
seed = tonumber(pos[1]) or seed
years = tonumber(pos[2]) or years
local config = require("sim.config")
if cap then config.SOCIETY.soft_cap = cap end
if nodread then
  for k, v in pairs(config.SOCIETY) do if k:match("^dread") and type(v) == "number" then config.SOCIETY[k] = 0 end end
end
local world = require("sim.world")
local construction = require("sim.society.construction")
local W = require("sim.worldgen.init").generate(seed)
local out = assert(io.open(outp or string.format("out/dread_%d_%s.csv", seed, nodread and "off" or "on"), "w"))
out:write("seed,arm,year,village,pop,dread,store_y,store_harv_y,cap_y,at_cap,fields,sown,harvest,need,spoil_g,starved,deaths,mig_out,famine,rationing,seed_eaten,crop_failure\n")
local arm = nodread and "off" or "on"
local mon = {}   -- village id -> { n, store_y, cap_y, at_cap }
local ev = {}    -- village id -> kind -> count (this year)
local last_ev = W.E.next_id
local KINDS = { famine = true, rationing = true, seed_eaten = true, crop_failure = true }
for y = 1, years do
  for t = 1, 48 do
    world.tick(W)
    if t % 4 == 0 then
      for _, v in ipairs(W.ent.villages) do
        if v.alive and v.pop > 0 then
          local need_y = math.max(1, (v.need_day or 0) * 336)
          local capg = construction.grain_capacity(W, v)
          local m = mon[v.id]
          if not m then m = { n = 0, s = 0, c = 0, at = 0 }; mon[v.id] = m end
          m.n = m.n + 1
          m.s = m.s + v.stock.grain / need_y
          m.c = m.c + capg / need_y
          if v.stock.grain >= 0.9 * capg then m.at = m.at + 1 end
        end
      end
    end
  end
  for id = last_ev, W.E.next_id - 1 do
    local r = W.E.rec[id]
    if r and KINDS[r.kind] and type(r.data) == "table" and r.data.village then
      local e = ev[r.data.village]
      if not e then e = {}; ev[r.data.village] = e end
      e[r.kind] = (e[r.kind] or 0) + 1
    end
  end
  last_ev = W.E.next_id
  for _, v in ipairs(W.ent.villages) do
    local a = v.acc_last
    local m = mon[v.id]
    if a and m and m.n > 0 then
      local starved = 0
      for k, x in pairs(a) do if k:sub(1, 2) == "d_" and (k:find("starv") or k:find("hunger") or k:find("malnut")) then starved = starved + x end end
      local e = ev[v.id] or {}
      local need_y = math.max(1, (v.need_day or 0) * 336)
      out:write(string.format("%d,%s,%d,%d,%d,%.3f,%.3f,%.3f,%.3f,%.3f,%d,%.0f,%.0f,%.0f,%.0f,%.1f,%.1f,%d,%d,%d,%d,%d\n",
        seed, arm, y, v.id, v.pop, v.dread or 0, m.s / m.n, (v.store_at_harvest or v.stock.grain) / need_y, m.c / m.n, m.at / m.n,
        #v.farms, a.sown or 0, a.harvest or 0, a.need or 0, a.spoil_g or 0, starved, a.deaths or 0, a.mig_out or 0,
        e.famine or 0, e.rationing or 0, e.seed_eaten or 0, e.crop_failure or 0))
    end
  end
  mon, ev = {}, {}
  if y % 25 == 0 then
    local pop, nv = 0, 0
    for _, v in ipairs(W.ent.villages) do if v.alive then pop = pop + v.pop; nv = nv + 1 end end
    io.write(string.format("seed %d %s y%d: %d villages, %d people\n", seed, arm, y, nv, pop))
  end
end
out:close()
