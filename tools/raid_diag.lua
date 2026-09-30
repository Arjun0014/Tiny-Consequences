-- Why villages do (not) raid: the relation score's parts and the raid reckoning per pair.
-- Usage: luajit tools/raid_diag.lua <seed | world.tcsave> <years> [every=25] [--save out.tcsave]
-- Prints, at each checkpoint, every village big enough to raid and its candidate targets with
-- the score parts (daughter tie, kin, trades, ledger, grievance, lord), the raid's reckoning
-- (distance or sea cost, attackers, defence with allies, pwin, gain, value) and the gate that
-- stops it. Read-only: it reproduces conflict.monthly's arithmetic without drawing dice.
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local worlds = require("sim.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local conflict = require("sim.society.conflict")
local rel = require("sim.society.relations")
local sites = require("sim.society.sites")
local sea = require("sim.society.sea")

local W
if arg[1] and arg[1]:match("%.tcsave$") then
  local f = io.open(arg[1], "rb"); local str = f:read("*a"); f:close()
  W = worlds.from_save_string(str)
else W = worlds.generate(tonumber(arg[1]) or 22) end
local years = tonumber(arg[2]) or 100
local every = tonumber(arg[3]) or 25
local save_out, quiet
for i = 1, #arg do if arg[i] == "--save" then save_out = arg[i + 1] end; if arg[i] == "--quiet" then quiet = true end end

local function parts(u, v)
  local out = { tie = 0, kin = 0, tr = 0, led = 0, gr = 0, lord = 0 }
  if u.from == v.id or v.from == u.id then
    local young = u.from == v.id and u or v
    local age = (W.hour - (young.founded or W.hour)) / 8064
    if not young.split then out.tie = math.max(0, 1 - age / cfgmod.SOCIETY.daughter_tie_years) end
  end
  local r = rel.get(W, u.id, v.id)
  if r then
    local small = math.min(u.pop or 30, v.pop or 30)
    out.kin = 0.25 * math.min(6, r.kin * 30 / math.max(30, small))
    out.tr = 0.15 * math.min(5, r.trades)
    local gave, got
    if u.id < v.id then gave, got = r.gab, r.gba else gave, got = r.gba, r.gab end
    local net = (gave - got) / math.max(1, u.need_day or 1)   -- (the ledger is in FU, D-074)
    local sc = require("sim.config").SOCIETY
    out.led = net > 0 and -0.004 * net or sc.owe_w * math.min(sc.owe_cap, -net)
    out.gr = -0.3 * math.min(6, (u.id < v.id) and (r.ga or 0) or (r.gb or 0))
    out.rawkin = r.kin
    out.rawtr = r.trades
  end
  if u.lord == v.id or v.lord == u.id then out.lord = 1 end
  return out
end

local function report(y)
  local cfg = cfgmod.CONFLICT
  local vs = W.ent.villages
  local H = W.ent.households
  local tot = 0
  for _, v in ipairs(vs) do if v.alive then tot = tot + v.pop end end
  print(string.format("=== year %d  (%d people)", y, tot))
  for _, v in ipairs(vs) do
    if v.alive and v.pop >= cfg.min_pop then
      local nf = conflict.fighters(W, v)
      local hunger = v.famine and 1.5 or (v.rationing and 1 or 0)
      local land = (v.intensify and v.food_days < 120) and 0.5 or 0
      local _, war = (conflict.status or { temper = function() return 1, 1 end }).temper(W, v)
      local amb = 0
      local pr = 0
      if v.leader_h and H[v.leader_h] then pr = H[v.leader_h].prestige or 0 end
      if v.pop >= cfg.amb_min_pop and pr > 0 then amb = cfg.ambition * math.min(1, pr / cfg.amb_prestige) end
      local seats = v.mooring and sea.n_free(W, v) * cfgmod.SOCIETY.boat_seats or 0
      print(string.format("%-18s id%-3d pop %4d cont %d cult %d nf %3d hunger %.1f land %.1f war %.2f prestige %.1f amb %.2f seats %d lord %s",
        v.name, v.id, v.pop, v.continent, v.culture or 0, nf, hunger, land, war, pr, amb, seats, tostring(v.lord)))
      local A0 = math.min(nf * cfg.raid_share, nf)
      local rows = {}
      for _, u in ipairs(vs) do
        if u ~= v and u.alive and u.pop >= cfg.min_target then
          local d, A, how = nil, A0, "land"
          if u.continent == v.continent then d = sites.dist(W, v.cell, u.cell)
          elseif seats >= cfg.min_raiders then A = math.min(A0, seats); d = sea.cost(W, v, u.cell, A); how = "sea"
          else how = "noboat" end
          local p = parts(v, u)
          local s = rel.score(W, v, u)
          local limit = cfg.friend_score + (hunger + land) * cfg.want_breaks_ties
          local grv = conflict.grievance(W, v.id, u.id)
          local gate = "ok"
          if not d then gate = how == "noboat" and "no boats" or "no route"
          elseif d > cfg.raid_reach then gate = "far"
          elseif u.lord == v.id or v.lord == u.id then gate = "lord"
          elseif conflict.truce(W, v.id, u.id) then gate = "truce"
          elseif s >= limit and grv <= 1 then gate = "friend" end
          local D = conflict.defence(W, u, v)
          local pwin = A * A / (A * A + D * D * cfg.home_advantage)
          local loot = math.min(u.stock.grain * cfg.loot_share, A * cfg.carry)
          local gain = (hunger + land) * loot / math.max(1, v.need_day * 30) + grv * cfg.revenge_weight
          local ag = 0
          if amb > 0 and A > D * cfg.amb_edge and u.lord ~= v.id then ag = amb * math.min(2, A / math.max(1, D) - 1) end
          gain = gain + ag
          local value = (pwin * gain - (1 - pwin) * cfg.risk) * war - (d or 99) * 0.02
          rows[#rows + 1] = { u = u, d = d, how = how, s = s, p = p, gate = gate, A = A, D = D, pwin = pwin, gain = gain, ag = ag, value = value, grv = grv }
        end
      end
      table.sort(rows, function(a, b) return (a.d or 999) < (b.d or 999) end)
      for i = 1, math.min(#rows, 8) do
        local r = rows[i]
        print(string.format("   -> %-16s %s d %5s score %5.2f [tie %.2f kin %.2f(%s) tr %.2f(%s) led %.2f gr %.2f lord %d] A %4.1f D %5.1f pwin %.2f gain %.2f (amb %.2f) value %6.2f  %s",
          r.u.name, r.how, r.d and string.format("%.1f", r.d) or "-", r.s, r.p.tie, r.p.kin, tostring(r.p.rawkin or 0), r.p.tr,
          tostring(r.p.rawtr or 0), r.p.led, r.p.gr, r.p.lord, r.A, r.D, r.pwin, r.gain, r.ag, r.value, r.gate))
      end
    end
  end
end

local c = {}
local last = W.E.next_id - 1
local y0 = W.year or 0
for y = 1, years do
  world.run_years(W, 1)
  for id = last + 1, W.E.next_id - 1 do local r = W.E.rec[id]; if r then c[r.kind] = (c[r.kind] or 0) + 1 end end
  last = W.E.next_id - 1
  if y % every == 0 or y == years then
    print(string.format("+%d: raids %d repelled %d cattle %d truce %d submission %d secession %d tribute %d famine %d rationing %d relief %d splits %d disputes %d/%d",
      y, c.raid or 0, c.raid_repelled or 0, c.cattle_raid or 0, c.truce or 0, c.submission or 0, c.secession or 0, c.tribute or 0,
      c.famine or 0, c.rationing or 0, c.relief or 0, c.village_split or 0, c.grazing_dispute or 0, c.fishing_dispute or 0))
    if not quiet then report(y) end
    local ks = {}
    for k, n in pairs(conflict.why) do ks[#ks + 1] = k .. " " .. n end
    table.sort(ks)
    print("gates: " .. table.concat(ks, ", "))
  end
end
-- the political history: raids, submissions, disputes and splits with their chains
local events = require("sim.causality.events")
local SHOW = { raid = true, raid_repelled = true, submission = true, secession = true, grazing_dispute = true,
  fishing_dispute = true, village_split = true }
local VN = function(id) local v = W.ent.villages[id or 0]; return v and v.name or "?" end
for _, id in ipairs(W.E.order) do
  local r = W.E.rec[id]
  if r and SHOW[r.kind] then
    local ch = events.chain(W, id)
    local ks = {}
    for i = 1, #ch do ks[#ks + 1] = ch[i].kind end
    local d = r.data or {}
    print(string.format("y%-4d %-16s %s <- %s  [%s]", math.floor(r.tick / 48), r.kind, VN(d.village),
      VN(d.from or d.other or d.lord), table.concat(ks, ">")))
  end
end
if save_out then
  local version = require("sim.version")
  local str = worlds.year0_save_string(W, { seed = W.seed, sim_version = version.SIM_VERSION, made_by = "tools/raid_diag.lua" })
  local f = io.open(save_out, "wb"); f:write(str); f:close()
end
