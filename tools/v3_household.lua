-- The households of given people, every few hours (S23): the larder (pantry), cooked meals, food, the granary,
-- who holds the household's claims - and each member: stage, hunger, at the edge, nursing, where, what they do
-- and why. Found the year-8 famine at Little Leelee (children shut in a sealed pocket) and toddlers who never got
-- their strength back (D-184). Load a year-boundary snapshot (tools/v3_chronicle.lua --days N --save F).
--   luajit tools/v3_household.lua SAVE PID[,PID] FROM_H TO_H [EVERY_H]
package.path = "./?.lua;./?/init.lua;" .. package.path
local worlds, world = require("sim.worlds"), require("sim.world")
local people = require("sim.agents.people")
local act = require("sim.agents.act")
local f = assert(io.open(arg[1], "rb"))
local W = assert(worlds.from_save_string(f:read("*a"))); f:close()
local V = W.S.v3
local pids = {}
for x in arg[2]:gmatch("%d+") do pids[#pids + 1] = tonumber(x) end
local from, to, every = tonumber(arg[3]), tonumber(arg[4]), tonumber(arg[5] or 3)
if from > W.hour then world.advance(W, from) end
for t = from, to, every do
  world.advance(W, t)
  local seen = {}
  for _, pid in ipairs(pids) do
    local p0 = V.people[pid]
    local hh = p0 and V.households[p0.hh]
    if hh and not seen[hh.id] then
      seen[hh.id] = true
      local s = V.settlements[p0.sett]
      local home = s and s.buildings[hh.home or 0]
      local g = s and s.granary
      local line = { string.format("h %7.1f d%5.1f hh %d %-10s pantry %5.1f meals %4.1f food %4.1f fish %4.1f smoked %4.1f meat %4.1f | gran %4.0f | fetch %s cook %s",
        t, t / 24, hh.id, (V.people[hh.members[1]] or {}).surname or "?", home and people.pantry(home) or -1, home and (home.stock.meals or 0) or -1,
        home and (home.stock.food or 0) or -1, home and (home.stock.fish or 0) or -1, home and (home.stock.smoked or 0) or -1, home and (home.stock.meat or 0) or -1,
        g and (g.stock.grain or 0) or -1, tostring(hh.fetch_food), tostring(hh.cooking)) }
      for _, id in ipairs(hh.members) do
        local q = V.people[id]
        if q and q.alive then
          local x, y = act.pos(q, t)
          line[#line + 1] = string.format("    #%d %-8s %-10s hu %.2f %s%s @%s (%.2f,%.2f) %s | %s | %s", q.id, q.name, people.stage(q, t), q.hunger or 0,
            q.edge and "EDGE " or "", (q.nursing or 0) > t and "nursing " or "", V.settlements[q.at or q.sett].name, x, y, q.act or "", q.obj or "", q.why or "")
        end
      end
      print(table.concat(line, "\n"))
    end
  end
  io.stdout:flush()
end
