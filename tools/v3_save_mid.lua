-- A v3 world saved in the middle of its life and restored must go on exactly as the world it was saved
-- from (S22: an 8-year run split by a save at day 192 starved people the continuous run did not).
-- Runs the world to a year boundary (--years Y planet years of 48 days), saves and restores a copy,
-- then advances both side by side an hour at a time and reports the first hour their states differ,
-- with the first differing path in W (drilled by world.hash_parts).
--   luajit tools/v3_save_mid.lua [--load assets/worlds/v3.tcsave] [--years 1] [--hours 48]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave", years = 1, hours = 48 }
local i = 1
while arg[i] do
  local k = arg[i]:match("^%-%-(.+)$")
  if k and arg[i + 1] then i = i + 1; args[k] = tonumber(arg[i]) or arg[i] end
  i = i + 1
end
local world = require("sim.world")
local worlds = require("sim.worlds")
local f = assert(io.open(args.load, "rb"))
local data = f:read("*a")
f:close()
local A = assert(worlds.from_save_string(data))
local goal = A.tick + 48 * args.years - A.tick % 48
local h = A.hour
while A.tick < goal do h = h + 24; world.advance(A, h) end
-- (exactly on the boundary: the hour the tick turned)
print(string.format("A at hour %.3f tick %d", A.hour, A.tick))
local str = worlds.year0_save_string(A, { seed = A.seed })
local B = assert(worlds.from_save_string(str))
print(string.format("B restored at hour %.3f tick %d; hashes A %d B %d", B.hour, B.tick, world.state_hash(A), world.state_hash(B)))
local function drill(path, depth)
  local pa, pb = world.hash_parts(A, path), world.hash_parts(B, path)
  local mb = {}
  for _, e in ipairs(pb) do mb[e.k] = e end
  for _, e in ipairs(pa) do
    local o = mb[e.k]
    if not o or o.h ~= e.h then
      local p2 = {}
      for q = 1, #path do p2[q] = path[q] end
      p2[#p2 + 1] = e.k
      if e.t == "table" and depth < 9 then return drill(p2, depth + 1) end
      local function get(W)
        local t = W
        for q = 1, #p2 do
          if type(t) ~= "table" then return nil end
          local key = p2[q]
          local v = t[key]
          if v == nil then v = t[tonumber(key)] end
          t = v
        end
        return t
      end
      return table.concat(p2, "."), tostring(get(A)), tostring(get(B))
    end
  end
  local ka = {}
  for _, e in ipairs(pa) do ka[e.k] = true end
  for _, e in ipairs(pb) do if not ka[e.k] then return table.concat(path, ".") .. "." .. e.k .. " (only in B)", "nil", "?" end end
  return table.concat(path, ".") .. " (?)", "", ""
end
-- every differing leaf path (at most `max`), depth first in key order
local function diffs(a, b, path, out, max, depth)
  if #out >= max then return end
  if type(a) ~= "table" or type(b) ~= "table" or depth > 10 then
    if a ~= b and not (type(a) == "number" and type(b) == "number" and a == b) then
      out[#out + 1] = path .. "   A=" .. tostring(a) .. "   B=" .. tostring(b)
    end
    return
  end
  local keys, seen = {}, {}
  for k in pairs(a) do keys[#keys + 1] = k; seen[k] = true end
  for k in pairs(b) do if not seen[k] then keys[#keys + 1] = k end end
  table.sort(keys, function(x, y)
    if type(x) == type(y) then return x < y end
    return type(x) == "number"
  end)
  for _, k in ipairs(keys) do
    diffs(a[k], b[k], path .. "." .. tostring(k), out, max, depth + 1)
    if #out >= max then return end
  end
end
local function report(label)
  print(label)
  for _, k in ipairs({ "S", "F", "G", "C", "I", "P", "static", "ent" }) do
    local out = {}
    if A[k] ~= nil then diffs(A[k], B[k], k, out, 12, 0) end
    for _, l in ipairs(out) do print("  " .. l) end
  end
  do return end
  for _, k in ipairs(world.SAVE_KEYS) do
    if k ~= "cmdlog" and k ~= "pending" and A[k] ~= nil then
      local a1 = world.hash_parts(A, { k })
      local b1 = world.hash_parts(B, { k })
      local same = #a1 == #b1
      for q = 1, #a1 do if not b1[q] or a1[q].h ~= b1[q].h or a1[q].k ~= b1[q].k then same = false end end
      if not same and k ~= "E" then
        local p, va, vb = drill({ k }, 1)
        print("  first difference: " .. p .. "   A=" .. va .. "   B=" .. vb)
      end
    end
  end
end
if args.who then
  for _, W_ in ipairs({ A, B }) do
    local p = W_.S.v3.people[args.who]
    print(string.format("%s person %d: act %s t0 %.4f t1 %.4f ver %d int_at %s int_late %s pi %s objk %s babe %s", W_ == A and "A" or "B", args.who,
      tostring(p.act), p.t0, p.t1, p.ver, tostring(p.int_at), tostring(p.int_late), tostring(p.pi), tostring(p.objk), tostring(p.babe)))
  end
  local R = A.v3rt
  if R then
    local n = 0
    for q = 1, R.n do if R.id[q] == args.who then n = n + 1; print(string.format("  A heap: t %.4f ver %d", R.t[q], R.ver[q])) end end
  end
end
if args.fine then
  for _ = 1, args.fine - 1 do world.advance(A, A.hour + 1); world.advance(B, B.hour + 1) end
  if world.state_hash(A) ~= world.state_hash(B) then report("already different before hour " .. args.fine); return end
  local h0 = A.hour
  for q = 1, 50 do
    world.advance(A, h0 + q * 0.02); world.advance(B, h0 + q * 0.02)
    if world.state_hash(A) ~= world.state_hash(B) then report(string.format("at hour %.2f: DIFFERENT", h0 + q * 0.02)); return end
  end
  print("hour " .. args.fine .. " the same")
  return
end
if args.stale then
  local now = B.hour
  local V = B.S.v3
  local stock = require("sim.agents.stock")
  for i = 1, #V.order do
    local p = V.people[V.order[i]]
    if p and p.alive and (p.t1 < now or (p.int_at and p.int_at < now)) then
      print(string.format("  person #%d %s act %s t1 %.3f int_at %s at %s", p.id, p.name, tostring(p.act), p.t1, tostring(p.int_at), tostring(p.at or p.sett)))
    end
  end
  for _, st in ipairs(V.settlements) do
    stock.each(st, function(H, a) if a.t1 < now then print(string.format("  %s %s #%d act %s t1 %.3f led %s held %s yoke %s why %s", st.name, H.kind, a.k, tostring(a.act), a.t1, tostring(a.led), tostring(a.held), tostring(a.yoke), tostring(a.why))) end end)
    require("sim.agents.wild").each(st, function(w) if w.t1 < now then print(string.format("  %s wild %s #%d act %s t1 %.3f", st.name, w.sp, w.k, tostring(w.act), w.t1)) end end)
    for _, d in ipairs(st.dogs or {}) do if d.alive and d.t1 < now then print(string.format("  %s dog #%d act %s t1 %.3f why %s", st.name, d.k, tostring(d.act), d.t1, tostring(d.why))) end end
    require("sim.agents.birds").each(st, function(f) if f.t1 < now then print(string.format("  %s birds #%d t1 %.3f", st.name, f.k, f.t1)) end end)
  end
  return
end
if args.trace then
  local act = require("sim.agents.act")
  local fin = act.finish
  local n = 0
  act.finish = function(W_, s_, p, now, f)
    if p.id == args.trace and n < 40 then
      n = n + 1
      local st = p.plan[p.pi] or {}
      print(string.format("  %s #%d finish at %.4f f %.2f: step %s %s ref %s (t0 %.4f t1 %.4f) obj %s | %s", W_ == A and "A" or "B", p.id, now, f,
        tostring(st.op), tostring(st.kind or st.what), tostring(st.ref), p.t0, p.t1, tostring(p.objk), tostring(p.why)))
    end
    return fin(W_, s_, p, now, f)
  end
  world.advance(B, B.hour + 0.02)
  return
end
if args.trees then
  local function felled(W_)
    local n, m = 0, 0
    for _, st in ipairs(W_.S.v3.settlements) do
      local tr = st.trees
      for k = 1, tr.n do if tr.st[k] == 2 then n = n + 1 elseif tr.st[k] == 1 then m = m + 1 end end
    end
    return n, m
  end
  local s1a, s1b = A.S.v3.settlements[1], B.S.v3.settlements[1]
  local ks = {}
  for k, v in pairs(s1a.tree_share or {}) do ks[#ks + 1] = string.format("%s=%.5f/%s", tostring(k), v, tostring((s1b.tree_share or {})[k])) end
  table.sort(ks)
  print("tree_share s1: " .. table.concat(ks, " "))
  print(string.format("felled/standing A %d/%d B %d/%d", select(1, felled(A)), select(2, felled(A)), select(1, felled(B)), select(2, felled(B))))
  world.advance(A, A.hour + 0.02); world.advance(B, B.hour + 0.02)
  print(string.format("after 0.02 h: felled/standing A %d/%d B %d/%d", select(1, felled(A)), select(2, felled(A)), select(1, felled(B)), select(2, felled(B))))
  for _, id in ipairs(B.S.v3.order) do
    local pa, pb = A.S.v3.people[id], B.S.v3.people[id]
    if pa and pb and math.abs((pa.sk_wood or 0) - (pb.sk_wood or 0)) > 1e-9 then
      print(string.format("  #%d %s: sk_wood A %.4f B %.4f | A %s %s t1 %.3f | B %s %s t1 %.3f why %s", id, pb.name, pa.sk_wood or 0, pb.sk_wood or 0,
        tostring(pa.act), tostring(pa.objk), pa.t1, tostring(pb.act), tostring(pb.objk), pb.t1, tostring(pb.why)))
      local pl = pb.plan
      for q = 1, math.min(#pl, 6) do
        local st = pl[q]
        print(string.format("     B plan %d: %s %s ref %s dur %s", q, tostring(st.op), tostring(st.kind or st.what), tostring(st.ref), tostring(st.dur)))
      end
    end
  end
  return
end
if args.split then
  -- (the hour's parts one at a time: what happens at the hour, then the agents through it)
  world.begin_hour(A); world.begin_hour(B)
  if world.state_hash(A) ~= world.state_hash(B) then report("after begin_hour (year boundary, planet step, hourly): DIFFERENT"); return end
  print("after begin_hour: same")
  -- (through the hour in small steps: the first moment they differ)
  local h0 = A.hour
  for q = 1, 50 do
    world.advance(A, h0 + q * 0.02); world.advance(B, h0 + q * 0.02)
    if world.state_hash(A) ~= world.state_hash(B) then report(string.format("at +%.2f h (the agents): DIFFERENT", q * 0.02)); return end
  end
  print("after the hour: same")
end
for t = 1, args.hours do
  world.advance(A, A.hour + 1)
  world.advance(B, B.hour + 1)
  local ha, hb = world.state_hash(A), world.state_hash(B)
  if ha ~= hb then
    print(string.format("hour +%d: DIFFERENT (A %d, B %d)", t, ha, hb))
    for _, k in ipairs(world.SAVE_KEYS) do
      if k ~= "cmdlog" and k ~= "pending" and A[k] ~= nil then
        local a1 = world.hash_parts(A, { k })
        local b1 = world.hash_parts(B, { k })
        local same = #a1 == #b1
        for q = 1, #a1 do if not b1[q] or a1[q].h ~= b1[q].h or a1[q].k ~= b1[q].k then same = false end end
        if not same then
          local p, va, vb = drill({ k }, 1)
          print("  first difference: " .. p .. "   A=" .. va .. "   B=" .. vb)
        end
      end
    end
    return
  end
  if t % 6 == 0 then print(string.format("hour +%d: same", t)) end
end
print("the restored world went on exactly as the saved one")
