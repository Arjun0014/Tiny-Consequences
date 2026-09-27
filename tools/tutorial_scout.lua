-- Scout seeds for the tutorial world (04_PLAYER.md §6, Stage 2.1): Lightning on the tanker
-- nearest a fishing village must produce a chain of >= 3 links within 3 years, fast (the
-- tutorial runs it at ▶▶▶, about a year per second).
-- npm run sim:tutorial -- --from 1 --to 60 [--years 3]
-- Per seed: the tanker, its distance to the fishing village, and the best chain from the bolt
-- (kinds, links, the year each link happened).
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local worlds = require("sim.worlds")
local world = require("sim.world")
local events = require("sim.causality.events")

local args = { from = 1, to = 20, years = 3 }
local i = 1
while arg[i] do
  if arg[i] == "--from" then i = i + 1; args.from = tonumber(arg[i])
  elseif arg[i] == "--to" then i = i + 1; args.to = tonumber(arg[i])
  elseif arg[i] == "--years" then i = i + 1; args.years = tonumber(arg[i])
  elseif arg[i] == "--seed" then i = i + 1; args.from = tonumber(arg[i]); args.to = args.from end
  i = i + 1
end

local M = {}

M.pick = require("sim.tutorial").pick

-- Strike the tanker and run `years`: returns the best chain (list of records) from the bolt.
function M.run(W, t, years)
  world.command(W, "power", { power = "lightning", cell = t.cell, ship = t.id })
  world.tick(W)
  local root = nil
  for k = #W.E.order, 1, -1 do
    local r = W.E.rec[W.E.order[k]]
    if r.kind == "power_lightning" then root = r; break end
  end
  for _ = 1, years * 48 do world.tick(W) end
  local best, bl = nil, 0
  for _, d in ipairs(events.descendants(W, root.id)) do
    local ch = events.chain(W, d.id)
    local heads = 0
    for q = 1, #ch do if ch[q].headline then heads = heads + 1 end end
    local score = #ch + heads * 0.1
    if score > bl then best, bl = ch, score end
  end
  return best or { root }, root
end

if not rawget(_G, "TC_TUTORIAL_LIB") then
  for seed = args.from, args.to do
    local W = worlds.generate(seed)
    local t, d, v = M.pick(W)
    if not t then
      print(string.format("seed %2d | no tanker near a fishing village", seed))
    else
      local t0 = W.tick
      local ch = M.run(W, t, args.years)
      local parts = {}
      for q = 1, #ch do
        parts[q] = string.format("%s%s@%.2f", ch[q].kind, ch[q].headline and "*" or "", (ch[q].tick - t0) / 48)
      end
      print(string.format("seed %2d | tanker %s %.1f steps from %s (pop %d) | %d links | %s", seed, t.name, d,
        v.name, v.pop, #ch, table.concat(parts, " > ")))
    end
  end
end

return M
