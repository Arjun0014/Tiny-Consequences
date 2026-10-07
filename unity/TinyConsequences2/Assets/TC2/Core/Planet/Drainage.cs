using System;
using TC2.Core.Foundation;

namespace TC2.Core.Planet
{
    /// <summary>
    /// Where water goes (05 §3b): priority-flood filling of depressions (Barnes et al. 2014) gives every land cell a
    /// receiver on a path to the sea; the visiting order is the stack (outlets first, upstream after), so drainage
    /// area and discharge accumulate in one reverse pass. Filled cells form depressions (lake basins), grouped by the
    /// cell they spill into: each holds the water it has gathered (<see cref="PlanetState.LakeWater"/>, kept at its
    /// deepest cell), loses it to evaporation from its wet area and passes on only what overflows (fill and spill).
    /// The tree is derived from relief and sea level: rebuilt (and versioned) when they change; never serialized.
    /// </summary>
    public sealed class Drainage
    {
        public const float FilledMin = 0.5f;   // metres above the bed that make a cell part of a depression
        public const float WetDepth = 0.3f;    // a lake shows where its water is deeper than this

        public readonly int Cells;
        public readonly int[] Receiver;     // downstream cell, -1 for sea cells
        public readonly int[] Stack;        // land cells in visiting order (outlets first)
        public int StackCount;
        public readonly float[] Filled;     // water surface of a filled basin (>= bed elevation)
        public readonly float[] AreaHa;     // drainage area upstream of (and including) each cell
        public readonly float[] Discharge;  // water passing through each cell in the last day (m3)
        public int Version;
        public double BuiltSeaLevel;
        readonly bool[] _builtLand;          // the land mask the tree was built for
        public bool BuiltLand(int c) => _builtLand[c];

        // depressions
        public readonly int[] BasinOf;      // basin of each cell, -1 when not in a depression
        public int BasinCount;
        public int[] BasinStart = new int[0], BasinLen = new int[0];  // ranges of BasinCells
        public int[] BasinCells;            // each basin's cells, deepest bed first
        public int[] BasinExit = new int[0];          // the cell a full basin spills into
        public double[] BasinSpill = new double[0];   // its water level when full
        public double[] BasinCapacity = new double[0];// m3 when full
        public double[] BasinInflow = new double[0];  // m3 entering today (scratch)
        public double[] BasinLevel = new double[0];   // water level now (derived from the stored volume)
        public double[] BasinWetHa = new double[0];   // wet area now (derived)
        readonly int[] _exitBasin;          // the basin spilling into each cell, -1 none

        // scratch for the flood
        readonly double[] _heapKey;
        readonly int[] _heapCell;
        readonly bool[] _done;

        public Drainage(int cells)
        {
            Cells = cells;
            Receiver = new int[cells]; Stack = new int[cells]; Filled = new float[cells]; AreaHa = new float[cells]; Discharge = new float[cells];
            BasinOf = new int[cells]; BasinCells = new int[cells]; _exitBasin = new int[cells]; _builtLand = new bool[cells];
            _heapKey = new double[cells * 2]; _heapCell = new int[cells * 2]; _done = new bool[cells];
        }

        public int DeepestCell(int b) => BasinCells[BasinStart[b]];

        /// <summary>Recomputes receivers, the stack, filled levels, drainage areas and the depressions.</summary>
        public void Rebuild(PlanetState P, bool fromSaved = false)
        {
            if (!fromSaved) Array.Copy(P.Ice, P.DrainIce, Cells); // a loaded world rebuilds with the ice it was built with
            var g = P.Grid;
            int n = Cells;
            int heapN = 0;
            StackCount = 0;
            for (int c = 0; c < n; c++)
            {
                _done[c] = false;
                Receiver[c] = -1;
                AreaHa[c] = 0;
                Filled[c] = (float)P.DrainSurface(c);
                BasinOf[c] = -1;
                _exitBasin[c] = -1;
                _builtLand[c] = P.IsLand(c);
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
                    double f = Math.Max(P.DrainSurface(nb), level + 0.01);
                    Filled[nb] = (float)f;
                    Receiver[nb] = c; // drains toward the cell it was reached from: downhill to the sea
                    Stack[StackCount++] = nb;
                    Push(ref heapN, f, nb);
                }
            }
            // drainage area: every land cell drains its own area plus all upstream, in reverse stack order
            for (int i = 0; i < StackCount; i++) AreaHa[Stack[i]] = (float)P.GeoAreaHa(Stack[i]);
            for (int i = StackCount - 1; i >= 0; i--)
            {
                int c = Stack[i];
                int r = Receiver[c];
                if (r >= 0 && P.IsLand(r)) AreaHa[r] = (float)((double)AreaHa[r] + AreaHa[c]);
            }
            BuildBasins(P);
            BuiltSeaLevel = sea;
            Version++;
        }

        void BuildBasins(PlanetState P)
        {
            // receivers precede their tributaries in the stack, so one pass labels every depression by its exit
            int nb = 0;
            for (int i = 0; i < StackCount; i++)
            {
                int c = Stack[i];
                if (Filled[c] - P.DrainSurface(c) <= FilledMin) continue;
                int r = Receiver[c];
                if (r >= 0 && BasinOf[r] >= 0) { BasinOf[c] = BasinOf[r]; continue; }
                if (_exitBasin[r] < 0) _exitBasin[r] = nb++;
                BasinOf[c] = _exitBasin[r];
            }
            BasinCount = nb;
            if (BasinStart.Length < nb)
            {
                int cap = Math.Max(nb, BasinStart.Length * 2);
                BasinStart = new int[cap]; BasinLen = new int[cap]; BasinExit = new int[cap]; BasinSpill = new double[cap];
                BasinCapacity = new double[cap]; BasinInflow = new double[cap]; BasinLevel = new double[cap]; BasinWetHa = new double[cap];
            }
            for (int b = 0; b < nb; b++) { BasinLen[b] = 0; BasinSpill[b] = double.MaxValue; }
            for (int c = 0; c < Cells; c++) if (BasinOf[c] >= 0) BasinLen[BasinOf[c]]++;
            int at = 0;
            for (int b = 0; b < nb; b++) { BasinStart[b] = at; at += BasinLen[b]; BasinLen[b] = 0; }
            for (int c = 0; c < Cells; c++)
            {
                int b = BasinOf[c];
                if (b < 0) continue;
                BasinCells[BasinStart[b] + BasinLen[b]++] = c;
                if (Filled[c] < BasinSpill[b]) BasinSpill[b] = Filled[c];
            }
            for (int c = 0; c < Cells; c++) if (_exitBasin[c] >= 0) BasinExit[_exitBasin[c]] = c;
            for (int b = 0; b < nb; b++)
            {
                // deepest bed first (a total order: elevation, then cell)
                int s = BasinStart[b], len = BasinLen[b];
                for (int i = s + 1; i < s + len; i++)
                {
                    int x = BasinCells[i];
                    int j = i - 1;
                    while (j >= s && Deeper(P, x, BasinCells[j])) { BasinCells[j + 1] = BasinCells[j]; j--; }
                    BasinCells[j + 1] = x;
                }
                BasinCapacity[b] = VolumeBelow(P, b, BasinSpill[b], out _);
            }
        }

        static bool Deeper(PlanetState P, int a, int b) => P.DrainSurface(a) < P.DrainSurface(b) || (P.DrainSurface(a) == P.DrainSurface(b) && a < b);

        /// <summary>Water held in basin b when its surface stands at <paramref name="level"/>, and the wet area.</summary>
        public double VolumeBelow(PlanetState P, int b, double level, out double wetHa)
        {
            double v = 0; wetHa = 0;
            int s = BasinStart[b], len = BasinLen[b];
            for (int i = s; i < s + len; i++)
            {
                int c = BasinCells[i];
                double depth = level - P.DrainSurface(c);
                if (depth <= 0) break;
                double ha = P.GeoAreaHa(c);
                v += depth * ha * 10000.0;
                wetHa += ha;
            }
            return v;
        }

        /// <summary>The surface level and wet area of basin b holding volume v (m3): fills the deepest cells first.</summary>
        public double LevelFor(PlanetState P, int b, double v, out double wetHa)
        {
            int s = BasinStart[b], len = BasinLen[b];
            wetHa = 0;
            double spill = BasinSpill[b];
            if (len == 0) return spill;
            double level = P.DrainSurface(BasinCells[s]);
            if (v <= 0) return level;
            double areaM2 = 0, held = 0;
            for (int i = s; i < s + len; i++)
            {
                int c = BasinCells[i];
                areaM2 += P.GeoAreaHa(c) * 10000.0;
                double next = i + 1 < s + len ? Math.Min(P.DrainSurface(BasinCells[i + 1]), spill) : spill;
                if (next < level) next = level;
                double room = areaM2 * (next - level);
                if (held + room >= v)
                {
                    wetHa = areaM2 / 10000.0;
                    return level + (v - held) / areaM2;
                }
                held += room;
                level = next;
                if (level >= spill) break;
            }
            wetHa = areaM2 / 10000.0;
            return spill;
        }

        /// <summary>Pure routing of the day's runoff (m of water over each land cell) down the tree: m3 per day.</summary>
        public void Accumulate(PlanetState P, float[] runoffM)
        {
            for (int c = 0; c < Cells; c++) Discharge[c] = 0;
            for (int i = 0; i < StackCount; i++)
            {
                int c = Stack[i];
                Discharge[c] = (float)(runoffM[c] * P.GeoAreaHa(c) * 10000.0);
            }
            for (int i = StackCount - 1; i >= 0; i--)
            {
                int c = Stack[i];
                int r = Receiver[c];
                if (r >= 0 && P.IsLand(r)) Discharge[r] = (float)((double)Discharge[r] + Discharge[c]);
            }
        }

        /// <summary>
        /// The day's water: runoff routed down the tree; depressions gather what reaches them, lose evaporation from
        /// their wet area and spill the rest. Writes <see cref="Discharge"/>, the stored volumes and lake depths.
        /// Returns the volume evaporated from lakes (m3), for the water balance.
        /// </summary>
        public double Route(PlanetState P, float[] runoffM)
        {
            for (int c = 0; c < Cells; c++) Discharge[c] = 0;
            for (int i = 0; i < StackCount; i++)
            {
                int c = Stack[i];
                Discharge[c] = (float)(runoffM[c] * P.GeoAreaHa(c) * 10000.0);
            }
            for (int b = 0; b < BasinCount; b++) BasinInflow[b] = 0;
            double evaporated = 0;
            for (int i = StackCount - 1; i >= 0; i--)
            {
                int c = Stack[i];
                int eb = _exitBasin[c];
                if (eb >= 0) Discharge[c] = (float)(Discharge[c] + Settle(P, eb, ref evaporated)); // all of that basin's inflow is in
                int bc = BasinOf[c];
                if (bc >= 0) { BasinInflow[bc] += Discharge[c]; continue; } // what enters a depression is in it
                int r = Receiver[c];
                if (r >= 0 && P.IsLand(r)) Discharge[r] = (float)((double)Discharge[r] + Discharge[c]);
            }
            // basins spilling straight into the sea (none with a land exit is left unsettled)
            for (int b = 0; b < BasinCount; b++) if (!P.IsLand(BasinExit[b])) Settle(P, b, ref evaporated);
            return evaporated;
        }

        /// <summary>Open-water evaporation (m a day) at a temperature: ~4 mm on a warm day, little under ice.</summary>
        public static double LakeEvaporation(double t) => t > 0 ? 0.0006 + 0.00016 * t : 0.0002;

        double Settle(PlanetState P, int b, ref double evaporated)
        {
            int deep = DeepestCell(b);
            double v = P.LakeWater[deep] + BasinInflow[b];
            BasinInflow[b] = 0;
            double e = LakeEvaporation(P.Temp[deep]) * BasinWetHa[b] * 10000.0;
            if (e > v) e = v;
            v -= e;
            evaporated += e;
            double over = 0;
            if (v > BasinCapacity[b]) { over = v - BasinCapacity[b]; v = BasinCapacity[b]; }
            P.LakeWater[deep] = (float)v;
            Derive(P, b, P.LakeWater[deep]); // from the stored value, exactly as a loaded world derives it
            return over;
        }

        void Derive(PlanetState P, int b, double v)
        {
            double level = LevelFor(P, b, v, out double wet);
            BasinLevel[b] = level;
            BasinWetHa[b] = wet;
            int s = BasinStart[b], len = BasinLen[b];
            for (int i = s; i < s + len; i++)
            {
                int c = BasinCells[i];
                double depth = level - P.DrainSurface(c);
                P.LakeDepth[c] = depth > 0 && v > 0 ? (float)depth : 0f;
            }
        }

        /// <summary>
        /// After a rebuild (or a load): water stored at a cell that is no longer a basin's deepest cell moves to the
        /// deepest cell of the basin it is now in (or drains away), volumes are clamped to capacity, depths derived.
        /// </summary>
        public void SettleStored(PlanetState P)
        {
            for (int c = 0; c < Cells; c++) P.LakeDepth[c] = 0f;
            for (int c = 0; c < Cells; c++)
            {
                float v = P.LakeWater[c];
                if (v == 0f) continue;
                int b = BasinOf[c];
                if (b >= 0 && DeepestCell(b) == c) continue;
                P.LakeWater[c] = 0f;
                if (b >= 0) { int deep = DeepestCell(b); P.LakeWater[deep] = (float)((double)P.LakeWater[deep] + v); }
            }
            for (int b = 0; b < BasinCount; b++)
            {
                int deep = DeepestCell(b);
                if (P.LakeWater[deep] > BasinCapacity[b]) P.LakeWater[deep] = (float)BasinCapacity[b];
                Derive(P, b, P.LakeWater[deep]);
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
