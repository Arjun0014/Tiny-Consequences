-- Blame propagation (03_CAUSALITY.md §3).
--
-- Systems compute each causal field's change as a sum of named terms. After updating a cell
-- they call blame.step(...) with up to 4 signed terms and the tag each carries — but only
-- when something non-baseline is involved (current tag ~= 0 or any term tag ~= 0). The quiet
-- majority of cells never pays for blame. Zero allocations; fixed arity, no varargs.
--
-- Rules:
--  1. |delta| < eps: the tag idles; after `hold` idle ticks it resets to 0.
--  2. Dominant term = largest magnitude among terms in the direction of the change.
--  3. If the dominant tag differs from the current one and is non-zero, it takes over when it
--     is ≥ MARGIN × the largest same-direction term carrying a different tag.
--  4. Baseline (tag 0) dominance or unreinforced ticks count as idle → reset after `hold`.

local blame = {}

local MARGIN = 1.5
blame.MARGIN = MARGIN

-- tags, idle: the field's cause and idle arrays. v1..v4 signed term values, t1..t4 tags.
-- Pass 0,0 for unused term slots.
function blame.step(tags, idle, c, delta, eps, hold, v1, t1, v2, t2, v3, t3, v4, t4)
  local cur = tags[c]
  if delta < eps and delta > -eps then
    if cur ~= 0 then
      local i = idle[c] + 1
      if i >= hold then tags[c] = 0; idle[c] = 0 else idle[c] = i end
    end
    return
  end
  -- orient so the change direction is positive
  if delta < 0 then v1, v2, v3, v4 = -v1, -v2, -v3, -v4 end
  -- best same-direction term
  local bm, bt = 0, 0
  if v1 > bm then bm, bt = v1, t1 end
  if v2 > bm then bm, bt = v2, t2 end
  if v3 > bm then bm, bt = v3, t3 end
  if v4 > bm then bm, bt = v4, t4 end
  if bt == cur then
    if cur ~= 0 then idle[c] = 0 end
    return
  end
  if bt ~= 0 then
    -- second: largest same-direction term with a different tag than the best
    local sm = 0
    if t1 ~= bt and v1 > sm then sm = v1 end
    if t2 ~= bt and v2 > sm then sm = v2 end
    if t3 ~= bt and v3 > sm then sm = v3 end
    if t4 ~= bt and v4 > sm then sm = v4 end
    if bm >= MARGIN * sm then
      tags[c] = bt
      idle[c] = 0
      return
    end
  end
  if cur ~= 0 then
    local i = idle[c] + 1
    if i >= hold then tags[c] = 0; idle[c] = 0 else idle[c] = i end
  end
end

-- True if event `tag` descends from `ancestor` (walks the flat parent array, ≤ 16 links).
-- Used so an upstream cause never overwrites the more-derived event it already produced.
function blame.descends(parent, tag, ancestor)
  local p, guard = parent[tag], 0
  while p and p ~= 0 and guard < 16 do
    if p == ancestor then return true end
    p = parent[p]
    guard = guard + 1
  end
  return false
end

-- Directly set a tag (root events at their origin, detector re-tagging).
function blame.set(tags, idle, c, tag)
  tags[c] = tag
  idle[c] = 0
end

-- Tag carried by a flow into cell c from its neighbours: the tag of the tagged source with the
-- largest contribution, if that contribution is ≥ share of the total inflow; else 0.
-- w: weights array (stride 6), vals: source values (prev tick), tags: source tags.
function blame.inflow_tag(nbr, cnt, base, w, vals, tags, share)
  local total, best, bt = 0, 0, 0
  for k = 1, cnt do
    local j = nbr[base + k]
    local wk = w[base + k]
    if wk > 0 then
      local contrib = wk * vals[j]
      total = total + contrib
      local tj = tags[j]
      if tj ~= 0 and contrib > best then best, bt = contrib, tj end
    end
  end
  if bt ~= 0 and total > 0 and best >= share * total then return bt end
  return 0
end

return blame
