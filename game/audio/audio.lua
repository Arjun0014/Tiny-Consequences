-- World audio (S21, docs/v3/80_WORLD_AUDIO.md): the sound of what is around the camera's focus, read from
-- the world - never invented. Ambient beds (loops, gains eased toward targets) follow the zoom (the planet
-- broad and quiet; close in, the village), the weather there, the woods, the shore, the river (and its
-- flood), people out of doors, the herds, anything burning, a mountain stirring. One-shots come only from
-- real actions near the focus (an axe in a hand that is felling, a hammer at a site, a dog that barked, a
-- beast in a herd near, a shout from someone alarmed, thunder from a strike, a quake felt, a house falling)
-- with distance, pan, cooldowns, takes and a density cap; at the skip speeds only the beds play. News cues
-- by level (sim/news.lua via game/ui/newsfeed.lua). Presentation only: its randomness never touches W.
local catalog = require("game.audio.catalog")
local mapm = require("sim.agents.map")
local clock = require("sim.agents.clock")

local M = {}
local floor, max, min, sqrt, abs = math.floor, math.max, math.min, math.sqrt, math.abs

M.ON = true
M.GROUPS = { master = 0.9, ambient = 0.7, sfx = 0.85, ui = 0.6 }
M.OBJ = "/sounds"
M.stats = { shots = 0, cues = 0, beds = {} }

local seed = 12345
local function rnd()
  seed = (seed * 1103515245 + 12345) % 2147483648
  return seed / 2147483648
end

local function url(name) return msg.url(nil, M.OBJ, name) end

local function have(id) return catalog.takes[id] ~= nil end

-- A take of sound id (id_1 .. id_n, or id).
local function take(id)
  local n = catalog.takes[id]
  if not n then return nil end
  if n == 1 then return id end
  return id .. "_" .. (1 + floor(rnd() * n))
end

local function smoothstep(a, b, x)
  local t = (x - a) / (b - a)
  if t < 0 then t = 0 elseif t > 1 then t = 1 end
  return t * t * (3 - 2 * t)
end

-- Beds --------------------------------------------------------------------------------------
local BEDS = { "amb_wind_high", "amb_wind_winter", "amb_forest_day", "amb_forest_night", "amb_coast", "amb_river", "amb_river_flood",
  "amb_rain", "amb_storm", "amb_village_day", "amb_village_night", "amb_fire", "amb_wildfire", "amb_volcano", "amb_herd", "amb_sea_open" }
local bed = {}     -- id -> { g, t, on, quiet }

function M.init()
  for name, g in pairs(M.GROUPS) do pcall(sound.set_group_gain, hash(name), g) end
  for _, id in ipairs(BEDS) do bed[id] = { g = 0, t = 0, on = false, quiet = 0 } end
  M.ready = true
end

local function ease_beds(dt)
  for _, id in ipairs(BEDS) do
    local b = bed[id]
    if have(id) then
      local t = M.ON and b.t or 0
      b.g = b.g + (t - b.g) * min(1, dt * 1.2)
      if b.g < 0.002 and t < 0.002 then
        b.quiet = b.quiet + dt
        if b.on and b.quiet > 2 then sound.stop(url(id)); b.on = false end
      else
        b.quiet = 0
        if not b.on and t > 0.004 then
          sound.play(url(id), { gain = max(0.001, b.g) })
          b.on = true
        end
        if b.on then sound.set_gain(url(id), min(1, b.g)) end
      end
      M.stats.beds[id] = b.on and floor(b.g * 100) / 100 or nil
    end
  end
end

-- One-shots -------------------------------------------------------------------------------------
local last_shot = {}      -- id -> real time of the last of that sound
local shots_window = {}   -- real times of the last shots (density cap)
M.MAX_PER_S = 4

local function shot(id, gain, pan, gap, now_s)
  if not M.ON or gain < 0.02 or not have(id) then return false end
  if now_s - (last_shot[id] or -99) < (gap or 0.25) then return false end
  local n = 0
  for i = #shots_window, 1, -1 do
    if now_s - shots_window[i] > 1 then table.remove(shots_window, i) else n = n + 1 end
  end
  if n >= M.MAX_PER_S then return false end
  last_shot[id] = now_s
  shots_window[#shots_window + 1] = now_s
  local name = take(id)
  sound.play(url(name), { gain = min(1, gain), pan = max(-0.8, min(0.8, pan or 0)), speed = 0.94 + 0.12 * rnd() })
  M.stats.shots = M.stats.shots + 1
  M.stats.last = name
  return true
end
M.shot = shot

-- The cue for a new story in the news (game/ui/newsfeed.lua): restrained; a stronger knock for danger,
-- a shimmer when the chain ends at the player.
function M.news_cue(st, root, now_s)
  if not st then return end
  local id = (root == "you" and "ui_you") or (st.level == 1 and "ui_breaking") or "ui_story"
  if shot(id, id == "ui_story" and 0.5 or 0.75, 0, 2.0, now_s or M.t or 0) then M.stats.cues = M.stats.cues + 1 end
end

function M.ui(id, now_s) shot(id, 0.6, 0, 0.08, now_s or M.t or 0) end

-- The world around the focus -------------------------------------------------------------------
local ACT_SND = { chop = "axe_chop", lop = "axe_chop", build = "hammer", repair = "hammer", mend_boat = "hammer", wall_fold = "hammer",
  help_kin = "hammer", quarry = "pick_stone", mine = "pick_stone", dig_out = "pick_stone", quern = "pick_stone", smith = "anvil",
  till = "hoe", grub = "hoe", tend = "hoe", spread = "hoe", harvest = "scythe", mow = "scythe", douse = "douse", fill = "splash" }
local ACT_GAP = { axe_chop = 1.6, hammer = 1.3, pick_stone = 1.5, anvil = 1.8, hoe = 2.2, scythe = 2.0, douse = 2.5, splash = 3.0 }
local SPECIES_SND = { sheep = "sheep", goat = "goat", cattle = "cow", pig = "pig" }
local ALARM = { fire = true, flood = true, thief = true, wave = true, quake = true }
local pgap = {}           -- person id -> real time of their last sound
local seen_bark = {}      -- dog -> its last bark heard
local deed_cur = nil

-- The village whose map the focus is on (the nearest frame), and the focus there in local units.
local function focus_village(V, fx, fy, fz)
  local best, bd = nil, 0.97
  for _, s in ipairs(V.settlements) do
    local f = s.frame
    if f and s.alive then
      local d = f[1] * fx + f[2] * fy + f[3] * fz
      if d > bd then best, bd = s, d end
    end
  end
  if not best then return nil end
  local x, y = mapm.local_of(best, fx, fy, fz)
  if not x then return nil end
  return best, x, y
end

-- Water, sea and flood near (x, y) on map s: the nearest distance of each (world units), scanned on a
-- coarse step (4 Hz is enough).
local function waters(s, x, y, R)
  local N, T = s.N, s.T
  local i0 = mapm.tile_of(s, x, y)
  if i0 == 0 then return 9, 9, 9 end
  local ix0, iy0 = (i0 - 1) % N, floor((i0 - 1) / N)
  local r = floor(R / T)
  local riv, land = s.riv, s.land
  local fd = mapm.FLOOD[s]
  local dr, ds, df = 1e9, 1e9, 1e9
  for dy = -r, r, 2 do
    local iy = iy0 + dy
    if iy >= 0 and iy < N then
      for dx = -r, r, 2 do
        local ix = ix0 + dx
        if ix >= 0 and ix < N then
          local i = iy * N + ix + 1
          local d2 = dx * dx + dy * dy
          if land[i] == 0 then if d2 < ds then ds = d2 end
          elseif riv[i] > 0 then if d2 < dr then dr = d2 end
          elseif fd and fd[i] > 0 and d2 < df then df = d2 end
        end
      end
    end
  end
  return sqrt(dr) * T, sqrt(ds) * T, sqrt(df) * T
end

local SAMPLE = { t = 0 }

function M.update(W, dt, zoom, now, focus, speed_level, skipping, project)
  if not M.ready or not W or not W.v3 or not W.S.v3 then return end
  M.t = (M.t or 0) + dt
  local now_s = M.t
  local V = W.S.v3
  local fx, fy, fz = focus[1], focus[2], focus[3]
  local wP = 1 - smoothstep(0.35, 0.95, zoom)
  local wC = smoothstep(0.95, 1.35, zoom)
  local wM = max(0, 1 - wP - wC)
  -- what is around (4 times a second)
  SAMPLE.t = SAMPLE.t - dt
  if SAMPLE.t <= 0 then
    SAMPLE.t = 0.25
    local s, x, y = focus_village(V, fx, fy, fz)
    SAMPLE.s, SAMPLE.x, SAMPLE.y = s, x, y
    local F = W.F
    -- the planet cell under the focus (the nearest vertex of the grid)
    local g = W.grid
    local cell = SAMPLE.cell or 1
    local bdot = -2
    if s then cell = s.cell_of[mapm.tile_of(s, x, y)] or s.cell
    else
      -- (a short greedy walk over the grid's neighbours toward the focus)
      for _ = 1, 64 do
        local improved = false
        local best = cell
        bdot = g.X[cell] * fx + g.Y[cell] * fy + g.Z[cell] * fz
        for k = 1, g.nbr_count[cell] do
          local j = g.nbr[(cell - 1) * 6 + k]
          local d = g.X[j] * fx + g.Y[j] * fy + g.Z[j] * fz
          if d > bdot then bdot, best, improved = d, j, true end
        end
        cell = best
        if not improved then break end
      end
    end
    SAMPLE.cell = cell
    local lh = clock.local_hour(now, W.grid.lon[cell])
    local sr, ss = (s and s.sunrise) or 6, (s and s.sunset) or 18
    SAMPLE.night = lh < sr - 0.3 or lh > ss + 0.4
    SAMPLE.ocean = W.geo.ocean[cell] == 1
    SAMPLE.forest = F.forest[cell] or 0
    SAMPLE.temp = F.temp[cell] or 10
    SAMPLE.precip = F.precip[cell] or 0
    SAMPLE.fire = F.fire and F.fire[cell] or 0
    SAMPLE.rain = s and (s.rain_now or 0) or (SAMPLE.precip > 0.06 and 1 or 0)
    SAMPLE.snow = s and (s.snow or 0) or 0
    if s then
      local R = 1.1
      SAMPLE.dr, SAMPLE.ds, SAMPLE.df = waters(s, x, y, R)
      -- people out of doors and awake near; anything burning near; the herds near
      local act = require("sim.agents.act")
      local n = 0
      for _, id in ipairs(s.members) do
        local p = V.people[id]
        if p and p.alive and not p.at and p.inside == 0 and p.act ~= "sleep" then
          local px, py = act.pos(p, now)
          if (px - x) * (px - x) + (py - y) * (py - y) < 0.49 then n = n + 1 end
        end
      end
      SAMPLE.people = n
      local heat = 0
      for _, f in ipairs(s.burn or {}) do
        if not f.out and f.heat > 0 then
          local d = sqrt((f.x - x) * (f.x - x) + (f.y - y) * (f.y - y))
          heat = heat + f.heat * max(0, 1 - d / 1.2)
        end
      end
      SAMPLE.heat = min(1.5, heat)
      local stock = require("sim.agents.stock")
      local beasts = 0
      for _, kind in ipairs({ "flock", "cattle", "swine" }) do
        local H = s.herds and s.herds[kind]
        for _, a in ipairs(H and H.animals or {}) do
          if a.alive then
            local ax, ay = stock.pos(a, now)
            if (ax - x) * (ax - x) + (ay - y) * (ay - y) < 0.36 then beasts = beasts + 1 end
          end
        end
      end
      SAMPLE.beasts = beasts
      -- standing trees near (a drop while someone near is felling: a tree came down)
      local T = s.trees
      local nt = 0
      for k = 1, (T and T.n or 0) do
        if T.st[k] == 1 then
          local dx, dy = T.x[k] - x, T.y[k] - y
          if dx * dx + dy * dy < 0.81 then nt = nt + 1 end
        end
      end
      SAMPLE.felled = (SAMPLE.trees_s == s and SAMPLE.trees_n and nt < SAMPLE.trees_n) and true or false
      SAMPLE.trees_s, SAMPLE.trees_n = s, nt
    else
      SAMPLE.dr, SAMPLE.ds, SAMPLE.df, SAMPLE.people, SAMPLE.heat, SAMPLE.beasts = 9, SAMPLE.ocean and 0 or 9, 9, 0, 0, 0
    end
    -- a mountain stirring near the focus (its pressure, an eruption lately)
    -- (only a list the world already made: asking volcano.list would make the sites - sim state - from
    -- the view, at a time of the view's choosing)
    local vol = 0
    local vl = V.volc and V.volc.list
    if vl then
      for _, v in ipairs(vl) do
        local d = g.X[v.cell] * fx + g.Y[v.cell] * fy + g.Z[v.cell] * fz
        if d > 0.97 then
          local p = (v.pressure or 0) / max(1e-6, v.thr or 1)
          local hot = v.erupted and (now - v.erupted) < 72 and 1 or 0
          vol = max(vol, min(1, max(hot, p - 0.7) * (d - 0.97) / 0.03))
        end
      end
    end
    SAMPLE.volcano = vol
  end
  local S = SAMPLE
  local s = S.s
  local storm = S.rain == 2 and 1 or 0
  local rain = S.rain == 1 and 1 or 0
  local cold = (S.snow > 0.02 or S.temp < -2) and 1 or 0
  local function near(d, r) return max(0, 1 - d / r) end
  -- the beds' targets
  bed.amb_wind_high.t = 0.55 * wP + 0.12 * wM + 0.25 * storm * (wP + wM)
  bed.amb_wind_winter.t = cold * (0.45 * wM + 0.55 * wC)
  bed.amb_forest_day.t = (S.night and 0 or 1) * min(1, S.forest * 1.6) * (0.25 * wM + 0.45 * wC) * (1 - 0.6 * rain - storm)
  bed.amb_forest_night.t = (S.night and 1 or 0) * min(1, 0.3 + S.forest) * (0.2 * wM + 0.45 * wC) * (1 - 0.6 * rain - storm) * (1 - cold)
  bed.amb_coast.t = (S.ocean and 0.35 * wM or 0) + 0.55 * wC * near(S.ds, 1.0)
  bed.amb_river.t = 0.55 * wC * near(S.dr, 0.7) * (s and s.fl and 0.3 or 1)
  bed.amb_river_flood.t = (s and s.fl) and (0.7 * wC * max(near(S.df, 0.9), near(S.dr, 0.9)) + 0.15 * wM) or 0
  bed.amb_rain.t = rain * (0.25 * wM + 0.55 * wC)
  bed.amb_storm.t = storm * (0.3 * wP + 0.4 * wM + 0.6 * wC)
  bed.amb_village_day.t = (S.night and 0 or 1) * wC * min(1, S.people / 8) * 0.55
  bed.amb_village_night.t = (S.night and 1 or 0) * wC * (s and 0.35 or 0) * (1 - storm)
  bed.amb_fire.t = wC * min(1, S.heat) * 0.9 + wM * min(1, S.heat) * 0.3
  bed.amb_wildfire.t = min(1, S.fire * 3) * (0.35 * wP + 0.5 * wM + 0.3 * wC)
  bed.amb_volcano.t = S.volcano * (0.4 * wP + 0.6 * wM + 0.4 * wC)
  bed.amb_herd.t = wC * min(1, S.beasts / 12) * 0.45 * (S.night and 0.4 or 1)
  bed.amb_sea_open.t = 0
  ease_beds(dt)
  -- one-shots: only close in, never while skipping; fewer at the faster watch speed
  if skipping or speed_level == 0 or wC < 0.25 or not s then return end
  local dens = speed_level >= 3 and 0.5 or 1
  local x, y = S.x, S.y
  local R = 1.25 - 0.35 * smoothstep(1.2, 1.75, zoom)
  local act = require("sim.agents.act")
  local function gainpan(px, py, base)
    local d = sqrt((px - x) * (px - x) + (py - y) * (py - y))
    if d > R then return 0, 0 end
    local gn = base * wC * (1 - d / R) ^ 1.6
    local pan = 0
    if project then
      local ux, uy, uz = mapm.unit(s, px, py)
      pan = project(ux, uy, uz)
    end
    return gn, pan
  end
  -- the people's work and alarms
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive and not p.at and p.inside == 0 then
      local snd = ACT_SND[p.act]
      local alarm = p.alarm and ALARM[p.alarm.kind] and now - p.alarm.t < 0.15
      if snd or alarm then
        local px, py = act.pos(p, now)
        if alarm then
          local gn, pan = gainpan(px, py, 0.7)
          if gn > 0 and now_s - (pgap[-p.id] or -99) > 6 and rnd() < 0.5 * dens then
            if shot("shout", gn, pan, 1.2, now_s) then pgap[-p.id] = now_s end
          end
        end
        if snd then
          local gap = (ACT_GAP[snd] or 2) * (0.85 + 0.3 * rnd()) / dens
          if now_s - (pgap[p.id] or -99) > gap then
            local gn, pan = gainpan(px, py, 0.8)
            if gn > 0 then shot(snd, gn, pan, 0.18, now_s); pgap[p.id] = now_s end
          end
        end
      end
    end
  end
  -- footsteps of someone walking very close (snow crunches)
  if zoom > 1.45 and rnd() < 0.08 * dens then
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and not p.at and p.act == "walk" then
        local px, py = act.pos(p, now)
        local gn, pan = gainpan(px, py, 0.35)
        if gn > 0.15 then shot((s.snow or 0) > 0.03 and "steps_snow" or "steps_grass", gn, pan, 1.5, now_s); break end
      end
    end
  end
  -- the beasts near: one calls now and then
  if S.beasts > 0 and rnd() < min(0.25, S.beasts * 0.006) * dt * 30 * dens then
    local stock = require("sim.agents.stock")
    local kinds = { "flock", "cattle", "swine" }
    local H = s.herds and s.herds[kinds[1 + floor(rnd() * 3)]]
    local L = H and H.animals or {}
    if #L > 0 then
      local a = L[1 + floor(rnd() * #L)]
      if a and a.alive then
        local ax, ay = stock.pos(a, now)
        local gn, pan = gainpan(ax, ay, 0.6)
        local id = SPECIES_SND[a.sp]
        if id and gn > 0.05 then shot(id, gn, pan, 1.6, now_s) end
      end
    end
  end
  -- dogs that barked
  for k, d in ipairs(s.dogs or {}) do
    if d.alive and d.barked and d.barked ~= seen_bark[d] then
      seen_bark[d] = d.barked
      if now - d.barked < 0.1 then
        local gn, pan = gainpan(d.tx or x, d.ty or y, 0.75)
        shot("dog_bark", max(gn, 0.12 * wC), pan, 0.6, now_s)
      end
    end
  end
  -- wolves on the map at night
  if S.night and rnd() < 0.004 * dt * 60 * dens and s.wildlife then
    for _, w in ipairs(s.wildlife.animals or {}) do
      if w.alive and w.pred and not w.gone then
        local gn, pan = gainpan(w.tx or x, w.ty or y, 0.6)
        shot("wolf_howl", max(gn, 0.18 * wC), pan, 25, now_s)
        break
      end
    end
  end
  -- boats rowed near
  for _, b in ipairs(s.boats or {}) do
    if (b.crew or 0) ~= 0 and not b.lost and b.x then
      local gn, pan = gainpan(b.x, b.y, 0.5)
      if gn > 0.08 and rnd() < 0.05 * dens then shot("oars", gn, pan, 2.5, now_s) end
    end
  end
  -- deeds of this map just now: thunder, a quake, a house falling, a tree felled
  local D = V.deeds or {}
  if not deed_cur then deed_cur = #D end
  for i = deed_cur + 1, #D do
    local d = D[i]
    if d and d.at == s.id then
      if d.kind == "strike" then
        local gn, pan = gainpan(d.x or x, d.y or y, 1.0)
        shot("thunder", max(gn, 0.45), pan, 0.5, now_s)
      elseif d.kind == "quake" then
        shot("quake", 0.85, 0, 0.5, now_s)
      elseif d.kind == "fell" then
        local gn, pan = gainpan(d.x or x, d.y or y, 0.9)
        shot("house_fall", max(gn, 0.3), pan, 0.5, now_s)
      end
    elseif d and d.kind == "strike" then
      -- (thunder from another village rolls in faint at the planet's distance)
      shot("thunder", 0.12 * (wP + wM), 0, 2, now_s)
    end
  end
  deed_cur = #D
  -- a tree coming down near
  if S.felled then
    S.felled = false
    for _, id in ipairs(s.members) do
      local p = V.people[id]
      if p and p.alive and not p.at and p.act == "chop" then
        local px, py = act.pos(p, now)
        local gn, pan = gainpan(px, py, 0.8)
        if gn > 0.05 then shot("tree_fall", gn, pan, 4, now_s); break end
      end
    end
  end
end

return M
