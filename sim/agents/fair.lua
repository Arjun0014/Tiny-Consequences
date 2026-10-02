-- Fairs (S19, roadmap v3 R4: exchange and travel). A fair is people physically gathering: after its
-- harvest a village that can feed its guests and lies at the middle of the villages it knows by road
-- calls a fair some days ahead; the news travels only with people (anyone who meets someone of a
-- village that has heard passes it on - econ.learn's hook - and the host sends a crier to each road
-- neighbour that has not heard); each household that hears weighs going - what it could sell and
-- wants, kin there, a son or daughter with nobody to marry at home, the road - and sends one or two
-- of its people with a pack of goods; they walk there, gather at the host's square for the fair's
-- hours, barter person to person (what each brought, each by their own household's worth), hear the
-- news of every village there, meet kin and strangers (a match may be made: the bride goes home with
-- the groom's people when the fair ends), and walk home with what they got.
--   s.fair = { host, t0, t1, called, year, present = { pid } }   (the host's fair)
--   s.fairs[host id] = { t0, t1, heard, cried }                  (fairs village s has heard of)
--   hh.fair = { host, t0, t1, who = { pid }, why, decided }      (a household going)
--   p.trip = { to = host, from, fair = host, t0, t1 }
local cfgmod = require("sim.config")
local clock = require("sim.agents.clock")
local people = require("sim.agents.people")
local econ = require("sim.agents.econ")
local trade = require("sim.agents.trade")
local sfmt = require("sim.fmt").format

local M = {}
local floor, max, min, sqrt, abs = math.floor, math.max, math.min, math.sqrt, math.abs
local function C() return cfgmod.V3 end

-- The goods taken to a fair (household goods to spare; the relish the village spares) in the order
-- they are weighed.
local FAIR_G = { "wool", "cloth", "hides", "leather", "salt", "smoked", "cheese", "grain" }
M.FAIR_G = FAIR_G

-- Is village s's fair on now (or soon)?
function M.on(s, now) local f = s.fair; return f and now >= f.t0 and now < f.t1 end

-- The host among village s and the villages it knows by road: the one with the least road to the
-- others (ties: the lower id).
local function best_host(W, s, partners)
  local set = { s }
  for _, o in ipairs(partners) do set[#set + 1] = o end
  local best, bs = nil, 1e9
  for _, a in ipairs(set) do
    local sum = 0
    for _, b in ipairs(set) do
      if a ~= b then
        local _, hrs = trade.road(W, a, b)
        sum = sum + (hrs or 999)
      end
    end
    if sum < bs - 1e-9 or (abs(sum - bs) < 1e-9 and a.id < best.id) then best, bs = a, sum end
  end
  return best
end

-- Village s hears of fairs from village o (they met: econ.learn): o's own and what o heard.
function M.heard(W, s, o, now)
  if s == o or not o.fairs then return end
  s.fairs = s.fairs or {}
  local keys = {}
  for host in pairs(o.fairs) do keys[#keys + 1] = host end
  table.sort(keys)
  for _, host in ipairs(keys) do
    local f = o.fairs[host]
    if now < f.t1 and not s.fairs[host] and W.S.v3.settlements[host] then
      s.fairs[host] = { t0 = f.t0, t1 = f.t1, heard = now, from = o.id }
      local hs = W.S.v3.settlements[host]
      for _, id in ipairs(s.members) do
        local q = W.S.v3.people[id]
        if q and q.alive and people.stage(q, now) == "adult" and (q.at == nil or q.at == s.id) then
          people.remember(q, now, sfmt("Heard from the people of %s that there will be a fair at %s", o.name, hs.name))
          break
        end
      end
    end
  end
end

-- What household hh of s would gain by going to the fair at village h, in FU (rough): what it could
-- sell (its goods to spare at a middling price over their worth at home), what it wants (half of it),
-- kin there, someone of it to court, the company; less the road. And the goods it would take.
local function weigh(W, s, hh, home, h, hrs, now)
  local c = C()
  local V = W.S.v3
  local sell, take = 0, {}
  for _, g in ipairs(FAIR_G) do
    local G = econ.GOODS[g]
    local sp = econ.spare(W, s, hh, home, g)
    if G.pool == "village" then sp = min(sp, home.stock[g == "grain" and "food" or g] or 0) end
    if sp > 0.05 then
      local own = econ.value(W, s, hh, home, g)
      local gain = (G.base * c.fair_price_k - own) * sp
      if gain > 0 then sell = sell + gain; take[#take + 1] = { g, sp } end
    end
  end
  local want = 0
  for _, g in ipairs(FAIR_G) do
    local G = econ.GOODS[g]
    local v = econ.value(W, s, hh, home, g)
    if v > G.base * 1.1 then want = want + (v - G.base * c.fair_price_k) * min(1, 0.5 / G.wt) end
  end
  local kin, court, courter = 0, 0, {}
  for _, id in ipairs(hh.members) do
    local p = V.people[id]
    if p and p.alive then
      for _, k in ipairs({ p.mother, p.father }) do
        local q = V.people[k or 0]
        if q and q.alive and q.sett == h.id and h ~= s then kin = kin + 1 end
      end
      for _, k in ipairs(p.kids or {}) do
        local q = V.people[k]
        if q and q.alive and q.sett == h.id and h ~= s then kin = kin + 1 end
      end
      local st, a = people.stage(p, now)
      if st == "adult" and p.partner == 0 and a < 40 and a >= (p.sex == "f" and c.marry_f or c.marry_m) then
        court = court + 1
        courter[#courter + 1] = p
      end
    end
  end
  local u = sell * 0.6 + min(want, 10) * 0.5 + min(3, kin) * c.fair_kin + min(1, court) * c.fair_court + c.fair_company
    - 2 * hrs * c.trade_hour * max(c.trade_idle_min, 1 - c.trade_idle_k * (s.idle_avg or 0)) * 1.5
  return u, take, kin, courter, sell
end

-- Each morning (settle.daily): fairs that are over forgotten; a fair called; households deciding.
function M.daily(W, s, now)
  local c = C()
  local V = W.S.v3
  s.fairs = s.fairs or {}
  local keys = {}
  for host in pairs(s.fairs) do keys[#keys + 1] = host end
  table.sort(keys)
  for _, host in ipairs(keys) do if now > s.fairs[host].t1 + 24 then s.fairs[host] = nil end end
  if s.fair and now > s.fair.t1 + 24 then s.fair = nil end
  -- calling a fair: after the harvest, at the middle of the villages it knows by road, with food to
  -- feed its guests, once a year
  local doy = clock.local_doy(now, W.grid.lat[s.cell])
  local year = floor((floor(now / 24) + (W.grid.lat[s.cell] < 0 and 24 or 0)) / 48)
  if not s.fair and s.fair_year ~= year and doy >= c.fair_doy[1] and doy <= c.fair_doy[2] and (s.need.food_days or 0) >= c.fair_food_days
    and s.granary and s.granary.state == "standing" and #s.members >= 8 then
    local partners = {}
    for _, o in ipairs(trade.partners(W, s)) do
      local _, hrs = trade.road(W, s, o)
      if hrs <= c.fair_reach and o.alive ~= false and #o.members > 0 then partners[#partners + 1] = o end
    end
    if #partners > 0 and best_host(W, s, partners) == s then
      s.fair_year = year
      -- (the first morning at least fair_notice days on, at fair_open local)
      local lh = clock.local_hour(now, W.grid.lon[s.cell])
      local t0 = now + c.fair_notice * 24 + (c.fair_open - lh) % 24
      s.fair = { host = s.id, t0 = t0, t1 = t0 + c.fair_h, called = now, year = year, present = {}, deals = 0, met = {} }
      s.fairs[s.id] = { t0 = t0, t1 = t0 + c.fair_h, heard = now }
      -- criers: to each road neighbour within reach
      s.fair_cry = {}
      for _, o in ipairs(partners) do s.fair_cry[#s.fair_cry + 1] = o.id end
      s.acc.fairs = (s.acc.fairs or 0) + 1
      for _, id in ipairs(s.members) do
        local q = V.people[id]
        if q and q.alive and people.stage(q, now) == "adult" then
          people.remember(q, now, sfmt("The harvest is in: we will hold a fair here in %d days, and send word to our neighbours", c.fair_notice))
        end
      end
    end
  end
  -- households deciding to go to the fairs they heard of
  for _, host in ipairs(keys) do
    local f = s.fairs[host]
    local h = V.settlements[host]
    if f and h and now < f.t0 then
      local hrs = 0
      if h ~= s then local _; _, hrs = trade.road(W, s, h) end
      for _, hid in ipairs(s.households) do
        local hh = V.households[hid]
        local home = hh and #hh.members > 0 and s.buildings[hh.home or 0]
        if home and not (hh.fair and hh.fair.host == host) and not hh.migrate and not hh.found and (hrs or 999) <= c.fair_reach then
          local u, take, kin, courter, sell = weigh(W, s, hh, home, h, hrs, now)
          if u > 0 then
            -- who goes: the household's most sociable grown-up who can be away (no babe at the breast),
            -- and whoever of it has nobody to marry at home
            local who, lead = {}, nil
            for _, id in ipairs(hh.members) do
              local p = V.people[id]
              local st = p and p.alive and people.stage(p, now)
              if st == "adult" and (p.nursing or 0) <= f.t1 and (p.preg_due or 0) == 0 and not p.move then
                if not lead or p.soc > lead.soc or (p.soc == lead.soc and p.id < lead.id) then lead = p end
              end
            end
            if lead then who[1] = lead.id end
            for _, p in ipairs(courter) do
              if #who < c.fair_party and p ~= lead and not p.move then who[#who + 1] = p.id end
            end
            if #who > 0 then
              local why = {}
              if sell > 0 then why[#why + 1] = "to sell what we have to spare" end
              if kin > 0 then why[#why + 1] = "to see our kin" end
              if #courter > 0 then why[#why + 1] = "to find a husband or a wife" end
              if #why == 0 then why[#why + 1] = "for the company and the news" end
              hh.fair = { host = host, t0 = f.t0, t1 = f.t1, who = who, decided = now, take = take,
                why = sfmt("The fair at %s: %s", h.name, table.concat(why, ", ")) }
              for _, id in ipairs(who) do
                local p = V.people[id]
                -- (each says their own reason: the one with nobody to marry, the household's for the rest)
                local mine = {}
                for _, w in ipairs(why) do if w ~= "to find a husband or a wife" then mine[#mine + 1] = w end end
                for _, q in ipairs(courter) do if q == p then mine[#mine + 1] = p.sex == "f" and "to find a husband" or "to find a wife" end end
                if #mine == 0 then mine[1] = "for the company and the news" end
                people.remember(p, now, sfmt("We will go to the fair at %s %s", h.name, table.concat(mine, ", ")))
              end
            end
          end
        end
      end
    end
  end
end

-- (S19) The worth of good g to person p's household (at its own village).
local function worth_to(W, p, g)
  local V = W.S.v3
  local s = V.settlements[p.sett]
  local hh = V.households[p.hh]
  local home = hh and s and s.buildings[hh.home or 0]
  return econ.value(W, s, hh, home, g), s, hh, home
end

-- One barter at the fair between person p and the best partner among those present: one good of
-- each pack for one of the other's, at the middle of their worths, both gaining.
function M.barter(W, s, p, now)
  local c = C()
  local V = W.S.v3
  local f = s.fair
  if not f or not p.pack then return false end
  local best, bq
  for _, qid in ipairs(f.present) do
    local q = V.people[qid]
    if q and q ~= p and q.alive and q.act == "fair" and q.pack and q.sett ~= p.sett then
      for _, X in ipairs(FAIR_G) do
        local px = p.pack[X] or 0
        if px > 0.05 then
          local aX = worth_to(W, p, X)
          local bX = worth_to(W, q, X)
          for _, Y in ipairs(FAIR_G) do
            local qy = q.pack[Y] or 0
            if Y ~= X and qy > 0.05 then
              local aY = worth_to(W, p, Y)
              local bY = worth_to(W, q, Y)
              if bX > aX * c.trade_margin and aY > bY * c.trade_margin then
                local r = (bX / aX) * (aY / bY)
                if not best or r > best.r then best = { q = q, X = X, Y = Y, aX = aX, bX = bX, aY = aY, bY = bY, r = r } end
              end
            end
          end
        end
      end
    end
  end
  if not best then return false end
  local q, X, Y = best.q, best.X, best.Y
  local k = sqrt(best.aX * best.bX / (best.aY * best.bY))   -- (units of Y for one of X)
  local n = min(p.pack[X], q.pack[Y] / k)
  if n * econ.GOODS[X].wt < 0.02 then return false end
  p.pack[X], q.pack[X] = p.pack[X] - n, (q.pack[X] or 0) + n
  q.pack[Y], p.pack[Y] = q.pack[Y] - n * k, (p.pack[Y] or 0) + n * k
  -- (food changing hands changes the villages' books)
  local sp, sq = V.settlements[p.sett], V.settlements[q.sett]
  if econ.GOODS[X].food then sp.acc.export = (sp.acc.export or 0) + n; sq.acc.import = (sq.acc.import or 0) + n end
  if econ.GOODS[Y].food then sq.acc.export = (sq.acc.export or 0) + n * k; sp.acc.import = (sp.acc.import or 0) + n * k end
  f.deals = (f.deals or 0) + 1
  s.acc.fair_deals = (s.acc.fair_deals or 0) + 1
  people.remember(p, now, sfmt("At the fair at %s: %s for %s with %s of %s", s.name, econ.amount(n, X), econ.amount(n * k, Y), q.name, sq.name))
  people.remember(q, now, sfmt("At the fair at %s: %s for %s with %s of %s", s.name, econ.amount(n * k, Y), econ.amount(n, X), p.name, sp.name))
  people.bond(p, q, 0.04)
  people.bond(q, p, 0.04)
  return true
end

-- Talk at the fair: the news of the villages there (each pair once), kin met, strangers met.
function M.mingle(W, s, p, now)
  local V = W.S.v3
  local f = s.fair
  if not f then return end
  local home = V.settlements[p.sett]
  local met = 0
  for _, qid in ipairs(f.present) do
    local q = V.people[qid]
    if q and q ~= p and q.alive and q.act == "fair" and met < 3 then
      local qs = V.settlements[q.sett]
      if qs and qs ~= home then
        local key = min(home.id, qs.id) * 1000 + max(home.id, qs.id)
        if not f.met[key] then
          f.met[key] = true
          econ.learn(W, home, qs, now)
          econ.learn(W, qs, home, now)
        end
        if people.is_kin(W, p, q.id) and q.id ~= p.partner and not (p.fair_met and p.fair_met[q.id]) then
          p.fair_met = p.fair_met or {}
          p.fair_met[q.id] = true
          people.remember(p, now, sfmt("Met %s of %s at the fair, my kin", q.name, qs.name))
        end
        people.bond(p, q, 0.02)
        met = met + 1
      end
    end
  end
end

-- A match at the fair: p (single, of an age) and a single of the other sex from another village who
-- take to each other; the bride goes home with the groom's people when the fair ends.
function M.court(W, s, p, now)
  local c = C()
  local V = W.S.v3
  local f = s.fair
  if not f or p.partner ~= 0 or p.move then return end
  local st, a = people.stage(p, now)
  if st ~= "adult" or a >= 40 or a < (p.sex == "f" and c.marry_f or c.marry_m) then return end
  local best, bs = nil, -1e9
  for _, qid in ipairs(f.present) do
    local q = V.people[qid]
    if q and q.alive and q.act == "fair" and q.sex ~= p.sex and q.partner == 0 and not q.move and q.sett ~= p.sett
      and not people.is_kin(W, p, q.id) then
      local s2, a2 = people.stage(q, now)
      if s2 == "adult" and a2 < 40 and a2 >= (q.sex == "f" and c.marry_f or c.marry_m) then
        local m, w = p.sex == "m" and p or q, p.sex == "m" and q or p
        local sc = people.affinity(p, q.id) * 2 - abs(people.age(w, now) + 2.5 - people.age(m, now)) * 0.08
          + require("sim.rng").float(W.seed, require("sim.rng").S.V3_THINK, p.id * 131 + q.id, floor(now / 24) % 4294967296, 141) * 0.6
        if sc > bs then best, bs = q, sc end
      end
    end
  end
  if not best or bs < c.fair_match then return end
  local groom, bride = p.sex == "m" and p or best, p.sex == "m" and best or p
  local gs, bsett = V.settlements[groom.sett], V.settlements[bride.sett]
  if not groom.trip then return end
  require("sim.agents.act").match_away(W, bsett, gs, groom, bride, now)
  -- (she goes home with her own people when the fair ends, and a day later walks to his village, as
  -- brides do)
  local _, hb = trade.road(W, s, bsett)
  local dep = f.t1 + (s == bsett and 0 or (hb or 0) * 1.8) + 24
  if bride.move then bride.move.depart = dep end
  for _, kid in ipairs(V.households[bride.hh] and V.households[bride.hh].members or {}) do
    local q = V.people[kid]
    if q and q.move and q.move.mother == bride.id then q.move.depart = dep end
  end
  s.acc.fair_matches = (s.acc.fair_matches or 0) + 1
  people.remember(groom, now, sfmt("At the fair at %s I met %s of %s: we will marry", s.name, bride.name, bsett.name))
  people.remember(bride, now, sfmt("At the fair at %s I met %s of %s: we will marry and I will go with him to %s", s.name, groom.name, gs.name, gs.name))
end

return M
