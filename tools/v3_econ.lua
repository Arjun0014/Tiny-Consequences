-- The economy of a v3 world over time (roadmap v3 R4): every settlement's stocks every few days
-- (what it has a person: grain, fish, meat, cheese, wool, cloth, hides, leather, logs, herds), its
-- trade (trips, goods out and in) and the trades themselves as they end.
--   luajit tools/v3_econ.lua [--load assets/worlds/v3.tcsave] [--days 48] [--every 4] [--out out/econ]
--                            [--sett K]   (one settlement's households in detail)
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local world = require("sim.world")
local clock = require("sim.agents.clock")
local people = require("sim.agents.people")

local args = { load = "assets/worlds/v3.tcsave", days = 48, every = 4, out = "out/econ" }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--every" then i = i + 1; args.every = tonumber(arg[i])
  elseif a == "--out" then i = i + 1; args.out = arg[i]
  elseif a == "--sett" then i = i + 1; args.sett = tonumber(arg[i])
  end
  i = i + 1
end
os.execute((package.config:sub(1, 1) == "\\" and "mkdir \"" .. args.out:gsub("/", "\\") .. "\" 2>nul") or ("mkdir -p " .. args.out))
io.stdout:setvbuf("no")
local lines = {}
local function say(fmt, ...)
  local l = select("#", ...) > 0 and string.format(fmt, ...) or fmt
  lines[#lines + 1] = l
  print(l)
end

local f = assert(io.open(args.load, "rb"))
local data = f:read("*a")
f:close()
local W = assert(require("sim.worlds").from_save_string(data))
local V = W.S.v3

-- trades as they end: the porter's memory "Sold ..." (people.remember is the one place)
local orig_rem = people.remember
people.remember = function(p, t, text, ...)
  if type(text) == "string" and (text:find("^Sold") or text:find("^Traded") or text:find("lost at sea") or text:find("^Bought")) then
    local s = V.settlements[p.sett]
    say("  d%-3d %-18s %-22s %s", clock.day(t) - clock.day(V.t0 or 0), s and s.name or "?", p.name, text)
  end
  return orig_rem(p, t, text, ...)
end

local KEYS = { "food", "meals", "smoked", "fish", "meat", "cheese", "milk", "wool", "flax", "fibre", "yarn", "cloth", "hides", "leather", "logs" }
local function census(s)
  local t = {}
  for _, k in ipairs(KEYS) do t[k] = 0 end
  local g = s.granary
  t.grain = g and (g.stock.grain or 0) or 0
  t.g_other = {}
  if g then
    for k, v in pairs(g.stock) do if k ~= "grain" and v > 0.01 then t.g_other[#t.g_other + 1] = string.format("%s %.0f", k, v) end end
    table.sort(t.g_other)
  end
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.state == "standing" then
      for _, k in ipairs(KEYS) do t[k] = t[k] + (b.stock[k] or 0) end
    end
  end
  t.ws = s.wood_store and (s.wood_store.stock.logs or 0) or 0
  local dn, ds, dlo, dhi = 0, 0, 0, 0
  for _, id in ipairs(s.blist) do
    local b = s.buildings[id]
    if b.kind == "house" and b.diet and b.hh ~= 0 then
      dn, ds = dn + 1, ds + b.diet
      if b.diet < require("sim.config").V3.diet_lo then dlo = dlo + 1 elseif b.diet > require("sim.config").V3.diet_hi then dhi = dhi + 1 end
    end
  end
  t.diet = dn > 0 and string.format("diet %.2f lo%d hi%d", ds / dn, dlo, dhi) or "diet -"
  local n, clothes = 0, 0
  for _, id in ipairs(s.members) do
    local p = V.people[id]
    if p and p.alive then n = n + 1; clothes = clothes + (p.clothes or 1) end
  end
  t.n = n
  t.clothes = n > 0 and clothes / n or 0
  local herd = {}
  for _, hk in ipairs({ "flock", "cattle", "swine" }) do
    local H = s.herds and s.herds[hk]
    if H then
      for _, a in ipairs(H.animals) do if a.alive then herd[a.sp] = (herd[a.sp] or 0) + 1 end end
    end
  end
  t.herd = herd
  return t
end

local day0 = clock.day(W.hour)
V.t0 = day0 * 24
say("world %s: %d settlements, day %d", args.load, #V.settlements, day0)
for _, s in ipairs(V.settlements) do
  local tr = require("sim.agents.trade")
  local ps = {}
  for _, o in ipairs(tr.partners(W, s)) do
    local d, h = tr.road(W, s, o)
    ps[#ps + 1] = string.format("%s %.1f (%.0fh)", o.name, d, h)
  end
  say("  %d %-20s cont %d coast %s  partners: %s", s.id, s.name, W.geo.continent[s.cell], s.dock and "dock" or "-", table.concat(ps, ", "))
end

local function report(d)
  say("day %d", d)
  say("  %-18s %4s %6s %6s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s %5s | %s", "settlement", "pop", "grain", "food", "smok", "meat", "chee",
    "wool", "fibr", "yarn", "clth", "wear", "hide", "lthr", "logs", "trip", "use/d target sow herds; granary extras")
  for _, s in ipairs(V.settlements) do
    if s.alive then
      local t = census(s)
      local hs = {}
      for k, v in pairs(t.herd) do hs[#hs + 1] = k .. " " .. v end
      table.sort(hs)
      say("  %-18s %4d %6.0f %6.0f %5.0f %5.0f %5.0f %5.1f %5.1f %5.1f %5.1f %5.2f %5.0f %5.0f %5.0f %5d | %.1f %.0f %d %s %s; %s", s.name, t.n, t.grain, t.food + t.meals, t.smoked + t.fish,
        t.meat, t.cheese, t.wool, t.fibre, t.yarn, t.cloth, t.clothes, t.hides, t.leather, t.ws + t.logs, s.acc.trips or 0, s.grain_use or 0, s.grain_target or 0, s.sow_want or 0, t.diet,
        table.concat(hs, " "), table.concat(t.g_other, " "))
    end
  end
end

report(0)
for d = 1, args.days do
  local base = (day0 + d - 1) * 24
  for h = 1, 24 do world.advance(W, base + h) end
  if d % args.every == 0 then report(d) end
end
say("deaths:")
for id, p in pairs(V.people) do
  if not p.alive and p.died and p.died >= day0 * 24 then
    local s = V.settlements[p.sett]
    say("  d%-3d %-16s %-18s age %.0f %s", clock.day(p.died) - day0, s and s.name or "?", p.name, (p.died - p.born) / (24 * 48), p.died_of or "?")
  end
end
say("accounts (whole run):")
for _, s in ipairs(V.settlements) do
  local a = s.acc
  local ks = {}
  for k, v in pairs(a) do if type(v) == "number" and v ~= 0 then ks[#ks + 1] = string.format("%s %.0f", k, v) end end
  table.sort(ks)
  say("  %s: %s", s.name, table.concat(ks, ", "))
end
if args.sett then
  local s = V.settlements[args.sett]
  say("households of %s:", s.name)
  for _, hid in ipairs(s.households) do
    local hh = V.households[hid]
    if hh and #hh.members > 0 then
      local home = people.home(W, s, V.people[hh.members[1]])
      local st = home and home.stock or {}
      local ks = {}
      for k, v in pairs(st) do if type(v) == "number" and v > 0.05 then ks[#ks + 1] = string.format("%s %.1f", k, v) end end
      table.sort(ks)
      local roles = {}
      for _, id in ipairs(hh.members) do local q = V.people[id]; roles[#roles + 1] = (q.role or "?") .. "/" .. people.stage(q, W.hour) end
      say("  hh %d (%d: %s): %s", hid, #hh.members, table.concat(roles, " "), table.concat(ks, ", "))
    end
  end
end
local fo = assert(io.open(args.out .. "/econ.txt", "w"))
fo:write(table.concat(lines, "\n"), "\n")
fo:close()
