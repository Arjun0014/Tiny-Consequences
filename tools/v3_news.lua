-- The world news over a headless run (S21, D-172): every story as it opens and grows (its newest line),
-- and at the end the cards the player would see with their causes (Why?).
--   luajit tools/v3_news.lua [--load assets/worlds/v3.tcsave] [--days 12] [--premise NAME ...] [--cards 8]
package.path = "./?.lua;./?/init.lua;" .. package.path
if rawget(_G, "jit") then jit.opt.start("maxtrace=8000", "maxmcode=8192", "maxrecord=8000") end -- lint:allow luajit-only
local args = { load = "assets/worlds/v3.tcsave", days = 12, premise = {}, cards = 8 }
local i = 1
while arg[i] do
  local a = arg[i]
  if a == "--load" then i = i + 1; args.load = arg[i]
  elseif a == "--days" then i = i + 1; args.days = tonumber(arg[i])
  elseif a == "--premise" then i = i + 1; args.premise[#args.premise + 1] = arg[i]
  elseif a == "--cards" then i = i + 1; args.cards = tonumber(arg[i]) end
  i = i + 1
end
io.stdout:setvbuf("no")
local f = assert(io.open(args.load, "rb"))
local W = assert(require("sim.worlds").from_save_string(f:read("*a")))
f:close()
local world = require("sim.world")
local news = require("sim.news")
local premise = require("sim.agents.premise")
for _, pr in ipairs(args.premise) do
  assert(premise.named(W, pr), "no named premise " .. pr)
  print("premise " .. pr)
end
local V = W.S.v3
local t0 = W.hour
local LV = { "BREAKING", "STORY", "SLOW" }
local said = {}
local function day_of(t) return string.format("d%05.2f", (t - t0) / 24) end
for h = 1, args.days * 24 do
  world.advance(W, t0 + h)
  news.update(W, W.hour)
  local R = news.rt(W)
  for _, st in ipairs(R.list) do
    local k = st.id .. ":" .. #st.lines .. ":" .. tostring(st.line) .. ":" .. tostring(st.active)
    if st.title and not said[k] then
      said[k] = true
      print(string.format("%s %-8s %-9s #%-3d %-44s | %s%s", day_of(W.hour), LV[st.level], st.kind, st.id, st.title, st.line or "",
        st.active and "" or "  (quiet)"))
    end
  end
end
print("\n== the cards now")
local R = news.rt(W)
local all = {}
for k = #R.list, 1, -1 do
  local st = R.list[k]
  if st.title and not (st.weak and #st.lines <= 1) then st.score = news.score(W, st, W.hour); all[#all + 1] = st end
  if #all >= args.cards then break end
end
for _, st in ipairs(all) do
  local ch = news.chain(W, st)
  print(string.format("[%s] %s %s  (score %.0f, root %s)", LV[st.level], st.title, st.active and "- ACTIVE" or "", st.score, ch.root:upper()))
  for _, l in ipairs(st.lines) do print("      " .. l) end
  for k, ln in ipairs(ch.links) do print(string.format("      %s %s (%s, %.1f days ago)", k == 1 and "why:" or "  <-", ln.text, ln.who, (W.hour - ln.t) / 24)) end
  local fo = news.focus(W, st)
  print(string.format("      go: %s (%.2f, %.2f)%s", (V.settlements[fo.sid] or {}).name or "?", fo.x or 0, fo.y or 0,
    fo.pid and (" select " .. (V.people[fo.pid] or {}).name) or ""))
end
