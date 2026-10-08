using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Planet;
using UnityEngine;

namespace TC2.Game
{
    /// <summary>
    /// The rivers as drawn (D2-084): the core's river line (`TC2.Core.Planet.RiverLines`, D2-118: one line serves the simulation - homes keep off it -
    /// and the view) shared by the water's ribbons (<see cref="WaterView"/>), the corridor the ground draws along them - wet banks, the riparian
    /// strip, reeds - and the woods that keep to the water (<see cref="TerrainLod"/>). Its width comes from the river's flow (the simulation's
    /// RiverFlow); its rank among the planet's rivers says how far off it should still read. Presentation only: rebuilt when the line changes;
    /// the widths and ranks follow the flows every few seconds.
    /// </summary>
    public static class Rivers
    {
        /// <summary>One piece of a reach: its ends (unit directions), its flow (m3/day), half its drawn width near the camera (m) and its
        /// rank among the planet's rivers by flow (0-1: the largest 1).</summary>
        public struct Piece { public Vec3d A, B; public float Flow, Half, Rank; public int Cell; }

        public static readonly List<Piece> All = new List<Piece>();
        static readonly Dictionary<int, List<int>> _byCell = new Dictionary<int, List<int>>();
        static int _lines = -1, _world = -1;
        static float _widthsAt = -999f;
        public static int Version;

        /// <summary>Half the drawn width (m) of a river carrying this flow (m3/day) near the camera.</summary>
        public static float HalfWidth(float flow) => (float)RiverLines.HalfWidth(flow);

        /// <summary>Rebuilds the shape when the river line or the world changed, and the widths when the flows may have (every few seconds).</summary>
        public static void Refresh(TC2.Core.Simulation.World w, int worldVersion)
        {
            var P = w.Planet;
            if (w.Drainage == null) return;
            RiverLines.Ensure(P, w.Drainage, w.Seed);
            if (_lines == RiverLines.Version && _world == worldVersion)
            {
                if (Time.realtimeSinceStartup - _widthsAt < 4f) return;
                Widths(P);
                return;
            }
            _lines = RiverLines.Version; _world = worldVersion;
            All.Clear(); _byCell.Clear();
            var src = RiverLines.All;
            for (int i = 0; i < src.Count; i++)
            {
                All.Add(new Piece { A = src[i].A, B = src[i].B, Cell = src[i].Cell });
                Index(P.Grid.CellOf(src[i].A), i); Index(P.Grid.CellOf(src[i].B), i); Index(P.Grid.CellOf(Vec3d.Slerp(src[i].A, src[i].B, 0.5)), i);
            }
            Widths(P);
        }

        static void Index(int cell, int piece)
        {
            if (!_byCell.TryGetValue(cell, out var list)) { list = new List<int>(4); _byCell[cell] = list; }
            if (list.Count == 0 || list[list.Count - 1] != piece) list.Add(piece);
        }

        static readonly List<float> _flows = new List<float>();

        static void Widths(PlanetState P)
        {
            _widthsAt = Time.realtimeSinceStartup;
            _flows.Clear();
            for (int i = 0; i < All.Count; i++) if (i == 0 || All[i].Cell != All[i - 1].Cell) _flows.Add(P.RiverFlow[All[i].Cell]);   // (one a reach)
            _flows.Sort();
            for (int i = 0; i < All.Count; i++)
            {
                var p = All[i];
                p.Flow = P.RiverFlow[p.Cell];
                p.Half = HalfWidth(p.Flow);
                int at = _flows.BinarySearch(p.Flow); if (at < 0) at = ~at;
                p.Rank = _flows.Count > 1 ? at / (float)(_flows.Count - 1) : 1f;
                All[i] = p;
            }
            Version++;
        }

        /// <summary>
        /// The nearest river's centre line to a point of the ground (m along the surface, approximately), and half its drawn width
        /// there; float.MaxValue where no river runs within a cell or so.
        /// </summary>
        public static float Nearest(PlanetState P, Vec3d dir, int cell, out float half)
        {
            half = 0f;
            float best = float.MaxValue;
            if (All.Count == 0) return best;
            var g = P.Grid;
            for (int k = -1; k < 8; k++)
            {
                int cc = k < 0 ? cell : g.Nbr8[cell * 8 + k];
                if (!_byCell.TryGetValue(cc, out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var p = All[list[i]];
                    float dd = (float)(RiverLines.SegDist(dir, p.A, p.B) * P.Radius);
                    if (dd < best) { best = dd; half = p.Half; }
                }
            }
            return best;
        }
    }
}
