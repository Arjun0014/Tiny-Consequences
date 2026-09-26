-- Shared view state (Stage 1.8). With [script] shared_state = 1 the render script and the game
-- scripts share one Lua state, so the render script reads camera matrices and shader inputs
-- from this module instead of receiving messages.
local M = {}

M.R = 10                         -- planet radius (world units)
M.fov = math.rad(30)             -- vertical field of view
M.near, M.far = 0.5, 400
M.view = vmath.matrix4()
M.eye = vmath.vector3(0, 0, 54)
M.target = vmath.vector3(0, 0, 0)
M.up = vmath.vector3(0, 1, 0)
M.sun = vmath.vector3(-0.5, 0.6, 0.6)
M.time = 0                       -- visual time (stops while frozen)
M.frozen = false
M.planet_on = false              -- world meshes ready to draw

-- Shader inputs (vector4s reused every frame; the render script copies them into a buffer)
M.u_cam = vmath.vector4(0, 0, 54, 0)
M.u_sun = vmath.vector4(-0.5, 0.6, 0.6, 0)
M.u_tex = vmath.vector4(0, 0, 0, 0)
M.u_anim = vmath.vector4(0, 1, 0, 0)
M.u_sky = vmath.vector4(0, 16 / 9, 0, 0)
M.u_atmo = vmath.vector4(10, 0.02, 0.11, 1)

-- Filled by the render script each frame (used by picking/projection later)
M.proj = vmath.matrix4()
M.window_w, M.window_h = 1280, 720

-- Loading screen
M.loading = { active = true, frac = 0, text = "Growing a tiny world..." }

return M
