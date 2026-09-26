-- Minimal PNG writer (RGB, 8-bit) in pure Lua 5.1 + bit. Uses stored (uncompressed) deflate
-- blocks: files are larger but correct and dependency-free. Tools only (not used by sim/).
local bit = bit or require("bit")
local band, bxor, rshift = bit.band, bit.bxor, bit.rshift
local char, concat = string.char, table.concat

local png = {}

local crc_table = {}
for i = 0, 255 do
  local c = i
  for _ = 1, 8 do
    if band(c, 1) == 1 then c = bxor(0xEDB88320, rshift(c, 1)) else c = rshift(c, 1) end
  end
  crc_table[i] = c
end

local function crc32(s)
  local c = 0xFFFFFFFF
  for i = 1, #s do
    c = bxor(crc_table[band(bxor(c, s:byte(i)), 0xFF)], rshift(c, 8))
  end
  return bxor(c, 0xFFFFFFFF) % 4294967296
end

local function u32(v)
  v = v % 4294967296
  return char(math.floor(v / 16777216) % 256, math.floor(v / 65536) % 256, math.floor(v / 256) % 256, v % 256)
end

local function chunk(kind, data)
  return u32(#data) .. kind .. data .. u32(crc32(kind .. data))
end

-- pixels: function(x, y) -> r, g, b (0..255), x in [0,w), y in [0,h)
function png.write(path, w, h, pixels)
  local rows = {}
  for y = 0, h - 1 do
    local row = { "\0" }
    for x = 0, w - 1 do
      local r, g, b = pixels(x, y)
      row[#row + 1] = char(r, g, b)
    end
    rows[#rows + 1] = concat(row)
  end
  local raw = concat(rows)
  -- zlib: header, stored blocks (≤ 65535 bytes), adler32
  local parts = { "\120\1" }
  local a, b = 1, 0
  for i = 1, #raw do
    a = (a + raw:byte(i)) % 65521
    b = (b + a) % 65521
  end
  local pos = 1
  local n = #raw
  while pos <= n do
    local len = math.min(65535, n - pos + 1)
    local final = (pos + len > n) and 1 or 0
    local nlen = 65535 - len
    parts[#parts + 1] = char(final, len % 256, math.floor(len / 256), nlen % 256, math.floor(nlen / 256))
    parts[#parts + 1] = raw:sub(pos, pos + len - 1)
    pos = pos + len
  end
  parts[#parts + 1] = u32(b * 65536 + a)
  local idat = concat(parts)
  local ihdr = u32(w) .. u32(h) .. char(8, 2, 0, 0, 0)
  local f = assert(io.open(path, "wb"))
  f:write("\137PNG\r\n\26\n", chunk("IHDR", ihdr), chunk("IDAT", idat), chunk("IEND", ""))
  f:close()
end

return png
