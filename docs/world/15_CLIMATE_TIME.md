# W15 — Time, day/night, seasons and daily weather

Research basis: `00_RESEARCH.md` §8. Supersedes 02 §1 (time) and 04 §3 (speed rates).

## 1. Calendar

| Unit | Hours | Notes |
|---|---|---|
| hour | 1 | the sim's step (`world.step`) |
| day | 24 | local solar day per place |
| week | 168 | planet systems run on UTC week starts (`W.tick` = completed weeks) |
| month | 672 (4 weeks) | |
| season | 2,016 (12 weeks) | spring = weeks 0–11 (northern labels) |
| year | 8,064 (48 weeks, 336 days) | year-boundary snap and save capture |

`W.hour` is the master clock. `W.day = floor(W.hour / 24)`, day of year `doy = W.day mod 336`.
Commands carry the hour they apply at; events carry `tick` (week) and `hour`.

## 2. Sun, day length and local time

- **Declination**: δ = 23.44° × sin(2π × doy / 336). Spring equinox at doy 0 (start of week 0),
  northern summer solstice at doy 84 (week 12), autumn equinox doy 168, winter solstice doy 252.
  Temperature keeps its ~6-week lag (its annual peak at week 18), as on Earth.
- **Subsolar point**: latitude δ, longitude λs = −15° × (UTC hour − 12). Local solar time at
  longitude λ: `h_local = (UTC hour + λ/15) mod 24`.
- **Day length** (Forsythe CBM form): with p = 0.833° (the sun's centre below the horizon at
  sunrise), the sunrise hour angle is `cos ω₀ = −(sin p + sin φ sin δ) / (cos φ cos δ)`, clamped to
  [−1, 1]; day length `D = (24/π) × ω₀` hours; sunrise = 12 − D/2, sunset = 12 + D/2 local solar
  time. Polar day (D = 24) and polar night (D = 0) fall out; hemispheres are opposite by
  construction.
- **Human rhythm** (Yetish 2015): wake = clamp(sunrise − 0.5, 4, 8); sleep = clamp(sunset + 3.3, 19,
  23.5). Work window = wake + 1 → min(sunset, wake + 12), ≤ 10 working hours.

## 3. Seasons in the sim (spatially varying)

- Temperature: the existing formula `T_base(lat) + season(lat, week) × amp(lat)` with a southern
  phase flip — seasons are opposite across the equator and strongest at high latitudes, damped over
  oceans and coasts.
- Crops: sowing windows and ripening from each field's own temperatures (`11` §3), so northern and
  southern villages sow half a year apart, tropical villages can crop most of the year, and
  high-latitude villages have short, risky seasons.
- Snow, sea ice, frozen ground: existing ice model; sea ice at the mooring stops boats.
- Animals: birth pulses in the local spring; herds move down and toward warmth in autumn.
- Fish: spawning pulse in local spring/summer.
- Fire risk: dry, warm, fuel-rich summers (existing).
- People: short winter days mean fewer working hours; heating fuel dominates in cold months; winter
  storms keep boats in.

## 4. Daily weather (downscaled from the weekly planet state)

Each place's day is realised deterministically from the week's fields (WGEN idea, keyed by
rng(cell, day), no stored state):
- **Wet day?** A two-state chain: p(wet | previous day dry) = clamp(0.1 + 0.8 × P_w, 0.02, 0.9),
  p(wet | wet) = clamp(p_dw + 0.25, …), where P_w = the week's precip relative to a wet week. Seven
  days are generated from the week's first day, so the chain is exact within a week.
- **Amount**: the week's precip is distributed over its wet days in proportion to a gamma-like
  weight (conserving the weekly total, so hydrology is unchanged).
- **Storm**: a wet day whose amount ≥ 2.5 × the week's mean daily amount and ≥ a floor, or wind ≥
  threshold (strong westerlies/trades × a daily gust factor) → boats stay in, outdoor work stops.
- **Snow day**: wet day below 0 °C.
- **Daily temperature**: the week's temperature + a small daily wobble; diurnal range 4–12 °C
  (larger when dry and inland) for display and fuel.
- The view's clouds and rain use the same daily state, so what you see is what villagers react to.

## 5. Day and night in the view

- The sun direction follows the subsolar point; the terminator sweeps across the planet; high
  latitudes show polar day and night with the seasons.
- Night: dark blue ambient with moonlight rim, stars in the sky, lit windows in homes where people
  are awake, fires/lamps at piers and the square, chimney smoke all day in cold weather.
- Fast speeds: when a real day lasts < 3 s, lighting blends toward a steady "daylight" (sun kept on
  the camera side as before) so the planet doesn't strobe; the sim is unaffected.

## 6. Speeds

As in `10_WORLD_SYSTEMS.md` §6: ▶ 1 h / 5 s, ▶▶ 1 h / 0.8 s, ▶▶▶ 1 day / 3 s, ⏩ 1 week / s,
⏩⏩ 16 weeks / s, ⏩⏩⏩ as fast as the frame budget allows (≥ 1.5 years/s target).
