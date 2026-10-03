-- A long run of a v3 world as its people lived it (S19 long-run audit): every memory anyone makes
-- (people.remember is the one place), with the day, the hour, the village, who and how old; each
-- village's state every few days (people, households, houses and huts and camps, boats, dock, salt
-- pans, stores, herds, trips and exchanges); and at the end what happened in the whole run (deaths by
-- cause, births, marriages within and across villages, moves, foundings, exchanges by goods and way,
-- kin across villages, boats and who owns them). Optionally a premise first (sim/agents/premise.lua)
-- and the world saved at the end (to follow people afterwards with tools/v3_follow.lua --load).
--   luajit tools/v3_chronicle.lua [--load assets/worlds/v3.tcsave] [--days 96] [--every 8] [--out out/chron]
--          [--premise crowd:2|fail_harvest:3|empty_stores:3|hard_winter|...] [--save FILE] [--quiet]
-- Files: <out>/mem.txt (every memory), <out>/days.txt (the states), <out>/summary.txt (printed too).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local clock = require("sim.agents.clock")
local people = require("sim.agents.people")

local args = { load = "assets/worlds/v3.tcsave", days = 96, every = 8, out = "out/chron", premise = {} }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i])
  elseif a == "--out" then i = i + 1; args.out = arg[i]
  elseif a == "--premise" then i = i + 1; args.premise[#args.premise + 1] = arg[i]
  elseif a == "--save" then i = i + 1; args.save = arg[i]
  elseif a == "--quiet" then args.quiet = true
  end
  i = i + 1
end
os.execute((package.config:sub(1, 1) == "\\" and "mkdir \"" .. args.out:gsub("/", "\\") .. "\" 2>nul") or ("mkdir -p " .. args.out))
io.stdout:setvbuf("no")

local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local V = W.S.v3
local day0 = clock.day(W.hour)
local t0 = W.hour

-- premises: name:settlement id (a world created on purpose; what follows is the people's own)
local premise = require("sim.agents.premise")
for _, pr in ipairs(args.premise) do
  local name, sid = pr:match("^([%w_]+):?(%d*)$")
  local s = V.settlements[tonumber(sid) or 1]
  if name == "crowd" then premise.crowd(W, s, 12, 40)
  elseif name == "fail_harvest" then premise.fail_harvest(W, s)
  elseif name == "empty_stores" then premise.empty_stores(W, s)
  elseif premise[name] then premise[name](W, s)
  elseif premise.named(W, name) then s = { name = "the world (named premise)" }   -- (S20: cold_snap, hard_winter, quake, eruption, ...)
  else error("no premise " .. tostring(name)) end
  print(string.format("premise %s at %s", name, s.name))
end

local mem = assert(io.open(args.out .. "/mem.txt", "w"))
local days_f = assert(io.open(args.out .. "/days.txt", "w"))
local sum = {}
local function say(fmt, ...)
  local l = select("#", ...) > 0 and string.format(fmt, ...) or fmt
  sum[#sum + 1] = l
  if not args.quiet then print(l) end
end

local counts = {}
local function bump(k, n) counts[k] = (counts[k] or 0) + (n or 1) end
-- categories of memories (the summary counts them; mem.txt has every one)
local CATS = {
  { "^Traded ", "trade" }, { "^Got .* from .* of .* for", "trade_got" }, { "lost at sea", "lost_at_sea" },
  { "^Married .* of .*, who came courting", "married_away" }, { "^Married ", "married" },
  { "^Went courting at .*: nobody", "courting_failed" }, { "I will go courting", "courting" },
  { "^Came to .* to live with", "bride_moved" }, { "^Came to .* to live", "moved_in" },
  { "^We decided to leave .* for ", "decided_move" }, { "make a new home", "decided_found" },
  { "^Came to the place we chose", "founded" }, { "^Landed on the shore", "landed_found" },
  { "^Landed at ", "landed_move" }, { "^We gave up crossing", "gave_up_crossing" },
  { "^We could not sail", "sailing_put_off" }, { "^Made our camp", "camp_made" },
  { "^Moved into our hut", "hut" }, { "^Moved into our own house", "own_house" },
  { "^We will build a proper house", "hut_to_house" }, { "^Moved into our new house", "new_house" },
  { "^Moved with .* into the new house", "new_house" }, { "^Moved into the empty house", "empty_house" },
  { "^Moved into a house standing empty", "empty_house" },
  { "^Launched our new boat", "boat_launched" }, { "^Laid down a new boat", "boat_laid" },
  { "^Visited ", "kin_visit" }, { "came from .* to see us", "kin_visited" },
  { "gave us .* of food to carry home", "kin_food" }, { "had nothing to spare", "kin_nothing" },
  { "said we could come and live", "kin_offer" }, { "^Took .* to .* at ", "visit_gift" },
  { "^Talked with the people of", "news_visit" }, { "gave us .* in our need", "relief" },
  { "^Asked .* for food: they had none", "relief_none" }, { "^Nobody at .* wanted our", "trade_unsold" },
  { "^Gave up the trading trip", "trade_given_up" }, { "^Gave birth", "birth" },
  { "^Lost ", "bereaved" }, { "^Taken in by", "orphan_taken" }, { "^Took in the .* household", "took_in_household" },
  { "^Took in ", "took_in_children" }, { "Barefoot", "barefoot" }, { "Shivering", "rags" },
  { "week after week", "diet" }, { "flux", "flux" }, { "Wolves killed", "wolf_kill" }, { "killed our dog", "dog_killed" },
  { "Drove the wolves", "wolves_driven" }, { "flood swept", "bridge_swept" }, { "Finished the bridge", "bridge_built" },
  { "squall", "squall" }, { "Left our .* with the", "beasts_left" }, { "field of those who left", "field_inherited" },
  { "Leaving my father's house", "couple_leaves" }, { "^Sewed new clothes", "clothes" }, { "new shoes", "shoes" },
  { "Speared a", "hunt_kill" }, { "the quarry", "hunt_failed" },
}
local function cat_of(text)
  for _, c in ipairs(CATS) do if text:find(c[1]) then return c[2] end end
  return "other"
end
local per_cat_example = {}
local orig_rem = people.remember
people.remember = function(p, t, text, ...)
  if type(text) == "string" then
    local s = V.settlements[p.sett]
    local at = p.at and p.at ~= p.sett and V.settlements[p.at]
    local d = clock.day(t) - day0
    local lh = s and clock.local_hour(t, W.grid.lon[s.cell]) or 0
    mem:write(string.format("d%03d %05.2f %-16s %-14s #%-4d %3.0f %-10s %s%s\n", d, lh, s and s.name or "?", p.name .. " " .. (p.surname or ""), p.id,
      (t - p.born) / (24 * 48), people.stage(p, t), at and ("[at " .. at.name .. "] ") or "", text))
    local c = cat_of(text)
    bump("mem." .. c)
    if c ~= "other" and not per_cat_example[c] then per_cat_example[c] = string.format("d%d %s #%d: %s", d, p.name, p.id, text) end
  end
  return orig_rem(p, t, text, ...)
end

local function state(s)
  local t = { houses = 0, hsite = 0, huts = 0, camps = 0, salt = 0, food = 0, logs = 0, docks = 0, pans = 0, empty = 0, cloth = 0, leather = 0 }
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" then
      if b.state == "standing" then
        if b.hut then t.huts = t.huts + 1 else t.houses = t.houses + 1 end
        if (b.hh or 0) == 0 then t.empty = t.empty + 1 end
        t.salt = t.salt + (b.stock.salt or 0)
        t.food = t.food + (b.stock.food or 0) + (b.stock.smoked or 0) + (b.stock.fish or 0) + (b.stock.cheese or 0) + (b.stock.meat or 0) + (b.stock.meals or 0)
        t.logs = t.logs + (b.stock.logs or 0)
        t.cloth = t.cloth + (b.stock.cloth or 0)
        t.leather = t.leather + (b.stock.leather or 0)
      else
        t.hsite = t.hsite + 1
      end
    elseif b.kind == "camp" and b.state == "standing" then
      t.camps = t.camps + 1
      t.food = t.food + (b.stock.food or 0) + (b.stock.smoked or 0) + (b.stock.fish or 0) + (b.stock.meat or 0)
    elseif b.kind == "dock" and b.state == "standing" then t.docks = t.docks + 1
    elseif b.kind == "saltpan" and b.state == "standing" then t.pans = t.pans + 1
    end
  end
  local g = s.granary and s.granary.stock or {}
  t.grain = (g.grain or 0) + (g.sheaves or 0)
  t.relish = (g.smoked or 0) + (g.cheese or 0)
  t.ws = s.wood_store and (s.wood_store.stock.logs or 0) or 0
  local nb, own, away, lost, moved = 0, 0, 0, 0, 0
  for _, b in ipairs(s.boats or {}) do
    if b.moved then moved = moved + 1
    elseif b.lost then lost = lost + 1
    else
      nb = nb + 1
      if (b.owner or 0) ~= 0 then own = own + 1 end
      if b.away then away = away + 1 end
    end
  end
  t.boats = string.format("%d(%down,%daway,%dlost,%dmoved)", nb, own, away, lost, moved)
  local herd = {}
  for _, hk in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[hk]
    if H then for _, a in ipairs(H.animals) do if a.alive then herd[a.sp] = (herd[a.sp] or 0) + 1 end end end
  end
  local hs = {}
  for _, k in ipairs({ "sheep", "goat", "cattle", "pig" }) do if herd[k] then hs[#hs + 1] = k:sub(1, 2) .. herd[k] end end
  t.herd = table.concat(hs, " ")
  local n, nhh = 0, 0
  for _, id in ipairs(s.members) do local p = V.people[id]; if p and p.alive then n = n + 1 end end
  for _, hid in ipairs(s.households) do local hh = V.households[hid]; if hh and #hh.members > 0 then nhh = nhh + 1 end end
  t.n, t.nhh = n, nhh
  return t
end

local function report(d)
  days_f:write(string.format("day %d\n", d))
  for _, s in ipairs(V.settlements) do
    if s.alive then
      local t = state(s)
      days_f:write(string.format("  %-16s pop %3d hh %2d | house %2d site %d hut %d camp %d empty %d | dock %d pans %d boats %s | grain %5.0f relish %4.0f larders %5.0f logs %4.0f | salt %5.1f cloth %4.1f lthr %4.1f | %s | trips %d\n",
        s.name, t.n, t.nhh, t.houses, t.hsite, t.huts, t.camps, t.empty, t.docks, t.pans, t.boats, t.grain, t.relish, t.food, t.ws + t.logs,
        t.salt, t.cloth, t.leather, t.herd, s.acc.trips or 0))
    end
  end
  days_f:flush()
end

say("world %s: %d settlements, day %d, %d people", args.load, #V.settlements, day0, #V.order)
report(0)
local clk = os.clock()
for d = 1, args.days do
  local base = (day0 + d - 1) * 24
  for h = 1, 24 do world.advance(W, base + h) end
  if d % args.every == 0 or d == args.days then report(d) end
  if not args.quiet and d % 8 == 0 then io.write(string.format("  .. day %d (%.0f s)\n", d, os.clock() - clk)) end
end
mem:close()

say("== %d days in %.0f s", args.days, os.clock() - clk)
-- deaths, births
local deaths, births = {}, 0
for id, p in pairs(V.people) do
  if not p.alive and p.died and p.died >= t0 then
    local why = p.died_of or "?"
    deaths[why] = (deaths[why] or 0) + 1
  end
  if p.born and p.born >= t0 then births = births + 1 end
end
local dk = {}
for k, v in pairs(deaths) do dk[#dk + 1] = k .. " " .. v end
table.sort(dk)
say("births %d; deaths: %s", births, table.concat(dk, ", "))
local dl = {}
for _, id in ipairs(V.order) do
  local p = V.people[id]
  if p and not p.alive and p.died and p.died >= t0 then dl[#dl + 1] = p end
end
for id, p in pairs(V.people) do
  local seen = false
  for _, q in ipairs(dl) do if q == p then seen = true end end
  if not seen and not p.alive and p.died and p.died >= t0 then dl[#dl + 1] = p end
end
table.sort(dl, function(a, b) if a.died ~= b.died then return a.died < b.died end return a.id < b.id end)
for _, p in ipairs(dl) do
  local s = V.settlements[p.sett]
  say("  died d%-3d %-16s %-16s #%-4d age %4.1f %s", clock.day(p.died) - day0, s and s.name or "?", p.name .. " " .. (p.surname or ""), p.id,
    (p.died - p.born) / (24 * 48), p.died_of or "?")
end
local ck = {}
for k, v in pairs(counts) do ck[#ck + 1] = { k, v } end
table.sort(ck, function(a, b) return a[1] < b[1] end)
say("memories by kind:")
for _, e in ipairs(ck) do
  local c = e[1]:gsub("^mem%.", "")
  say("  %-20s %5d  %s", c, e[2], per_cat_example[c] or "")
end
-- the villages now
say("villages at the end:")
for _, s in ipairs(V.settlements) do
  local t = state(s)
  say("  %d %-16s %s pop %3d hh %2d house %2d hut %d camp %d | dock %d pans %d boats %s | %s%s", s.id, s.name, s.alive and "" or "(gone)", t.n, t.nhh, t.houses,
    t.huts, t.camps, t.docks, t.pans, t.boats, t.herd, s.founded and s.founded > t0 and string.format(" | founded day %d", clock.day(s.founded) - day0) or "")
end
-- kin across villages: living people with a living parent, child or sibling in another village
local across = {}
for _, id in ipairs(V.order) do
  local p = V.people[id]
  if p and p.alive then
    local function check(qid)
      local q = qid and qid ~= 0 and V.people[qid]
      if q and q.alive and q.sett ~= p.sett then
        local a, b = math.min(p.sett, q.sett), math.max(p.sett, q.sett)
        local k = V.settlements[a].name .. " - " .. V.settlements[b].name
        across[k] = (across[k] or 0) + 1
      end
    end
    check(p.mother); check(p.father)
    for _, k in ipairs(p.kids or {}) do check(k) end
  end
end
local ak = {}
for k, v in pairs(across) do ak[#ak + 1] = k .. " " .. v end
table.sort(ak)
say("parent-child ties across villages: %s", #ak > 0 and table.concat(ak, "; ") or "none")
say("accounts (whole run):")
for _, s in ipairs(V.settlements) do
  local ks = {}
  for _, k in ipairs({ "trips", "export", "import", "salt_made", "salted", "salt_used", "news_visits", "visit_gifts", "relief_out", "relief_in", "founders", "taken_in", "guests", "wades", "culled_hunger", "meat_given", "shore", "quarried", "querns", "ore_dug", "blooms", "iron_made", "credit", "debts_paid", "debts_lost", "fairs", "fair_deals", "fair_matches", "estates", "leaving", "arrived", "ferries" }) do
    if s.acc[k] and s.acc[k] ~= 0 then ks[#ks + 1] = string.format("%s %.0f", k, s.acc[k]) end
  end
  say("  %-16s %s", s.name, table.concat(ks, ", "))
end
local fo = assert(io.open(args.out .. "/summary.txt", "w"))
fo:write(table.concat(sum, "\n"), "\n")
fo:close()
days_f:close()
if args.save then
  -- (a v3 world saves at a year boundary: day multiples of 48)
  if W.tick % 48 == 0 then
    local sf = assert(io.open(args.save, "wb"))
    sf:write(require("sim.worlds").year0_save_string(W, { seed = W.seed, made_by = "tools/v3_chronicle.lua" }))
    sf:close()
    print("saved " .. args.save)
  else
    print("not saved: the world is not at a year boundary (tick " .. W.tick .. ")")
  end
end
