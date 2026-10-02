-- Wall-time sampler for Lua in this runtime (the HTML5 Lua 5.1 is what the browser runs; LuaJIT
-- interpreter counts mislead there, D-090). A count hook fires every `every` VM instructions; the
-- wall time since the previous hook (minus the hook's own cost) goes to the function running now
-- (self) and to every distinct function on its stack (inclusive, up to `depth` levels), and to the
-- current line. C functions (string.format, bit, table.sort) and the collector show up in the Lua
-- function that called them. Never used by sim/.
local clock = require("game.platform.clock")

local M = {}

function M.run(fn, opts)
  opts = opts or {}
  local every = opts.every or 500
  local depth = opts.depth or 14
  local now = clock.now_ms
  local getinfo = debug.getinfo
  local self_t, incl_t, line_t, names = {}, {}, {}, {}
  -- slow windows: a window of `every` instructions far dearer than a VM loop is C-side work
  -- (a table rehash, table.sort, string building, the collector) charged to the line running then
  local slow_t, slow_n, line_n = {}, {}, {}
  local slow_us = opts.slow_us or 40
  local seen = {}
  local last = now()
  local hooks, total = 0, 0
  local function hook()
    local t_in = now()
    local dt = t_in - last
    hooks = hooks + 1
    total = total + dt
    local info = getinfo(2, "Sl")
    if info then
      local k = info.short_src .. ":" .. info.linedefined
      self_t[k] = (self_t[k] or 0) + dt
      local lk = info.short_src .. ":" .. (info.currentline or 0)
      line_t[lk] = (line_t[lk] or 0) + dt
      line_n[lk] = (line_n[lk] or 0) + 1
      if dt * 1000 > slow_us then
        slow_t[lk] = (slow_t[lk] or 0) + dt
        slow_n[lk] = (slow_n[lk] or 0) + 1
      end
      for lv = 2, depth + 1 do
        local fi = getinfo(lv, "Sn")
        if not fi then break end
        local fk = fi.short_src .. ":" .. fi.linedefined
        if not seen[fk] then
          seen[fk] = true
          incl_t[fk] = (incl_t[fk] or 0) + dt
          if fi.name and not names[fk] then names[fk] = fi.name end
        end
      end
      for kk in pairs(seen) do seen[kk] = nil end
    end
    last = now()
  end
  local t0 = now()
  debug.sethook(hook, "", every)
  fn()
  debug.sethook()
  local wall = now() - t0
  local function top(tb, n)
    local ks = {}
    for k in pairs(tb) do ks[#ks + 1] = k end
    table.sort(ks, function(a, b) return tb[a] > tb[b] end)
    local out = {}
    for j = 1, math.min(n, #ks) do
      local k = ks[j]
      out[j] = { k = k, name = names[k], ms = tb[k], pct = 100 * tb[k] / math.max(total, 1e-9) }
    end
    return out
  end
  return {
    wall_ms = wall, sampled_ms = total, hooks = hooks,
    self = top(self_t, opts.top or 40), incl = top(incl_t, opts.top or 40), lines = top(line_t, opts.top or 40),
    slow = (function()
      local r = top(slow_t, opts.top or 40)
      for _, x in ipairs(r) do x.name = string.format("%d slow of %d samples", slow_n[x.k], line_n[x.k]) end
      return r
    end)(),
  }
end

return M
