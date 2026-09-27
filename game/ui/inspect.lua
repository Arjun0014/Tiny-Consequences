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

local function add(items, t) items[#items + 1] = t end

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

local function village_items(W, v, items)
  local mem = type(v.members) == "table" and v.members or {}
  local alive = 0
  for k = 1, #mem do if ents.is_alive(W, mem[k]) then alive = alive + 1 end end
  local g = W.grid
  add(items, { t = "row", icon = "people", label = "Population", value = tostring(alive) })
  local fm = v.food_months or v.food_security
  if type(fm) == "number" then
    add(items, { t = "row", icon = "bread", label = "Food", value = num(fm) .. " months" .. (fm < 2 and " (hungry)" or ""), hl = fm < 2 })
  elseif type(v.food) == "number" then
    add(items, { t = "row", icon = "bread", label = "Food store", value = num(v.food) })
  end
  local farms = type(v.farms) == "table" and #v.farms or tonumber(v.farms)
  if farms then add(items, { t = "row", icon = "wheat", label = "Farms", value = tostring(farms) }) end
  local boats = type(v.boats) == "table" and #v.boats or tonumber(v.boats)
  local fishing = type(v.fishing) == "table" and #v.fishing or nil
  if (boats and boats > 0) or (fishing and fishing > 0) then
    add(items, { t = "row", icon = "boat", label = "Fishing", value = (boats or 0) == 1 and "1 boat" or ((boats or 0) .. " boats") })
  end
  local houses = tonumber(v.houses)
  if houses then add(items, { t = "row", icon = "house", label = "Houses", value = tostring(houses) }) end
  if type(v.founded) == "number" then
    local parent = v.from and W.ent and W.ent.villages and W.ent.villages[v.from]
    add(items, { t = "row", icon = "hourglass", label = "Founded", value = (v.founded < 0 and "Long ago" or ("Year " .. floor(v.founded / 48)))
      .. (parent and parent.name and (", from " .. parent.name) or "") })
  end
  if v.alive == false then
    add(items, { t = "row", icon = "skull", label = "Abandoned", value = type(v.abandoned) == "number" and ("Year " .. floor(v.abandoned / 48)) or "yes", hl = true })
  end
  if v.state then
    local st = tostring(v.state)
    local bad = st == "famine" or st == "struggling" or st == "abandoned"
    add(items, { t = "row", icon = bad and "skull" or "heart", label = "Mood", value = cap(st), hl = bad })
  end
  local conts = W.names and W.names.continents
  local home = conts and v.continent and conts[v.continent]
  if type(home) == "table" then home = home.name end
  if type(home) == "string" then add(items, { t = "row", icon = "globe", label = "Homeland", value = home })
  elseif type(v.culture) == "string" then add(items, { t = "row", icon = "globe", label = "Culture", value = cap(v.culture) }) end
  -- recent events around the village and its farms
  local cells = { [v.cell] = true }
  if type(v.farms) == "table" then for _, f in ipairs(v.farms) do if type(f) == "number" then cells[f] = true end end end
  local base = v.cell * 6 - 6
  for k = 1, g.nbr_count[v.cell] do cells[g.nbr[base + k]] = true end
  local ev = M.recent_events(W, cells, 20, 4)
  if #ev > 0 then
    add(items, { t = "head", text = "Recent events" })
    for _, e in ipairs(ev) do
      add(items, { t = "row", icon = e.actor == "player" and "star" or "info", label = "Year " .. e.year, value = e.text })
    end
  end
  add(items, { t = "head", text = "Residents" })
  local shown = 0
  for k = 1, #mem do
    local pid = mem[k]
    local p = ents.person(W, pid)
    if p and ents.is_alive(W, pid) then
      shown = shown + 1
      if shown <= 8 then
        local age = ents.age(W, p)
        add(items, { t = "row", icon = M.JOB_ICON[p.job] or "person", label = p.name or ("#" .. tostring(pid)),
          value = cap(p.job or "") .. (age and (", " .. age) or ""), link = { kind = "person", id = pid } })
      end
    end
  end
  if shown > 8 then add(items, { t = "text", text = "…and " .. (shown - 8) .. " more" }) end
  if shown == 0 then add(items, { t = "text", text = "Nobody lives here any more." }) end
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
  local function bar01(x) if type(x) ~= "number" then return nil end if x > 1 then return x / 100 end return x end
  local hunger, health = bar01(p.hunger), bar01(p.health)
  if health then add(items, { t = "bar", icon = "heart", label = "Health", frac = health, value = pct(health) .. (health < 0.3 and " (ill)" or ""), hl = health < 0.3 }) end
  if hunger then add(items, { t = "bar", icon = "bread", label = "Hunger", frac = hunger, value = pct(hunger) .. (hunger > 0.6 and " (starving)" or ""), hl = hunger > 0.6 }) end
  if p.mood ~= nil then
    local mv = p.mood
    local word = type(mv) == "number" and ((mv > 0.35 and "cheerful") or (mv > 0.05 and "content") or (mv > -0.3 and "worried") or "miserable") or cap(mv)
    add(items, { t = "row", icon = "bubble", label = "Mood", value = word, hl = type(mv) == "number" and mv < -0.3 })
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
    add(items, { t = "row", icon = "skull", label = "Died", value = "Year " .. floor(p.dead / 48) .. (p.died_of and (", " .. cap(p.died_of)) or ""), hl = true })
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
