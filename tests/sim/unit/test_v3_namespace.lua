-- (S20) Work, plan, start, pick and drop kinds are one namespace per table: a second definition of
-- the same name silently replaces the first (the helper's "lift" replaced the tan pit's - hides were
-- never lifted again and a village starved in its eighth year). Every name is defined once.
local T = require("tests.sim.lib")

local M = {}

function M.every_kind_is_defined_once()
  local seen, dups = {}, {}
  for _, path in ipairs({ "sim/agents/act.lua", "sim/agents/think.lua" }) do
    local f = assert(io.open(path, "r"))
    local n = 0
    for line in f:lines() do
      n = n + 1
      local tbl, name = line:match("^(%u+)%.([%w_]+) = function")
      if tbl then
        local key = tbl .. "." .. name
        if seen[key] then dups[#dups + 1] = key .. " (" .. seen[key] .. " and " .. path .. ":" .. n .. ")" end
        seen[key] = path .. ":" .. n
      end
    end
    f:close()
  end
  T.ok(#dups == 0, "defined twice: " .. table.concat(dups, ", "))
  local k = 0
  for _ in pairs(seen) do k = k + 1 end
  return k .. " kinds, each defined once"
end

return M
