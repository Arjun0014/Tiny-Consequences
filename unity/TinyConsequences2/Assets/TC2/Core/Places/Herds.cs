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
        readonly Dictionary<long, double> _claimed = new Dictionary<long, double>();   // (lookups only: species x cell -> counted)

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
            _claimed.Clear();
            // this village's herds, in id order
            _mine.Clear(); _done.Clear();
            for (int h = w.Herds.First(); h != 0; h = w.Herds.Next(h)) if (w.Herds.Get(h).Village == sid) _mine.Add(h);
            for (int i = 0; i < _mine.Count; i++)
            {
                int hid = _mine[i];
                ref var hd = ref w.Herds.Get(hid);
                int q = IndexOfSpecies(P, hd.Species);
                if (q < 0) { w.Herds.Remove(hid); continue; }   // the kind died out
                // it moves toward where its kind is thickest round it, within the village's reach
                int best = hd.Cell; double bv = P.IsLand(hd.Cell) ? Around(q, hd.Cell) : -1;
                for (int k = 0; k < 8; k++)
                {
                    int nb = g.Nbr8[hd.Cell * 8 + k];
                    if (!P.IsLand(nb) || !_cells.Contains(nb)) continue;
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
                    add = DMath.Min(add, max - o.Count);
                    if (add > 0) { o.Count = (float)(o.Count + DMath.Floor(add)); Claim(q, o.Cell, DMath.Floor(add)); }
                    if (o.Count > o.Peak) o.Peak = o.Count;
                    if (hd.Hunted > o.Hunted) o.Hunted = hd.Hunted;
                    w.Herds.Remove(hid);
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
                    w.Herds.Remove(hid);
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
                    Claim(q, c, count);
                }
            }
        }

        double Claimed(int q, int c)
        {
            var g = _w.Planet.Grid;
            double v = 0;
            if (_claimed.TryGetValue(((long)q << 32) | (uint)c, out double a)) v += a;
            for (int k = 0; k < 8; k++) if (_claimed.TryGetValue(((long)q << 32) | (uint)g.Nbr8[c * 8 + k], out double b)) v += b;
            return v;
        }

        void Claim(int q, int c, double count)
        {
            long key = ((long)q << 32) | (uint)c;
            _claimed[key] = (_claimed.TryGetValue(key, out double a) ? a : 0) + count;
        }

        /// <summary>A village's herds go back to being the land's (the village is gone).</summary>
        public void Dissolve(int sid)
        {
            var w = _w;
            _mine.Clear();
            for (int h = w.Herds.First(); h != 0; h = w.Herds.Next(h)) if (w.Herds.Get(h).Village == sid) _mine.Add(h);
            for (int i = 0; i < _mine.Count; i++) w.Herds.Remove(_mine[i]);
        }

        /// <summary>People killed from the ground round a cell: the herds there were hunted today.</summary>
        public void MarkHunted(int ground)
        {
            var w = _w; var g = w.Planet.Grid;
            for (int h = w.Herds.First(); h != 0; h = w.Herds.Next(h))
            {
                ref var hd = ref w.Herds.Get(h);
                if (hd.Cell == ground) { hd.Hunted = w.Now; continue; }
                for (int k = 0; k < 8; k++) if (g.Nbr8[ground * 8 + k] == hd.Cell) { hd.Hunted = w.Now; break; }
            }
        }

        /// <summary>The largest herd of game near a village (a hunter's goal), or 0.</summary>
        public int BestGame(int sid)
        {
            var w = _w;
            int best = 0; float bc = 0;
            for (int h = w.Herds.First(); h != 0; h = w.Herds.Next(h))
            {
                ref var hd = ref w.Herds.Get(h);
                if (hd.Village != sid || hd.Kind == 5) continue;   // (people hunt game, not wolves)
                if (hd.Count > bc) { bc = hd.Count; best = h; }
            }
            return best;
        }
    }
}
