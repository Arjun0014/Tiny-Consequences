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
        public const byte Wound = 4;        // struck someone (Qty the hurt; D2-071)
        public const byte Kill = 5;         // someone died of the wounds they were given
        public const byte Amends = 6;       // a village made amends for a wrong (a help; Answer: the wrong it settles; D2-072)
        public const byte Refusal = 7;      // a village would not make amends when asked (a harm; Answer: the wrong claimed)
        public static bool Harm(byte k) => k == Take || k == TriedToTake || k == Wound || k == Kill || k == Refusal;
        /// <summary>Flags: the one struck was taking when struck; amends were asked for it; amends made it good.</summary>
        public const byte InTheAct = 1, Claimed = 2, Made = 4;   // (Made: amends made it good)
        public static bool Blood(byte k) => k == Wound || k == Kill;
    }

    /// <summary>How a village knows a deed.</summary>
    public static class KnownHow
    {
        public const byte Own = 1;     // its own people did it
        public const byte Saw = 2;     // one of its people saw it: the doer known by name and village
        public const byte Found = 3;   // it found the loss: the doer unknown
        public const byte Told = 4;    // someone told it: news carried by visitors and traders (D2-072)
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
        public int Answer;             // the deed that answered it: amends (settled) or a refusal (D2-072); for those, the wrong
        public byte Flags;             // DeedKind.InTheAct, Claimed

        public void Write(BinWriter w) { w.Int(Id); w.Byte(Kind); w.Long(Time); w.Int(By); w.Int(ByS); w.Int(ToHh); w.Int(ToS); w.Byte(Good); w.Float(Qty); w.Float(Worth); w.Long(Event); w.Int(Answer); w.Byte(Flags); }
        public static Deed Read(BinReader r) => new Deed { Id = r.Int(), Kind = r.Byte(), Time = r.Long(), By = r.Int(), ByS = r.Int(), ToHh = r.Int(), ToS = r.Int(), Good = r.Byte(), Qty = r.Float(), Worth = r.Float(), Event = r.Long(), Answer = r.Int(), Flags = r.Byte() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Kind); h.Add(Time); h.Add(By); h.Add(ByS); h.Add(ToHh); h.Add(ToS); h.Add(Good); h.Add(Qty); h.Add(Worth); h.Add(Event); h.Add(Answer); h.Add(Flags); }
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
    /// and per village a list of the ones it knows, sorted by id. Grievance and gratitude are the weight of the harms (help) a
    /// village knows another's people did (gave) it. They are kept as sums that age as the deeds do - one per pair of villages and
    /// kind of weight (goods, blood, help), brought to the present when they change - so a choice reads them at once (D2-072: a
    /// scan of every known deed for every choice cost a dense world a tenth of its day). A deed is forgotten everywhere once its
    /// weight has faded below half a day's food.
    /// </summary>
    public sealed class DeedBook
    {
        World _w;
        List<KnownDeed>[] _known = new List<KnownDeed>[0];
        List<int>[] _tell = new List<int>[0];     // per village: the deeds done to it that it saw (its news), ascending
        List<int>[] _beat = new List<int>[0];     // per village: the beatings of takers in the act it knows of, ascending
        List<int>[] _others = new List<int>[0];   // per village: the villages it keeps sums against, ascending
        readonly Dictionary<long, Weights> _sums = new Dictionary<long, Weights>();
        readonly Dictionary<long, int> _toldUpTo = new Dictionary<long, int>();   // (news told from a village to another so far: a cache, not state)
        readonly List<int> _drop = new List<int>();

        struct Weights { public long At; public double Goods, Blood, Help; }

        public void Bind(World w) { _w = w; }

        /// <summary>Half-lives (years) of a harm to goods, of help, and of blood: how fast they weigh less.</summary>
        public const double HarmHalfLife = 2.0, HelpHalfLife = 4.0, BloodHalfLife = 10.0;
        /// <summary>A deed weighing less than this (FU) is forgotten.</summary>
        public const double ForgetBelow = 0.5;
        const double Ln2 = 0.6931471805599453;

        static long Key(int sid, int other) => ((long)sid << 32) | (uint)other;

        List<KnownDeed> Slot(int sid)
        {
            if (sid >= _known.Length) Array.Resize(ref _known, Math.Max(sid + 1, _known.Length * 2));
            return _known[sid] ?? (_known[sid] = new List<KnownDeed>());
        }

        static List<int> Slot(ref List<int>[] a, int sid)
        {
            if (sid >= a.Length) Array.Resize(ref a, Math.Max(sid + 1, a.Length * 2));
            return a[sid] ?? (a[sid] = new List<int>());
        }

        static void InsertSorted(List<int> list, int id) { int i = list.BinarySearch(id); if (i < 0) list.Insert(~i, id); }

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

        /// <summary>Whether a known deed counts in a village's sums: done to it (or given it) by another village's people, the doer known, not made good.</summary>
        static bool Counts(int sid, in Deed d, byte how) =>
            (how == KnownHow.Saw || how == KnownHow.Told) && d.ToS == sid && d.ByS > 0 && d.ByS != sid && !(DeedKind.Harm(d.Kind) && (d.Flags & DeedKind.Made) != 0);

        void Age(ref Weights s, long now)
        {
            if (s.At == now) return;
            double y = (double)(now - s.At) / SimTime.Year;
            s.Goods *= DMath.Exp(-Ln2 * y / HarmHalfLife);
            s.Blood *= DMath.Exp(-Ln2 * y / BloodHalfLife);
            s.Help *= DMath.Exp(-Ln2 * y / HelpHalfLife);
            s.At = now;
        }

        static double Floor(double x) => x < 1e-9 ? 0.0 : x;

        void AddSum(int sid, in Deed d, double sign)
        {
            long now = _w.Now;
            long key = Key(sid, d.ByS);
            if (!_sums.TryGetValue(key, out var s)) { s = new Weights { At = now }; InsertSorted(Slot(ref _others, sid), d.ByS); }
            Age(ref s, now);
            double v = sign * Weight(in d, now);
            if (DeedKind.Blood(d.Kind)) s.Blood = Floor(s.Blood + v);
            else if (DeedKind.Harm(d.Kind)) s.Goods = Floor(s.Goods + v);
            else s.Help = Floor(s.Help + v);
            _sums[key] = s;
        }

        void Index(int sid, in Deed d, byte how)
        {
            if (how == KnownHow.Saw && d.ToS == sid) InsertSorted(Slot(ref _tell, sid), d.Id);
            if (how != KnownHow.Found && d.Kind == DeedKind.Wound && (d.Flags & DeedKind.InTheAct) != 0) InsertSorted(Slot(ref _beat, sid), d.Id);
        }

        /// <summary>A village comes to know a deed (what reveals the doer replaces a loss found).</summary>
        public void Learn(int sid, int deed, byte how)
        {
            if (sid <= 0 || deed == 0) return;
            var list = Slot(sid);
            int i = Find(list, deed);
            ref var d = ref _w.Deeds.Get(deed);
            if (i >= 0)
            {
                var k = list[i];
                if (k.How == KnownHow.Found && how != KnownHow.Found)
                {
                    k.How = how; k.At = _w.Now; list[i] = k;
                    if (Counts(sid, in d, how)) AddSum(sid, in d, 1.0);
                    Index(sid, in d, how);
                }
                return;
            }
            list.Insert(~i, new KnownDeed { Deed = deed, How = how, At = _w.Now });
            if (Counts(sid, in d, how)) AddSum(sid, in d, 1.0);
            Index(sid, in d, how);
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
            double half = DeedKind.Blood(d.Kind) ? BloodHalfLife : DeedKind.Harm(d.Kind) ? HarmHalfLife : HelpHalfLife;
            return (double)d.Worth * DMath.Exp(-Ln2 * years / half);
        }

        /// <summary>What a village holds against another: the weight of the harms it knows that village's people did to it.</summary>
        public double Grievance(int sid, int other)
        {
            if (!_sums.TryGetValue(Key(sid, other), out var s)) return 0;
            double y = (double)(_w.Now - s.At) / SimTime.Year;
            return s.Goods * DMath.Exp(-Ln2 * y / HarmHalfLife) + s.Blood * DMath.Exp(-Ln2 * y / BloodHalfLife);
        }

        /// <summary>What a village owes another in thanks: the weight of the help it knows that village gave it.</summary>
        public double Gratitude(int sid, int other)
        {
            if (!_sums.TryGetValue(Key(sid, other), out var s)) return 0;
            double y = (double)(_w.Now - s.At) / SimTime.Year;
            return s.Help * DMath.Exp(-Ln2 * y / HelpHalfLife);
        }

        /// <summary>Whether a wrong was made good (amends answered it).</summary>
        public static bool Settled(in Deed d) => (d.Flags & DeedKind.Made) != 0;

        /// <summary>A wrong answered (D2-072): amends make it good - it leaves the wronged village's sums -; a refusal leaves it standing.</summary>
        public void Answered(int deedId, int answerId)
        {
            var w = _w;
            ref var d = ref w.Deeds.Get(deedId);
            ref var a = ref w.Deeds.Get(answerId);
            if (a.Kind == DeedKind.Amends)
            {
                if (Knows(d.ToS, deedId, out byte how) && Counts(d.ToS, in d, how)) AddSum(d.ToS, in d, -1.0);
                d.Flags |= DeedKind.Made;
            }
            a.Answer = deedId;
            d.Answer = answerId;
        }

        /// <summary>More given under one deed (relief carried over days): the receiver's sums take the increase.</summary>
        public void AddWorth(int deedId, double qty, double worth)
        {
            ref var d = ref _w.Deeds.Get(deedId);
            bool counted = Knows(d.ToS, deedId, out byte how) && Counts(d.ToS, in d, how);
            if (counted) AddSum(d.ToS, in d, -1.0);
            d.Qty = (float)((double)d.Qty + qty);
            d.Worth = (float)((double)d.Worth + worth);
            if (counted) AddSum(d.ToS, in d, 1.0);
        }

        /// <summary>
        /// News (D2-072): when people of two villages meet - a visit, a trade, a claim - each tells what was done to it, the doer
        /// known (the wrongs it suffered and the help it was given: what it saw itself, never hearsay, never its own people's
        /// deeds). The other comes to know them as told. Each meeting tells only what is new since the last.
        /// </summary>
        public void Meet(int a, int b)
        {
            if (a == b || a <= 0 || b <= 0) return;
            Tell(a, b);
            Tell(b, a);
        }

        void Tell(int from, int to)
        {
            if (from >= _tell.Length || _tell[from] == null || _tell[from].Count == 0) return;
            var list = _tell[from];
            long key = Key(from, to);
            _toldUpTo.TryGetValue(key, out int upTo);
            int i = list.Count;
            while (i > 0 && list[i - 1] > upTo) i--;
            var w = _w;
            for (; i < list.Count; i++)
            {
                int id = list[i];
                if (w.Deeds.Get(id).ByS != to) Learn(to, id, KnownHow.Told);   // (not the hearer's own people's deeds)
            }
            _toldUpTo[key] = list[list.Count - 1];
        }

        /// <summary>
        /// What a village knows of another's answer to takers (D2-072: the reputation that keeps takers off): the hurt its people
        /// dealt to takers caught in the act, by age, as the village has seen or been told.
        /// </summary>
        public double Answers(int sid, int other)
        {
            if (sid <= 0 || other <= 0 || sid >= _beat.Length || _beat[sid] == null) return 0;
            var list = _beat[sid];
            var w = _w;
            long now = w.Now;
            double g = 0;
            for (int i = 0; i < list.Count; i++)
            {
                ref var d = ref w.Deeds.Get(list[i]);
                if (d.ByS != other) continue;
                double years = (double)(now - d.Time) / SimTime.Year;
                g += (double)d.Qty * DMath.Exp(-Ln2 * years / HarmHalfLife);
            }
            return g;
        }

        /// <summary>The village a village holds most against (and its weight), or 0 - read once a day for taking back (D2-072).</summary>
        public int Foe(int sid, out double worst)
        {
            worst = 0;
            if (sid <= 0 || sid >= _others.Length || _others[sid] == null) return 0;
            var w = _w;
            var list = _others[sid];
            int foe = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int o = list[i];
                if (!w.Settlements.Exists(o) || !w.Settlements.Get(o).Inhabited) continue;
                double g = Grievance(sid, o);
                if (g > worst) { worst = g; foe = o; }
            }
            return foe;
        }

        /// <summary>The event of the latest wrong (in fifty years) a village knows another's people did it, not made good, or 0.</summary>
        public long LatestHarm(int sid, int other)
        {
            if (sid <= 0 || sid >= _known.Length || _known[sid] == null) return 0;
            var list = _known[sid];
            var w = _w;
            long since = w.Now - 50 * SimTime.Year;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var k = list[i];
                ref var d = ref w.Deeds.Get(k.Deed);
                if (d.Time < since) break;   // (ids rise with time)
                if (k.How == KnownHow.Found || k.How == KnownHow.Own) continue;
                if (DeedKind.Harm(d.Kind) && d.ToS == sid && d.ByS == other && !Settled(in d)) return d.Event;
            }
            return 0;
        }

        /// <summary>A village's own list of what it knows (for claims; read-only use).</summary>
        public List<KnownDeed> KnownBy(int sid) => sid > 0 && sid < _known.Length ? _known[sid] : null;

        /// <summary>
        /// The weight of the harms a village knows were done to it since a time - by whomever, the doer known or not (losses found
        /// count): what a watch would guard against (D2-071). The latest such deed's event comes back as the cause.
        /// </summary>
        public double HarmSince(int sid, long since, out long latest)
        {
            latest = 0;
            if (sid <= 0 || sid >= _known.Length || _known[sid] == null) return 0;
            var list = _known[sid];
            var w = _w;
            long now = w.Now, at = long.MinValue;
            double g = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var k = list[i];
                ref var d = ref w.Deeds.Get(k.Deed);
                if (d.Time < since) break;   // (ids rise with time)
                if (k.How == KnownHow.Own || !DeedKind.Harm(d.Kind) || d.ToS != sid) continue;
                g += Weight(in d, now);
                if (d.Time > at) { at = d.Time; latest = d.Event; }
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

        /// <summary>Once a year: deeds whose weight has faded are forgotten by every village (and leave its sums) and dropped.</summary>
        public void Year()
        {
            var w = _w;
            long now = w.Now;
            _drop.Clear();
            for (int id = w.Deeds.First(); id != 0; id = w.Deeds.Next(id))
                if (Weight(in w.Deeds.Get(id), now) < ForgetBelow) _drop.Add(id);
            if (_drop.Count == 0) return;
            for (int i = 0; i < _drop.Count; i++)
            {
                ref var d = ref w.Deeds.Get(_drop[i]);
                if (d.ToS > 0 && Knows(d.ToS, d.Id, out byte how) && Counts(d.ToS, in d, how)) AddSum(d.ToS, in d, -1.0);
            }
            for (int s = 0; s < _known.Length; s++)
            {
                var list = _known[s];
                if (list == null) continue;
                int n = 0;
                for (int i = 0; i < list.Count; i++) if (_drop.BinarySearch(list[i].Deed) < 0) list[n++] = list[i];
                list.RemoveRange(n, list.Count - n);
            }
            Prune(_tell);
            Prune(_beat);
            for (int i = 0; i < _drop.Count; i++) w.Deeds.Remove(_drop[i]);
        }

        void Prune(List<int>[] lists)
        {
            for (int s = 0; s < lists.Length; s++)
            {
                var list = lists[s];
                if (list == null) continue;
                int n = 0;
                for (int i = 0; i < list.Count; i++) if (_drop.BinarySearch(list[i]) < 0) list[n++] = list[i];
                list.RemoveRange(n, list.Count - n);
            }
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
            // the sums (state: they carry their own rounding; rebuilt they would differ in the last bits)
            int m = 0;
            for (int s = 0; s < _others.Length; s++) if (_others[s] != null) m += _others[s].Count;
            bw.Int(m);
            for (int s = 0; s < _others.Length; s++)
            {
                var list = _others[s];
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var x = _sums[Key(s, list[i])];
                    bw.Int(s); bw.Int(list[i]); bw.Long(x.At); bw.Double(x.Goods); bw.Double(x.Blood); bw.Double(x.Help);
                }
            }
        }

        public void Read(BinReader r)
        {
            _known = new List<KnownDeed>[0];
            _tell = new List<int>[0]; _beat = new List<int>[0]; _others = new List<int>[0];
            _sums.Clear(); _toldUpTo.Clear();
            int n = r.Int();
            for (int j = 0; j < n; j++)
            {
                int s = r.Int(), c = r.Int();
                var list = Slot(s);
                for (int i = 0; i < c; i++) list.Add(new KnownDeed { Deed = r.Int(), How = r.Byte(), At = r.Long() });
            }
            int m = r.Int();
            for (int j = 0; j < m; j++)
            {
                int s = r.Int(), o = r.Int();
                var x = new Weights { At = r.Long(), Goods = r.Double(), Blood = r.Double(), Help = r.Double() };
                _sums[Key(s, o)] = x;
                Slot(ref _others, s).Add(o);
            }
            // the indexes, from what each village knows
            var w = _w;
            for (int s = 0; s < _known.Length; s++)
            {
                var list = _known[s];
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++) Index(s, in w.Deeds.Get(list[i].Deed), list[i].How);
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
            for (int s = 0; s < _others.Length; s++)
            {
                var list = _others[s];
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var x = _sums[Key(s, list[i])];
                    h.Add(s); h.Add(list[i]); h.Add(x.At); h.Add(x.Goods); h.Add(x.Blood); h.Add(x.Help);
                }
            }
        }
    }
}
