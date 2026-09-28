-- Friendship (W8, D-062): people befriend those they spend their days with — the crew of their
-- boat, the others on the same field or building site or woodlot, the children they play with,
-- the elders at the well. A person keeps a few friends (p.friends: person ids, saved with the
-- person); friends who die or move away are lost in time.
--
-- What friends change: mood (belonging; grief when a friend dies is a memory already), courtship
-- (a single woman is betrothed to a man she knows first), migration (a household goes where its
-- friends went: life.lua / mobility.lua), and splits (households bound to the leaving lineage by
-- friendship may go with it: status.lua).
local cfgmod = require("sim.config")
local rng = require("sim.rng")

local M = {}
local floor, min = math.floor, math.min

-- Is q among p's friends?
function M.is_friend(p, q)
  local f = p.friends
  if not f then return false end
  for i = 1, #f do if f[i] == q.id then return true end end
  return false
end

local function add(p, id)
  local f = p.friends
  if not f then f = {}; p.friends = f end
  f[#f + 1] = id
end

local function drop(p, id)
  local f = p.friends
  if not f then return end
  for i = #f, 1, -1 do if f[i] == id then table.remove(f, i) end end
  if #f == 0 then p.friends = nil end
end

-- Living friends of p in village vid (count).
function M.near(W, p, vid)
  local f = p.friends
  if not f then return 0 end
  local P = W.ent.people
  local n = 0
  for i = 1, #f do
    local q = P[f[i]]
    if q and not q.dead and q.village == vid then n = n + 1 end
  end
  return n
end

-- Monthly for village v: friends lost (dead, gone for years), new friends among companions.
local TASK_ID, NTASK = {}, 0
function M.monthly(W, v)
  local cfg = cfgmod.SOCIETY
  local P = W.ent.people
  local hour = W.hour
  local month = floor(hour / 672)
  -- (one full hash per village-month; each person's draws are one mixing round from it)
  local b_fade = rng.hash(W.seed, rng.S.FRIENDS, v.key_cell, month, 1)
  local b_new = rng.hash(W.seed, rng.S.FRIENDS, v.key_cell, month, 2)
  local buckets, order = {}, {}
  for i = 1, #v.members do
    local p = P[v.members[i]]
    if p and not p.dead then
      -- the lost: the dead, and friends in other villages fade (a chance a month)
      local f = p.friends
      if f then
        for k = #f, 1, -1 do
          local q = P[f[k]]
          if not q or q.dead then drop(p, f[k])
          elseif q.village ~= p.village and rng.fbase(b_fade, p.key + k) < cfg.friend_fade then
            drop(p, f[k])
          end
        end
      end
      if p.away == 0 then
        local a = (hour - p.born) / 8064
        -- the company they keep: children of an age at play, the same task at the same site, the
        -- elders at the well, the rest at home (numeric keys: no strings made per person)
        local key
        if a < 3 then key = nil
        elseif a < 12 then key = -1 - floor(a / 4)
        elseif p.task ~= "" and p.task ~= nil then
          local ti = TASK_ID[p.task]
          if not ti then NTASK = NTASK + 1; ti = NTASK; TASK_ID[p.task] = ti end
          key = ti * 4194304 + (p.site or 0)
        else key = a >= 55 and -10 or -11 end
        if key then
          local b = buckets[key]
          if not b then b = {}; buckets[key] = b; order[#order + 1] = key end
          b[#b + 1] = p
        end
      end
    end
  end
  local maxf = cfg.friend_max
  for o = 1, #order do
    local b = buckets[order[o]]
    local n = #b
    if n >= 2 then
      for i = 1, n do
        local p = b[i]
        local nf = p.friends and #p.friends or 0
        if nf < maxf then
          local u = rng.fbase(b_new, p.key)
          if u < cfg.friend_prob then
            -- someone of the same company, picked by the same draw
            local j = floor(u / cfg.friend_prob * n) + 1
            if j == i then j = j % n + 1 end
            local q = b[j]
            if q ~= p and not M.is_friend(p, q) and (q.friends and #q.friends or 0) < maxf then
              add(p, q.id)
              add(q, p.id)
            end
          end
        end
      end
    end
  end
end

-- Belonging: the mood a person's friends at hand give (life.moods).
function M.mood(W, p)
  local n = M.near(W, p, p.village)
  if n == 0 then
    local a = (W.hour - p.born) / 8064
    return a >= 14 and -cfgmod.SOCIETY.friend_lonely or 0
  end
  return cfgmod.SOCIETY.friend_mood * min(3, n)
end

return M
