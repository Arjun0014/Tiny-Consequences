-- Exchange between villages over a run (W7): relief, trade by kind, brides, famines; trails; each
-- village's usual imports and exports (the normals the field plan and woodcutters use).
-- Usage (repo root): luajit tools/exchange_report.lua [--seed N] [--years Y] [--every K]
package.path = "./?.lua;./?/init.lua;" .. package.path
io.stdout:setvbuf("line")
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local worlds = require("sim.worlds")
local world = require("sim.world")
local events = require("sim.causality.events")

local args = { seed = 22, years = 100, every = 10 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--seed" then i = i + 1; args.seed = tonumber(arg[i])
  elseif a == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i]) end
  i = i + 1
end

local W = worlds.generate(args.seed)
local counts = {}
local last = W.E.next_id - 1
local chains = {}
local function count(k) counts[k] = (counts[k] or 0) + 1 end
for y = 1, args.years do
  world.run_years(W, 1)
  for id = last + 1, W.E.next_id - 1 do
    local r = W.E.rec[id]
    if r then
      count(r.kind)
      if r.kind == "trade" and r.data then count("trade_" .. tostring(r.data.give) .. "_for_" .. tostring(r.data.get)); count("get_" .. tostring(r.data.get)) end
      if (r.kind == "raid" or r.kind == "raid_repelled" or r.kind == "village_split" or r.kind == "submission" or r.kind == "secession") and #chains < 30 then
        local ch = events.chain(W, id)
        local ks = {}
        for q = 1, #ch do ks[q] = ch[q].kind end
        local d = r.data or {}
        chains[#chains + 1] = string.format("y%d %s [%s: from %s, raiders %s, dead %s/%s, loot %s]", y, table.concat(ks, ">"),
          r.kind, tostring(d.from), tostring(d.raiders), tostring(d.dead_raiders), tostring(d.dead_defenders), tostring(d.loot))
      elseif (r.kind == "relief" or r.kind == "rationing") and #chains < 12 then
        local ch = events.chain(W, id)
        local ks = {}
        for q = 1, #ch do ks[q] = ch[q].kind end
        if #ks >= 3 then chains[#chains + 1] = string.format("y%d %s", y, table.concat(ks, ">")) end
      end
    end
  end
  last = W.E.next_id - 1
  if y % args.every == 0 then
    local alive, pop = 0, 0
    for _, v in ipairs(W.ent.villages) do if v.alive then alive = alive + 1; pop = pop + v.pop end end
    local nrel = 0
    for _ in pairs(W.ent.rel or {}) do nrel = nrel + 1 end
    local ntr, maxtr = 0, 0
    for _, t in pairs(W.S.trails or {}) do ntr = ntr + 1; if t > maxtr then maxtr = t end end
    local m = require("sim.metrics").compute(W)
    local farms = 0
    for c = 1, W.n do if W.F.land_use[c] > 0 then farms = farms + 1 end end
    local cb = require("sim.society.carbon").by
    local kk = {}
    for k, x in pairs(cb) do kk[#kk + 1] = string.format("%s %.1f", k, x) end
    table.sort(kk)
    print(string.format("      co2 %.0f ppm, g_temp %+.2f, sea %+.1f m, forest_nat %.2f, farm cells %d | biomass released: %s", W.G.co2 or 0, W.G.g_temp or 0,
      (W.G.sea_level or 0) * 1000, m.forest_nat or 0, farms, table.concat(kk, ", ")))
    local pal = 0
    for _, b in pairs(W.ent.buildings) do if b.kind == "palisade" and b.state == "standing" then pal = pal + 1 end end
    local conflict = require("sim.society.conflict")
    local pol = {}
    for _, v in ipairs(W.ent.villages) do
      if v.alive and v.lord then
        local top = conflict.polity(W, v)
        pol[top.name] = conflict.tiers(W, top)
      end
    end
    local ps = {}
    for k, t in pairs(pol) do ps[#ps + 1] = k .. " (" .. t .. " tiers)" end
    table.sort(ps)
    print(string.format("      raids %d repelled %d splits %d leaders %d palisades %d | submissions %d secessions %d tribute %d | polities: %s",
      counts.raid or 0, counts.raid_repelled or 0, counts.village_split or 0, counts.new_leader or 0, pal,
      counts.submission or 0, counts.secession or 0, counts.tribute or 0, table.concat(ps, ", ")))
    print(string.format("y%3d villages %2d pop %4d rel %2d | relief %3d trade %3d (food %d wood %d tools %d pots %d cloth %d) bride %3d | famine %2d rationing %3d migration %3d | trails %3d cells (max %.0f)",
      y, alive, pop, nrel, counts.relief or 0, counts.trade or 0, counts.get_food or 0, counts.get_wood or 0,
      counts.get_tools or 0, counts.get_pots or 0, counts.get_cloth or 0,
      counts.bride or 0, counts.famine or 0, counts.rationing or 0, counts.migration or 0, ntr, maxtr))
  end
end
local crafts = require("sim.society.crafts")
print("crafts (source quality, stock in years of need, made/bought last year, buys?):")
for _, v in ipairs(W.ent.villages) do
  if v.alive then
    local parts = {}
    local a = v.acc_last or {}
    for _, g in ipairs(crafts.GOODS) do
      local need = crafts.need_year(W, v, g)
      local e = v.crafts and v.crafts[g]
      parts[#parts + 1] = string.format("%s q%.2f %.1fy made %.0f bought %.0f sold %.0f%s", g, e and e.q or 0,
        need > 0 and (v.stock[g] or 0) / need or 0, a["made_" .. g] or 0, a[g .. "_bought"] or 0, a[g .. "_sold"] or 0,
        crafts.buys(v, g) and " BUY" or "")
    end
    print(string.format("  %-16s %s", v.name, table.concat(parts, " | ")))
  end
end
print("villages (usual weekly imports/exports: food FU, wood WU):")
for _, v in ipairs(W.ent.villages) do
  if v.alive then
    local N = v.norm
    print(string.format("  %-16s pop %3d farms %2d boats %d | food imp %6.1f exp %6.1f | wood imp %6.1f exp %6.1f | food %4.0f d, wood %4.0f d",
      v.name, v.pop, #v.farms, #v.boats, N.food_imp or 0, N.food_exp or 0, N.wood_imp or 0, N.wood_exp or 0,
      v.food_days or 0, v.wood_days or 0))
  end
end
for _, c in ipairs(chains) do print(c) end
local cw = require("sim.society.conflict").why
local ks = {}
for k, n in pairs(cw) do ks[#ks + 1] = k .. " " .. n end
table.sort(ks)
print("raid gates: " .. table.concat(ks, ", "))
if os.getenv("TC_DEBUG_TRADE") then
  local rel = require("sim.society.relations")
  for _, v in ipairs(W.ent.villages) do
    if v.alive and crafts.buys(v, "tools") then
      local w, u = rel.want_of(W, v, "tools")
      print(v.name, "want tools", w, u, "porters_out", v.porters_out, "famine", v.famine, "market", v.market.tools.id, v.market.tools.q, v.market.tools.d)
      for _, g in ipairs(rel.GOODS) do io.write(string.format(" %s spare %.0f", g, rel.spare_of(W, v, g))) end
      print()
      local m = W.ent.villages[v.market.tools.id]
      if m then print("  supplier", m.name, "spare tools", rel.spare_of(W, m, "tools"), "score", rel.score(W, m, v), "porters_out", m.porters_out) end
    end
  end
end
if os.getenv("TC_DEBUG_TRADE") then
  local kinds = {}
  for _, id in ipairs(W.ent.party_list or {}) do
    local pa = W.ent.parties[id]
    if pa then kinds[#kinds + 1] = pa.kind .. "(" .. pa.from .. ">" .. pa.to .. " arr " .. pa.arrive - W.hour .. "h)" end
  end
  print("parties:", table.concat(kinds, " "))
  for _, v in ipairs(W.ent.villages) do if v.porters_out then print("porters_out:", v.name, v.id) end end
end
