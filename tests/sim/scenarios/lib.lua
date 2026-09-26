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
local worldgen = require("sim.worldgen.init")
local events = require("sim.causality.events")
local save = require("sim.save")

local L = {}

L.SEEDS = {
  quick = { 1, 2, 3 },
  full = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
}

-- Worlds after worldgen + spin-up, cached per seed (deep copies of the persistent state).
local cache = {}

local function snapshot(W)
  local snap = {}
  for _, k in ipairs(world.SAVE_KEYS) do
    if k ~= "cmdlog" and k ~= "pending" then snap[k] = save.deepcopy(W[k]) end
  end
  return snap
end

local function restore(snap)
  -- other tests (the toy world) swap the global system list; always run the real one here
  world.set_systems(require("sim.systems.init"))
  local W = world.new(snap.seed, { level = snap.level })
  for _, k in ipairs(world.SAVE_KEYS) do
    if k == "E" then
      local src = save.deepcopy(snap.E)
      for kk, vv in pairs(src) do W.E[kk] = vv end
      W.E.feed = {}
    elseif k ~= "cmdlog" and k ~= "pending" and snap[k] ~= nil then
      W[k] = save.deepcopy(snap[k])
    end
  end
  W.cmdlog, W.pending = {}, {}
  world.rebuild_derived(W)
  world.init_systems(W)
  return W
end

function L.fresh_world(seed)
  local snap = cache[seed]
  if not snap then
    local W = worldgen.generate(seed)
    snap = snapshot(W)
    cache[seed] = snap
  end
  return restore(snap)
end

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
    for i = 1, #geo.components do
      local comp = geo.components[i]
      if comp.size / W.n < 0.1 then
        local p = 0
        for k = 1, #comp.cells do p = p + W.F.predators[comp.cells[k]] end
        if p == 0 then
          local cells = {}
          for k = 1, #comp.cells do cells[k] = comp.cells[k] end
          table.sort(cells)
          return { cell = cells[1], cells = cells, label = "island " .. i }
        end
      end
    end
    return nil
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
  local field = name:match("^([%w_]+)@target$")
  local arr = W.F[field]
  if not arr then error("unknown metric " .. name) end
  local s = 0
  local cells = target.cells
  for i = 1, #cells do s = s + arr[cells[i]] end
  return s / #cells
end

-- Actions -------------------------------------------------------------------------------------------
local function apply_action(W, a, target)
  local p = {}
  for k, v in pairs(a) do
    if k ~= "year" and k ~= "hook" and k ~= "helper" and k ~= "target" and k ~= "species" then p[k] = v end
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
  end
  world.test_action(W, hook, p)
  return hook
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
  for i = 1, #desc do
    local ks = chain_kinds(W, desc[i].id)
    local good = #ks >= (e.min_links or 1) and includes_in_order(ks, e.must_include or {})
    if good and (not ok or #ks > best_len) then ok, best_len, best = true, #ks, ks
    elseif not ok and #ks > best_len then best_len, best = #ks, ks end
  end
  return ok, best and table.concat(best, ">") or "(no derived events)"
end

-- Run one seed. Returns pass(bool), detail(string), per-expectation results.
function L.run_seed(spec, seed, opts)
  opts = opts or {}
  local Wa = L.fresh_world(seed)
  local Wb = L.fresh_world(seed)
  local ticks = spec.run_years * 48
  local acts = spec.actions or {}
  local roots, targets, act_tick = {}, {}, {}
  local series = {}
  for i, e in ipairs(spec.expect) do
    if e.metric then series[i] = {} end
  end
  local pending_root = {}
  for t = 0, ticks - 1 do
    for ai, a in ipairs(acts) do
      if t == math.floor((a.year or 0) * 48) then
        local tg = L.select(Wa, a.target)
        if not tg then return false, "target " .. a.target .. " not found", {} end
        targets[ai] = tg
        act_tick[ai] = t
        pending_root[ai] = apply_action(Wa, a, tg)
      end
    end
    world.tick(Wa)
    world.tick(Wb)
    for ai, hook in pairs(pending_root) do
      -- find the root event this action created
      local E = Wa.E
      for k = #E.order, 1, -1 do
        local r = E.rec[E.order[k]]
        if r.tick < act_tick[ai] then break end
        if r.kind == "test_" .. hook and r.parent == 0 then roots[ai] = r.id; break end
      end
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
    if e.metric then
      local win = (e.within_years or spec.run_years) * 48
      local best, bt = nil, 0
      for _, smp in ipairs(series[i]) do
        if smp.t <= win and smp.b > 0 then
          local r = smp.a / smp.b
          local hit = (e.op == ">=" and r >= e.value) or (e.op == "<=" and r <= e.value)
          if best == nil or (e.op == ">=" and r > best) or (e.op == "<=" and r < best) then best, bt = r, smp.t end
          if hit and not ok then ok = true; txt = string.format("%s x%.2f@%.1fy", e.metric, r, smp.t / 48) end
        end
      end
      if ok then txt = txt .. string.format(" (best x%.2f)", best) end
      if not ok then txt = string.format("%s best x%.2f@%.1fy (need %s %.2f)", e.metric, best or 0, bt / 48, e.op, e.value) end
    elseif e.chain_from_action then
      local root = roots[e.chain_from_action]
      if root then
        local good, ch = eval_chain(Wa, root, e)
        ok = good
        txt = "chain " .. ch
      else
        txt = "no root event"
      end
    elseif e.descendant_kind then
      local root = roots[e.from_action or 1]
      if root then
        for _, d in ipairs(events.descendants(Wa, root)) do
          if d.kind == e.descendant_kind then ok = true; break end
        end
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
  return all, table.concat(parts, "; "), res
end

-- Wrap a spec as a test module for tests/sim/run.lua.
function L.as_module(spec, args)
  args = args or {}
  return {
    tests = {
      { spec.name, function()
        local profile = args.full and "full" or "quick"
        local seeds = L.SEEDS[profile]
        if args.seeds then
          seeds = {}
          for s = 1, args.seeds do seeds[s] = s end
        end
        local pass_n, lines = 0, {}
        for _, seed in ipairs(seeds) do
          local ok, detail = L.run_seed(spec, seed, { dump = true })
          if ok then pass_n = pass_n + 1 end
          lines[#lines + 1] = string.format("s%d %s: %s", seed, ok and "ok" or "FAIL", detail)
        end
        local need = math.ceil(#seeds * (spec.pass_ratio[profile] or 1) - 1e-9)
        local summary = string.format("%d/%d seeds (need %d)\n      %s", pass_n, #seeds, need, table.concat(lines, "\n      "))
        if pass_n < need then error(summary, 0) end
        return summary
      end, chapter = spec.chapter or 1 },
    },
  }
end

return L
