using System;

namespace TC2.Core.Foundation
{
    /// <summary>
    /// Deterministic double-precision math (D2-003). Every function is built only from IEEE-754
    /// correctly rounded operations (+ - * / sqrt), floor, comparisons and bit manipulation, so results
    /// are bit-identical on .NET (RyuJIT), Mono and IL2CPP/wasm. Kernels follow fdlibm (Sun, freely
    /// redistributable) polynomial approximations; accuracy ~1 ulp on the reduced range.
    /// Never use System.Math transcendental functions in simulation code.
    /// </summary>
    public static class DMath
    {
        public const double PI = 3.14159265358979311600e+00;
        public const double TwoPi = 6.28318530717958623200e+00;
        public const double HalfPi = 1.57079632679489655800e+00;
        public const double Deg2Rad = PI / 180.0;
        public const double Rad2Deg = 180.0 / PI;
        public const double Ln2 = 6.93147180559945286227e-01;
        public const double E = 2.71828182845904509080e+00;

        // ---------- basic exact helpers ----------
        public static double Sqrt(double x) => Math.Sqrt(x);     // IEEE correctly rounded everywhere
        public static double Floor(double x) => Math.Floor(x);   // exact
        public static double Abs(double x) => x < 0 ? -x : x;
        public static double Min(double a, double b) => a < b ? a : b;
        public static double Max(double a, double b) => a > b ? a : b;
        public static double Clamp(double x, double lo, double hi) => x < lo ? lo : (x > hi ? hi : x);
        public static double Clamp01(double x) => x < 0 ? 0 : (x > 1 ? 1 : x);
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
        public static double SmoothStep(double e0, double e1, double x)
        {
            double t = Clamp01((x - e0) / (e1 - e0));
            return t * t * (3.0 - 2.0 * t);
        }
        /// <summary>Round half away from zero... no: round half up (floor(x + 0.5)), exact and portable.</summary>
        public static double RoundHalfUp(double x) => Math.Floor(x + 0.5);
        public static long ToLong(double x) { if (x >= 9.2e18) return long.MaxValue; if (x <= -9.2e18) return long.MinValue; if (double.IsNaN(x)) return 0; return (long)x; }
        public static int ToInt(double x) { if (x >= 2147483647.0) return int.MaxValue; if (x <= -2147483648.0) return int.MinValue; if (double.IsNaN(x)) return 0; return (int)x; }
        public static int FloorToInt(double x) => ToInt(Math.Floor(x));

        // ---------- sin / cos ----------
        // pi/2 split into three parts (fdlibm __ieee754_rem_pio2 medium case)
        const double InvPio2 = 6.36619772367581382433e-01;
        const double Pio2_1 = 1.57079632673412561417e+00;
        const double Pio2_1t = 6.07710050650619224932e-11;
        const double Pio2_2 = 6.07710050630396597660e-11;
        const double Pio2_2t = 2.02226624879595063154e-21;
        const double Pio2_3 = 2.02226624871116645580e-21;
        const double Pio2_3t = 8.47842766036889956997e-32;

        const double S1 = -1.66666666666666324348e-01, S2 = 8.33333333332248946124e-03, S3 = -1.98412698298579493134e-04,
                     S4 = 2.75573137070700676789e-06, S5 = -2.50507602534068634195e-08, S6 = 1.58969099521155010221e-10;
        const double C1 = 4.16666666666666019037e-02, C2 = -1.38888888888741095749e-03, C3 = 2.48015872894767294178e-05,
                     C4 = -2.75573143513906633035e-07, C5 = 2.08757232129817482790e-09, C6 = -1.13596475577881948265e-11;

        static double KSin(double x, double y)
        {
            double z = x * x;
            double v = z * x;
            double r = S2 + z * (S3 + z * (S4 + z * (S5 + z * S6)));
            return x - ((z * (0.5 * y - v * r) - y) - v * S1);
        }

        static double KCos(double x, double y)
        {
            double z = x * x;
            double r = z * (C1 + z * (C2 + z * (C3 + z * (C4 + z * (C5 + z * C6)))));
            double hz = 0.5 * z;
            double w = 1.0 - hz;
            return w + (((1.0 - w) - hz) + (z * r - x * y));
        }

        /// <summary>Reduces x to y0 + y1 in [-pi/4, pi/4]; returns the quadrant n (mod 4 used).
        /// Accurate for |x| up to ~1e9 (three-part Cody-Waite); larger arguments lose accuracy but stay deterministic.</summary>
        static int RemPio2(double x, out double y0, out double y1)
        {
            double fn = Math.Floor(x * InvPio2 + 0.5);
            double r = x - fn * Pio2_1;
            double w = fn * Pio2_1t;
            double y = r - w;
            // second iteration for better accuracy when cancellation is large
            double t = r;
            w = fn * Pio2_2;
            r = t - w;
            w = fn * Pio2_2t - ((t - r) - w);
            y = r - w;
            // third iteration
            t = r;
            w = fn * Pio2_3;
            r = t - w;
            w = fn * Pio2_3t - ((t - r) - w);
            y = r - w;
            y0 = y;
            y1 = (r - y) - w;
            long n = (long)fn;
            return (int)(n & 3);
        }

        public static double Sin(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            double ax = Abs(x);
            if (ax <= 7.85398163397448278999e-01) // pi/4
            {
                if (ax < 1e-9) return x;
                return KSin(x, 0.0);
            }
            if (double.IsInfinity(x)) return double.NaN;
            int n = RemPio2(x, out double y0, out double y1);
            switch (n)
            {
                case 0: return KSin(y0, y1);
                case 1: return KCos(y0, y1);
                case 2: return -KSin(y0, y1);
                default: return -KCos(y0, y1);
            }
        }

        public static double Cos(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            double ax = Abs(x);
            if (ax <= 7.85398163397448278999e-01)
            {
                if (ax < 1e-9) return 1.0;
                return KCos(x, 0.0);
            }
            if (double.IsInfinity(x)) return double.NaN;
            int n = RemPio2(x, out double y0, out double y1);
            switch (n)
            {
                case 0: return KCos(y0, y1);
                case 1: return -KSin(y0, y1);
                case 2: return -KCos(y0, y1);
                default: return KSin(y0, y1);
            }
        }

        public static double Tan(double x)
        {
            double c = Cos(x);
            return Sin(x) / c;
        }

        // ---------- exp / log ----------
        const double Ln2Hi = 6.93147180369123816490e-01, Ln2Lo = 1.90821492927058770002e-10, InvLn2 = 1.44269504088896338700e+00;
        const double P1 = 1.66666666666666019037e-01, P2 = -2.77777777770155933842e-03, P3 = 6.61375632143793436117e-05,
                     P4 = -1.65339022054652515390e-06, P5 = 4.13813679705723846039e-08;

        /// <summary>2^k for integer k in [-1074, 1023], exactly.</summary>
        public static double Pow2(int k)
        {
            if (k > 1023) return double.PositiveInfinity;
            if (k >= -1022) return BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);
            if (k < -1074) return 0.0;
            // subnormal: 2^-1022 * 2^(k+1022)
            return BitConverter.Int64BitsToDouble(1L << (k + 1074));
        }

        public static double Exp(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 709.782712893383973096) return double.PositiveInfinity;
            if (x < -745.13321910194110842) return 0.0;
            if (Abs(x) < 1e-300) return 1.0;
            double kf = Math.Floor(x * InvLn2 + 0.5);
            int k = (int)kf;
            double hi = x - kf * Ln2Hi;
            double lo = kf * Ln2Lo;
            double r = hi - lo;
            double t = r * r;
            double c = r - t * (P1 + t * (P2 + t * (P3 + t * (P4 + t * P5))));
            double y = 1.0 - ((lo - (r * c) / (2.0 - c)) - hi);
            // scale by 2^k in two steps to avoid overflow/underflow of the factor
            if (k > 1000) { y *= Pow2(1000); k -= 1000; }
            if (k < -1000) { y *= Pow2(-1000); k += 1000; }
            return y * Pow2(k);
        }

        const double Lg1 = 6.666666666666735130e-01, Lg2 = 3.999999999940941908e-01, Lg3 = 2.857142874366239149e-01,
                     Lg4 = 2.222219843214978396e-01, Lg5 = 1.818357216161805012e-01, Lg6 = 1.531383769920937332e-01,
                     Lg7 = 1.479819860511658591e-01;
        const double Sqrt2Half = 7.07106781186547572737e-01;

        public static double Log(double x)
        {
            if (double.IsNaN(x) || x < 0) return double.NaN;
            if (x == 0) return double.NegativeInfinity;
            if (double.IsPositiveInfinity(x)) return x;
            long bits = BitConverter.DoubleToInt64Bits(x);
            int k = 0;
            if ((bits >> 52) == 0) // subnormal: scale up
            {
                x *= 18014398509481984.0; // 2^54
                bits = BitConverter.DoubleToInt64Bits(x);
                k = -54;
            }
            k += (int)((bits >> 52) & 0x7FF) - 1023;
            long mant = bits & 0x000FFFFFFFFFFFFFL;
            double m = BitConverter.Int64BitsToDouble(mant | 0x3FF0000000000000L); // in [1, 2)
            if (m > 1.41421356237309514547) { m *= 0.5; k += 1; }                // m in [sqrt2/2, sqrt2)
            double f = m - 1.0;
            double s = f / (2.0 + f);
            double z = s * s;
            double w = z * z;
            double t1 = w * (Lg2 + w * (Lg4 + w * Lg6));
            double t2 = z * (Lg1 + w * (Lg3 + w * (Lg5 + w * Lg7)));
            double R = t2 + t1;
            double hfsq = 0.5 * f * f;
            double dk = k;
            return dk * Ln2Hi - ((hfsq - (s * (hfsq + R) + dk * Ln2Lo)) - f);
        }

        public static double Log2(double x) => Log(x) * 1.44269504088896338700e+00;
        public static double Log10(double x) => Log(x) * 4.34294481903251816668e-01;

        /// <summary>x^y for x > 0 via exp(y log x); integer powers of any sign by repeated squaring.</summary>
        public static double Pow(double x, double y)
        {
            if (y == 0) return 1.0;
            if (x == 1) return 1.0;
            if (y == Math.Floor(y) && Abs(y) <= 64) return PowInt(x, (int)y);
            if (x < 0) return double.NaN;
            if (x == 0) return y > 0 ? 0.0 : double.PositiveInfinity;
            return Exp(y * Log(x));
        }

        public static double PowInt(double x, int n)
        {
            bool neg = n < 0;
            int e = neg ? -n : n;
            double r = 1.0, b = x;
            while (e > 0)
            {
                if ((e & 1) != 0) r *= b;
                b *= b;
                e >>= 1;
            }
            return neg ? 1.0 / r : r;
        }

        // ---------- atan / atan2 / asin / acos ----------
        static readonly double[] AtanHi = { 4.63647609000806093515e-01, 7.85398163397448278999e-01, 9.82793723247329054082e-01, 1.57079632679489655800e+00 };
        static readonly double[] AtanLo = { 2.26987774529616870924e-17, 3.06161699786838301793e-17, 1.39033110312309984516e-17, 6.12323399573676603587e-17 };
        const double AT0 = 3.33333333333329318027e-01, AT1 = -1.99999999998764832476e-01, AT2 = 1.42857142725034663711e-01,
                     AT3 = -1.11111104054623557880e-01, AT4 = 9.09088713343650656196e-02, AT5 = -7.69187620504482999495e-02,
                     AT6 = 6.66107313738753120669e-02, AT7 = -5.83357013379057348645e-02, AT8 = 4.97687799461593236017e-02,
                     AT9 = -3.65315727442169155270e-02, AT10 = 1.62858201153657823623e-02;

        public static double Atan(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            bool neg = x < 0;
            double ax = neg ? -x : x;
            int id;
            if (ax > 2.4375)
            {
                if (double.IsPositiveInfinity(ax)) return neg ? -HalfPi : HalfPi;
                id = 3; ax = -1.0 / ax;
            }
            else if (ax >= 0.4375)
            {
                if (ax < 1.1875)
                {
                    if (ax < 0.6875) { id = 0; ax = (2.0 * ax - 1.0) / (2.0 + ax); }
                    else { id = 1; ax = (ax - 1.0) / (ax + 1.0); }
                }
                else
                {
                    id = 2; ax = (ax - 1.5) / (1.0 + 1.5 * ax);
                }
            }
            else
            {
                if (ax < 1e-9) return x;
                id = -1;
            }
            double z = ax * ax;
            double w = z * z;
            double s1 = z * (AT0 + w * (AT2 + w * (AT4 + w * (AT6 + w * (AT8 + w * AT10)))));
            double s2 = w * (AT1 + w * (AT3 + w * (AT5 + w * (AT7 + w * AT9))));
            double res;
            if (id < 0) res = ax - ax * (s1 + s2);
            else res = AtanHi[id] - ((ax * (s1 + s2) - AtanLo[id]) - ax);
            return neg ? -res : res;
        }

        public static double Atan2(double y, double x)
        {
            if (double.IsNaN(x) || double.IsNaN(y)) return double.NaN;
            if (x == 0)
            {
                if (y > 0) return HalfPi;
                if (y < 0) return -HalfPi;
                return 0.0;
            }
            double a = Atan(y / x);
            if (x > 0) return a;
            return y >= 0 ? a + PI : a - PI;
        }

        public static double Asin(double x)
        {
            if (x > 1 || x < -1) return double.NaN;
            return Atan2(x, Sqrt((1.0 - x) * (1.0 + x)));
        }

        public static double Acos(double x)
        {
            if (x > 1 || x < -1) return double.NaN;
            return Atan2(Sqrt((1.0 - x) * (1.0 + x)), x);
        }

        public static double Tanh(double x)
        {
            if (x > 20) return 1.0;
            if (x < -20) return -1.0;
            double e2 = Exp(2.0 * x);
            return (e2 - 1.0) / (e2 + 1.0);
        }

        public static double Sigmoid(double x) => 1.0 / (1.0 + Exp(-x));

        /// <summary>Wraps an angle into [-pi, pi).</summary>
        public static double WrapPi(double a)
        {
            double t = a + PI;
            t -= TwoPi * Math.Floor(t / TwoPi);
            return t - PI;
        }
    }
}
