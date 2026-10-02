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
--             (S16: members lying down at both ends are left out - resting together is not a formation)
--             group's radius (0 = a rigid formation; ~1 = they mix)
--   nnkeep    share of members whose nearest neighbour is the same one `lag` hours later
--   moving    share of member-samples moving; nowhy: member-samples without a reason
--   (S16) vcorr   mean over pairs of members of the correlation of their velocity series (sum of
--             v_i.v_j over the frames both are seen / sqrt(sum |v_i|^2 sum |v_j|^2)): 1 = they move
--             as one rigid body, ~0 = each its own way (a herd driven together is legitimately > 0)
--   stops     moves ended, and the share ended in a "mass stop" (as mass start)
--   rests     lyings-down begun, and the share begun in a "mass rest" (>= half, >= 3, one window)
--   ctrcv     coefficient of variation of the members' distances from the group's centre (0 = a
--             ring or a grid at one radius; a natural spread ~0.4-0.7); ctr: the mean distance
--             over the species' spacing
--   young     mean distance of a young animal from its mother over the spacing, and far%: the
--             share of young-samples farther than 3 spacings
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
local RESTING = { rest = true, lie = true, sit = true, pen = true }

-- opts: from (absolute hour), hours, dt, lag, sett (one settlement id), on_sample(t, s, kind, a, x, y)
function M.run(W, opts)
  local V = W.S.v3
  local dt, lag = opts.dt or 0.02, opts.lag or 2
  local t0 = opts.from or W.hour
  if t0 > W.hour then world.advance(W, t0) end
  local G = {}
  local function grp(key, label, kind, cls)
    local g = G[key]
    if not g then g = { key = key, label = label, kind = kind, cls = cls or kind, frames = {}, nowhy = 0, samples = 0, yd = 0, yn = 0, yfar = 0 }; G[key] = g end
    return g
  end
  local prev = {}
  local fi = 0
  local function note(g, mk, x, y, moving, a, sep)
    local fr = g.frames[fi]
    if not fr then fr = {}; g.frames[fi] = fr end
    local p = prev[mk]
    local vx, vy = 0, 0
    if p then vx, vy = (x - p[1]) / dt, (y - p[2]) / dt end
    local resting = RESTING[a.act] or false
    fr[mk] = { x, y, vx, vy, moving, resting }
    fr.n = (fr.n or 0) + 1
    fr.sep = sep
    if moving then fr.m = (fr.m or 0) + 1 end
    if moving and p and not p[3] then fr.s = (fr.s or 0) + 1 end   -- (appearing is not starting)
    if p and p[3] and not moving then fr.e = (fr.e or 0) + 1 end
    if p and resting and not p[4] then fr.r = (fr.r or 0) + 1 end
    prev[mk] = { x, y, moving, resting }
    g.samples = g.samples + 1
    if not a.why then g.nowhy = g.nowhy + 1 end
  end
  -- (S16) a young animal's distance from its mother, in spacings
  local function young(g, d, sep)
    g.yd, g.yn = g.yd + d / sep, g.yn + 1
    if d > 3 * sep then g.yfar = g.yfar + 1 end
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
                local sep = stock.SP(a.sp).sep
                local g
                if (a.led or 0) ~= 0 then
                  -- (one of a drove, with its drover: S16)
                  g = grp(s.id .. ":drove" .. a.led, s.name .. " drove of " .. a.led, "drove", "drove " .. kind)
                  note(g, mk, x, y, moving, a, sep)
                elseif out and a.act ~= "pen" and a.act ~= "held" and a.act ~= "yoked" then
                  local k2 = herder and "herded" or "left"
                  g = grp(s.id .. ":" .. kind .. ":" .. k2, s.name .. " " .. kind .. (herder and " (herded)" or " (left out)"), "stock_" .. k2, k2 .. " " .. kind)
                  note(g, mk, x, y, moving, a, sep)
                elseif not night and a.act ~= "held" and a.act ~= "yoked" and not H.out then
                  g = grp(s.id .. ":" .. kind .. ":pen", s.name .. " " .. kind .. " (pen by day)", "stock_pen", "pen " .. kind)
                  note(g, mk, x, y, moving, a, sep)
                end
                local mom = g and (a.mother or 0) ~= 0 and H.animals[a.mother]
                if mom and mom.alive and (t - a.born) / 1152 < stock.SP(a.sp).young_years then
                  local mx, my = stock.pos(mom, t)
                  young(g, sqrt((mx - x) * (mx - x) + (my - y) * (my - y)), sep)
                end
                if not g then
                  prev[mk] = { x, y, moving, RESTING[a.act] or false }
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
              local sep = wild.WP(w.sp).sep
              if w.pred then
                note(grp(s.id .. ":pack" .. w.gid, s.name .. " " .. w.sp .. " pack " .. w.gid, "pack", "pack " .. w.sp), mk, x, y, moving, w, sep)
              elseif not night then
                local g = grp(s.id .. ":band" .. w.gid, s.name .. " " .. w.sp .. " band " .. w.gid, "band", "band " .. w.sp)
                note(g, mk, x, y, moving, w, sep)
                local mom = (w.mother or 0) ~= 0 and wild.byk(s, w.mother)
                if mom and mom.alive and (t - w.born) / 1152 < 0.5 then
                  local mx, my = wild.pos(mom, t)
                  young(g, sqrt((mx - x) * (mx - x) + (my - y) * (my - y)), sep)
                end
              else
                prev[mk] = { x, y, moving, RESTING[w.act] or false }
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
              note(grp(s.id .. ":dogs", s.name .. " dogs out with herds", "dogs"), mk, x, y, moving, d, 0.05)
            else
              prev[mk] = { x, y, moving, RESTING[d.act] or false }
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
    local stops, mstops, rests, mrests, ccv, ccn, ctr, ctn = 0, 0, 0, 0, 0, 0, 0, 0
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
        local en, rn = fr.e or 0, fr.r or 0
        stops, rests = stops + en, rests + rn
        if en >= 3 and en >= n * 0.5 then mstops = mstops + en end
        if rn >= 3 and rn >= n * 0.5 then mrests = mrests + rn end
        do
          -- (S16) the spread of distances from the centre
          local cx, cy = 0, 0
          for _, b in pairs(fr) do if type(b) == "table" then cx, cy = cx + b[1], cy + b[2] end end
          cx, cy = cx / n, cy / n
          local d1, d2 = 0, 0
          for _, b in pairs(fr) do
            if type(b) == "table" then
              local d = sqrt((b[1] - cx) * (b[1] - cx) + (b[2] - cy) * (b[2] - cy))
              d1, d2 = d1 + d, d2 + d * d
            end
          end
          local mean = d1 / n
          if mean > 1e-6 then
            ccv, ccn = ccv + sqrt(max(0, d2 / n - mean * mean)) / mean, ccn + 1
            ctr, ctn = ctr + mean / (fr.sep or 0.05), ctn + 1
          end
        end
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
            -- (S16: one lying at both ends of the window keeps its place for a reason: not counted)
            if b2 and b[6] and b2[6] then b2 = nil end
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
    -- (S16) pairwise velocity correlation over the window (members seen in the same frames)
    local vs = {}
    local mks = {}
    for _, f2 in ipairs(fis) do
      local fr = g.frames[f2]
      if (fr.n or 0) >= 3 then
        for mk, b in pairs(fr) do
          if type(b) == "table" then
            local v = vs[mk]
            if not v then v = {}; vs[mk] = v; mks[#mks + 1] = mk end
            v[f2] = b
          end
        end
      end
    end
    table.sort(mks)
    if #mks > 60 then
      -- (a large herd: every other member, enough pairs to tell)
      local m2 = {}
      for i2 = 1, #mks, math.ceil(#mks / 60) do m2[#m2 + 1] = mks[i2] end
      mks = m2
    end
    local vc, vcn = 0, 0
    for i1 = 1, #mks do
      local A1 = vs[mks[i1]]
      for i2 = i1 + 1, #mks do
        local A2 = vs[mks[i2]]
        local dot, n1, n2, nc = 0, 0, 0, 0
        for f2, b in pairs(A1) do
          local b2 = A2[f2]
          if b2 then
            dot = dot + b[3] * b2[3] + b[4] * b2[4]
            n1 = n1 + b[3] * b[3] + b[4] * b[4]
            n2 = n2 + b2[3] * b2[3] + b2[4] * b2[4]
            nc = nc + 1
          end
        end
        if nc >= 20 and n1 > 1e-12 and n2 > 1e-12 then vc, vcn = vc + dot / sqrt(n1 * n2), vcn + 1 end
      end
    end
    if nf > 0 then
      rows[#rows + 1] = {
        cls = g.cls, vcorr = vcn > 0 and vc / vcn or 0, vcn = vcn,
        stops = stops, mstop = stops > 0 and mstops / stops or 0, rests = rests, mrest = rests > 0 and mrests / rests or 0,
        ctrcv = ccn > 0 and ccv / ccn or 0, ctr = ctn > 0 and ctr / ctn or 0,
        young = g.yn > 0 and g.yd / g.yn or -1, yfar = g.yn > 0 and g.yfar / g.yn or 0, yn = g.yn,
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
  print(string.format("%-34s %5s %4s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s", "group", "frms", "maxn",
    "move", "start", "mass", "stopm", "restm", "align", "blob", "spdcv", "vcorr", "offst", "nnkp", "ctrcv", "ctr", "young", "yfar", "nowhy"))
  for _, r in ipairs(rows) do
    print(string.format("%-34s %5d %4d %4.0f%% %5d %4.0f%% %4.0f%% %4.0f%% %5.2f %4.0f%% %5.2f %5.2f %5.2f %5.2f %5.2f %5.1f %5.1f %4.0f%% %4.0f%%",
      r.label:sub(1, 34), r.frames, r.maxn, 100 * r.moving, r.starts, 100 * r.mass, 100 * r.mstop, 100 * r.mrest, r.align, 100 * r.blob,
      r.speedcv, r.vcorr, r.offset, r.nnkeep, r.ctrcv, r.ctr, r.young, 100 * r.yfar, 100 * r.nowhy))
  end
end

-- (S16) The groups summed by class (kind and species), weighted by frames: one line per class.
function M.summary(rows)
  local S, order = {}, {}
  local F = { "moving", "mass", "mstop", "mrest", "align", "blob", "speedcv", "vcorr", "offset", "nnkeep", "ctrcv", "ctr", "nowhy" }
  for _, r in ipairs(rows) do
    local c = S[r.cls]
    if not c then c = { cls = r.cls, n = 0, groups = 0, yd = 0, yn = 0, yfar = 0 }; S[r.cls] = c; order[#order + 1] = r.cls end
    c.groups = c.groups + 1
    local w = r.frames
    for _, f in ipairs(F) do
      if r[f] and r[f] >= 0 then c[f] = (c[f] or 0) + r[f] * w; c[f .. "_w"] = (c[f .. "_w"] or 0) + w end
    end
    if r.yn > 0 then c.yd, c.yn, c.yfar = c.yd + r.young * r.yn, c.yn + r.yn, c.yfar + r.yfar * r.yn end
  end
  table.sort(order)
  local out = {}
  for _, k in ipairs(order) do
    local c = S[k]
    local o = { cls = k, groups = c.groups, young = c.yn > 0 and c.yd / c.yn or -1, yfar = c.yn > 0 and c.yfar / c.yn or 0, yn = c.yn }
    for _, f in ipairs(F) do o[f] = (c[f .. "_w"] or 0) > 0 and c[f] / c[f .. "_w"] or -1 end
    out[#out + 1] = o
  end
  return out
end

function M.print_summary(sum)
  print(string.format("%-16s %3s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s", "class", "grp", "move", "mass", "stopm",
    "restm", "align", "blob", "spdcv", "vcorr", "offst", "nnkp", "ctrcv", "ctr", "young", "yfar"))
  for _, o in ipairs(sum) do
    print(string.format("%-16s %3d %4.0f%% %4.0f%% %4.0f%% %4.0f%% %5.2f %4.0f%% %5.2f %5.2f %5.2f %5.2f %5.2f %5.1f %5.1f %4.0f%%",
      o.cls, o.groups, 100 * o.moving, 100 * o.mass, 100 * o.mstop, 100 * o.mrest, o.align, 100 * o.blob, o.speedcv, o.vcorr,
      o.offset, o.nnkeep, o.ctrcv, o.ctr, o.young, 100 * o.yfar))
  end
end

return M
