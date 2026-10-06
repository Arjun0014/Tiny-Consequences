-- Skipping time, seen (S25, D-214): when a skip begins the camera rises to the whole planet and the world turns under
-- it - the sun's light sweeping over it day after day as the hours pass (the planet's own day and night, from the sim's
-- clock) - and when the skip ends it flies back down to where the player was looking, at the same zoom. A view of the
-- skip only: it reads the speed controller's state and moves the camera, nothing else.
local camera = require("game.view.camera")

local M = {}
M.ZOOM_OUT = 0.14      -- the whole planet in the frame
M.SPIN = 0.42          -- radians a second about the planet's axis while time runs
M.active = false

function M.update(dt)
  local speed = require("game.platform.speed")
  local sk = speed.skip
  if sk and not M.active then
    M.active = true
    M.saved = camera.get_frame()
    M.follow, M.tag = camera.follow, camera.follow_tag
    camera.stop_follow()
    camera.parked = true
    local f = M.saved
    -- (up and out: the same place under the eye, the planet whole)
    camera.set_frame(f[1], f[2], f[3], f[4], f[5], f[6], M.ZOOM_OUT)
    M.t = 0
  elseif sk and M.active then
    M.t = (M.t or 0) + dt
    -- (the turn eases in as the camera rises)
    local k = math.min(1, M.t / 1.2)
    camera.spin(M.SPIN * k * dt)
  elseif not sk and M.active then
    M.active = false
    camera.parked = false
    local f = M.saved
    if f then
      -- (back down to where the eye was, the planet having turned to bring it round again)
      camera.set_frame(f[1], f[2], f[3], f[4], f[5], f[6], f[7])
    end
    -- (the follow parked by the skip comes back; one that ended meanwhile returns no direction and stops itself)
    if M.follow and not camera.follow then camera.start_follow(M.follow, M.tag) end
    M.saved, M.follow, M.tag = nil, nil, nil
  end
end

return M
