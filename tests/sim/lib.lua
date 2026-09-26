-- Minimal test harness shared by unit/property/scenario tests.
local T = { results = {}, args = {} }

local function fmt(v)
  if type(v) == "number" then return string.format("%.6g", v) end
  return tostring(v)
end

function T.ok(cond, msg)
  if not cond then error(msg or "assertion failed", 2) end
end

function T.eq(a, b, msg)
  if a ~= b then error((msg or "not equal") .. ": " .. fmt(a) .. " ~= " .. fmt(b), 2) end
end

function T.near(a, b, tol, msg)
  if math.abs(a - b) > (tol or 1e-9) then
    error((msg or "not near") .. ": " .. fmt(a) .. " vs " .. fmt(b) .. " (tol " .. fmt(tol) .. ")", 2)
  end
end

function T.between(v, lo, hi, msg)
  if not (v >= lo and v <= hi) then
    error((msg or "out of range") .. ": " .. fmt(v) .. " not in [" .. fmt(lo) .. ", " .. fmt(hi) .. "]", 2)
  end
end

function T.record(name, pass, detail, extra)
  T.results[#T.results + 1] = { name = name, pass = pass, detail = detail, extra = extra }
  local line = (pass and "  ok   " or "  FAIL ") .. name .. (detail and ("  -- " .. detail) or "")
  print(line)
  io.stdout:flush()
end

-- A module is a table of { [name] = function() ... end } or has a .tests list of {name, fn}.
-- Test functions may return a string detail (printed) on success.
function T.run_module(modname, mod)
  local list = {}
  if mod.tests then
    list = mod.tests
  else
    local names = {}
    for k in pairs(mod) do if type(mod[k]) == "function" then names[#names + 1] = k end end
    table.sort(names)
    for _, k in ipairs(names) do list[#list + 1] = { k, mod[k] } end
  end
  for _, t in ipairs(list) do
    local name = modname:gsub("^tests%.sim%.", "") .. "." .. t[1]
    if not T.args.only or name:find(T.args.only, 1, true) then
      if (t.chapter or 1) <= (T.args.chapter or 1) then
        local t0 = os.clock()
        local ok, res = xpcall(t[2], debug.traceback)
        local dt = os.clock() - t0
        local detail
        if ok then detail = type(res) == "string" and res or nil else detail = tostring(res) end
        if ok then detail = (detail and (detail .. "  ") or "") .. string.format("(%.1fs)", dt) end
        T.record(name, ok, detail)
      end
    end
  end
end

function T.summary(elapsed)
  local pass, fail = 0, 0
  local lines = {}
  for _, r in ipairs(T.results) do
    if r.pass then pass = pass + 1 else fail = fail + 1 end
    lines[#lines + 1] = (r.pass and "ok   " or "FAIL ") .. r.name .. (r.detail and ("  -- " .. r.detail) or "")
  end
  local s = string.format("\n%d passed, %d failed (%.1fs)", pass, fail, elapsed or 0)
  print(s)
  os.execute(package.config:sub(1, 1) == "\\" and "mkdir out\\reports 2>nul" or "mkdir -p out/reports")
  local f = io.open("out/reports/latest.txt", "w")
  if f then
    f:write(table.concat(lines, "\n"), s, "\n")
    f:close()
  end
  return fail == 0
end

return T
