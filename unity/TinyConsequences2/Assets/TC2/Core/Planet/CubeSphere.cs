using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Equiangular cubed-sphere grid (D2-007): 6 faces x N x N cells; cell index = f*N*N + j*N + i.
    /// The planet's axis is +Y. Neighbours across face seams are found geometrically, so there are no
    /// hand-written seam tables to get wrong. Areas are exact solid angles (they sum to 4 pi).
    /// </summary>
    public sealed class CubeSphere
    {
        public readonly int N;
        public readonly int CellCount;
        public readonly Vec3d[] Center;
        public readonly double[] Area;      // steradians
        public readonly int[] Nbr4;         // [cell*4 + k], k: 0 +i, 1 +j, 2 -i, 3 -j
        public readonly int[] Nbr8;         // [cell*8 + k], k 0..3 as Nbr4 then the 4 diagonals
        public readonly double MeanSpacing; // mean angular distance between edge neighbours (radians)

        static readonly Vec3d[] FaceN = { new Vec3d(1, 0, 0), new Vec3d(-1, 0, 0), new Vec3d(0, 1, 0), new Vec3d(0, -1, 0), new Vec3d(0, 0, 1), new Vec3d(0, 0, -1) };
        static readonly Vec3d[] FaceU = { new Vec3d(0, 0, -1), new Vec3d(0, 0, 1), new Vec3d(1, 0, 0), new Vec3d(1, 0, 0), new Vec3d(1, 0, 0), new Vec3d(-1, 0, 0) };
        static readonly Vec3d[] FaceV = { new Vec3d(0, 1, 0), new Vec3d(0, 1, 0), new Vec3d(0, 0, -1), new Vec3d(0, 0, 1), new Vec3d(0, 1, 0), new Vec3d(0, 1, 0) };

        const double QuarterPi = 7.85398163397448278999e-01;

        public CubeSphere(int n)
        {
            N = n;
            CellCount = 6 * n * n;
            Center = new Vec3d[CellCount];
            Area = new double[CellCount];
            Nbr4 = new int[CellCount * 4];
            Nbr8 = new int[CellCount * 8];
            double step = DMath.HalfPi / n;
            for (int f = 0; f < 6; f++)
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int c = f * n * n + j * n + i;
                        double a = -QuarterPi + (i + 0.5) * step;
                        double b = -QuarterPi + (j + 0.5) * step;
                        Center[c] = FacePoint(f, a, b);
                        double x0 = Tan(-QuarterPi + i * step), x1 = Tan(-QuarterPi + (i + 1) * step);
                        double y0 = Tan(-QuarterPi + j * step), y1 = Tan(-QuarterPi + (j + 1) * step);
                        Area[c] = A(x1, y1) - A(x0, y1) - A(x1, y0) + A(x0, y0);
                    }
            double sum = 0; int cnt = 0;
            for (int f = 0; f < 6; f++)
                for (int j = 0; j < n; j++)
                    for (int i = 0; i < n; i++)
                    {
                        int c = f * n * n + j * n + i;
                        double a = -QuarterPi + (i + 0.5) * step;
                        double b = -QuarterPi + (j + 0.5) * step;
                        Nbr4[c * 4 + 0] = CellOf(FacePoint(f, a + step, b));
                        Nbr4[c * 4 + 1] = CellOf(FacePoint(f, a, b + step));
                        Nbr4[c * 4 + 2] = CellOf(FacePoint(f, a - step, b));
                        Nbr4[c * 4 + 3] = CellOf(FacePoint(f, a, b - step));
                        for (int k = 0; k < 4; k++) Nbr8[c * 8 + k] = Nbr4[c * 4 + k];
                        Nbr8[c * 8 + 4] = CellOf(FacePoint(f, a + step, b + step));
                        Nbr8[c * 8 + 5] = CellOf(FacePoint(f, a - step, b + step));
                        Nbr8[c * 8 + 6] = CellOf(FacePoint(f, a - step, b - step));
                        Nbr8[c * 8 + 7] = CellOf(FacePoint(f, a + step, b - step));
                        for (int k = 0; k < 4; k++) { sum += Vec3d.Angle(Center[c], Center[Nbr4[c * 4 + k]]); cnt++; }
                    }
            MeanSpacing = sum / cnt;
        }

        static double Tan(double x) => DMath.Tan(x);
        static double A(double x, double y) => DMath.Atan(x * y / DMath.Sqrt(1.0 + x * x + y * y));

        /// <summary>Point on the unit sphere for face f and equiangular coordinates (a, b); a or b beyond
        /// +-pi/4 land on neighbouring faces after normalisation.</summary>
        public static Vec3d FacePoint(int f, double a, double b)
        {
            double x = DMath.Tan(a), y = DMath.Tan(b);
            Vec3d p = FaceN[f] + FaceU[f] * x + FaceV[f] * y;
            return p.Normalized;
        }

        public static int FaceOf(Vec3d p)
        {
            double ax = DMath.Abs(p.X), ay = DMath.Abs(p.Y), az = DMath.Abs(p.Z);
            if (ax >= ay && ax >= az) return p.X >= 0 ? 0 : 1;
            if (ay >= ax && ay >= az) return p.Y >= 0 ? 2 : 3;
            return p.Z >= 0 ? 4 : 5;
        }

        /// <summary>Face and equiangular coordinates of a direction.</summary>
        public static void ToFace(Vec3d p, out int f, out double a, out double b)
        {
            f = FaceOf(p);
            double pn = Vec3d.Dot(p, FaceN[f]);
            a = DMath.Atan(Vec3d.Dot(p, FaceU[f]) / pn);
            b = DMath.Atan(Vec3d.Dot(p, FaceV[f]) / pn);
        }

        /// <summary>The cell containing direction p (need not be normalised).</summary>
        public int CellOf(Vec3d p)
        {
            ToFace(p, out int f, out double a, out double b);
            double step = DMath.HalfPi / N;
            int i = DMath.FloorToInt((a + QuarterPi) / step);
            int j = DMath.FloorToInt((b + QuarterPi) / step);
            if (i < 0) i = 0; else if (i >= N) i = N - 1;
            if (j < 0) j = 0; else if (j >= N) j = N - 1;
            return f * N * N + j * N + i;
        }

        public void Decompose(int cell, out int f, out int i, out int j)
        {
            f = cell / (N * N);
            int r = cell - f * N * N;
            j = r / N;
            i = r - j * N;
        }

        /// <summary>Cell of this grid that contains the centre of a cell of another grid.</summary>
        public int CellOfOther(CubeSphere other, int otherCell) => CellOf(other.Center[otherCell]);

        /// <summary>Face basis vectors (for building render meshes).</summary>
        public static void FaceBasis(int f, out Vec3d n, out Vec3d u, out Vec3d v) { n = FaceN[f]; u = FaceU[f]; v = FaceV[f]; }
    }
}
