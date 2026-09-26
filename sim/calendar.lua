-- Calendar (02_WORLD_MODEL.md §1). 1 tick = 1 week, 4 ticks = 1 month, 48 ticks = 1 year.
local calendar = {}

calendar.TICKS_PER_MONTH = 4
calendar.TICKS_PER_SEASON = 12
calendar.TICKS_PER_YEAR = 48

local SEASONS = { "Spring", "Summer", "Autumn", "Winter" }
local PARTS = { "Early", "Mid", "Late" }
calendar.SEASONS = SEASONS

function calendar.year(tick) return math.floor(tick / 48) end
function calendar.tick_of_year(tick) return tick % 48 end
function calendar.month(tick) return math.floor((tick % 48) / 4) end        -- 0..11
function calendar.season(tick) return math.floor((tick % 48) / 12) end      -- 0..3 (north)
function calendar.season_name(tick) return SEASONS[calendar.season(tick) + 1] end

-- "Year 12 · Late Spring"
function calendar.label(tick)
  local toy = tick % 48
  local s = math.floor(toy / 12)
  local part = math.floor((toy % 12) / 4)
  return "Year " .. math.floor(tick / 48) .. " \194\183 " .. PARTS[part + 1] .. " " .. SEASONS[s + 1]
end

function calendar.is_year_start(tick) return tick % 48 == 0 end
function calendar.ticks(years) return math.floor(years * 48 + 0.5) end

return calendar
