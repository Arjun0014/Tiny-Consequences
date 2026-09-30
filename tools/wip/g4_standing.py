# NOT APPLIED YET. Session 7 draft of D-075 (standing from contribution): the household's weekly work
# (capacity x skill, weighed by scarcity), great catches, defenders and the fallen, porters; prestige
# shapes courtship. Apply AFTER g2_social_storage.py (it anchors on its config lines):
#   python tools/wip/g4_standing.py <repo root>
# Goal 4 (D-075): standing from contribution. Apply from the repo root: python g4.py <root>
import os, sys
os.chdir(sys.argv[1])
def load(p): return open(p, encoding='utf-8').read()
def save(p, s): open(p, 'w', encoding='utf-8', newline='\n').write(s)
def rep(s, old, new):
    assert s.count(old) == 1, (old[:100], s.count(old))
    return s.replace(old, new)

# --- config ---------------------------------------------------------------------------------
p = 'sim/config.lua'
s = load(p)
s = rep(s, """  feast_prestige = 0.01,                  -- per need-day of the village's food eaten at a feast it hosts (D-074)
""", """  feast_prestige = 0.01,                  -- per need-day of the village's food eaten at a feast it hosts (D-074)
  -- standing from contribution (D-075): what a household's people do for the village
  provide_gain = 0.06,                    -- monthly, per unit of (its share of the village's work / its share of the mouths - 1)
  provide_cap = 2,                        --   (capped: a household doing three times its share)
  great_catch = 2.0, catch_prestige = 0.05, -- a boat's day's catch this many times its usual: its crew's households honoured
  porter_prestige = 0.02,                 -- per porter who carried goods home or a gift to a friend
""")
s = rep(s, """  court_age = """, """  court_choice = 3,                       -- a woman with no friend to marry looks at the next this many single men: the most respected (D-075)
  court_age = """)
save(p, s)

p = 'sim/config.lua'
s = load(p)
s = rep(s, """  palisade = 2.0,                         -- a standing palisade multiplies the defence
""", """  palisade = 2.0,                         -- a standing palisade multiplies the defence
  defend_prestige = 0.3,                  -- each man who stood and beat off a raid: his household honoured (D-075)
  fallen_prestige = 0.4,                  -- each man who fell defending: his household honoured
""")
save(p, s)

# --- labour: each worker's capacity at the week's task -----------------------------------------
p = 'sim/society/labour.lua'
s = load(p)
s = rep(s, """  c = c * (cfg.skill_lo + (cfg.skill_hi - cfg.skill_lo) * ((cat and sk and sk[cat]) or 0))
  d.got = d.got + c * week_h
""", """  c = c * (cfg.skill_lo + (cfg.skill_hi - cfg.skill_lo) * ((cat and sk and sk[cat]) or 0))
  p.wcap = c   -- (what they bring to the village's work this week: standing from contribution, D-075)
  d.got = d.got + c * week_h
""")
s = rep(s, """    p.task, p.site = "", 0
    if p.away == 0 then
      local age = (hour - p.born) / 8064""", """    p.task, p.site, p.wcap = "", 0, 0
    if p.away == 0 then
      local age = (hour - p.born) / 8064""")
save(p, s)

# --- life: the household's week of work; courtship by standing -----------------------------------
p = 'sim/society/life.lua'
s = load(p)
s = rep(s, """  local CROWD = CROWDED
  for k in pairs(CROWD) do CROWD[k] = nil end
""", """  local CROWD = CROWDED
  for k in pairs(CROWD) do CROWD[k] = nil end
  -- (a week's work counts for more when the village is hungry: 1 fed .. 1.45 on the leanest ration)
  local scarce = 2 - ration
""")
s = rep(s, """    local h = H[p.household]
    local home = h and B[h.home]
""", """    local h = H[p.household]
    local home = h and B[h.home]
    -- the household's work this week: its people's capacity at their tasks (strength, health and
    -- skill), for the standing it earns (status.monthly, D-075)
    local wc = p.wcap
    if h and wc and wc > 0 and p.away == 0 then h.work = (h.work or 0) + wc * scarce end
""")
s = rep(s, """      if not mm then mm = sm[mi]; mi = mi + 1 end
""", """      if not mm then
        -- else the most respected of the next few single men: his household's standing (D-075)
        local bp, seen = -1e9, 0
        local H = E.households
        for k = mi, #sm do
          local q = sm[k]
          if not taken[q] then
            seen = seen + 1
            local qh = H[q.household]
            local ps = qh and qh.prestige or 0
            if ps > bp then mm, bp = q, ps end
            if seen >= cfg.court_choice then break end
          end
        end
      end
""")
save(p, s)

# --- fishing: a great catch --------------------------------------------------------------------
p = 'sim/society/fishing.lua'
s = load(p)
s = rep(s, """        local catch = d * cfg.fish_fu
        landed = landed + catch
        acc(v, "trips", 1)
        b.catch = catch
""", """        local catch = d * cfg.fish_fu
        landed = landed + catch
        acc(v, "trips", 1)
        b.catch = catch
        -- a great day's catch (so many times the boat's share of the usual): its crew's households
        -- are honoured (D-075)
        local stc = cfgmod.STATUS
        local usual = (v.norm.catch_in or 0) / 7 / math.max(1, #v.boats)
        if usual > 0 and catch > stc.great_catch * usual then
          local HH, PP = W.ent.households, W.ent.people
          for k = 1, #b.crew do
            local q = PP[b.crew[k]]
            local h = q and HH[q.household]
            if h then h.prestige = (h.prestige or 0) + stc.catch_prestige; h.why, h.why_h = "catch", W.hour end
          end
        end
""")
save(p, s)

# --- conflict: defenders and the fallen --------------------------------------------------------
p = 'sim/society/conflict.lua'
s = load(p)
s = rep(s, """    for i = 1, min(ld, n) do ents.kill(W, list[i], "raid", id) end
    ents.compact(W, u)
  end
""", """    local HH = W.ent.households
    for i = 1, min(ld, n) do
      -- (those who fell defending their village: their households are honoured, D-075)
      local h = HH[list[i].household]
      if h then h.prestige = (h.prestige or 0) + cfg.fallen_prestige; h.why, h.why_h = "fallen", hour end
      ents.kill(W, list[i], "raid", id)
    end
    ents.compact(W, u)
  end
  if not win then
    -- the raid beaten off: the men who stood are honoured
    local list = {}
    local n = fighters(W, u, list)
    local HH = W.ent.households
    for i = 1, n do
      local h = HH[list[i].household]
      if h then h.prestige = (h.prestige or 0) + cfg.defend_prestige; h.why, h.why_h = "defence", hour end
    end
  end
""")
save(p, s)

# --- relations: porters honoured for goods brought home and gifts carried -------------------
p = 'sim/society/relations.lua'
s = load(p)
s = rep(s, """    if c.get and c.want then M.settle_expected(W, home, c.get, c.want, c.got) end
""", """    if c.get and c.want then M.settle_expected(W, home, c.get, c.want, c.got) end
    -- the porters who brought goods home are thanked (D-075)
    if c.get and (c.got or 0) > 0 then honour_party(W, party, cfgmod.STATUS.porter_prestige, "trade") end
""")
s = rep(s, """      local a = dest.acc
      local kind = c.surplus and "gift" or "relief"
""", """      honour_party(W, { members = living }, cfgmod.STATUS.porter_prestige, "gift")
      local a = dest.acc
      local kind = c.surplus and "gift" or "relief"
""")
s = rep(s, """function M.porters_arrive(W, party, dest, living)
""", """-- The households of a party's living members honoured (x each), and why (D-075).
local function honour_party(W, party, x, why)
  local P, H = W.ent.people, W.ent.households
  for i = 1, #party.members do
    local p = party.members[i]
    if type(p) == "number" then p = P[p] end
    local h = p and not p.dead and H[p.household]
    if h then h.prestige = (h.prestige or 0) + x; h.why, h.why_h = why, W.hour end
  end
end

function M.porters_arrive(W, party, dest, living)
""")
save(p, s)

# --- status: the monthly reckoning of contribution ---------------------------------------------
p = 'sim/society/status.lua'
s = load(p)
s = rep(s, """  local wp = cfgmod.HERDS.wealth_prestige
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then
      local pr = (h.prestige or 0) * cfg.decay
      if mean > 0.5 then pr = pr + wp * max(-1, min(3, (h.beasts or 0) / mean - 1)) end
""", """  local wp = cfgmod.HERDS.wealth_prestige
  -- contribution (D-075): each household's share of the month's work against its share of the
  -- mouths — those who feed others earn respect, those fed by others lose a little
  local wsum, msum = 0, 0
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then wsum = wsum + (h.work or 0); msum = msum + #h.members end
  end
  local pg, pc = cfg.provide_gain, cfg.provide_cap
  for i = 1, #v.households do
    local h = H[v.households[i]]
    if h then
      local pr = (h.prestige or 0) * cfg.decay
      if mean > 0.5 then pr = pr + wp * max(-1, min(3, (h.beasts or 0) / mean - 1)) end
      if wsum > 0 and msum > 0 and #h.members > 0 then
        local r = ((h.work or 0) / wsum) / (#h.members / msum) - 1
        if r > pc then r = pc elseif r < -0.5 then r = -0.5 end
        pr = pr + pg * r
        if r > 0.5 then h.fed_others = hour end
      end
      h.work = 0
""")
save(p, s)
print("ok")
