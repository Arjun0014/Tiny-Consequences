-- Save/load (05_ARCHITECTURE.md §8). Pure Lua 5.1, string in / string out, text-safe.
--
-- A save = the snapped world state at the most recent year boundary (captured by
-- save.capture at the start of that tick) + the commands issued since + the current tick.
-- Loading restores the snapshot and replays commands up to the saved tick, so a reloaded world
-- is bit-identical to one that never stopped (P02).
--
-- Encoding: a small deterministic token format.
--   numbers: integers as "i<digits>;", others as "n<%.17g>;"   strings: "s<len>:<bytes>"
--   true/false: "T"/"F"    arrays: "[" items "]"    maps: "{" (key value)* "}" (sorted keys)
--   quantised field arrays: "Q<lo>;<hi>;<mode>;<count>;" + 3 chars per value (base64 alphabet)
--   int arrays with runs: "R<count>;" then (value "*" run ",")...

local world = require("sim.world")
local fields = require("sim.fields")
local version = require("sim.version")

local save = {}

local B64 = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"
local B64V = {}
for i = 1, 64 do B64V[B64:byte(i)] = i - 1 end

local floor, format, concat = math.floor, string.format, table.concat
local sorted_keys = world.sorted_keys

-- Deep copy (tables only; no metatables, no functions).
local function deepcopy(v)
  if type(v) ~= "table" then return v end
  local t = {}
  for k, x in pairs(v) do t[k] = deepcopy(x) end
  return t
end
save.deepcopy = deepcopy

-- Snapshot of the persistent state at a year boundary (call right after fields.snap_all).
function save.capture(W)
  local snap = {}
  for _, k in ipairs(world.SAVE_KEYS) do
    if k == "E" then
      local E = W.E
      snap.E = deepcopy({ next_id = E.next_id, rec = E.rec, order = E.order, parent = E.parent,
        root = E.root, agg = E.agg, agg_keys = E.agg_keys, cool = E.cool, cap = E.cap,
        headline_count = E.headline_count, total = E.total, last_headline = E.last_headline })
    elseif k ~= "cmdlog" then
      snap[k] = deepcopy(W[k])
    end
  end
  snap.cmdlog_len = #W.cmdlog
  return snap
end

-- Encoder -------------------------------------------------------------------------------------
local function is_array(t)
  local n = #t
  local c = 0
  for _ in pairs(t) do c = c + 1 end
  return c == n
end

local function enc_int_array_rle(out, a)
  local n = #a
  out[#out + 1] = "R" .. n .. ";"
  local i = 1
  while i <= n do
    local v = a[i]
    local j = i
    while j < n and a[j + 1] == v do j = j + 1 end
    out[#out + 1] = format("%d*%d,", v, j - i + 1)
    i = j + 1
  end
end

local function all_ints(a)
  for i = 1, #a do
    local v = a[i]
    if type(v) ~= "number" or v ~= floor(v) or v >= 2147483648 or v <= -2147483648 then return false end
  end
  return true
end

local enc

local function enc_qarray(out, a, r)
  local lo, hi, mode = r[1], r[2], r[3] and 1 or 0
  local n = #a
  out[#out + 1] = format("Q%.17g;%.17g;%d;%d;", lo, hi, mode, n)
  local buf = {}
  local s = (hi - lo) / 65535
  for i = 1, n do
    local v = a[i]
    local k
    if mode == 1 then
      if v <= 0 then k = 0 elseif v >= hi then k = 65535 else k = floor(math.sqrt(v / hi) * 65535 + 0.5) end
    else
      if v <= lo then k = 0 elseif v >= hi then k = 65535 else k = floor((v - lo) / s + 0.5) end
    end
    local c1 = floor(k / 4096)
    local rem = k - c1 * 4096
    local c2 = floor(rem / 64)
    local c3 = rem - c2 * 64
    buf[i] = B64:sub(c1 + 1, c1 + 1) .. B64:sub(c2 + 1, c2 + 1) .. B64:sub(c3 + 1, c3 + 1)
  end
  out[#out + 1] = concat(buf)
end

enc = function(out, v, qhint)
  local tv = type(v)
  if tv == "number" then
    if v == floor(v) and v < 2147483648 and v > -2147483648 then out[#out + 1] = format("i%d;", v)
    else out[#out + 1] = format("n%.17g;", v) end
  elseif tv == "string" then
    out[#out + 1] = "s" .. #v .. ":" .. v
  elseif tv == "boolean" then
    out[#out + 1] = v and "T" or "F"
  elseif tv == "table" then
    if qhint then enc_qarray(out, v, qhint); return end
    if is_array(v) then
      if #v >= 64 and all_ints(v) then enc_int_array_rle(out, v); return end
      out[#out + 1] = "["
      for i = 1, #v do enc(out, v[i]) end
      out[#out + 1] = "]"
    else
      out[#out + 1] = "{"
      local nk, sk = sorted_keys(v)
      for i = 1, #nk do enc(out, nk[i]); enc(out, v[nk[i]]) end
      for i = 1, #sk do enc(out, sk[i]); enc(out, v[sk[i]]) end
      out[#out + 1] = "}"
    end
  else
    error("cannot encode " .. tv)
  end
end

-- Encode a field table (F or P): quantised arrays where a QRANGE exists (values are on the
-- quantisation grid after a snap, so decode is bit-exact); others generic.
local function enc_fieldset(out, t)
  out[#out + 1] = "{"
  local _, sk = sorted_keys(t)
  for i = 1, #sk do
    local name = sk[i]
    enc(out, name)
    enc(out, t[name], fields.QRANGE[name])
  end
  out[#out + 1] = "}"
end

-- Decoder -------------------------------------------------------------------------------------
local dec

local function read_until(s, pos, ch)
  local e = s:find(ch, pos, true)
  return s:sub(pos, e - 1), e + 1
end

dec = function(s, pos)
  local c = s:sub(pos, pos)
  if c == "i" then
    local d, p = read_until(s, pos + 1, ";")
    return tonumber(d), p
  elseif c == "n" then
    local d, p = read_until(s, pos + 1, ";")
    return tonumber(d), p
  elseif c == "s" then
    local len, p = read_until(s, pos + 1, ":")
    len = tonumber(len)
    return s:sub(p, p + len - 1), p + len
  elseif c == "T" then return true, pos + 1
  elseif c == "F" then return false, pos + 1
  elseif c == "[" then
    local t = {}
    local p = pos + 1
    while s:sub(p, p) ~= "]" do
      local v
      v, p = dec(s, p)
      t[#t + 1] = v
    end
    return t, p + 1
  elseif c == "{" then
    local t = {}
    local p = pos + 1
    while s:sub(p, p) ~= "}" do
      local k, v
      k, p = dec(s, p)
      v, p = dec(s, p)
      t[k] = v
    end
    return t, p + 1
  elseif c == "R" then
    local n, p = read_until(s, pos + 1, ";")
    n = tonumber(n)
    local t = {}
    local i = 0
    while i < n do
      local e = s:find(",", p, true)
      local chunk = s:sub(p, e - 1)
      local star = chunk:find("*", 1, true)
      local v = tonumber(chunk:sub(1, star - 1))
      local run = tonumber(chunk:sub(star + 1))
      for _ = 1, run do i = i + 1; t[i] = v end
      p = e + 1
    end
    return t, p
  elseif c == "Q" then
    local lo, hi, mode, n, p
    lo, p = read_until(s, pos + 1, ";")
    hi, p = read_until(s, p, ";")
    mode, p = read_until(s, p, ";")
    n, p = read_until(s, p, ";")
    lo, hi, mode, n = tonumber(lo), tonumber(hi), tonumber(mode), tonumber(n)
    local t = {}
    local sc = (hi - lo) / 65535
    local byte = string.byte
    for i = 1, n do
      local b1, b2, b3 = byte(s, p, p + 2)
      local k = B64V[b1] * 4096 + B64V[b2] * 64 + B64V[b3]
      p = p + 3
      if mode == 1 then
        local q = k / 65535
        t[i] = q * q * hi
      else
        t[i] = lo + k * sc
      end
    end
    return t, p
  end
  error("bad save token '" .. c .. "' at " .. pos)
end

-- Public API ------------------------------------------------------------------------------------
-- Serialise: snapshot (from save.capture) + commands since + current tick + extras (settings,
-- goals, real timestamp, recent command log...). Returns a text-safe string.
function save.encode(snap, cmds_since, current_tick, extras)
  local out = { "TCSAVE1|", tostring(version.SAVE_VERSION), "|" }
  local doc = {}
  for k, v in pairs(snap) do doc[k] = v end
  doc.F, doc.P = nil, nil
  doc.cmds_since = cmds_since or {}
  doc.current_tick = current_tick or snap.tick
  doc.extras = extras or {}
  enc(out, doc)
  out[#out + 1] = "|"
  enc_fieldset(out, snap.F)
  enc_fieldset(out, snap.P)
  return concat(out)
end

function save.decode(str)
  local magic, ver, rest = str:match("^(TCSAVE1)|(%d+)|(.*)$")
  if not magic then return nil, "not a save" end
  if tonumber(ver) ~= version.SAVE_VERSION then return nil, "version" end
  local doc, p = dec(rest, 1)
  if rest:sub(p, p) ~= "|" then return nil, "corrupt" end
  doc.F, p = dec(rest, p + 1)
  doc.P, p = dec(rest, p)
  return doc
end

-- Build a live world from a decoded save doc: restore state, rebuild derived data, re-queue
-- commands since the snapshot and replay up to current_tick. opts.replay=false stops at the
-- snapshot tick.
function save.restore(doc, opts)
  opts = opts or {}
  local W = world.new(doc.seed, { level = doc.level })
  for _, k in ipairs(world.SAVE_KEYS) do
    if k == "E" then
      local E = W.E
      for kk, vv in pairs(doc.E) do E[kk] = vv end
      E.feed = {}
    elseif k ~= "cmdlog" and doc[k] ~= nil then
      W[k] = doc[k]
    end
  end
  W.cmdlog = (doc.extras and doc.extras.cmdlog) or {}
  world.rebuild_derived(W)
  world.init_systems(W)
  W.pending = {}
  local cmds = doc.cmds_since or {}
  if opts.replay ~= false then
    local target = doc.current_tick or W.tick
    local ci = 1
    while W.tick < target do
      while cmds[ci] and cmds[ci].tick <= W.tick do
        W.pending[#W.pending + 1] = { tick = cmds[ci].tick, type = cmds[ci].type, params = cmds[ci].params }
        ci = ci + 1
      end
      world.tick(W)
    end
    while cmds[ci] do
      W.pending[#W.pending + 1] = cmds[ci]
      ci = ci + 1
    end
  end
  return W
end

-- Convenience: save the live world W given its last year-boundary snapshot.
function save.save_world(W, extras)
  local snap = W.last_snapshot
  if not snap then error("no year-boundary snapshot captured (set W.on_year_boundary)") end
  local since = {}
  for i = snap.cmdlog_len + 1, #W.cmdlog do since[#since + 1] = W.cmdlog[i] end
  -- commands queued but not yet applied also belong to the save
  for i = 1, #W.pending do since[#since + 1] = W.pending[i] end
  return save.encode(snap, since, W.tick, extras)
end

-- Enable automatic capture at every year boundary.
function save.enable_capture(W)
  W.on_year_boundary = function(w) w.last_snapshot = save.capture(w) end
end

return save
