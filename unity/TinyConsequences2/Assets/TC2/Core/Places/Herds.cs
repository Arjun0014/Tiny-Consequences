using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Planet;
using TC2.Core.Simulation;

namespace TC2.Core.Places
{
    /// <summary>
    /// A herd or pack near a village (D2-047): a group of one kind with an identity, a place and a size - the deer of the
    /// valley, the boar of the oak wood, the pack on the ridge. It is not a copy of the animals: the abundance field stays their
    /// only truth. Each day a herd is read again from the field round its cell (it shrinks as the field does, it grows only from
    /// animals no other herd of its kind there counts), and it moves toward where its kind is thickest within the village's
    /// reach. Hunters go where a herd is; what they kill comes out of the field there, and the herd is smaller the next day.
    /// </summary>
    public struct Herd
    {
        public int Id;
        public int Species;     // the species' entity id (kinds come and go: never a remembered index)
        public byte Kind;       // template kind (0 deer, 2 aurochs, 3 boar, 4 ibex, 5 wolf)
        public int Cell;        // where it is
        public int Village;     // the village whose reach it lives in
        public float Count;     // animals in it
        public float Peak;      // the most it has numbered (to know a herd hunted out from one that wandered off)
        public long Born, Hunted;   // when it was first known; when people last killed from it (0 never)

        public void Write(BinWriter w) { w.Int(Id); w.Int(Species); w.Byte(Kind); w.Int(Cell); w.Int(Village); w.Float(Count); w.Float(Peak); w.Long(Born); w.Long(Hunted); }
        public static Herd Read(BinReader r) => new Herd { Id = r.Int(), Species = r.Int(), Kind = r.Byte(), Cell = r.Int(), Village = r.Int(), Count = r.Float(), Peak = r.Float(), Born = r.Long(), Hunted = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Species); h.Add(Kind); h.Add(Cell); h.Add(Village); h.Add(Count); h.Add(Peak); h.Add(Born); h.Add(Hunted); }
    }

    public sealed class HerdSystem
    {
        /// <summary>The kinds that go in herds and packs near people, and how many make one (at least, at most).</summary>
        public static bool Herding(byte kind, out int min, out int max)
        {
            switch (kind)
            {
                case 0: min = 3; max = 14; return true;   // deer
                case 2: min = 3; max = 24; return true;   // aurochs
                case 3: min = 3; max = 12; return true;   // boar: a sounder
                case 4: min = 3; max = 12; return true;   // ibex
                case 5: min = 2; max = 9; return true;    // wolves: a pack
                default: min = 0; max = 0; return false;
            }
        }

        World _w;
        readonly List<int> _cells = new List<int>(), _mine = new List<int>(), _done = new List<int>();
        int[] _in, _at; int _inNow;   // membership of today's reach (stamps: no search through the list) and each cell's place in it
        double[] _claimed = new double[0];   // animals already counted by a herd: [species x place in the reach]

        public void Bind(World w) { _w = w; }

        /// <summary>The animals of a species in a cell and its eight neighbours (what a herd there could be).</summary>
        double Around(int q, int c)
        {
            var P = _w.Planet; int n = P.Cells; var g = P.Grid;
            double v = P.Abund[q * n + c];
            for (int k = 0; k < 8; k++) v += P.Abund[q * n + g.Nbr8[c * 8 + k]];
            return v;
        }

        static int IndexOfSpecies(PlanetState P, int id) { for (int q = 0; q < P.Species.Count; q++) if (P.Species[q].Id == id) return q; return -1; }

        /// <summary>
        /// A village's herds, read again from the fields (once a day, at its survey): each herd follows its kind, shrinks to what
        /// the field round it holds, grows from what no herd counts; herds hunted to nothing are told; new herds are recognised
        /// where the field holds enough of a herding kind that no herd counts.
        /// </summary>
        public void Update(int sid)
        {
            var w = _w; var P = w.Planet; int n = P.Cells; var g = P.Grid;
            ref var s = ref w.Settlements.Get(sid);
            if (!s.Inhabited) { Dissolve(sid); return; }
            w.Hazards.CellsWithin(w, s.Cell, WildFood.HuntReach, _cells);
            if (_in == null || _in.Length != n) { _in = new int[n]; _inNow = 0; }
            if (_at == null || _at.Length != n) _at = new int[n];
            _inNow++;
            for (int i = 0; i < _cells.Count; i++) { _in[_cells[i]] = _inNow; _at[_cells[i]] = i; }
            int places = _cells.Count, kinds = P.Species.Count;
            if (_claimed.Length < places * kinds) _claimed = new double[places * kinds * 2];
            System.Array.Clear(_claimed, 0, places * kinds);
            // this village's herds, in id order
            _mine.Clear(); _done.Clear();
            var mine = w.HerdsOf(sid);
            for (int i = 0; i < mine.Count; i++) _mine.Add(mine[i]);
            for (int i = 0; i < _mine.Count; i++)
            {
                int hid = _mine[i];
                ref var hd = ref w.Herds.Get(hid);
                int q = IndexOfSpecies(P, hd.Species);
                if (q < 0) { Remove(hid); continue; }   // the kind died out
                // it moves toward where its kind is thickest round it, within the village's reach
                int best = hd.Cell; double bv = P.IsLand(hd.Cell) ? Around(q, hd.Cell) : -1;
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[hd.Cell * 8 + k];
                    if (!P.IsLand(nb) || _in[nb] != _inNow) continue;
                    double v = Around(q, nb);
                    if (v > bv * 1.15) { bv = v; best = nb; }
                }
                hd.Cell = best;
                Herding(hd.Kind, out int min, out int max);
                // two herds of a kind that come together become one (the elder keeps its name)
                int join = 0;
                for (int j = 0; j < _done.Count && join == 0; j++)
                {
                    ref var o = ref w.Herds.Get(_done[j]);
                    if (o.Species != hd.Species) continue;
                    if (o.Cell == hd.Cell) join = _done[j];
                    else for (int k = 0; k < 8; k++) if (g.Nbr8[o.Cell * 8 + k] == hd.Cell) { join = _done[j]; break; }
                }
                if (join != 0)
                {
                    ref var o = ref w.Herds.Get(join);
                    double add = DMath.Min(hd.Count, DMath.Max(0.0, Around(q, o.Cell) - Claimed(q, o.Cell)));
                    add = DMath.Min(add, (double)max - o.Count);
                    if (add > 0) { o.Count = (float)(o.Count + DMath.Floor(add)); Claim(q, o.Cell, DMath.Floor(add)); }
                    if (o.Count > o.Peak) o.Peak = o.Count;
                    if (hd.Hunted > o.Hunted) o.Hunted = hd.Hunted;
                    Remove(hid);
                    continue;
                }
                double there = Around(q, hd.Cell) - Claimed(q, hd.Cell);
                double count = DMath.Min(DMath.Min(hd.Count + DMath.Max(0.0, there - hd.Count) * 0.5, there), max);
                if (count < 1.0)
                {
                    // gone: hunted out (people killed from it lately, it was a herd, and the land round it is emptied of its
                    // kind) or scattered into the land
                    if (hd.Hunted != 0 && w.Now - hd.Hunted < 10 * SimTime.Day && hd.Peak >= min && Around(q, hd.Cell) < 1.0)
                        w.Record(new HistoryEvent
                        {
                            Kind = HistoryKind.HerdHuntedOut, Time = w.Now, Cell = hd.Cell, Settlement = sid, Subject = new EntityRef(EntityKind.Process, hid),
                            Object = new EntityRef(EntityKind.Species, hd.Species), Magnitude = hd.Peak, RootKind = RootKind.People,
                        });
                    Remove(hid);
                    continue;
                }
                hd.Count = (float)DMath.Floor(count);
                if (hd.Count > hd.Peak) hd.Peak = hd.Count;
                Claim(q, hd.Cell, hd.Count);
                _done.Add(hid);
            }
            // new herds where a herding kind gathers and no herd counts it
            for (int i = 0; i < _cells.Count; i++)
            {
                int c = _cells[i];
                if (!P.IsLand(c)) continue;
                for (int q = 0; q < P.Species.Count; q++)
                {
                    var sp = P.Species[q];
                    if (sp.Gone != 0 || sp.Habitat != 0 || !Herding(sp.Kind, out int min, out int max)) continue;
                    if (P.Abund[q * n + c] < 0.5) continue;
                    double free = Around(q, c) - Claimed(q, c);
                    if (free < min) continue;
                    int id = w.Ids.Next(EntityKind.Process);
                    double count = DMath.Floor(DMath.Min(free, max));
                    w.Herds.Add(id, new Herd { Id = id, Species = sp.Id, Kind = sp.Kind, Cell = c, Village = sid, Count = (float)count, Peak = (float)count, Born = w.Now });
                    w.HerdsOf(sid).InsertSorted(id);
                    Claim(q, c, count);
                }
            }
        }

        /// <summary>What herds already count of a species round a cell (claims are only ever made in the reach).</summary>
        double Claimed(int q, int c)
        {
            var g = _w.Planet.Grid;
            int places = _cells.Count;
            double v = 0;
            if (_in[c] == _inNow) v += _claimed[q * places + _at[c]];
            for (int k = 0; k < 8; k++) { int nb = g.Nbr8[c * 8 + k]; if (_in[nb] == _inNow) v += _claimed[q * places + _at[nb]]; }
            return v;
        }

        void Claim(int q, int c, double count)
        {
            if (_in[c] == _inNow) _claimed[q * _cells.Count + _at[c]] += count;
        }

        void Remove(int hid)
        {
            _w.HerdsOf(_w.Herds.Get(hid).Village).Remove(hid);
            _w.Herds.Remove(hid);
        }

        /// <summary>A village's herds go back to being the land's (the village is gone).</summary>
        public void Dissolve(int sid)
        {
            var w = _w;
            var mine = w.HerdsOf(sid);
            for (int i = 0; i < mine.Count; i++) w.Herds.Remove(mine[i]);
            mine.Clear();
        }

        /// <summary>A village's people killed from the ground round a cell: its herds there were hunted today.</summary>
        public void MarkHunted(int sid, int ground)
        {
            var w = _w; var g = w.Planet.Grid;
            var mine = w.HerdsOf(sid);
            for (int i = 0; i < mine.Count; i++)
            {
                ref var hd = ref w.Herds.Get(mine[i]);
                if (hd.Cell == ground) { hd.Hunted = w.Now; continue; }
                for (int k = 0; k < 8; k++) if (g.Nbr8[ground * 8 + k] == hd.Cell) { hd.Hunted = w.Now; break; }
            }
        }

        /// <summary>The largest herd of game near a village (a hunter's goal), or 0.</summary>
        public int BestGame(int sid)
        {
            var w = _w;
            int best = 0; float bc = 0;
            var mine = w.HerdsOf(sid);
            for (int i = 0; i < mine.Count; i++)
            {
                ref var hd = ref w.Herds.Get(mine[i]);
                if (hd.Kind == 5) continue;   // (people hunt game, not wolves)
                if (hd.Count > bc) { bc = hd.Count; best = mine[i]; }
            }
            return best;
        }
    }
}
