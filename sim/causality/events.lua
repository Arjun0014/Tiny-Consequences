-- Event log (03_CAUSALITY.md §2). Records are small tables (events are rare: ≤ ~50/tick).
-- Every event's parent/root is also kept in flat arrays forever (cheap), so chains survive
-- when old unpinned records are dropped from the ring: display skips dropped links.

local KINDS = require("sim.causality.event_kinds")

local events = {}
events.KINDS = KINDS

function events.new()
  return {
    next_id = 1,
    rec = {},          -- id -> record (only kept ones)
    order = {},        -- ids of kept records, ascending
    parent = {},       -- id -> parent id (all events ever)
    root = {},         -- id -> root id (all events ever)
    feed = {},         -- new event ids for the view to drain
    agg = {},          -- aggregation key -> id
    agg_keys = {},     -- keys in insertion order (for pruning without pairs)
    cool = {},         -- cooldown key -> tick last emitted
    cap = 8192,
    headline_count = 0,
    total = 0,
  }
end

local function region_of(W, cell)
  if cell and cell > 0 and W.region then return W.region[cell] or 0 end
  return 0
end

-- Emit an event. Returns the id (possibly an existing id when aggregated), or 0 when
-- suppressed by a cooldown. opts: {key=string for cooldown, headline=bool override,
-- actor=override, region=override}
function events.emit(W, kind, cell, parent, magnitude, data, opts)
  local E = W.E
  local k = KINDS[kind]
  if not k then error("unknown event kind " .. tostring(kind)) end
  parent = parent or 0
  if parent ~= 0 and E.parent[parent] == nil then parent = 0 end
  local tick = W.tick
  local region = (opts and opts.region) or region_of(W, cell)

  if opts and opts.key and k.cooldown then
    local ck = kind .. "|" .. opts.key
    local last = E.cool[ck]
    if last and tick - last < k.cooldown then return 0 end
    E.cool[ck] = tick
  end

  if k.agg then
    local ak = kind .. "|" .. parent .. "|" .. region
    local id = E.agg[ak]
    local r = id and E.rec[id]
    if r and tick - r.tick <= k.agg then
      r.mag = r.mag + (magnitude or 1)
      r.count = (r.count or 1) + 1
      events.update_headline(W, r)
      return id
    end
  end

  local id = E.next_id
  E.next_id = id + 1
  local actor = (opts and opts.actor) or k.actor
  if actor == "derived" and parent == 0 then actor = "nature" end
  local root = id
  if parent ~= 0 then root = E.root[parent] end
  local r = {
    id = id, kind = kind, tick = tick, cell = cell or 0, actor = actor, parent = parent,
    root = root, mag = magnitude or 1, data = data, region = region, desc = 0, count = 1,
    headline = false,
  }
  E.parent[id] = parent
  E.root[id] = root
  E.rec[id] = r
  E.order[#E.order + 1] = id
  E.total = E.total + 1
  if k.agg then
    local ak = kind .. "|" .. parent .. "|" .. region
    if not E.agg[ak] then E.agg_keys[#E.agg_keys + 1] = ak end
    E.agg[ak] = id
  end
  if opts and opts.headline ~= nil then r.headline = opts.headline else events.update_headline(W, r) end
  -- descendants up the chain (walk flat parent array; dropped ancestors still counted)
  local p = parent
  local guard = 0
  while p ~= 0 and guard < 256 do
    local pr = E.rec[p]
    if pr then pr.desc = pr.desc + 1 end
    p = E.parent[p]
    guard = guard + 1
  end
  local feed = E.feed
  feed[#feed + 1] = id
  if #feed > 512 then table.remove(feed, 1) end
  if W.on_event then W.on_event(W, r) end
  return id
end

function events.update_headline(W, r)
  local k = KINDS[r.kind]
  if k.headline and not r.headline and r.mag >= (k.hmin or 0) then
    r.headline = true
    W.E.headline_count = W.E.headline_count + 1
    W.E.last_headline = r.id
  end
end

function events.get(W, id) return W.E.rec[id] end

-- Chain from root to id (records only; dropped links skipped).
function events.chain(W, id)
  local E = W.E
  local out = {}
  local p, guard = id, 0
  while p and p ~= 0 and guard < 256 do
    local r = E.rec[p]
    if r then table.insert(out, 1, r) end
    p = E.parent[p]
    guard = guard + 1
  end
  return out
end

function events.chain_length(W, id)
  local E = W.E
  local n, p, guard = 0, id, 0
  while p and p ~= 0 and guard < 256 do
    n = n + 1
    p = E.parent[p]
    guard = guard + 1
  end
  return n
end

function events.is_player_rooted(W, id)
  local root = W.E.root[id]
  local r = root and W.E.rec[root]
  return r ~= nil and r.actor == "player"
end

-- All descendants of `root_id` (walks kept records; O(records)). For tests / chain queries.
function events.descendants(W, root_id)
  local E = W.E
  local out = {}
  for i = 1, #E.order do
    local id = E.order[i]
    if id ~= root_id and E.root[id] == E.root[root_id] then
      -- confirm root_id is an ancestor
      local p, guard = E.parent[id], 0
      while p ~= 0 and guard < 256 do
        if p == root_id then out[#out + 1] = E.rec[id]; break end
        p = E.parent[p]
        guard = guard + 1
      end
    end
  end
  return out
end

-- Drop old unreferenced records when over capacity. `referenced(id)` -> bool supplied by the
-- world (cause tags, entity causes, memories, goals). Headlines are pinned while the total
-- number of kept headlines is under cap/4.
function events.gc(W, referenced_set)
  local E = W.E
  if #E.order <= E.cap then return 0 end
  local target = math.floor(E.cap * 0.75)
  local drop_n = #E.order - target
  local keep = {}
  local dropped = 0
  local headlines_kept = 0
  for i = #E.order, 1, -1 do
    local r = E.rec[E.order[i]]
    if r.headline then headlines_kept = headlines_kept + 1 end
  end
  for i = 1, #E.order do
    local id = E.order[i]
    local r = E.rec[id]
    local pinned = referenced_set[id] or (r.headline and headlines_kept <= E.cap / 4)
    if dropped < drop_n and not pinned then
      E.rec[id] = nil
      dropped = dropped + 1
      if r.headline then headlines_kept = headlines_kept - 1 end
    else
      keep[#keep + 1] = id
    end
  end
  E.order = keep
  -- prune aggregation keys pointing at dropped/old records
  local nk = {}
  for i = 1, #E.agg_keys do
    local ak = E.agg_keys[i]
    local id = E.agg[ak]
    local r = id and E.rec[id]
    if r and W.tick - r.tick < 480 then nk[#nk + 1] = ak else E.agg[ak] = nil end
  end
  E.agg_keys = nk
  return dropped
end

return events
