-- Blight on a village's fields (S23, R7 v2; docs/v3/70_R7_GOD_POWERS.md §2): a plant sickness in a growing crop.
-- It is a state of the plot, nothing scripted after it:
--   pl.blight      severity 0..1 (the share of the crop it has taken)
--   pl.blight_ev   the cause it carries (the god's blight: an event id; passed on with it)
-- Daily (settle's daily round, after the crops grow): it worsens with warm, wet days and eases in dry heat or
-- cold; the corn it holds fills less (settle.grow_plots reads the severity into the yield); it spreads to the
-- plots near it by the wind - by its severity, their nearness and the wet. At harvest the sheaves of a blighted
-- field carry it into the granary's grain (s.seed_blight: the share of the grain that is tainted and the cause);
-- a row sown from that grain may start the sickness in a new field. The owners notice it once it shows - a
-- deed of their loss with its cause (for the news and the chain of what the god did). Harvested fields are
-- clear of it (it lives in the standing crop).
local sfmt = require("sim.fmt").format
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local floor, max, min, sqrt = math.floor, math.max, math.min, math.sqrt
local function C() return cfgmod.V3 end

-- Plot pl of village s catches it (severity sev, cause ev); a plot already blighter keeps its own.
function M.infect(W, s, pl, sev, ev)
  if pl.stage ~= "growing" and pl.stage ~= "ripe" then return false end
  if (pl.blight or 0) >= sev then return false end
  pl.blight = sev
  pl.blight_ev = pl.blight_ev or ev
  return true
end

local function centre(pl) return (pl.x0 + pl.x1) * 0.5, (pl.y0 + pl.y1) * 0.5 end

-- Daily, for village s on day `day` (after the crops grew).
function M.daily(W, s, day)
  local P = s.plots
  if not P then return end
  local c = C()
  local any = false
  for k = 1, #P do if (P[k].blight or 0) > 0 then any = true; break end end
  if not any then return end
  local T = W.F.temp[s.cell] or 10
  local wet = min(1, (W.F.soil_water[s.cell] or 0.4) * 1.4 + ((s.rain_now or 0) > 0 and 0.3 or 0))
  -- (blights of corn love mild, damp weather; dry heat and cold hold them back)
  local warm = (T > c.blight_tmin and T < c.blight_tmax) and 1 or 0.2
  local grow = c.blight_grow * warm * max(c.blight_wet_min, wet - 0.35)
  local V = W.S.v3
  for k = 1, #P do
    local pl = P[k]
    local b = pl.blight or 0
    if b > 0 then
      if pl.stage == "growing" or pl.stage == "ripe" then
        b = max(0.01, min(1, b + grow * (0.3 + b)))
        pl.blight = b
        -- the owners see it once it shows
        if b >= c.blight_seen and pl.blight_told ~= pl.sown_day and (pl.hh or 0) ~= 0 then
          pl.blight_told = pl.sown_day
          local deeds = require("sim.agents.deeds")
          local people = require("sim.agents.people")
          local x, y = centre(pl)
          local d = deeds.add(W, { kind = "blight", t = W.hour, at = s.id, x = x, y = y, by = 0, by_hh = 0, by_s = 0, to = 0,
            to_hh = pl.hh, to_s = s.id, what = pl.crop, n = 1, fu = (pl.sown or 0) * b * 2, harm = true, event = pl.blight_ev })
          deeds.learn(W, s, d, "the blight in the corn", 0, 0, W.hour)
          for _, id in ipairs((V.households[pl.hh] or {}).members or {}) do
            local q = V.people[id]
            if q and q.alive and people.stage(q, W.hour) == "adult" then people.remember(q, W.hour, sfmt("Blight in %s: the corn is spotted and withering", pl.name or "our field")) end
          end
        end
        -- the wind takes it to the fields near
        if b >= 0.08 and pl.stage == "growing" then
          local x, y = centre(pl)
          for j = 1, #P do
            local o = P[j]
            if j ~= k and o.stage == "growing" and (o.blight or 0) < 0.02 then
              local ox, oy = centre(o)
              local dd = sqrt((ox - x) * (ox - x) + (oy - y) * (oy - y))
              if dd < c.blight_reach then
                local pz = c.blight_spread * b * wet * (1 - dd / c.blight_reach)
                if rng.float(W.seed, rng.S.BLIGHT, s.id * 1000 + j, day, k) < pz then M.infect(W, s, o, 0.02, pl.blight_ev) end
              end
            end
          end
        end
      else
        -- (harvested and gone: the field is clear of it)
        pl.blight, pl.blight_ev = nil, nil
      end
    end
  end
end

-- The sheaves of a row harvested from a blighted field taint the village's grain (as much as the blight held
-- of it); called with y the row's yield.
function M.on_harvest(W, s, pl, y)
  local b = pl.blight or 0
  if b <= 0 or y <= 0 then return end
  local g = s.granary
  local have = g and ((g.stock.grain or 0) + (g.stock.sheaves or 0)) or 0
  local sb = s.seed_blight or { share = 0 }
  local bad = sb.share * have + y * min(1, b * 1.5)
  sb.share = min(1, bad / max(1, have + y))
  sb.ev = sb.ev or pl.blight_ev
  s.seed_blight = sb
end

-- A row of plot pl sown (from the village's grain): a tainted seed may start the blight in it.
function M.on_sow(W, s, k, row, now)
  local sb = s.seed_blight
  local pl = s.plots and s.plots[k]
  if not sb or not pl or sb.share <= 0.005 then return end
  local c = C()
  if rng.float(W.seed, rng.S.BLIGHT, s.id * 1000 + k, floor(now), row) < sb.share * c.blight_seed then
    M.infect(W, s, pl, 0.02, sb.ev)
  end
  -- (the tainted grain is used up as it is sown and eaten: the share fades as the store turns over)
  sb.share = sb.share * 0.995
end

-- The god's blight: the growing field nearest the point (else the village's largest growing field).
-- (S23: `target` reads only - the cast and the aim preview share it)
function M.target(s, x, y)
  local best, bd
  for _, pl in ipairs(s.plots or {}) do
    if pl.stage == "growing" or pl.stage == "ripe" then
      local px, py = centre(pl)
      local d = x and ((px - x) * (px - x) + (py - y) * (py - y)) or -(pl.sown or 0)
      if not best or d < bd then best, bd = pl, d end
    end
  end
  return best
end

function M.cast(W, s, x, y, ev)
  local best = M.target(s, x, y)
  if not best then return nil end
  M.infect(W, s, best, C().blight_cast, ev)
  return best
end

return M
