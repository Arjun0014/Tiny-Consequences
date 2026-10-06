-- The idle view (S26): when nobody has touched the game for a while, the HUD slides away (game/ui/hud.gui_script) and the
-- camera watches the real, running world on its own - the whole planet turning, then down over a region with a village in
-- daylight, then low over that village circling it slowly, then out again to the planet - in long, eased glides. It moves
-- only the camera and reads the world (which village, where the sun is); the world runs as the player left it. Any input
-- ends it at once and the camera stays where it is, the player's again.
local camera = require("game.view.camera")

local M = {}

M.active = false
M.SHOTS = {
  { kind = "wide", zoom = 0.12, t = 34, spin = 0.05, orbit = 0 },
  { kind = "region", zoom = 0.58, t = 26, spin = 0.010, orbit = 0.018 },
  { kind = "village", zoom = 1.12, t = 40, spin = 0, orbit = 0.05 },
}
M.GLIDE = 0.45          -- the camera's tween rate while idle (1/s; the player's is 3): slow, smooth glides

local function local_hour(W, s, cell)
  if W.v3 then return require("sim.agents.clock").local_hour(require("game.platform.speed").v3_t or W.hour, W.grid.lon[cell]) end
  return require("sim.society.daylight").local_hour(W.hour or 0, W.grid.lon[cell])
end

-- The places to look at: a v3 world's settlements (their squares), else the legacy villages (their cells); in daylight first.
local function places(W)
  local out, day = {}, {}
  if W.v3 and W.S and W.S.v3 then
    for _, s in ipairs(W.S.v3.settlements) do
      if s.alive ~= false and s.frame then
        local x, y, z = require("sim.agents.map").unit(s, s.sqx or 0, s.sqy or 0)
        local o = { x = x, y = y, z = z, id = s.id }
        out[#out + 1] = o
        local lh = local_hour(W, s, s.cell)
        if lh >= 7 and lh <= 17 then day[#day + 1] = o end
      end
    end
  else
    local g = W.grid
    for _, v in ipairs(W.ent and W.ent.villages or {}) do
      if v.alive ~= false and v.cell then
        local o = { x = g.X[v.cell], y = g.Y[v.cell], z = g.Z[v.cell], id = v.id }
        out[#out + 1] = o
        local lh = local_hour(W, nil, v.cell)
        if lh >= 7 and lh <= 17 then day[#day + 1] = o end
      end
    end
  end
  return #day > 0 and day or out
end

local function pick(W)
  local list = places(W)
  if #list == 0 then return nil end
  M.turn = (M.turn or 0) + 1
  -- (a different place each time round, where the sun is up)
  return list[(M.turn - 1) % #list + 1]
end

local function begin(W, i)
  M.i, M.t = i, 0
  local sh = M.SHOTS[i]
  if sh.kind == "wide" then
    local x, y, z = camera.look_dir()
    camera.fly_to_dir(x, y, z, sh.zoom)
  else
    if sh.kind == "region" or not M.place then M.place = pick(W) end
    local p = M.place
    if p then camera.fly_to_dir(p.x, p.y, p.z, sh.zoom)
    else
      local x, y, z = camera.look_dir()
      camera.fly_to_dir(x, y, z, sh.zoom)
    end
  end
end

function M.start(W)
  if M.active or not W then return end
  M.active = true
  M.speed0 = camera.SPEED
  camera.SPEED = M.GLIDE
  camera.stop_follow()
  M.place = nil
  -- (from close in, the first glide is out to the planet; from afar, down to a village)
  begin(W, camera.zoom > 0.4 and 1 or 2)
end

function M.stop()
  if not M.active then return end
  M.active = false
  camera.SPEED = M.speed0 or 3.0
  camera.touch()
end

function M.update(W, dt)
  if not M.active or not W then return end
  M.t = M.t + dt
  local sh = M.SHOTS[M.i]
  -- the slow drift of the shot, eased in as the glide arrives
  local ease = math.min(1, M.t / 6)
  if sh.spin ~= 0 then camera.spin(sh.spin * ease * dt) end
  if sh.orbit ~= 0 then camera.orbit(sh.orbit * ease * dt) end
  -- (a gentle breath in the wide shot's distance)
  if sh.kind == "wide" then camera.tzoom = sh.zoom + 0.015 * math.sin(M.t * 0.15) end
  camera.idle = 0
  if M.t >= sh.t then begin(W, M.i % #M.SHOTS + 1) end
end

return M
