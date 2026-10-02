-- The river network as the sim has it (S14 geography): sources, mouths, sinks, lengths, the
-- elevation from source to mouth, discharge along the main stems, tributary junctions; and how
-- many land cells carry a river at the threshold surface.river_widths uses.
--   luajit tools/v3_rivers.lua [--load assets/worlds/v3.tcsave]
package.path = "./?.lua;./?/init.lua;" .. package.path
local args = { load = "assets/worlds/v3.tcsave" }
for i = 1, #arg do if arg[i] == "--load" then args.load = arg[i + 1] end end
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local surface = require("sim.surface")
local g, geo, st = W.grid, W.geo, W.static
local sea = W.G.sea_level
local fm = W.S.hydro.flow_mean
local wd, thr = surface.river_widths(W)
local down = geo.downhill
local up = {}
for c = 1, W.n do
  if geo.ocean[c] == 0 and wd[c] > 0 then
    local d = down[c]
    if d and d ~= 0 then up[d] = (up[d] or 0) + 1 end
  end
end
local nriv, sources, mouths, sinks, junctions = 0, {}, 0, 0, 0
for c = 1, W.n do
  if geo.ocean[c] == 0 and wd[c] > 0 then
    nriv = nriv + 1
    if not up[c] then sources[#sources + 1] = c end
    if (up[c] or 0) >= 2 then junctions = junctions + 1 end
    local d = down[c]
    if not d or d == 0 then sinks = sinks + 1 elseif geo.ocean[d] == 1 then mouths = mouths + 1 end
  end
end
print(string.format("land %d, river cells %d (thr %.4f), sources %d, mouths %d, inland sinks %d, junctions %d",
  #geo.land_cells, nriv, thr, #sources, mouths, sinks, junctions))
-- each source traced to its end
local rows = {}
for _, s0 in ipairs(sources) do
  local c, len, e0 = s0, 0, st.elev[s0] - sea
  local uphill_steps = 0
  local last_e = e0
  while true do
    local d = down[c]
    if not d or d == 0 or geo.ocean[d] == 1 or len > 200 then break end
    local e = st.elev[d] - sea
    if e > last_e + 1e-9 then uphill_steps = uphill_steps + 1 end
    last_e = e
    c, len = d, len + 1
  end
  local d = down[c]
  local outlet = (d and d ~= 0 and geo.ocean[d] == 1) and "sea" or "sink"
  rows[#rows + 1] = { s0 = s0, len = len + 1, e0 = e0, e1 = st.elev[c] - sea, q = fm[c], out = outlet, upn = uphill_steps }
end
table.sort(rows, function(a, b) return a.q > b.q end)
local hi_src = 0
for _, r in ipairs(rows) do if r.e0 > 1.0 then hi_src = hi_src + 1 end end
print(string.format("sources above 1 km: %d of %d", hi_src, #rows))
for i = 1, math.min(25, #rows) do
  local r = rows[i]
  print(string.format("  source cell %4d (%.2f km, lat %5.1f lon %6.1f) -> %d cells -> mouth %.2f km, Q %.3f, %s%s", r.s0, r.e0,
    math.deg(g.lat[r.s0]), math.deg(g.lon[r.s0]), r.len, r.e1, r.q, r.out, r.upn > 0 and (" UPHILL x" .. r.upn) or ""))
end
-- settlements' rivers
for _, s in ipairs(W.S.v3 and W.S.v3.settlements or {}) do
  local best = 0
  local cells = {}
  for i = 1, s.N * s.N, 37 do local c = s.cell_of and s.cell_of[i]; if c then cells[c] = true end end
  for c in pairs(cells) do if wd[c] > best then best = wd[c] end end
  print(string.format("settlement %-18s cell %d widest river on its map %.3f", s.name, s.cell, best))
end
-- (the basins of v3 worlds) each mouth's trunk traced upstream along the largest tributary to its
-- farthest source; the carved bed falling all the way
local bed = geo.bed
if bed then
  local kids = {}
  for c = 1, W.n do
    local d = down[c]
    if geo.ocean[c] == 0 and d and d ~= 0 then kids[d] = kids[d] or {}; table.insert(kids[d], c) end
  end
  local bad, maxcut = 0, 0
  for c = 1, W.n do
    if geo.ocean[c] == 0 then
      local d = down[c]
      if d and d ~= 0 and geo.ocean[d] == 0 and bed[d] >= bed[c] then bad = bad + 1 end
      local cut = (st.elev[c] - sea) - bed[c]
      if cut > maxcut then maxcut = cut end
    end
  end
  print(string.format("beds: %d links not falling; deepest gorge %.2f km", bad, maxcut))
  local trunks = {}
  for c = 1, W.n do
    if geo.ocean[c] == 0 and down[c] ~= 0 and geo.ocean[down[c]] == 1 then
      -- area and the trunk
      local area, stack = 0, { c }
      while #stack > 0 do local x = table.remove(stack); area = area + 1; for _, k in ipairs(kids[x] or {}) do stack[#stack + 1] = k end end
      local x, len = c, 1
      while kids[x] do
        local best, bq = nil, -1
        for _, k in ipairs(kids[x]) do if fm[k] > bq then best, bq = k, fm[k] end end
        x, len = best, len + 1
      end
      trunks[#trunks + 1] = { mouth = c, src = x, len = len, area = area, q = fm[c] }
    end
  end
  table.sort(trunks, function(a, b) return a.q > b.q end)
  for i = 1, math.min(12, #trunks) do
    local t = trunks[i]
    print(string.format("  mouth %4d Q %.3f basin %3d cells, trunk %2d cells from source %4d at %.2f km (bed %.2f) to the mouth at %.2f km",
      t.mouth, t.q, t.area, t.len, t.src, st.elev[t.src] - sea, bed[t.src], bed[t.mouth]))
  end
  local fl = {}
  for c = 1, W.n do if geo.ocean[c] == 0 then fl[#fl + 1] = fm[c] end end
  table.sort(fl)
  print(string.format("flow_mean quantiles: 50%% %.4f 70%% %.4f 80%% %.4f 90%% %.4f 95%% %.4f max %.4f", fl[math.floor(#fl * 0.5)], fl[math.floor(#fl * 0.7)],
    fl[math.floor(#fl * 0.8)], fl[math.floor(#fl * 0.9)], fl[math.floor(#fl * 0.95)], fl[#fl]))
end
