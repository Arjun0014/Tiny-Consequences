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
M.hour_f = 0                     -- view clock: sim hour + fraction of the next hour (D-050)
M.focus_lon = 0                  -- longitude under the camera (the HUD shows local time there)
M.sun_real = vmath.vector3(1, 0, 0)  -- direction to the sim's sun (subsolar point)
M.daynight = 1                   -- 1: light from the real sun; 0: camera-side sun (fast speeds)

-- Shader inputs (vector4s reused every frame; the render script copies them into a buffer)
M.u_cam = vmath.vector4(0, 0, 54, 0)
M.u_sun = vmath.vector4(-0.5, 0.6, 0.6, 0)
M.u_tex = vmath.vector4(0, 0, 0, 0)
M.u_anim = vmath.vector4(0, 1, 0, 0)
M.u_sky = vmath.vector4(0, 16 / 9, 0, 0)
M.u_atmo = vmath.vector4(10, 0.02, 0.11, 1)

-- Renderer v2 (D-048): sun shadow map + post (tilt-shift DoF, bloom, grade)
M.shadow_on = true
M.shadow_movers = true
M.post_on = true
M.SHADOW_SIZE = 2048
M.light_view = vmath.matrix4()
M.light_proj = vmath.matrix4()
M.u_sh0 = vmath.vector4(0, 0, 0, 0)          -- rows of bias * sun proj * sun view
M.u_sh1 = vmath.vector4(0, 0, 0, 0)
M.u_sh2 = vmath.vector4(0, 0, 0, 0)
M.u_shn = vmath.vector4(0.02, 0, 0, 0)       -- x = normal offset (world units)
M.u_shadowp = vmath.vector4(1 / 2048, 0.0003, 0.9, 1)  -- 1/size, depth bias, strength, on
M.u_focus = vmath.vector4(40, 0.6, 0.08, 0.2) -- focus distance, strength, in-focus band, sky CoC
M.u_post = vmath.vector4(0.35, 0.28, 1.1, 1.05) -- bloom, vignette, saturation, contrast
M.u_cut = vmath.vector4(0, 0, 10, 0)           -- cloud cut-away: focus point (world), strength

-- Filled by the render script each frame (used by picking/projection later)
M.proj = vmath.matrix4()
M.window_w, M.window_h = 1280, 720

-- Loading screen
M.loading = { active = true, frac = 0, text = "Growing a tiny world..." }

return M
