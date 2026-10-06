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
-- (S21, R7 v1, D-174) the v3 world's powers (sim/powers3.lua): each tip says what it writes, never what
-- will follow
M.DEFS_V3 = {
  { id = "lightning", icon = "bolt", name = "Lightning", cooldown = 1, cool_h = 2, radius = 0,
    tip = "Lightning: a bolt at the point - close in, on a village; the tallest thing there takes it" },
  { id = "rain", icon = "rain", name = "Rain", cooldown = 4, cool_h = 12, radius = 2, tip = "Rain: a cloudburst over these hills for three days" },
  { id = "dry", icon = "drought", name = "Dry spell", cooldown = 4, cool_h = 12, radius = 2, tip = "Dry spell: no rain and a little heat here for six days" },
  { id = "warm", icon = "heat", name = "Warm air", cooldown = 4, cool_h = 12, radius = 2, tip = "Warm air: 8 degrees warmer here for four days" },
  { id = "cold", icon = "chill", name = "Cold air", cooldown = 4, cool_h = 12, radius = 2, tip = "Cold air: 8 degrees colder here for four days" },
  { id = "stir", icon = "volcano", name = "Stir the mountain", cooldown = 10, cool_h = 96, radius = 0,
    tip = "Stir the mountain: the nearest volcano's pressure rises (it erupts when it does)" },
  { id = "strain", icon = "pickaxe", name = "Strain the fault", cooldown = 10, cool_h = 96, radius = 0,
    tip = "Strain the fault: the nearest plate boundary's stress rises (it gives when it does)" },
  { id = "cull", icon = "skull", name = "Cull", cooldown = 5, cool_h = 48, radius = 2,
    species = { { id = "wolves", icon = "wolf", name = "wolves" }, { id = "deer", icon = "deer", name = "deer" } },
    tip = "Cull: nine in ten of the wolves or the deer here die where they stand" },
  { id = "bring", icon = "paw", name = "Bring", cooldown = 5, cool_h = 48, radius = 1,
    species = { { id = "deer", icon = "deer", name = "deer" }, { id = "wolves", icon = "wolf", name = "wolves" }, { id = "fish", icon = "fish", name = "fish" } },
    tip = "Bring: a herd of deer, a pack of wolves, or a shoal of fish here" },
  -- (S23, R7 v2)
  { id = "sickness", icon = "thermo", name = "Sickness in a herd", cooldown = 6, cool_h = 48, radius = 0,
    tip = "Sickness in a herd: a few beasts of the herd you aim at catch a sickness - it shows in a day or two" },
  { id = "blight", icon = "blight", name = "Blight", cooldown = 6, cool_h = 48, radius = 0,
    tip = "Blight: a plant sickness in the growing field you aim at" },
  { id = "sea", icon = "wave", name = "Warm or cool the sea", cooldown = 6, cool_h = 72, radius = 2,
    modes = { { id = "warm", icon = "sun", name = "warm" }, { id = "cool", icon = "snow", name = "cool" } },
    tip = "Warm or cool the sea: the water here 4 degrees warmer or cooler for sixteen days" },
  { id = "oil", icon = "oil", name = "Oil", cooldown = 8, cool_h = 48, radius = 0, tip = "Oil: a slick on the sea here - the currents carry it" },
  { id = "meteor", icon = "meteor", name = "Meteor", cooldown = 20, cool_h = 168, radius = 2,
    tip = "Meteor: a rock from the sky here - heat, fire, the ground shaken, dust in the air; a wave if it falls in the sea" },
}
M.DEFS_V2 = M.DEFS
M.BY_ID = {}
for i, d in ipairs(M.DEFS) do d.index = i; M.BY_ID[d.id] = d end

-- (S21) The bar's set for world W (the v3 set in v3 worlds).
function M.use_world(W)
  local list = (W and W.v3) and M.DEFS_V3 or M.DEFS_V2
  if M.DEFS == list and M.set_for == (W and W.v3) then return end
  M.DEFS = list
  M.set_for = W and W.v3
  M.BY_ID = {}
  for i, d in ipairs(list) do d.index = i; M.BY_ID[d.id] = d end
  M.armed = nil
end

-- (S25, D-205) The v3 powers recharge in GAME time: cool_h game hours after each cast (lightning 2 h, the air and the
-- rain half a day, the beasts, the fields, the sea and oil two or three days, the mountain and the fault four, the
-- meteor seven). Skipping time recharges them as it passes; the bar shows each one's countdown.
M.ready = {}            -- power id -> the game hour it is ready again (v3)
function M.now(W)
  local sp = package.loaded["game.platform.speed"]
  return (sp and sp.v3_t) or (W and W.hour) or 0
end
function M.left_h(W, d)
  local r = d and M.ready[d.id]
  if not r then return 0 end
  local l = r - M.now(W)
  return l > 0 and l or 0
end
function M.fmt_h(h)
  if h <= 0 then return "now" end
  if h < 1 then return string.format("%d min", math.max(1, math.floor(h * 60 + 0.5))) end
  if h < 24 then return string.format("%d h", math.floor(h + 0.5)) end
  local dd = math.floor(h / 24)
  local hh = math.floor(h - dd * 24 + 0.5)
  if hh == 24 then dd, hh = dd + 1, 0 end
  return hh > 0 and string.format("%d d %d h", dd, hh) or string.format("%d d", dd)
end
function M.short_h(h)
  if h < 1 then return string.format("%dm", math.max(1, math.floor(h * 60 + 0.5))) end
  if h < 24 then return string.format("%dh", math.floor(h + 0.5)) end
  return string.format("%dd", math.floor(h / 24 + 0.5))
end

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
  if W.v3 and d.cool_h and not opts.force then
    local left = M.left_h(W, d)
    if left > 0 then return refuse(d.name .. " is recharging: ready in " .. M.fmt_h(left)) end
  elseif (M.cool[id] or 0) > 0 and not opts.force then return refuse(d.name .. " is recharging") end
  local o = M.option(d)
  local p = { power = id, cell = cell }
  if d.modes then p.mode = opts.mode or (o and o.id) end
  if d.species then p.species = opts.species or (o and o.id) end
  p.ship, p.list, p.person = opts.ship, opts.list, opts.person
  local ocean = W.geo.ocean[cell] == 1
  if W.v3 then
    p.sid, p.x, p.y = opts.sid, opts.x, opts.y
    if id == "cull" or id == "bring" then
      local sp = p.species
      if sp == "fish" and not ocean then return refuse("Fish need the sea") end
      if sp ~= "fish" and ocean then return refuse("Wild beasts need land") end
    end
    -- (S23, R7 v2) what each needs: a herd, a field, the sea
    if id == "oil" and not ocean then return refuse("Oil goes on the sea") end
    if id == "sea" then
      local any = ocean
      local g = W.grid
      for k = 1, g.nbr_count[cell] do if W.geo.ocean[g.nbr[cell * 6 - 6 + k]] == 1 then any = true end end
      if not any then return refuse("Aim at the sea") end
    end
    if (id == "sickness" or id == "blight") and not opts.sid then return refuse("Come close to a village and aim at its " .. (id == "blight" and "fields" or "herds")) end
    -- (S26) the meteor's command is given when the rock it throws reaches the ground (game/view/meteor.lua), so nothing of
    -- what it does begins before the strike is seen; opts.instant (the harnesses): at once, as every other power
    local deferred = id == "meteor" and not opts.instant and M.defer_meteor ~= nil
    if deferred then
      M.defer_meteor(W, cell, p, function() require("sim.world").command(W, "power", p) end)
    else
      require("sim.world").command(W, "power", p)
    end
    if M.on_cast then M.on_cast(W, id, cell, p, deferred) end
    do local ok, audio = pcall(require, "game.audio.audio"); if ok and audio.ui then audio.ui("ui_power") end end
    M.ready[id] = M.now(W) + (d.cool_h or 0)
    M.casts = M.casts + 1
    M.last = { id = id, cell = cell, p = p }
    return p
  end
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
  local deferred = id == "meteor" and not opts.instant and M.defer_meteor ~= nil
  if deferred then
    M.defer_meteor(W, cell, p, function() require("sim.world").command(W, "power", p) end)
  else
    require("sim.world").command(W, "power", p)
  end
  if M.on_cast then M.on_cast(W, id, cell, p, deferred) end
  M.cool[id] = d.cooldown
  M.casts = M.casts + 1
  M.last = { id = id, cell = cell, p = p }
  return p
end

return M
