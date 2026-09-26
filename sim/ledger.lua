-- Global cause ledgers for co2, aerosol and g_temp (03_CAUSALITY.md §3 "Global fields").
-- Each ledger keeps standing categories for diffuse sources and the top player/nature roots.
-- Stored in W.G.ledgers (persistent). Amounts decay yearly so old causes fade.
local cfgmod = require("sim.config")

local ledger = {}

ledger.CATEGORIES = { "villagers", "wildfire", "volcanic", "sinks", "baseline", "player", "nature" }
local MAX_ROOTS = 8

local function get(W, name)
  local L = W.G.ledgers
  if not L then L = {}; W.G.ledgers = L end
  local l = L[name]
  if not l then
    l = { cats = {}, roots = {} }  -- roots: array of {id=root_id, amt=...}
    for _, c in ipairs(ledger.CATEGORIES) do l.cats[c] = 0 end
    L[name] = l
  end
  return l
end
ledger.get = get

-- Add `amount` to ledger `name`. If tag ~= 0 the amount is credited to the tag's root event
-- (player-rooted roots are named individually; others fall back to `category`).
function ledger.add(W, name, amount, tag, category)
  if amount == 0 then return end
  local l = get(W, name)
  if tag and tag ~= 0 then
    local root = W.E.root[tag]
    if root then
      local rs = l.roots
      for i = 1, #rs do
        if rs[i].id == root then rs[i].amt = rs[i].amt + amount; return end
      end
      if #rs < MAX_ROOTS then
        rs[#rs + 1] = { id = root, amt = amount }
        return
      end
      -- replace the smallest if this contribution is larger
      local mi, mv = 0, math.huge
      for i = 1, #rs do if rs[i].amt < mv then mi, mv = i, rs[i].amt end end
      if amount > mv then rs[mi] = { id = root, amt = amount }; return end
    end
  end
  category = category or "baseline"
  l.cats[category] = (l.cats[category] or 0) + amount
end

-- Yearly decay of all contributions.
function ledger.decay(W)
  local f = 1 - cfgmod.LEDGER.decay_per_year
  local L = W.G.ledgers
  if not L then return end
  for _, name in ipairs({ "co2", "aerosol", "g_temp" }) do
    local l = L[name]
    if l then
      for _, c in ipairs(ledger.CATEGORIES) do l.cats[c] = (l.cats[c] or 0) * f end
      local keep = {}
      for i = 1, #l.roots do
        local r = l.roots[i]
        r.amt = r.amt * f
        if math.abs(r.amt) > 1e-6 then keep[#keep + 1] = r end
      end
      l.roots = keep
    end
  end
end

-- Top root contributor of ledger `name` if it explains ≥ share of `anomaly` (> 0), else 0.
function ledger.top_root(W, name, anomaly)
  local L = W.G.ledgers
  local l = L and L[name]
  if not l or anomaly <= 0 then return 0 end
  local best, bid = 0, 0
  local rs = l.roots
  for i = 1, #rs do if rs[i].amt > best then best, bid = rs[i].amt, rs[i].id end end
  if bid ~= 0 and best >= cfgmod.LEDGER.dominant_share * anomaly then return bid end
  return 0
end

-- Tag used by per-cell terms driven by a global: 0 unless one root explains ≥ 40%.
function ledger.dominant_tag(W, name)
  local G = W.G
  if name == "co2" then
    return ledger.top_root(W, "co2", G.co2 - (G.co2_ref or 280))
  elseif name == "aerosol" then
    if G.aerosol < 0.01 then return 0 end
    local l = get(W, "aerosol")
    local total = 0
    for _, c in ipairs(ledger.CATEGORIES) do total = total + math.max(0, l.cats[c] or 0) end
    for i = 1, #l.roots do total = total + math.max(0, l.roots[i].amt) end
    return ledger.top_root(W, "aerosol", total)
  elseif name == "g_temp" then
    local gt = G.g_temp
    if gt < 0.3 and gt > -0.3 then return 0 end
    if gt < 0 and G.aerosol > 0.01 then return ledger.dominant_tag(W, "aerosol") end
    return ledger.dominant_tag(W, "co2")
  end
  return 0
end

-- Shares for the planet inspector: list of {label, share} for ledger name (positive parts).
function ledger.shares(W, name)
  local l = get(W, name)
  local items, total = {}, 0
  for _, c in ipairs(ledger.CATEGORIES) do
    local v = l.cats[c] or 0
    if v > 0 then items[#items + 1] = { label = c, amt = v }; total = total + v end
  end
  for i = 1, #l.roots do
    local r = l.roots[i]
    if r.amt > 0 then items[#items + 1] = { label = "root", id = r.id, amt = r.amt }; total = total + r.amt end
  end
  table.sort(items, function(a, b) if a.amt ~= b.amt then return a.amt > b.amt end return (a.id or 0) < (b.id or 0) end)
  for _, it in ipairs(items) do it.share = total > 0 and it.amt / total or 0 end
  return items
end

return ledger
