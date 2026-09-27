-- Player powers, game side (Stage 2.1, 04_PLAYER.md §2): the power bar's state (armed power,
-- modes, species, real-time cooldowns), target checks and casting. Casting only queues the
-- logged sim command `power` (sim/powers.lua applies it at the next tick): the view never
-- writes sim state. Shared by the HUD (bar), controls (aiming, clicks), fx (cast effects) and
-- the test bridge (`power` command).
local M = {}

-- radius: level-4 steps (as sim/powers.lua), for the aiming ring
M.DEFS = {
  { id = "lightning", icon = "bolt", name = "Lightning", cooldown = 1, radius = 0,
    tip = "Lightning: sets dry land alight, sinks a ship, strikes a villager" },
  { id = "weather", icon = "rain", name = "Weather", cooldown = 5, radius = 2,
    modes = { { id = "rain", icon = "rain", name = "Deluge" }, { id = "drought", icon = "drought", name = "Drought" } },
    tip = "Weather: a season of deluge or drought" },
  { id = "temperature", icon = "heat", name = "Temperature", cooldown = 5, radius = 2,
    modes = { { id = "heat", icon = "heat", name = "Heat" }, { id = "chill", icon = "chill", name = "Chill" } },
    tip = "Temperature: a season of heat or chill (the sea holds it longer)" },
  { id = "cull", icon = "skull", name = "Cull", cooldown = 5, radius = 2,
    species = { { id = "predators", icon = "wolf", name = "wolves" }, { id = "grazers", icon = "deer", name = "deer" },
      { id = "fish", icon = "fish", name = "fish" } },
    tip = "Cull: removes nine in ten of a species here" },
  { id = "introduce", icon = "paw", name = "Introduce", cooldown = 5, radius = 1,
    species = { { id = "grazers", icon = "deer", name = "deer" }, { id = "predators", icon = "wolf", name = "wolves" },
      { id = "fish", icon = "fish", name = "fish" }, { id = "settlers", icon = "settlers", name = "settlers" } },
    tip = "Introduce: a breeding herd, a pack, a shoal, or five settlers by boat" },
  { id = "blight", icon = "blight", name = "Blight", cooldown = 20, radius = 0,
    tip = "Blight: infects a farm; it spreads farm to farm and village to village" },
  { id = "oil", icon = "oil", name = "Oil Spill", cooldown = 10, radius = 1,
    tip = "Oil Spill: a tanker's worth of oil (or sinks the tanker you aim at)" },
  { id = "meteor", icon = "meteor", name = "Meteor", cooldown = 60, radius = 2,
    tip = "Meteor: fire, ruin and an impact winter" },
}
M.BY_ID = {}
for i, d in ipairs(M.DEFS) do d.index = i; M.BY_ID[d.id] = d end

M.armed = nil           -- index of the armed power, or nil
M.mode = {}             -- power id -> selected mode / species index
M.cool = {}             -- power id -> seconds left
M.hint = nil            -- short message after a refused cast { text, t }
M.casts = 0

function M.option(d)
  local list = d.modes or d.species
  if not list then return nil end
  return list[M.mode[d.id] or 1]
end

function M.icon(d)
  local o = M.option(d)
  if d.modes and o then return o.icon end
  return d.icon
end

function M.arm(i)
  if M.armed == i then M.armed = nil else M.armed = i end
end

function M.disarm() M.armed = nil end

function M.cycle_option(d, k)
  local list = d.modes or d.species
  if not list then return end
  M.mode[d.id] = k or ((M.mode[d.id] or 1) % #list + 1)
end

function M.tick(dt)
  for id, t in pairs(M.cool) do
    if t > 0 then M.cool[id] = math.max(0, t - dt) end
  end
  if M.hint then
    M.hint.t = M.hint.t - dt
    if M.hint.t <= 0 then M.hint = nil end
  end
end

local function refuse(text)
  M.hint = { text = text, t = 2.2 }
  return nil, text
end

--- Checks and casts power `id` at sim cell `cell` for world W. opts: mode, species, ship,
-- list, person (from picking). Returns the queued params, or nil and a reason.
function M.cast(W, id, cell, opts)
  opts = opts or {}
  local d = M.BY_ID[id]
  if not d then return refuse("Unknown power") end
  if not cell or not W or cell < 1 or cell > W.n then return refuse("Nowhere to aim") end
  if (M.cool[id] or 0) > 0 and not opts.force then return refuse(d.name .. " is recharging") end
  local o = M.option(d)
  local p = { power = id, cell = cell }
  if d.modes then p.mode = opts.mode or (o and o.id) end
  if d.species then p.species = opts.species or (o and o.id) end
  p.ship, p.list, p.person = opts.ship, opts.list, opts.person
  local ocean = W.geo.ocean[cell] == 1
  if id == "blight" then
    local lu = W.F.land_use
    local ok = lu[cell] > 0
    if not ok then
      local g = W.grid
      local b = cell * 6 - 6
      for k = 1, g.nbr_count[cell] do if lu[g.nbr[b + k]] > 0 then ok = true end end
    end
    if not ok then return refuse("Blight needs farmland") end
  elseif id == "oil" then
    if not ocean and not p.ship then return refuse("Oil goes on the sea") end
  elseif id == "introduce" then
    local sp = p.species
    if sp == "fish" and not ocean then return refuse("Fish need the sea") end
    if (sp == "grazers" or sp == "predators") and ocean then return refuse("Animals need land") end
  elseif id == "cull" then
    local sp = p.species
    if sp == "fish" and not ocean then return refuse("No fish here") end
    if sp ~= "fish" and ocean then return refuse("No animals at sea") end
  end
  require("sim.world").command(W, "power", p)
  if M.on_cast then M.on_cast(W, id, cell, p) end
  M.cool[id] = d.cooldown
  M.casts = M.casts + 1
  M.last = { id = id, cell = cell, p = p }
  return p
end

return M
