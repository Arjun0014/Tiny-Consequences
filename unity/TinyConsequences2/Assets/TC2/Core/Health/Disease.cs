using System.Collections.Generic;
using TC2.Core.Agents;
using TC2.Core.Foundation;
using TC2.Core.History;
using TC2.Core.Places;
using TC2.Core.Simulation;

namespace TC2.Core.Health
{
    /// <summary>A pathogen: one lineage of a sickness with its own traits (D2-066).</summary>
    public struct Pathogen
    {
        public int Id;
        public int Parent;           // the lineage it mutated from (0: it came over from animals)
        public int Antigen;          // what immunity recognises: its lineage's, until a shift gives it a new one
        public int Family;           // the sickness it is a strain of (the lineage that came over from the animals: D2-070)
        public int Origin;           // the village where it appeared
        public float Beta;           // infecting contacts a day of an infectious person
        public float Latent, Infectious;   // days carried without sign; days infectious (and abed)
        public float Virulence;      // the chance it kills a grown body in good health
        public float Immunity;       // years recovery protects (99: for life)
        public long Born;
        public long Event;           // its appearance in history
        public int Cases, Deaths;

        public void Write(BinWriter w) { w.Int(Id); w.Int(Parent); w.Int(Antigen); w.Int(Family); w.Int(Origin); w.Float(Beta); w.Float(Latent); w.Float(Infectious); w.Float(Virulence); w.Float(Immunity); w.Long(Born); w.Long(Event); w.Int(Cases); w.Int(Deaths); }
        public static Pathogen Read(BinReader r) => new Pathogen { Id = r.Int(), Parent = r.Int(), Antigen = r.Int(), Family = r.Int(), Origin = r.Int(), Beta = r.Float(), Latent = r.Float(), Infectious = r.Float(), Virulence = r.Float(), Immunity = r.Float(), Born = r.Long(), Event = r.Long(), Cases = r.Int(), Deaths = r.Int() };
        public void Hash(ref StateHasher h) { h.Add(Id); h.Add(Parent); h.Add(Antigen); h.Add(Family); h.Add(Origin); h.Add(Beta); h.Add(Latent); h.Add(Infectious); h.Add(Virulence); h.Add(Immunity); h.Add(Born); h.Add(Event); h.Add(Cases); h.Add(Deaths); }
    }

    /// <summary>
    /// Sickness among persistent people (D2-066; 02 §disease: metapopulation SEIR with travel coupling, pathogens as lineages that
    /// mutate). Each person carries their own infection - latent, then infectious and abed, then dead or recovered and immune
    /// to its antigen for its years - so a sickness goes wherever its carriers walk: a visitor to a stricken village brings it
    /// home, a bride or a household moving carries it. Each day in each village the infectious among those at home infect the
    /// susceptible at their pathogen's rate (frequency-dependent: beta x I / N); a COARSE day couples villages at the lived rate
    /// of visits. New pathogens come over from animals (more where more are kept and more people crowd) and mutate as they spread
    /// (rarely shifting their antigen past old immunity). Small villages burn an epidemic out: crowd sicknesses come and go in a
    /// world of a few thousand. Each village's epidemic is caused by the epidemic its first carrier caught it from: "why" leads
    /// back to the animals.
    /// </summary>
    public sealed class Disease
    {
        World _w;
        public void Bind(World w) { _w = w; }

        public const byte Latent = 1, Infectious = 2;
        /// <summary>Chance a village's day brings a sickness over from its animals, before its herds (up to 3x) and its crowding (up
        /// to 2x): D2-070 - unbounded, a dense world of thirty villages met a new sickness every few years.</summary>
        public const double SpillPerDay = 6e-6;
        /// <summary>Chance a new infection mutates the strain; a fiftieth of mutations shift its antigen (D2-070: a tenth shifted
        /// several times in one great epidemic).</summary>
        public const double MutatePerCase = 5e-4, ShiftShare = 0.02;
        /// <summary>Immunity to one strain of a sickness keeps off another strain of it this often (an antigen shifted, not new: D2-070).</summary>
        public const double CrossProtection = 0.6;
        /// <summary>The immunities a person remembers (D2-070: two were forgotten as new antigens came).</summary>
        public const int Slots = 6;
        /// <summary>Visits a healthy grown person makes a day to a neighbouring village (measured in lived days: 0.032 over five test worlds,
        /// D2-067 - D2-066's 0.027 was one small sample), and their hours.</summary>
        public const double VisitsPerAdultDay = 0.03, VisitHours = 3.0;
        /// <summary>Tools (`--visittally`, C9.5c survey): a COARSE day's visits between neighbours within a day's walk - [0] visits along a way on foot,
        /// [1] across water (no way on foot); [2] caught on visits along a way, [3] across water; news met [4] along a way, [5] across water; [6] ways worn
        /// to a path or a road between villages no way on foot joins (history told it); null in the game.</summary>
        public static double[] VisitTally;
        public const int VisitTallyN = 7;
        /// <summary>`--oldvisits`: a COARSE day's visits go to every village within a day's walk, across water too (before C9.5c) - on the same binary.</summary>
        public static bool OldVisits;

        /// <summary>
        /// A neighbour a COARSE day's visitors go to (C9.5c, D2-166): within a day's walk and joined by a way on foot, as a lived visitor walks one. Across
        /// water villages meet only as their boats' crews do - on voyages, with relief, grain given back or the tenth, at both resolutions.
        /// </summary>
        public static bool Visited(World w, int sid, int o, double d) => d <= 6000 && (OldVisits || w.Routes.Get(w, sid, o) != null);
        public const double ForLife = 99.0;

        readonly List<int> _present = new List<int>();
        readonly List<int> _sick = new List<int>();
        readonly Dictionary<int, int> _infectious = new Dictionary<int, int>();
        readonly List<int> _paths = new List<int>();

        // ------------------------------------------------------------------ a person's sickness
        static int SlotAntigen(in Person p, int i) => i == 0 ? p.Imm0 : i == 1 ? p.Imm1 : i == 2 ? p.Imm2 : i == 3 ? p.Imm3 : i == 4 ? p.Imm4 : p.Imm5;
        static long SlotUntil(in Person p, int i) => i == 0 ? p.ImmUntil0 : i == 1 ? p.ImmUntil1 : i == 2 ? p.ImmUntil2 : i == 3 ? p.ImmUntil3 : i == 4 ? p.ImmUntil4 : p.ImmUntil5;

        static void SetSlot(ref Person p, int i, int antigen, long until)
        {
            switch (i)
            {
                case 0: p.Imm0 = antigen; p.ImmUntil0 = until; break;
                case 1: p.Imm1 = antigen; p.ImmUntil1 = until; break;
                case 2: p.Imm2 = antigen; p.ImmUntil2 = until; break;
                case 3: p.Imm3 = antigen; p.ImmUntil3 = until; break;
                case 4: p.Imm4 = antigen; p.ImmUntil4 = until; break;
                default: p.Imm5 = antigen; p.ImmUntil5 = until; break;
            }
        }

        public static bool ImmuneTo(in Person p, int antigen, long now)
        {
            for (int i = 0; i < Slots; i++) if (SlotAntigen(in p, i) == antigen && SlotUntil(in p, i) > now) return true;
            return false;
        }

        /// <summary>Whether a person is immune to some strain of a sickness (another antigen of the same family: D2-070).</summary>
        public bool Kindred(in Person p, int family, long now)
        {
            var w = _w;
            for (int i = 0; i < Slots; i++)
            {
                int a = SlotAntigen(in p, i);
                if (a == 0 || SlotUntil(in p, i) <= now || !w.Pathogens.Exists(a)) continue;
                if (w.Pathogens.Get(a).Family == family) return true;
            }
            return false;
        }

        /// <summary>A recovered body remembers an antigen: its own slot refreshed, else a free or spent one, else the one ending soonest.</summary>
        public static void Remember(ref Person p, int antigen, long until, long now)
        {
            int pick = 0; long key = long.MaxValue;
            for (int i = 0; i < Slots; i++)
            {
                int a = SlotAntigen(in p, i); long u = SlotUntil(in p, i);
                if (a == antigen) { if (until > u) SetSlot(ref p, i, antigen, until); return; }
                long k = a == 0 || u <= now ? long.MinValue : u;
                if (k < key) { key = k; pick = i; }
            }
            SetSlot(ref p, pick, antigen, until);
        }

        /// <summary>A person catches a pathogen: latent from now (cause: the epidemic they caught it in).</summary>
        public void Infect(ref Person p, int path, long cause)
        {
            var w = _w;
            if (!p.Alive || p.Sick != 0 || !w.Pathogens.Exists(path)) return;
            ref var g = ref w.Pathogens.Get(path);
            if (ImmuneTo(in p, g.Antigen, w.Now)) return;
            // a strain of a sickness they had: their immunity holds in part against its new antigen (D2-070)
            if (Kindred(in p, g.Family, w.Now) && KeyedRng.Unit(w.Seed, RngStream.Disease, (ulong)p.Id, (ulong)w.Now, (ulong)(14 + 31 * path)) < CrossProtection) return;
            // now and then the strain changes in the new host (D2-066: lineages mutate as they spread)
            double u = KeyedRng.Unit(w.Seed, RngStream.Disease, (ulong)p.Id, (ulong)w.Now, 11);
            if (u < MutatePerCase) path = Mutate(path, p.Home);
            ref var g2 = ref w.Pathogens.Get(path);
            p.Sick = path; p.SickPhase = Latent; p.SickUntil = w.Now + SimTime.Days(g2.Latent); p.SickCause = cause;
            g2.Cases++;
            w.Metrics.Infections++;
        }

        /// <summary>The fraction of an infectious person's death chance a body's age and health give.</summary>
        static double Frailty(double age, double health) =>
            (age < 2 ? 3.0 : age < 5 ? 2.0 : age < 15 ? 0.6 : age > 65 ? 2.5 : age > 50 ? 1.4 : 1.0) * (1.0 + 2.0 * (1.0 - health));

        // ------------------------------------------------------------------ a village's day
        public void Day(int sid, int days = 1)
        {
            var w = _w;
            long now = w.Now;
            ulong seed = w.Seed;
            ref var s0 = ref w.Settlements.Get(sid);
            var res = w.Residents(sid);
            _present.Clear(); _sick.Clear(); _infectious.Clear(); _paths.Clear();
            for (int i = 0; i < res.Count; i++)
            {
                ref var p = ref w.People.Get(res[i]);
                if (!p.Alive) continue;
                if (p.At == sid) _present.Add(res[i]);
                if (p.Sick != 0) _sick.Add(res[i]);
            }
            // the course of each sickness: latent -> infectious (abed) -> dead or recovered and immune
            for (int i = 0; i < _sick.Count; i++)
            {
                ref var p = ref w.People.Get(_sick[i]);
                if (!p.Alive || p.Sick == 0 || now < p.SickUntil) continue;
                ref var g = ref w.Pathogens.Get(p.Sick);
                if (p.SickPhase == Latent)
                {
                    p.SickPhase = Infectious; p.SickUntil = now + SimTime.Days(g.Infectious);
                    // a carrier come home: the sickness has come to the village with them (history, its cause where they caught it)
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.Epidemic == 0 && p.Home == sid) Arrived(sid, p.Sick, p.Id, p.SickCause);
                    continue;
                }
                double age = p.AgeYears(now);
                double die = DMath.Min(0.95, g.Virulence * Frailty(age, p.Health));
                if (KeyedRng.Unit(seed, RngStream.Disease, (ulong)p.Id, (ulong)now, 12) < die)
                {
                    g.Deaths++;
                    ref var s = ref w.Settlements.Get(sid);
                    if (s.EpiPath == p.Sick) s.EpiDead++;
                    long cause = p.SickCause;
                    p.Sick = 0; p.SickPhase = 0;
                    w.Agents.Kill(ref p, cause, 10);
                    w.Metrics.DiseaseDeaths++;
                    continue;
                }
                // recovered: immune to its antigen for its years
                long until = g.Immunity >= ForLife ? long.MaxValue / 4 : now + SimTime.Years(g.Immunity);
                Remember(ref p, g.Antigen, until, now);
                p.Sick = 0; p.SickPhase = 0;
            }
            // the infectious at home, by pathogen
            int n = _present.Count;
            for (int i = 0; i < _present.Count; i++)
            {
                ref var p = ref w.People.Get(_present[i]);
                if (p.Alive && p.SickPhase == Infectious)
                {
                    if (!_infectious.ContainsKey(p.Sick)) { _infectious[p.Sick] = 0; _paths.Add(p.Sick); }
                    _infectious[p.Sick]++;
                }
            }
            _paths.Sort();
            // each susceptible at home: caught at the rate the infectious meet them (beta x I / N a day)
            if (n > 0)
                for (int k = 0; k < _paths.Count; k++)
                {
                    int path = _paths[k];
                    ref var g = ref w.Pathogens.Get(path);
                    double lambda = (double)g.Beta * _infectious[path] / n;
                    double chance = 1.0 - DMath.Exp(-lambda);
                    long cause = CauseIn(sid, path);
                    for (int i = 0; i < _present.Count; i++)
                    {
                        ref var q = ref w.People.Get(_present[i]);
                        if (!q.Alive || q.Sick != 0 || ImmuneTo(in q, g.Antigen, now)) continue;
                        if (KeyedRng.Unit(seed, RngStream.Disease, (ulong)q.Id, (ulong)now, (ulong)(100 + path)) < chance) Infect(ref q, path, cause);
                    }
                }
            // COARSE: the visits a lived day would make to stricken neighbours, and the sickness they bring home (D2-066)
            if (w.Resolution == Resolution.Coarse) Visits(sid, days);
            // a sickness comes over from the animals now and then: more where more are kept and more crowd together
            Spill(sid, n, days);
            // the village's tally: what is infectious here today (neighbours' COARSE visits read it); an epidemic that has
            // left nobody sick is over
            ref var s2 = ref w.Settlements.Get(sid);
            int inf = 0, main = 0, sickNow = 0;
            for (int k = 0; k < _paths.Count; k++) if (_infectious[_paths[k]] > inf) { inf = _infectious[_paths[k]]; main = _paths[k]; }
            for (int i = 0; i < res.Count; i++) { ref var q = ref w.People.Get(res[i]); if (q.Alive && q.Sick != 0 && q.Sick == s2.EpiPath) sickNow++; }
            s2.InfNow = inf; s2.InfPath = main; s2.InfPresent = n;
            if (s2.Epidemic != 0 && sickNow == 0) Ended(sid);
        }

        /// <summary>The epidemic event infections of a pathogen in a village are caused by (its arrival there, else its appearance).</summary>
        long CauseIn(int sid, int path)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            if (s.Epidemic != 0 && s.EpiPath == path) return s.Epidemic;
            return w.Pathogens.Exists(path) ? w.Pathogens.Get(path).Event : 0;
        }

        void Arrived(int sid, int path, int carrier, long cause)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            // (where it came over from the animals it breaks out; elsewhere it comes with a carrier)
            bool source = w.Pathogens.Exists(path) && w.Pathogens.Get(path).Origin == sid && cause == w.Pathogens.Get(path).Event;
            s.Epidemic = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.EpidemicArrived, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Pathogen, path), Other = EntityRef.Person(carrier), Cause1 = cause, Rel1 = CauseRel.CausedBy,
                RootKind = cause == 0 ? RootKind.Nature : RootKind.Unknown, ArgA = source ? 1 : 0,
            });
            s.EpiPath = path; s.EpiDead = 0; s.EpiStart = w.Now;
            w.Metrics.Epidemics++;
        }

        void Ended(int sid)
        {
            var w = _w;
            ref var s = ref w.Settlements.Get(sid);
            w.Record(new HistoryEvent
            {
                Kind = HistoryKind.EpidemicEnded, Time = w.Now, Cell = s.Cell, Settlement = sid, Subject = EntityRef.Settlement(sid),
                Object = new EntityRef(EntityKind.Pathogen, s.EpiPath), Cause1 = s.Epidemic, Rel1 = CauseRel.PartOf, Magnitude = s.EpiDead,
                ArgA = (int)((w.Now - s.EpiStart) / SimTime.Day),
            });
            s.Epidemic = 0; s.EpiPath = 0; s.EpiDead = 0;
        }

        /// <summary>
        /// A visitor arrives at a village for a stay (ACTIVITY): in the hours they share with its people, they may catch what
        /// its infectious have - and carry it home (D2-066). The sick keep to their beds and do not visit.
        /// </summary>
        public void Arrive(ref Person p, int sid, long stay)
        {
            var w = _w;
            if (!p.Alive || p.Sick != 0 || stay <= 0) return;
            ref var s = ref w.Settlements.Get(sid);
            if (s.InfNow <= 0 || s.InfPath == 0 || s.InfPresent <= 0 || !w.Pathogens.Exists(s.InfPath)) return;
            ref var g = ref w.Pathogens.Get(s.InfPath);
            double lambda = g.Beta * (stay / (double)SimTime.Day) * s.InfNow / s.InfPresent;
            if (KeyedRng.Unit(w.Seed, RngStream.Disease, (ulong)p.Id, (ulong)w.Now, 13) < 1.0 - DMath.Exp(-lambda)) Infect(ref p, s.InfPath, CauseIn(sid, s.InfPath));
        }

        /// <summary>COARSE: the village's healthy grown people visit stricken neighbours at the lived rate and some bring the sickness home.</summary>
        void Visits(int sid, int days)
        {
            var w = _w;
            long now = w.Now;
            ref var s = ref w.Settlements.Get(sid);
            int adults = 0;
            for (int i = 0; i < _present.Count; i++)
            {
                ref var p = ref w.People.Get(_present[i]);
                double age = p.AgeYears(now);
                if (p.Alive && p.Sick == 0 && age >= 16 && age < 62 && p.Health > 0.7) adults++;
            }
            if (adults == 0) return;
            double total = 0;
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius);
                if (Visited(w, sid, o, d)) total += 1.0 / (300.0 + d);   // (along a way on foot: C9.5c)
            }
            if (total <= 0) return;
            double visits = adults * VisitsPerAdultDay * days;   // (DEEP: the step's visits)
            // every visit is a walk there and back: it wears the way to each neighbour, by how near it is (C4.1)
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius);
                if (!Visited(w, sid, o, d)) continue;
                w.Ways.Walk(w, sid, o, 2.0 * visits * (1.0 / (300.0 + d)) / total);
                if (VisitTally != null) VisitTally[w.Routes.Get(w, sid, o) != null ? 0 : 1] += visits * (1.0 / (300.0 + d)) / total;   // (tools: C9.5c survey)
            }
            for (int o = w.Settlements.First(); o != 0; o = w.Settlements.Next(o))
            {
                if (o == sid) continue;
                ref var t = ref w.Settlements.Get(o);
                if (!t.Inhabited || t.InfNow <= 0 || t.InfPath == 0 || t.InfPresent <= 0 || !w.Pathogens.Exists(t.InfPath)) continue;
                double d = Vec3d.ArcDistance(s.Anchor, t.Anchor, w.Planet.Radius);
                if (!Visited(w, sid, o, d)) continue;
                ref var g = ref w.Pathogens.Get(t.InfPath);
                double perVisit = 1.0 - DMath.Exp(-g.Beta * VisitHours / 24.0 * t.InfNow / t.InfPresent);
                double expect = visits * (1.0 / (300.0 + d)) / total * perVisit;
                int caught = Poisson(expect, KeyedRng.Hash(w.Seed, RngStream.Disease, (ulong)sid, (ulong)now, (ulong)o));
                // who caught it: healthy grown people of the village, by a keyed pick
                for (int c = 0; c < caught; c++)
                {
                    int pick = (int)KeyedRng.Below(KeyedRng.Hash(w.Seed, RngStream.Disease, (ulong)sid, (ulong)now, (ulong)(o * 31 + c)), _present.Count);
                    ref var q = ref w.People.Get(_present[pick]);
                    double age = q.AgeYears(now);
                    if (!q.Alive || q.Sick != 0 || age < 16 || age >= 62) continue;
                    Infect(ref q, t.InfPath, CauseIn(o, t.InfPath));
                    if (VisitTally != null) VisitTally[w.Routes.Get(w, sid, o) != null ? 2 : 3] += 1;   // (tools: C9.5c survey)
                }
            }
        }

        static int Poisson(double mean, ulong key)
        {
            if (mean <= 0) return 0;
            var rs = new RandomSequence(key);
            double l = DMath.Exp(-DMath.Min(mean, 30.0)), p = 1.0;
            int k = 0;
            do { k++; p *= rs.NextUnit(); } while (p > l && k < 60);
            return k - 1;
        }

        /// <summary>A village's chance a day that a sickness comes over from its animals: more with herds and crowding, both bounded (D2-070).</summary>
        public static double SpillChance(int beasts, int present) => SpillPerDay * DMath.Min(3.0, 1.0 + beasts / 40.0) * DMath.Min(2.0, present / 120.0);

        /// <summary>A sickness comes over from the village's animals (D2-066): a new lineage with traits of its own, one of its people its first host.</summary>
        void Spill(int sid, int present, int days)
        {
            var w = _w;
            if (present < 8) return;
            long now = w.Now;
            int beasts = w.Stock.CountOf(sid, Livestock.Cattle) + w.Stock.CountOf(sid, Livestock.Goat) + w.Stock.CountOf(sid, Livestock.Pig);
            double chance = SpillChance(beasts, present);
            if (KeyedRng.Unit(w.Seed, RngStream.Disease, (ulong)sid, (ulong)now, 1) >= Places.SettlementSystem.Over(chance, days)) return;
            int id = w.Ids.Next(EntityKind.Pathogen);
            var rs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.Disease, (ulong)id, 77));
            // traits from the ranges of the sicknesses of farming peoples: a crowd fever, a pox, a cough, a flux
            double beta = 0.15 * DMath.Exp(rs.NextUnit() * DMath.Log(10.0));          // 0.15-1.5 infecting contacts a day
            double latent = 2.0 + 10.0 * rs.NextUnit();                               // 2-12 days
            double virulence = 0.002 * DMath.Exp(rs.NextUnit() * DMath.Log(200.0));   // 0.002-0.4
            double infectious = (4.0 + 12.0 * rs.NextUnit()) * (1.0 - 0.5 * virulence); // the deadlier, the shorter (the trade-off)
            double immunity = rs.NextUnit() < 0.5 ? ForLife : 2.0 + 40.0 * rs.NextUnit();
            int first = _present[(int)KeyedRng.Below(KeyedRng.Hash(w.Seed, RngStream.Disease, (ulong)id, 78), _present.Count)];
            Appear(id, sid, first, beta, latent, infectious, virulence, immunity, beasts > 0);
        }

        /// <summary>
        /// A new pathogen appears in a village (from its animals, or a premise's outbreak): its lineage, its name, its history, its
        /// first host latent from now, and the outbreak there (D2-066).
        /// </summary>
        void Appear(int id, int sid, int firstHost, double beta, double latent, double infectious, double virulence, double immunity, bool animals)
        {
            var w = _w;
            long now = w.Now;
            var g = new Pathogen
            {
                Id = id, Parent = 0, Antigen = id, Family = id, Origin = sid, Beta = (float)beta, Latent = (float)latent, Infectious = (float)infectious,
                Virulence = (float)virulence, Immunity = (float)immunity, Born = now,
            };
            w.Pathogens.Add(id, g);
            w.History.Register(new EntityRef(EntityKind.Pathogen, id), UniqueName(id, in g, sid), now);
            ref var s = ref w.Settlements.Get(sid);
            ref var g2 = ref w.Pathogens.Get(id);
            g2.Event = w.Record(new HistoryEvent
            {
                Kind = HistoryKind.PathogenAppeared, Time = now, Cell = s.Cell, Settlement = sid, Subject = new EntityRef(EntityKind.Pathogen, id),
                Object = EntityRef.Settlement(sid), ArgA = animals ? 1 : 0, Magnitude = (float)virulence, RootKind = RootKind.Nature,
            });
            w.Metrics.PathogensBorn++;
            ref var p = ref w.People.Get(firstHost);
            Infect(ref p, id, g2.Event);
            ref var s2 = ref w.Settlements.Get(sid);
            if (s2.Epidemic == 0) Arrived(sid, p.Sick != 0 ? p.Sick : id, firstHost, g2.Event);
        }

        /// <summary>
        /// Premise (tests, tools, the god's sickness to come): a new sickness of the given traits breaks out in a village, one of its
        /// people at home its first host. Returns the pathogen.
        /// </summary>
        public int Outbreak(int sid, double beta, double latent, double infectious, double virulence, double immunity)
        {
            var w = _w;
            var res = w.Residents(sid);
            int first = 0;
            for (int i = 0; i < res.Count && first == 0; i++) { ref var q = ref w.People.Get(res[i]); if (q.Alive && q.At == sid && q.Sick == 0) first = res[i]; }
            if (first == 0) return 0;
            int id = w.Ids.Next(EntityKind.Pathogen);
            Appear(id, sid, first, beta, latent, infectious, virulence, immunity, false);
            return id;
        }

        /// <summary>A name not already given to another sickness (else its village of origin follows it).</summary>
        string UniqueName(int id, in Pathogen g, int sid)
        {
            var w = _w;
            string name = Name(id, in g);
            for (int o = w.Pathogens.First(); o != 0; o = w.Pathogens.Next(o))
                if (o != id && w.Pathogens.Get(o).Parent == 0 && w.History.NameOf(new EntityRef(EntityKind.Pathogen, o)) == name)
                    return name + " of " + w.History.NameOf(EntityRef.Settlement(sid));
            return name;
        }

        /// <summary>A strain mutates in a new host: its traits drift; a tenth of the time its antigen shifts past old immunity.</summary>
        /// <summary>Premise (tests, tools): a new strain of a pathogen now, its antigen shifted or not.</summary>
        public int Strain(int path, int sid, bool shift) => Mutate(path, sid, shift ? 1 : 0);

        int Mutate(int path, int sid, int force = -1)
        {
            var w = _w;
            ref var g = ref w.Pathogens.Get(path);
            int id = w.Ids.Next(EntityKind.Pathogen);
            var rs = new RandomSequence(KeyedRng.Hash(w.Seed, RngStream.Disease, (ulong)id, 79));
            double beta = g.Beta * DMath.Exp(0.3 * (rs.NextUnit() - 0.5));
            double vir = DMath.Clamp(g.Virulence * DMath.Exp(0.4 * (rs.NextUnit() - 0.5)), 0.001, 0.6);
            bool shift = rs.NextUnit() < ShiftShare;
            if (force >= 0) shift = force == 1;
            var child = new Pathogen
            {
                Id = id, Parent = path, Antigen = shift ? id : g.Antigen, Family = g.Family, Origin = sid, Beta = (float)beta, Latent = g.Latent,
                Infectious = (float)DMath.Max(2.0, g.Infectious * (1.0 - 0.5 * (vir - g.Virulence))), Virulence = (float)vir, Immunity = g.Immunity,
                Born = w.Now, Event = g.Event,
            };
            w.Pathogens.Add(id, child);
            // (a shifted strain is named for its sickness and the year it turned: "the black spots (the new kind of year 364)")
            string name = shift ? w.History.NameOf(new EntityRef(EntityKind.Pathogen, child.Family)) + " (the new kind of year " + (w.Now / SimTime.Year).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"
                                : w.History.NameOf(new EntityRef(EntityKind.Pathogen, path));
            w.History.Register(new EntityRef(EntityKind.Pathogen, id), name, w.Now);
            w.Metrics.Mutations++;
            return id;
        }

        static readonly string[] Adjectives = { "red", "white", "spotted", "shaking", "black", "summer", "winter", "cattle", "marsh", "sweating", "children's", "wasting" };
        static readonly string[] Nouns = { "fever", "pox", "cough", "flux", "ague", "sickness", "spots", "sweat" };

        /// <summary>A name the people give it (keyed by its id; a cough when it spreads fast and short, a pox when it lingers).</summary>
        static string Name(int id, in Pathogen g)
        {
            ulong k = KeyedRng.Mix((ulong)id * 0x9E3779B97F4A7C15UL ^ 0xD15EA5E);
            string noun = g.Infectious > 11 ? (k % 2 == 0 ? "pox" : "spots") : g.Latent < 4 && g.Beta > 0.6 ? (k % 2 == 0 ? "cough" : "sweat") : Nouns[(int)(k % (ulong)Nouns.Length)];
            string adj = Adjectives[(int)((k >> 16) % (ulong)Adjectives.Length)];
            return "the " + adj + " " + noun;
        }
    }
}
