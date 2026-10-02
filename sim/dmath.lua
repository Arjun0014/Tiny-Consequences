-- Deterministic transcendental functions for the simulation (S14).
-- The browser's Lua (emscripten: musl's libm, and `^` through pow) and LuaJIT (the C runtime's
-- libm on Windows) disagree in the last bit for ~1.5% of sin/cos inputs, ~0.5% of exp, ~0.9% of
-- acos, and even x ^ 2 differs from x * x now and then in the browser (scripts/mathcheck.mjs). A
-- last-bit difference in an animal's position or a sunrise is enough to send a walker over a
-- different tile and the two runtimes then live different worlds. These are fdlibm's algorithms
-- written with + - * / only (correctly rounded in IEEE double on every runtime), math.floor,
-- math.sqrt (correctly rounded), math.frexp and math.ldexp (exact): the same bits everywhere.
-- Accuracy ~1 ulp (sin, cos, exp, log, atan) - plenty for a simulation; determinism is the point.
-- Pure Lua 5.1.
local M = {}
local floor, sqrt, frexp, ldexp = math.floor, math.sqrt, math.frexp, math.ldexp
local HUGE = math.huge

M.pi = 3.14159265358979311600e+00

-- sin / cos ---------------------------------------------------------------------------------
local S1, S2, S3 = -1.66666666666666324348e-01, 8.33333333332248946124e-03, -1.98412698298579493134e-04
local S4, S5, S6 = 2.75573137070700676789e-06, -2.50507602534068634195e-08, 1.58969099521155010221e-10
local C1, C2, C3 = 4.16666666666666019037e-02, -1.38888888888741095749e-03, 2.48015872894767294178e-05
local C4, C5, C6 = -2.75573143513906633035e-07, 2.08757232129817482790e-09, -1.13596475577881948265e-11
local INVPIO2 = 6.36619772367581382433e-01
local PIO2_1 = 1.57079632673412561417e+00     -- the first 33 bits of pi/2
local PIO2_1T = 6.07710050650619224932e-11    -- pi/2 - PIO2_1
local TWO_PI = 6.28318530717958623200e+00

local function ksin(x)
  local z = x * x
  local v = z * x
  local r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)))
  return x + v * (S1 + z * r)
end
local function kcos(x)
  local z = x * x
  local r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))))
  return 1 - (0.5 * z - z * r)
end
-- x = n pi/2 + y, |y| <= pi/4 (Cody-Waite in two parts: n * PIO2_1 is exact for |n| < 2^20)
local function reduce(x)
  if x ~= x or x == HUGE or x == -HUGE then return 0, 0 / 0 end
  if x > 1.6e6 or x < -1.6e6 then x = x - floor(x / TWO_PI) * TWO_PI end
  local n = floor(x * INVPIO2 + 0.5)
  return n % 4, (x - n * PIO2_1) - n * PIO2_1T
end

function M.sin(x)
  if x > -0.7853981633974483 and x < 0.7853981633974483 then return ksin(x) end
  local q, y = reduce(x)
  if q == 0 then return ksin(y) elseif q == 1 then return kcos(y) elseif q == 2 then return -ksin(y) end
  return -kcos(y)
end

function M.cos(x)
  if x > -0.7853981633974483 and x < 0.7853981633974483 then return kcos(x) end
  local q, y = reduce(x)
  if q == 0 then return kcos(y) elseif q == 1 then return -ksin(y) elseif q == 2 then return -kcos(y) end
  return ksin(y)
end

-- exp ---------------------------------------------------------------------------------------
local LN2HI, LN2LO, INVLN2 = 6.93147180369123816490e-01, 1.90821492927058770002e-10, 1.44269504088896338700e+00
local P1, P2, P3 = 1.66666666666666019037e-01, -2.77777777770155933842e-03, 6.61375632143793436117e-05
local P4, P5 = -1.65339022054652515390e-06, 4.13813679705723846039e-08

function M.exp(x)
  if x ~= x then return x end
  if x > 709.782712893384 then return HUGE end
  if x < -745.1332191019411 then return 0 end
  if x > -3.725290298461914e-09 and x < 3.725290298461914e-09 then return 1 + x end
  local k = floor(x * INVLN2 + 0.5)
  local hi = x - k * LN2HI
  local lo = k * LN2LO
  local r = hi - lo
  local t = r * r
  local c = r - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))))
  local y = 1 - ((lo - (r * c) / (2 - c)) - hi)
  return ldexp(y, k)
end

-- log ---------------------------------------------------------------------------------------
local LG1, LG2, LG3 = 6.666666666666735130e-01, 3.999999999940941908e-01, 2.857142874366239149e-01
local LG4, LG5, LG6, LG7 = 2.222219843214978396e-01, 1.818357216161805012e-01, 1.531383769920937332e-01, 1.479819860511658591e-01
local SQRT_HALF = 0.70710678118654757

function M.log(x)
  if x ~= x or x < 0 then return 0 / 0 end
  if x == 0 then return -HUGE end
  if x == HUGE then return HUGE end
  local m, e = frexp(x)              -- x = m 2^e, 0.5 <= m < 1 (exact)
  if m < SQRT_HALF then m = m * 2; e = e - 1 end
  local f = m - 1
  local s = f / (2 + f)
  local z = s * s
  local w = z * z
  local t1 = w * (LG2 + w * (LG4 + w * LG6))
  local t2 = z * (LG1 + w * (LG3 + w * (LG5 + w * LG7)))
  local R = t2 + t1
  local hfsq = 0.5 * f * f
  return e * LN2HI - ((hfsq - (s * (hfsq + R) + e * LN2LO)) - f)
end

-- pow: integer exponents by multiplication (x ^ 2 is x * x, exactly as LuaJIT computes it), the
-- rest as exp(y log x).
function M.pow(x, y)
  if y == 2 then return x * x end
  if y == 1 then return x end
  if y == 0 then return 1 end
  if y == 0.5 then return sqrt(x) end
  if y == floor(y) and y > -64 and y < 64 then
    local n = y < 0 and -y or y
    local r, b = 1, x
    while n > 0 do
      if n % 2 == 1 then r = r * b end
      b = b * b
      n = floor(n / 2)
    end
    if y < 0 then return 1 / r end
    return r
  end
  if x == 0 then return y > 0 and 0 or HUGE end
  if x < 0 then return 0 / 0 end
  return M.exp(y * M.log(x))
end

-- atan, atan2, asin, acos -------------------------------------------------------------------
local ATANHI = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 }
local ATANLO = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 }
local A0, A1, A2, A3 = 3.33333333333329318027e-01, -1.99999999998764832476e-01, 1.42857142725034663711e-01, -1.11111104054623557880e-01
local A4, A5, A6, A7 = 9.09088713343650656196e-02, -7.69187620504482999495e-02, 6.66107313738753120669e-02, -5.83357013379057348645e-02
local A8, A9, A10 = 4.97687799461593236017e-02, -3.65315727442169155270e-02, 1.62858201153657823623e-02

function M.atan(x)
  if x ~= x then return x end
  local neg = x < 0
  if neg then x = -x end
  local r
  if x >= 7.378697629483821e+19 then
    r = ATANHI[4] + ATANLO[4]
  else
    local id
    if x < 0.4375 then
      if x < 3.725290298461914e-09 then return neg and -x or x end
      id = 0
    elseif x < 1.1875 then
      if x < 0.6875 then id = 1; x = (2 * x - 1) / (2 + x) else id = 2; x = (x - 1) / (x + 1) end
    elseif x < 2.4375 then id = 3; x = (x - 1.5) / (1 + 1.5 * x)
    else id = 4; x = -1 / x end
    local z = x * x
    local w = z * z
    local s1 = z * (A0 + w * (A2 + w * (A4 + w * (A6 + w * (A8 + w * A10)))))
    local s2 = w * (A1 + w * (A3 + w * (A5 + w * (A7 + w * A9))))
    if id == 0 then r = x - x * (s1 + s2)
    else r = ATANHI[id] - ((x * (s1 + s2) - ATANLO[id]) - x) end
  end
  return neg and -r or r
end

local PI, PI_O2 = 3.14159265358979311600e+00, 1.57079632679489655800e+00
function M.atan2(y, x)
  if x ~= x or y ~= y then return 0 / 0 end
  if y == 0 then
    if x > 0 or (x == 0 and 1 / x > 0) then return y end
    return (1 / y > 0) and PI or -PI
  end
  if x == 0 then return y > 0 and PI_O2 or -PI_O2 end
  local a = M.atan(y / x)
  if x > 0 then return a end
  if y >= 0 then return a + PI end
  return a - PI
end

function M.asin(x)
  if x > 1 or x < -1 then return 0 / 0 end
  if x == 1 then return PI_O2 elseif x == -1 then return -PI_O2 end
  return M.atan(x / sqrt((1 - x) * (1 + x)))
end

function M.acos(x)
  if x > 1 or x < -1 then return 0 / 0 end
  if x == 1 then return 0 elseif x == -1 then return PI end
  -- acos x = 2 atan sqrt((1 - x) / (1 + x)): accurate near both ends
  return 2 * M.atan(sqrt((1 - x) / (1 + x)))
end

-- A square, for distances (x ^ 2 goes through pow in the browser's Lua).
function M.sq(v) return v * v end

return M
