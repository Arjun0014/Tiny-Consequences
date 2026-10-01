-- Persons and households of the v3 agent layer (docs/v3/10_AGENTS.md §4, §7).
--
-- W.S.v3.people[id] (a map; ids never reused), W.S.v3.households[id]. A person persists from
-- birth to death with identity, family, body, mind, skills and a continuous position/action
-- (see sim/agents/act.lua for the action fields).
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local names = require("content.names")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min = math.floor, math.max, math.min

local function C() return cfgmod.V3 end

-- Life stage at hour t.
function M.stage(p, t)
  local a = (t - p.born) / 1152
  local c = C()
  if a < c.child_age then return "infant", a end
  if a < c.apprentice_age then return "child", a end
  if a < c.adult_age then return "apprentice", a end
  if a < c.elder_age then return "adult", a end
  return "elder", a
end

function M.age(p, t) return (t - p.born) / 1152 end

-- A deterministic stream of floats for one person and purpose.
function M.u(W, p, purpose, k)
  return rng.float(W.seed, rng.S.V3_PEOPLE, p.key, purpose, k or 0)
end

local function ufn(W, key, purpose)
  local k = 0
  return function()
    k = k + 1
    return rng.float(W.seed, rng.S.V3_PEOPLE, key, purpose, k)
  end
end

-- Create a person. opts: sex, born, mother, father, hh, sett, surname, x, y, role.
function M.new(W, opts)
  local V = W.S.v3
  local id = V.next_person
  V.next_person = id + 1
  local key = rng.hash(W.seed, rng.S.V3_PEOPLE, opts.sett or 0, floor(opts.born * 60) % 4294967296, id)
  local u = ufn(W, key, 1)
  local sex = opts.sex or (u() < 0.5 and "f" or "m")
  local c = C()
  local p = {
    id = id, key = key, sex = sex, born = opts.born,
    name = names.person(ufn(W, key, 2), opts.culture or 1, sex),
    surname = opts.surname or names.surname(ufn(W, key, 3), opts.culture or 1),
    mother = opts.mother or 0, father = opts.father or 0, partner = 0, kids = {},
    hh = opts.hh or 0, sett = opts.sett or 0,
    -- mind: chronotype (wake offset, hours), pace, diligence, sociability, patience, route temperament
    chrono = c.chrono[1] + (c.chrono[2] - c.chrono[1]) * u(),
    pace = c.pace[1] + (c.pace[2] - c.pace[1]) * u(),
    dil = 0.6 + 0.8 * u(), soc = u(), pat = u(),
    temper = floor(u() * 3),
    strong = u() < 0.4,
    -- skills (0..1, grow with practice)
    sk_farm = 0.2, sk_wood = 0.2, sk_build = 0.2, sk_fish = 0.2, sk_craft = 0.2,
    clothes = 1,                           -- the condition of what they wear (D-088)
    role = opts.role or "",
    mentor = 0,
    -- body
    hunger = 0.2 + 0.2 * u(), energy = 0.8, health = 1, fear = 0,
    preg_due = 0, nursing = 0, babe = 0,   -- babe: the infant they hold (sim/agents/kin.lua)
    -- relationships (id, affinity) and memories (ring)
    rel_id = {}, rel_v = {}, mem = {}, mem_n = 0,
    -- now
    x = opts.x or 0, y = opts.y or 0, fx = 1, fy = 0,
    act = "idle", t0 = W.hour, t1 = W.hour, mv = nil, spd = 0, inside = 0,
    plan = {}, pi = 0, obj = "", why = "", dest = "", carry = "", camt = 0, lunch = false,
    ver = 0, alive = true, died = 0, died_of = "",
  }
  V.people[id] = p
  V.order[#V.order + 1] = id
  return p
end

function M.new_household(W, sett, home)
  local V = W.S.v3
  local id = V.next_household
  V.next_household = id + 1
  local h = { id = id, sett = sett, members = {}, home = home or 0, formed = W.hour }
  V.households[id] = h
  return h
end

function M.join(W, p, hh)
  local V = W.S.v3
  if p.hh ~= 0 and V.households[p.hh] then M.remove_id(V.households[p.hh].members, p.id) end
  p.hh = hh.id
  hh.members[#hh.members + 1] = p.id
end

function M.remove_id(list, id)
  for i = 1, #list do
    if list[i] == id then table.remove(list, i); return true end
  end
  return false
end

-- Home building of person p (or nil).
function M.home(W, s, p)
  local hh = W.S.v3.households[p.hh]
  if not hh or hh.home == 0 then return nil end
  local b = s.buildings[hh.home]
  if not b or b.state ~= "standing" then return nil end
  return b
end

-- Raw food in a house's pantry (FU): grain and gathered food, fresh fish (D-087), meat, milk and
-- cheese (D-092).
function M.pantry(b)
  local st = b.stock
  return (st.food or 0) + (st.fish or 0) + (st.smoked or 0) + (st.meat or 0) + (st.milk or 0) + (st.cheese or 0)
end

-- Walking speed (world units per hour) at time t.
function M.speed(W, p, t)
  local c = C()
  local st = M.stage(p, t)
  local v = c.walk * p.pace
  if st == "child" then v = v * c.speed_child
  elseif st == "elder" then v = v * c.speed_elder end
  if p.carry ~= "" and p.carry ~= "seed" then v = v * c.speed_load end
  if p.health < 0.5 then v = v * (0.6 + 0.8 * p.health) end
  return v
end

-- Skills: practice raises skill toward 1 (hours of the work).
function M.practice(p, field, hours)
  local v = p[field]
  p[field] = v + (1 - v) * min(0.2, hours * 0.004)
end

-- Relationships: add affinity (creates the entry; keeps at most 10, dropping the weakest).
function M.bond(p, q, amt)
  local ids, vs = p.rel_id, p.rel_v
  for i = 1, #ids do
    if ids[i] == q.id then
      local v = vs[i] + amt
      if v > 1 then v = 1 elseif v < -1 then v = -1 end
      vs[i] = v
      return
    end
  end
  if #ids >= 10 then
    local wi, wv = 1, 9
    for i = 1, #ids do
      local a = vs[i] < 0 and -vs[i] or vs[i]
      if a < wv then wi, wv = i, a end
    end
    if wv > (amt < 0 and -amt or amt) then return end
    table.remove(ids, wi)
    table.remove(vs, wi)
  end
  ids[#ids + 1] = q.id
  vs[#vs + 1] = amt
end

function M.affinity(p, qid)
  local ids = p.rel_id
  for i = 1, #ids do if ids[i] == qid then return p.rel_v[i] end end
  return 0
end

-- Memories: a ring of the last 8 things that happened to them (hour, text).
function M.remember(p, t, text)
  local n = p.mem_n % 8 + 1
  p.mem_n = p.mem_n + 1
  p.mem[n] = { t, text }
end

function M.full_name(p) return p.name .. " " .. p.surname end

-- Is person q (id) kin of p (parent, child, sibling, partner)?
function M.is_kin(W, p, qid)
  if qid == p.partner or qid == p.mother or qid == p.father then return true end
  for i = 1, #p.kids do if p.kids[i] == qid then return true end end
  local q = W.S.v3.people[qid]
  if q and ((q.mother ~= 0 and q.mother == p.mother) or (q.father ~= 0 and q.father == p.father)) then return true end
  return false
end

return M
