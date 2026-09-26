-- Fauna & fish baseline stability (02_WORLD_MODEL.md §10), untouched world, 200 years:
--   * no continental grazer/predator extinction (≥ 9/10 seeds; quick: every seed),
--   * predators oscillate: mean over continents of the predator CV (monthly, years 5–200) ≥ 0.15,
--   * fish: no ocean-wide collapse (global mean never below half its median),
-- plus the vegetation bands, reported per seed; forest and grassland asserted against grazing
-- damage (see below). Desert/ice are reported; S01 baseline_stability will assert every band.
local T = require("tests.sim.lib")
local B = require("tests.sim.support.baseline")
local cfgmod = require("sim.config")
local M = { tests = {} }

local function seeds()
  if T.args.full then return { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 } end
  return { 1, 2, 3 }
end

local function median(t)
  local c = {}
  for i = 1, #t do c[i] = t[i] end
  table.sort(c)
  return c[math.floor(#c / 2) + 1]
end

M.tests[1] = { "fauna_baseline_stability_200y", function()
  local list = seeds()
  local ok_n, lines = 0, {}
  local ext = cfgmod.DETECT.extinct_density
  for _, seed in ipairs(list) do
    local r = B.run(seed, B.STATS_YEARS)
    local extinct = {}
    local cvs, cvsum = {}, 0
    for k = 1, #r.conts do
      local gmin = B.minmax(r.g[k], 1)
      local pmin = B.minmax(r.p[k], 1)
      if gmin < ext then extinct[#extinct + 1] = "grazers@c" .. r.conts[k] end
      if pmin < ext then extinct[#extinct + 1] = "predators@c" .. r.conts[k] end
      local cv = B.cv(r.p[k], 61)
      cvs[#cvs + 1] = string.format("%.2f", cv)
      cvsum = cvsum + cv
    end
    local cvmean = cvsum / #r.conts
    local fmin = B.minmax(r.fish, 1)
    local fmed = median(r.fish)
    local fish_ok = fmin >= 0.5 * fmed
    local ok = #extinct == 0 and cvmean >= 0.15 and fish_ok
    if ok then ok_n = ok_n + 1 end
    lines[#lines + 1] = string.format("s%d %s: pred CV %.2f [%s]%s, fish min %.3f / median %.3f",
      seed, ok and "ok" or "FAIL", cvmean, table.concat(cvs, " "),
      #extinct > 0 and (" EXTINCT " .. table.concat(extinct, ",")) or "", fmin, fmed)
  end
  local need = math.ceil(#list * 0.9 - 1e-9)
  local summary = string.format("%d/%d seeds (need %d)\n      %s", ok_n, #list, need, table.concat(lines, "\n      "))
  if ok_n < need then error(summary, 0) end
  return summary
end }

-- Mean forest cover / grassland over years 5..Y of an untouched run with grazing and browsing
-- switched off (reference for "did the animals push this band out?").
local function no_grazing_means(seed, years)
  local world = require("sim.world")
  local metrics = require("sim.metrics")
  local scen = require("tests.sim.scenarios.lib")
  local F = cfgmod.FAUNA
  local g0, b0 = F.graze, F.browse
  F.graze, F.browse = 0, 0
  local ok, fo, gr = pcall(function()
    local W = scen.fresh_world(seed)
    local sf, sg, n = 0, 0, 0
    for y = 1, years do
      world.run_years(W, 1)
      if y >= 5 then
        local m = metrics.compute(W)
        sf, sg, n = sf + m.forest_nat, sg + m.grass_nat, n + 1
      end
    end
    return sf / n, sg / n
  end)
  F.graze, F.browse = g0, b0
  if not ok then error(fo, 0) end
  return fo, gr
end

-- Bands from 02 §10 for forest cover and grassland (natural land, D-028). A seed out of band fails
-- only if grazing and browsing moved the mean by more than 3 points (the same seed run without
-- them is the reference); worlds out of band by geography are S01's business.
M.tests[2] = { "vegetation_bands_with_fauna_200y", function()
  local list = seeds()
  local lines, bad = {}, {}
  local BANDS = { forest = { 0.20, 0.55 }, grass = { 0.20, 0.50 } }
  for _, seed in ipairs(list) do
    local r = B.run(seed, B.STATS_YEARS)
    local s = { forest = {}, grass = {}, desert = {}, ice = {} }
    for y = 5, B.STATS_YEARS do
      local v = r.yearly[y]
      s.forest[#s.forest + 1] = v.forest_nat   -- natural land (D-028)
      s.grass[#s.grass + 1] = v.grass_nat
      s.desert[#s.desert + 1] = v.desert_nat
      s.ice[#s.ice + 1] = v.ice
    end
    local parts, means = {}, {}
    for _, k in ipairs({ "forest", "grass", "desert", "ice" }) do
      local lo, hi = B.minmax(s[k], 1)
      local _, mean = B.cv(s[k], 1)
      means[k] = mean
      parts[#parts + 1] = string.format("%s %.2f (%.2f-%.2f)", k, mean, lo, hi)
      -- ("forest must vary by 5 points" is S01's band)
    end
    local out = {}
    for _, k in ipairs({ "forest", "grass" }) do
      if means[k] < BANDS[k][1] or means[k] > BANDS[k][2] then out[#out + 1] = k end
    end
    local note = ""
    if #out > 0 then
      local rf, rg = no_grazing_means(seed, B.STATS_YEARS)
      local ref = { forest = rf, grass = rg }
      for _, k in ipairs(out) do
        local in_band = ref[k] >= BANDS[k][1] and ref[k] <= BANDS[k][2]
        local moved = math.abs(means[k] - ref[k]) > 0.03
        -- the animals wreck a band only if they move it by more than 3 points (a 1-point
        -- browsing effect on a world sitting at the edge is ecology, not a broken band)
        note = note .. string.format(" [%s out of band; without grazing %.3f%s]", k, ref[k],
          moved and " -> caused by animals" or (in_band and " -> animals nudge an edge case" or " -> geography"))
        if moved then bad[#bad + 1] = "s" .. seed .. " " .. k end
      end
    end
    lines[#lines + 1] = string.format("s%d: %s%s", seed, table.concat(parts, ", "), note)
  end
  local summary = table.concat(lines, "\n      ")
  if #bad > 0 then error("bands broken by fauna: " .. table.concat(bad, ", ") .. "\n      " .. summary, 0) end
  return summary
end }

return M
