using System;
using System.Collections.Generic;
using TC2.Core.Foundation;
using TC2.Core.Simulation;

namespace TC2.Core.Society
{
    /// <summary>What a deed was (D2-069; 10 §1).</summary>
    public static class DeedKind
    {
        public const byte Take = 1;         // took a load from a village's store in the night, unseen (a harm)
        public const byte TriedToTake = 2;  // was seen taking and ran, leaving it (a harm: the fright and the intent)
        public const byte Relief = 3;       // gave grain to a village in famine (a help)
        public static bool Harm(byte k) => k == Take || k == TriedToTake;
    }

    /// <summary>How a village knows a deed.</summary>
    public static class KnownHow
    {
        public const byte Own = 1;     // its own people did it
        public const byte Saw = 2;     // one of its people saw it: the doer known by name and village
        public const byte Found = 3;   // it found the loss: the doer unknown
        public const byte Told = 4;    // someone told it (reserved: news comes with the answers to deeds, F6 slice 3)
    }

    /// <summary>
    /// A deed (D2-069): a significant act of people toward others - who did it, to whom, what and its worth to the one who
    /// suffered or gained it - kept while someone may still weigh it. Grievance and gratitude are read from the deeds a village
    /// knows (their worth, their age), never stored as scores.
    /// </summary>
    public struct Deed
    {
        public int Id;
        public byte Kind;
        public long Time;
        public int By, ByS;            // the doer (a person; 0: the village as a whole, as in relief) and their village
        public int ToHh, ToS;          // who suffered or gained it (household 0: the village's store) and their village
        public byte Good;              // Trade.Grain, Metal, Fibre, Hides
        public float Qty;              // units of the good that changed hands
        public float Worth;            // FU-equivalents to the one who suffered or gained it
        public long Event;             // its history event (0 none)

        public void Write(BinWriter w) { w.Int(Id); w.Byte(Kind); w.Long(Time); w.Int(By); w.Int(ByS); w.Int(ToHh); w.Int(ToS); w.Byte(Good); w.Float(Qty); w.Float(Worth); w.Long(Event); }
        public static Deed Read(BinReader r) => new Deed { Id = r.Int(), Kind = r.Byte(), Time = r.Long(), By = r.Int(), ByS = r.Int(), ToHh = r.Int(), ToS = r.Int(), Good = r.Byte(), Qty = r.Float(), Worth = r.Float(), Event = r.Long() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Kind); h.Add(Time); h.Add(By); h.Add(ByS); h.Add(ToHh); h.Add(ToS); h.Add(Good); h.Add(Qty); h.Add(Worth); h.Add(Event); }
    }

    /// <summary>A village's knowledge of a deed: which, how it learned it, when.</summary>
    public struct KnownDeed
    {
        public int Deed;
        public byte How;
        public long At;
    }

    /// <summary>
    /// What each village knows of deeds (D2-069; 10 §1, the primitive shared with F6's later slices): the deeds (`World.Deeds`)
    /// and per village a list of the ones it knows, sorted by id. Grievance and gratitude are sums over it, read when a choice
    /// needs them. A deed is forgotten everywhere once its weight has faded below half a day's food.
    /// </summary>
    public sealed class DeedBook
    {
        World _w;
        List<KnownDeed>[] _known = new List<KnownDeed>[0];
        readonly List<int> _drop = new List<int>();

        public void Bind(World w) { _w = w; }

        /// <summary>Half-lives (years) of a harm to goods and of help: how fast they weigh less.</summary>
        public const double HarmHalfLife = 2.0, HelpHalfLife = 4.0;
        /// <summary>A deed weighing less than this (FU) is forgotten.</summary>
        public const double ForgetBelow = 0.5;

        List<KnownDeed> Slot(int sid)
        {
            if (sid >= _known.Length) Array.Resize(ref _known, Math.Max(sid + 1, _known.Length * 2));
            return _known[sid] ?? (_known[sid] = new List<KnownDeed>());
        }

        static int Find(List<KnownDeed> list, int deed)
        {
            int lo = 0, hi = list.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1, d = list[mid].Deed;
                if (d == deed) return mid;
                if (d < deed) lo = mid + 1; else hi = mid - 1;
            }
            return ~lo;
        }

        /// <summary>Records a deed (and returns its id).</summary>
        public int Record(byte kind, int by, int byS, int toHh, int toS, byte good, double qty, double worth, long ev)
        {
            var w = _w;
            int id = w.Ids.Next(EntityKind.Deed);
            w.Deeds.Add(id, new Deed { Id = id, Kind = kind, Time = w.Now, By = by, ByS = byS, ToHh = toHh, ToS = toS, Good = good, Qty = (float)qty, Worth = (float)worth, Event = ev });
            return id;
        }

        /// <summary>A village comes to know a deed (what reveals the doer replaces a loss found).</summary>
        public void Learn(int sid, int deed, byte how)
        {
            if (sid <= 0 || deed == 0) return;
            var list = Slot(sid);
            int i = Find(list, deed);
            if (i >= 0)
            {
                var k = list[i];
                if (k.How == KnownHow.Found && how != KnownHow.Found) { k.How = how; k.At = _w.Now; list[i] = k; }
                return;
            }
            list.Insert(~i, new KnownDeed { Deed = deed, How = how, At = _w.Now });
        }

        public bool Knows(int sid, int deed, out byte how)
        {
            how = 0;
            if (sid <= 0 || sid >= _known.Length || _known[sid] == null) return false;
            int i = Find(_known[sid], deed);
            if (i < 0) return false;
            how = _known[sid][i].How;
            return true;
        }

        public int KnownCount(int sid) => sid > 0 && sid < _known.Length && _known[sid] != null ? _known[sid].Count : 0;

        /// <summary>What a deed weighs now (its worth, halved every half-life).</summary>
        public double Weight(in Deed d, long now)
        {
            double years = (double)(now - d.Time) / SimTime.Year;
            double half = DeedKind.Harm(d.Kind) ? HarmHalfLife : HelpHalfLife;
            return (double)d.Worth * DMath.Exp(-0.6931471805599453 * years / half);
        }

        /// <summary>What a village holds against another: the weight of the harms it knows that village's people did to it.</summary>
        public double Grievance(int sid, int other) => Sum(sid, other, true);

        /// <summary>What a village owes another in thanks: the weight of the help it knows that village gave it.</summary>
        public double Gratitude(int sid, int other) => Sum(sid, other, false);

        double Sum(int sid, int other, bool harm)
        {
            if (sid <= 0 || other <= 0 || sid >= _known.Length || _known[sid] == null) return 0;
            var list = _known[sid];
            var w = _w;
            long now = w.Now;
            double g = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var k = list[i];
                if (k.How == KnownHow.Found || k.How == KnownHow.Own) continue;   // (the doer unknown; its own people's deeds)
                ref var d = ref w.Deeds.Get(k.Deed);
                if (DeedKind.Harm(d.Kind) != harm || d.ToS != sid || d.ByS != other) continue;
                g += Weight(in d, now);
            }
            return g;
        }

        /// <summary>The help a village has given another since a time (for relief carried over days into one deed), or 0.</summary>
        public int HelpSince(int giver, int receiver, long since)
        {
            if (receiver <= 0 || receiver >= _known.Length || _known[receiver] == null) return 0;
            var list = _known[receiver];
            for (int i = list.Count - 1; i >= 0; i--)
            {
                ref var d = ref _w.Deeds.Get(list[i].Deed);
                if (d.Time < since) break;   // (ids rise with time)
                if (d.Kind == DeedKind.Relief && d.ByS == giver && d.ToS == receiver) return d.Id;
            }
            return 0;
        }

        /// <summary>Once a year: deeds whose weight has faded are forgotten by every village and dropped.</summary>
        public void Year()
        {
            var w = _w;
            long now = w.Now;
            _drop.Clear();
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id))
                if (Weight(in w.Deeds.Get(id), now) < ForgetBelow) _drop.Add(id);
            if (_drop.Count == 0) return;
            for (int s = 0; s < _known.Length; s++)
            {
                var list = _known[s];
                if (list == null) continue;
                int n = 0;
                for (int i = 0; i < list.Count; i++) if (_drop.BinarySearch(list[i].Deed) < 0) list[n++] = list[i];
                list.RemoveRange(n, list.Count - n);
            }
            for (int i = 0; i < _drop.Count; i++) w.Deeds.Remove(_drop[i]);
        }

        public void Write(BinWriter bw)
        {
            int n = 0;
            for (int s = 0; s < _known.Length; s++) if (_known[s] != null && _known[s].Count > 0) n++;
            bw.Int(n);
            for (int s = 0; s < _known.Length; s++)
            {
                var list = _known[s];
                if (list == null || list.Count == 0) continue;
                bw.Int(s); bw.Int(list.Count);
                for (int i = 0; i < list.Count; i++) { bw.Int(list[i].Deed); bw.Byte(list[i].How); bw.Long(list[i].At); }
            }
        }

        public void Read(BinReader r)
        {
            _known = new List<KnownDeed>[0];
            int n = r.Int();
            for (int j = 0; j < n; j++)
            {
                int s = r.Int(), c = r.Int();
                var list = Slot(s);
                for (int i = 0; i < c; i++) list.Add(new KnownDeed { Deed = r.Int(), How = r.Byte(), At = r.Long() });
            }
        }

        public void Hash(ref StateHasher h)
        {
            for (int s = 0; s < _known.Length; s++)
            {
                var list = _known[s];
                if (list == null || list.Count == 0) continue;
                h.Add(s); h.Add(list.Count);
                for (int i = 0; i < list.Count; i++) { h.Add(list[i].Deed); h.Add(list[i].How); h.Add(list[i].At); }
            }
        }
    }
}
