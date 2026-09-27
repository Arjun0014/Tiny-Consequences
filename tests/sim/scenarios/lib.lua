-- Scenario runner (07_TESTING.md §3). A scenario file returns a pure-data spec:
--   { name, seeds = "standard", pass_ratio = { quick, full }, run_years, chapter, actions, expect }
-- actions: { year, hook = <test hook name> | helper = "cull_all", target = <selector>, ...params }
-- expect:
--   { metric = "<field>@target", compare = "ratio_to_baseline", op = ">=" | "<=", value, within_years }
--   { chain_from_action = i, min_links = n, must_include = { kinds in chain order } }
--   { descendant_kind = "<kind>", from_action = i }       -- some derived event of that kind exists
-- Each seed runs twice from the same cached post-spin-up world: once with the actions, once
-- without (baseline), in lockstep, sampling metrics monthly. ratio_to_baseline passes if the
-- ratio satisfies op at any sample within `within_years` after the action.
local world = require("sim.world")
local events = require("sim.causality.events")

local L = {}

L.SEEDS = {
  quick = { 1, 2, 3 },
  full = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
}

-- Worlds after worldgen + spin-up, cached per seed (tests/sim/support/worlds.lua).
local worlds = require("tests.sim.support.worlds")
L.fresh_world = worlds.fresh

-- Target selectors -------------------------------------------------------------------------------
-- Returns { cell = representative cell, cells = list, label = text } or nil.
local function ocean_ring(W, c, radius)
  local g = W.grid
  local ocean = W.geo.ocean
  local list = {}
  local dist = { [c] = 0 }
  local q = { c }
  local head = 1
  while head <= #q do
    local x = q[head]; head = head + 1
    list[#list + 1] = x
    if dist[x] < radius then
      local b = x * 6 - 6
      for k = 1, g.nbr_count[x] do
        local j = g.nbr[b + k]
        if dist[j] == nil and ocean[j] == 1 then dist[j] = dist[x] + 1; q[#q + 1] = j end
      end
    end
  end
  table.sort(list)
  return list
end
L.ocean_ring = ocean_ring

function L.select(W, spec)
  local geo = W.geo
  local kind, rest = spec:match("^([%w_]+):(.+)$")
  if kind == "continent" then
    local id = rest == "largest" and 1 or tonumber(rest)
    local cells = {}
    for c = 1, W.n do if geo.continent[c] == id then cells[#cells + 1] = c end end
    return { cell = cells[1], cells = cells, label = "continent " .. id, continent = id }
  elseif kind == "cell" then
    local c = tonumber(rest)
    return { cell = c, cells = { c }, label = "cell " .. c }
  elseif kind == "island" and rest == "no_predators" then
    -- a small island (<= 40 level-4-equivalent... cells of this grid scaled to area) with
    -- vegetation and no wolves; smallest first
    local order = {}
    for i = 1, #geo.components do order[#order + 1] = i end
    table.sort(order, function(a, b) return geo.components[a].size < geo.components[b].size end)
    local max_cells = math.max(6, math.floor(60 / (W.sscale * W.sscale)))
    for _, i in ipairs(order) do
      local comp = geo.components[i]
      if comp.size <= max_cells then
        local p = 0
        for k = 1, #comp.cells do p = p + W.F.predators[comp.cells[k]] end
        local veg = 0
        for k = 1, #comp.cells do veg = veg + W.F.grass[comp.cells[k]] + W.F.forest[comp.cells[k]] end
        if p == 0 and veg / #comp.cells > 0.3 then
          local cells = {}
          for k = 1, #comp.cells do cells[k] = comp.cells[k] end
          table.sort(cells)
          return { cell = cells[1], cells = cells, label = "island " .. i }
        end
      end
    end
    return nil
  elseif kind == "village" and (rest == "inland" or rest == "fishing") then
    -- "inland": the biggest living village without fishing waters; "fishing": the biggest with
    local best, bp = nil, -1
    for _, v in ipairs(W.ent.villages or {}) do
      if v.alive then
        local ok = (rest == "inland" and #v.fishing == 0) or (rest == "fishing" and #v.fishing > 0 and (v.fish_jobs or 0) > 0)
        if ok and v.pop > bp then best, bp = v, v.pop end
      end
    end
    if not best then return nil end
    local cells = {}
    for i = 1, #best.farms do cells[i] = best.farms[i] end
    if #cells == 0 then cells[1] = best.cell end
    return { cell = best.cell, cells = cells, village = best.id, label = "village " .. best.name }
  elseif kind == "village" and rest == "farming" then
    -- the biggest village with several farms and another village within blight's trade reach
    local g = W.grid
    local best, bp = nil, -1
    local vs = W.ent.villages or {}
    for _, v in ipairs(vs) do
      if v.alive and #v.farms >= 3 then
        local near = 0
        for _, o in ipairs(vs) do
          if o.alive and o.id ~= v.id and #o.farms > 0 then
            local d = g.X[o.cell] * g.X[v.cell] + g.Y[o.cell] * g.Y[v.cell] + g.Z[o.cell] * g.Z[v.cell]
            if math.acos(math.max(-1, math.min(1, d))) / 0.07553 <= require("sim.config").POWERS.blight_trade_steps then near = near + 1 end
          end
        end
        if near > 0 and v.pop > bp then best, bp = v, v.pop end
      end
    end
    if not best then return nil end
    return { cell = best.farms[1], cells = { best.farms[1] }, village = best.id, label = "fields of " .. best.name }
  elseif kind == "near" and rest == "village" then
    -- a land cell next to the biggest village (meteor target)
    local best, bp = nil, -1
    for _, v in ipairs(W.ent.villages or {}) do
      if v.alive and v.pop > bp then best, bp = v, v.pop end
    end
    if not best then return nil end
    local g = W.grid
    local b = best.cell * 6 - 6
    local c = best.cell
    for k = 1, g.nbr_count[best.cell] do
      local j = g.nbr[b + k]
      if geo.ocean[j] == 0 and W.F.land_use[j] >= 0 then c = j; break end
    end
    return { cell = c, cells = { c }, village = best.id, label = "near " .. best.name }
  elseif kind == "forest" and rest == "dry" then
    -- a dry spot in thick forest: forest around it (fuel for the fire to spread) and dry soil
    local g = W.grid
    local best, bs = nil, -1e9
    for c = 1, W.n do
      if geo.ocean[c] == 0 and W.F.forest[c] >= 0.35 and W.F.ice[c] < 0.2 and W.F.land_use[c] == 0 then
        local fsum, b = W.F.forest[c], c * 6 - 6
        for k = 1, g.nbr_count[c] do fsum = fsum + W.F.forest[g.nbr[b + k]] end
        local score = fsum - 3 * W.F.soil_water[c]
        if score > bs then best, bs = c, score end
      end
    end
    if not best then return nil end
    return { cell = best, cells = { best }, label = "dry forest at " .. best }
  elseif kind == "ocean_upwind" and rest == "land" then
    -- a coastal ocean cell that is upwind of the most land (this season's wind table); the
    -- metric cells are the land downwind within 3 steps
    local g = W.grid
    local season = math.floor((W.tick % 48) / 12)
    local wt = W.wind[season + 1]
    local best, bs, bcells = nil, -1, nil
    for c = 1, W.n do
      if geo.ocean[c] == 1 and W.F.ice[c] == 0 and geo.coast_dist[c] <= 1 then
        -- land cells that list c as an upwind neighbour, then 2 more steps downwind of them
        local down = {}
        local b = c * 6 - 6
        for k = 1, g.nbr_count[c] do
          local j = g.nbr[b + k]
          if geo.ocean[j] == 0 then
            local bj = j * 6 - 6
            for q = 1, g.nbr_count[j] do
              if g.nbr[bj + q] == c and wt.w[bj + q] > 0.08 then down[#down + 1] = j end
            end
          end
        end
        if #down > bs then best, bs, bcells = c, #down, down end
      end
    end
    if not best then return nil end
    local cells, seen = {}, {}
    local frontier = bcells
    for step = 1, 2 do
      local nxt = {}
      for _, x in ipairs(frontier) do
        if not seen[x] and geo.ocean[x] == 0 then
          seen[x] = true
          cells[#cells + 1] = x
          local b = x * 6 - 6
          for k = 1, g.nbr_count[x] do nxt[#nxt + 1] = g.nbr[b + k] end
        end
      end
      frontier = nxt
    end
    table.sort(cells)
    return { cell = best, cells = cells, label = "land downwind of " .. best }
  elseif kind == "tanker" and rest == "tutorial" then
    -- the tanker nearest a fishing village (tools/tutorial_scout.lua), struck where it is
    local t, _, v = require("sim.tutorial").pick(W)
    if not t then return nil end
    return { cell = t.cell, cells = { t.cell }, ship = t.id, village = v.id, label = "tanker " .. t.name }
  elseif kind == "ocean" and rest == "cold_coast" then
    -- the coldest ice-free coastal sea (a Chill can freeze it); target = the sea around it
    local best, bt = nil, 1e9
    for c = 1, W.n do
      if geo.ocean[c] == 1 and geo.coast_dist[c] <= 1 and W.F.ice[c] < 0.05 and W.F.sst[c] > 0 then
        if W.F.sst[c] < bt then best, bt = c, W.F.sst[c] end
      end
    end
    if not best then return nil end
    return { cell = best, cells = ocean_ring(W, best, 1), label = "cold coast at " .. best }
  elseif kind == "ocean_near" and rest == "village:fishing" and (W.ent.villages and #W.ent.villages > 0) then
    local vt = L.select(W, "village:fishing")
    if vt then
      local v = W.ent.villages[vt.village]
      local g = W.grid
      local best, bs = nil, -1
      for _, c in ipairs(v.fishing) do
        local b = c * 6 - 6
        local no = 0
        for k = 1, g.nbr_count[c] do if geo.ocean[g.nbr[b + k]] == 1 then no = no + 1 end end
        if no > bs then best, bs = c, no end
      end
      return { cell = best, cells = ocean_ring(W, best, 2), village = v.id, label = "sea of " .. v.name }
    end
    return L.select(W, "ocean_near:coast:largest")
  elseif kind == "ocean_near" then
    -- "coast:largest" (and, until villages exist, "village:fishing"): a fishing-ground-like
    -- coastal ocean cell next to the largest continent — ice-free, |lat| ≤ 55°, open water, good
    -- fish; target cells = ocean within 2 steps ("the nearby sea").
    local best, bs = nil, -1
    local g = W.grid
    for c = 1, W.n do
      if geo.ocean[c] == 1 and W.F.ice[c] == 0 and math.abs(g.lat[c]) <= 0.96 then
        local b = c * 6 - 6
        local touches, no = false, 0
        for k = 1, g.nbr_count[c] do
          local j = g.nbr[b + k]
          if geo.continent[j] == 1 then touches = true end
          if geo.ocean[j] == 1 then no = no + 1 end
        end
        local score = no + W.F.fish[c]
        if touches and score > bs then best, bs = c, score end
      end
    end
    if not best then return nil end
    return { cell = best, cells = ocean_ring(W, best, 2), label = "sea near cell " .. best }
  end
  error("unknown target selector " .. tostring(spec))
end

-- Metrics -----------------------------------------------------------------------------------------
local function metric_value(W, name, target)
  -- pop@community: the village plus the villages its people founded after the action (a
  -- daughter village moves people without losing them, so budding doesn't count as loss)
  if name == "pop@community" then
    local vs = W.ent.villages
    local v = vs[target.village]
    if not v then return 0 end
    local total = v.pop
    local member = { [v.id] = true }
    for i = v.id + 1, #vs do
      local u = vs[i]
      if u.from and member[u.from] and u.founded >= (target.tick or 0) then
        member[u.id] = true
        if u.alive then total = total + u.pop end
      end
    end
    return total
  end
  if name == "aerosol@global" then return W.G.aerosol or 0 end
  if name == "blight_villages" then
    local S = W.S.powers
    if not S then return 0 end
    local seen, n = {}, 0
    for _, b in ipairs(S.blight) do
      local vid = W.F.land_use[b.cell]
      if vid > 0 and not seen[vid] then seen[vid] = true; n = n + 1 end
    end
    return n
  end
  if name == "veg@target" then
    local s, cells = 0, target.cells
    for i = 1, #cells do s = s + W.F.grass[cells[i]] + W.F.forest[cells[i]] end
    return s / #cells
  end
  local vf = name:match("^([%w_]+)@village$")
  if vf then
    local v = W.ent.villages[target.village]
    if not v then return 0 end
    if vf == "pop" then return v.pop end
    return v[vf] or 0
  end
  local field = name:match("^([%w_]+)@target$")
  local arr = W.F[field]
  if not arr then error("unknown metric " .. name) end
  local s = 0
  local cells = target.cells
  for i = 1, #cells do s = s + arr[cells[i]] end
  return s / #cells
end

-- Actions -------------------------------------------------------------------------------------------
-- Power actions (Stage 2.1): queue `power` commands like a player would. Helpers:
--   cull_all        Cull (radius 2) on every cluster of the species in the target cells
--   lightning_farms Lightning on every farm cell of the target village
-- Returns the root event kind to collect (power_<kind>).
local ROOT_KIND = { weather = { rain = "power_rain", drought = "power_drought" },
  temperature = { heat = "power_heat", chill = "power_chill" } }
local function apply_power(W, a, target)
  local pw = a.power
  local base = { power = pw, mode = a.mode, species = a.species, radius = a.radius, ship = target.ship }
  local function cast(c)
    local q = {}
    for k, v in pairs(base) do q[k] = v end
    q.cell = c
    world.command(W, "power", q)
  end
  if a.helper == "cull_all" then
    local fld = W.F[a.species or "predators"]
    local covered = {}
    local area = require("sim.powers").area
    for _, c in ipairs(target.cells) do
      if not covered[c] and fld[c] > 1e-4 then
        cast(c)
        for _, x in ipairs(area(W, c, 2)) do covered[x] = true end
      end
    end
  elseif a.helper == "lightning_farms" then
    local v = W.ent.villages[target.village]
    for _, c in ipairs(v and v.farms or target.cells) do cast(c) end
  else
    cast(target.cell)
  end
  local rk = ROOT_KIND[pw]
  if rk then return rk[a.mode or (pw == "weather" and "rain" or "heat")] end
  return "power_" .. pw
end

local function apply_action(W, a, target)
  if a.power then return apply_power(W, a, target) end
  local p = {}
  for k, v in pairs(a) do
    if k ~= "year" and k ~= "hook" and k ~= "helper" and k ~= "target" and k ~= "species"
      and k ~= "local_calendar" then p[k] = v end
  end
  local hook = a.hook
  if a.helper == "cull_all" then
    -- Chapter 2: Cull (radius 2) on every predator cluster; until then remove them directly.
    hook = "remove_predators"
    p.fraction = p.fraction or 1
  end
  if hook == "remove_predators" or hook == "ignite" or hook == "add_grazers" or hook == "kill_fish"
    or hook == "heat" then
    p.cells = target.cells
  elseif hook == "oil" then
    p.cell = target.cell
  elseif hook == "burn_farms" then
    p.village = target.village
  elseif hook == "sink_tanker" then
    p.tanker = target.tanker
  end
  world.test_action(W, hook, p)
  return "test_" .. hook
end

-- Chains --------------------------------------------------------------------------------------------
local function chain_kinds(W, id)
  local ch = events.chain(W, id)
  local ks = {}
  for i = 1, #ch do ks[i] = ch[i].kind end
  return ks
end

local function includes_in_order(ks, must)
  local j = 1
  for i = 1, #ks do
    if must[j] and ks[i] == must[j] then j = j + 1 end
  end
  return j > #must
end

local function eval_chain(W, root, e)
  local desc = events.descendants(W, root)
  local best_len, best = 0, nil
  local ok = false
  local limit = e.within_years and (W.E.rec[root].tick + e.within_years * 48) or math.huge
  for i = 1, #desc do
    local ks = chain_kinds(W, desc[i].id)
    if desc[i].tick > limit then ks = {} end
    local good = #ks >= (e.min_links or 1) and includes_in_order(ks, e.must_include or {})
    if good and (not ok or #ks > best_len) then ok, best_len, best = true, #ks, ks
    elseif not ok and #ks > best_len then best_len, best = #ks, ks end
  end
  return ok, best and table.concat(best, ">") or "(no derived events)"
end

-- Run one seed. Returns pass(bool), detail(string), per-expectation results.
local baseline_seed
function L.run_seed(spec, seed, opts)
  if spec.baseline_only then return baseline_seed(spec, seed) end
  opts = opts or {}
  local Wa = L.fresh_world(seed)
  local Wb = L.fresh_world(seed)
  local ticks = spec.run_years * 48
  local acts = spec.actions or {}
  local roots, targets, act_tick = {}, {}, {}
  local roots_all = {}
  local series = {}
  for i, e in ipairs(spec.expect) do
    if e.metric then series[i] = {} end
  end
  local pending_root = {}
  local due = {}
  for t = 0, ticks - 1 do
    for ai, a in ipairs(acts) do
      if t == math.floor((a.year or 0) * 48) then
        local same = a.target:match("^same:(%d+)$")
        local tg = same and targets[tonumber(same)] or L.select(Wa, a.target)
        if not tg then return false, "target " .. a.target .. " not found", {} end
        targets[ai] = tg
        tg.tick = Wa.tick
        due[ai] = t
        -- local_calendar: `year` is in the target village's own farming calendar (southern
        -- villages farm half a year later, sim/systems/villages.lua season_shift)
        if a.local_calendar and tg.village then
          due[ai] = t + require("sim.systems.villages").season_shift(Wa, Wa.ent.villages[tg.village].cell)
        end
      end
      if due[ai] == t and not act_tick[ai] then
        act_tick[ai] = t
        pending_root[ai] = apply_action(Wa, a, targets[ai])
      end
    end
    world.tick(Wa)
    world.tick(Wb)
    for ai, rkind in pairs(pending_root) do
      -- find the root event(s) this action created (a helper can cast several powers)
      local E = Wa.E
      local all = {}
      for k = #E.order, 1, -1 do
        local r = E.rec[E.order[k]]
        if r.tick < act_tick[ai] then break end
        if r.kind == rkind and r.parent == 0 then all[#all + 1] = r.id end
      end
      table.sort(all)
      roots[ai] = all[1]
      roots_all[ai] = all
      pending_root[ai] = nil
    end
    if t % 4 == 3 then
      for i, e in ipairs(spec.expect) do
        if e.metric then
          local ai = e.action or 1
          local tg = targets[ai]
          if tg and act_tick[ai] and t >= act_tick[ai] then
            local va, vb = metric_value(Wa, e.metric, tg), metric_value(Wb, e.metric, tg)
            local s = series[i]
            s[#s + 1] = { t = t - act_tick[ai], a = va, b = vb }
          end
        end
      end
    end
  end
  -- evaluate
  local all = true
  local parts = {}
  local res = {}
  for i, e in ipairs(spec.expect) do
    local ok, txt = false, ""
    if e.metric and e.mean then
      -- ratio of the means over the window (noisy weekly fields such as precipitation)
      local win = (e.within_years or spec.run_years) * 48
      local sa, sb = 0, 0
      for _, smp in ipairs(series[i]) do
        if smp.t <= win then sa, sb = sa + smp.a, sb + smp.b end
      end
      local r = sb > 0 and sa / sb or 0
      if os.getenv("TC_DEBUG") then print("MEAN", e.metric, #series[i], sa, sb) end
      ok = (e.op == ">=" and r >= e.value) or (e.op == "<=" and r <= e.value)
      txt = string.format("%s mean x%.2f over %.1fy%s", e.metric, r, win / 48, ok and "" or string.format(" (need %s %.2f)", e.op, e.value))
    elseif e.metric then
      local win = (e.within_years or spec.run_years) * 48
      local absolute = e.compare == "absolute"
      local best, bt = nil, 0
      for _, smp in ipairs(series[i]) do
        if smp.t <= win and (absolute or smp.b > 0) then
          local r = absolute and smp.a or smp.a / smp.b
          local hit = (e.op == ">=" and r >= e.value) or (e.op == "<=" and r <= e.value)
          if best == nil or (e.op == ">=" and r > best) or (e.op == "<=" and r < best) then best, bt = r, smp.t end
          if hit and not ok then ok = true; txt = string.format("%s x%.2f@%.1fy", e.metric, r, smp.t / 48) end
        end
      end
      if ok then txt = txt .. string.format(" (best x%.2f)", best) end
      if not ok then txt = string.format("%s best x%.2f@%.1fy (need %s %.2f)", e.metric, best or 0, bt / 48, e.op, e.value) end
    elseif e.chain_from_action then
      -- the best chain from any of the action's roots (helpers cast several powers)
      local list = roots_all[e.chain_from_action] or {}
      if #list > 0 then
        local best_txt, best_len = nil, -1
        for _, root in ipairs(list) do
          local good, ch = eval_chain(Wa, root, e)
          local len = select(2, ch:gsub(">", ">"))
          if good and not ok then ok = true; best_txt = ch
          elseif not ok and len > best_len then best_len, best_txt = len, ch end
          if ok then break end
        end
        txt = "chain " .. best_txt
      else
        txt = "no root event"
      end
    elseif e.descendant_kind then
      for _, root in ipairs(roots_all[e.from_action or 1] or {}) do
        for _, d in ipairs(events.descendants(Wa, root)) do
          if d.kind == e.descendant_kind then ok = true; break end
        end
        if ok then break end
      end
      txt = e.descendant_kind .. (ok and " yes" or " MISSING")
    end
    res[i] = { ok = ok, text = txt }
    parts[#parts + 1] = (ok and "" or "!") .. txt
    if not ok then all = false end
  end
  if opts.dump then
    local report = require("tools.report")
    local dir = "out/sim/scenarios/" .. spec.name
    report.mkdir(dir)
    report.write_events(Wa, dir .. "/seed" .. seed .. "_events.txt")
  end
  L.attribution(spec, seed, Wa, Wb, act_tick)
  return all, table.concat(parts, "; "), res
end

-- P08 (07 §4): headline events of the action world that the baseline world doesn't have (same
-- kind and region within 2 months) are consequences of the player's actions; count how many
-- of them chain back to a player root. Accumulated over every scenario run (L.P08).
L.P08 = { extra = 0, player = 0, misses = {} }
function L.attribution(spec, seed, Wa, Wb, act_tick)
  local t0 = math.huge
  for _, t in pairs(act_tick) do if t < t0 then t0 = t end end
  if t0 == math.huge then return end
  local pool = {}
  for _, id in ipairs(Wb.E.order) do
    local r = Wb.E.rec[id]
    if r and r.headline and r.tick >= t0 then
      local k = r.kind .. "|" .. tostring(r.region)
      pool[k] = pool[k] or {}
      table.insert(pool[k], r.tick)
    end
  end
  for _, id in ipairs(Wa.E.order) do
    local r = Wa.E.rec[id]
    if r and r.headline and r.tick >= t0 then
      local k = r.kind .. "|" .. tostring(r.region)
      local list = pool[k]
      local matched = false
      if list then
        for i = 1, #list do
          if list[i] and math.abs(list[i] - r.tick) <= 8 then list[i] = false; matched = true; break end
        end
      end
      if not matched then
        L.P08.extra = L.P08.extra + 1
        if events.is_player_rooted(Wa, r.id) then L.P08.player = L.P08.player + 1
        elseif #L.P08.misses < 12 then
          local ch = events.chain(Wa, r.id)
          local ks = {}
          for q = 1, #ch do ks[q] = ch[q].kind end
          L.P08.misses[#L.P08.misses + 1] = spec.name .. " s" .. seed .. ": " .. table.concat(ks, ">")
        end
      end
    end
  end
end

-- Baseline-only specs (S01): bands over one untouched run per seed.
baseline_seed = function(spec, seed)
  local B = require("tests.sim.support.baseline")
  local cfgmod = require("sim.config")
  local r = B.run(seed, spec.run_years)
  local W = r.W
  local Y = spec.run_years
  local all, parts = true, {}
  local extra = {}
  for _, e in ipairs(spec.expect) do
    local ok, txt = true, nil
    if e.band then
      local from = e.from_year or 5
      if e.stat == "final" then
        local v = r.yearly[Y][e.band]
        ok = v >= e.min and v <= e.max
        txt = string.format("%s@y%d %.2f", e.band, Y, v)
      elseif e.stat == "all" then
        local lo, hi = math.huge, -math.huge
        for y = from, Y do
          local v = r.yearly[y][e.band]
          if v < lo then lo = v end
          if v > hi then hi = v end
        end
        ok = lo >= e.min and hi <= e.max
        txt = string.format("%s %.4g..%.4g", e.band, lo, hi)
      else
        local s, n = 0, 0
        for y = from, Y do s = s + r.yearly[y][e.band]; n = n + 1 end
        local m = s / n
        ok = m >= e.min and m <= e.max
        txt = string.format("%s %.2f", e.band, m)
      end
      if not ok then txt = txt .. string.format(" (band %.4g..%.4g)", e.min, e.max) end
    elseif e.vary then
      local lo, hi = math.huge, -math.huge
      for y = 5, Y do
        local v = r.yearly[y][e.vary]
        if v < lo then lo = v end
        if v > hi then hi = v end
      end
      ok = hi - lo >= e.min
      txt = string.format("%s varies %.2f", e.vary, hi - lo)
    elseif e.fauna then
      local ext = cfgmod.DETECT.extinct_density
      local cvsum, bad = 0, {}
      for k = 1, #r.conts do
        local gmin = B.minmax(r.g[k], 1)
        local pmin = B.minmax(r.p[k], 1)
        if gmin < ext then bad[#bad + 1] = "grazers" end
        if pmin < ext then bad[#bad + 1] = "predators" end
        cvsum = cvsum + B.cv(r.p[k], 61)
      end
      local cv = cvsum / #r.conts
      local fmin = B.minmax(r.fish, 1)
      local sorted = {}
      for i = 1, #r.fish do sorted[i] = r.fish[i] end
      table.sort(sorted)
      local fmed = sorted[math.floor(#sorted / 2) + 1]
      ok = #bad == 0 and cv >= 0.15 and fmin >= 0.5 * fmed
      txt = string.format("pred CV %.2f%s fish %.2f/%.2f", cv, #bad > 0 and (" EXTINCT " .. table.concat(bad, ",")) or "", fmin, fmed)
    elseif e.events_per_decade then
      local n = 0
      local E = W.E
      for i = 1, #E.order do
        local rec = E.rec[E.order[i]]
        if rec.kind == e.events_per_decade and rec.tick < Y * 48 then n = n + 1 end
      end
      local per = n / (Y / 10)
      ok = per >= e.min
      txt = string.format("%s %.1f/decade", e.events_per_decade, per)
    elseif e.wildfire_every_years then
      local win = e.wildfire_every_years
      local have = {}
      local E = W.E
      for i = 1, #E.order do
        local rec = E.rec[E.order[i]]
        if rec.kind == "wildfire" and rec.tick < Y * 48 then have[math.floor(rec.tick / (win * 48))] = true end
      end
      local miss = 0
      for w = 0, math.floor(Y / win) - 1 do if not have[w] then miss = miss + 1 end end
      ok = miss == 0
      txt = string.format("wildfire in %d/%d windows of %dy", math.floor(Y / win) - miss, math.floor(Y / win), win)
    elseif e.headline_rate then
      local rate = r.yearly[Y].headlines / Y
      ok = rate >= e.headline_rate[1] and rate <= e.headline_rate[2]
      txt = string.format("headlines %.3f/y", rate)
    elseif e.cross_seed == "turnover" then
      local y = r.yearly[Y]
      extra.turnover = (y.villages_founded + y.villages_abandoned) > 0
      txt = string.format("villages +%d -%d", y.villages_founded, y.villages_abandoned)
    end
    if txt then parts[#parts + 1] = (ok and "" or "!") .. txt end
    if not ok then all = false end
  end
  return all, table.concat(parts, "; "), extra
end

-- Wrap a spec as a test module for tests/sim/run.lua.
function L.as_module(spec, args)
  args = args or {}
  return {
    tests = {
      { spec.name, function()
        local profile = args.full and "full" or "quick"
        local seeds = type(spec.seeds) == "table" and spec.seeds or L.SEEDS[profile]
        if args.seeds then
          seeds = {}
          for s = 1, args.seeds do seeds[s] = s end
        end
        local pass_n, lines = 0, {}
        local turnover = 0
        for _, seed in ipairs(seeds) do
          local ok, detail, extra
          if spec.baseline_only then ok, detail, extra = baseline_seed(spec, seed)
          else ok, detail = L.run_seed(spec, seed, { dump = true }) end
          if ok then pass_n = pass_n + 1 end
          if extra and extra.turnover then turnover = turnover + 1 end
          lines[#lines + 1] = string.format("s%d %s: %s", seed, ok and "ok" or "FAIL", detail)
        end
        local need = math.ceil(#seeds * (spec.pass_ratio[profile] or 1) - 1e-9)
        local cross_ok, cross_txt = true, ""
        for _, e in ipairs(spec.expect) do
          if e.cross_seed == "turnover" then
            local want = math.ceil(#seeds * e.min_share - 1e-9)
            cross_ok = turnover >= want
            cross_txt = string.format("; village turnover in %d/%d seeds (need %d)", turnover, #seeds, want)
          end
        end
        local summary = string.format("%d/%d seeds (need %d)%s\n      %s", pass_n, #seeds, need, cross_txt, table.concat(lines, "\n      "))
        if pass_n < need or not cross_ok then error(summary, 0) end
        return summary
      end, chapter = spec.chapter or 1 },
    },
  }
end

return L
