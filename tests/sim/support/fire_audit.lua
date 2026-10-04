-- Animals and fire (S22): a fire set beside a group of animals - a herd out at pasture, a wild band,
-- or a herd shut in its pen - on a dry day (lightning on a tree, or a stand of trees alight at once: the
-- front of a running fire), then every member sampled every `dt` game hours: its distance to the fire's
-- footprint (each burning thing's own extent: a tree's crown, a building's walls, a field's edges),
-- what it does and why. Used by tools/v3_fire_animals.lua and tests/sim/unit/test_v3_beasts_fire.lua.
-- Measures (returned in a table):
--   exposed     members that came within `near` of the fire's footprint while it burned
--   react       minutes from first exposure to the first reaction (a why about fire, or flight):
--               median, p90, max; never = exposed members that never reacted while exposed
--   inside      member-hours within `danger` (the flames' reach) while the fire burned
--   reentry     members that got clear (beyond `safe`) and later came back within `near` while it burned
--   spread      mean resultant length of the headings of members fleeing at the same moment (1 = one
--               heading for all, 0 = every way); vcorr the pairwise velocity correlation of the fleeing
--   startspread minutes between the first and the last member to start running
--   dead, hurt  members dead (fire), members that lost health
--   disp1/disp3 mean distance from where each stood before, 1 and 3 hours after the fire began
--   regroup     hours after the fire was out until the group's spread came back to 1.3x its old one
local world = require("sim.world")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")
local blaze = require("sim.agents.blaze")
local danger = require("sim.agents.danger")

local M = {}
local sqrt, floor, max, min = math.sqrt, math.floor, math.max, math.min

local FIREWHY = { "fire", "Fire", "burn", "Running" }
local function about_fire(why)
  if not why then return false end
  for _, k in ipairs(FIREWHY) do if why:find(k, 1, true) then return true end end
  return false
end
M.about_fire = about_fire

-- opts: sett, herd ("flock" | "cattle" | "swine" | "wild" | "pen:flock" ...), fire (trees), off (from the
-- group's middle), dry, hours, dt, day, near, danger, safe, trace (member index), print (a fn)
function M.run(W, opts)
  local V = W.S.v3
  local s = V.settlements[opts.sett or 1]
  local herd = opts.herd or "flock"
  local penned = herd:sub(1, 4) == "pen:"
  if penned then herd = herd:sub(5) end
  local dt = opts.dt or 0.02
  local near, dng, safe = opts.near or 0.15, opts.danger or 0.07, opts.safe or 0.3
  local out = opts.print or function() end
  local function lh(t) return clock.local_hour(t, W.grid.lon[s.cell]) end
  -- 1. a time when the group is where we want it: out at pasture by day, or shut in for the night
  local t = W.hour + (opts.day or 0) * 24
  local H
  local function ready()
    if herd == "wild" then
      local n = 0
      wild.each(s, function(w) if not w.pred then n = n + 1 end end)
      return n >= 4
    end
    H = s.herds and s.herds[herd]
    if not H then return false end
    local h = lh(t)
    local n = 0
    if penned then
      if h < (s.sunset or 18) + 1.5 or h > 23 then return false end
      for _, a in ipairs(H.animals) do if a.alive and a.act == "pen" then n = n + 1 end end
      return n >= 6
    end
    if not H.out then return false end
    if h < (s.sunrise or 6) + 2 or h > (s.sunset or 18) - 4 then return false end
    for _, a in ipairs(H.animals) do if a.alive and a.act ~= "pen" and (a.led or 0) == 0 then n = n + 1 end end
    return n >= 6
  end
  repeat
    t = t + 0.25
    world.advance(W, t)
  until ready() or t > W.hour + 24 * 6
  if not ready() then return nil, "the group was never where it was wanted" end
  local G = {}
  if herd == "wild" then
    -- (S24: the biggest band on the map - a map shows its herds' animals at a fixed share now, D-196)
    local cnt, gid, gn = {}, nil, 0
    wild.each(s, function(w) if not w.pred then cnt[w.gid] = (cnt[w.gid] or 0) + 1 end end)
    wild.each(s, function(w)
      local n = cnt[w.gid] or 0
      if not w.pred and (n > gn or (n == gn and w.gid < gid)) then gid, gn = w.gid, n end
    end)
    wild.each(s, function(w) if w.gid == gid then G[#G + 1] = w end end)
  else
    for _, a in ipairs(H.animals) do
      if a.alive and (a.led or 0) == 0 and (penned and a.act == "pen" or (not penned and a.act ~= "pen")) then G[#G + 1] = a end
    end
  end
  local function apos(a, tt)
    if herd == "wild" then return wild.pos(a, tt) end
    return stock.pos(a, tt)
  end
  local cx, cy = 0, 0
  for _, a in ipairs(G) do local x, y = apos(a, t); cx, cy = cx + x, cy + y end
  cx, cy = cx / #G, cy / #G
  -- 2. the fire: a dry spell; the trees nearest a point `off` from the group's middle alight at once (the
  -- direction from the village's key: the same every run)
  W.F.soil_water[s.cell] = min(W.F.soil_water[s.cell], opts.dry or 0.15)
  local ang = (opts.ang or ((s.key % 97) / 97 * 6.2832))
  local px, py = cx + math.cos(ang) * (opts.off or 0.15), cy + math.sin(ang) * (opts.off or 0.15)
  local cand = {}
  blaze.trees_near(s, px, py, 0.6, function(k, d2) cand[#cand + 1] = { k, d2 } end)
  table.sort(cand, function(a, b) if a[2] ~= b[2] then return a[2] < b[2] end return a[1] < b[1] end)
  local t_fire = t
  local lit = 0
  for q = 1, min(opts.fire or 6, #cand) do
    if blaze.ignite(W, s, "tree", cand[q][1], t_fire, nil) then lit = lit + 1 end
  end
  local R = { sett = s.name, herd = (penned and "pen:" or "") .. herd, n = #G, t_fire = t_fire, lit = lit,
    fx = px, fy = py, gx = cx, gy = cy, lh = lh(t), dryness = blaze.dryness(W, s) }
  out(string.format("%s: %s %d at (%.2f,%.2f) at h %.2f (%02d:%02d), %d trees set alight about (%.2f,%.2f) [%.2f from them], dryness %.2f",
    s.name, R.herd, #G, cx, cy, t, floor(lh(t)), floor(lh(t) % 1 * 60), lit, px, py, opts.off or 0.15, R.dryness))
  local function dfire(x, y)
    local best = 1e9
    for _, fb in ipairs(s.burn or {}) do
      if not fb.out and fb.heat > 0.02 then best = min(best, danger.fire_edge(s, fb, x, y)) end
    end
    return best
  end
  -- 3. sample
  local rec = {}
  for j, a in ipairs(G) do rec[j] = { a = a, h0 = a.health or 1 } end
  local x0s = {}
  for j, a in ipairs(G) do local x, y = apos(a, t); x0s[j] = { x, y } end
  local function spread_of(tt)
    local mx, my, n = 0, 0, 0
    local P = {}
    for _, r in ipairs(rec) do
      if r.a.alive then local x, y = apos(r.a, tt); P[#P + 1] = { x, y }; mx, my, n = mx + x, my + y, n + 1 end
    end
    if n == 0 then return 0 end
    mx, my = mx / n, my / n
    local d = 0
    for _, p in ipairs(P) do d = d + sqrt((p[1] - mx) ^ 2 + (p[2] - my) ^ 2) end
    return d / n, mx, my
  end
  local function disp(tt)
    local sum, n = 0, 0
    for j, r in ipairs(rec) do
      if r.a.alive then
        local x, y = apos(r.a, tt)
        sum, n = sum + sqrt((x - x0s[j][1]) ^ 2 + (y - x0s[j][2]) ^ 2), n + 1
      end
    end
    return n > 0 and sum / n or 0
  end
  local base_spread = spread_of(t)
  local prev = {}
  local Rsum, Rn, vc_num, vc_n = 0, 0, 0, 0
  local fire_out_t, burning_max = nil, 0
  local first_run, last_run
  local last_sig
  local next_print = t
  out(string.format("%7s %5s %6s %8s %8s %6s %6s %6s %s", "h", "local", "burn", "meanD", "minD", "near", "inside", "flee",
    "group spread / centre (others on the map near the fire: dogs, wild)"))
  for tt = t + dt, t + (opts.hours or 6), dt do
    world.advance(W, tt)
    local nb = 0
    for _, fb in ipairs(s.burn or {}) do if not fb.out and fb.heat > 0.02 then nb = nb + 1 end end
    burning_max = max(burning_max, nb)
    if nb == 0 and not fire_out_t then fire_out_t = tt end
    local sumd, mind, nnear, ninside, nflee, nd = 0, 1e9, 0, 0, 0, 0
    local heads = {}
    for j, r in ipairs(rec) do
      local a = r.a
      if a.alive then
        local x, y = apos(a, tt)
        local d = dfire(x, y)
        if nb > 0 then
          sumd, nd = sumd + min(d, 3), nd + 1
          mind = min(mind, d)
          if d < near then
            nnear = nnear + 1
            if not r.first_near then r.first_near = tt end
            if r.got_clear then r.reentry = true end
          end
          if d < dng then
            ninside = ninside + 1
            r.inside = (r.inside or 0) + dt
            -- (once it has noticed the fire: lingering in the flames, or going back into them)
            if r.react and tt > r.react + 5 / 60 then r.inside_aware = (r.inside_aware or 0) + dt end
          end
          if d > safe and r.first_near then r.got_clear = true end
        end
        if r.first_near and not r.react and about_fire(a.why) then r.react = tt end
        local p = prev[j]
        if p then
          local vx, vy = (x - p[1]) / dt, (y - p[2]) / dt
          local sp = sqrt(vx * vx + vy * vy)
          if about_fire(a.why) and sp > 0.05 then
            nflee = nflee + 1
            heads[#heads + 1] = { vx / sp, vy / sp, vx, vy }
            if not r.ran then r.ran = tt; first_run = first_run or tt; last_run = tt end
          end
        end
        prev[j] = { x, y }
        if opts.trace and j == opts.trace then
          local sig = (a.act or "") .. "|" .. (a.why or "")
          if sig ~= last_sig then
            last_sig = sig
            out(string.format("   [%d] h %.2f %-6s (%.3f,%.3f) dfire %.3f health %.2f | %s", j, tt, a.act or "?", x, y, d,
              a.health or a.hp or 1, a.why or ""))
          end
        end
      end
    end
    if #heads >= 3 then
      local hx, hy = 0, 0
      for _, h in ipairs(heads) do hx, hy = hx + h[1], hy + h[2] end
      Rsum, Rn = Rsum + sqrt(hx * hx + hy * hy) / #heads, Rn + 1
      for p = 1, #heads do
        for q = p + 1, #heads do
          local A, B = heads[p], heads[q]
          local la, lb = sqrt(A[3] ^ 2 + A[4] ^ 2), sqrt(B[3] ^ 2 + B[4] ^ 2)
          vc_num, vc_n = vc_num + (A[3] * B[3] + A[4] * B[4]) / max(1e-9, la * lb), vc_n + 1
        end
      end
    end
    if not R.disp1 and tt >= t_fire + 1 then R.disp1 = disp(tt) end
    if not R.disp3 and tt >= t_fire + 3 then R.disp3 = disp(tt) end
    if fire_out_t and not R.regroup and spread_of(tt) <= base_spread * 1.3 then R.regroup = tt - fire_out_t end
    if tt >= next_print - 1e-9 then
      next_print = next_print + 0.25
      local sp, mx, my = spread_of(tt)
      local od, ow = 0, 0
      for _, dg in ipairs(s.dogs or {}) do
        if dg.alive then local x, y = dogs.pos(dg, tt); if dfire(x, y) < near then od = od + 1 end end
      end
      wild.each(s, function(w) local x, y = wild.pos(w, tt); if dfire(x, y) < near then ow = ow + 1 end end)
      out(string.format("%7.2f %02d:%02d %6d %8.3f %8.3f %6d %6d %6d %.3f (%.2f,%.2f)  dogs near %d, wild near %d%s",
        tt, floor(lh(tt)), floor(lh(tt) % 1 * 60), nb, nd > 0 and sumd / nd or 0, mind < 1e8 and mind or 0, nnear, ninside, nflee,
        sp, mx or 0, my or 0, od, ow, (W.F.fire[s.cell] or 0) > 0 and "  [the planet's cell is burning]" or ""))
    end
  end
  -- 4. summary
  local rts = {}
  R.exposed, R.never, R.reentry, R.dead, R.hurt, R.inside, R.inside_aware = 0, 0, 0, 0, 0, 0, 0
  for _, r in ipairs(rec) do
    if r.first_near then
      R.exposed = R.exposed + 1
      if r.react then rts[#rts + 1] = (r.react - r.first_near) * 60 else R.never = R.never + 1 end
      if r.reentry then R.reentry = R.reentry + 1 end
    end
    R.inside = R.inside + (r.inside or 0)
    R.inside_aware = R.inside_aware + (r.inside_aware or 0)
    if not r.a.alive then R.dead = R.dead + 1 elseif (r.a.health or r.a.hp or 1) < r.h0 - 0.05 then R.hurt = R.hurt + 1 end
  end
  table.sort(rts)
  local function pct(L, q) if #L == 0 then return 0 end return L[max(1, min(#L, floor(#L * q + 0.5)))] end
  R.react_med, R.react_p90, R.react_max = pct(rts, 0.5), pct(rts, 0.9), rts[#rts] or 0
  R.spread = Rn > 0 and Rsum / Rn or 0
  R.vcorr = vc_n > 0 and vc_num / vc_n or 0
  R.startspread = first_run and (last_run - first_run) * 60 or 0
  R.burning_max = burning_max
  R.fire_out = fire_out_t and (fire_out_t - t_fire) or nil
  R.base_spread = base_spread
  R.end_spread = spread_of(W.hour)
  return R
end

function M.print(R, out)
  out = out or print
  out("")
  out(string.format("group %d, trees alight at most %d at once; the fire out after %s", R.n, R.burning_max,
    R.fire_out and string.format("%.1f h", R.fire_out) or "- (still burning)"))
  out(string.format("exposed %d; reaction minutes median %.1f, p90 %.1f, max %.1f; never reacted %d; first to last to run %.1f min",
    R.exposed, R.react_med, R.react_p90, R.react_max, R.never, R.startspread))
  out(string.format("member-hours within the flames' reach %.2f (%.2f after noticing it); came back near after getting clear %d", R.inside,
    R.inside_aware, R.reentry))
  out(string.format("escape headings: mean resultant %.2f; pairwise velocity correlation %.2f", R.spread, R.vcorr))
  out(string.format("dead %d, hurt %d", R.dead, R.hurt))
  out(string.format("displaced from where they stood: %.3f after 1 h, %.3f after 3 h; regrouped %s after the fire was out",
    R.disp1 or 0, R.disp3 or 0, R.regroup and string.format("%.2f h", R.regroup) or "not in the run"))
  out(string.format("group spread before %.3f, at the end %.3f", R.base_spread, R.end_spread))
end

return M
