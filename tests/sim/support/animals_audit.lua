-- Animal movement audit (S15): are the animals of a herd, band or pack individuals, or a
-- synchronised blob? Advances a v3 world, samples every animal's position every `dt` game hours and
-- measures, per group:
--   starts    moves begun from standing (a hop straight after a hop is not a start), and the share
--             begun in a "mass start" (>= half the group, >= 3, starting within the same window)
--   align     mean pairwise cosine of the headings of the members moving at the same moment
--             (1 = all one heading; a pack travelling in file is naturally high)
--   blob      share of moments with >= 60% of the group moving and that cosine > 0.97
--   speedcv   coefficient of variation of the speeds of the members moving at the same moment
--             (0 = all at one speed)
--   offset    how far each member's offset from the group's centre moved over `lag` hours, over the
--             group's radius (0 = a rigid formation; ~1 = they mix)
--   nnkeep    share of members whose nearest neighbour is the same one `lag` hours later
--   moving    share of member-samples moving; nowhy: member-samples without a reason
-- Groups: livestock herds out at pasture (herded, or left on the ground), in the pen by day, wild
-- bands by day, wolf packs by night, dogs out with a herd.
-- Used by tools/v3_animals.lua and tests/sim/unit/test_v3_animals.lua.
local world = require("sim.world")
local clock = require("sim.agents.clock")
local stock = require("sim.agents.stock")
local wild = require("sim.agents.wild")
local dogs = require("sim.agents.dogs")

local M = {}
local sqrt, floor, max = math.sqrt, math.floor, math.max
local MOVING = { walk = true, flee = true, chase = true, leave = true, run = true }

-- opts: from (absolute hour), hours, dt, lag, sett (one settlement id), on_sample(t, s, kind, a, x, y)
function M.run(W, opts)
  local V = W.S.v3
  local dt, lag = opts.dt or 0.02, opts.lag or 2
  local t0 = opts.from or W.hour
  if t0 > W.hour then world.advance(W, t0) end
  local G = {}
  local function grp(key, label, kind)
    local g = G[key]
    if not g then g = { key = key, label = label, kind = kind, frames = {}, nowhy = 0, samples = 0 }; G[key] = g end
    return g
  end
  local prev = {}
  local fi = 0
  local function note(g, mk, x, y, moving, a)
    local fr = g.frames[fi]
    if not fr then fr = {}; g.frames[fi] = fr end
    local p = prev[mk]
    local vx, vy = 0, 0
    if p then vx, vy = (x - p[1]) / dt, (y - p[2]) / dt end
    fr[mk] = { x, y, vx, vy, moving }
    fr.n = (fr.n or 0) + 1
    if moving then fr.m = (fr.m or 0) + 1 end
    if moving and not (p and p[3]) then fr.s = (fr.s or 0) + 1 end
    prev[mk] = { x, y, moving }
    g.samples = g.samples + 1
    if not a.why then g.nowhy = g.nowhy + 1 end
  end
  local cb = opts.on_sample
  for t = t0 + dt, t0 + (opts.hours or 48), dt do
    world.advance(W, t)
    fi = fi + 1
    for _, s in ipairs(V.settlements) do
      if s.alive and (not opts.sett or s.id == opts.sett) then
        local lh = clock.local_hour(t, W.grid.lon[s.cell])
        local night = lh >= (s.sunset or 18) + 0.4 or lh < (s.sunrise or 6) - 0.3
        for _, kind in ipairs(stock.HERDS) do
          local H = s.herds and s.herds[kind]
          if H then
            local herder = stock.herder(W, s, H)
            local out = H.out and not night
            for _, a in ipairs(H.animals) do
              if a.alive then
                local x, y = stock.pos(a, t)
                local mk = "s" .. s.id .. kind .. a.k
                local moving = MOVING[a.act] and t < a.t1
                if out and a.act ~= "pen" and a.act ~= "held" and a.act ~= "yoked" then
                  local k2 = herder and "herded" or "left"
                  note(grp(s.id .. ":" .. kind .. ":" .. k2, s.name .. " " .. kind .. (herder and " (herded)" or " (left out)"), "stock_" .. k2), mk, x, y, moving, a)
                elseif not night and a.act ~= "held" and a.act ~= "yoked" and not H.out then
                  note(grp(s.id .. ":" .. kind .. ":pen", s.name .. " " .. kind .. " (pen by day)", "stock_pen"), mk, x, y, moving, a)
                else
                  prev[mk] = { x, y, moving }
                end
                if cb then cb(t, s, kind, a, x, y) end
              end
            end
          end
        end
        if s.wildlife then
          for _, w in ipairs(s.wildlife.animals) do
            if w.alive then
              local x, y = wild.pos(w, t)
              local mk = "w" .. s.id .. ":" .. w.k
              local moving = MOVING[w.act] and t < w.t1
              if w.pred then
                note(grp(s.id .. ":pack" .. w.gid, s.name .. " " .. w.sp .. " pack " .. w.gid, "pack"), mk, x, y, moving, w)
              elseif not night then
                note(grp(s.id .. ":band" .. w.gid, s.name .. " " .. w.sp .. " band " .. w.gid, "band"), mk, x, y, moving, w)
              else
                prev[mk] = { x, y, moving }
              end
              if cb then cb(t, s, "wild", w, x, y) end
            end
          end
        end
        for _, d in ipairs(s.dogs or {}) do
          if d.alive then
            local x, y = dogs.pos(d, t)
            local mk = "d" .. s.id .. ":" .. d.k
            local moving = MOVING[d.act] and t < d.t1
            local H = d.hk and s.herds[d.hk]
            if H and H.out and not night and stock.herder(W, s, H) then
              note(grp(s.id .. ":dogs", s.name .. " dogs out with herds", "dogs"), mk, x, y, moving, d)
            else
              prev[mk] = { x, y, moving }
            end
            if cb then cb(t, s, "dog", d, x, y) end
          end
        end
      end
    end
  end
  -- metrics
  local lagf = floor(lag / dt + 0.5)
  local function nearest(fr, mk)
    local a = fr[mk]
    local best, bd = nil, 1e18
    for k2, b in pairs(fr) do
      if type(b) == "table" and k2 ~= mk then
        local dx, dy = a[1] - b[1], a[2] - b[2]
        local d = dx * dx + dy * dy
        if d < bd or (d == bd and k2 < best) then best, bd = k2, d end
      end
    end
    return best
  end
  local function centre(fr)
    local cx, cy, n = 0, 0, 0
    for _, b in pairs(fr) do if type(b) == "table" then cx, cy, n = cx + b[1], cy + b[2], n + 1 end end
    if n == 0 then return 0, 0, 0 end
    cx, cy = cx / n, cy / n
    local r = 0
    for _, b in pairs(fr) do if type(b) == "table" then r = r + (b[1] - cx) * (b[1] - cx) + (b[2] - cy) * (b[2] - cy) end end
    return cx, cy, sqrt(r / n)
  end
  local keys = {}
  for k in pairs(G) do keys[#keys + 1] = k end
  table.sort(keys)
  local rows = {}
  for _, k in ipairs(keys) do
    local g = G[k]
    local nf, maxn, mvs, ns, starts, mass = 0, 0, 0, 0, 0, 0
    local aln, als, blob, mfr, cvs, cvn = 0, 0, 0, 0, 0, 0
    local offs, offn, nnk, nnn = 0, 0, 0, 0
    local fis = {}
    for f2 in pairs(g.frames) do fis[#fis + 1] = f2 end
    table.sort(fis)
    for _, f2 in ipairs(fis) do
      local fr = g.frames[f2]
      local n = fr.n or 0
      if n >= 3 then
        nf = nf + 1
        if n > maxn then maxn = n end
        local m = fr.m or 0
        mvs, ns = mvs + m, ns + n
        local st = fr.s or 0
        starts = starts + st
        if st >= 3 and st >= n * 0.5 then mass = mass + st end
        if m >= 3 then
          local sx, sy, sp, sp2, mm = 0, 0, 0, 0, 0
          for _, b in pairs(fr) do
            if type(b) == "table" and b[5] then
              local v = sqrt(b[3] * b[3] + b[4] * b[4])
              if v > 1e-6 then sx, sy, sp, sp2, mm = sx + b[3] / v, sy + b[4] / v, sp + v, sp2 + v * v, mm + 1 end
            end
          end
          if mm >= 3 then
            local cosm = (sx * sx + sy * sy - mm) / (mm * (mm - 1))
            als, aln, mfr = als + cosm, aln + 1, mfr + 1
            if m >= 0.6 * n and cosm > 0.97 then blob = blob + 1 end
            local mean = sp / mm
            if mean > 0 then cvs, cvn = cvs + sqrt(max(0, sp2 / mm - mean * mean)) / mean, cvn + 1 end
          end
        end
        local fr2 = g.frames[f2 + lagf]
        if fr2 and (fr2.n or 0) >= 3 then
          local cx, cy, r = centre(fr)
          local cx2, cy2, r2 = centre(fr2)
          local rr = max(1e-4, (r + r2) * 0.5)
          for mk, b in pairs(fr) do
            local b2 = type(b) == "table" and fr2[mk]
            if b2 then
              local ex, ey = (b[1] - cx) - (b2[1] - cx2), (b[2] - cy) - (b2[2] - cy2)
              offs, offn = offs + sqrt(ex * ex + ey * ey) / rr, offn + 1
              local a1, a2 = nearest(fr, mk), nearest(fr2, mk)
              if a1 and a2 then
                nnn = nnn + 1
                if a1 == a2 then nnk = nnk + 1 end
              end
            end
          end
        end
      end
    end
    if nf > 0 then
      rows[#rows + 1] = {
        key = g.key, label = g.label, kind = g.kind, frames = nf, maxn = maxn, moving = ns > 0 and mvs / ns or 0,
        starts = starts, mass = starts > 0 and mass / starts or 0, align = aln > 0 and als / aln or 0,
        blob = mfr > 0 and blob / mfr or 0, speedcv = cvn > 0 and cvs / cvn or 0, cvn = cvn,
        offset = offn > 0 and offs / offn or -1, offn = offn, nnkeep = nnn > 0 and nnk / nnn or -1,
        nowhy = g.samples > 0 and g.nowhy / g.samples or 0,
      }
    end
  end
  return rows
end

function M.print(rows)
  print(string.format("%-40s %6s %5s %6s %6s %6s %6s %6s %7s %6s %6s %6s", "group", "frames", "maxn", "moving", "starts", "mass%",
    "align", "blob%", "speedcv", "offset", "nnkeep", "nowhy"))
  for _, r in ipairs(rows) do
    print(string.format("%-40s %6d %5d %5.0f%% %6d %5.0f%% %6.2f %5.0f%% %7.2f %6.2f %6.2f %5.0f%%", r.label:sub(1, 40), r.frames, r.maxn,
      100 * r.moving, r.starts, 100 * r.mass, r.align, 100 * r.blob, r.speedcv, r.offset, r.nnkeep, 100 * r.nowhy))
  end
end

return M
