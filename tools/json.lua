-- Minimal JSON for tool/test files (objects, arrays, strings, numbers, booleans, null).
-- encode sorts object keys and indents, so committed files diff cleanly.
local J = {}

local function enc(v, ind, out)
  local t = type(v)
  if t == "table" then
    local pad = string.rep("  ", ind + 1)
    if #v > 0 or next(v) == nil then
      out[#out + 1] = "["
      for i = 1, #v do
        out[#out + 1] = (i > 1 and ", " or "")
        enc(v[i], ind + 1, out)
      end
      out[#out + 1] = "]"
    else
      local keys = {}
      for k in pairs(v) do keys[#keys + 1] = tostring(k) end
      table.sort(keys)
      out[#out + 1] = "{\n"
      for i, k in ipairs(keys) do
        out[#out + 1] = pad .. string.format("%q", k) .. ": "
        enc(v[k], ind + 1, out)
        out[#out + 1] = (i < #keys and ",\n" or "\n")
      end
      out[#out + 1] = string.rep("  ", ind) .. "}"
    end
  elseif t == "string" then
    out[#out + 1] = string.format("%q", v)
  elseif t == "number" then
    if v == math.floor(v) and math.abs(v) < 1e15 then out[#out + 1] = string.format("%d", v)
    else out[#out + 1] = string.format("%.6g", v) end
  elseif t == "boolean" then
    out[#out + 1] = tostring(v)
  else
    out[#out + 1] = "null"
  end
end

function J.encode(v)
  local out = {}
  enc(v, 0, out)
  return table.concat(out) .. "\n"
end

function J.decode(s)
  local pos = 1
  local function ws() pos = s:find("[^ \t\r\n]", pos) or #s + 1 end
  local val
  local function str()
    local e = pos + 1
    local buf = {}
    while true do
      local c = s:sub(e, e)
      if c == '"' then break end
      if c == "\\" then
        local n = s:sub(e + 1, e + 1)
        local map = { n = "\n", t = "\t", r = "\r", b = "\b", f = "\f" }
        buf[#buf + 1] = map[n] or n
        e = e + 2
      else
        buf[#buf + 1] = c
        e = e + 1
      end
    end
    pos = e + 1
    return table.concat(buf)
  end
  val = function()
    ws()
    local c = s:sub(pos, pos)
    if c == "{" then
      pos = pos + 1
      local t = {}
      ws()
      if s:sub(pos, pos) == "}" then pos = pos + 1; return t end
      while true do
        ws()
        local k = str()
        ws(); pos = pos + 1   -- ':'
        t[k] = val()
        ws()
        local d = s:sub(pos, pos)
        pos = pos + 1
        if d == "}" then return t end
      end
    elseif c == "[" then
      pos = pos + 1
      local t = {}
      ws()
      if s:sub(pos, pos) == "]" then pos = pos + 1; return t end
      while true do
        t[#t + 1] = val()
        ws()
        local d = s:sub(pos, pos)
        pos = pos + 1
        if d == "]" then return t end
      end
    elseif c == '"' then
      return str()
    elseif s:sub(pos, pos + 3) == "true" then pos = pos + 4; return true
    elseif s:sub(pos, pos + 4) == "false" then pos = pos + 5; return false
    elseif s:sub(pos, pos + 3) == "null" then pos = pos + 4; return nil
    else
      local num = s:match("^-?%d+%.?%d*[eE]?[-+]?%d*", pos)
      pos = pos + #num
      return tonumber(num)
    end
  end
  return val()
end

return J
