-- Minimal stand-ins for the Defold APIs the view modules touch, so view code (clouds, props
-- planning) can be profiled headless under LuaJIT: `luajit tools/viewbench.lua`. Buffers are
-- plain Lua arrays; resources, messages and game objects are no-ops that count calls.
local S = { calls = 0 }

function _G.hash(s) return s end

_G.vmath = {
  vector3 = function(x, y, z) return { x = x or 0, y = y or 0, z = z or 0 } end,
  vector4 = function(x, y, z, w) return { x = x or 0, y = y or 0, z = z or 0, w = w or 0 } end,
  quat = function() return { x = 0, y = 0, z = 0, w = 1 } end,
  matrix4 = function() return {} end,
}

_G.buffer = {
  VALUE_TYPE_FLOAT32 = 1,
  create = function(n, decl)
    local b = { n = n, streams = {}, widths = {} }
    for _, d in ipairs(decl) do b.streams[d.name] = {}; b.widths[d.name] = d.count end
    return b
  end,
  get_stream = function(b, name) return b.streams[name] end,
  set_metadata = function() end,
  -- copies `count` elements of every stream of src into dst (streams matched by name)
  copy_buffer = function(dst, doff, src, soff, count)
    S.calls = S.calls + 1
    for name, ss in pairs(src.streams) do
      local ds = dst.streams[name]
      local w = src.widths[name]
      for k = 0, count * w - 1 do ds[doff * w + k + 1] = ss[soff * w + k + 1] end
    end
  end,
}

_G.resource = {
  create_buffer = function(path, t) S.calls = S.calls + 1; return { path = path } end,
  set_buffer = function() S.calls = S.calls + 1 end,
}

_G.go = {
  set = function() S.calls = S.calls + 1 end,
  set_position = function() S.calls = S.calls + 1 end,
  set_rotation = function() S.calls = S.calls + 1 end,
  set_scale = function() S.calls = S.calls + 1 end,
  get = function() return nil end,
}

local nid = 0
_G.factory = { create = function() nid = nid + 1; return nid end }
_G.msg = { url = function(a, b, c) return { a, b, c } end, post = function() S.calls = S.calls + 1 end }

return S
