using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Where water goes (05 §3b): priority-flood filling of depressions (Barnes et al. 2014) gives every land cell a
    /// receiver on a path to the sea; the visiting order is the stack (outlets first, upstream after), so drainage
    /// area and discharge accumulate in one reverse pass. Filled minus bed elevation is the depth of a lake basin.
    /// Derived from relief and sea level: rebuilt (and versioned) when they change; never serialized.
    /// </summary>
    public sealed class Drainage
    {
        public readonly int Cells;
        public readonly int[] Receiver;     // downstream cell, -1 for sea cells
        public readonly int[] Stack;        // land cells in visiting order (outlets first)
        public int StackCount;
        public readonly float[] Filled;     // water surface of a filled basin (>= bed elevation)
        public readonly float[] AreaHa;     // drainage area upstream of (and including) each cell
        public readonly float[] Discharge;  // water passing through each cell in the last day (m3)
        public int Version;
        public double BuiltSeaLevel;

        // scratch for the flood
        readonly double[] _heapKey;
        readonly int[] _heapCell;
        readonly bool[] _done;

        public Drainage(int cells)
        {
            Cells = cells;
            Receiver = new int[cells]; Stack = new int[cells]; Filled = new float[cells]; AreaHa = new float[cells]; Discharge = new float[cells];
            _heapKey = new double[cells * 2]; _heapCell = new int[cells * 2]; _done = new bool[cells];
        }

        public bool IsLake(PlanetState P, int c) => P.IsLand(c) && Filled[c] - P.Elev[c] > 2.0f;

        /// <summary>Recomputes receivers, the stack, filled levels and drainage areas from the relief.</summary>
        public void Rebuild(PlanetState P)
        {
            var g = P.Grid;
            int n = Cells;
            int heapN = 0;
            StackCount = 0;
            for (int c = 0; c < n; c++)
            {
                _done[c] = false;
                Receiver[c] = -1;
                AreaHa[c] = 0;
                Filled[c] = P.Elev[c];
            }
            // every sea cell next to land is an outlet at sea level
            double sea = P.SeaLevel;
            for (int c = 0; c < n; c++)
            {
                if (P.IsLand(c)) continue;
                _done[c] = true;
                Filled[c] = (float)sea;
                bool shore = false;
                for (int k = 0; k < 8; k++) if (P.IsLand(g.Nbr8[c * 8 + k])) { shore = true; break; }
                if (shore) Push(ref heapN, sea, c);
            }
            while (heapN > 0)
            {
                Pop(ref heapN, out double level, out int c);
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[c * 8 + k];
                    if (_done[nb]) continue;
                    _done[nb] = true;
                    double f = Math.Max(P.Elev[nb], level + 0.01);
                    Filled[nb] = (float)f;
                    Receiver[nb] = c; // drains toward the cell it was reached from: downhill to the sea
                    Stack[StackCount++] = nb;
                    Push(ref heapN, f, nb);
                }
            }
            // drainage area: every land cell drains its own area plus all upstream, in reverse stack order
            for (int i = 0; i < StackCount; i++) AreaHa[Stack[i]] = (float)P.AreaHa(Stack[i]);
            for (int i = StackCount - 1; i >= 0; i--)
            {
                int c = Stack[i];
                int r = Receiver[c];
                if (r >= 0 && P.IsLand(r)) AreaHa[r] += AreaHa[c];
            }
            BuiltSeaLevel = sea;
            Version++;
        }

        /// <summary>
        /// Routes the day's runoff (m of water over each land cell) down the tree: discharge in m3 per day.
        /// </summary>
        public void Accumulate(PlanetState P, float[] runoffM)
        {
            for (int c = 0; c < Cells; c++) Discharge[c] = 0;
            for (int i = 0; i < StackCount; i++)
            {
                int c = Stack[i];
                Discharge[c] = (float)(runoffM[c] * P.AreaHa(c) * 10000.0);
            }
            for (int i = StackCount - 1; i >= 0; i--)
            {
                int c = Stack[i];
                int r = Receiver[c];
                if (r >= 0 && P.IsLand(r)) Discharge[r] += Discharge[c];
            }
        }

        // binary heap keyed by (level, cell) - deterministic ties
        static bool Less(double ka, int ca, double kb, int cb) => ka < kb || (ka == kb && ca < cb);

        void Push(ref int n, double key, int cell)
        {
            int i = n++;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (!Less(key, cell, _heapKey[p], _heapCell[p])) break;
                _heapKey[i] = _heapKey[p]; _heapCell[i] = _heapCell[p];
                i = p;
            }
            _heapKey[i] = key; _heapCell[i] = cell;
        }

        void Pop(ref int n, out double key, out int cell)
        {
            key = _heapKey[0]; cell = _heapCell[0];
            n--;
            if (n == 0) return;
            double lk = _heapKey[n]; int lc = _heapCell[n];
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1;
                if (l >= n) break;
                int r = l + 1;
                int m = r < n && Less(_heapKey[r], _heapCell[r], _heapKey[l], _heapCell[l]) ? r : l;
                if (!Less(_heapKey[m], _heapCell[m], lk, lc)) break;
                _heapKey[i] = _heapKey[m]; _heapCell[i] = _heapCell[m];
                i = m;
            }
            _heapKey[i] = lk; _heapCell[i] = lc;
        }
    }
}
