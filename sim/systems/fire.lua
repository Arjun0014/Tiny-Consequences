-- Fire (02_WORLD_MODEL.md §5.7). Sparse active-fire list; never loops over all cells for
-- spread. Natural lightning ignition (rolled on a rotating quarter of land cells per tick),
-- spread to neighbours weighted by fuel, dryness, intensity and wind, burning consumes grass
-- fast and forest slower, heavy rain extinguishes.
-- Effects written here: grass/forest/crop loss, co2 (ledger), heat_anom, burn_scar, ash
-- fertility. Animals and villagers react by reading `fire` in their own systems.
-- Events: lightning_strike (nature root) -> fire_started -> wildfire (cluster ≥ 10 cells).
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local blame = require("sim.causality.blame")
local events = require("sim.causality.events")
local ledger = require("sim.ledger")

local sys = { name = "fire" }
local sqrt = math.sqrt
-- (S24, D-190) owned(W, c): is planet cell c a village map's (mostly on its map, sim/agents/map.lua owner)? Set by
-- sim/agents/init.lua for v3 worlds. On such a cell the fire is the map's own: the planet's lightning does not
-- strike it (the map has its own storms), its fuel is the map's trees (burnt one by one there), and its intensity is
-- written from the map each hour (blaze.planet) - this system only carries it on to the cells about it.
sys.owned = nil

function sys.init(W)
  local S = W.S.fire
  if not S then
    S = { list = {}, cluster = {}, clusters = {}, next_cluster = 1 }
    W.S.fire = S
  end
end

-- Ignite cell c with cause tag `tag` (0 = none). kind: "lightning" (natural root created
-- here), or nil (tag is the parent event, e.g. a power/test root, village accident, lava).
-- Returns true if ignited.
function sys.ignite(W, c, tag, natural_kind)
  local cfg = cfgmod.FIRE
  local F = W.F
  if W.geo.ocean[c] == 1 or F.fire[c] > 0 then return false end
  local fuel = 0.5 * F.grass[c] + F.forest[c] + 0.5 * F.crop[c]
  if fuel < cfg.min_fuel then return false end
  local S = W.S.fire
  local parent = tag or 0
  local ev = 0
  if W.record_events then
    if natural_kind then parent = events.emit(W, natural_kind, c, 0, 1) end
    ev = events.emit(W, "fire_started", c, parent, 1)
  end
  local id = S.next_cluster
  S.next_cluster = id + 1
  S.clusters[#S.clusters + 1] = { id = id, first = ev, tag = ev ~= 0 and ev or parent, size = 1, burning = 1, wild = 0, start = W.tick }
  S.cluster[c] = id
  F.fire[c] = cfg.start_intensity
  S.list[#S.list + 1] = c
  blame.set(W.C.fire, W.I.fire, c, ev ~= 0 and ev or parent)
  return true
end

-- (S24, D-190) A village map's fire on a cell it owns, at intensity v: on the planet's list (so it carries on to the
-- cells about it), no event of its own (the map's fire has its story and its cause); v = 0 takes it off at the next
-- step. `tag` the cause the planet's blame carries (the event behind what lit it, or 0).
function sys.from_map(W, c, v, tag)
  local F = W.F
  local S = W.S.fire
  if not S then return end
  if v > 0 and F.fire[c] <= 0 then
    local id = S.next_cluster
    S.next_cluster = id + 1
    S.clusters[#S.clusters + 1] = { id = id, first = 0, tag = tag or 0, size = 1, burning = 1, wild = 0, start = W.tick, map = true }
    S.cluster[c] = id
    S.list[#S.list + 1] = c
    blame.set(W.C.fire, W.I.fire, c, tag or 0)
  end
  F.fire[c] = v
end

-- Wildfire threshold in cells: 10 level-4 cells of area, at any SIM_LEVEL (min 3).
function sys.wildfire_cells(W)
  local n = math.ceil(cfgmod.FIRE.wildfire_cells / (W.sscale * W.sscale) - 0.01)
  if n < 3 then n = 3 end
  return n
end

local function find_cluster(S, id)
  local cl = S.clusters
  for i = #cl, 1, -1 do if cl[i].id == id then return cl[i] end end
  return nil
end

function sys.update(W)
  local cfg = cfgmod.FIRE
  local n, g = W.n, W.grid
  local nbr, cnt = g.nbr, g.nbr_count
  local F, C, I = W.F, W.C, W.I
  local fire, grass, forest, crop, soil = F.fire, F.grass, F.forest, F.crop, F.soil_water
  local precip, temp, ha, fert, scar = F.precip, F.temp, F.heat_anom, F.fertility, F.burn_scar
  local cfire, ifire = C.fire, I.fire
  local cg, ig, cfo, ifo, ccrop, icrop = C.grass, I.grass, C.forest, I.forest, C.crop, I.crop
  local cha, iha, cfert, ifert = C.heat_anom, I.heat_anom, C.fertility, I.fertility
  local ocean = W.geo.ocean
  local S = W.S.fire
  local list = S.list
  local seed, tick = W.seed, W.tick
  local fc = cfgmod.HYDRO.field_capacity

  -- 1. natural lightning on a rotating quarter of land cells
  local land = W.geo.land_cells
  local toy = tick % 48
  local season = dmath.sin(2 * math.pi * toy / 48 + cfgmod.TEMP.season_phase)
  local lat = g.lat
  local base_ign = rng.base(seed, rng.S.FIRE_IGNITE, tick)
  local ss = W.sscale
  local k_l = cfg.k_lightning * 4 * ss * ss      -- per unit area: bigger cells get more strikes
  local q = tick % 4
  for i = 1 + q, #land, 4 do
    local c = land[i]
    if fire[c] == 0 and temp[c] > 5 and not (sys.owned and sys.owned(W, c)) then
      local summer = season * (lat[c] >= 0 and 1 or -1)
      if summer > 0 then
        local dry = 1 - soil[c] / fc
        if dry > 0 then
          local fuel = (0.5 * grass[c] + forest[c]) * dry
          local p = k_l * fuel * dry * summer * (0.3 + precip[c] * cfg.storm_precip)
          if p > 0 and rng.fbase(base_ign, c) < p then
            sys.ignite(W, c, 0, "lightning_strike")
          end
        end
      end
    end
  end

  -- 2. spread from cells burning at the start of this tick
  local nburn = #list
  local base_sp = rng.base(seed, rng.S.FIRE_SPREAD, tick)
  local season_idx = math.floor(toy / 12) + 1
  local wind = W.wind[season_idx]
  local wv = wind.w
  local k_spread = cfg.k_spread / ss             -- fronts cover the same distance per week
  for i = 1, nburn do
    local b = list[i]
    local inten = fire[b]
    if inten > 0 then
      local base = b * 6 - 6
      local clid = S.cluster[b]
      for k = 1, cnt[b] do
        local j = nbr[base + k]
        if ocean[j] == 0 and fire[j] == 0 then
          local dry = 1 - soil[j] / fc
          if dry > 0 then
            local fuel = 0.5 * grass[j] + forest[j] + 0.5 * crop[j]
            if fuel >= cfg.min_fuel then
              -- wind alignment: b is upwind of j when j's weight on b is large
              local align = 0
              local jb = j * 6 - 6
              for m = 1, cnt[j] do if nbr[jb + m] == b then align = wv[jb + m] * cfg.wind_boost end end
              local p = k_spread * fuel * dry * inten * (1 + align)
              if rng.fbase(base_sp, b * 8 + k) < p then
                fire[j] = cfg.start_intensity
                list[#list + 1] = j
                S.cluster[j] = clid
                local cl = find_cluster(S, clid)
                local tag = cfire[b]
                if cl then
                  cl.size = cl.size + 1
                  cl.burning = cl.burning + 1
                  if cl.wild == 0 and cl.size >= sys.wildfire_cells(W) and W.record_events then
                    -- magnitude in level-4-equivalent cells, so "≥ 10 cells" means the same at any level
                    cl.wild = events.emit(W, "wildfire", j, cl.first, cl.size * ss * ss)
                    if cl.wild ~= 0 then cl.tag = cl.wild end
                  elseif cl.wild ~= 0 then
                    local r = events.get(W, cl.wild)
                    if r then r.mag = cl.size * ss * ss; events.update_headline(W, r) end
                  end
                  tag = cl.tag
                end
                blame.set(cfire, ifire, j, tag)
              end
            end
          end
        end
      end
    end
  end

  -- 3. burn: consume fuel, emit co2 and heat, extinguish
  local keep = 0
  local co2_k, heat_k = cfg.k_co2, cfg.k_heat
  local grass_c = cfgmod.VEG.grass_c
  local G = W.G
  local owned = sys.owned
  for i = 1, #list do
    local c = list[i]
    local inten = fire[c]
    local tag = cfire[c]
    if owned and owned(W, c) then
      -- (S24) a map's cell: what burns there is the map's (blaze.planet writes it, the trees thinning the planet's
      -- woods one by one as they burn); here only the air it warms, and the cell leaves the list when the map's fire
      -- there is out
      if inten > 0 then
        ha[c] = ha[c] + heat_k * inten
        keep = keep + 1
        list[keep] = c
      else
        local cl = find_cluster(S, S.cluster[c])
        S.cluster[c] = nil
        if cl then cl.burning = cl.burning - 1 end
      end
    else
    local og, of, oc = grass[c], forest[c], crop[c]
    local bg = cfg.burn_grass * inten
    if bg > og then bg = og end
    local bf = (W.v3 and cfg.burn_forest_v3 or cfg.burn_forest) * inten   -- (S25, D-207)
    if bf > of then bf = of end
    grass[c], forest[c] = og - bg, of - bf
    if oc > 0 then crop[c] = 0 end
    blame.step(cg, ig, c, -bg, 0.0005, 96, -bg, tag, 0, 0, 0, 0, 0, 0)
    blame.step(cfo, ifo, c, -bf, 0.0002, 480, -bf, tag, 0, 0, 0, 0, 0, 0)
    if oc > 0 then blame.set(ccrop, icrop, c, tag) end
    -- (grass at the weight its growth absorbed; crops' short cycle is not in the vegetation's
    -- books, so their ash is not counted either)
    local co2 = (bg * grass_c + bf) * co2_k
    G.co2_src = G.co2_src + co2
    local cl = find_cluster(S, S.cluster[c])
    local root = tag ~= 0 and W.E.root[tag]
    local rr = root and W.E.rec[root]
    if rr and rr.actor == "player" then ledger.add(W, "co2", co2, tag)
    else ledger.add(W, "co2", co2, 0, "wildfire") end
    ha[c] = ha[c] + heat_k * inten
    blame.set(cha, iha, c, tag)
    scar[c] = 1
    -- new intensity from remaining fuel; rain puts it out
    local fuel = 0.5 * grass[c] + forest[c] + 0.5 * crop[c]
    local ni = fuel * cfg.intensity_per_fuel
    if ni > 1 then ni = 1 end
    ni = ni * (1 - cfg.rain_extinguish * precip[c])
    if ni < (W.v3 and cfg.min_intensity_v3 or cfg.min_intensity) then
      fire[c] = 0
      fert[c] = fert[c] + cfg.ash_fertility * (1 - fert[c])
      blame.set(cfert, ifert, c, tag)
      S.cluster[c] = nil
      if cl then cl.burning = cl.burning - 1 end
    else
      fire[c] = ni
      keep = keep + 1
      list[keep] = c
    end
    end
  end
  for i = #list, keep + 1, -1 do list[i] = nil end
  -- drop finished clusters
  local cls = S.clusters
  local k2 = 0
  for i = 1, #cls do
    if cls[i].burning > 0 then k2 = k2 + 1; cls[k2] = cls[i] end
  end
  for i = #cls, k2 + 1, -1 do cls[i] = nil end
end

return sys
