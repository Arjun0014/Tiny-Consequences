local T = require("tests.sim.lib")
local names = require("content.names")
local M = {}

-- Deterministic u(): Park–Miller minimal standard LCG (products stay < 2^53, exact in doubles).
local function make_u(seed)
  local s = (seed % 2147483646) + 1
  return function()
    s = (s * 16807) % 2147483647
    return (s - 1) / 2147483646
  end
end

local NC = #names.CULTURES
local KINDS = names.REGION_KINDS

-- Every generator as gen(u) -> string, with a label.
local function all_generators()
  local g = {}
  for ci = 1, NC do
    g[#g + 1] = { "person_f_" .. ci, function(u) return names.person(u, ci, "f") end }
    g[#g + 1] = { "person_m_" .. ci, function(u) return names.person(u, ci, "m") end }
    g[#g + 1] = { "person_any_" .. ci, function(u) return names.person(u, ci) end }
    g[#g + 1] = { "surname_" .. ci, function(u) return names.surname(u, ci) end }
    g[#g + 1] = { "village_" .. ci, function(u) return names.village(u, ci) end }
    g[#g + 1] = { "new_village_" .. ci, function(u) return names.new_village(u, ci, "Muddlewick") end }
  end
  for i = 1, #KINDS do
    local k = KINDS[i]
    g[#g + 1] = { "region_" .. k, function(u) return names.region(u, k) end }
  end
  g[#g + 1] = { "volcano", names.volcano }
  g[#g + 1] = { "tanker", names.tanker }
  g[#g + 1] = { "god", names.god }
  return g
end

local function run_all(seed)
  local u = make_u(seed)
  local out = {}
  local gens = all_generators()
  for i = 1, #gens do
    for _ = 1, 25 do out[#out + 1] = gens[i][2](u) end
  end
  local used = {}
  for _ = 1, 50 do out[#out + 1] = names.unique(u, names.god, used, 3) end
  return out
end

function M.determinism()
  local a, b = run_all(1234), run_all(1234)
  T.eq(#a, #b, "same length")
  for i = 1, #a do T.eq(a[i], b[i], "same output at " .. i) end
  -- each generator alone, fresh u, must match its slice when run in isolation twice
  local gens = all_generators()
  for i = 1, #gens do
    local u1, u2 = make_u(99 + i), make_u(99 + i)
    for _ = 1, 40 do T.eq(gens[i][2](u1), gens[i][2](u2), gens[i][1] .. " deterministic") end
  end
  -- different seeds should differ somewhere (u is actually consumed)
  local c = run_all(4321)
  local diff = 0
  for i = 1, #a do if a[i] ~= c[i] then diff = diff + 1 end end
  T.ok(diff > #a / 2, "different seeds give different names")
end

function M.cultures_and_lists()
  T.between(NC, 5, 6, "culture count")
  local fields = { "on", "mid", "nu", "co", "person", "fem", "masc", "place", "place_end",
    "place_pre", "place_post", "sur_end", "fallback_f", "fallback_m", "fallback_place", "fallback_sur" }
  for ci = 1, NC do
    local c = names.CULTURES[ci]
    T.ok(type(c.id) == "string", "culture id")
    for f = 1, #fields do T.ok(type(c[fields[f]]) == "table" and #c[fields[f]] > 0, c.id .. "." .. fields[f]) end
    for f = 1, #c.fallback_f do T.ok(c.fallback_f[f]:match("^%u%l+$") and not names.is_blocked(c.fallback_f[f]), "fallback_f") end
    for f = 1, #c.fallback_m do T.ok(c.fallback_m[f]:match("^%u%l+$") and not names.is_blocked(c.fallback_m[f]), "fallback_m") end
    for f = 1, #c.fallback_place do T.ok(not names.is_blocked(c.fallback_place[f]), "fallback_place") end
    for f = 1, #c.place_pre do T.ok(not names.is_blocked(c.place_pre[f]), "place_pre") end
    for f = 1, #c.place_post do T.ok(not names.is_blocked(c.place_post[f]), "place_post") end
  end
  -- every fixed word list is clean
  local L = names.LISTS
  local lists = { L.region_adj, L.cont_a, L.cont_b, L.hopeful, L.volcano_titles, L.volcano_names,
    L.volcano_suffix, L.volcano_special, L.tanker_fixed, L.tanker_adv, L.tanker_adj, L.tanker_adj2,
    L.tanker_noun, L.tanker_virtue, L.god_adj, L.god_noun }
  for i = 1, #KINDS do
    local r = L.region[KINDS[i]]
    T.ok(r and #r.nouns >= 5, "region nouns for " .. KINDS[i])
    lists[#lists + 1] = r.adj; lists[#lists + 1] = r.nouns
  end
  for i = 1, #lists do
    for j = 1, #lists[i] do T.ok(not names.is_blocked(lists[i][j]), "blocked list word: " .. lists[i][j]) end
  end
  T.ok(#L.region_adj >= 30, "≥30 shared region adjectives")
  T.ok(#L.god_adj >= 30 and #L.god_noun >= 30, "god lists ≥30×30")
  T.ok(#L.tanker_fixed >= 25, "≥25 fixed tanker names")
  T.ok(#L.volcano_names * #L.volcano_titles >= 25, "≥25 volcano combos")
  -- blocklist sanity
  T.ok(names.is_blocked("Little Bum"), "word block")
  T.ok(names.is_blocked("Shitwick"), "substring block")
  T.ok(not names.is_blocked("Nightfall"), "nig-h allowed")
  T.ok(not names.is_blocked("Grasslands"), "word-only 'ass'")
  local sd = names.species_display
  T.eq(sd.grazers.one, "deer"); T.eq(sd.grazers.many, "deer")
  T.eq(sd.predators.one, "wolf"); T.eq(sd.predators.many, "wolves")
  T.eq(sd.fish.one, "fish"); T.eq(sd.fish.many, "fish")
end

function M.person_names()
  local details = {}
  for ci = 1, NC do
    local u = make_u(7000 + ci)
    local seen, uniq = {}, 0
    for i = 1, 2000 do
      local sex = (i % 2 == 0) and "f" or "m"
      local n = names.person(u, ci, sex)
      T.ok(type(n) == "string" and n:match("^%u%l+$") ~= nil, "pattern: " .. tostring(n))
      T.between(#n, 3, 9, "length of " .. n)
      T.ok(not names.is_blocked(n), "blocked: " .. n)
      if not seen[n] then seen[n] = true; uniq = uniq + 1 end
    end
    T.ok(uniq >= 1200, names.CULTURES[ci].id .. " unique " .. uniq .. "/2000")
    details[#details + 1] = string.format("%s %d%%", names.CULTURES[ci].id, math.floor(uniq / 20))
    -- surnames: well-formed
    for _ = 1, 300 do
      local s = names.surname(u, ci)
      T.ok(s:match("^%u%l+$") ~= nil and not names.is_blocked(s), "surname " .. s)
    end
  end
  return table.concat(details, ", ")
end

local function check_set(label, gen, seed)
  local u = make_u(seed)
  local seen, uniq = {}, 0
  for _ = 1, 500 do
    local n = gen(u)
    T.ok(type(n) == "string" and #n > 0, label .. " non-empty")
    T.ok(#n <= 28, label .. " too long: " .. n)
    T.ok(not names.is_blocked(n), label .. " blocked: " .. n)
    if not seen[n] then seen[n] = true; uniq = uniq + 1 end
  end
  T.ok(uniq >= 250, label .. " unique " .. uniq .. "/500")
  return uniq
end

function M.place_and_misc_names()
  local worst, worst_label = 501, ""
  local function track(label, n) if n < worst then worst, worst_label = n, label end end
  for ci = 1, NC do
    track("village" .. ci, check_set("village" .. ci, function(u) return names.village(u, ci) end, 100 + ci))
    track("new_village" .. ci, check_set("new_village" .. ci, function(u)
      return names.new_village(u, ci, names.village(u, ci))
    end, 200 + ci))
  end
  -- new_village also copes with nil / long / already-"New" origins
  local u = make_u(5)
  for _ = 1, 200 do
    T.ok(#names.new_village(u, 1, nil) > 0, "nil origin")
    T.ok(#names.new_village(u, 2, "Nether Grumblebottomwick End") <= 28, "long origin")
    local s = names.new_village(u, 3, "New Hope")
    T.ok(s ~= "New New Hope", "New New")
  end
  for i = 1, #KINDS do
    local k = KINDS[i]
    track("region_" .. k, check_set("region_" .. k, function(uu) return names.region(uu, k) end, 300 + i))
  end
  track("volcano", check_set("volcano", names.volcano, 401))
  track("tanker", check_set("tanker", names.tanker, 402))
  track("god", check_set("god", names.god, 403))
  return string.format("lowest unique: %s %d/500", worst_label, worst)
end

function M.unique_helper()
  local u = make_u(77)
  -- realistic generator: never duplicates over 300 draws
  local used, seen = {}, {}
  for _ = 1, 300 do
    local n = names.unique(u, names.volcano, used, 5)
    T.ok(not seen[n], "duplicate from unique: " .. n)
    seen[n] = true
    T.ok(used[n] == true, "recorded in used set")
  end
  -- tiny generator forces roman numerals
  local tiny = function(uu) return (uu() < 0.5) and "Ann" or "Bo" end
  local used2, seen2, romans = {}, {}, 0
  for _ = 1, 300 do
    local n = names.unique(u, tiny, used2, 4)
    T.ok(not seen2[n], "duplicate from unique(tiny): " .. n)
    seen2[n] = true
    if n:match(" [IVXLCDM]+$") then romans = romans + 1 end
  end
  T.ok(romans >= 290, "numerals once both base names are used: " .. romans)
  T.ok(seen2["Ann II"] and seen2["Bo II"] and seen2["Ann III"] and seen2["Bo IV"], "roman formatting")
end

-- Guard the purity rules for content/names.lua (05_ARCHITECTURE.md §4).
function M.source_is_pure()
  local f = io.open("content/names.lua", "r")
  T.ok(f ~= nil, "can read content/names.lua")
  local src = f:read("*a"); f:close()
  local banned = { "math%.random", "[^%w_]os%.", "[^%w_]io%.", "[^i]pairs%(", "goto", "//",
    "table%.unpack", "require" }
  for i = 1, #banned do T.ok(not src:find(banned[i]), "banned construct: " .. banned[i]) end
end

return M
