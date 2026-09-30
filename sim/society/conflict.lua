-- Conflict: grievances, raids and defence (docs/world/18 §2.4, W8, D-057).
--
-- Grievance (per pair of villages, relations W.ent.rel: ga = the lower id's grievance against the
-- higher, gb the other way, gea/geb = the event of the last wrong): raids suffered and kin killed,
-- help refused by a village with plenty; it fades slowly and gifts wash some of it away.
--
-- Raids (monthly decision): a village in want (rationing or famine that relief did not end, land
-- too short to feed it) or nursing a grievance, with enough young men, may send a war party
-- against a neutral or hostile village within a few days' walk whose store is worth the risk.
-- The leader's temper matters (status.temper). The raiders are real people walking there
-- (a mobility party). At dawn they fall on the village: the fight follows the numbers on each
-- side (the defenders' men, the palisade if one stands, surprise) — Lanchester's square law;
-- both sides lose people (deaths tagged with the raid event), the winners carry off grain and
-- sometimes fire a house; the raiders walk home with the loot. The raided village remembers:
-- grievance, a palisade (construction), households drawn to bigger or walled villages
-- (mobility), revenge when it can. Feuds are emergent cycles, not scripts.
local cfgmod = require("sim.config")
local rng = require("sim.rng")
local events = require("sim.causality.events")
local ents = require("sim.society.entities")
local sites = require("sim.society.sites")
local sea = require("sim.society.sea")
local cotag = require("sim.society.cotag")

local M = {}
local floor, min, max, ceil, sqrt = math.floor, math.min, math.max, math.ceil, math.sqrt

-- Set by sim/society/init.lua (callbacks: no require cycles).
M.depart = function() end
M.rel = nil          -- sim/society/relations.lua
M.herds = require("sim.society.herds")
M.status = nil       -- sim/society/status.lua
-- A house fired in a raid (the first standing house by the village's building order).
function M.burn(W, u, share)
  local B = W.ent.buildings
  local H = W.ent.households
  for i = 1, #u.buildings do
    local b = B[u.buildings[i]]
    if b and b.state == "standing" and (b.kind == "house" or b.kind == "hut" or b.kind == "granary") then
      b.condition = b.condition - share
      if b.condition <= 0 then
        b.state = "ruin"
        b.ruined = W.hour
        local h = H[b.household]
        if h then h.home = 0 end
        b.household = 0
        u.hh_dirty = true
      end
      return b
    end
  end
end

local function emit(W, kind, cell, parent, mag, data, v, quiet)
  if not W.record_events then return 0 end
  local co = v and cotag.co_of(W, v)
  local opts = nil
  if co or quiet then
    opts = { co = co }
    if quiet then opts.headline = false end
  end
  return events.emit(W, kind, cell, parent, mag, data, opts)
end

-- News (the player's headlines): the first blow between two villages is news; the raids that
-- follow it within news_years are the feud's, told in the villages' chronicles (still recorded,
-- still in every chain). A truce, a submission, a secession stay headlines. The social
-- counterpart of nature's regional news fatigue (D-040).
local function old_news(W, a, b)
  local r = M.rel.get(W, a, b, true)
  local last = r.news
  if last and W.hour - last < cfgmod.CONFLICT.news_years * 8064 then return true end
  r.news = W.hour
  return false
end

-- A village's reach as a set of cells (derived, cached on its geometry).
local function ringset(G)
  local set = G.ringset
  if not set then
    set = {}
    for k = 1, #G.ring do set[G.ring[k]] = true end
    G.ringset = set
  end
  return set
end

-- Grievance of village a against village b (and the event behind it).
function M.grievance(W, a, b)
  local r = M.rel.get(W, a, b)
  if not r then return 0, 0 end
  if a < b then return r.ga or 0, r.gea or 0 end
  return r.gb or 0, r.geb or 0
end

-- Add grievance of village a against village b, remembering event ev.
function M.wrong(W, a, b, x, ev)
  local r = M.rel.get(W, a, b, true)
  if a < b then
    r.ga = max(0, (r.ga or 0) + x)
    if ev and ev ~= 0 then r.gea = ev end
  else
    r.gb = max(0, (r.gb or 0) + x)
    if ev and ev ~= 0 then r.geb = ev end
  end
end

-- Village v claimed field c, land that other villages also reached: where land is short, they
-- hold it against v (the field by the river was theirs to take).
function M.encroach(W, v, c)
  local cfg = cfgmod.CONFLICT
  local vs = W.ent.villages
  for i = 1, #vs do
    local u = vs[i]
    if u ~= v and u.alive and u.intensify and W.vgeo[u.id] then
      if ringset(W.vgeo[u.id])[c] then M.wrong(W, u.id, v.id, cfg.encroach_grievance, 0) end
    end
  end
end

-- Rivals for the land's and the sea's gifts (monthly, session 5). A village whose pasture is
-- overgrazed (more beasts than its grass carries: they are dying of it) holds it against the
-- neighbours whose herds graze the same land; one whose fishing grounds are fished well below
-- their recent state, against the villages whose boats fish them too. The grudge grows with the
-- shortfall, the share of the land (grounds) shared and the rival's share of the take (herds,
-- boats). When it first turns bitter (past 1) a dispute is recorded — its parent the cause of the
-- shortfall (a drought on the grass, a fish collapse) — and a raid it leads to follows from it.
function M.rivals(W, v)
  local cfg = cfgmod.CONFLICT
  local over = (v.herd_crowd or 0) - 1
  if over > 1 then over = 1 end
  -- the village's fishing grounds against their recent state
  local fshort = 0
  local grounds = v.grounds
  local fref = W.S.ocean and W.S.ocean.fish_ref
  if grounds and #grounds > 0 and fref and #v.boats > 0 then
    local f, r = 0, 0
    local fish = W.F.fish
    for i = 1, #grounds do f = f + fish[grounds[i]]; r = r + fref[grounds[i]] end
    if r > 0 then
      fshort = 1 - (f / r) / cfg.fish_rival
      if fshort < 0 then fshort = 0 end
    end
  end
  if over <= 0.05 and fshort <= 0 then return end
  local G = W.vgeo[v.id]
  if not G then return end
  local vs = W.ent.villages
  local ocean, land_use = W.geo.ocean, W.F.land_use
  local se_v = v.herd and M.herds.se(v.herd) or 0
  -- the village's own pasture cells (land, not fields)
  local nland = 0
  if over > 0.05 then
    for k = 1, #G.ring do local c = G.ring[k]; if ocean[c] == 0 and land_use[c] == 0 then nland = nland + 1 end end
  end
  for i = 1, #vs do
    local u = vs[i]
    if u ~= v and u.alive and u.continent == v.continent and W.vgeo[u.id] then
      local g, parent, kind = 0, 0, nil
      if over > 0.05 and nland > 0 and u.herd then
        local se_u = M.herds.se(u.herd)
        if se_u > 1 then
          local set = ringset(W.vgeo[u.id])
          local shared = 0
          for k = 1, #G.ring do
            local c = G.ring[k]
            if set[c] and ocean[c] == 0 and land_use[c] == 0 then shared = shared + 1 end
          end
          if shared > 0 then
            g = g + cfg.pasture_grievance * over * (shared / nland) * se_u / (se_u + se_v)
            kind = "grazing_dispute"
            parent = W.C.grass[v.herd_cell or v.cell] or 0
          end
        end
      end
      if fshort > 0 and u.grounds and #u.boats > 0 then
        local shared = 0
        local ug = u.grounds
        for a = 1, #grounds do
          for b = 1, #ug do if ug[b] == grounds[a] then shared = shared + 1; break end end
        end
        if shared > 0 then
          local x = cfg.fish_grievance * fshort * (shared / #grounds) * #u.boats / (#u.boats + #v.boats)
          if x > g * 0.5 then kind = "fishing_dispute"; parent = W.C.fish[grounds[1]] or 0 end
          g = g + x
        end
      end
      if g > 0 then
        local before = M.grievance(W, v.id, u.id)
        local ev = 0
        if before <= 1 and before + g > 1 then
          ev = emit(W, kind, v.cell, parent, 1, { village = v.id, other = u.id }, v)
        end
        M.wrong(W, v.id, u.id, g, ev)
      end
    end
  end
end

-- Monthly: grievances fade (called once per relation by its lower-id village).
function M.fade(W, v)
  local vs = W.ent.villages
  local cfg = cfgmod.CONFLICT
  local k = cfg.grievance_keep
  for i = v.id + 1, #vs do
    local r = M.rel.get(W, v.id, i)
    if r then
      if r.ga then r.ga = r.ga * k; if r.ga < 0.05 then r.ga, r.gea = nil, nil end end
      if r.gb then r.gb = r.gb * k; if r.gb < 0.05 then r.gb, r.geb = nil, nil end end
      -- a feud (both sides wronged, blood or beasts lost on both) may end in a truce: the
      -- leaders meet, the dead are paid for, the grudges are set aside for a generation
      local u = vs[i]
      if (r.ga or 0) > 0.5 and (r.gb or 0) > 0.5 and u.alive and v.alive and (r.feud or 0) >= cfg.truce_after then
        local g1, w1 = M.status.temper(W, v)
        local g2, w2 = M.status.temper(W, u)
        local p = cfg.truce_prob * (r.feud - cfg.truce_after + 1) * (2 / (w1 + w2))
        if rng.float(W.seed, rng.S.CONFLICT, v.key_cell * 4096 + u.key_cell % 4096, floor(W.hour / 672), 21) < p then
          local ev = max(r.gea or 0, r.geb or 0)
          local id = emit(W, "truce", v.cell, ev, r.feud, { village = v.id, other = u.id, raids = r.feud }, nil)
          r.ga, r.gb = (r.ga or 0) * 0.15, (r.gb or 0) * 0.15
          r.feud = 0
          r.truce_until = W.hour + cfg.truce_years * 8064
          r.truce_ev = id
          -- the leaders who made peace are honoured
          M.status.honour_leader(W, v, cfg.truce_prestige)
          M.status.honour_leader(W, u, cfg.truce_prestige)
        end
      end
    end
  end
end

-- Under a truce? (no raids either way)
function M.truce(W, a, b)
  local r = M.rel.get(W, a, b)
  return r and r.truce_until and W.hour < r.truce_until
end

-- A raid between a and b: the feud's count (both ways), and revenge taken satisfies the raider.
local function feud(W, raider, target, won)
  local r = M.rel.get(W, raider.id, target.id, true)
  r.feud = (r.feud or 0) + 1
  if won then M.wrong(W, raider.id, target.id, -cfgmod.CONFLICT.revenge_satisfies, 0) end
end
M.feud = feud

-- Fighters of village u: men of fighting age, fit, at home.
local function fighters(W, u, out)
  local cfg = cfgmod.CONFLICT
  local P = W.ent.people
  local n = 0
  for i = 1, #u.members do
    local p = P[u.members[i]]
    if p.sex == "m" and p.away == 0 and p.health >= 50 then
      local a = (W.hour - p.born) / 8064
      if a >= cfg.fight_age[1] and a <= cfg.fight_age[2] then
        n = n + 1
        if out then out[n] = p end
      end
    end
  end
  return n
end
M.fighters = fighters

-- The count alone, kept for the week (the monthly raid and defence reckonings ask it of every
-- neighbour; people's health and absences change slowly).
local function nfighters(W, u)
  local wk = floor(W.hour / 168)
  if u.fight_wk ~= wk then u.fight_wk, u.fight_n = wk, fighters(W, u) end
  return u.fight_n
end
M.nfighters = nfighters

-- Allies who come to u's defence: its lord (bound to protect its tributaries) and villages that
-- hold it dear (kin, daughters, old friends) within a day's walk send a share of their fighting
-- men. Returns the allied men and the list { village, men } (scratch table `out`).
-- (the villages that would come to u's help, reckoned once a week: ids and men)
local function ally_base(W, u)
  local cfg = cfgmod.CONFLICT
  local sc = W.scratch
  if not sc then sc = {}; W.scratch = sc end
  local memo = sc.allies
  if not memo then memo = {}; sc.allies = memo end
  local wk = floor(W.hour / 168)
  local m = memo[u.id]
  if m and m.wk == wk then return m end
  m = { wk = wk }
  local vs = W.ent.villages
  for i = 1, #vs do
    local a = vs[i]
    if a ~= u and a.alive and a.continent == u.continent then
      local lord = u.lord == a.id
      local s = lord and 9 or M.rel.score(W, a, u)
      if s >= cfg.ally_score and sites.dist(W, a.cell, u.cell) <= cfg.ally_reach then
        local men = floor(nfighters(W, a) * (lord and cfg.ally_lord_share or cfg.ally_share))
        if men > 0 then m[#m + 1] = a.id; m[#m + 1] = men end
      end
    end
  end
  memo[u.id] = m
  return m
end

function M.allies(W, u, attacker, out)
  local vs = W.ent.villages
  local n = 0
  for i = #out, 1, -1 do out[i] = nil end
  local m = ally_base(W, u)
  for i = 1, #m, 2 do
    local a = vs[m[i]]
    if a ~= attacker and a.alive and not a.raid_out
      and not (attacker and (attacker.lord == a.id or a.lord == attacker.id)) then
      n = n + m[i + 1]
      out[#out + 1] = { a, m[i + 1] }
    end
  end
  return n, out
end

local ALLY = {}

-- The defence of village u against `attacker` (optional): its fighting men (and a little from
-- everyone else), the allies who come, its palisade.
function M.defence(W, u, attacker, exact)
  local cfg = cfgmod.CONFLICT
  local men = exact and fighters(W, u) or nfighters(W, u)
  local others = max(0, u.pop - men) * cfg.other_defence
  local allied = M.allies(W, u, attacker, ALLY)
  local d = men + others + allied
  local B = W.ent.buildings
  for i = 1, #u.buildings do
    local b = B[u.buildings[i]]
    if b and b.kind == "palisade" and b.state == "standing" and b.condition > 0.3 then
      d = d * (1 + (cfg.palisade - 1) * b.condition)
      break
    end
  end
  return d, men, allied
end

-- Monthly for village v: go raiding?
-- (diagnostics for tools: why villages did not raid; not state)
M.why = {}
local function why(k) M.why[k] = (M.why[k] or 0) + 1 end

function M.monthly(W, v)
  local cfg = cfgmod.CONFLICT
  if v.pop < cfg.min_pop or v.raid_out then why("small_or_out"); return end
  local hour = W.hour
  if hour - (v.raid_last or -1e9) < cfg.raid_gap_months * 672 then why("gap"); return end
  local list = {}
  local nf = fighters(W, v, list)
  if nf < cfg.min_raiders then why("fighters"); return end
  -- want: hunger that relief did not end, land too short, grievance (per target)
  local hunger = 0
  if v.famine then hunger = 1.5 elseif v.rationing then hunger = 1 end
  if hunger > 0 and hour - (v.relief_got or -1e9) < 3 * 672 then hunger = hunger * 0.3 end
  local land = (v.intensify and v.food_days < 120) and 0.5 or 0
  local _, war = M.status.temper(W, v)
  -- ambition (D-065): a big village whose leader is much respected seeks tribute and glory from
  -- weaker neighbours who are not its kin (early chiefdoms grew by it, not only from want)
  local amb = 0
  if v.pop >= cfg.amb_min_pop and v.leader_h then
    local lh = W.ent.households[v.leader_h]
    local pr = lh and lh.prestige or 0
    if pr > 0 then amb = cfg.ambition * min(1, pr / cfg.amb_prestige) end
  end
  local vs = W.ent.villages
  local best, bv, bp, bd, bg, bge, bA = nil, 0, 0, 0, 0, 0, 0
  local bm = nil   -- the best raid's strongest motive: "want", "revenge" or "ambition"
  local A0 = min(nf * cfg.raid_share, nf)
  -- raiders by sea: as many as the village's free boats seat
  local seats = v.mooring and sea.n_free(W, v) * cfgmod.SOCIETY.boat_seats or 0
  for i = 1, #vs do
    local u = vs[i]
    if u ~= v and u.alive and u.pop >= cfg.min_target and u.lord ~= v.id and v.lord ~= u.id and not M.truce(W, v.id, u.id) then
      local d, A = nil, A0
      if u.continent == v.continent then d = sites.dist(W, v.cell, u.cell)
      elseif seats >= cfg.min_raiders then
        A = min(A0, seats)
        d = sea.cost(W, v, u.cell, A)
      end
      if d and d <= cfg.raid_reach then
        local s = M.rel.score(W, v, u)
        local gr, gev = M.grievance(W, v.id, u.id)
        -- kin, daughters and friends are not raided unless wronged
        -- (hunger and want of land break the peace between kin)
        local limit = cfg.friend_score + (hunger + land) * cfg.want_breaks_ties
        if s >= limit and gr <= 1 then why("friend") end
        if s < limit or gr > 1 then
          local D = M.defence(W, u, v)
          local pwin = A * A / (A * A + D * D * cfg.home_advantage)
          local loot = min(u.stock.grain * cfg.loot_share, A * cfg.carry)
          local g_want = (hunger + land) * loot / max(1, v.need_day * 30)
          local g_rev = gr * cfg.revenge_weight
          local g_amb = 0
          -- (ambition only against the clearly weaker, and not those who already pay it)
          if amb > 0 and A > D * cfg.amb_edge and u.lord ~= v.id then g_amb = amb * min(2, A / max(1, D) - 1) end
          local gain = g_want + g_rev + g_amb
          local value = (pwin * gain - (1 - pwin) * cfg.risk) * war - d * 0.02
          if value > bv then
            best, bv, bp, bd, bg, bge, bA = u, value, pwin, d, gr, gev, A
            bm = (g_amb >= g_want and g_amb >= g_rev) and "ambition" or (g_rev > g_want and "revenge" or "want")
          end
        end
      end
    end
  end
  if hunger + land > 0 then why("wanting") end
  -- cattle raids (W8): a small party runs off a neighbour's herd from its pasture — for beasts
  -- the village lacks, bride-wealth for its young men, or revenge; the herders and whoever the
  -- alarm brings are all that stand in the way
  local cb, cv, cp, cd, cg, cge, cA = M.cattle_target(W, v, nf, hunger, war)
  if cb and cv > bv and cv >= cfg.cattle_threshold then
    best, bv, bp, bd, bg, bge, bA = cb, cv, cp, cd, cg, cge, cA
    bm = (cg > 1 and cge ~= 0) and "revenge" or "cattle"
    why("cattle")
  else
    cb = nil
  end
  if not best then why("no_target"); return end
  if bv < cfg.threshold then why("low_value"); return end
  if rng.float(W.seed, rng.S.CONFLICT, v.key_cell, floor(hour / 672), 1) > cfg.raid_prob then why("dice"); return end
  why("raid")
  -- the war party: the fittest of the fighting men
  table.sort(list, function(a, b) if a.health ~= b.health then return a.health > b.health end return a.id < b.id end)
  local party = {}
  for i = 1, max(cfg.min_raiders, floor(bA)) do party[i] = list[i] end
  -- why: the village's want, or the wrong it avenges
  -- (a raid for tribute and glory follows from the leader who seeks it: its chain starts at their
  -- accession)
  local reason
  if bm == "ambition" and v.leader_ev then reason = v.leader_ev
  elseif bg > 1 and bge ~= 0 then reason = bge
  else reason = v.foodtag[1] ~= 0 and v.foodtag[1] or v.cause end
  v.raid_out = true
  v.raid_last = hour
  M.depart(W, v, party, best.id, nil, "raid", reason, 0, 0, { target = best.id, home = v.id, pwin = bp,
    cattle = cb and true or nil, motive = bm })
end

-- The herd's guard at village u: its herders on the pasture and the men the alarm brings.
local function herd_guard(W, u)
  local cfg = cfgmod.CONFLICT
  local h = u.herd
  if not h then return 0 end
  local se = M.herds.se(h)
  -- (herders are mostly boys and girls: half a fighter each)
  return se / cfgmod.HERDS.herder_se * 0.5 + nfighters(W, u) * cfg.cattle_alarm
end

-- The best cattle raid for v: returns target, value, pwin, distance, grievance, its event, raiders.
function M.cattle_target(W, v, nf, hunger, war)
  local cfg = cfgmod.CONFLICT
  local hc = cfgmod.HERDS
  -- motive: beasts lacking against the herd the village wants, bachelors wanting bride-wealth
  local se_v = v.herd and M.herds.se(v.herd) or 0
  local want_se = v.herd_target or (v.pop * hc.per_person[1])
  local lack = want_se > 0 and max(0, 1 - se_v / want_se) or 0
  local P = W.ent.people
  local H = W.ent.households
  local mean_b = 0
  for i = 1, #v.households do local hh = H[v.households[i]]; if hh then mean_b = mean_b + (hh.beasts or 0) end end
  mean_b = #v.households > 0 and mean_b / #v.households or 0
  local bach = 0
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p.sex == "m" and p.partner == 0 and p.away == 0 then
      local a = (W.hour - p.born) / 8064
      -- (young men whose families cannot find the bride-wealth, D-061)
      local h = H[p.household]
      if a >= 16 and a < 30 and (not h or (h.beasts or 0) * hc.bride_share < hc.bride_price * mean_b * 0.5) then bach = bach + 1 end
    end
  end
  local motive = lack * cfg.cattle_lack + min(1, bach / max(1, nf)) * cfg.cattle_bachelors + hunger * 0.5
  local A = min(cfg.cattle_party, floor(nf * cfg.cattle_share))
  if A < cfg.min_raiders then return nil end
  local vs = W.ent.villages
  local best, bv, bp, bd, bg, bge = nil, 0, 0, 0, 0, 0
  for i = 1, #vs do
    local u = vs[i]
    if u ~= v and u.alive and u.herd and u.lord ~= v.id and v.lord ~= u.id and u.continent == v.continent
      and not M.truce(W, v.id, u.id) then
      local d = sites.dist(W, v.cell, u.cell)
      local se_u = M.herds.se(u.herd)
      if d <= cfg.cattle_reach and se_u >= cfg.cattle_min_se then
        local s = M.rel.score(W, v, u)
        local gr, gev = M.grievance(W, v.id, u.id)
        if s < cfg.friend_score + hunger * cfg.want_breaks_ties or gr > 1 then
          local dh = herd_guard(W, u)
          local pwin = A * A * cfg.cattle_surprise / (A * A * cfg.cattle_surprise + dh * dh * cfg.home_advantage)
          local take = min(se_u * hc.raid_take, A * cfg.cattle_per_raider)
          local gain = motive * take / max(1, v.pop * 0.5) + gr * cfg.revenge_weight
          local value = (pwin * gain - (1 - pwin) * cfg.cattle_risk) * war - d * 0.02
          if value > bv then best, bv, bp, bd, bg, bge = u, value, pwin, d, gr, gev end
        end
      end
    end
  end
  return best, bv, bp, bd, bg, bge, A
end

-- A war party reaches its target (mobility): the fight.
function M.battle(W, party, living)
  local cfg = cfgmod.CONFLICT
  local E = W.ent
  local P = E.people
  local home = E.villages[party.cargo.home]
  local u = E.villages[party.cargo.target]
  if not u or not u.alive or #living == 0 then
    if home then M.home_again(W, party, living, home, 0) end
    return
  end
  local hour = W.hour
  local A = 0
  for i = 1, #living do A = A + living[i].health / 100 end
  if party.cargo.cattle then return M.cattle_raid(W, party, living, home, u, A) end
  local D, men, allied = M.defence(W, u, home, true)
  local allies = {}
  for i = 1, #ALLY do allies[i] = ALLY[i] end
  local key = party.key
  local r1 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 2)
  local r2 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 3)
  local r3 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 4)
  local pwin = A * A * cfg.surprise / (A * A * cfg.surprise + D * D * cfg.home_advantage)
  local win = r1 < pwin
  -- losses: each side loses in proportion to the other's strength (and more when it loses)
  local la = floor(#living * (0.05 + 0.2 * r2) * (win and 0.6 or 1.5) * min(2, D / max(1, A)) + 0.5)
  local ld = floor((men + allied) * (0.05 + 0.2 * r3) * (win and 1.4 or 0.5) * min(2, A / max(1, D)) + 0.5)
  -- the allies' share of the dead
  local ld_allies = allied > 0 and floor(ld * allied / (men + allied) + 0.5) or 0
  ld = ld - ld_allies
  if la > #living then la = #living end
  local loot = 0
  local beasts = nil
  if win then
    loot = min(u.stock.grain * cfg.loot_share * (0.6 + 0.4 * r2), #living * cfg.carry)
    -- and the beasts they can drive off
    local h = u.herd
    if h then
      local take = cfgmod.HERDS.raid_take * (0.6 + 0.4 * r3)
      beasts = {}
      for _, k in ipairs({ "sheep", "goats", "cattle" }) do
        local x = (h[k] or 0) * take
        h[k] = (h[k] or 0) - x
        beasts[k] = x
      end
    end
  end
  local parent = party.reason or 0
  local id = emit(W, win and "raid" or "raid_repelled", u.cell, parent, max(1, floor(loot)),
    { village = u.id, from = home and home.id or 0, raiders = #living, dead_raiders = la, dead_defenders = ld,
      loot = floor(loot), allies = #allies, allied_men = allied, dead_allies = ld_allies }, u,
    home and old_news(W, home.id, u.id))
  -- the allies: their dead, their grudge against the raiders, the help remembered
  for i = 1, #allies do
    local a, m = allies[i][1], allies[i][2]
    local k = ld_allies > 0 and floor(ld_allies * m / max(1, allied) + 0.5) or 0
    if k > 0 then
      local list = {}
      local n = fighters(W, a, list)
      table.sort(list, function(x, y) return (x.u + x.id * 1e-9) < (y.u + y.id * 1e-9) end)
      for q = 1, min(k, n) do ents.kill(W, list[q], "raid", id) end
      ents.compact(W, a)
    end
    if home then M.wrong(W, a.id, home.id, cfg.ally_grievance + k * 0.5, id) end
    M.rel.helped(W, a.id, u.id, m * cfg.ally_days)
    a.acc.allied = (a.acc.allied or 0) + 1
  end
  -- the dead: raiders (the last in the party) and defenders (fighting men, the youngest last)
  local dead_home = 0
  for i = #living, #living - la + 1, -1 do
    if living[i] then ents.kill(W, living[i], "raid", id); dead_home = dead_home + 1 end
  end
  -- (raiders stay members of their village while away: the dead leave its lists)
  if dead_home > 0 and home then ents.compact(W, home) end
  local kept = {}
  for i = 1, #living - la do kept[i] = living[i] end
  if ld > 0 then
    local list = {}
    local n = fighters(W, u, list)
    table.sort(list, function(a, b) return (a.u + a.id * 1e-9) < (b.u + b.id * 1e-9) end)
    for i = 1, min(ld, n) do ents.kill(W, list[i], "raid", id) end
    ents.compact(W, u)
  end
  -- the store, and a house fired now and then
  if loot > 0 then
    u.stock.grain = u.stock.grain - loot
    u.foodtag[1], u.foodidle[1] = id, 0
    u.cause = id
    u.acc.raided_loss = (u.acc.raided_loss or 0) + loot
    if r3 < cfg.burn_chance then M.burn(W, u, cfg.burn_damage) end
  end
  u.raided = hour
  u.raid_tag = id
  u.acc.raids_suffered = (u.acc.raids_suffered or 0) + 1
  -- memory and standing
  if home then
    feud(W, home, u, win)
    M.wrong(W, u.id, home.id, cfg.raid_grievance + ld * 0.5, id)
    if dead_home > 0 then M.wrong(W, home.id, u.id, dead_home * 0.5, id) end
    M.protect(W, u, home, id)
    if win then
      M.status.honour_leader(W, home, cfg.win_prestige)
      M.status.honour_leader(W, u, -cfg.win_prestige * 0.5)
      M.defeated(W, u, home, id)
    else
      M.status.honour_leader(W, u, cfg.win_prestige)
      M.status.honour_leader(W, home, -cfg.win_prestige * 0.5)
    end
    for i = 1, #kept do
      local p = kept[i]
      if win then M.status.honour(W, p.household, cfg.raider_prestige) end
    end
    M.home_again(W, party, kept, home, loot, id, beasts)
  end
end

-- The raiders walk home (with the loot).
function M.home_again(W, party, living, home, loot, ev, beasts)
  if #living == 0 or not home.alive then
    if home then home.raid_out = nil end
    return
  end
  M.depart(W, W.ent.villages[party.cargo.target] or home, living, home.id, nil, "raid_home", ev or party.reason, 0, 0,
    { grain = loot, home = home.id, beasts = beasts }, party.path[#party.path])
end

-- Home again: the loot goes into the store.
-- A cattle raid reaches the pasture: the herders and the alarm against the raiders; beasts
-- driven off when they win (no grain, no fire).
function M.cattle_raid(W, party, living, home, u, A)
  local cfg = cfgmod.CONFLICT
  local hour = W.hour
  local key = party.key
  local r1 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 12)
  local r2 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 13)
  local r3 = rng.float(W.seed, rng.S.CONFLICT, key, hour, 14)
  local dh = herd_guard(W, u)
  local pwin = A * A * cfg.cattle_surprise / (A * A * cfg.cattle_surprise + dh * dh * cfg.home_advantage)
  local win = r1 < pwin
  local la = floor(#living * (0.03 + 0.12 * r2) * (win and 0.5 or 1.3) * min(2, dh / max(1, A)) + 0.5)
  local ld = floor(dh * (0.03 + 0.12 * r3) * (win and 1.2 or 0.4) * min(2, A / max(1, dh)) + 0.5)
  if la > #living then la = #living end
  local beasts, took = nil, 0
  if win and u.herd then
    local h = u.herd
    local se_u = M.herds.se(h)
    local f = se_u > 0 and min(cfgmod.HERDS.raid_take * (0.6 + 0.4 * r2), #living * cfg.cattle_per_raider / se_u) or 0
    beasts = {}
    for _, k in ipairs({ "sheep", "goats", "cattle" }) do
      local x = (h[k] or 0) * f
      h[k] = (h[k] or 0) - x
      beasts[k] = x
    end
    took = se_u * f
  end
  local id = emit(W, win and "cattle_raid" or "cattle_raid_repelled", u.herd_cell or u.cell, party.reason or 0,
    max(1, floor(took)), { village = u.id, from = home and home.id or 0, raiders = #living, dead_raiders = la,
    dead_defenders = ld, beasts = floor(took) }, u, home and old_news(W, home.id, u.id))
  local dead_home = 0
  for i = #living, #living - la + 1, -1 do
    if living[i] then ents.kill(W, living[i], "raid", id); dead_home = dead_home + 1 end
  end
  if dead_home > 0 and home then ents.compact(W, home) end
  local kept = {}
  for i = 1, #living - la do kept[i] = living[i] end
  if ld > 0 then
    local list = {}
    local n = fighters(W, u, list)
    table.sort(list, function(a, b) return (a.u + a.id * 1e-9) < (b.u + b.id * 1e-9) end)
    for i = 1, min(ld, n) do ents.kill(W, list[i], "raid", id) end
    ents.compact(W, u)
  end
  u.acc.cattle_lost = (u.acc.cattle_lost or 0) + took
  if home then
    feud(W, home, u, win)
    M.wrong(W, u.id, home.id, cfg.cattle_grievance + ld * 0.5, id)
    if dead_home > 0 then M.wrong(W, home.id, u.id, dead_home * 0.5, id) end
    M.protect(W, u, home, id)
    if win then
      M.status.honour_leader(W, home, cfg.win_prestige * 0.3)
      for i = 1, #kept do M.status.honour(W, kept[i].household, cfg.raider_prestige) end
    else
      M.status.honour_leader(W, u, cfg.win_prestige * 0.3)
    end
    M.home_again(W, party, kept, home, 0, id, beasts)
  end
end

function M.raiders_home(W, party, home)
  if home and home.alive then
    local g = party.cargo and party.cargo.grain or 0
    home.stock.grain = home.stock.grain + g
    local b = party.cargo and party.cargo.beasts
    if b and home.herd then
      for _, k in ipairs({ "sheep", "goats", "cattle" }) do home.herd[k] = (home.herd[k] or 0) + (b[k] or 0) end
      -- the spoils belong to the raiders' households (D-061)
      local se = M.herds.se(b)
      local H, P = W.ent.households, W.ent.people
      local hs = {}
      for i = 1, #party.members do
        local p = P[party.members[i]]
        local h = p and not p.dead and H[p.household]
        if h then hs[#hs + 1] = h end
      end
      for i = 1, #hs do hs[i].beasts = (hs[i].beasts or 0) + se / #hs end
    end
    home.acc.loot = (home.acc.loot or 0) + g
    home.wk_imports = (home.wk_imports or 0) + g
    home.raid_out = nil
  end
end

---------------------------------------------------------------------------------------------
-- Tribute and polities (W9, docs/world/18 §2.5, D-058)
--
-- A village beaten again and again by the same raider, that cannot simply move away (its land
-- is full, or it is much the weaker), submits: it pays a share of every harvest to its lord,
-- carried by its porters (Carneiro's circumscription: where people cannot flee, war makes
-- subjects). The lord stops raiding it, avenges raids on it (grievance against the raider),
-- sends it relief first in a famine (staple finance), and its standing rises with every
-- payment. A tributary whose lord is gone or has grown weaker than it stops paying (secession).
-- A polity is a connected tree of lords and tributaries; its depth is its tiers (M.polity).

-- The raider `a` beat village `u` (battle): count it, and maybe u submits.
function M.defeated(W, u, a, ev)
  local cfg = cfgmod.CONFLICT
  local lost = u.lost_to
  if not lost then lost = {}; u.lost_to = lost end
  local k = tostring(a.id)
  local e = lost[k]
  if not e or W.hour - e.last > cfg.defeat_memory_years * 8064 then e = { n = 0, last = 0 }; lost[k] = e end
  e.n, e.last = e.n + 1, W.hour
  if u.lord or a.lord == u.id or e.n < cfg.submit_defeats then return end
  local fu, fa = fighters(W, u), fighters(W, a)
  -- can't flee (land full) or hopelessly outnumbered
  if not (u.intensify or fu < fa * cfg.submit_ratio) then return end
  u.lord = a.id
  u.lord_since = W.hour
  local id = emit(W, "submission", u.cell, ev, u.pop, { village = u.id, lord = a.id }, u)
  u.lord_ev = id
  M.status.honour_leader(W, a, cfg.submit_prestige)
end

-- Yearly after the harvest (economy): a tributary sends its lord a share of the harvest.
function M.pay_tribute(W, v)
  local cfg = cfgmod.CONFLICT
  local L = v.lord and W.ent.villages[v.lord]
  if not L then return end
  if not L.alive or (fighters(W, L) < fighters(W, v) * cfg.secede_ratio) then
    -- the lord is gone or weaker than us: we pay no more
    local id = emit(W, "secession", v.cell, v.lord_ev or 0, v.pop, { village = v.id, lord = v.lord }, v)
    if L.alive then M.wrong(W, L.id, v.id, 1.5, id) end
    v.lord, v.lord_since, v.lord_ev = nil, nil, nil
    return
  end
  local amount = min((v.last_harvest or 0) * cfg.tribute_share, max(0, v.stock.grain - v.need_day * 60))
  if amount < v.need_day * 3 then return end
  local ps = {}
  local P = W.ent.people
  for i = 1, #v.members do
    local p = P[v.members[i]]
    local a = (W.hour - p.born) / 8064
    if p.away == 0 and a >= 16 and a < 50 and p.health >= 60 then ps[#ps + 1] = p end
    if #ps >= max(2, min(8, ceil(amount / 120))) then break end
  end
  if #ps < 2 then return end
  v.stock.grain = v.stock.grain - amount
  local id = emit(W, "tribute", v.cell, v.lord_ev or 0, floor(amount), { village = v.id, lord = L.id, grain = floor(amount) }, nil)
  M.depart(W, v, ps, L.id, nil, "porters", id, 0, 0, { gift = true, tribute = true, grain = amount, back_to = v.id })
  v.acc.tribute_paid = (v.acc.tribute_paid or 0) + amount
end

-- A raid on a tributary wrongs its lord too (the lord will want revenge).
function M.protect(W, u, attacker, ev)
  local L = u.lord and W.ent.villages[u.lord]
  if L and L.alive and L ~= attacker then M.wrong(W, L.id, attacker.id, cfgmod.CONFLICT.protect_grievance, ev) end
end

-- The polity of village v: its paramount (the lord at the top) and v's tier below it.
function M.polity(W, v)
  local vs = W.ent.villages
  local top, depth = v, 0
  local guard = 0
  while top.lord and vs[top.lord] and vs[top.lord].alive and guard < 10 do
    top = vs[top.lord]
    depth = depth + 1
    guard = guard + 1
  end
  return top, depth
end

-- The polity's name: its paramount's, and what its tiers of tribute make it (a chiefdom of two
-- tiers, a paramount chiefdom of three, a kingdom of four or more — Wright and Johnson's
-- administrative levels).
function M.polity_name(W, top)
  local t = M.tiers(W, top)
  if t <= 1 then return nil, 1 end
  local kind = t >= 4 and "kingdom" or (t == 3 and "paramount chiefdom" or "chiefdom")
  return string.format("the %s %s", top.name or "?", kind), t
end

-- Tiers of the polity headed by paramount village `top` (1 = a lone village).
function M.tiers(W, top)
  local vs = W.ent.villages
  local best = 1
  for i = 1, #vs do
    local u = vs[i]
    if u.alive and u.lord then
      local t, d = M.polity(W, u)
      if t == top and d + 1 > best then best = d + 1 end
    end
  end
  return best
end

return M
