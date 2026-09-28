-- Inspector content (Stage 1.9, 04_PLAYER.md §5): facts only ("Why?" arrives in Chapter 2).
-- build(W, sel) returns a plain description the GUI lays out and the bridge can return:
--   { kind, title, subtitle, icon, tint = {r,g,b}, items = {
--       { t = "head", text } | { t = "row", icon, label, value, hl = bool, link = sel }
--     | { t = "bar", icon, label, frac, value, hl } | { t = "text", text, icon } } }
-- Unusual values are flagged with hl = true AND an explanatory word in the value text (the GUI
-- adds a "!" badge), so nothing relies on colour alone.
local ents = require("game.view.ents")
local biome = require("sim.systems.biome")
local kinds = require("sim.causality.event_kinds")
local calendar = require("sim.calendar")

local M = {}

local floor, max, min, abs = math.floor, math.max, math.min, math.abs

M.PRETTY_BIOME = {
  ocean = "Ocean", sea_ice = "Sea ice", ice_sheet = "Ice sheet", village = "Village", farmland = "Farmland",
  ruins = "Ruins", ash = "Ash", marsh = "Marsh", desert = "Desert", tundra = "Tundra", jungle = "Jungle",
  forest = "Forest", grassland = "Grassland", barren = "Barren",
}
M.BIOME_ICON = {
  ocean = "wave", sea_ice = "snow", ice_sheet = "snow", village = "house", farmland = "wheat", ruins = "house",
  ash = "fire", marsh = "drop", desert = "sun", tundra = "snow", jungle = "tree", forest = "tree",
  grassland = "leaf", barren = "volcano",
}
M.JOB_ICON = { farmer = "wheat", fisher = "fish", hunter = "deer", woodcutter = "tree", builder = "house",
  trader = "boat", elder = "star", child = "heart", healer = "heart" }

-- Display scales for densities (presentation only): individuals per cell at density 1.
M.SCALE = { grazers = 120, predators = 20, fish = 600 }

local function pct(x) return string.format("%d%%", floor((x or 0) * 100 + 0.5)) end
local function num(x, d) return string.format("%." .. (d or 1) .. "f", x or 0) end
local function cap(s) s = tostring(s or ""); return (s:gsub("^%l", string.upper):gsub("_", " ")) end

function M.region_name(W, c)
  local reg = W.static and W.static.region and W.static.region[c]
  local recs = W.names and W.names.regions
  local r = reg and recs and recs[reg]
  return r and r.name or nil
end

function M.biome_name(W, c)
  local key = biome.NAMES[W.F.biome[c]] or "barren"
  return M.PRETTY_BIOME[key] or cap(key), key
end

local function rain_word(p)
  if p < 0.01 then return "dry" elseif p < 0.06 then return "light" elseif p < 0.2 then return "steady" end
  return "heavy"
end

-- Recent events touching any of `cells` (set) within `years`: newest first, at most n.
function M.recent_events(W, cells, years, n)
  local E = W.E
  local out = {}
  if not E or not E.order then return out end
  local since = W.tick - years * 48
  for i = #E.order, 1, -1 do
    local r = E.rec[E.order[i]]
    if r then
      if r.tick < since then break end
      if cells[r.cell] then
        local k = kinds[r.kind]
        out[#out + 1] = { year = floor(r.tick / 48), text = (k and k.text) or cap(r.kind), actor = r.actor, id = r.id }
        if #out >= n then break end
      end
    end
  end
  return out
end

-- Text rows are one line in the panel: long texts wrap onto following rows at word breaks
-- (M.WRAP characters of the panel's body font; continuation rows carry no icon).
M.WRAP = 40
local function add(items, t)
  if t.t == "text" and t.text and #t.text > M.WRAP then
    local line, first = "", true
    for word in t.text:gmatch("%S+") do
      if line ~= "" and #line + 1 + #word > (first and M.WRAP or M.WRAP + 3) then
        items[#items + 1] = { t = "text", icon = first and t.icon or nil, text = line, hl = t.hl }
        line, first = word, false
      else
        line = line == "" and word or (line .. " " .. word)
      end
    end
    if line ~= "" then items[#items + 1] = { t = "text", icon = first and t.icon or nil, text = line, hl = t.hl } end
    return
  end
  items[#items + 1] = t
end

local function cell_items(W, c, items)
  local F = W.F
  local ocean = W.geo.ocean[c] == 1
  add(items, { t = "head", text = "Weather & water" })
  local temp = F.temp[c]
  local anom = F.heat_anom[c]
  local hl = abs(anom) > 3
  add(items, { t = "row", icon = "thermo", label = "Temperature",
    value = num(temp) .. " °C" .. (hl and (anom > 0 and " (heat spell)" or " (cold snap)") or ""), hl = hl })
  local p = F.precip[c]
  add(items, { t = "row", icon = "drop", label = "Rain", value = rain_word(p) .. " (" .. num(p, 2) .. ")" })
  if not ocean then
    local sw = F.soil_water[c]
    add(items, { t = "bar", icon = "drop", label = "Soil water", frac = sw, value = pct(sw) .. (sw < 0.1 and " (parched)" or ""), hl = sw < 0.1 })
    add(items, { t = "bar", icon = "leaf", label = "Fertility", frac = F.fertility[c], value = pct(F.fertility[c]) })
    add(items, { t = "head", text = "Life" })
    add(items, { t = "bar", icon = "leaf", label = "Grass", frac = F.grass[c], value = pct(F.grass[c]) })
    add(items, { t = "bar", icon = "tree", label = "Forest", frac = F.forest[c], value = pct(F.forest[c]) })
    add(items, { t = "row", icon = "deer", label = "Grazers", value = tostring(floor(F.grazers[c] * M.SCALE.grazers + 0.5)) .. " deer" })
    add(items, { t = "row", icon = "wolf", label = "Predators", value = tostring(floor(F.predators[c] * M.SCALE.predators + 0.5)) .. " wolves" })
    if F.crop[c] > 0 or F.land_use[c] > 0 then
      add(items, { t = "bar", icon = "wheat", label = "Crops", frac = F.crop[c], value = pct(F.crop[c]) })
    end
    if F.fire[c] > 0 then add(items, { t = "row", icon = "fire", label = "Fire", value = "burning (" .. pct(F.fire[c]) .. ")", hl = true }) end
  else
    add(items, { t = "head", text = "Sea" })
    add(items, { t = "row", icon = "fish", label = "Fish", value = tostring(floor(F.fish[c] * M.SCALE.fish + 0.5)) .. " shoals" .. (F.fish[c] < 0.05 and " (depleted)" or ""), hl = F.fish[c] < 0.05 })
    add(items, { t = "bar", icon = "leaf", label = "Plankton", frac = F.plankton[c], value = pct(F.plankton[c]) })
    add(items, { t = "row", icon = "thermo", label = "Sea temp", value = num(F.sst[c]) .. " °C" })
  end
  local oil = F.oil[c]
  if oil > 0.001 then add(items, { t = "row", icon = "drop", label = "Oil", value = pct(oil) .. " (slick)", hl = true }) end
  local ice = F.ice[c]
  local lat = W.grid.lat[c]
  local ice_hl = ice > 0.5 and abs(lat) < 0.7
  add(items, { t = "bar", icon = "snow", label = ocean and "Sea ice" or "Snow & ice", frac = ice, value = pct(ice) .. (ice_hl and " (unusual here)" or ""), hl = ice_hl })
end

-- What a task looks like in words (the sim's task codes, sim/society/labour.lua).
M.TASK_TEXT = {
  sow = "Sowing", tend = "Weeding", care = "Manuring the fields", harvest = "Harvesting", clear = "Clearing land",
  fuel = "Cutting firewood", timber = "Felling timber", brush = "Gathering brush", build = "Building",
  fish = "Fishing", forage = "Foraging", hunt = "Hunting", travel = "Travelling",
  craft_tools = "Knapping stone tools", craft_pots = "Making pots", craft_cloth = "Spinning and weaving",
  herd = "Minding the flocks",
}
M.TASK_GROUP = { sow = "farming", tend = "farming", care = "farming", harvest = "farming", clear = "farming",
  fuel = "wood", timber = "wood", brush = "wood", build = "building", fish = "fishing", forage = "foraging",
  hunt = "hunting" }
local PLACE_TEXT = { field = "a field", wood = "the woods", site = "a building site", boat = "the boat",
  square = "the well", home = "home", path = "the road" }

local function year_of(h)
  if type(h) ~= "number" then return nil end
  if h < 0 then return "long ago" end
  return "Year " .. floor(h / 8064)
end
M.year_of = year_of

-- The village's society: who leads, its herd, what it makes and trades, whom it pays or rules,
-- whom it hates and why (the event behind the grievance).
function M.society_items(W, v, items)
  local E = W.ent
  if not E or v.alive == false then return end
  add(items, { t = "head", text = "Society" })
  local P = E.people or {}
  local L = E.lineages or {}
  local ld = v.leader and P[v.leader]
  if ld and not ld.dead then
    local lin = ld.lin and L[ld.lin]
    local since = v.leader_since and year_of(v.leader_since)
    add(items, { t = "text", icon = "star", text = "Leader: " .. (ld.name or "?") .. (lin and (" of the " .. lin.name .. " line") or "")
      .. (since and (", since " .. since) or "") })
    local tr = ld.traits and table.concat(ld.traits, ", ")
    if tr and tr ~= "" then add(items, { t = "text", icon = "bubble", text = "The leader is " .. tr }) end
  end
  -- lineages by size
  local n, order = {}, {}
  for _, id in ipairs(v.members or {}) do
    local p = P[id]
    if p and p.lin then
      if not n[p.lin] then n[p.lin] = 0; order[#order + 1] = p.lin end
      n[p.lin] = n[p.lin] + 1
    end
  end
  table.sort(order, function(a, b) if n[a] ~= n[b] then return n[a] > n[b] end return a < b end)
  local parts = {}
  for i = 1, math.min(4, #order) do
    local lin = L[order[i]]
    parts[#parts + 1] = (lin and lin.name or "?") .. " " .. n[order[i]]
  end
  if #parts > 0 then add(items, { t = "text", icon = "people", text = "Lineages: " .. table.concat(parts, " · ") .. (#order > 4 and " …" or "") }) end
  local h = v.herd
  if h then
    add(items, { t = "row", icon = "deer", label = "Herd", value = string.format("%d sheep · %d goats · %d cattle",
      floor(h.sheep or 0), floor(h.goats or 0), floor(h.cattle or 0)) })
    -- how it is shared among the households (D-061): the richest fifth's share
    local H = E.households or {}
    local b = {}
    for _, id in ipairs(v.households or {}) do local hh = H[id]; if hh then b[#b + 1] = hh.beasts or 0 end end
    if #b >= 5 then
      table.sort(b, function(x, y) return x > y end)
      local tot, top = 0, 0
      for i = 1, #b do tot = tot + b[i]; if i <= math.ceil(#b / 5) then top = top + b[i] end end
      if tot > 1 then add(items, { t = "text", icon = "star", text = string.format("The richest fifth of the families own %d%% of the herd", floor(top / tot * 100 + 0.5)) }) end
    end
  end
  -- crafts: what the village makes well, what it buys
  local crafts_ok, crafts = pcall(require, "sim.society.crafts")
  if crafts_ok and v.crafts then
    local mk, buy = {}, {}
    local a = v.acc_last or {}
    for _, g in ipairs(crafts.GOODS) do
      if (a[g .. "_sold"] or 0) > 0 then mk[#mk + 1] = string.format("%s (sold %d)", g, floor(a[g .. "_sold"])) end
      if (a[g .. "_bought"] or 0) > 0 then buy[#buy + 1] = string.format("%s (%d)", g, floor(a[g .. "_bought"])) end
    end
    if #mk > 0 then add(items, { t = "text", icon = "pickaxe", text = "Makes for others: " .. table.concat(mk, ", ") }) end
    if #buy > 0 then add(items, { t = "text", icon = "boat", text = "Buys: " .. table.concat(buy, ", ") }) end
  end
  -- lord and tributaries
  local vs = E.villages or {}
  if v.lord and vs[v.lord] then
    local L2 = vs[v.lord]
    add(items, { t = "row", icon = "wheat", label = "Pays tribute to", value = (L2.name or "?") .. (v.lord_since and (", since " .. year_of(v.lord_since)) or ""),
      link = { kind = "village", id = L2.id, cell = L2.cell }, hl = true })
  end
  local tribs = {}
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u.lord == v.id then tribs[#tribs + 1] = u.name end
  end
  if #tribs > 0 then add(items, { t = "text", icon = "star", text = "Rules: " .. table.concat(tribs, ", ") }) end
  -- the polity it belongs to (W9): named for its paramount, sized by its tiers of tribute
  local conflict_ok, conflict = pcall(require, "sim.society.conflict")
  if conflict_ok and (v.lord or #tribs > 0) then
    local top = conflict.polity(W, v)
    local name, tiers = conflict.polity_name(W, top)
    if name then
      local n = 0
      for i = 1, #vs do if vs[i].alive and conflict.polity(W, vs[i]) == top then n = n + 1 end end
      add(items, { t = "text", icon = "star", text = string.format("Part of %s: %d villages, %d tiers", name, n, tiers) })
    end
  end
  -- the strongest grievance and the wrong behind it
  local worst, wg, wev = nil, 0, 0
  local R = E.rel or {}
  for i = 1, #vs do
    local u = vs[i]
    if u ~= v and u.alive then
      local a, b = math.min(v.id, u.id), math.max(v.id, u.id)
      local r = R[a .. "_" .. b]
      if r then
        local g = v.id < u.id and (r.ga or 0) or (r.gb or 0)
        local ev = v.id < u.id and (r.gea or 0) or (r.geb or 0)
        if g > wg then worst, wg, wev = u, g, ev end
      end
    end
  end
  if worst and wg >= 0.5 then
    local why = ""
    local rec = wev ~= 0 and W.E and W.E.rec and W.E.rec[wev]
    if rec then why = string.format(" (%s, year %d)", cap(rec.kind), floor((rec.tick or 0) / 48)) end
    add(items, { t = "row", icon = "skull", label = "Grievance", value = string.format("against %s%s", worst.name or "?", why),
      link = { kind = "village", id = worst.id, cell = worst.cell }, hl = wg >= 2 })
  end
end

local function village_items(W, v, items)
  local mem = type(v.members) == "table" and v.members or {}
  local P = W.ent.people or {}
  local B = W.ent.buildings or {}
  local alive = v.pop or #mem
  local homes, sites, ruins = 0, 0, 0
  for _, bid in ipairs(v.buildings or {}) do
    local b = B[bid]
    if b then
      if b.state == "standing" and (b.kind == "house" or b.kind == "hut") then homes = homes + 1
      elseif b.state == "site" then sites = sites + 1
      elseif b.state == "ruin" then ruins = ruins + 1 end
    end
  end
  local hh = type(v.households) == "table" and #v.households or 0
  add(items, { t = "row", icon = "people", label = "People", value = alive .. " in " .. hh .. " households" })
  add(items, { t = "row", icon = "house", label = "Homes", value = homes .. (sites > 0 and (" (+" .. sites .. " being built)") or "")
    .. (ruins > 0 and (", " .. ruins .. " ruins") or "") })
  if v.state then
    local st = tostring(v.state)
    local bad = st == "famine" or st == "struggling" or st == "abandoned" or st == "rationing"
    local extra = (v.ration and v.ration < 0.99) and string.format(" (eating %d%%)", floor(v.ration * 100 + 0.5)) or ""
    add(items, { t = "row", icon = bad and "skull" or "heart", label = "State", value = cap(st) .. extra, hl = bad })
  end
  local st = v.stock or {}
  if type(v.food_days) == "number" and v.alive ~= false then
    local fd = v.food_days
    add(items, { t = "row", icon = "bread", label = "Food", value = string.format("%d days", floor(min(fd, 9999))) .. (fd < 30 and " (short)" or ""), hl = fd < 30 })
    add(items, { t = "text", icon = "wheat", text = string.format("Grain %d · fish %d · seed %d", floor(st.grain or 0), floor(st.fresh or 0), floor(st.seed or 0)) })
    local wd = v.wood_days or 0
    add(items, { t = "row", icon = "tree", label = "Firewood", value = string.format("%d days (%d stacked)", floor(min(wd, 9999)), floor(st.wood or 0))
      .. (wd < 10 and " (cold)" or ""), hl = wd < 10 })
  end
  local farms = type(v.farms) == "table" and #v.farms or 0
  if farms > 0 then
    local sown, ripe = 0, 0
    for _, c in ipairs(v.farms) do
      local f = W.S.farm and W.S.farm[c]
      if f then sown = sown + (f.sown or 0) * (1 - (f.harvested or 0)); if f.ripe then ripe = ripe + 1 end end
    end
    local phase = v.season and v.season.phase or "?"
    add(items, { t = "row", icon = "wheat", label = "Fields", value = string.format("%d (%s%s)", farms, phase,
      sown > 0.05 and string.format(", %.1f sown", sown) or "") .. (v.intensify and ", tilled hard" or "") })
  end
  local nb = type(v.boats) == "table" and #v.boats or 0
  if nb > 0 or (type(v.fishing) == "table" and #v.fishing > 0) then
    local out = 0
    local Bo = W.ent.boats or {}
    for _, id in ipairs(v.boats or {}) do
      local b = Bo[id]
      if b and b.trip and b.trip.ret and W.hour < b.trip.ret and W.hour >= b.trip.depart - 1 then out = out + 1 end
    end
    add(items, { t = "row", icon = "boat", label = "Boats", value = nb .. (out > 0 and (" (" .. out .. " out today)") or " (moored)") })
  end
  -- what people are doing this week (the labour plan)
  local counts, idle, kids = {}, 0, 0
  for k = 1, #mem do
    local p = P[mem[k]]
    if p then
      local g = M.TASK_GROUP[p.task]
      if g then counts[g] = (counts[g] or 0) + 1
      elseif (ents.age(W, p) or 0) < 14 then kids = kids + 1
      else idle = idle + 1 end
    end
  end
  local parts = {}
  for _, g in ipairs({ "farming", "fishing", "wood", "building", "foraging", "hunting" }) do
    if counts[g] then parts[#parts + 1] = g .. " " .. counts[g] end
  end
  if idle > 0 then parts[#parts + 1] = "free " .. idle end
  if #parts > 0 then add(items, { t = "text", icon = "pickaxe", text = "This week: " .. table.concat(parts, " · ") }) end
  local a = v.acc_last
  if type(a) == "table" then
    add(items, { t = "head", text = "Last year" })
    add(items, { t = "text", icon = "wheat", text = string.format("Harvest %d · catch %d · foraged %d · hunted %d",
      floor(a.harvest or 0), floor(a.catch or 0), floor(a.forage or 0), floor(a.meat or 0)) })
    add(items, { t = "text", icon = "people", text = string.format("%d born · %d died · %d weddings · %d came · %d left",
      a.births or 0, a.deaths or 0, a.marriages or 0, a.mig_in or 0, a.mig_out or 0) })
    add(items, { t = "text", icon = "tree", text = string.format("Wood cut %d · burned %d", floor((a.wood_cut or 0) + (a.brush or 0)), floor(a.burned or 0)) })
    local ex = {}
    if (a.relief_in or 0) > 0 then ex[#ex + 1] = string.format("grain received %d", floor(a.relief_in)) end
    if (a.relief_out or 0) > 0 then ex[#ex + 1] = string.format("grain given %d", floor(a.relief_out)) end
    if (a.wood_bought or 0) > 0 then ex[#ex + 1] = string.format("wood bought %d", floor(a.wood_bought)) end
    if (a.wood_sold or 0) > 0 then ex[#ex + 1] = string.format("wood sold %d", floor(a.wood_sold)) end
    if #ex > 0 then add(items, { t = "text", icon = "boat", text = cap(table.concat(ex, " · ")) }) end
  end
  -- society (W8-W10): leader, lineages, herd, crafts and trade, lord and tributaries, feuds
  M.society_items(W, v, items)
  -- its chronicle: the last entries, oldest first
  local ch = M.chronicle(W, v)
  if #ch > 0 then
    add(items, { t = "head", text = "Chronicle" })
    local from = math.max(1, #ch - 5)
    if from > 1 then add(items, { t = "text", text = string.format("… %d earlier entries", from - 1) }) end
    for i = from, #ch do add(items, { t = "text", icon = "hourglass", text = "Year " .. ch[i].year .. ": " .. ch[i].text }) end
  end
  if type(v.founded) == "number" then
    local parent = v.from and W.ent and W.ent.villages and W.ent.villages[v.from]
    add(items, { t = "row", icon = "hourglass", label = "Founded", value = cap(year_of(v.founded))
      .. (parent and parent.name and (", from " .. parent.name) or ""),
      link = parent and { kind = "village", id = parent.id, cell = parent.cell } or nil })
  end
  if v.alive == false then
    add(items, { t = "row", icon = "skull", label = "Abandoned", value = cap(year_of(v.abandoned) or "yes"), hl = true })
  end
  -- recent events around the village and its farms
  local g = W.grid
  local cells = { [v.cell] = true }
  if type(v.farms) == "table" then for _, f in ipairs(v.farms) do if type(f) == "number" then cells[f] = true end end end
  local base = v.cell * 6 - 6
  for k = 1, g.nbr_count[v.cell] do cells[g.nbr[base + k]] = true end
  local ev = M.recent_events(W, cells, 20, 4)
  if #ev > 0 then
    add(items, { t = "head", text = "Recent events" })
    for _, e in ipairs(ev) do
      add(items, { t = "text", icon = e.actor == "player" and "star" or "info", text = "Year " .. e.year .. ": " .. e.text })
    end
  end
  add(items, { t = "head", text = "Residents" })
  local shown = 0
  for k = 1, #mem do
    local pid = mem[k]
    local p = P[pid]
    if p and not p.dead then
      shown = shown + 1
      if shown <= 8 then
        local age = ents.age(W, p)
        local doing = M.TASK_TEXT[p.task]
        add(items, { t = "row", icon = M.JOB_ICON[p.job] or "person", label = p.name or ("#" .. tostring(pid)),
          value = cap(p.job or "") .. (age and (", " .. age) or "") .. (doing and (" · " .. doing:lower()) or ""),
          link = { kind = "person", id = pid } })
      end
    end
  end
  if shown > 8 then add(items, { t = "text", text = "…and " .. (shown - 8) .. " more" }) end
  if shown == 0 then add(items, { t = "text", text = "Nobody lives here any more." }) end
end

-- Where a person is and what they are doing right now, in words (the sim's schedule).
local function doing_now(W, v, p)
  if p.dead then return nil end
  local schedule = require("sim.society.schedule")
  local w = schedule.where(W, v, p, W.hour, {})
  local act = w.act
  if act == "sleep" then return "Asleep at home" end
  if act == "travel" or act == "camp" then
    local party = p.away and p.away ~= 0 and W.ent.parties and W.ent.parties[p.away]
    if party and party.si then
      local i = schedule.party_pos(W, party, W.hour)
      if i >= party.si - 1 and i <= party.sj then return "At sea, in the boat" end
    end
  end
  if act == "travel" then return "On the road" end
  if act == "camp" then return "Camped for the night on the road" end
  if act == "fish" then return "Out fishing" end
  if act == "square" then return "At the well" end
  if act == "home" then return "At home" end
  local task = M.TASK_TEXT[p.task]
  if act == "walk" then return "Walking to " .. (PLACE_TEXT[w.place] or "work") end
  if act == "work" then return (task or "Working") .. (w.place and PLACE_TEXT[w.place] and (" at " .. PLACE_TEXT[w.place]) or "") end
  return cap(act)
end

-- Why person p is doing what they do: the village's own state behind the task (the labour
-- demands it answers, sim/society/labour.lua), or the errand of the party they travel with.
local PARTY_WHY = {
  porters = "carrying goods", returning = "coming home from an errand", bride = "going to marry",
  migrants = "leaving to live elsewhere", colonists = "setting out to found a village", orphans = "taken in by another village",
  raid = "on a raid", raid_home = "coming home from a raid",
}
local function days_text(d)
  d = floor((d or 0) + 0.5)
  if d > 400 then return "over a year" end
  if d >= 60 then return floor(d / 28 + 0.5) .. " months" end
  return d .. " days"
end
function M.reason(W, v, p)
  local E = W.ent
  local vs = E.villages or {}
  if p.away and p.away ~= 0 then
    local party = E.parties and E.parties[p.away]
    if not party then return nil end
    local to = party.to ~= 0 and vs[party.to]
    local where = to and to.name or "new land"
    local c = party.cargo or {}
    local sea = party.si and " by boat" or ""
    local k = party.kind
    if k == "porters" then
      if c.tribute then return string.format("Carrying %d grain to %s: tribute to its lord%s", floor(c.grain or 0), where, sea) end
      if c.gift then return string.format("Carrying %d grain to %s, who are going hungry%s", floor(c.grain or 0), where, sea) end
      if c.get then return string.format("Trading %s for %s with %s%s", c.give or "goods", c.get, where, sea) end
    elseif k == "returning" and c.get and (c.got or 0) > 0 then
      return string.format("Bringing home %d %s from trade%s", floor(c.got), c.get, sea)
    elseif k == "raid" then
      local r = W.E and W.E.rec and W.E.rec[party.reason or 0]
      local why = r and r.kind and (r.kind == "raid" and "to avenge a raid" or ("after " .. r.kind:gsub("_", " "))) or "for plunder"
      return string.format("Raiding %s, %s%s", where, why, sea)
    elseif k == "raid_home" then
      return string.format("Coming home from the raid with %d grain%s", floor(c.grain or 0), sea)
    elseif k == "bride" then
      return "Going to " .. where .. " to marry" .. sea
    elseif k == "migrants" or k == "colonists" then
      local r = W.E and W.E.rec and W.E.rec[party.reason or 0]
      local why = r and r.kind and r.kind ~= "migration" and (" (" .. r.kind:gsub("_", " ") .. ")") or ""
      return cap(PARTY_WHY[k]) .. ": to " .. where .. why .. sea
    end
    return cap(PARTY_WHY[k] or "travelling") .. sea
  end
  local t = p.task
  local fd = v.food_days or 0
  if t == "sow" or t == "tend" or t == "care" or t == "harvest" or t == "clear" then
    return string.format("%d fields feed %d people; grain in store for %s", #(v.farms or {}), v.pop or 0, days_text(fd))
  elseif t == "fuel" or t == "brush" then
    return "The woodpile holds " .. days_text(v.wood_days) .. " of fuel"
  elseif t == "timber" then
    return "Timber for the village's building sites"
  elseif t == "build" then
    local b = E.buildings and E.buildings[p.site]
    if b then
      local hh = b.household and b.household ~= 0 and E.households and E.households[b.household]
      local lead = hh and hh.members and hh.members[1] and E.people[hh.members[1]]
      return "Raising a " .. (b.kind or "building") .. (lead and (" for " .. (lead.name or "a household")) or "")
    end
  elseif t == "fish" then
    return fd < 60 and ("Food is short: " .. days_text(fd) .. " in store") or "Fresh fish for the village's tables"
  elseif t == "forage" or t == "hunt" then
    return "Food is short: " .. days_text(fd) .. " in store"
  elseif t == "craft_tools" or t == "craft_pots" or t == "craft_cloth" then
    local g = t:sub(7)
    local h = v.have and v.have[g]
    return h and h < 0.6 and ("The village's " .. g .. " are worn and too few") or ("Making " .. g .. " in the slack season")
  elseif t == "herd" then
    local h = v.herd
    if h then return string.format("%d sheep, %d goats and %d cattle need minding (wolves about)", floor(h.sheep or 0), floor(h.goats or 0), floor(h.cattle or 0)) end
  end
  return nil
end

-- What weighs on person p now (their needs unmet, D-062): hunger, cold, no home, no friends, a
-- bride-wealth the family cannot find, grief, fear of raiders.
function M.worries(W, v, p)
  local out = {}
  local E = W.ent
  local a = (W.hour - (p.born or W.hour)) / 8064
  if (p.nutrition or 1) < 0.6 then out[#out + 1] = "hunger" end
  local temp = v.plan and v.plan.temp or 10
  if temp < 5 and (v.stock and v.stock.wood or 1) <= 1 then out[#out + 1] = "the cold (no firewood)" end
  local h = E.households and E.households[p.household]
  if h and (h.home == 0 or not E.buildings[h.home] or E.buildings[h.home].state ~= "standing") then out[#out + 1] = "no home of their own" end
  if a >= 14 and not p.friends then out[#out + 1] = "loneliness" end
  if p.betrothed and p.betrothed ~= 0 and p.wait_since and W.hour - p.wait_since > 672 * 6 then
    local q = E.people[p.betrothed]
    local life = require("sim.society.life")
    local bride = p.sex == "f" and p or q
    local groom = p.sex == "f" and q or p
    if bride and groom and not select(4, life.bride_terms(W, v, bride, groom)) then
      out[#out + 1] = "the bride-wealth"
    else
      out[#out + 1] = "a house to marry into"
    end
  end
  if p.mem then
    local worst, ws = nil, 0.5
    for i = 1, #p.mem - 1, 2 do if (p.mem[i + 1] or 0) > ws then worst, ws = p.mem[i], p.mem[i + 1] end end
    local r = worst and W.E and W.E.rec and W.E.rec[worst]
    if r then out[#out + 1] = "grief (" .. tostring(r.kind):gsub("_", " ") .. ")" end
  end
  if v.raided and W.hour - v.raided < 3 * 8064 then out[#out + 1] = "raiders" end
  return out
end

-- The village's own history (D-062): founding, leaders, splits, raids given and suffered, feuds
-- and truces, submission, famines and losses — from the event record, gathered incrementally
-- (each inspector refresh reads only the events since the last).
local CHRON = { W = nil, last = 0, by = {} }
local CHRON_KINDS = { village_founded = true, village_split = true, raid = true, raid_repelled = true,
  cattle_raid = true, cattle_raid_repelled = true, truce = true, submission = true, secession = true, famine = true,
  shipment_lost = true, village_abandoned = true, bride = false }
local function chron_add(vid, rec)
  if not vid or vid == 0 then return end
  local l = CHRON.by[vid]
  if not l then l = {}; CHRON.by[vid] = l end
  l[#l + 1] = rec
end
function M.chronicle(W, v)
  local E = W.E
  if not E or not E.order then return {} end
  if CHRON.W ~= W then CHRON.W, CHRON.last, CHRON.by = W, 0, {} end
  local order = E.order
  for i = CHRON.last + 1, #order do
    local r = E.rec[order[i]]
    if r and CHRON_KINDS[r.kind] and type(r.data) == "table" then
      local d = r.data
      chron_add(d.village, r)
      if d.from and d.from ~= d.village then chron_add(d.from, r) end
      if d.lord then chron_add(d.lord, r) end
      if d.other then chron_add(d.other, r) end
    end
  end
  CHRON.last = #order
  local vs = W.ent and W.ent.villages or {}
  local P = W.ent and W.ent.people or {}
  local function nm(id) local u = vs[id]; return u and u.name or "?" end
  local out = {}
  for _, r in ipairs(CHRON.by[v.id] or {}) do
    local d, k = r.data, r.kind
    local t
    if k == "village_founded" and d.village == v.id then
      t = d.settlers and "Founded by settlers from over the sea" or ("Founded by people from " .. nm(d.from))
    elseif k == "village_founded" then t = "People from here founded " .. nm(d.village)
    elseif k == "new_leader" then
      local p = P[d.person]
      t = (p and p.name or "Someone") .. " became the leader"
    elseif k == "village_split" then t = "The " .. tostring(d.name or "?") .. " line left to found a village of their own"
    elseif k == "raid" or k == "cattle_raid" then
      local what = k == "raid" and "raided" or "had its herds driven off"
      if d.village == v.id then t = string.format("%s by %s", cap(what), nm(d.from))
      else t = string.format("Raided %s%s", nm(d.village), k == "cattle_raid" and " for cattle" or "") end
      if (d.dead_defenders or 0) + (d.dead_raiders or 0) > 0 then t = t .. string.format(" (%d dead)", (d.dead_defenders or 0) + (d.dead_raiders or 0)) end
    elseif k == "raid_repelled" or k == "cattle_raid_repelled" then
      if d.village == v.id then t = "Beat off raiders from " .. nm(d.from) else t = "Raiders beaten off at " .. nm(d.village) end
    elseif k == "truce" then t = "A truce with " .. nm(d.village == v.id and d.other or d.village) .. " after " .. tostring(d.raids or "many") .. " raids"
    elseif k == "submission" then
      if d.village == v.id then t = "Submitted to " .. nm(d.lord) .. " and pays tribute" else t = nm(d.village) .. " submitted and pays tribute" end
    elseif k == "secession" then
      if d.village == v.id then t = "Stopped paying tribute to " .. nm(d.lord) else t = nm(d.village) .. " stopped paying tribute" end
    elseif k == "famine" then t = "Famine"
    elseif k == "shipment_lost" then t = "A boat and its cargo lost at sea"
    elseif k == "village_abandoned" then t = "Abandoned"
    end
    if t then out[#out + 1] = { year = floor((r.tick or 0) / 48), text = t } end
  end
  return out
end

local function person_link(W, pid)
  local p = ents.person(W, pid)
  if not p then return nil end
  local alive = ents.is_alive(W, pid)
  return (p.name or ("#" .. tostring(pid))) .. (alive and "" or " (died)")
end

local function list_of(x)
  if type(x) == "table" then return x end
  if x == nil then return {} end
  return { x }
end

local function person_items(W, pid, p, items)
  local age = ents.age(W, p)
  local v = ents.village_of_person(W, pid)
  add(items, { t = "row", icon = "hourglass", label = "Age", value = age and (age .. " years") or "?" })
  add(items, { t = "row", icon = M.JOB_ICON[p.job] or "pickaxe", label = "Job", value = cap(p.job or "none") })
  if v then add(items, { t = "row", icon = "house", label = "Village", value = v.name or "?", link = { kind = "village", id = v.id, cell = v.cell } }) end
  if v and not p.dead then
    local now = doing_now(W, v, p)
    if now then add(items, { t = "row", icon = "pin", label = "Now", value = now }) end
    local task = M.TASK_TEXT[p.task]
    if task then add(items, { t = "row", icon = "pickaxe", label = "This week", value = task }) end
    local why = M.reason(W, v, p)
    if why then add(items, { t = "text", icon = "bubble", text = "Why: " .. why }) end
    local worries = M.worries(W, v, p)
    if #worries > 0 then add(items, { t = "text", icon = "skull", text = "Worries: " .. table.concat(worries, ", "), hl = true }) end
    local h = W.ent.households and W.ent.households[p.household]
    if h then
      local home = h.home ~= 0 and W.ent.buildings and W.ent.buildings[h.home]
      local where = (home and home.state == "standing") and (home.material == "earth" and "an earth house" or ("a " .. home.kind))
        or "no home of their own"
      add(items, { t = "row", icon = "house", label = "Home", value = cap(where) .. ", " .. #h.members .. " living there",
        hl = not (home and home.state == "standing") })
      -- the family's beasts (their wealth, D-061)
      if (h.beasts or 0) >= 0.5 then
        local vh = v.herd
        local tot = vh and ((vh.sheep or 0) + (vh.goats or 0) * 0.9 + (vh.cattle or 0) * 5) or 0
        local share = tot > 0 and h.beasts / tot or 0
        local word = share * #v.households > 2.5 and " (among the richest)" or (share * #v.households < 0.3 and " (poor)" or "")
        add(items, { t = "row", icon = "deer", label = "Family herd", value = string.format("%d sheep-equivalents%s", floor(h.beasts + 0.5), word) })
      end
    end
  end
  local function bar01(x) if type(x) ~= "number" then return nil end if x > 1 then return x / 100 end return x end
  local health = bar01(p.health)
  if health then add(items, { t = "bar", icon = "heart", label = "Health", frac = health, value = pct(health) .. (health < 0.3 and " (ill)" or ""), hl = health < 0.3 }) end
  if type(p.nutrition) == "number" then
    local n = p.nutrition
    add(items, { t = "bar", icon = "bread", label = "Fed", frac = n, value = pct(n) .. (n < 0.6 and " (hungry)" or ""), hl = n < 0.6 })
  end
  if (p.preg_due or 0) > W.hour then add(items, { t = "row", icon = "heart", label = "Expecting", value = "a child in " .. floor((p.preg_due - W.hour) / 672 + 0.5) .. " months" }) end
  if p.mood ~= nil then
    local mv = p.mood
    local word = type(mv) == "number" and ((mv > 0.35 and "cheerful") or (mv > 0.05 and "content") or (mv > -0.3 and "worried") or "miserable") or cap(mv)
    add(items, { t = "row", icon = "bubble", label = "Mood", value = word, hl = type(mv) == "number" and mv < -0.3 })
  end
  -- lineage and standing (W8)
  local L = W.ent.lineages
  local lin = p.lin and L and L[p.lin]
  local hh = W.ent.households and W.ent.households[p.household]
  if lin or hh then
    local vv = W.ent.villages and W.ent.villages[p.village]
    local lead = vv and vv.leader == p.id
    local st = hh and hh.prestige or 0
    local word = lead and "leads the village" or (st > 3 and "much respected" or (st > 1 and "respected" or "ordinary"))
    add(items, { t = "row", icon = "star", label = "Lineage", value = (lin and ("the " .. lin.name .. " line") or "?") .. " · " .. word })
  end
  -- friends (D-062): the companions of their days
  local fl = type(p.friends) == "table" and p.friends or {}
  if #fl > 0 then
    local names = {}
    for i = 1, #fl do
      local q = W.ent.people[fl[i]]
      if q then names[#names + 1] = (q.name or "?") .. ((q.dead and " (died)") or (q.village ~= p.village and " (away)") or "") end
    end
    if #names > 0 then add(items, { t = "text", icon = "people", text = "Friends: " .. table.concat(names, ", ") }) end
  end
  add(items, { t = "head", text = "Family" })
  local any = false
  local partner = ents.pid(p.partner)
  if partner then
    local s2 = person_link(W, partner)
    if s2 then any = true; add(items, { t = "row", icon = "heart", label = "Partner", value = s2, link = { kind = "person", id = partner } }) end
  end
  local parents = {}
  for _, par in ipairs(list_of(p.parents)) do parents[#parents + 1] = par end
  if ents.pid(p.mother) then parents[#parents + 1] = p.mother end
  if ents.pid(p.father) then parents[#parents + 1] = p.father end
  for _, par in ipairs(parents) do
    local s2 = person_link(W, par)
    if s2 then
      any = true
      local lp = ents.person(W, par)
      local rel = (lp and lp.sex == "f") and "Mother" or ((lp and lp.sex == "m") and "Father" or "Parent")
      add(items, { t = "row", icon = "person", label = rel, value = s2, link = { kind = "person", id = par } })
    end
  end
  if type(p.children) == "number" then
    if p.children > 0 then any = true; add(items, { t = "row", icon = "people", label = "Children", value = tostring(p.children) }) end
  else
    local kids = list_of(p.children)
    for i, ch in ipairs(kids) do
      if i > 5 then add(items, { t = "text", text = "..." .. (#kids - 5) .. " more children" }); break end
      local s2 = person_link(W, ch)
      if s2 then any = true; add(items, { t = "row", icon = "people", label = "Child", value = s2, link = { kind = "person", id = ch } }) end
    end
  end
  if not any then add(items, { t = "text", text = "No family yet." }) end
  local traits = list_of(p.traits)
  if #traits > 0 then
    add(items, { t = "head", text = "Traits" })
    local names = {}
    for _, tr in ipairs(traits) do names[#names + 1] = cap(type(tr) == "table" and (tr.name or tr.id) or tr) end
    add(items, { t = "text", icon = "star", text = table.concat(names, " · ") })
  end
  -- memories: p.mem = { event id, salience, ... } (sim/systems/people.lua) or p.memories list
  local mems = {}
  if type(p.mem) == "table" then
    for i = 1, #p.mem - 1, 2 do mems[#mems + 1] = { id = p.mem[i], sal = p.mem[i + 1] } end
    table.sort(mems, function(x, y) return (x.sal or 0) > (y.sal or 0) end)
  elseif type(p.memories) == "table" then
    for _, m in ipairs(p.memories) do mems[#mems + 1] = type(m) == "table" and m or { text = m } end
  end
  if #mems > 0 then
    add(items, { t = "head", text = "Memories" })
    for i = 1, math.min(4, #mems) do
      local m = mems[i]
      local r = m.id and W.E and W.E.rec and W.E.rec[m.id]
      if r then
        local k = kinds[r.kind]
        add(items, { t = "row", icon = r.actor == "player" and "star" or "bubble", label = "Year " .. floor(r.tick / 48), value = (k and k.text) or cap(r.kind) })
      elseif m.kind or m.event then
        local k = kinds[m.kind or m.event]
        add(items, { t = "row", icon = "bubble", label = m.year and ("Year " .. m.year) or "Once", value = (k and k.text) or cap(m.kind or m.event) })
      elseif m.text then
        add(items, { t = "text", icon = "bubble", text = cap(m.text) })
      else
        add(items, { t = "text", icon = "bubble", text = "A faded memory" })
      end
    end
  end
  if p.dead then
    add(items, { t = "row", icon = "skull", label = "Died", value = cap(year_of(p.dead)) .. (p.died_of and (", " .. cap(p.died_of)) or ""), hl = true })
  end
end

--- Totals shared with the HUD.
function M.totals(W)
  local F, g, n = W.F, W.grid, W.n
  local area, ocean = g.area, W.geo.ocean
  local gz, pr, fi = 0, 0, 0
  local forest, land = 0, 0
  for c = 1, n do
    local a = area[c]
    if ocean[c] == 0 then
      gz = gz + F.grazers[c] * a
      pr = pr + F.predators[c] * a
      forest = forest + F.forest[c] * a
      land = land + a
    else
      fi = fi + F.fish[c] * a
    end
  end
  local vcount = 0
  for _ in ents.each_village(W) do vcount = vcount + 1 end
  return {
    grazers = floor(gz * M.SCALE.grazers), predators = floor(pr * M.SCALE.predators), fish = floor(fi * M.SCALE.fish),
    forest = land > 0 and forest / land or 0, population = ents.population(W), villages = vcount,
  }
end

local function planet_items(W, items)
  local G = W.G
  add(items, { t = "row", icon = "globe", label = "Era", value = cap(G.era or "garden") })
  add(items, { t = "row", icon = "thermo", label = "Global temperature", value = string.format("%+.2f °C", G.g_temp or 0), hl = abs(G.g_temp or 0) > 1.5 })
  add(items, { t = "row", icon = "cloud", label = "CO₂", value = string.format("%d ppm", floor((G.co2 or 280) + 0.5)), hl = abs((G.co2 or 280) - 280) > 60 })
  add(items, { t = "row", icon = "wave", label = "Sea level", value = string.format("%+d m", floor((G.sea_level or 0) * 1000 + 0.5)) })
  add(items, { t = "bar", icon = "snow", label = "Ice cover", frac = min(1, (G.ice_fraction or 0) / 0.4), value = pct(G.ice_fraction or 0) })
  local T = M.totals(W)
  add(items, { t = "head", text = "Life" })
  add(items, { t = "row", icon = "people", label = "People", value = T.population .. " in " .. T.villages .. " villages" })
  add(items, { t = "row", icon = "tree", label = "Forest cover", value = pct(T.forest) .. " of land" })
  add(items, { t = "row", icon = "deer", label = "Grazers", value = tostring(T.grazers) })
  add(items, { t = "row", icon = "wolf", label = "Predators", value = tostring(T.predators) })
  add(items, { t = "row", icon = "fish", label = "Fish", value = tostring(T.fish) })
  -- blame ledger
  local ok, ledger = pcall(require, "sim.ledger")
  if ok and ledger.shares then
    local ok2, shares = pcall(ledger.shares, W, "co2")
    if ok2 and type(shares) == "table" and #shares > 0 then
      add(items, { t = "head", text = "Who changed the air?" })
      local you, merged = 0, {}
      for _, s in ipairs(shares) do
        local label = s.label
        if label == "root" then
          local r = W.E and W.E.rec and W.E.rec[s.id]
          if r and r.actor == "player" then you = you + s.share; label = nil
          else label = r and ((kinds[r.kind] and kinds[r.kind].text) or r.kind) or "Nature" end
        end
        if label then merged[#merged + 1] = { label = cap(label), share = s.share } end
      end
      if you > 0 then table.insert(merged, 1, { label = "You", share = you }) end
      for i = 1, min(5, #merged) do
        local s = merged[i]
        add(items, { t = "bar", icon = s.label == "You" and "star" or "cloud", label = s.label, frac = s.share, value = pct(s.share) })
      end
    end
  end
end

--- Builds the inspector description for selection `sel` (nil -> nil).
function M.build(W, sel)
  if not sel or not W then return nil end
  local out = { kind = sel.kind, items = {} }
  local items = out.items
  local g = W.grid
  if sel.kind == "cell" then
    local c = sel.cell
    local name, key = M.biome_name(W, c)
    out.title = name
    out.icon = M.BIOME_ICON[key] or "pin"
    local reg = M.region_name(W, c)
    out.subtitle = (reg and (reg .. " · ") or "") .. string.format("%d°%s %d°%s",
      abs(floor(math.deg(g.lat[c]) + 0.5)), g.lat[c] >= 0 and "N" or "S",
      abs(floor(math.deg(g.lon[c]) + 0.5)), g.lon[c] >= 0 and "E" or "W")
    cell_items(W, c, items)
  elseif sel.kind == "village" then
    local v = ents.village(W, sel.id)
    if not v then return nil end
    out.title = v.name or "Village"
    out.icon = "house"
    local reg = M.region_name(W, v.cell)
    out.subtitle = (v.alive == false and "Abandoned" or "Village") .. (reg and (" · " .. reg) or "")
    village_items(W, v, items)
  elseif sel.kind == "person" then
    local p = ents.person(W, sel.id)
    if not p then return nil end
    out.title = (p.name or "Someone") .. (p.surname and (" " .. p.surname) or "")
    out.icon = "person"
    local pupp = require("game.view.puppets")
    out.tint = pupp.JOB_COLORS[p.job] or pupp.JOB_COLORS.default
    local sex = p.sex == "f" and "Woman" or (p.sex == "m" and "Man" or "Villager")
    local age = ents.age(W, p)
    if age and age < 14 then sex = p.sex == "f" and "Girl" or (p.sex == "m" and "Boy" or "Child") end
    out.subtitle = sex .. (ents.is_alive(W, sel.id) and "" or " · died")
    person_items(W, sel.id, p, items)
  elseif sel.kind == "planet" then
    out.title = "The Planet"
    out.icon = "globe"
    out.subtitle = calendar.label(W.tick)
    planet_items(W, items)
  elseif sel.kind == "ship" then
    local s = ents.ship_list(W, sel.list)[sel.index]
    if type(s) ~= "table" then return nil end
    local kind = ents.ship_kind(s)
    out.title = s.name or (kind == "tanker" and "Tanker" or "Fishing boat")
    out.icon = kind == "tanker" and "ship" or "boat"
    out.subtitle = kind == "tanker" and "The Tanker Company" or "Fishing boat"
    if kind == "tanker" then
      add(items, { t = "row", icon = "ship", label = "Status", value = s.alive == false and "sunk" or "sailing its lane", hl = s.alive == false })
      local lane = W.static and W.static.lanes and W.static.lanes[s.lane]
      local lv = lane and ents.village(W, lane.village)
      if lv then add(items, { t = "row", icon = "house", label = "Passes by", value = lv.name or "a village", link = { kind = "village", id = lv.id, cell = lv.cell } }) end
    else
      local hv = ents.village(W, s.village)
      if hv then add(items, { t = "row", icon = "house", label = "Home port", value = hv.name or "a village", link = { kind = "village", id = hv.id, cell = hv.cell } }) end
      local tr = s.trip
      local status = "Moored"
      if s.laid_up then status = "Laid up for repairs"
      elseif tr and tr.ret and W.hour < tr.ret and W.hour >= tr.depart then
        status = W.hour < tr.at and "Sailing out to the fishing ground" or (W.hour < tr.leave and "Fishing" or "Coming home")
      end
      add(items, { t = "row", icon = "boat", label = "Now", value = status })
      add(items, { t = "row", icon = "people", label = "Crew", value = tostring(type(s.crew) == "table" and #s.crew or 0) })
      add(items, { t = "bar", icon = "pickaxe", label = "Condition", frac = s.condition or 1, value = pct(s.condition or 1) })
    end
    if s.cell then add(items, { t = "row", icon = "wave", label = "Waters", value = M.region_name(W, s.cell) or "open sea" }) end
  elseif sel.kind == "animal" then
    local c = sel.cell
    out.title = sel.species == "wolf" and "Wolves" or "Deer"
    out.icon = sel.species == "wolf" and "wolf" or "deer"
    out.subtitle = "A herd near " .. (M.region_name(W, c) or "here")
    if c and c > 0 then
      local F = W.F
      add(items, { t = "row", icon = "deer", label = "Grazers here", value = tostring(floor(F.grazers[c] * M.SCALE.grazers + 0.5)) })
      add(items, { t = "row", icon = "wolf", label = "Predators here", value = tostring(floor(F.predators[c] * M.SCALE.predators + 0.5)) })
      add(items, { t = "bar", icon = "leaf", label = "Grass", frac = F.grass[c], value = pct(F.grass[c]) })
    end
  elseif sel.kind == "volcano" then
    local vo = ents.volcanoes(W)[sel.index]
    out.title = (type(vo) == "table" and vo.name) or "Volcano"
    out.icon = "volcano"
    out.subtitle = M.region_name(W, sel.cell) or ""
    if type(vo) == "table" then
      for _, key in ipairs({ "pressure", "vei", "state" }) do
        if vo[key] ~= nil then add(items, { t = "row", icon = "volcano", label = cap(key), value = tostring(vo[key]) }) end
      end
    end
  else
    return nil
  end
  return out
end

return M
