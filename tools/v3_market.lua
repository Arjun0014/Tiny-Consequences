-- What each village has and wants of every good, what it is worth there, what it could spare, and how
-- near each pair of villages is to an exchange (S17, the quiet economy): the best trip a village could
-- make to each partner by what it knows, profitable or not, and why it does not go.
--   luajit tools/v3_market.lua [--load assets/worlds/v3.tcsave] [--days 0] [--sett K]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local args = { load = "assets/worlds/v3.tcsave", days = 0 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--sett" then i = i + 1; args.sett = tonumber(arg[i])
  end
  i = i + 1
end
local f = assert(io.open(args.load, "rb"))
local data = f:read("*a")
f:close()
local W = assert(require("sim.worlds").from_save_string(data))
local V = W.S.v3
if args.days > 0 then world.advance(W, W.hour + args.days * 24) end
local econ = require("sim.agents.econ")
local trade = require("sim.agents.trade")
local c = require("sim.config").V3
local now = W.S.v3.now or W.hour
print(string.format("hour %.0f (day %.1f)", now, now / 24))
for _, s in ipairs(V.settlements) do
  if not args.sett or args.sett == s.id then
    print(string.format("\n== %s (pop %d, %s, food %.0f days)", s.name, #s.members, s.dock and "coast" or "inland", s.need and s.need.food_days or 0))
    for _, g in ipairs(econ.ORDER) do
      local G = econ.GOODS[g]
      if not G.beast then
        if G.pool == "village" then
          local have, want = econ.village_stock(W, s, g)
          print(string.format("  %-8s village  have %7.1f want %7.1f  worth %.2f  spare %.1f", g, have, want, econ.worth(g, have, want), econ.spare(W, s, nil, nil, g)))
        else
          local th, tw, lo, hi, sp = 0, 0, 1e9, 0, 0
          for _, hid in ipairs(s.households) do
            local hh = V.households[hid]
            local home = hh and s.buildings[hh.home or 0]
            if home then
              local h, w = econ.house_stock(W, hh, home, g)
              th, tw = th + h, tw + w
              local v = econ.value(W, s, hh, home, g)
              lo, hi = math.min(lo, v), math.max(hi, v)
              sp = sp + econ.spare(W, s, hh, home, g)
            end
          end
          print(string.format("  %-8s houses   have %7.1f want %7.1f  worth %.2f..%.2f  spare %.1f", g, th, tw, lo, hi, sp))
        end
      end
    end
    local partners = trade.partners(W, s)
    for _, o in ipairs(partners) do
      local _, hrs = trade.road(W, s, o)
      local k = s.know and s.know[o.id]
      print(string.format("  -> %s (%.0f h by road) news %s", o.name, hrs, k and string.format("%.0f days old", (now - k.t) / 24) or "none"))
      -- the best give/take pair whatever its net
      local best
      for _, X in ipairs(econ.ORDER) do
        local GX = econ.GOODS[X]
        if GX.pool == "village" then
          local aX = econ.value(W, s, nil, nil, X)
          local bX = econ.expect(W, s, o, X, now)
          for _, H in ipairs(econ.ORDER) do
            local GH = econ.GOODS[H]
            if H ~= X and GH.pool == "village" then
              local aH = econ.value(W, s, nil, nil, H)
              local _, sl, sp = econ.expect(W, s, o, H, now)
              local ratio = bX * aH / (aX * sl)
              if not best or ratio > best.ratio then best = { X = X, H = H, ratio = ratio, aX = aX, bX = bX, aH = aH, sl = sl, sp = sp } end
            end
          end
        end
      end
      if best then
        print(string.format("     best pair: %s (here %.2f, there %.2f) for %s (here %.2f, there %.2f, spare %.1f): ratio %.2f (needs %.2f), trip cost %.1f FU",
          best.X, best.aX, best.bX, best.H, best.aH, best.sl, best.sp, best.ratio, c.trade_margin, 2 * hrs * c.trade_hour))
      end
    end
  end
end

-- the households' best trips (house goods given for any good), net of the road, profitable or not
print("\n== households: the best trips by what they know (top 8 by net)")
local rows = {}
for _, s in ipairs(V.settlements) do
  local partners = trade.partners(W, s)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    local home = hh and s.buildings[hh.home or 0]
    if home then
      for _, X in ipairs(econ.ORDER) do
        local GX = econ.GOODS[X]
        if GX.pool == "house" and not GX.beast then
          local spX = econ.spare(W, s, hh, home, X)
          local aX = econ.value(W, s, hh, home, X)
          for _, o in ipairs(partners) do
            local _, hrs = trade.road(W, s, o)
            local bX = econ.expect(W, s, o, X, now)
            for _, H in ipairs(econ.ORDER) do
              local GH = econ.GOODS[H]
              if H ~= X and not GH.beast then
                local aH = econ.value(W, s, hh, home, H)
                local _, sl, sp = econ.expect(W, s, o, H, now)
                local ratio = bX * aH / (aX * sl)
                local k = math.sqrt(aX * bX / (aH * sl))
                local n = math.min(spX, 1 / GX.wt)
                local qH = math.min(n * k, 1 / GH.wt, sp)
                n = qH / k
                local gain = qH * aH - n * aX
                local w = econ.fresh(W, s, o, now)
                local net = gain * (c.trade_guess + (1 - c.trade_guess) * w) - 2 * hrs * c.trade_hour
                rows[#rows + 1] = { net = net, txt = string.format("%-16s hh%-3d %s %.2f -> %s (there %.2f) for %s (home %.2f, there %.2f, spare %.1f) ratio %.2f n %.2f gain %.2f fresh %.2f net %.2f",
                  s.name, hid, X, aX, o.name, bX, H, aH, sl, sp, ratio, n, gain, w, net) }
              end
            end
          end
        end
      end
    end
  end
end
table.sort(rows, function(a, b) return a.net > b.net end)
for i = 1, math.min(8, #rows) do print(rows[i].txt) end
