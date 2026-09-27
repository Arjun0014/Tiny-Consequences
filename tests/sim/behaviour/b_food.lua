-- B08 famine changes behaviour (rations fall, conceptions drop, mortality rises, households
-- emigrate); B09 losing workers reduces the area sown and the harvest; B10 crops don't grow in the
-- cold and sowing waits for the local spring. Interventions are logged test hooks, so every
-- consequence must chain back to them.
local T = require("tests.sim.lib")
local worlds = require("tests.sim.support.worlds")
local world = require("sim.world")
local cfgmod = require("sim.config")
local U = require("tests.sim.behaviour.util")
local M = {}

-- Run `weeks` planet weeks, summing the village's flow account keys across the yearly rollover.
local function run_acc(W, v, weeks, keys, each_week)
  local sum = {}
  for _, k in ipairs(keys) do sum[k] = 0 end
  local cur, prev = v.acc, {}
  for _, k in ipairs(keys) do prev[k] = cur[k] or 0 end
  for _ = 1, weeks do
    world.tick(W)
    if v.acc ~= cur then
      for _, k in ipairs(keys) do sum[k] = sum[k] + ((v.acc_last or {})[k] or 0) - prev[k]; prev[k] = 0 end
      cur = v.acc
    end
    for _, k in ipairs(keys) do
      local x = cur[k] or 0
      sum[k] = sum[k] + x - prev[k]
      prev[k] = x
    end
    if each_week then each_week() end
  end
  return sum
end

-- The village most exposed to a food shock: the fewest boats (farmers can't fish their way out).
local function farm_village(W)
  local vs = U.villages(W, function(u) return #u.farms >= 3 end)
  table.sort(vs, function(a, b)
    if #a.boats ~= #b.boats then return #a.boats < #b.boats end
    return a.id < b.id
  end)
  return vs[1]
end

local b08_body
function M.b08_famine_changes_behaviour()
  -- the body's and household's own response to hunger: exchange between villages (W7: relief,
  -- barter, brides — relations.lua) would change the village and its store, so it is off here
  local cfg0 = require("sim.config").SOCIETY
  local r0, t0, b0 = cfg0.relief_reach, cfg0.trade_reach, cfg0.bride_prob
  cfg0.relief_reach, cfg0.trade_reach, cfg0.bride_prob = 0, 0, 0
  local ok, err = pcall(b08_body)
  cfg0.relief_reach, cfg0.trade_reach, cfg0.bride_prob = r0, t0, b0
  if not ok then error(err, 0) end
  return err
end

b08_body = function()
  local Wa, Wb = worlds.fresh(1), worlds.fresh(1)
  local va = farm_village(Wa)
  local vb = Wb.ent.villages[va.id]
  world.test_action(Wa, "empty_stores", { village = va.id })
  world.test_action(Wa, "burn_farms", { village = va.id })
  local t0 = Wa.hour
  local members = {}
  for _, id in ipairs(va.members) do members[id] = true end
  local min_ra, min_rb, hungry_a = 1, 1, 0
  local cfg = cfgmod.SOCIETY
  -- conceptions among the married women living in the village (per woman-month)
  local conc = { a = { n = 0, wm = 0 }, b = { n = 0, wm = 0 } }
  local function count(W, v, c)
    if not v.alive then return end
    local P = W.ent.people
    for _, id in ipairs(v.members) do
      local p = P[id]
      local age = (W.hour - p.born) / 8064
      if p.sex == "f" and p.partner ~= 0 and age >= cfg.fecund[1] and age <= cfg.fecund[2] then
        c.wm = c.wm + 1
        local conceived = p.preg_due > 0 and p.preg_due - cfg.preg_days * 24 or -1e9
        if conceived > W.hour - 672 then c.n = c.n + 1 end
      end
    end
  end
  local hz0a, pw0a, hz0b, pw0b, hza, hzb = 0, 0, 0, 0, 0, 0
  for m = 1, 24 do
    world.run_ticks(Wa, 4); world.run_ticks(Wb, 4)
    if va.alive then min_ra = math.min(min_ra, va.ration) end
    min_rb = math.min(min_rb, vb.ration)
    if m >= 2 and m <= 14 then count(Wa, va, conc.a); count(Wb, vb, conc.b) end
    if m == 1 then hz0a, pw0a, hz0b, pw0b = va.acc.hz or 0, va.acc.pw or 0, vb.acc.hz or 0, vb.acc.pw or 0 end
    if m == 7 then
      -- expected deaths per person-week (the summed weekly hazards) over the hungry months
      hza = ((va.acc.hz or 0) - hz0a) / math.max(1, (va.acc.pw or 0) - pw0a)
      hzb = ((vb.acc.hz or 0) - hz0b) / math.max(1, (vb.acc.pw or 0) - pw0b)
    end
  end
  local function outcome(W)
    local births, deaths = 0, 0
    for id, p in pairs(W.ent.people) do
      if members[id] and p.dead and p.dead > t0 and p.died_of ~= "age" then deaths = deaths + 1 end
    end
    return births, deaths
  end
  local ba, da = outcome(Wa)
  local bb, db = outcome(Wb)
  T.ok(min_ra < 0.8, "rations fall (min " .. min_ra .. ")")
  T.ok(min_rb > min_ra, "the control village eats better")
  local cra = conc.a.n / math.max(1, conc.a.wm)
  local crb = conc.b.n / math.max(1, conc.b.wm)
  T.ok(conc.b.n > 0 and cra < crb * 0.5, string.format("conceptions drop: %.3f vs %.3f per woman-month", cra, crb))
  T.ok(hza > hzb * 1.5, string.format("mortality rises: weekly death hazard %.5f vs %.5f", hza, hzb))
  T.ok(da >= db, string.format("deaths %d vs %d", da, db))
  local mig = U.events_of(Wa, "migration", va.id, t0)
  local rooted = 0
  for _, r in ipairs(mig) do
    local k = U.root_kind(Wa, r.id)
    if k == "test_empty_stores" or k == "test_burn_farms" then rooted = rooted + 1 end
  end
  T.ok(rooted >= 1, "households emigrate, and the migration chains back to the lost stores")
  local ra = U.events_of(Wa, "rationing", va.id, t0)
  T.ok(#ra >= 1 and (U.root_kind(Wa, ra[1].id) == "test_empty_stores" or U.root_kind(Wa, ra[1].id) == "test_burn_farms"),
    "rationing chains back to the intervention")
  return string.format("%s: ration min %.2f (control %.2f), conceptions %.3f vs %.3f, deaths %d vs %d, %d migrations",
    va.name, min_ra, min_rb, cra, crb, da, db, #mig)
end

function M.b09_losing_workers_shrinks_the_sown_area_and_harvest()
  local Wa, Wb = worlds.fresh(22), worlds.fresh(22)
  local va = farm_village(Wa)
  local vb = Wb.ent.villages[va.id]
  -- wait for the months before the local sowing season, then half the households leave
  local farming = require("sim.society.farming")
  local guard = 0
  while not (va.season.phase == "winter" and farming.local_week(Wa, va) >= 40) and guard < 60 do
    world.tick(Wa); world.tick(Wb); guard = guard + 1
  end
  world.test_action(Wa, "emigrate", { village = va.id, fraction = 0.5 })
  local keys = { "sown", "harvest" }
  local sa = run_acc(Wa, va, 48, keys)
  local sb = run_acc(Wb, vb, 48, keys)
  T.ok(sb.sown > 0 and sb.harvest > 0, "the control village sowed and harvested")
  T.ok(sa.sown < sb.sown * 0.9, string.format("less sown: %.2f vs %.2f fields", sa.sown, sb.sown))
  T.ok(sa.harvest < sb.harvest * 0.9, string.format("smaller harvest: %.0f vs %.0f FU", sa.harvest, sb.harvest))
  return string.format("%s: sown %.1f vs %.1f, harvest %.0f vs %.0f", va.name, sa.sown, sb.sown, sa.harvest, sb.harvest)
end

function M.b10_no_growth_in_the_cold_and_sowing_waits_for_spring()
  local W = worlds.fresh(22)
  local cfg = cfgmod.SOCIETY
  local lo = cfg.crop_t[1]
  local vs = U.villages(W, function(u) return #u.farms > 0 end)
  -- a cold spell (a chill held over the village) while its crops stand in the fields: the crops
  -- stop growing (they may be frost-bitten), and the unchilled villages' crops keep growing
  local cold
  local sow_weeks, checked, grew_elsewhere = {}, 0, 0
  for wk = 1, 60 do
    if not cold then
      for _, v in ipairs(vs) do
        if v.season.phase == "growing" then cold = v; break end
      end
    end
    if cold and checked < 40 then
      local cells = { cold.cell }
      for _, c in ipairs(cold.farms) do cells[#cells + 1] = c end
      local t = W.F.temp[cold.cell]
      world.test_action(W, "heat", { cells = cells, amount = -(t - lo + 6) })
    end
    local before, phase0, sown0 = {}, {}, {}
    for _, v in ipairs(vs) do
      for _, c in ipairs(v.farms) do before[c] = W.F.crop[c] end
      phase0[v.id] = v.season.phase
      sown0[v.id] = v.acc.sown or 0
    end
    world.tick(W)
    for _, v in ipairs(vs) do
      if v.alive then
        for _, c in ipairs(v.farms) do
          if before[c] and v.season.phase ~= "sowing" and phase0[v.id] ~= "sowing" then
            if W.F.temp[c] <= lo then
              T.ok(W.F.crop[c] <= before[c] + 1e-9, string.format("no crop growth at %.1f C", W.F.temp[c]))
              checked = checked + 1
            elseif v ~= cold and W.F.crop[c] > before[c] + 1e-6 then
              grew_elsewhere = grew_elsewhere + 1
            end
          end
        end
        if phase0[v.id] ~= "sowing" and v.season.phase == "sowing" then
          T.ok(W.F.temp[v.cell] >= cfg.sow_temp, v.name .. " opens its sowing season only when warm enough")
          sow_weeks[v.id] = sow_weeks[v.id] or wk
        end
        if (v.acc.sown or 0) > sown0[v.id] + 1e-9 then
          T.eq(v.season.phase, "sowing", v.name .. " sows only in its sowing season")
        end
      end
    end
  end
  T.ok(cold ~= nil, "a village with crops in the fields")
  T.ok(checked > 0, "cold field-weeks were checked")
  T.ok(grew_elsewhere > 0, "crops grow where it is warm")
  -- a village held in the cold through its spring does not open its sowing season
  local W2 = worlds.fresh(22)
  local v2
  for _, v in ipairs(U.villages(W2, function(u) return #u.farms > 0 end)) do
    if v.season.phase == "winter" then v2 = v; break end
  end
  if v2 then
    for _ = 1, 30 do
      world.test_action(W2, "heat", { cells = { v2.cell }, amount = (cfg.sow_temp - 6) - W2.F.temp[v2.cell] })
      world.tick(W2)
      T.ok(v2.season.phase == "winter", v2.name .. " waits for spring while it stays cold")
    end
  end
  return string.format("%d cold field-weeks (chilled %s), %d warm field-weeks grew; sowing held back in %s",
    checked, cold and cold.name or "-", grew_elsewhere, v2 and v2.name or "-")
end

return M
