local T = require("tests.sim.lib")
local cal = require("sim.calendar")
local M = {}
function M.conversions()
  T.eq(cal.year(0), 0); T.eq(cal.year(47), 0); T.eq(cal.year(48), 1)
  T.eq(cal.tick_of_year(50), 2)
  T.eq(cal.month(4), 1); T.eq(cal.month(47), 11)
  T.eq(cal.season(11), 0); T.eq(cal.season(12), 1); T.eq(cal.season(36), 2 + 1)
  T.eq(cal.season_name(24), "Autumn")
  T.eq(cal.label(48 * 12 + 8), "Year 12 \194\183 Late Spring")
  T.eq(cal.label(0), "Year 0 \194\183 Early Spring")
  T.ok(cal.is_year_start(96)); T.ok(not cal.is_year_start(97))
  T.eq(cal.ticks(2.5), 120)
end
return M
