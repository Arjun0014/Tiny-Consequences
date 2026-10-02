-- Skills (W8, D-063): people get better at what they do. Each person keeps a skill (0..1) in the
-- kinds of work they have practised — farming, fishing, woodcutting, building, crafts, herding
-- (p.sk = { farm = x, ... }, saved with the person). A week's work at a task raises its skill
-- (fast at first, slowly toward mastery); unpractised skills fade. What an hour achieves scales
-- with the skill: 0.7 for a novice, ~1.0 for a practised hand, 1.1 for a master (labour.assign
-- counts each worker's capacity by it; a boat's crew fishes by theirs). So a village that loses
-- its old fishers to a storm catches less than its numbers say, and migrants bring what they know.
local dmath = require("sim.dmath")   -- (the same bits in every runtime, S14)
local cfgmod = require("sim.config")

local M = {}
local exp, min, floor = dmath.exp, math.min, math.floor

M.CAT = { sow = "farm", tend = "farm", harvest = "farm", care = "farm", clear = "farm", fish = "fish",
  fuel = "wood", timber = "wood", brush = "wood", build = "build", craft_tools = "craft", craft_pots = "craft",
  craft_cloth = "craft", herd = "herd", forage = "farm", hunt = "wood" }
local JOB_CAT = { farmer = "farm", fisher = "fish", woodcutter = "wood", builder = "build" }
local CATS = { "farm", "fish", "wood", "build", "craft", "herd" }
M.CATS = CATS

-- The skill of person p in the work of task kind k (0..1).
function M.get(p, k)
  local c = M.CAT[k]
  local sk = p.sk
  return (c and sk and sk[c]) or 0
end

-- What an hour of p's work at task k achieves, relative to an average hand.
function M.factor(p, k)
  local cfg = cfgmod.SOCIETY
  return cfg.skill_lo + (cfg.skill_hi - cfg.skill_lo) * M.get(p, k)
end

-- A person from before skills (worldgen, old saves): the skill of the years at their job and at the
-- field work everyone lends a hand to.
local function seed(W, p)
  local a = (W.hour - p.born) / 8064
  local sk = {}
  local worked = a - 12
  if worked > 0 then
    local c = JOB_CAT[p.job]
    if c then sk[c] = 1 - exp(-worked / 6) end
    if c ~= "farm" then sk.farm = 1 - exp(-worked / 15) end
  end
  p.sk = sk
end
M.seed = seed

-- Weekly for village v: practice (after the week's work: p.task is what they did); once every
-- four weeks, the fading of what went unpractised (keep^4).
function M.weekly(W, v)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local learn = cfg.skill_learn
  local fade = floor(W.hour / 168) % 4 == 0
  local keep4 = dmath.pow(cfg.skill_keep, 4)
  local CAT = M.CAT
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p and not p.dead then
      local sk = p.sk
      if not sk then seed(W, p); sk = p.sk end
      local c = p.away == 0 and CAT[p.task or ""] or nil
      if c then
        local x = sk[c] or 0
        sk[c] = x + (1 - x) * learn
      end
      if fade then
        for k = 1, #CATS do
          local cat = CATS[k]
          local x = sk[cat]
          if x and cat ~= c then
            x = x * keep4
            if x < 0.02 then x = nil end
            sk[cat] = x
          end
        end
      end
    end
  end
end

-- The person's best skills, for the inspector: { { cat, x }, ... } strongest first.
function M.best(p, n)
  local out = {}
  for k = 1, #CATS do
    local x = p.sk and p.sk[CATS[k]]
    if x and x >= 0.15 then out[#out + 1] = { CATS[k], x } end
  end
  table.sort(out, function(a, b) if a[2] ~= b[2] then return a[2] > b[2] end return a[1] < b[1] end)
  for i = #out, (n or 3) + 1, -1 do out[i] = nil end
  return out
end

return M
