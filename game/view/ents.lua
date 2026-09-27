-- Nil-safe read-only access to sim entities for the view and UI (Stage 1.9).
-- Schema (sim/systems/villages.lua header, Stage 1.7):
--   W.ent.villages : array of { id, key, name, cell, culture, founded, members, food,
--                    food_security, farms, boats, houses, state, cause, alive }
--   W.ent.people   : lookup by id -> { name, sex, birth, job, health, hunger, partner, parents,
--                    children, traits, memories, mood, village? }
--   W.ent.ships    : array of ships (tankers on lanes, boats) with a cell and/or position
-- Everything here tolerates missing tables and fields, so the view works before 1.7 lands.
local M = {}

local sqrt, cos, sin = math.sqrt, math.cos, math.sin

local function tbl(x) return type(x) == "table" and x or nil end

function M.villages(W)
  local e = tbl(W.ent)
  return e and tbl(e.villages) or {}
end

--- Iterates alive villages: for i, v in M.each_village(W)
function M.each_village(W)
  local list = M.villages(W)
  local i = 0
  return function()
    while true do
      i = i + 1
      local v = list[i]
      if v == nil then return nil end
      if type(v) == "table" and type(v.cell) == "number" and v.alive ~= false then return i, v end
    end
  end
end

function M.village(W, id)
  if id == nil then return nil end
  local list = M.villages(W)
  for i = 1, #list do
    local v = list[i]
    if type(v) == "table" and (v.id == id) then return v end
  end
  local v = list[id]
  if type(v) == "table" and v.id == nil then return v end
  return nil
end

function M.village_of_person(W, pid)
  local p = M.person(W, pid)
  if p and p.village then
    local v = M.village(W, p.village)
    if v then return v end
  end
  for _, v in M.each_village(W) do
    local mem = tbl(v.members)
    if mem then
      for k = 1, #mem do if mem[k] == pid then return v end end
    end
  end
  return nil
end

function M.people(W)
  local e = tbl(W.ent)
  return e and tbl(e.people) or {}
end

function M.person(W, id)
  if id == nil then return nil end
  local p = M.people(W)[id]
  return tbl(p)
end

function M.population(W)
  local n = 0
  for _, v in M.each_village(W) do
    local mem = tbl(v.members)
    n = n + (tonumber(v.pop) or (mem and #mem) or tonumber(v.population) or 0)
  end
  return n
end

function M.ships(W)
  local e = tbl(W.ent)
  return e and tbl(e.ships) or {}
end

function M.boats(W)
  local e = tbl(W.ent)
  return e and tbl(e.boats) or {}
end

--- Ship list by name: "ships" (tankers) or "boats" (fishing boats).
function M.ship_list(W, list)
  if list == "boats" then return M.boats(W) end
  return M.ships(W)
end

--- Unit direction of a ship (lane position, cell, lat/lon, or position fields), or nil.
function M.ship_dir(W, s)
  if type(s) ~= "table" then return nil end
  local x, y, z
  local lanes = W.static and W.static.lanes
  local lane = lanes and type(s.lane) == "number" and lanes[s.lane]
  local g = W.grid
  if lane and type(lane.cells) == "table" and #lane.cells > 0 and type(s.pos) == "number" then
    -- smooth: interpolate between consecutive lane cells by the fractional position
    local n = #lane.cells
    local i0 = math.floor(s.pos)
    local f = s.pos - i0
    local a = lane.cells[i0 % n + 1]
    local b = lane.cells[(i0 + 1) % n + 1]
    x = g.X[a] * (1 - f) + g.X[b] * f
    y = g.Y[a] * (1 - f) + g.Y[b] * f
    z = g.Z[a] * (1 - f) + g.Z[b] * f
  elseif type(s.x) == "number" and type(s.y) == "number" and type(s.z) == "number" then
    x, y, z = s.x, s.y, s.z
  elseif type(s.pos) == "table" and type(s.pos.x) == "number" then
    x, y, z = s.pos.x, s.pos.y, s.pos.z
  elseif type(s.lat) == "number" and type(s.lon) == "number" then
    local cl = cos(s.lat)
    x, y, z = cl * cos(s.lon), sin(s.lat), cl * sin(s.lon)
  elseif type(s.cell) == "number" and W.grid.X[s.cell] then
    x, y, z = W.grid.X[s.cell], W.grid.Y[s.cell], W.grid.Z[s.cell]
  end
  if not x then return nil end
  local l = sqrt(x * x + y * y + z * z)
  if l < 1e-9 then return nil end
  return x / l, y / l, z / l
end

function M.ship_kind(s)
  local k = type(s) == "table" and (s.kind or s.type) or nil
  if k == "tanker" or (type(s) == "table" and s.tanker) then return "tanker" end
  return "boat"
end

function M.volcanoes(W)
  local e = tbl(W.ent)
  return e and tbl(e.volcanoes) or {}
end

--- Age in whole years (the settlement layer stores births as absolute hours, 8,064 a year).
function M.age(W, p)
  local b = p and p.born
  if type(b) ~= "number" then return nil end
  return math.floor(((W.hour or 0) - b) / 8064)
end

function M.name_of(W, pid)
  local p = M.person(W, pid)
  return p and p.name or ("#" .. tostring(pid))
end

function M.is_alive(W, pid)
  local p = M.person(W, pid)
  if not p then return false end
  if p.alive == false or p.dead ~= nil then return false end
  return true
end

--- Person id or nil (the sim uses 0 for "none").
function M.pid(x)
  if x == nil or x == 0 or x == false then return nil end
  return x
end

return M
